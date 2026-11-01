# Esteira

Plataforma SaaS que pega um vídeo viral de referência, extrai o molde de edição (*style bible*), aplica ao vídeo bruto
do cliente e publica em todas as redes no formato de cada uma. Regras de arquitetura e licença: `CLAUDE.md`.

## Setup no Mac (Apple Silicon)

Pré-requisitos (sem sudo): Homebrew, .NET SDK 10, ffmpeg, Node 22+, Docker (Docker.app ou colima).

```bash
brew install dotnet
brew install ffmpeg node@22 whisper-cpp
brew install --cask docker        # ou: brew install colima docker docker-compose
```

Confere tudo, sobe o Docker e baixa o modelo do whisper (`models/ggml-small.bin`, fora do git):

```bash
scripts/check-env.sh
```

Cria a solução e compila:

```bash
scripts/bootstrap.sh
dotnet build
dotnet test
```

Sobe só a infraestrutura (PostgreSQL 16 e RabbitMQ 3 com management em http://localhost:15672, esteira/esteira):

```bash
docker compose up -d
```

API e workers rodam direto no Mac:

```bash
dotnet run --project src/Esteira.Api        # http://localhost:5080
dotnet run --project src/Esteira.Workers
```

## Estrutura

```
src/Esteira.Domain          entidades e enums, sem dependências
src/Esteira.Application     casos de uso, abstrações (IObjectStorage, ITranscriber, ...), DTOs
src/Esteira.Infrastructure  EF Core/Postgres, MassTransit, storage local, ffmpeg, whisper.cpp
src/Esteira.Connectors      ISocialPublisher por rede (modos Api e Assisted)
src/Esteira.Api             ASP.NET Core: upload, consulta de jobs, seed/auth
src/Esteira.Workers         um consumer por fila: ingest, transcribe, plan, render
tests/Esteira.UnitTests     xUnit
docs/contracts              edl.schema.json, style-bible.schema.json, network-specs.json
docs/adr                    decisões de arquitetura
scripts                     check-env.sh, bootstrap.sh, demo.sh, stop.sh
```

## Tudo no Docker (alternativa)

A mesma imagem da CI (`Dockerfile.dev`: SDK 10 + ffmpeg + whisper.cpp) sobe API e workers junto com a infra:

```bash
docker compose --profile app up --build
```

O código é montado em `/src`, então edições locais valem dentro do container. O modelo do whisper continua em `models/`.

## Fluxo de trabalho

A cópia de trabalho do Claude é o clone na nuvem; ele faz push na `main` e a CI (`.github/workflows/ci.yml`) compila,
testa e roda a esteira de ponta a ponta. No Mac, só `git pull`.
