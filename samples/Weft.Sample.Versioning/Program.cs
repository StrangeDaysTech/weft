using Weft;
using Weft.Versioning;
using Weft.Versioning.Blobs;
using Weft.Yrs;

// US1 sample: edit and version content-addressed documents from .NET (T030).
// Walks the full user journey: publish → diff → checkout → branch → merge.

ICrdtEngine engine = YrsEngine.Instance;
var store = new VersionStore(engine, new InMemoryBlobStore());

Console.WriteLine($"Engine: {engine.Name}\n");

// 1. Create and edit a document.
using ICrdtDoc doc = engine.CreateDoc();
doc.InsertText("title", 0, "The quick brown bat");
VersionId v1 = await store.PublishAsync(doc);
Console.WriteLine($"v1 published  → {v1}");
Console.WriteLine($"   title: \"{doc.GetText("title")}\"\n");

// 2. Edit and publish a second version.
doc.DeleteText("title", 10, 9);            // deletes "brown bat"
doc.InsertText("title", 10, "hummingbird");
VersionId v2 = await store.PublishAsync(doc);
Console.WriteLine($"v2 published  → {v2}");
Console.WriteLine($"   title: \"{doc.GetText("title")}\"\n");

// 3. Diff between versions (word level).
TextDiff diff = await store.DiffAsync(v1, v2, "title");
Console.WriteLine("Diff v1 → v2 (title):");
foreach (TextDiffSegment seg in diff.Segments)
{
    string mark = seg.Op switch { DiffOp.Inserted => "+", DiffOp.Deleted => "-", _ => " " };
    Console.WriteLine($"   {mark} \"{seg.Text}\"");
}
Console.WriteLine();

// 4. Checkout: reconstruct the v1 document (verifies integrity).
using ICrdtDoc restored = await store.CheckoutAsync(v1);
Console.WriteLine($"Checkout v1   → title: \"{restored.GetText("title")}\"\n");

// 5. Branch + merge: two concurrent branches off v2 that converge.
using ICrdtDoc branchA = await store.BranchAsync(v2);
using ICrdtDoc branchB = await store.BranchAsync(v2);
branchA.InsertText("title", 0, "[A] ");
branchB.InsertText("title", 0, "[B] ");
store.Merge(branchA, branchB);
VersionId merged = await store.PublishAsync(branchA);
Console.WriteLine($"Merge A◁B     → {merged}");
Console.WriteLine($"   title: \"{branchA.GetText("title")}\"");

Console.WriteLine("\n✓ Versioning journey complete.");
