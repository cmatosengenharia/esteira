Leia os arquivos CLAUDE.md, docs/instrucoes-projeto-claude.md e docs/contracts/*.json que estão nesta pasta antes de qualquer coisa. Eles definem a arquitetura, a regra de licença e os contratos. Se não existirem, pare e me avise.

Objetivo desta sessão: criar a fundação da Esteira e rodar de ponta a ponta no meu Mac um vídeo bruto virando um vídeo editado com legendas, usando um molde JSON fixo. Nada de interface bonita ainda, nada de publicação em rede.

0. Ambiente (faça antes de criar qualquer arquivo)
   - Confirme a arquitetura com `uname -m` e as versões: `dotnet --version` (precisa ser 9.x), `ffmpeg -version`, `ffprobe -version`, `node --version` (22+), `docker --version`, `docker compose version`.
   - Verifique se o Docker está rodando com `docker info`. Se falhar: se existir `/Applications/Docker.app`, abra com `open -a Docker` e aguarde até `docker info` responder; se existir o comando `colima`, rode `colima start`. Não instale nada com sudo; se faltar algo, me diga o comando brew e pare.
   - Instale o whisper.cpp com `brew install whisper-cpp` se não existir, e baixe o modelo small em ./models/ggml-small.bin (script de download do próprio whisper.cpp ou o link oficial do Hugging Face ggerganov/whisper.cpp). O modelo fica fora do git.
   - Só prossiga quando tudo acima responder.

Entregue, nesta ordem, e me mostre cada passo funcionando antes de ir ao próximo:

1. Repositório
   - `git init`, `.gitignore` para .NET, Node e macOS (inclua models/, data/, samples/*.mp4), `README.md` com setup no Mac.
   - Solução `Esteira.sln` em .NET 9 com Clean Architecture: Esteira.Domain, Esteira.Application, Esteira.Infrastructure, Esteira.Connectors, Esteira.Api, Esteira.Workers e tests/Esteira.UnitTests. `Directory.Build.props` com Nullable e TreatWarningsAsErrors.
   - `docker-compose.yml` só com PostgreSQL 16 e RabbitMQ 3 (com management), portas padrão, volumes nomeados. API e workers rodam direto no Mac com `dotnet run`.
   - `THIRD_PARTY_NOTICES.md` com cabeçalho e colunas: repo, commit, licença, arquivos de origem, onde foi portado.
   - `docs/PR_CHECKLIST.md` com o item de revisão de licença.
   - GitHub Actions `ci.yml`: restore, build, test em ubuntu-latest.

2. Domínio e persistência
   - Entidades: Tenant, VideoJob (com Stage, Budget, Usage, Outputs, Targets), StyleTemplate, SocialAccount. Todas com TenantId.
   - EF Core + Npgsql, DbContext com filtro global por TenantId obrigatório, e um teste que prova que uma query sem tenant não retorna dados de outro tenant. Migration inicial.
   - Autenticação JWT simples com TenantId no token; um endpoint de seed cria tenant e usuário de desenvolvimento.

3. Mensageria
   - MassTransit com RabbitMQ. Uma fila por etapa: ingest, transcribe, plan, render. Cada worker é um IConsumer idempotente que lê o VideoJob, executa, atualiza Stage e publica o evento da próxima etapa. Erro grava FailureReason e Stage=Failed.

4. Esteira mínima funcionando com um vídeo real
   - POST /jobs recebe um mp4, salva em ./data/storage via IObjectStorage e publica em ingest.
   - Ingest: ffprobe para duração e fps; ffmpeg extrai áudio WAV mono 16 kHz.
   - Transcribe: chama o whisper.cpp local com o modelo small e converte a saída para Transcript {word, start, end, score}. ITranscriber fica como interface para trocar por serviço depois.
   - Plan: por enquanto SEM LLM. Implemente em C# a detecção de silêncio portada do auto-editor (RMS por janela + threshold + margem) e gere um EDL válido conforme docs/contracts/edl.schema.json, com os trechos mantidos e uma camada de legendas no modo frase, agrupando palavras por janela de 1200 ms (padrão do @remotion/captions, MIT). Registre as duas origens no THIRD_PARTY_NOTICES.md. Teste unitário com um WAV sintético.
   - Render: gere o ASS a partir das legendas do EDL e execute ffmpeg com select/concat dos trechos mantidos mais burn-in das legendas, saída 9:16 1080x1920 com crop central. Grave em Outputs.
   - GET /jobs/{id} mostra Stage, Usage e caminhos dos outputs.

5. Prova
   - Script `scripts/demo.sh` que: garante o Docker ativo (Docker.app ou colima, como no passo 0), sobe o docker-compose, aplica migrations, inicia API e Workers em background, gera um vídeo de teste de 20 s em ./samples/ se não houver nenhum (áudio com `say` do macOS falando um texto com pausas, imagem com ffmpeg testsrc), envia o mp4 para POST /jobs e faz polling até Stage=Rendered ou Failed, com timeout de 10 minutos.
   - Ao final, me diga o caminho do vídeo final, o tempo de cada etapa e como parar tudo.

Regras: não adicione pacote de nenhum repo de referência; porte o algoritmo e cite a origem. Não use Remotion. Pergunte antes de qualquer decisão que não esteja no CLAUDE.md. Commits pequenos em português, um por entrega acima.
---
