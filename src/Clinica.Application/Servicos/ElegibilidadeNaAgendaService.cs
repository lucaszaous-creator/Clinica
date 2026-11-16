using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>Um convênio em que a recepção consegue consultar a elegibilidade agora.</summary>
public sealed record ConvenioComConsulta(string Codigo, string Nome);

/// <summary>
/// Um paciente da agenda do dia, pronto para a consulta: convênio e carteirinha vindos do cadastro.
/// <see cref="Impedimento"/> nulo = dá para consultar; preenchido = a frase que a tela mostra no lugar.
/// </summary>
public sealed record PacienteDaAgendaParaElegibilidade(
    int AgendamentoId, int PacienteId, DateTime Hora, string Paciente, string? Profissional,
    string ConvenioCodigo, string ConvenioNome, string? Carteira, DateOnly? Validade,
    string? Impedimento, SeloDaConsultaDeHoje? ConsultaDeHoje);

/// <summary>O sinal pequeno do paciente já consultado hoje: rótulo curto e o tom do componente <c>Selo</c>.</summary>
public sealed record SeloDaConsultaDeHoje(string Rotulo, string Tom);

/// <summary>O que o painel da agenda precisa numa leitura: os convênios consultáveis e os pacientes do dia.</summary>
public sealed record ElegibilidadeNaAgenda(
    IReadOnlyList<ConvenioComConsulta> Convenios, IReadOnlyList<PacienteDaAgendaParaElegibilidade> Pacientes);

/// <summary>Um paciente achado pelo CPF, com o convênio e a carteirinha da ficha.</summary>
public sealed record PacienteDoCpf(int PacienteId, string Nome, string ConvenioCodigo, string ConvenioNome, string? Carteira, DateOnly? Validade);

/// <summary>O resultado da busca pelo CPF: quem foi achado e, quando não dá para consultar, a frase.</summary>
public sealed record BuscaDeElegibilidadePorCpf(IReadOnlyList<PacienteDoCpf> Encontrados, string? Frase)
{
    /// <summary>Um paciente só, com carteirinha: a consulta pode sair.</summary>
    public PacienteDoCpf? Unico => Frase is null && Encontrados.Count == 1 ? Encontrados[0] : null;
}

/// <summary>
/// A consulta de elegibilidade a partir da AGENDA (nov/2026, pedido do dono: "apenas uma função para
/// termos consulta"). Ela NÃO pergunta à operadora: monta a lista de quem dá para consultar e acha o
/// paciente pelo CPF. Quem pede a consulta é o MESMO componente da ficha do paciente
/// (<c>ElegibilidadeNaOperadora</c>), sobre a MESMA fila (<see cref="FilaTissService.EnfileirarElegibilidadeAsync"/>)
/// — com o mesmo intervalo, o mesmo teto diário e o reaproveitamento do resultado do dia.
///
/// Convênio consultável = família com elegibilidade no catálogo (<see cref="OperadorasTiss"/>) E
/// credencial gravada. O padrão TISS 4.01.00 não aceita CPF na verificação de elegibilidade
/// (<c>ct_elegibilidadeVerifica</c> pede <c>numeroCarteira</c>; não há campo de CPF): pelo CPF, o
/// sistema acha o paciente no cadastro e usa a carteirinha dele.
/// </summary>
public sealed class ElegibilidadeNaAgendaService
{
    public const string SemConsultaAutomatica = "Sem consulta automática para este convênio.";
    public const string Particular = "Atendimento particular: não há convênio a consultar.";
    public const string SemCarteirinha = "Sem carteirinha na ficha: cadastre o número para consultar.";
    public const string CpfInvalido = "CPF inválido: confira os 11 dígitos.";
    public const string CpfNaoEncontrado = "Nenhum paciente com este CPF no cadastro. Consulte pela carteirinha.";

    private readonly IClinicaRepositorio _repo;
    private readonly CredenciaisTissService _credenciais;
    private readonly Func<DateTime> _agora;

