using Weft;
using Weft.Yrs;

namespace Weft.Core.Tests;

/// <summary>
/// Unit tests del binding yrs (T018): round-trip byte-idéntico, mapeo de errores tipificados,
/// semántica de dispose y buffers vacíos.
/// </summary>
public sealed class YrsDocTests
{
    private static readonly ICrdtEngine Engine = YrsEngine.Instance;

    [Fact]
    public void Engine_identity()
    {
        Assert.Equal("yrs", Engine.Name);
        Assert.Null(Engine.NativeVersioning);
    }

    [Fact]
    public void Insert_then_read_returns_content()
    {
        using ICrdtDoc doc = Engine.CreateDoc();
        doc.InsertText("body", 0, "Hola mundo");
        Assert.Equal("Hola mundo", doc.GetText("body"));
    }

    [Fact]
    public void Delete_removes_range()
    {
        using ICrdtDoc doc = Engine.CreateDoc();
        doc.InsertText("body", 0, "Hola mundo");
        doc.DeleteText("body", 4, 6); // borra " mundo"
        Assert.Equal("Hola", doc.GetText("body"));
    }

    [Fact]
    public void Roundtrip_export_load_is_byte_identical()
    {
        using ICrdtDoc doc = Engine.CreateDoc();
        doc.InsertText("body", 0, "contenido áéí");
        byte[] blob = doc.ExportState();

        using ICrdtDoc reloaded = Engine.LoadDoc(blob);
        Assert.Equal(blob, reloaded.ExportState());      // byte-idéntico (P-III)
        Assert.Equal("contenido áéí", reloaded.GetText("body"));
    }

    [Fact]
    public void Incremental_since_produces_delta_that_converges()
    {
        using ICrdtDoc a = Engine.CreateDoc();
        using ICrdtDoc b = Engine.CreateDoc();
        a.InsertText("t", 0, "abc");

        byte[] delta = a.ExportUpdateSince(b.ExportStateVector());
        b.ApplyUpdate(delta);
        Assert.Equal("abc", b.GetText("t"));
        Assert.Equal(a.ExportState(), b.ExportState());
    }

    [Fact]
    public void Empty_field_read_returns_empty_string()
    {
        using ICrdtDoc doc = Engine.CreateDoc();
        Assert.Equal(string.Empty, doc.GetText("inexistente"));
    }

    [Fact]
    public void Corrupt_blob_load_throws_corrupt_update()
    {
        byte[] garbage = [1, 2, 3, 4, 5, 6, 7, 8];
        Assert.Throws<CorruptUpdateException>(() => Engine.LoadDoc(garbage));
    }

    [Fact]
    public void Corrupt_apply_throws_corrupt_update()
    {
        using ICrdtDoc doc = Engine.CreateDoc();
        byte[] garbage = [9, 8, 7, 6, 5, 4, 3, 2, 1];
        Assert.Throws<CorruptUpdateException>(() => doc.ApplyUpdate(garbage));
    }

    [Fact]
    public void Out_of_range_index_throws_argument_out_of_range()
    {
        using ICrdtDoc doc = Engine.CreateDoc();
        // Índice válido para C# (>= 0) pero fuera de rango en el motor → OUT_OF_BOUNDS.
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.InsertText("body", 5, "x"));
    }

    [Fact]
    public void Negative_index_throws_before_crossing_boundary()
    {
        using ICrdtDoc doc = Engine.CreateDoc();
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.InsertText("body", -1, "x"));
    }

    [Fact]
    public void Empty_field_name_throws_argument()
    {
        using ICrdtDoc doc = Engine.CreateDoc();
        Assert.Throws<ArgumentException>(() => doc.InsertText("", 0, "x"));
    }

    [Fact]
    public void Null_field_name_throws_argument_null()
    {
        using ICrdtDoc doc = Engine.CreateDoc();
        Assert.Throws<ArgumentNullException>(() => doc.InsertText(null!, 0, "x"));
    }

    [Fact]
    public void Use_after_dispose_throws_object_disposed()
    {
        ICrdtDoc doc = Engine.CreateDoc();
        doc.InsertText("t", 0, "x");
        doc.Dispose();
        Assert.Throws<ObjectDisposedException>(() => doc.GetText("t"));
    }
}
