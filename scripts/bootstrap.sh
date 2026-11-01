#!/usr/bin/env bash
# Cria Esteira.sln e adiciona os projetos. Idempotente.
set -euo pipefail
cd "$(dirname "$0")/.."
[[ -f Esteira.sln ]] || dotnet new sln -n Esteira --format sln
for p in src/*/*.csproj tests/*/*.csproj; do
  dotnet sln Esteira.sln add "$p" >/dev/null 2>&1 || true
done
dotnet sln Esteira.sln list
