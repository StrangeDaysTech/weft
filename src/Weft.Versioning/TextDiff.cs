using System.Text;

namespace Weft.Versioning;

/// <summary>Operación de un segmento de diff.</summary>
public enum DiffOp
{
    /// <summary>Texto sin cambios entre ambas versiones.</summary>
    Equal,

    /// <summary>Texto presente solo en la versión nueva.</summary>
    Inserted,

    /// <summary>Texto presente solo en la versión antigua.</summary>
    Deleted,
}

/// <summary>Un segmento contiguo de un diff de texto.</summary>
public readonly record struct TextDiffSegment(DiffOp Op, string Text);

/// <summary>
/// Diff de texto por palabras (research R9): LCS sobre tokens palabra/espacio, determinista
/// (mismas entradas → mismos segmentos). Alcance v1: texto plano por campo.
/// </summary>
public sealed record TextDiff(IReadOnlyList<TextDiffSegment> Segments)
{
    /// <summary>Indica si hay al menos un segmento insertado o borrado.</summary>
    public bool HasChanges => Segments.Any(s => s.Op != DiffOp.Equal);

    /// <summary>Computa el diff por palabras entre <paramref name="oldText"/> y <paramref name="newText"/>.</summary>
    public static TextDiff Compute(string oldText, string newText)
    {
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);

        string[] a = Tokenize(oldText);
        string[] b = Tokenize(newText);
        int[,] lcs = BuildLcsTable(a, b);

        var segments = new List<TextDiffSegment>();
        // Reconstrucción desde el final de la tabla LCS hacia el inicio.
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

        // Fusiona tokens contiguos de la misma operación en un solo segmento legible.
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

    /// <summary>Tokeniza en unidades palabra y espacio (cada run de whitespace o no-whitespace).</summary>
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
