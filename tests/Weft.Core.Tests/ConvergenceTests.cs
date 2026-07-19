using CsCheck;
using Weft;
using Weft.Yrs;

namespace Weft.Core.Tests;

/// <summary>
/// Property-based (T019, SC-001): random sequences of concurrent edits over N replicas
/// that, after exchanging deltas, converge to a byte-identical state (same VersionId across replicas).
/// </summary>
public sealed class ConvergenceTests
{
    private static readonly ICrdtEngine Engine = YrsEngine.Instance;

    // One edit: which replica (0/1/2) it goes to, and what short text it inserts at the start of the field.
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
                // Concurrent edits: each replica edits its copy without syncing yet.
                foreach ((int replica, string text) in edits)
                {
                    replicas[replica].InsertText("body", 0, text);
                }

                // All-against-all synchronization via incremental deltas (state-vector → since).
                // Two passes guarantee that changes propagated in the 1st reach everyone.
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

                // Byte-identical convergence: all export the same state (same VersionId).
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
