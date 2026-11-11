using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>O profissional de uma guia, já nos códigos do padrão TISS. Campo nulo = a clínica não o cadastrou.</summary>
public sealed record ProfissionalTiss(string? Nome, string? Conselho, string? Numero, string? Uf, string? Cbos);

/// <summary>
/// O que o XML TISS exige e não mora na guia (nov/2026): QUEM atendeu cada atendimento
/// (conselho, número, UF e CBO) e QUANTO vale cada procedimento na tabela do convênio.
/// O gerador é puro e síncrono; este objeto é o que ele recebe pronto.
/// </summary>
public sealed class ContextoTiss
{
    /// <summary>Sem nada conhecido: o gerador sai com "não informado" e zero — só para teste e legado.</summary>
    public static readonly ContextoTiss Vazio = new();

    /// <summary>Por <c>AtendimentoId</c>.</summary>
    public Dictionary<int, ProfissionalTiss> Profissionais { get; } = new();

    /// <summary>Por <c>CodigoFaturamento.Id</c>, o preço da tabela do convênio vigente no dia do atendimento.</summary>
    public Dictionary<int, decimal> Valores { get; } = new();

    /// <summary>
    /// O que a operadora pode recusar e a clínica ainda consegue cadastrar: preço que falta
    /// na tabela, profissional sem conselho legível, sem CBO. Vazio = pronto.
    /// </summary>
    public List<string> Pendencias { get; } = new();

    public ProfissionalTiss? ProfissionalDe(CodigoFaturamento c)
        => Profissionais.TryGetValue(c.AtendimentoId, out var p) ? p : null;

    public decimal ValorDe(CodigoFaturamento c) => Valores.TryGetValue(c.Id, out var v) ? v : 0m;
}

/// <summary>
/// Monta o <see cref="ContextoTiss"/> de um conjunto de guias: uma leitura para os
/// profissionais do lote inteiro e a tabela de preços (que o <see cref="PrecoConvenioService"/>
/// lê uma vez só). As pendências saem com NOME — "Dra. Ana: registro do conselho…" — porque
/// "dados incompletos" sem dizer de quem não se corrige.
/// </summary>
public sealed class ContextoTissService
{
    private readonly IClinicaRepositorio _repo;
    private readonly PrecoConvenioService _precos;

    public ContextoTissService(IClinicaRepositorio repo, PrecoConvenioService precos)
    {
        _repo = repo;
        _precos = precos;
    }

    public async Task<ContextoTiss> MontarAsync(IReadOnlyCollection<CodigoFaturamento> codigos, CancellationToken ct = default)
    {
        var contexto = new ContextoTiss();
        if (codigos.Count == 0) return contexto;

        var atendimentos = codigos.Select(c => c.AtendimentoId).Distinct().ToList();
        var profissionais = await _repo.ProfissionaisDosAtendimentosAsync(atendimentos, ct);

        var pendencias = new SortedSet<string>(StringComparer.CurrentCulture);
        var semProfissional = 0;
        foreach (var grupo in codigos.GroupBy(c => c.AtendimentoId))
        {
            if (!profissionais.TryGetValue(grupo.Key, out var p)) { semProfissional++; continue; }

            var conselho = TabelasTiss.LerConselho(p.RegistroConselho);
            var especialidadeDaGuia = grupo.Select(c => c.EspecialidadeCodigo ?? c.Especialidade?.ToString())
                .FirstOrDefault(e => e is not null);
            var cbos = TabelasTiss.Cbos(p.CodigoCbo, p.EspecialidadeCodigo, especialidadeDaGuia);

            if (conselho is null)
                pendencias.Add($"{p.Nome}: o registro do conselho não pôde ser lido — cadastre em Equipe com conselho, UF e número (ex.: CRM-RJ 123456).");
            if (cbos is null)
                pendencias.Add($"{p.Nome}: sem CBO — preencha o CBO em Equipe (ex.: 225105, médico acupunturista).");

            contexto.Profissionais[grupo.Key] = new ProfissionalTiss(p.Nome, conselho?.Conselho, conselho?.Numero, conselho?.Uf, cbos);
        }
        if (semProfissional > 0)
            pendencias.Add($"{semProfissional} atendimento(s) sem profissional na agenda — a guia sai sem quem atendeu, e a operadora recusa.");

        var semPreco = new SortedSet<string>(StringComparer.CurrentCulture);
        foreach (var c in codigos)
        {
            var convenio = LoteTissService.ConvenioDaGuia(c);
            var dia = c.Atendimento?.Data ?? DateOnly.FromDateTime(DateTime.Today);
            var preco = await _precos.ResolverAsync(convenio, c.Tipo, dia, c.Especialidade, ct);
            if (preco is { Valor: > 0m }) contexto.Valores[c.Id] = preco.Valor;
            else semPreco.Add($"{CatalogoConvenios.Nome(convenio)}: sem preço de {c.Tipo} na tabela do convênio — a guia sai com valor zero.");
        }

        contexto.Pendencias.AddRange(pendencias);
        contexto.Pendencias.AddRange(semPreco);
        return contexto;
    }
}
