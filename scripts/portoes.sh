#!/usr/bin/env bash
# Portões do CLAUDE.md como comando (etapa `pr` do kit do círculo; a CI também pode chamar). Sai 1 ao primeiro problema.
#   1. Licença: nenhum pacote Remotion (core é comercial) em csproj/props.
#   2. Tenant: `IgnoreQueryFilters(` só com justificativa `// tenant:` na mesma linha.
#   3. Orçamento: todo consumer novo em Workers/Consumers consulta o orçamento (EnsureBudget/Budget) ou registra uso (IUsageMeter/Usage).
#   4. Contratos: schema em docs/contracts mudado exige ADR novo no mesmo diff (contra origin/main, quando existe).
#   5. Testes: o diff que toca src/ traz mudança em tests/ (reprodução primeiro).
set -uo pipefail
cd "$(dirname "$0")/.."
falha() { echo "PORTÃO ✗ $*" >&2; exit 1; }
ok() { echo "PORTÃO ✓ $*"; }

if grep -rIn --include='*.csproj' --include='*.props' -E 'Include="Remotion' . >/dev/null 2>&1; then
  falha "licença: pacote Remotion referenciado (core é comercial; use Hyperframes/ffmpeg)"
fi
ok "licença: sem Remotion"

if grep -rIn --include='*.cs' 'IgnoreQueryFilters(' src | grep -v '// tenant:' >/dev/null 2>&1; then
  falha "tenant: IgnoreQueryFilters sem justificativa '// tenant: <motivo>' na mesma linha"
fi
ok "tenant: filtro global respeitado"

for f in src/Esteira.Workers/Consumers/*Consumer.cs; do
  [[ "$(basename "$f")" == "StageConsumer.cs" || "$(basename "$f")" == "IngestConsumer.cs" ]] && continue   # base e ingest (sem custo variável) já auditados na Fase 0
  grep -qE 'Budget|Usage|Meter' "$f" || falha "orçamento: $f não consulta Budget nem registra uso (IUsageMeter)"
done
ok "orçamento: consumers consultam o orçamento"

BASE=""
git rev-parse --verify -q origin/main >/dev/null 2>&1 && BASE="origin/main"
if [[ -n "$BASE" ]]; then
  MUDADOS=$(git diff --name-only "$BASE"...HEAD 2>/dev/null; git diff --name-only --cached 2>/dev/null; git diff --name-only 2>/dev/null; git ls-files --others --exclude-standard)
  if echo "$MUDADOS" | grep -q '^docs/contracts/.*\.json$'; then
    echo "$MUDADOS" | grep -q '^docs/adr/' || falha "contratos: schema em docs/contracts mudou sem ADR novo em docs/adr/"
  fi
  ok "contratos: mudança de schema acompanhada de ADR (ou sem mudança)"
  if echo "$MUDADOS" | grep -q '^src/'; then
    echo "$MUDADOS" | grep -q '^tests/' || falha "testes: o diff toca src/ sem tocar tests/ (reprodução primeiro)"
  fi
  ok "testes: mudança em src/ vem com teste"
fi
echo "PORTÕES OK"
