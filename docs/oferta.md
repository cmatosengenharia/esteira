# Oferta da Esteira

Posicionamento e preço definidos antes de construir a landing e a cobrança (Fase 1, item 7). Este arquivo é a
fonte única: a tabela de `## Planos` é lida por `PlanCatalog` (`src/Esteira.Application/Billing/PlanCatalog.cs`).
Os preços são hipótese inicial, a validar em conversa com os primeiros clientes antes de mudar o produto.

## Alternativas

O que o cliente usa hoje para publicar vídeo curto, e onde cada opção deixa a desejar:

- **editor freelancer**: entende o estilo, mas custa por vídeo, demora dias e some quando o volume cresce.
- **CapCut**: barato e com templates, mas a edição é manual, vídeo a vídeo, e o template é genérico, não o do nicho.
- **Opus Clip**: corta vídeo longo em clipes de forma automática, mas aplica um estilo próprio e não publica no formato
  de cada rede.

## Valor único

A Esteira replica o molde de edição de um viral do próprio nicho (ritmo de cortes, legenda, gancho e CTA) no vídeo
bruto do cliente, e entrega pronto para cada rede (9:16, 16:9, 1:1, com título, descrição e hashtags). Nenhuma das
alternativas parte de um viral escolhido pelo cliente.

## Público inicial

Criadores e pequenas agências de um nicho definido que postam vídeo curto toda semana em mais de uma rede, gravam o
próprio material e hoje pagam editor ou perdem horas no CapCut.

## Planos

Cobrança mensal por minutos de render (o custo que mais pesa por job). Minuto não usado não acumula.

| id | nome | preço mensal (R$) | minutos de render/mês |
| --- | --- | --- | --- |
| inicial | Inicial | 97 | 60 |
| criador | Criador | 297 | 240 |
| agencia | Agência | 897 | 900 |
