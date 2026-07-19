using Weft;
using Weft.Loro;
using Weft.Versioning.Blobs;
using Weft.Yrs;

namespace Weft.Versioning.Tests;

/// <summary>
/// Engine compatibility guard (FU-008 / audit G4): merging documents from different engines
/// (yrs↔Loro) must fail with a clear <see cref="ArgumentException"/> BEFORE crossing the FFI, not with
/// an opaque <c>CorruptUpdateException</c> from the native decoder.
/// </summary>
public sealed class CrossEngineMergeGuardTests
{
    [Fact]
    public void Merge_across_engines_throws_ArgumentException()
    {
        var store = new VersionStore(YrsEngine.Instance, new InMemoryBlobStore());
        using ICrdtDoc yrs = YrsEngine.Instance.CreateDoc();
        using ICrdtDoc loro = LoroEngine.Instance.CreateDoc();
        yrs.InsertText("body", 0, "destino yrs");
        loro.InsertText("body", 0, "rama loro");

        ArgumentException ex = Assert.Throws<ArgumentException>(() => store.Merge(yrs, loro));
        Assert.Contains("yrs", ex.Message);
        Assert.Contains("loro", ex.Message);
    }

    [Fact]
    public async Task MergeAsync_into_foreign_engine_target_throws_ArgumentException()
    {
        // Store over yrs; we publish a valid yrs version...
        var blobs = new InMemoryBlobStore();
        var store = new VersionStore(YrsEngine.Instance, blobs);
        using ICrdtDoc yrs = YrsEngine.Instance.CreateDoc();
        yrs.InsertText("body", 0, "versión yrs");
        VersionId version = await store.PublishAsync(yrs);

        // ...but the target is a Loro document: it must be rejected as an incompatible engine.
        using ICrdtDoc loroTarget = LoroEngine.Instance.CreateDoc();
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await store.MergeAsync(loroTarget, version));
    }

    [Fact]
    public void Merge_same_engine_still_works()
    {
        var store = new VersionStore(YrsEngine.Instance, new InMemoryBlobStore());
        using ICrdtDoc target = YrsEngine.Instance.CreateDoc();
        using ICrdtDoc branch = YrsEngine.Instance.CreateDoc();
        target.InsertText("body", 0, "a ");
        branch.InsertText("body", 0, "b ");

        store.Merge(target, branch); // must not throw

        Assert.Contains("b", target.GetText("body"));
    }
}
