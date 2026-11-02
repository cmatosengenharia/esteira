# Avisos de terceiros

Registro de todo algoritmo ou trecho **portado** de repositórios de referência para dentro de `src/`.
Regra (CLAUDE.md): nenhuma dependência de runtime em repo de referência; só se internaliza código sob
MIT, ISC, BSD, Apache-2.0 ou domínio público. GPL, AGPL, licença comercial ou sem licença: reescrever do zero.

Cada linha precisa de: repo, commit (ou tag) consultado, licença, arquivos de origem e onde foi portado aqui.

| Repo | Commit / tag | Licença | Arquivos de origem | Onde foi portado | Observações |
| --- | --- | --- | --- | --- | --- |
| [WyattBlue/auto-editor](https://github.com/WyattBlue/auto-editor) | série 27.x (2025); registrar o commit exato na primeira revisão com acesso ao repo | Unlicense (domínio público) | `auto_editor/analyze.py` (níveis de áudio por janela), `auto_editor/utils/func.py` (margem, minclip/mincut) | `src/Esteira.Infrastructure/Media/Audio/SilenceDetector.cs` | Reescrito em C#. Diferença deliberada: nível em RMS dBFS com limiar em dB (contrato `params.silenceThresholdDb`) em vez de amplitude máxima normalizada 0..1. Testes em `tests/Esteira.UnitTests/Media/SilenceDetectorTests.cs`. |
| [remotion-dev/remotion](https://github.com/remotion-dev/remotion) — pacote `@remotion/captions` | 4.0.x (2025) | MIT (o pacote `@remotion/captions`; o core do Remotion tem licença comercial e **não** é usado) | `packages/captions/src/create-tiktok-style-captions.ts` | `src/Esteira.Infrastructure/Media/Captions/CaptionGrouper.cs` | Só a regra de agrupamento e o padrão de 1200 ms (`combineTokensWithinMilliseconds`); implementação própria, sem cópia. Confirmar a licença do pacote no `package.json` na primeira revisão com acesso ao repo. |
| [sergebulaev/linkedin-skills](https://github.com/sergebulaev/linkedin-skills) | não consultado (sem acesso ao repo nesta entrega); registrar o commit na primeira revisão com acesso | não verificada: tratada como sem licença, só a ideia foi usada | ideia geral: fórmulas de gancho extraídas de posts virais viram template de post | `src/Esteira.Application/Copy/CopyTemplate.cs` | Reescrito do zero, sem cópia de texto ou código: fórmulas próprias em português por `hook.kind` e `cta.kind` do contrato style-bible, corte por limites de `network-specs.json`. Testes em `tests/Esteira.UnitTests/Copy/CopyTemplateTests.cs`. |

## Dependências de runtime (não portadas)

| Ferramenta | Licença | Uso |
| --- | --- | --- |
| ffmpeg / ffprobe | LGPL/GPL (binário externo, chamado por processo) | ingest, extração de áudio, render |
| whisper.cpp (`whisper-cli`) | MIT (binário externo, chamado por processo) | transcrição com timestamps por palavra |
| Hyperframes | Apache-2.0 | composições HTML (a partir da Fase 1) |
