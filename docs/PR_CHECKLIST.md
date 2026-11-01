# Checklist de PR

Marque todos antes de pedir revisão.

- [ ] **Licença**: nenhum pacote de repo de referência foi adicionado (`dotnet add package` / `npm install`). Todo algoritmo portado está listado em `THIRD_PARTY_NOTICES.md` com repo, commit, licença, arquivos de origem e destino. Nada de GPL/AGPL/comercial/sem licença copiado. Nada de Remotion core.
- [ ] **Tenant**: nenhuma query nova sem filtro por `TenantId` (o filtro global do `DbContext` está ativo e não foi desligado com `IgnoreQueryFilters` sem justificativa em comentário).
- [ ] **Orçamento**: toda etapa nova consulta `Budget` antes de rodar e registra consumo no `IUsageMeter`.
- [ ] **Contratos**: mudança em `docs/contracts/*.json` veio com nova versão e ADR em `docs/adr/`.
- [ ] **Eventos**: nenhum worker chama outro worker diretamente; só publica mensagem.
- [ ] **Testes**: xUnit em `tests/`; algoritmo portado tem teste reproduzindo casos do repo original.
- [ ] **Build**: `dotnet build` sem warnings (`TreatWarningsAsErrors`) e `dotnet test` verde.
- [ ] **Commit**: mensagens em português, imperativo.
