using Weft;
using Weft.Loro;
using Weft.Versioning.Blobs;
using Weft.Yrs;

namespace Weft.Versioning.Tests;

/// <summary>
/// Guard de compatibilidad de motor (FU-008 / auditoría G4): mezclar documentos de motores distintos
/// (yrs↔Loro) debe fallar con un <see cref="ArgumentException"/> claro ANTES de cruzar el FFI, no con
/// una <c>CorruptUpdateException</c> opaca del decoder nativo.
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
        // Almacén sobre yrs; publicamos una versión yrs válida...
        var blobs = new InMemoryBlobStore();
        var store = new VersionStore(YrsEngine.Instance, blobs);
        using ICrdtDoc yrs = YrsEngine.Instance.CreateDoc();
        yrs.InsertText("body", 0, "versión yrs");
        VersionId version = await store.PublishAsync(yrs);

        // ...pero el destino es un documento Loro: debe rechazarse por motor incompatible.
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

        store.Merge(target, branch); // no debe lanzar

        Assert.Contains("b", target.GetText("body"));
    }
}
