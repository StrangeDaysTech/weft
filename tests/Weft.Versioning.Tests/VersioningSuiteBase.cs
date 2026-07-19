using Weft;
using Weft.Versioning.Blobs;

namespace Weft.Versioning.Tests;

/// <summary>
/// Parameterized versioning suite (T027): the 7 postconditions of contracts/versioning-api.md.
/// It is abstract; each concrete subclass fixes the engine (P-IV: the same suite runs identically over
/// YrsEngine AND LoroEngine — postcondition 6, by inheritance).
/// </summary>
public abstract class VersioningSuiteBase
{
    protected abstract ICrdtEngine Engine { get; }

    private (VersionStore store, InMemoryBlobStore blobs) NewStore()
    {
        var blobs = new InMemoryBlobStore();
        return (new VersionStore(Engine, blobs), blobs);
    }

    /// <summary>Synchronizes two replicas via incremental deltas until they converge.</summary>
    private static void SyncBidirectional(ICrdtDoc a, ICrdtDoc b)
    {
        byte[] svA = a.ExportStateVector();
        byte[] svB = b.ExportStateVector();
        a.ApplyUpdate(b.ExportUpdateSince(svA));
        b.ApplyUpdate(a.ExportUpdateSince(svB));
    }

    // Postcondition 1: Publish x2 with no changes → same VersionId, a single blob (dedup).
    [Fact]
    public async Task Publish_twice_same_content_dedups()
    {
        (VersionStore store, InMemoryBlobStore blobs) = NewStore();
        using ICrdtDoc doc = Engine.CreateDoc();
        doc.InsertText("body", 0, "contenido estable");

        VersionId id1 = await store.PublishAsync(doc);
        VersionId id2 = await store.PublishAsync(doc);

        Assert.Equal(id1, id2);
        Assert.Equal(1, blobs.Count);
    }

    // Postcondition 2: Checkout(Publish(doc)) → ExportState byte-identical to the published blob.
    [Fact]
    public async Task Checkout_roundtrip_is_byte_identical()
    {
        (VersionStore store, _) = NewStore();
        using ICrdtDoc doc = Engine.CreateDoc();
        doc.InsertText("body", 0, "hola áéí mundo");
        byte[] published = doc.ExportState();

        VersionId id = await store.PublishAsync(doc);
        using ICrdtDoc restored = await store.CheckoutAsync(id);

        Assert.Equal(published, restored.ExportState());
        Assert.Equal("hola áéí mundo", restored.GetText("body"));
    }

    // Postcondition 3: converged replicas publish the SAME VersionId (SC-002).
    [Fact]
    public async Task Converged_replicas_publish_same_version_id()
    {
        (VersionStore store, _) = NewStore();
        using ICrdtDoc a = Engine.CreateDoc();
        using ICrdtDoc b = Engine.CreateDoc();
        a.InsertText("body", 0, "izquierda ");
        b.InsertText("body", 0, "derecha ");

        SyncBidirectional(a, b);

        VersionId ida = await store.PublishAsync(a);
        VersionId idb = await store.PublishAsync(b);
        Assert.Equal(ida, idb);
    }

    // Postcondition 4: Diff(a,a) with no changes; Diff(a,b) reflects the edits.
    [Fact]
    public async Task Diff_reflects_edits()
    {
        (VersionStore store, _) = NewStore();
        using ICrdtDoc doc = Engine.CreateDoc();
        doc.InsertText("body", 0, "el gato duerme");
        VersionId v1 = await store.PublishAsync(doc);

        doc.DeleteText("body", 3, 4);          // deletes "gato"
        doc.InsertText("body", 3, "perro");    // inserts "perro"
        VersionId v2 = await store.PublishAsync(doc);

        Assert.False((await store.DiffAsync(v1, v1, "body")).HasChanges);
        TextDiff diff = await store.DiffAsync(v1, v2, "body");
        Assert.True(diff.HasChanges);
        Assert.Contains(diff.Segments, s => s.Op == DiffOp.Deleted && s.Text.Contains("gato"));
        Assert.Contains(diff.Segments, s => s.Op == DiffOp.Inserted && s.Text.Contains("perro"));
    }

    // Postcondition 5: commutative merge of concurrent branches (same result, any order).
    [Fact]
    public async Task Merge_is_commutative()
    {
        (VersionStore store, _) = NewStore();
        using ICrdtDoc baseDoc = Engine.CreateDoc();
        baseDoc.InsertText("body", 0, "base ");
        VersionId baseVersion = await store.PublishAsync(baseDoc);

        // Two independent branches from the base, with concurrent edits.
        using ICrdtDoc branch1 = await store.BranchAsync(baseVersion);
        using ICrdtDoc branch2 = await store.BranchAsync(baseVersion);
        branch1.InsertText("body", 0, "uno ");
        branch2.InsertText("body", 0, "dos ");

        // Order A: target = branch1, merge branch2. Order B: target = branch2, merge branch1.
        using ICrdtDoc ordA = await store.BranchAsync(baseVersion);
        ordA.ApplyUpdate(branch1.ExportState());
        store.Merge(ordA, branch2);

        using ICrdtDoc ordB = await store.BranchAsync(baseVersion);
        ordB.ApplyUpdate(branch2.ExportState());
        store.Merge(ordB, branch1);

        Assert.Equal(ordA.ExportState(), ordB.ExportState());
        VersionId idA = await store.PublishAsync(ordA);
        VersionId idB = await store.PublishAsync(ordB);
        Assert.Equal(idA, idB);
    }

    // Postcondition 7: compaction by construction (FR-012). Many versions with insert+delete
    // cycles → all recoverable byte-identical and bounded in size (engine GC active).
    [Fact]
    public async Task Compaction_versions_recoverable_and_bounded()
    {
        (VersionStore store, InMemoryBlobStore blobs) = NewStore();
        using ICrdtDoc doc = Engine.CreateDoc();

        var ids = new List<VersionId>();
        var snapshots = new List<byte[]>();
        for (int i = 0; i < 25; i++)
        {
            doc.InsertText("body", 0, $"edición-{i} ");
            // Cancel part of the content to generate history that the GC can reclaim.
            if (i % 2 == 1)
            {
                doc.DeleteText("body", 0, 4);
            }
            VersionId id = await store.PublishAsync(doc);
            ids.Add(id);
            snapshots.Add(doc.ExportState());
        }

        // (a) All versions recoverable byte-identical by their hash.
        for (int i = 0; i < ids.Count; i++)
        {
            using ICrdtDoc restored = await store.CheckoutAsync(ids[i]);
            Assert.Equal(snapshots[i], restored.ExportState());
        }

        // (b) Dedup: no more blobs than distinct published versions.
        Assert.True(blobs.Count <= ids.Count);

        // (c) The final blob does not grow monotonically with the length of the history:
        //     its size stays modest despite 25 edit cycles (GC active, not tombstones).
        Assert.True(snapshots[^1].Length < 4096,
            $"El blob final ({snapshots[^1].Length} B) sugiere acumulación de historial (¿GC desactivado?).");
    }
}
