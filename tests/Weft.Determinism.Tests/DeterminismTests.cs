using System.Security.Cryptography;
using System.Text.Json;
using Weft;
using Weft.Loro;
using Weft.Versioning;
using Weft.Versioning.Blobs;
using Weft.Yrs;

namespace Weft.Determinism.Tests;

/// <summary>
/// Encoding determinism gate (T029, constitution P-III): a version's identity is
/// reproducible. Foundation for the cross-RID job — absolute cross-implementation determinism vs Yjs JS
/// (same hash on ALL RIDs) is completed in T058 (US4) with the shared corpus; here we fix
/// the reproducible encoding properties that job compares.
/// </summary>
public sealed class DeterminismTests
{
    private static readonly ICrdtEngine Engine = YrsEngine.Instance;

    // Deterministic corpus: a fixed sequence of edits spread across 3 replicas.
    private static readonly (int replica, string op, string text, int index, int len)[] Corpus =
    [
        (0, "ins", "El veloz ", 0, 0),
        (1, "ins", "murciélago ", 0, 0),
        (2, "ins", "hindú ", 0, 0),
        (0, "ins", "comía ", 9, 0),
        (1, "ins", "feliz ", 0, 0),
        (2, "del", "", 0, 3),
    ];

    private static void ApplyCorpus(ICrdtDoc[] replicas)
    {
        foreach ((int r, string op, string text, int index, int len) in Corpus)
        {
            if (op == "ins")
            {
                replicas[r].InsertText("body", index, text);
            }
            else
            {
                // Apply the deletion only if there is enough content (corpus robustness).
                if (replicas[r].GetText("body").Length >= index + len)
                {
                    replicas[r].DeleteText("body", index, len);
                }
            }
        }
    }

    private static void SyncAll(ICrdtDoc[] replicas)
    {
        // Two all-against-all passes: guarantees convergence with 3 replicas.
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
    }

    // Replicas converged after the same corpus → identical VersionId (SC-002, P-III).
    [Fact]
    public async Task Converged_replicas_share_version_id()
    {
        var blobs = new InMemoryBlobStore();
        var store = new VersionStore(Engine, blobs);
        ICrdtDoc[] replicas = [Engine.CreateDoc(), Engine.CreateDoc(), Engine.CreateDoc()];
        try
        {
            ApplyCorpus(replicas);
            SyncAll(replicas);

            VersionId reference = await store.PublishAsync(replicas[0]);
            foreach (ICrdtDoc r in replicas)
            {
                Assert.Equal(reference, await store.PublishAsync(r));
            }
            // A single blob: all replicas converged to the same state (dedup).
            Assert.Equal(1, blobs.Count);
        }
        finally
        {
            foreach (ICrdtDoc r in replicas)
            {
                r.Dispose();
            }
        }
    }

    // ── Cross-implementation parity vs Yjs JS (FU-012/CHARTER-09, research R13, P-III) ──────────
    // The yrs v1 export over the shared corpus must be BYTE-IDENTICAL to that of Yjs JS — that is,
    // Weft's determinism is "by format" (Yjs/yrs v1 encoding), not an accident of this
    // version of yrs. Requires deterministic client-ids (YrsEngine.CreateDoc(clientId), FU-012). The
    // Yjs golden hash lives in tests/determinism-yjs/golden.json; the Node harness regenerates and
    // self-checks it in release.yml (catches Yjs drift). This assertion is the per-PR BLOCKING gate.

    [Theory]
    [InlineData("corpus.json", "ascii")]
    [InlineData("corpus-unicode.json", "unicode")]
    public void Yrs_export_matches_yjs_golden(string corpusFile, string goldenKey)
    {
        string dir = DeterminismCorpusDir();
        CorpusSpec corpus = LoadCorpus(Path.Combine(dir, corpusFile));
        string golden = GoldenHash(Path.Combine(dir, "golden.json"), goldenKey);

        ICrdtDoc[] replicas = [.. corpus.ClientIds.Select(id => YrsEngine.Instance.CreateDoc((ulong)id))];
        try
        {
            // Apply each op to its replica (no length guard: exact parity with apply.mjs).
            foreach (CorpusOp step in corpus.Ops)
            {
                ICrdtDoc doc = replicas[step.Replica];
                if (step.Op == "ins")
                {
                    doc.InsertText(corpus.Type, step.Index, step.Text!);
                }
                else if (step.Op == "del")
                {
                    doc.DeleteText(corpus.Type, step.Index, step.Len);
                }
                else
                {
                    throw new InvalidOperationException($"op desconocida: {step.Op}");
                }
            }

            // Sync all-against-all until convergence (same scheme as apply.mjs).
            for (int pass = 0; pass < corpus.SyncPasses; pass++)
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

            byte[] export = replicas[0].ExportState();
            string yrsHash = Convert.ToHexStringLower(SHA256.HashData(export));

            Assert.Equal(golden, yrsHash);
        }
        finally
        {
            foreach (ICrdtDoc r in replicas)
            {
                r.Dispose();
            }
        }
    }

