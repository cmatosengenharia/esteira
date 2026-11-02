# Experimento — estilo 2: mapa animado

Catálogo de estilos, missão **Vender resultado**, item G. Composição em
`src/Esteira.Infrastructure/Media/Maps/MapComposer.cs`, com testes em `tests/Esteira.UnitTests/Media/MapComposerTests.cs`.

O que é: a partir dos locais informados no job (nome, latitude e longitude), um fundo de mapa estático com tiles
OpenStreetMap e um marcador por local. O marcador entra quando o local é citado na transcrição (ou em intervalos
iguais, se não for citado) e cresce até a escala cheia em 0,4 s. A atribuição "© OpenStreetMap contributors" fica sempre
visível (ODbL, ver `THIRD_PARTY_NOTICES.md`).

## Hipótese

Vídeos "Vender resultado" que citam locais (obras, clientes atendidos, cidades) retêm mais com o mapa animado, porque o
marcador transforma "atendemos em vários lugares" em prova visual.

## Métrica

- Primária: retenção aos 3 s.
- Secundária: watch time médio.
- Comparação: o mesmo vídeo bruto, publicado com e sem o mapa (A/B por rede, mesmo horário e mesma copy).

## Janela

14 dias ou 10 publicações com mapa, o que vier primeiro.

## Critério de corte

- Mantém o estilo no catálogo se a retenção aos 3 s subir pelo menos 5 pontos percentuais sem queda no watch time médio.
- Descarta se não houver ganho ao fim da janela.
- Interrompe antes se o volume de tiles exceder a Tile Usage Policy da OSMF (aí o próximo passo é usar um provedor de
  tiles próprio).
