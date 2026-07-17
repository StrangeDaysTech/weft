#!/usr/bin/env bash
# Guard contra «verificaciones fantasma» de la clase filtro-de-test (FU-020, CHARTER-12).
#
# El fallo que cierra: un comando `dotnet test --filter X` documentado o en CI que no casa con
# ningún test pasa EN VERDE ejecutando cero tests — su síntoma es idéntico al del éxito, así que
# sobrevive indefinidamente. Ocurrió con `Category=Concurrency` (quickstart US2, 0 tests durante
# meses hasta CHARTER-11) y sigue latente en `FullyQualifiedName~RedisDocumentStoreContractTests`
# (ci.yml): si alguien renombra esa clase, el job Redis pasa verde sin correr nada.
#
# Qué hace: descubre cada `dotnet test … --filter <expr>` en los archivos vigilados, y para cada
# uno exige que `--list-tests --filter <expr>` case con ≥1 test. `.NET` emite el texto exacto
# «No test matches the given testcase filter» cuando son cero — ese es el detector.
#
# Qué NO cubre (acotado a propósito, R3 del Charter): solo filtros de test. Un `dotnet run
# --project X` inexistente falla en ROJO, que es molesto pero no engañoso — otra clase.
set -euo pipefail

# Archivos donde un filtro documentado/ejecutado puede quedar huérfano.
FILES=(
  ".github/workflows/ci.yml"
  ".github/workflows/release.yml"
  "specs/001-weft-crdt-versioning/quickstart.md"
  "CONTRIBUTING.md"
  "README.md"
)

fail=0
found_any=0

# Extrae, de cada línea con `dotnet test … --filter …`: el proyecto (primer token `tests/…`) y el
# filtro (token tras --filter, con o sin comillas).
while IFS= read -r line; do
  [[ "$line" == *"dotnet test"* && "$line" == *"--filter"* ]] || continue

  proj="$(sed -n 's/.*dotnet test[[:space:]]\+\(tests\/[A-Za-z0-9._\/-]*\).*/\1/p' <<<"$line")"
  filter="$(sed -n 's/.*--filter[[:space:]]\+"\([^"]*\)".*/\1/p' <<<"$line")"
  [[ -z "$filter" ]] && filter="$(sed -n 's/.*--filter[[:space:]]\+\([^[:space:]]*\).*/\1/p' <<<"$line")"

  [[ -z "$proj" || -z "$filter" ]] && continue
  found_any=1

  echo "· ${proj%/}  --filter ${filter}"
  out="$(dotnet test "$proj" --configuration Release --list-tests --filter "$filter" 2>&1 || true)"
  if grep -qF "No test matches the given testcase filter" <<<"$out"; then
    echo "  ✗ FANTASMA: el filtro no casa con ningún test → el comando pasaría en verde con 0 tests"
    fail=1
  else
    echo "  ✓ casa con ≥1 test"
  fi
done < <(grep -rhnE "dotnet test.*--filter" "${FILES[@]}" 2>/dev/null || true)

# El guard mismo no debe ser una verificación fantasma: si dejó de encontrar filtros, algo cambió
# (¿se renombró un archivo? ¿cambió la sintaxis?) y hay que revisarlo, no pasar en silencio.
if [[ "$found_any" -eq 0 ]]; then
  echo "::error::el guard no encontró NINGÚN 'dotnet test --filter' en los archivos vigilados;" \
       "si de verdad ya no hay filtros documentados, actualiza este script — no lo dejes ciego"
  exit 1
fi

exit $fail
