using Weft;
using Weft.Loro;
using Weft.Yrs;

namespace Weft.Versioning.Tests;

/// <summary>
/// Optional capability <see cref="IDeterministicSeeding"/> (CHARTER-13/FU-016): seeding the replica
/// identity (yrs client_id, Loro peer_id) for reproducible exports. These tests pin down the
/// <b>asymmetry of the valid domain</b> between engines — the reason the capability is an optional
/// interface and not a method of <see cref="ICrdtEngine"/>.
/// </summary>
public sealed class DeterministicSeedingTests
{
    public static TheoryData<ICrdtEngine> Engines() => new() { YrsEngine.Instance, LoroEngine.Instance };

    [Theory]
    [MemberData(nameof(Engines))]
    public void Both_engines_expose_deterministic_seeding(ICrdtEngine engine)
    {
        Assert.NotNull(engine.DeterministicSeeding);
    }

    [Fact]
    public void Yrs_replica_id_domain_is_53_bits()
    {
        Assert.Equal(1UL << 53, YrsEngine.Instance.DeterministicSeeding!.MaxReplicaIdExclusive);
    }

    [Fact]
    public void Loro_replica_id_domain_is_full_ulong_minus_reserved()
    {
        // The u64::MAX reserved by Loro is the only value outside the domain; the exclusive bound is MaxValue.
        Assert.Equal(ulong.MaxValue, LoroEngine.Instance.DeterministicSeeding!.MaxReplicaIdExclusive);
    }

    [Fact]
    public void Loro_reserved_peer_id_throws_out_of_range()
    {
        // u64::MAX is reserved by Loro; the shim rejects it at the boundary → ArgumentOutOfRangeException.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => LoroEngine.Instance.DeterministicSeeding!.CreateDoc(ulong.MaxValue));
    }

    [Fact]
    public void Yrs_replica_id_beyond_53_bits_throws_out_of_range()
    {
        // yrs only accepts < 2^53; the shim rejects the excess at the boundary.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => YrsEngine.Instance.DeterministicSeeding!.CreateDoc(1UL << 53));
    }

    [Theory]
    [MemberData(nameof(Engines))]
    public void Seeded_doc_converges_with_an_unseeded_peer(ICrdtEngine engine)
    {
        // Seeding the identity must not break CRDT semantics: a seeded doc and a normal one converge.
        using ICrdtDoc seeded = engine.DeterministicSeeding!.CreateDoc(7);
        using ICrdtDoc other = engine.CreateDoc();
        seeded.InsertText("body", 0, "hola");

        other.ApplyUpdate(seeded.ExportUpdateSince(other.ExportStateVector()));
        Assert.Equal("hola", other.GetText("body"));
    }

    [Fact]
    public void Loro_seeded_export_is_stable_across_two_docs_with_the_same_peer_id()
    {
        // The value of seeding: two docs with the SAME peer_id and the SAME edits export identically.
        // Without seeding, Loro's random peer_id would make these bytes diverge. (yrs already has its
        // parity gate vs Yjs; here the Loro equivalent is pinned at the unit level.)
        byte[] a = SeededExport(LoroEngine.Instance, peerId: 99);
        byte[] b = SeededExport(LoroEngine.Instance, peerId: 99);
        Assert.Equal(a, b);

        // And a different peer_id produces different bytes (seeding really influences the encoding).
        byte[] c = SeededExport(LoroEngine.Instance, peerId: 100);
        Assert.NotEqual(a, c);
    }

    private static byte[] SeededExport(ICrdtEngine engine, ulong peerId)
    {
        using ICrdtDoc doc = engine.DeterministicSeeding!.CreateDoc(peerId);
        doc.InsertText("body", 0, "contenido determinista");
        return doc.ExportState();
    }
}