    // ── Loro self-determinism with a seeded peer_id (FU-016/CHARTER-13, research R13(a), P-III) ──
    // Unlike the yrs gate (parity vs Yjs, an INDEPENDENT implementation), Loro has NO
    // independent counterpart: npm's loro-crdt is a wasm build of the SAME Rust core, so a
    // "Loro↔reference gate" would be tautological. What this gate fixes is more modest and honest: with a
    // seeded peer_id, Loro's export over the shared corpus is STABLE cross-run and cross-RID.
    // The golden is a REGRESSION WITNESS, not a parity proof: its value is catching an encoding
    // change when bumping `loro` (R16). It is enabled by record_timestamp defaulting to false in loro
    // 1.13.6 (verified); if that changed, this gate would detect it when regenerating the golden.
    [Theory]
    [InlineData("corpus.json", "ascii")]
    [InlineData("corpus-unicode.json", "unicode")]
    public void Loro_seeded_export_matches_golden(string corpusFile, string goldenKey)
    {
        string dir = DeterminismCorpusDir();
        CorpusSpec corpus = LoadCorpus(Path.Combine(dir, corpusFile));
        string golden = GoldenHash(Path.Combine(dir, "golden-loro.json"), goldenKey);

        string hash = LoroSeededExportHash(corpus);
        Assert.Equal(golden, hash);
    }

    [Theory]
    [InlineData("corpus.json")]
    [InlineData("corpus-unicode.json")]
    public void Loro_seeded_export_is_stable_across_runs(string corpusFile)
    {
        // Self-determinism is the premise of the golden: without it, pinning a hash would be meaningless.
        CorpusSpec corpus = LoadCorpus(Path.Combine(DeterminismCorpusDir(), corpusFile));
        Assert.Equal(LoroSeededExportHash(corpus), LoroSeededExportHash(corpus));
    }

    // Applies the shared corpus to Loro replicas seeded with the ClientIds as peer_ids (the
    // corpus ClientIds are small ints, valid for both engines), syncs to convergence and
    // returns the SHA-256 of replica 0's export — same scheme as the yrs gate.
    private static string LoroSeededExportHash(CorpusSpec corpus)
    {
        IDeterministicSeeding seeding = LoroEngine.Instance.DeterministicSeeding
            ?? throw new InvalidOperationException("LoroEngine.DeterministicSeeding no debe ser null (FU-016).");

        ICrdtDoc[] replicas = [.. corpus.ClientIds.Select(id => seeding.CreateDoc((ulong)id))];
        try
        {
            foreach (CorpusOp step in corpus.Ops)
            {
                ICrdtDoc doc = replicas[step.Replica];
                if (step.Op == "ins")
                {
                    doc.InsertText(corpus.Type, step.Index, step.Text!);
                }
                else if (step.Op == "del")
                {
                    doc.DeleteText(corpus.Type, step.Index, step.Len);
                }
                else
                {
                    throw new InvalidOperationException($"op desconocida: {step.Op}");
                }
            }

            for (int pass = 0; pass < corpus.SyncPasses; pass++)
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

            return Convert.ToHexStringLower(SHA256.HashData(replicas[0].ExportState()));
        }
        finally
        {
            foreach (ICrdtDoc r in replicas)
            {
                r.Dispose();
            }
        }
    }

    // Locates tests/determinism-yjs/ by walking up from the test binary to the repo root.
    private static string DeterminismCorpusDir()
    {
        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            string candidate = Path.Combine(d.FullName, "tests", "determinism-yjs");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("tests/determinism-yjs/ not found from the test binary.");
    }

    private static CorpusSpec LoadCorpus(string path) =>
        JsonSerializer.Deserialize<CorpusSpec>(File.ReadAllText(path), CorpusJson)
        ?? throw new InvalidOperationException($"empty corpus: {path}");

    private static string GoldenHash(string path, string key)
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.GetProperty(key).GetString()
            ?? throw new InvalidOperationException($"golden[{key}] ausente en {path}");
    }

    private static readonly JsonSerializerOptions CorpusJson = new(JsonSerializerDefaults.Web);

    private sealed record CorpusSpec(
        string Type,
        int[] ClientIds,
        int SyncPasses,
        CorpusOp[] Ops);

    private sealed record CorpusOp(
        int Replica,
        string Op,
        int Index,
        string? Text,
        int Len);

    // The encoding is stable: loading a blob and re-exporting is byte-identical, indefinitely
    // (the property that makes a VersionId citable cross-platform).
    [Fact]
    public void Reload_and_reexport_is_byte_stable()
    {
        using ICrdtDoc doc = Engine.CreateDoc();
        doc.InsertText("body", 0, "contenido para hashear áéíóú");
        byte[] original = doc.ExportState();
        VersionId id = VersionId.FromBlob(original);

        byte[] current = original;
        for (int i = 0; i < 8; i++)
        {
            using ICrdtDoc reloaded = Engine.LoadDoc(current);
            byte[] next = reloaded.ExportState();
            Assert.Equal(original, next);                 // byte-identical on every reload
            Assert.Equal(id, VersionId.FromBlob(next));   // same hash
            current = next;
        }
    }
}
