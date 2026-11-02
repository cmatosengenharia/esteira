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
| [wondelai/skills](https://github.com/wondelai/skills) | não consultado (sem acesso ao repo nesta entrega); registrar o commit na primeira revisão com acesso | não verificada: tratada como sem licença, só a ideia foi usada | ideias das skills `obviously-awesome` (alternativas, valor único, cliente-alvo), `monetizing-innovation` (preço antes de construir) e `hundred-million-offers` | `docs/oferta.md`, `src/Esteira.Application/Billing/PlanCatalog.cs` | Reescrito do zero, sem cópia de texto: oferta própria em português e planos por minutos de render lidos da tabela da oferta. Testes em `tests/Esteira.UnitTests/Billing/PlanCatalogTests.cs`. |
| [remotion-dev/skills](https://github.com/remotion-dev/skills) | não consultado (sem acesso ao repo nesta entrega); registrar o commit na primeira revisão com acesso | não verificada: tratada como sem licença, só a ideia foi usada | regra `timing.md`: `interpolate` com `Easing.spring({damping: 200})` e clamp nas duas pontas | `src/Esteira.Infrastructure/Media/Rendering/SpringEasing.cs` | Reescrito do zero a partir da física do oscilador amortecido (forma fechada sobreamortecida, massa 1, rigidez 100, damping 200), sem código do core do Remotion (licença comercial). Gera a expressão ffmpeg do `zoompan`/`scale` com `clip` no tempo e na saída. Testes em `tests/Esteira.UnitTests/Media/SpringEasingTests.cs`. |
| [SamurAIGPT/AI-Youtube-Shorts-Generator](https://github.com/SamurAIGPT/AI-Youtube-Shorts-Generator) | não consultado (sem acesso ao repo nesta entrega); registrar o commit na primeira revisão com acesso | MIT | ideia geral: detecção de rosto por quadro + suavização do centro do recorte vertical | `src/Esteira.Infrastructure/Media/Rendering/FaceReframe.cs` | Reescrito em C#, sem cópia. Diferenças deliberadas: amostras a 2 fps vindas de processo externo, média móvel de 1 s que não atravessa corte de cena, passo máximo por amostra (5% da largura), cena sem rosto → centro fixo; saída é filtro `crop` do ffmpeg com expressão por tempo. Testes em `tests/Esteira.UnitTests/Media/FaceReframeTests.cs`. |

## Dependências de runtime (não portadas)

| Ferramenta | Licença | Uso |
| --- | --- | --- |
| ffmpeg / ffprobe | LGPL/GPL (binário externo, chamado por processo) | ingest, extração de áudio, render |
| whisper.cpp (`whisper-cli`) | MIT (binário externo, chamado por processo) | transcrição com timestamps por palavra |
| Hyperframes | Apache-2.0 | composições HTML (a partir da Fase 1) |
