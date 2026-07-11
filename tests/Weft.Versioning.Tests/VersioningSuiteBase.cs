using Weft;
using Weft.Versioning.Blobs;

namespace Weft.Versioning.Tests;

/// <summary>
/// Suite parametrizada de versionado (T027): las 7 postcondiciones de contracts/versioning-api.md.
/// Es abstracta; cada subclase concreta fija el motor (P-IV: la misma suite corre idéntica sobre
/// YrsEngine Y LoroEngine — postcondición 6, por herencia).
/// </summary>
public abstract class VersioningSuiteBase
{
    protected abstract ICrdtEngine Engine { get; }

    private (VersionStore store, InMemoryBlobStore blobs) NewStore()
    {
        var blobs = new InMemoryBlobStore();
        return (new VersionStore(Engine, blobs), blobs);
    }

    /// <summary>Sincroniza dos réplicas por deltas incrementales hasta converger.</summary>
    private static void SyncBidirectional(ICrdtDoc a, ICrdtDoc b)
    {
        byte[] svA = a.ExportStateVector();
        byte[] svB = b.ExportStateVector();
        a.ApplyUpdate(b.ExportUpdateSince(svA));
        b.ApplyUpdate(a.ExportUpdateSince(svB));
    }

    // Postcondición 1: Publish x2 sin cambios → mismo VersionId, un solo blob (dedup).
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

    // Postcondición 2: Checkout(Publish(doc)) → ExportState byte-idéntico al blob publicado.
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

    // Postcondición 3: réplicas convergidas publican el MISMO VersionId (SC-002).
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

    // Postcondición 4: Diff(a,a) sin cambios; Diff(a,b) refleja las ediciones.
    [Fact]
    public async Task Diff_reflects_edits()
    {
        (VersionStore store, _) = NewStore();
        using ICrdtDoc doc = Engine.CreateDoc();
        doc.InsertText("body", 0, "el gato duerme");
        VersionId v1 = await store.PublishAsync(doc);

        doc.DeleteText("body", 3, 4);          // borra "gato"
        doc.InsertText("body", 3, "perro");    // inserta "perro"
        VersionId v2 = await store.PublishAsync(doc);

        Assert.False((await store.DiffAsync(v1, v1, "body")).HasChanges);
        TextDiff diff = await store.DiffAsync(v1, v2, "body");
        Assert.True(diff.HasChanges);
        Assert.Contains(diff.Segments, s => s.Op == DiffOp.Deleted && s.Text.Contains("gato"));
        Assert.Contains(diff.Segments, s => s.Op == DiffOp.Inserted && s.Text.Contains("perro"));
    }

    // Postcondición 5: merge de ramas concurrentes conmutativo (mismo resultado, cualquier orden).
    [Fact]
    public async Task Merge_is_commutative()
    {
        (VersionStore store, _) = NewStore();
        using ICrdtDoc baseDoc = Engine.CreateDoc();
        baseDoc.InsertText("body", 0, "base ");
        VersionId baseVersion = await store.PublishAsync(baseDoc);

        // Dos ramas independientes desde la base, con ediciones concurrentes.
        using ICrdtDoc branch1 = await store.BranchAsync(baseVersion);
        using ICrdtDoc branch2 = await store.BranchAsync(baseVersion);
        branch1.InsertText("body", 0, "uno ");
        branch2.InsertText("body", 0, "dos ");

        // Orden A: target = branch1, merge branch2. Orden B: target = branch2, merge branch1.
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

    // Postcondición 7: compactación por construcción (FR-012). Muchas versiones con ciclos
    // insert+delete → todas recuperables byte-idéntico y tamaño acotado (GC del motor activo).
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
            // Cancela parte del contenido para generar historial que el GC puede recuperar.
            if (i % 2 == 1)
            {
                doc.DeleteText("body", 0, 4);
            }
            VersionId id = await store.PublishAsync(doc);
            ids.Add(id);
            snapshots.Add(doc.ExportState());
        }

        // (a) Todas las versiones recuperables byte-idéntico por su hash.
        for (int i = 0; i < ids.Count; i++)
        {
            using ICrdtDoc restored = await store.CheckoutAsync(ids[i]);
            Assert.Equal(snapshots[i], restored.ExportState());
        }

        // (b) Dedup: no más blobs que versiones distintas publicadas.
        Assert.True(blobs.Count <= ids.Count);

        // (c) El blob final no crece de forma monótona con la longitud del historial:
        //     su tamaño se mantiene modesto pese a 25 ciclos de edición (GC activo, no tombstones).
        Assert.True(snapshots[^1].Length < 4096,
            $"El blob final ({snapshots[^1].Length} B) sugiere acumulación de historial (¿GC desactivado?).");
    }
}
