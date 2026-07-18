using Weft;
using Weft.Loro;
using Weft.Yrs;

namespace Weft.Versioning.Tests;

/// <summary>
/// Capacidad opcional <see cref="IDeterministicSeeding"/> (CHARTER-13/FU-016): sembrar la identidad de
/// réplica (client_id de yrs, peer_id de Loro) para exports reproducibles. Estos tests fijan la
/// <b>asimetría del dominio válido</b> entre motores — la razón por la que la capacidad es una interfaz
/// opcional y no un método de <see cref="ICrdtEngine"/>.
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
        // El u64::MAX reservado por Loro es el único valor fuera del dominio; la cota exclusiva es MaxValue.
        Assert.Equal(ulong.MaxValue, LoroEngine.Instance.DeterministicSeeding!.MaxReplicaIdExclusive);
    }

    [Fact]
    public void Loro_reserved_peer_id_throws_out_of_range()
    {
        // u64::MAX está reservado por Loro; el shim lo rechaza en la frontera → ArgumentOutOfRangeException.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => LoroEngine.Instance.DeterministicSeeding!.CreateDoc(ulong.MaxValue));
    }

    [Fact]
    public void Yrs_replica_id_beyond_53_bits_throws_out_of_range()
    {
        // yrs solo acepta < 2^53; el shim rechaza el exceso en la frontera.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => YrsEngine.Instance.DeterministicSeeding!.CreateDoc(1UL << 53));
    }

    [Theory]
    [MemberData(nameof(Engines))]
    public void Seeded_doc_converges_with_an_unseeded_peer(ICrdtEngine engine)
    {
        // Sembrar la identidad no debe romper la semántica CRDT: un doc sembrado y otro normal convergen.
        using ICrdtDoc seeded = engine.DeterministicSeeding!.CreateDoc(7);
        using ICrdtDoc other = engine.CreateDoc();
        seeded.InsertText("body", 0, "hola");

        other.ApplyUpdate(seeded.ExportUpdateSince(other.ExportStateVector()));
        Assert.Equal("hola", other.GetText("body"));
    }

    [Fact]
    public void Loro_seeded_export_is_stable_across_two_docs_with_the_same_peer_id()
    {
        // El valor de la siembra: dos docs con el MISMO peer_id y las MISMAS ediciones exportan igual.
        // Sin siembra, el peer_id aleatorio de Loro haría divergir estos bytes. (yrs ya tiene su gate
        // de paridad vs Yjs; aquí se fija el equivalente de Loro a nivel de unidad.)
        byte[] a = SeededExport(LoroEngine.Instance, peerId: 99);
        byte[] b = SeededExport(LoroEngine.Instance, peerId: 99);
        Assert.Equal(a, b);

        // Y un peer_id distinto produce bytes distintos (la siembra realmente influye en el encoding).
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
