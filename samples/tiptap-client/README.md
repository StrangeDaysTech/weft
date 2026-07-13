# Weft · cliente Tiptap + validación de compat del wire

Sample de US3 (CHARTER-05): un editor **Tiptap** colaborativo real contra el relay `Weft.Server`, más un
check **headless** de compatibilidad del wire con `yjs`/`y-websocket`. Demuestra que el relay interopera con
el ecosistema Yjs **sin adaptación** — el servidor habla `y-sync` estándar.

## Requisitos

- El sample server corriendo:
  ```bash
  dotnet run --project ../Weft.Sample.Server    # escucha en ws://127.0.0.1:5199/collab/{docId}
  ```
- Node.js + npm. Instalar deps una vez: `npm install`.

## 1) Validación headless (sin navegador) — gate de compat del wire

Dos `Y.Doc` reales de Yjs se conectan vía `y-websocket` y deben converger tras ediciones cruzadas:

```bash
npm run check
# ✓ convergencia Yjs (y-websocket) ↔ Weft.Server (yrs): "Hello from A. And B too."
```

Sale con código 0 si converge; 1 si diverge o da timeout. Es la evidencia de que los updates de yrs (servidor)
y Yjs (cliente) son intercambiables a nivel binario.

## 2) Validación manual con Tiptap (quickstart §US3)

```bash
npm run dev            # Vite sirve el editor en http://localhost:5173
```

1. Abre `http://localhost:5173/?doc=demo` en **2+ pestañas** (o navegadores).
2. Escribe en una pestaña → el texto aparece en vivo en las demás (convergencia).
3. Los cursores/nombres de los pares se ven (awareness); al cerrar una pestaña, su cursor desaparece (retirada).
4. Recarga una pestaña → recupera el estado desde el relay (delta en reconexión).
5. Reinicia el sample server → los documentos persisten (`FileSystemDocumentStore`).

El `docId` es el parámetro `?doc=`; cambia la URL base con `?url=ws://host:port/collab` si hace falta.
