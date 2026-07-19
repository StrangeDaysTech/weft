using Weft;
using Weft.Versioning;
using Weft.Versioning.Blobs;
using Weft.Yrs;

// pack-smoke (CHARTER-07/T057, SC-007): "hello-Weft" consumed from the packaged .nupkg.
// Proves that the native binary resolves from runtimes/<rid>/native/ on a clean machine and that
// an edit→publish cycle runs. Exits with a non-zero code on any failure (per-RID CI gate).

Console.WriteLine($"Weft pack-smoke — RID: {System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier}");

ICrdtEngine engine = YrsEngine.Instance;
Console.WriteLine($"  motor nativo resuelto: {engine.Name}");

var store = new VersionStore(engine, new InMemoryBlobStore());
using ICrdtDoc doc = engine.CreateDoc();
doc.InsertText("titulo", 0, "hola weft");
VersionId version = await store.PublishAsync(doc);

string text = doc.GetText("titulo");
string versionText = version.ToString();
Console.WriteLine($"  texto:   \"{text}\"");
Console.WriteLine($"  versión: {versionText}");

if (text != "hola weft")
{
    Console.Error.WriteLine($"✗ FALLO: texto inesperado (\"{text}\")");
    return 1;
}

// The VersionId is the hex SHA-256 of the deterministic export (64 chars); an empty/short hash = breakage.
if (string.IsNullOrWhiteSpace(versionText) || versionText.Length < 32)
{
    Console.Error.WriteLine($"✗ FALLO: VersionId vacío o demasiado corto (\"{versionText}\")");
    return 1;
}

Console.WriteLine("✓ pack-smoke OK: nativo resuelto desde el paquete, edit→publish verde.");
return 0;
