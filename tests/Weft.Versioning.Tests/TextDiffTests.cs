namespace Weft.Versioning.Tests;

/// <summary>Unit tests for the word-level LCS diff (T028): operations, determinism, edge cases.</summary>
public sealed class TextDiffTests
{
    [Fact]
    public void No_changes_when_identical()
    {
        TextDiff diff = TextDiff.Compute("el gato duerme", "el gato duerme");
        Assert.False(diff.HasChanges);
        Assert.All(diff.Segments, s => Assert.Equal(DiffOp.Equal, s.Op));
    }

    [Fact]
    public void Empty_fields()
    {
        Assert.False(TextDiff.Compute("", "").HasChanges);
        Assert.True(TextDiff.Compute("", "hola").HasChanges);
        Assert.True(TextDiff.Compute("hola", "").HasChanges);
    }

    [Fact]
    public void Insert_and_delete_words()
    {
        TextDiff diff = TextDiff.Compute("el gato duerme", "el perro duerme");
        Assert.True(diff.HasChanges);
        Assert.Contains(diff.Segments, s => s.Op == DiffOp.Deleted && s.Text.Contains("gato"));
        Assert.Contains(diff.Segments, s => s.Op == DiffOp.Inserted && s.Text.Contains("perro"));
        // "el " and " duerme" remain unchanged.
        Assert.Contains(diff.Segments, s => s.Op == DiffOp.Equal && s.Text.Contains("el"));
        Assert.Contains(diff.Segments, s => s.Op == DiffOp.Equal && s.Text.Contains("duerme"));
    }

    [Fact]
    public void Reconstructs_new_text_from_equal_plus_inserted()
    {
        TextDiff diff = TextDiff.Compute("uno dos", "uno tres dos");
        string rebuilt = string.Concat(
            diff.Segments.Where(s => s.Op != DiffOp.Deleted).Select(s => s.Text));
        Assert.Equal("uno tres dos", rebuilt);
    }

    [Fact]
    public void Is_deterministic()
    {
        TextDiff d1 = TextDiff.Compute("the quick brown fox", "the slow brown fox jumps");
        TextDiff d2 = TextDiff.Compute("the quick brown fox", "the slow brown fox jumps");
        Assert.Equal(
            d1.Segments.Select(s => (s.Op, s.Text)),
            d2.Segments.Select(s => (s.Op, s.Text)));
    }
}
