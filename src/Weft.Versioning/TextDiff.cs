using System.Text;

namespace Weft.Versioning;

/// <summary>Operation of a diff segment.</summary>
public enum DiffOp
{
    /// <summary>Text unchanged between the two versions.</summary>
    Equal,

    /// <summary>Text present only in the new version.</summary>
    Inserted,

    /// <summary>Text present only in the old version.</summary>
    Deleted,
}

/// <summary>A contiguous segment of a text diff.</summary>
public readonly record struct TextDiffSegment(DiffOp Op, string Text);

/// <summary>
/// Word-level text diff (research R9): LCS over word/space tokens, deterministic
/// (same inputs → same segments). v1 scope: plain text per field.
/// </summary>
public sealed record TextDiff(IReadOnlyList<TextDiffSegment> Segments)
{
    /// <summary>Indicates whether there is at least one inserted or deleted segment.</summary>
    public bool HasChanges => Segments.Any(s => s.Op != DiffOp.Equal);

    /// <summary>Computes the word-level diff between <paramref name="oldText"/> and <paramref name="newText"/>.</summary>
    public static TextDiff Compute(string oldText, string newText)
    {
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);

        string[] a = Tokenize(oldText);
        string[] b = Tokenize(newText);
        int[,] lcs = BuildLcsTable(a, b);

        var segments = new List<TextDiffSegment>();
        // Reconstruction from the end of the LCS table back to the start.
        int i = a.Length, j = b.Length;
        var rev = new List<TextDiffSegment>();
        while (i > 0 || j > 0)
        {
            if (i > 0 && j > 0 && a[i - 1] == b[j - 1])
            {
                rev.Add(new TextDiffSegment(DiffOp.Equal, a[i - 1]));
                i--;
                j--;
            }
            else if (j > 0 && (i == 0 || lcs[i, j - 1] >= lcs[i - 1, j]))
            {
                rev.Add(new TextDiffSegment(DiffOp.Inserted, b[j - 1]));
                j--;
            }
            else
            {
                rev.Add(new TextDiffSegment(DiffOp.Deleted, a[i - 1]));
                i--;
            }
        }
        rev.Reverse();

        // Merge contiguous tokens of the same operation into a single readable segment.
        foreach (TextDiffSegment token in rev)
        {
            if (segments.Count > 0 && segments[^1].Op == token.Op)
            {
                segments[^1] = segments[^1] with { Text = segments[^1].Text + token.Text };
            }
            else
            {
                segments.Add(token);
            }
        }
        return new TextDiff(segments);
    }

    /// <summary>Tokenizes into word and space units (each run of whitespace or non-whitespace).</summary>
    private static string[] Tokenize(string text)
    {
        if (text.Length == 0)
        {
            return [];
        }
        var tokens = new List<string>();
        var sb = new StringBuilder();
        bool currentIsSpace = char.IsWhiteSpace(text[0]);
        foreach (char c in text)
        {
            bool isSpace = char.IsWhiteSpace(c);
            if (isSpace != currentIsSpace)
            {
                tokens.Add(sb.ToString());
                sb.Clear();
                currentIsSpace = isSpace;
            }
            sb.Append(c);
        }
        tokens.Add(sb.ToString());
        return [.. tokens];
    }

    private static int[,] BuildLcsTable(string[] a, string[] b)
    {
        var lcs = new int[a.Length + 1, b.Length + 1];
        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                lcs[i, j] = a[i - 1] == b[j - 1]
                    ? lcs[i - 1, j - 1] + 1
                    : Math.Max(lcs[i - 1, j], lcs[i, j - 1]);
            }
        }
        return lcs;
    }
}
