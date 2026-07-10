using Weft;
using Weft.Versioning;
using Weft.Versioning.Blobs;
using Weft.Yrs;

namespace Weft.Determinism.Tests;

/// <summary>
/// Gate de determinismo del encoding (T029, constitución P-III): la identidad de una versión es
/// reproducible. Base del job cross-RID — el determinismo cross-implementación absoluto vs Yjs JS
/// (mismo hash en TODOS los RIDs) se completa en T058 (US4) con el corpus compartido; aquí se fijan
/// las propiedades reproducibles del encoding que ese job compara.
/// </summary>
public sealed class DeterminismTests
{
    private static readonly ICrdtEngine Engine = YrsEngine.Instance;

    // Corpus determinista: secuencia fija de ediciones repartidas entre 3 réplicas.
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
                // Aplica el borrado solo si hay suficiente contenido (robustez del corpus).
                if (replicas[r].GetText("body").Length >= index + len)
                {
                    replicas[r].DeleteText("body", index, len);
                }
            }
        }
    }

    private static void SyncAll(ICrdtDoc[] replicas)
    {
        // Dos pasadas todos-contra-todos: garantiza convergencia con 3 réplicas.
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

    // Réplicas convergidas tras el mismo corpus → VersionId idéntico (SC-002, P-III).
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
            // Un solo blob: todas las réplicas convergieron al mismo estado (dedup).
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

    // El encoding es estable: cargar un blob y re-exportar es byte-idéntico, indefinidamente
    // (la propiedad que hace que un VersionId sea citable cross-plataforma).
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
            Assert.Equal(original, next);                 // byte-idéntico en cada recarga
            Assert.Equal(id, VersionId.FromBlob(next));   // mismo hash
            current = next;
        }
    }
}
