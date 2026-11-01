# Fase 1 — o que entra na próxima sessão

Critério de saída da Fase 0 (cumprido): um vídeo bruto vira um vídeo 9:16 com silêncios cortados e legendas por frase,
de ponta a ponta, por fila, com tenant, orçamento e timing por etapa, validado na CI (`e2e`).

Ordem sugerida (cada item vem com contrato, teste e nota de custo, como manda o CLAUDE.md):

1. **Extração de molde — `IStyleExtractor`** (worker `ExtractTemplate`, fila `extract-template`)
   - Download do viral de referência (yt-dlp como processo externo; só com URL fornecida pelo cliente).
   - Medidas por ffmpeg: `scenedetect`/`select='gt(scene,0.3)'` → cortes por minuto e duração média de plano;
     `silencedetect` → pausas; `ebur128` → loudness integrado; ffprobe → duração e aspecto; transcrição → hook e WPM.
   - LLM de visão (Claude) com 6–10 frames amostrados + transcrição → `hook.kind`, `structure[]`, `captions`, `zoom`, `cta`, `notes`.
   - Saída validada contra `style-bible.schema.json`, salva em `StyleTemplate`. Custo: ~tokens de 10 imagens + transcrição.
2. **Planner com LLM — `IEditPlanner`** (substitui o `SilenceEditPlanner` quando há style bible)
   - Prompt com transcrição por palavra + style bible → `keep[]`, `layers[]` (zoom, title, cta), `clips[]` (prompt de viralidade portado do AI-Youtube-Shorts-Generator, checar licença) e `verify`.
   - Validação contra `edl.schema.json`; se inválido, uma rodada de correção; se ainda inválido, cai no planner por silêncio.
   - Custo: `Budget.MaxLlmTokens`; registrar no `IUsageMeter` antes e depois.
3. **Descoberta de virais** (worker `Discover`, fila `discover`)
   - YouTube Data API (search + videos.list) e cálculo de outlier: views / mediana do canal nos últimos N vídeos.
   - Guardar candidatos com `views`, `channelMedianViews`, `durationSeconds` para o `reference` da style bible.
4. **Render completo**
   - `IRenderer`: saídas 16:9 e 1:1, camadas `zoom` (punch/ratchet/kenburns via `zoompan`), `title`, `cta`;
     Hyperframes para composições HTML; `verify` (cuts/min, duração alvo, LUFS com `loudnorm`).
   - `reframe: face-track` (detecção de rosto por processo externo, ou crop fixo por cena).
5. **Painel React de aprovação** (etapa 9 do mapa)
   - Vite + TS + Tailwind: login, lista de jobs, player dos masters, EDL/legendas, aprovar/reprovar, escolher redes.
   - API: `POST /jobs/{id}/approve`, `GET /jobs/{id}/outputs/{aspect}` (stream), `GET /style-templates`.
6. **Conector YouTube — `ISocialPublisher`** (modos `Api` e `Assisted`)
   - Ler spec oficial (YouTube Data API v3 `videos.insert`, resumable upload, quota 1600 unidades/upload) e `network-specs.json`.
   - `Api`: OAuth 2 por tenant, tokens cifrados em `SocialAccount`, upload do master 16:9 ou Shorts 9:16, título/descrição/hashtags do worker `Copy`.
   - `Assisted`: pacote (arquivo + copy + instruções) e lembrete; fila de pendências no painel.
   - Abrir já os pedidos de acesso/app review das demais redes (Meta, TikTok, LinkedIn, X, Pinterest, Threads).
7. **Cobrança**: Stripe ou Asaas, plano por mensalidade, limite de minutos de render e de tokens por plano alimentando `Budget`.

Dívidas técnicas da Fase 0 a tratar quando convier:
- Outbox transacional no MassTransit (hoje `SaveChanges` e `Publish` não são atômicos).
- Storage remoto (S3/GCS) implementando `IObjectStorage`; `GetLocalPathAsync` baixa para temporário.
- Pinagem do commit do auto-editor e confirmação da licença do `@remotion/captions` em `THIRD_PARTY_NOTICES.md`.
- Limpeza de `data/work` após render; retenção de masters por plano.