    public ElegibilidadeNaAgendaService(IClinicaRepositorio repo, CredenciaisTissService credenciais)
        : this(repo, credenciais, HoraDaClinica.Agora) { }

    public ElegibilidadeNaAgendaService(IClinicaRepositorio repo, CredenciaisTissService credenciais, Func<DateTime> agora)
    {
        _repo = repo;
        _credenciais = credenciais;
        _agora = agora;
    }

    /// <summary>
    /// Os convênios em que a consulta funciona agora: ativos, da família com elegibilidade no
    /// catálogo e com login e senha gravados. Os outros não entram — a tela os mostra desabilitados.
    /// </summary>
    public async Task<IReadOnlyList<ConvenioComConsulta>> ConveniosComConsultaAsync(CancellationToken ct = default)
    {
        var cadastro = await _repo.ConveniosAsync(ct);
        // Os cadastrados, mais as famílias EMBUTIDAS do catálogo de operadoras que não têm linha no
        // banco (código = nome da família, como o paciente as grava): é a mesma regra do catálogo de
        // convênios, que garante as embutidas, e do endereço do web service (CredenciaisTissService).
        var candidatos = cadastro.Where(c => c.Ativo && OperadorasTiss.TemElegibilidade(c.Familia))
            .Select(c => (c.Codigo, c.Nome))
            .Concat(OperadorasTiss.Todas
                .Where(o => OperadorasTiss.TemElegibilidade(o.Familia)
                            && !cadastro.Any(c => string.Equals(c.Codigo, o.Familia.ToString(), StringComparison.OrdinalIgnoreCase)))
                .Select(o => (Codigo: o.Familia.ToString(), Nome: CatalogoConvenios.Nome(o.Familia.ToString(), o.Familia))))
            .ToList();

        var lista = new List<ConvenioComConsulta>();
        // SEQUENCIAL de propósito: é o mesmo repositório (o mesmo DbContext). São poucos convênios.
        foreach (var (codigo, nome) in candidatos)
        {
            var credencial = await _credenciais.ParaATelaAsync(codigo, ct);
            if (credencial is { Login: not null, TemSenha: true }) lista.Add(new ConvenioComConsulta(codigo, nome));
        }
        return lista.OrderBy(c => c.Nome, StringComparer.Create(new System.Globalization.CultureInfo("pt-BR"), ignoreCase: true)).ToList();
    }

