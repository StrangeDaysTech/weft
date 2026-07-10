using CsCheck;
using Weft;
using Weft.Yrs;

namespace Weft.Core.Tests;

/// <summary>
/// Property-based (T019, SC-001): secuencias aleatorias de ediciones concurrentes sobre N réplicas
/// que, tras intercambiar deltas, convergen a un estado byte-idéntico (mismo VersionId cross-réplica).
/// </summary>
public sealed class ConvergenceTests
{
    private static readonly ICrdtEngine Engine = YrsEngine.Instance;

    // Una edición: a qué réplica (0/1/2) va, y qué texto corto inserta al inicio del campo.
    private static readonly Gen<(int replica, string text)> GenEdit =
        Gen.Select(Gen.Int[0, 2], Gen.String[Gen.Char['a', 'z'], 1, 6], (r, s) => (r, s));

    [Fact]
    public void Concurrent_edits_converge_byte_identical()
    {
        GenEdit.List[0, 60].Sample(edits =>
        {
            ICrdtDoc[] replicas = [Engine.CreateDoc(), Engine.CreateDoc(), Engine.CreateDoc()];
            try
            {
                // Ediciones concurrentes: cada réplica edita su copia sin sincronizar aún.
                foreach ((int replica, string text) in edits)
                {
                    replicas[replica].InsertText("body", 0, text);
                }

                // Sincronización todos-contra-todos por deltas incrementales (state-vector → since).
                // Dos pasadas garantizan que los cambios propagados en la 1ª lleguen a todos.
                for (int pass = 0; pass < 2; pass++)
                {
                    foreach (ICrdtDoc target in replicas)
                    {
                        byte[] sv = target.ExportStateVector();
                        foreach (ICrdtDoc source in replicas)
                        {
                            if (!ReferenceEquals(source, target))
                            {
                                target.ApplyUpdate(source.ExportUpdateSince(sv));
                            }
                        }
                    }
                }

                // Convergencia byte-idéntica: todas exportan el mismo estado (mismo VersionId).
                byte[] reference = replicas[0].ExportState();
                return replicas.All(r => r.ExportState().AsSpan().SequenceEqual(reference));
            }
            finally
            {
                foreach (ICrdtDoc r in replicas)
                {
                    r.Dispose();
                }
            }
        });
    }
}
