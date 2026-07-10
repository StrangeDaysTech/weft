using Weft;
using Weft.Versioning;
using Weft.Versioning.Blobs;
using Weft.Yrs;

// Sample de US1: editar y versionar documentos content-addressed desde .NET (T030).
// Recorre el user journey completo: publicar → diff → checkout → branch → merge.

ICrdtEngine engine = YrsEngine.Instance;
var store = new VersionStore(engine, new InMemoryBlobStore());

Console.WriteLine($"Motor: {engine.Name}\n");

// 1. Crear y editar un documento.
using ICrdtDoc doc = engine.CreateDoc();
doc.InsertText("titulo", 0, "El veloz murciélago");
VersionId v1 = await store.PublishAsync(doc);
Console.WriteLine($"v1 publicada  → {v1}");
Console.WriteLine($"   titulo: \"{doc.GetText("titulo")}\"\n");

// 2. Editar y publicar una segunda versión.
doc.DeleteText("titulo", 9, 10);           // borra "murciélago"
doc.InsertText("titulo", 9, "colibrí");
VersionId v2 = await store.PublishAsync(doc);
Console.WriteLine($"v2 publicada  → {v2}");
Console.WriteLine($"   titulo: \"{doc.GetText("titulo")}\"\n");

// 3. Diff entre versiones (por palabras).
TextDiff diff = await store.DiffAsync(v1, v2, "titulo");
Console.WriteLine("Diff v1 → v2 (titulo):");
foreach (TextDiffSegment seg in diff.Segments)
{
    string mark = seg.Op switch { DiffOp.Inserted => "+", DiffOp.Deleted => "-", _ => " " };
    Console.WriteLine($"   {mark} \"{seg.Text}\"");
}
Console.WriteLine();

// 4. Checkout: reconstruir el documento de la v1 (verifica integridad).
using ICrdtDoc restored = await store.CheckoutAsync(v1);
Console.WriteLine($"Checkout v1   → titulo: \"{restored.GetText("titulo")}\"\n");

// 5. Branch + merge: dos ramas concurrentes desde v2 que convergen.
using ICrdtDoc branchA = await store.BranchAsync(v2);
using ICrdtDoc branchB = await store.BranchAsync(v2);
branchA.InsertText("titulo", 0, "[A] ");
branchB.InsertText("titulo", 0, "[B] ");
store.Merge(branchA, branchB);
VersionId merged = await store.PublishAsync(branchA);
Console.WriteLine($"Merge A◁B     → {merged}");
Console.WriteLine($"   titulo: \"{branchA.GetText("titulo")}\"");

Console.WriteLine("\n✓ Journey de versionado completado.");
