# Borrador de `ICrdtEngine` — subproducto de Spike 03 (§10.4)

Interfaz que la **capa de versionado** necesita del motor CRDT, **validada por código** en Spike 03
(la misma capa de dominio corrió idéntica sobre yrs y Loro). Código de referencia:
`dotnet/Spike03/ICrdtEngine.cs`.

## Principio de diseño (el hallazgo)

El **núcleo portable es deliberadamente pequeño**: con **6 primitivas** + blobs content-addressed +
merge CRDT se construyen diff, branch/merge y compactación **sin tocar el motor**. Las primitivas de
versionado nativas (Loro) se modelan como **capacidad opcional**, no como parte del núcleo — así el
motor sigue reemplazable y la capa de versionado no se acopla a un motor concreto.

## Núcleo (obligatorio para cualquier motor)

```csharp
interface ICrdtEngine {
    string Name { get; }
    ICrdtDoc NewDoc();                          // documento vacío
    ICrdtDoc LoadDoc(ReadOnlySpan<byte> blob);  // reconstruir desde blob exportado
    INativeVersioning? NativeVersioning { get; } // capacidad opcional (null si no la hay)
}

interface ICrdtDoc : IDisposable {
    void   InsertText(string field, uint index, string text);
    void   DeleteText(string field, uint index, uint len);
    string ReadText(string field);
    byte[] ExportState();                    // blob determinista -> content-addressing (SHA-256)
    void   Import(ReadOnlySpan<byte> update); // merge de otra versión/rama
}
```

**Sobre estas 6 se construyeron (en la capa de dominio, portable):**
- `PublishVersion(doc)` → `SHA-256(ExportState)`, guarda blob content-addressed.
- `Diff(vA, vB)` → reconstruir ambos (`LoadDoc`) + text-diff.
- `Branch(v)` → `LoadDoc(blob)`; `Merge(main, branch)` → `main.Import(branch.ExportState())`.
- `Compact()` → conservar solo blobs publicados (store acotado, GC activo).

## Capacidad opcional (motores que la ofrezcan; Loro sí, yrs no la necesita)

```csharp
interface INativeVersioning {
    string NativeDiffProbe(ICrdtDoc doc, string field);        // Loro: DiffCalculator (diff semántico)
    string NativeBranchMergeProbe(ICrdtDoc doc, string field); // Loro: fork_at + merge
    byte[] ShallowSnapshot(ICrdtDoc doc);                      // Loro: snapshot con GC de historia
}
```

## Extensiones útiles observadas (para sync incremental / diff eficiente)

No imprescindibles para el versionado básico, pero baratas y útiles (yrs las expone; Loro tiene análogos):
```csharp
byte[] StateVector();                          // resumen "qué conozco"
byte[] ExportUpdateSince(ReadOnlySpan<byte> sv); // delta desde una versión (523 B -> 29 B medido)
```

## Notas para la fase de construcción
- **No** incluir `Snapshot`/time-travel in-doc en el núcleo: en yrs exige `skip_gc` (crecimiento sin
  cota). El versionado citable se resuelve con **blobs content-addressed de dominio** (GC activo).
- Blame: capa de dominio en ambos motores; si se elige yrs, rastrear autoría nosotros (no hay
  `PermanentUserData` en el port Rust).
- `INativeVersioning` permite, si algún día se cambia a Loro, **aprovechar** sus primitivas sin
  reescribir la capa de dominio (que sigue funcionando sobre el núcleo).
