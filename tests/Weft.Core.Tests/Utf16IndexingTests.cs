using Weft;
using Weft.Yrs;

namespace Weft.Core.Tests;

/// <summary>
/// Regression: insert/delete indices are UTF-16 code units (consistent with .NET string
/// and with Yjs), not UTF-8 bytes (the yrs default). A latent CHARTER-01 bug on non-ASCII
/// text, fixed with OffsetKind::Utf16 in the shim.
/// </summary>
public sealed class Utf16IndexingTests
{
    [Fact]
    public void Delete_spans_correct_utf16_units_with_accents()
    {
        using ICrdtDoc doc = YrsEngine.Instance.CreateDoc();
        doc.InsertText("f", 0, "El veloz murciélago");
        Assert.Equal(19, doc.GetText("f").Length);      // UTF-16 code units
        doc.DeleteText("f", 9, 10);                      // deletes "murciélago"
        Assert.Equal("El veloz ", doc.GetText("f"));
        doc.InsertText("f", 9, "colibrí");
        Assert.Equal("El veloz colibrí", doc.GetText("f"));
    }

    [Fact]
    public void Insert_at_index_past_accent()
    {
        using ICrdtDoc doc = YrsEngine.Instance.CreateDoc();
        doc.InsertText("f", 0, "café");
        Assert.Equal(4, doc.GetText("f").Length);
        doc.InsertText("f", 4, " con leche");
        Assert.Equal("café con leche", doc.GetText("f"));
    }
}
