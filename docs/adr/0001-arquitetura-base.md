# ADR 0001 — Arquitetura base da Esteira

- **Status**: aceito
- **Data**: 2026-11-01
- **Contexto**: Fase 0 (Fundação). Fonte das decisões: `CLAUDE.md` e documento de visão.

## Decisões

### 1. .NET 9 em Clean Architecture
`Domain` (entidades, sem dependências) → `Application` (casos de uso, interfaces, DTOs) → `Infrastructure`
(EF Core/Postgres, RabbitMQ, storage, ffmpeg, whisper, LLM) → `Api` (ASP.NET Core) e `Workers` (um
`BackgroundService`/consumer por etapa). `Connectors` fica ao lado de `Infrastructure`, dependendo só de `Application`.
**Por quê**: isola regra de negócio de ferramentas que vão trocar (LLM, render, redes).

### 2. Orientada a eventos
A API grava e publica mensagem em RabbitMQ (MassTransit). Cada worker consome uma fila, é idempotente e publica o
evento da próxima etapa. Nunca encadear workers por chamada direta.
**Por quê**: cada etapa tem custo e tempo diferentes; filas dão retentativa, dead-letter e escala independente.

### 3. Multi-tenant por `TenantId`
Toda entidade de negócio herda de `TenantEntity`; o `DbContext` aplica filtro global por tenant e recusa operar sem
tenant no contexto. Um teste garante que uma query sem tenant não vaza dados.
**Por quê**: SaaS por mensalidade desde o dia 1; vazamento entre clientes é falha fatal.

### 4. Orçamento por job
`VideoJob` carrega `Budget` (tokens de LLM e minutos de render) e `Usage`. Cada worker checa o orçamento antes de rodar e
aborta com `BudgetExceeded` em vez de gerar custo sem teto.
**Por quê**: custo variável (LLM, render) precisa de teto por cliente e por job.

### 5. Contratos JSON versionados
`docs/contracts/edl.schema.json` e `docs/contracts/style-bible.schema.json`. Mudança = nova versão + ADR. O EDL é
validado contra o schema antes de ser persistido.
**Por quê**: o EDL é a fronteira entre planejamento (IA) e render (determinístico); precisa ser estável e testável.

### 6. Render com Hyperframes + ffmpeg; Remotion core proibido
Composições HTML com Hyperframes (Apache-2.0); corte, legendas ASS e recodificação por rede com ffmpeg.
**Por quê**: Remotion core tem licença comercial para empresas; Hyperframes e ffmpeg cobrem o mesmo caso sem risco.

### 7. Conectores com dois modos
`ISocialPublisher` com `Api` (oficial) e `Assisted` (fila com arquivo pronto, copy e lembrete). Nunca automação de
browser contra uma rede.
**Por quê**: redes sem API pública (Kwai, WhatsApp) continuam atendidas sem violar termos de uso.

## Consequências
- Mais projetos e mais boilerplate no início; compensa a partir da Fase 1, quando entram LLM e redes.
- Toda etapa nova exige: contrato de entrada/saída, teste unitário e nota de custo.
