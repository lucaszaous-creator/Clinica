// Vários testes exercitam caches ESTÁTICOS de referência (CatalogoConvenios,
// CatalogoModalidades, CatalogoEspecialidades) — estado global mutável. Com a
// paralelização padrão do xUnit, classes diferentes se atropelam nesse cache e
// causam falhas intermitentes. Desabilitar a paralelização entre coleções torna
// a suíte determinística. O custo deixou de ser irrelevante (~30 min em série, out/2026):
// a saída não foi religar o paralelismo, foi dividir a suíte entre PROCESSOS — o CI roda
// quatro fatias em jobs paralelos (FatiaDaSuite.cs, variável CLINICA_FATIA).
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
