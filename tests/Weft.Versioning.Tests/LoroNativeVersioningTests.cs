using System.Text.Json;
using Weft;
using Weft.Loro;
using Weft.Yrs;

namespace Weft.Versioning.Tests;

/// <summary>
/// Superficie opcional <see cref="INativeVersioning"/> de Loro (CHARTER-10/FU-006, hallazgo G1):
/// LoroEngine expone probes DEMOSTRATIVOS del versionado nativo (diff/fork/shallow snapshot). No son
/// content-addressing (no deterministas, no alimentan VersionId); estos tests asertan reachability,
/// round-trip, convergencia del merge nativo, y que los probes NO mutan el doc del caller.
/// </summary>
public sealed class LoroNativeVersioningTests
{
    private static INativeVersioning Native =>
        LoroEngine.Instance.NativeVersioning
        ?? throw new Xunit.Sdk.XunitException("LoroEngine.NativeVersioning no debe ser null (G1 cerrado).");

    [Fact]
    public void Yrs_engine_has_no_native_versioning()
    {
        // Contraste: yrs no tiene versionado nativo → la capacidad opcional es null (permanente).
        Assert.Null(YrsEngine.Instance.NativeVersioning);
    }

    [Fact]
    public void ShallowSnapshot_is_nonempty_and_reloadable()
    {
        using ICrdtDoc doc = LoroEngine.Instance.CreateDoc();
        doc.InsertText("body", 0, "contenido áéí 🦀");

        byte[] snapshot = Native.ShallowSnapshot(doc);
        Assert.NotEmpty(snapshot);

        using ICrdtDoc reloaded = LoroEngine.Instance.LoadDoc(snapshot);
        Assert.Equal("contenido áéí 🦀", reloaded.GetText("body"));
    }

    [Fact]
    public void NativeDiffProbe_reflects_edits_as_json()
    {
        using ICrdtDoc doc = LoroEngine.Instance.CreateDoc();
        doc.InsertText("body", 0, "hola mundo");

        using JsonDocument json = JsonDocument.Parse(Native.NativeDiffProbe(doc, "body"));
        JsonElement root = json.RootElement;
        Assert.Equal("body", root.GetProperty("field").GetString());
        Assert.True(root.GetProperty("containers_changed").GetInt32() >= 1, "tras editar debe haber ≥1 container en el diff");
        Assert.Equal(10, root.GetProperty("text_len_utf16").GetInt32());
    }

    [Fact]
    public void NativeBranchMergeProbe_converges_and_does_not_mutate_caller()
    {
        using ICrdtDoc doc = LoroEngine.Instance.CreateDoc();
        doc.InsertText("body", 0, "base");

        using JsonDocument json = JsonDocument.Parse(Native.NativeBranchMergeProbe(doc, "body"));
        Assert.True(json.RootElement.GetProperty("converged").GetBoolean(), "el merge nativo debe converger");

        // El probe forkea aparte: el doc del caller no cambia.
        Assert.Equal("base", doc.GetText("body"));
    }

    [Fact]
    public void Probes_reject_a_non_loro_doc()
    {
        using ICrdtDoc yrsDoc = YrsEngine.Instance.CreateDoc();
        Assert.Throws<ArgumentException>(() => Native.ShallowSnapshot(yrsDoc));
        Assert.Throws<ArgumentException>(() => Native.NativeDiffProbe(yrsDoc, "body"));
        Assert.Throws<ArgumentException>(() => Native.NativeBranchMergeProbe(yrsDoc, "body"));
    }
}
