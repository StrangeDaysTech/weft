# Weft.Sample.Versioning

**User Story 1 (M0): edit and version documents from .NET — no server, no network.**

A console app that walks the full content-addressed versioning journey on top of the `yrs`
engine, entirely in memory. It's the "hello world" of `Weft.Versioning`.

## Run

```bash
dotnet run --project samples/Weft.Sample.Versioning
```

## What it does

1. **Create & edit** a document, then **publish v1** — you get a `VersionId` (the SHA-256 of the
   deterministic export; same content → same id, always).
2. **Edit & publish v2**, then **diff v1 → v2** word by word.
3. **Checkout** v1 — reconstructs the document from its hash and verifies integrity.
4. **Branch ×2 + merge** two concurrent branches off v2 — they converge automatically — and publish
   the merged result.

No authentication, no persistence adapter, no relay: this is the pure versioning layer, which
delivers citable value with only the library referenced. For the real-time collaboration story see
[`../Weft.Sample.Server`](../Weft.Sample.Server) and [`../tiptap-client`](../tiptap-client).
