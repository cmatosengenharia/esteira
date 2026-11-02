# Esteira

Plataforma SaaS que pega um vídeo viral de referência, extrai o molde de edição (*style bible*), aplica ao vídeo bruto
do cliente e publica em todas as redes no formato de cada uma. Regras de arquitetura e licença: `CLAUDE.md`.
Decisões: `docs/adr/`. Próxima fase: `docs/FASE_1.md`.

**Estado (Fase 0, concluída):** `POST /jobs` com um mp4 → `ingest` (ffprobe + WAV) → `transcribe` (whisper.cpp) →
`plan` (corte de silêncio + legendas por frase, EDL validado contra `docs/contracts/edl.schema.json`) → `render`
(ffmpeg, 9:16 1080x1920, legendas ASS queimadas). Tudo por fila (RabbitMQ), com tenant, orçamento e tempo por etapa.
A CI (`e2e`) roda isso com um vídeo real a cada push.

## Setup no Mac (Apple Silicon)

Pré-requisitos (sem sudo): Homebrew, .NET SDK 10, ffmpeg, Node 22+, Docker (Docker.app ou colima).

```bash
brew install dotnet ffmpeg node@22 whisper-cpp
brew install --cask docker        # ou: brew install colima docker docker-compose
```

Confere tudo, sobe o Docker e baixa o modelo do whisper (`models/ggml-small.bin`, fora do git):

```bash
bash scripts/check-env.sh
```

## Rodar a prova de ponta a ponta

```bash
bash scripts/demo.sh             # sobe infra, API e Workers, gera samples/demo.mp4 e acompanha o job
bash scripts/demo.sh meu.mp4     # com um vídeo seu
bash scripts/stop.sh             # para API, Workers e containers
```

Ao final o script imprime o caminho do master 9:16, o tempo de cada etapa e o consumo (`Usage`). Logs em `.run/`.
Linux funciona igual (TTS com `espeak-ng` em vez de `say`); é o que a CI faz.

## Rodar na mão

```bash
docker compose up -d                                   # PostgreSQL 16 e RabbitMQ 3 (management em :15672, esteira/esteira)
dotnet build
dotnet test
dotnet run --project src/Esteira.Api                   # http://localhost:5080 (aplica migrations em Development)
dotnet run --project src/Esteira.Workers

TOKEN=$(curl -s -X POST localhost:5080/dev/seed -H 'Content-Type: application/json' -d '{}' | python3 -c 'import sys,json;print(json.load(sys.stdin)["token"])')
curl -s -X POST localhost:5080/jobs -H "Authorization: Bearer $TOKEN" -F file=@samples/demo.mp4 -F targets=YouTubeShorts
curl -s localhost:5080/jobs -H "Authorization: Bearer $TOKEN"
curl -s localhost:5080/jobs/<id> -H "Authorization: Bearer $TOKEN"        # stage, usage, timings, outputs
curl -s localhost:5080/jobs/<id>/edl -H "Authorization: Bearer $TOKEN"    # plano de edição
```

Tudo no Docker, com a mesma imagem da CI (`Dockerfile.dev`: SDK 10 + ffmpeg + whisper.cpp):

```bash
docker compose --profile app up --build
```

## Testes

`dotnet test` roda 29 testes xUnit: isolamento por tenant (SQLite em memória), semântica dos workers (harness em
memória do MassTransit), detector de silêncio com WAV sintético, agrupamento de legendas com transcrição fixa, EDL
contra o schema, filtros do ffmpeg, parser do whisper.

## Migrations

O workflow `Migrations` gera a migration quando o modelo muda: escreva o nome em
`src/Esteira.Infrastructure/Persistence/Migrations/NEXT_MIGRATION_NAME` e faça push; o job gera, compila, testa e
commita na `main`. Localmente: `dotnet ef migrations add Nome --project src/Esteira.Infrastructure --startup-project src/Esteira.Api --output-dir Persistence/Migrations`.

## Estrutura

```
src/Esteira.Domain          entidades (Tenant, AppUser, VideoJob, StyleTemplate, SocialAccount), enums, exceções
src/Esteira.Application     abstrações (IObjectStorage, ITranscriber, IEditPlanner, IRenderer, IUsageMeter, ISocialPublisher…),
                            mensagens por fila, NetworkSpecCatalog, contratos embutidos (docs/contracts)
src/Esteira.Infrastructure  EF Core/Postgres com filtro global por tenant, MassTransit, storage local,
                            Media/: ffprobe, ffmpeg, whisper.cpp, SilenceDetector, CaptionGrouper, EDL, AssWriter, FfmpegRenderer
src/Esteira.Connectors      ISocialPublisher por rede (stub até a Fase 1)
src/Esteira.Api             ASP.NET Core: /dev/seed, /jobs, /jobs/{id}, /jobs/{id}/edl, /jobs/{id}/transcript
src/Esteira.Workers         StageConsumer<T> + Ingest, Transcribe, Plan, Render (filas ingest, transcribe, plan, render)
tests/Esteira.UnitTests     xUnit
docs/contracts              edl.schema.json, style-bible.schema.json, network-specs.json
docs/adr                    0001 arquitetura base, 0002 .NET 10
scripts                     check-env.sh, bootstrap.sh, demo.sh, stop.sh, print-job.py
data/                       storage local (fora do git)   models/   modelo do whisper (fora do git)
```

## Fluxo de trabalho

O círculo autoevolutivo (repo `circulo`) evolui este repositório por PR em branches `circulo/<n>`, com plano, teste
antes do código e evidência guardada no loop (ADR 0003; kit em `circulo.toml`; portões em `scripts/portoes.sh`).

A cópia de trabalho do Claude é o clone na nuvem; ele faz push na `main` e a CI compila, testa e roda a esteira de
ponta a ponta (`e2e`, com o master como artifact). No Mac, só `git pull`.