    /// <summary>
    /// A agenda do <paramref name="dia"/> para o painel: cada agendamento ativo (sem os cancelados,
    /// faltas e substituídos), com o convênio e a carteirinha da ficha, o motivo quando não dá para
    /// consultar e o sinal de quem já foi consultado HOJE.
    /// </summary>
    public async Task<ElegibilidadeNaAgenda> PrepararAsync(DateOnly dia, CancellationToken ct = default)
    {
        var convenios = await ConveniosComConsultaAsync(ct);
        var agendamentos = await _repo.AgendamentosNoPeriodoAsync(dia.ToDateTime(TimeOnly.MinValue), dia.ToDateTime(TimeOnly.MaxValue), ct);
        var (desde, ate) = FilaTiss.Dia(_agora());
        var deHoje = await _repo.ElegibilidadesTissDoDiaAsync(desde, ate, ct);
        var agora = _agora();

        var pacientes = agendamentos
            .Where(a => a.Paciente is not null && a.Status is StatusAgendamento.Agendado or StatusAgendamento.Realizado)
            .Select(a =>
            {
                var p = a.Paciente!;
                var codigo = p.ConvenioCodigo ?? p.Convenio.ToString();
                var carteira = Limpa(p.Carteirinha);
                var impedimento = Impedimento(a.Particular, codigo, carteira, convenios);
                var consulta = carteira is null ? null : deHoje.FirstOrDefault(t =>
                    string.Equals(t.ConvenioCodigo, codigo, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(t.NumeroCarteira, carteira, StringComparison.Ordinal));
                return new PacienteDaAgendaParaElegibilidade(
                    a.Id, p.Id, a.DataHora, p.Nome, a.Profissional?.NomeCurto, codigo, p.ConvenioNome,
                    carteira, p.ValidadeCarteirinha, impedimento,
                    consulta is null || impedimento is not null ? null : Selo(EnvioTissNaTela.DaElegibilidade(consulta, agora)));
            })
            .ToList();
        return new ElegibilidadeNaAgenda(convenios, pacientes);
    }

    /// <summary>
    /// Acha o paciente pelo CPF para a consulta avulsa. O CPF não vai à operadora (o padrão não o
    /// aceita): ele só aponta a ficha, e a consulta usa a carteirinha dela. Com
    /// <paramref name="convenioCodigo"/>, fica só quem é daquele convênio.
    /// </summary>
    public async Task<BuscaDeElegibilidadePorCpf> ProcurarPorCpfAsync(string? cpf, string? convenioCodigo, CancellationToken ct = default)
    {
        var digitos = Cpf.Normalizar(cpf);
        if (!Cpf.Valido(digitos)) return new([], CpfInvalido);

        var achados = (await _repo.PacientesPorCpfAsync(digitos, ct))
            .Select(p =>
            {
                var codigo = p.ConvenioCodigo ?? p.Convenio.ToString();
                return new PacienteDoCpf(p.Id, p.Nome, codigo, CatalogoConvenios.Nome(codigo, p.Convenio), Limpa(p.Carteirinha), p.ValidadeCarteirinha);
            })
            .ToList();
        if (achados.Count == 0) return new([], CpfNaoEncontrado);

        if (!string.IsNullOrWhiteSpace(convenioCodigo))
        {
            var doConvenio = achados.Where(p => string.Equals(p.ConvenioCodigo, convenioCodigo.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            if (doConvenio.Count == 0)
                return new(achados, $"O paciente com este CPF está em outro convênio na ficha ({string.Join(", ", achados.Select(a => a.ConvenioNome).Distinct())}).");
            achados = doConvenio;
        }
        if (achados.Count > 1)
            return new(achados, "Há mais de um paciente com este CPF no cadastro: consulte pela carteirinha de quem está no balcão.");
        return achados[0].Carteira is null ? new(achados, SemCarteirinha) : new(achados, null);
    }

    /// <summary>Por que este agendamento não pode ser consultado (nulo = pode).</summary>
    public static string? Impedimento(bool particular, string convenioCodigo, string? carteira, IReadOnlyList<ConvenioComConsulta> consultaveis)
        => particular ? Particular
            : !consultaveis.Any(c => string.Equals(c.Codigo, convenioCodigo, StringComparison.OrdinalIgnoreCase)) ? SemConsultaAutomatica
            : carteira is null ? SemCarteirinha
            : null;

    /// <summary>
    /// O sinal pequeno do agendamento consultado hoje, a partir da MESMA leitura da tela da consulta
    /// (<see cref="EnvioTissNaTela.DaElegibilidade"/>): "não sei" nunca tem a cara de "não elegível".
    /// </summary>
    public static SeloDaConsultaDeHoje Selo(ElegibilidadeNaTela leitura) => leitura switch
    {
        // O teto diário do convênio: o pedido está na fila e sai sozinho depois (não é falha).
        { Texto: FilaTiss.FraseLimiteDiario } => new("Na fila (limite do dia)", "aviso"),
        { Terminou: true, Tom: "sucesso" } => new("Elegível hoje", "sucesso"),
        { Terminou: true, Tom: "erro" } => new("Não elegível hoje", "perigo"),
        { Terminou: false } => new("Na fila", "info"),
        _ => new("Sem resposta hoje", "aviso")
    };

    private static string? Limpa(string? carteira)
    {
        var texto = (carteira ?? string.Empty).Replace(" ", string.Empty).Trim();
        return texto.Length == 0 ? null : texto;
    }
}
