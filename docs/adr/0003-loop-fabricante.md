# ADR 0003 — A Esteira é evoluída pelo círculo autoevolutivo (Fabricante)

- **Status**: aceito
- **Data**: 2026-11-01
- **Relacionado**: ADR 0014 do repositório `circulo`.

## Contexto
Cássio decidiu que a Esteira é o primeiro produto evoluído pelo loop (cmatosengenharia/circulo): o loop planeja,
implementa, testa e valida cada evolução, com as técnicas absorvidas das skills externas (plano e teste antes do
código, mudança cirúrgica, simplicidade, resposta direta, revisão cruzada), e guarda a evidência de cada entrega.

## Decisão
1. **Kit do círculo em `circulo.toml`** (o repositório não é Python): testes `dotnet test Esteira.sln`, lint
   `dotnet build -warnaserror`, constituição (`.github/`, `CLAUDE.md`, ADR 0001, `docs/contracts/`, o próprio kit e
   `scripts/portoes.sh`), etapa `pr` = `scripts/portoes.sh`.
2. **Portões do CLAUDE.md viram comando** (`scripts/portoes.sh`, também na CI): sem Remotion; `IgnoreQueryFilters` só
   com `// tenant:`; consumer novo consulta orçamento; schema mudado exige ADR; mudança em `src/` vem com `tests/`.
3. **Fluxo**: pedido na caixa do loop (`projeto: <id da esteira>`) → clone isolado em `circulo/<n>` → plano (só
   leitura) → teste que FALHA na base (provado pelo loop num worktree da base) → código → `dotnet test` → técnicas →
   Validador (modelo diferente do que escreveu) → push da branch → PR com a evidência. Nada entra na `main` sem CI.
   O humano continua podendo trabalhar na `main` como antes; as duas coisas convivem por PR.
4. **Skills no repositório** (`.claude/skills/`, MIT, cópias de obra/superpowers e forrestchang/andrej-karpathy-skills):
   test-driven-development, writing-plans, karpathy-guidelines — o Claude Code do Fabricante as carrega daqui.
5. **Backlog da Fase 1 fatiado** em pedidos pequenos (um contrato + um teste + uma nota de custo por pedido), na ordem:
   copy por rede → render 16:9 e 1:1 → cortes → extração de molde → planner com LLM → descoberta → conectores
   (YouTube, Instagram, TikTok, Facebook; modos Api e Assisted) → cobrança.

## Consequências
- Toda evolução passa a ter rastro no loop (plano, prova de teste vermelho/verde, veredito do validador, PR).
- Custo por pedido: 2–4 sessões de Claude Code + 1 chamada de revisão; o `dotnet test` roda duas vezes (prova e entrega).
- Mudança no kit ou nos portões é constituição: só por PR humano.
