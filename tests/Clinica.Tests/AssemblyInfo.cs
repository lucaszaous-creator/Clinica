// Vários testes exercitam caches ESTÁTICOS de referência (CatalogoConvenios,
// CatalogoModalidades, CatalogoEspecialidades) — estado global mutável. Com a
// paralelização padrão do xUnit, classes diferentes se atropelam nesse cache e
// causam falhas intermitentes. Desabilitar a paralelização entre coleções torna
// a suíte determinística (ela roda em poucos segundos, então o custo é irrelevante).
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
