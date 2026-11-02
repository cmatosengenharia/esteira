# Esteira — instruções para o Claude

Plataforma SaaS que pega um vídeo viral de referência, extrai o molde de edição (style bible), aplica ao vídeo bruto do cliente e publica em todas as redes no formato de cada uma. Documento de visão e MVP: https://claude.ai/artifact/6ieUTU3fgSZDkb48Zatpb8

## Arquitetura (não negociar sem ADR)

- **.NET 10 (LTS), Clean Architecture** (ADR 0002: migrado de .NET 9, fora de suporte): `Domain` (entidades, sem dependências) → `Application` (casos de uso, interfaces) → `Infrastructure` (EF Core/Postgres, RabbitMQ, storage, ffmpeg, LLM) → `Api` (ASP.NET Core) e `Workers` (um `BackgroundService` por etapa da esteira).
- **Orientada a eventos**: a API grava e publica mensagem; cada worker consome uma fila, é idempotente e publica o evento da próxima etapa. Nunca encadear workers por chamada direta.
- **Multi-tenant por `TenantId`** em toda entidade e toda query (filtro global no `DbContext`). Nunca uma query sem tenant.
- **Orçamento por job**: todo job carrega limite de tokens e de minutos de render; o worker aborta e registra `BudgetExceeded` em vez de gerar custo sem teto.
- **Contratos JSON versionados** em `docs/contracts/`: `edl.schema.json` (plano de edição) e `style-bible.schema.json` (molde extraído). Mudança de contrato = nova versão + ADR.
- **Conectores** implementam `ISocialPublisher` com dois modos: `Api` (oficial) e `Assisted` (fila com arquivo pronto, copy e lembrete). Nunca automação de browser contra uma rede.
- **Render**: Hyperframes (Apache-2.0) para composições HTML e ffmpeg para corte, legenda ASS e recodificação. **Remotion core é proibido** (licença comercial).

## Regra de código de terceiros

- Sem `dotnet add package` ou `npm install` de repos de referência. Portar o algoritmo para dentro de `src/`, com testes, e registrar origem (repo, commit, licença, arquivos) em `THIRD_PARTY_NOTICES.md`.
- Só internalizar MIT, ISC, BSD, Apache-2.0 ou domínio público. **GPL, AGPL, licença comercial ou sem licença: ler e reescrever do zero, nunca copiar.**
- Dependências de runtime permitidas: ffmpeg, Whisper como serviço, SDKs oficiais das redes, Hyperframes, pacotes NuGet/npm mainstream (EF Core, MassTransit, Serilog, Vite, etc.).

## Convenções

- C#: nullable habilitado, `TreatWarningsAsErrors`, records para DTOs, `CancellationToken` em todo método async.
- Testes: xUnit em `tests/`; todo algoritmo portado tem teste reproduzindo os casos do repo original.
- Commits em português, imperativo. PR precisa passar no checklist de licença (`docs/PR_CHECKLIST.md`).
- Idioma do código e comentários: inglês nos identificadores, português nas docs.

## O círculo autoevolutivo trabalha aqui (ADR 0003)

- O loop (cmatosengenharia/circulo) recebe pedidos para este repositório e entrega cada um numa branch `circulo/<n>`
  com PR: plano antes do código, teste que falha antes da implementação (provado pelo loop na base), `dotnet test`,
  técnicas (cirúrgica, simplicidade), revisão cruzada e `scripts/portoes.sh`. Kit em `circulo.toml`.
- `scripts/portoes.sh` é o CLAUDE.md em forma de comando: rode antes de abrir PR. Constituição (nunca mexer sem PR
  humano): `.github/`, `CLAUDE.md`, ADR 0001, `docs/contracts/`, `circulo.toml`, `scripts/portoes.sh`.
- Skills em `.claude/skills/` (TDD, writing-plans, karpathy-guidelines): leia o SKILL.md que serve à tarefa.

## Mapa da esteira (fila → worker)

| Etapa | Fila | Worker | Entrada → Saída |
| --- | --- | --- | --- |
| 1 | `discover` | `Discover` | nicho → lista de virais (outliers) |
| 2 | `extract-template` | `ExtractTemplate` | url do viral → `StyleBible` |
| 3-4 | `ingest` | `Ingest` | upload → arquivo normalizado + metadados |
| 5 | `transcribe` | `Transcribe` | áudio → transcrição com timestamps por palavra |
| 6 | `plan` | `Plan` | transcrição + `StyleBible` → `Edl` |
| 7 | `render` | `Render` | `Edl` → masters (9:16, 16:9, 1:1) + cortes + `verify` |
| 8 | `copy` | `Copy` | `Edl` + rede → título, descrição, hashtags |
| 9 | (painel) | — | aprovação humana |
| 10 | `publish` | `Publish` | master + copy + conector → post publicado ou item assistido |
