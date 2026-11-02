# ADR 0004 — O MVP ataca um nicho só: Imóveis

- **Status**: aceito · **Data**: 2026-11-02

## Contexto
O documento de visão (claude.ai/artifact/6ieUTU3fgSZDkb48Zatpb8, § Recorte do MVP) prevê quatro segmentos e um MVP com descoberta de virais, extração de molde e publicação por API em dez redes. A Fase 0 entregou o núcleo (upload → transcrição → corte → render 9:16 com legendas), mas sem nicho não há molde para replicar, nem cliente concreto para validar. Em 2026-11-02 o Cássio decidiu o nicho: **corretores e imobiliárias** que gravam vídeos brutos de imóveis (tour, fachada, área de lazer, fala do corretor) e querem vídeos curtos que vendem.

## Decisão
1. O MVP atende só o nicho de Imóveis. Outros nichos ficam no backlog até o piloto responder.
2. Fluxo único: bruto + dados do imóvel → molde escolhido de uma biblioteca curada à mão → um vídeo vertical 9:16 de 30 a 60 s (gancho nos 3 primeiros segundos, cortes, legendas, texto na tela com preço/quartos/bairro, CTA com contato) → título, legenda e hashtags → arquivo entregue para postar no Instagram Reels.
3. A biblioteca de formatos é montada manualmente a partir de vídeos de referência; a busca automática de virais (`Discover`) e a extração automática de molde (`IStyleExtractor`) ficam fora do MVP.
4. Publicação começa por uma rede (Instagram Reels). No MVP a entrega é o arquivo mais a copy; a publicação por API entra depois do app review da Meta.
5. O vídeo de imóvel frequentemente não tem fala: o planner precisa de um caminho sem transcrição (cenas por `scenedetect`), além do caminho por fala já existente.

## Por quê
Alternativas: (a) manter os quatro segmentos da visão, descartada porque nenhum molde concreto se escreve para "criador solo em geral"; (b) infoprodutores (cortes de aulas longas), onde o núcleo atual encaixa melhor, descartada por decisão do Cássio: Imóveis tem demanda recorrente, vídeo bruto abundante e o próprio corretor grava. O custo da escolha é fechar o caso "tour sem fala", pequeno em código.

## Consequências
`docs/oferta.md` § Público inicial passa a citar corretores e imobiliárias na próxima revisão da oferta. Moldes em `docs/formatos_imoveis/` (seed de `StyleTemplate`). `PropertyInfo` entra no job. O plano com as tarefas está na frente esteira da Cerebrum (`frentes/esteira/plano_mvp_imoveis.md`). Fora do MVP, registrado como backlog: outros nichos, descoberta de virais, multi-rede, vídeos longos, agendamento, analytics.
