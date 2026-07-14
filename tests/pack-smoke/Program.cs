using Weft;
using Weft.Versioning;
using Weft.Versioning.Blobs;
using Weft.Yrs;

// pack-smoke (CHARTER-07/T057, SC-007): "hello-Weft" consumido desde el .nupkg empaquetado.
// Prueba que el binario nativo resuelve desde runtimes/<rid>/native/ en una máquina limpia y que
// un ciclo edit→publish corre. Sale con código != 0 ante cualquier fallo (gate de CI por RID).

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

// El VersionId es el SHA-256 hex del export determinista (64 chars); un hash vacío/corto = ruptura.
if (string.IsNullOrWhiteSpace(versionText) || versionText.Length < 32)
{
    Console.Error.WriteLine($"✗ FALLO: VersionId vacío o demasiado corto (\"{versionText}\")");
    return 1;
}

Console.WriteLine("✓ pack-smoke OK: nativo resuelto desde el paquete, edit→publish verde.");
return 0;
