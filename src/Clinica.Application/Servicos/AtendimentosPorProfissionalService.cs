using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Application.Formatacao;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>Um atendimento realizado, na linha do relatório por profissional.</summary>
/// <param name="ValorTabela">
/// Soma da TABELA DE PREÇO POR CONVÊNIO para as guias do atendimento; nulo quando o atendimento
/// é particular (não há guia) ou quando ao menos uma guia não tem preço cadastrado — nesse caso
/// <paramref name="GuiasSemPreco"/> diz quantas. Somar só as que têm preço afirmaria um valor
/// menor com cara de exato.
/// </param>
/// <param name="Recebido">O que ENTROU no caixa ligado a este atendimento (lançamentos de entrada realizados).</param>
public sealed record AtendimentoDoProfissional(
    DateTime DataHora,
    int AtendimentoId,
    string? NumeroAtendimento,
    int PacienteId,
    string Paciente,
    string Convenio,
    bool Particular,
    string Procedimento,
    IReadOnlyList<string> Guias,
    decimal? ValorTabela,
    int GuiasSemPreco,
    decimal Recebido)
{
    /// <summary>
    /// O TIPO DE ATENDIMENTO da consulta — "Infertilidade", "Ginecologia", "Suplementação para
    /// gestante"… (nov/2026, a direção: "fica mais caracterizado o tipo de atendimento"). Vazio
    /// quando não é consulta: acupuntura e BSV já se dizem pelo procedimento.
    /// </summary>
    public string TipoDeAtendimento { get; init; } = string.Empty;

    /// <summary>
    /// A GUIA INTERNA em que o atendimento de infertilidade foi lançado ("Geriatria",
    /// "Psiquiatria"); nulo quando não é. Controle interno — vem do horário, não de guia do convênio.
    /// </summary>
    public string? GuiaInterna { get; init; }
}

/// <summary>Uma linha do resumo "por tipo de atendimento" ou "por guia interna".</summary>
public sealed record ContagemDoRelatorio(string Nome, int Quantidade, int Particulares, decimal Recebido);

/// <summary>Os atendimentos de UM profissional no período, com os totais.</summary>
public sealed record ProfissionalNoRelatorio(int ProfissionalId, string Profissional, IReadOnlyList<AtendimentoDoProfissional> Atendimentos)
{
    public int Quantidade => Atendimentos.Count;
    public int PacientesDistintos => Atendimentos.Select(a => a.PacienteId).Distinct().Count();
    /// <summary>
    /// Quantos foram PARTICULARES — pela ficha ou pela marca do horário (nov/2026, o pedido
    /// das médicas: "para a gente ter um relatório de quantas consultas particulares têm").
    /// </summary>
    public int Particulares => Atendimentos.Count(a => a.Particular);
    public decimal TotalTabela => Atendimentos.Sum(a => a.ValorTabela ?? 0m);
    public decimal TotalRecebido => Atendimentos.Sum(a => a.Recebido);
    /// <summary>Atendimentos de convênio cujo valor de tabela não pôde ser calculado (preço não cadastrado).</summary>
    public int SemPreco => Atendimentos.Count(a => a.GuiasSemPreco > 0);

    /// <summary>As consultas deste profissional por TIPO de atendimento (nov/2026).</summary>
    public IReadOnlyList<ContagemDoRelatorio> PorTipo => AtendimentosPorProfissionalService.ContarPorTipo(Atendimentos);

    /// <summary>Os atendimentos de infertilidade por GUIA INTERNA em que foram lançados (nov/2026).</summary>
    public IReadOnlyList<ContagemDoRelatorio> PorGuiaInterna => AtendimentosPorProfissionalService.ContarPorGuiaInterna(Atendimentos);
}

/// <summary>O relatório inteiro: período e um bloco por profissional.</summary>
public sealed record RelatorioAtendimentosPorProfissional(DateOnly Inicio, DateOnly Fim, IReadOnlyList<ProfissionalNoRelatorio> Profissionais)
{
    public int Quantidade => Profissionais.Sum(p => p.Quantidade);
    public int Particulares => Profissionais.Sum(p => p.Particulares);
    public decimal TotalTabela => Profissionais.Sum(p => p.TotalTabela);
    public decimal TotalRecebido => Profissionais.Sum(p => p.TotalRecebido);
    public int SemPreco => Profissionais.Sum(p => p.SemPreco);

    /// <summary>A clínica inteira por tipo de atendimento e por guia interna da infertilidade.</summary>
    public IReadOnlyList<ContagemDoRelatorio> PorTipo
        => AtendimentosPorProfissionalService.ContarPorTipo(Profissionais.SelectMany(p => p.Atendimentos));
    public IReadOnlyList<ContagemDoRelatorio> PorGuiaInterna
        => AtendimentosPorProfissionalService.ContarPorGuiaInterna(Profissionais.SelectMany(p => p.Atendimentos));
}

/// <summary>
/// RELATÓRIO DE ATENDIMENTOS POR MÉDICO, COM VALORES (nov/2026, pedido da gerência: "para
/// auxiliar na elaboração da folha de pagamento e dos repasses").
///
/// O Repasses responde "quanto pagar" segundo a regra cadastrada; este responde "o que cada
/// um atendeu, atendimento por atendimento, e quanto aquilo vale" — a conferência nominal
/// que a folha precisa e que o total do Repasses não mostra.
///
/// Três decisões, todas herdadas de onde a regra já mora:
///
/// 1. <b>Quem atendeu vem do AGENDAMENTO REALIZADO</b> (<see cref="IClinicaRepositorio.AgendamentosComAtendimentoAsync"/>,
///    o mesmo alimentador do <see cref="RepasseService"/>): o atendimento não guarda profissional,
///    e o horário cancelado que manteve o atendimento não é sessão dada.
/// 2. <b>Dois valores, lado a lado, nunca misturados.</b> O VALOR DE TABELA é o que a
///    operadora paga pelas guias (a tabela do Gerente, <see cref="PrecoConvenioService"/>, com
///    a vigência do dia do atendimento); o RECEBIDO é o que entrou no caixa ligado ao
///    atendimento (o particular, o pacote). Somar os dois contaria duas vezes a mesma sessão.
/// 3. <b>Sem preço cadastrado não se inventa valor</b> — a linha diz "sem preço" e o total diz
///    quantas ficaram de fora, a mesma regra da tabela.
/// </summary>
public sealed class AtendimentosPorProfissionalService
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>O mesmo teto da conferência de lançamentos: um ano de atendimentos numa leitura.</summary>
    public const int MaximoDias = 366;

    private readonly IClinicaRepositorio _repo;
    private readonly PrecoConvenioService _precos;

    public AtendimentosPorProfissionalService(IClinicaRepositorio repo, PrecoConvenioService precos)
    {
        _repo = repo;
        _precos = precos;
    }

    /// <summary>
    /// Lê o período — SEQUENCIAL no mesmo repositório (o mesmo DbContext não aceita duas
    /// operações ao mesmo tempo) — e monta o relatório. <paramref name="profissionalId"/> nulo = todos.
    /// </summary>
    public async Task<RelatorioAtendimentosPorProfissional> GerarAsync(
        DateOnly inicio, DateOnly fim, int? profissionalId = null, CancellationToken ct = default)
    {
        if (fim < inicio) throw new InvalidOperationException("A data final está antes da inicial.");
        if (fim.DayNumber - inicio.DayNumber + 1 > MaximoDias)
            throw new InvalidOperationException($"Escolha um período de até {MaximoDias} dias.");

        var agendamentos = (await _repo.AgendamentosComAtendimentoAsync(inicio, fim, ct))
            .Where(a => profissionalId is null || a.ProfissionalId == profissionalId)
            .ToList();
        if (agendamentos.Count == 0) return new(inicio, fim, []);

        // O atendimento pode ter a data diferente da do horário (lançado no dia seguinte); a
        // janela de leitura dos atendimentos ganha um dia de folga em cada ponta.
        var atendimentos = (await _repo.AtendimentosNoPeriodoAsync(inicio.AddDays(-1), fim.AddDays(1), ct))
            .ToDictionary(a => a.Id);
        var ids = agendamentos.Select(a => a.AtendimentoId!.Value).Distinct().ToList();
        // O atendimento acompanha a data do horário na remarcação, mas o lançado à mão antes
        // de o horário existir (e o histórico de antes da regra) pode ter outra data. Sem esta
        // volta ele sairia do relatório EM SILÊNCIO — e a folha sairia menor com cara de
        // conferida. Normalmente não falta nenhum, e não há ida ao banco a mais.
        foreach (var id in ids.Where(id => !atendimentos.ContainsKey(id)).ToList())
            if (await _repo.ObterAtendimentoAsync(id, ct) is { Estornado: false } avulso)
                atendimentos[id] = avulso;
        var recebidos = (await _repo.LancamentosDosAtendimentosAsync(ids, ct))
            .Where(l => l.Tipo == TipoLancamento.Entrada && l.Status == StatusLancamento.Realizado && l.AtendimentoId is not null)
            .GroupBy(l => l.AtendimentoId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Valor));

        var linhasPorProfissional = new Dictionary<int, (string Nome, List<AtendimentoDoProfissional> Linhas)>();
        // Um atendimento conta UMA vez por profissional, mesmo com dois horários apontando para ele.
        var vistos = new HashSet<(int, int)>();
        foreach (var ag in agendamentos)
        {
            var atendimentoId = ag.AtendimentoId!.Value;
            var prof = ag.ProfissionalId!.Value;
            if (!vistos.Add((prof, atendimentoId))) continue;
            // Estornado: não foi sessão dada — sai do relatório.
            if (!atendimentos.TryGetValue(atendimentoId, out var atendimento)) continue;

            var linha = await MontarLinhaAsync(ag, atendimento, recebidos.GetValueOrDefault(atendimentoId), ct);
            if (!linhasPorProfissional.TryGetValue(prof, out var grupo))
                linhasPorProfissional[prof] = grupo = (ag.Profissional?.Rotulo ?? $"Profissional {prof}", []);
            grupo.Linhas.Add(linha);
        }

        var profissionais = linhasPorProfissional
            .Select(kv => new ProfissionalNoRelatorio(kv.Key, kv.Value.Nome,
                kv.Value.Linhas.OrderBy(l => l.DataHora).ThenBy(l => l.Paciente, StringComparer.Create(PtBr, true)).ToList()))
            .OrderBy(p => p.Profissional, StringComparer.Create(PtBr, true))
            .ToList();
        return new(inicio, fim, profissionais);
    }

    private async Task<AtendimentoDoProfissional> MontarLinhaAsync(
        Agendamento ag, Atendimento atendimento, decimal recebido, CancellationToken ct)
    {
        var paciente = atendimento.Paciente ?? ag.Paciente;
        // Particular pela ficha OU pela marca da sessão (nov/2026: "a paciente tem plano, mas
        // eu não atendo o plano dela") — a mesma pergunta do fechamento e da fila.
        var particular = SessaoParticular.Eh(paciente, atendimento.Particular || ag.Particular);
        // As guias que a operadora PAGA: a dispensada (não aplicável) e a justificada como não
        // conformidade não viram valor de ninguém.
        var guias = atendimento.Codigos
            .Where(c => c.Status is StatusCodigo.Aberto or StatusCodigo.Baixado)
            .OrderBy(c => c.Ordem).ThenBy(c => c.Id)
            .ToList();

        decimal? valor = null;
        var semPreco = 0;
        if (!particular && guias.Count > 0)
        {
            var soma = 0m;
            foreach (var g in guias)
            {
                var preco = await _precos.ResolverAsync(paciente?.ConvenioCodigo ?? paciente?.Convenio.ToString(),
                    g.Tipo, atendimento.Data, g.Especialidade ?? atendimento.EspecialidadeConsulta, ct);
                if (preco is null) semPreco++;
                else soma += preco.Valor;
            }
            valor = semPreco == 0 ? soma : null;
        }

        // O TIPO pelo CATÁLOGO (nov/2026): os tipos novos — Infertilidade, Suplementação para
        // gestante — não estão no enum, e ler só o enum os deixaria fora do relatório, em silêncio.
        var codigoDoTipo = atendimento.EspecialidadeConsultaCodigo ?? atendimento.EspecialidadeConsulta?.ToString()
                           ?? ag.EspecialidadeConsultaCodigo ?? ag.EspecialidadeConsulta?.ToString();
        var tipo = atendimento.Modalidade == ModalidadeAtendimento.Consulta && codigoDoTipo is not null
            ? CatalogoEspecialidades.Nome(codigoDoTipo) : string.Empty;
        var guiaInterna = ag.EhInfertilidade ? CatalogoEspecialidades.Nome(ag.InfertilidadeGuiaDe) : null;
        var especialidade = tipo.Length > 0 ? $" · {tipo}" : string.Empty;
        return new AtendimentoDoProfissional(
            ag.DataHora,
            atendimento.Id,
            atendimento.Numero,
            paciente?.Id ?? atendimento.PacienteId,
            paciente?.Nome ?? "(paciente não encontrado)",
            particular ? "Particular" : paciente?.ConvenioNome ?? "—",
            particular,
            CatalogoModalidades.Nome(atendimento.ModalidadeCodigo, atendimento.Modalidade) + especialidade,
            guias.Select(g => $"{RotulosEnum.De(g.Tipo)} · {RotulosEnum.De(g.Ordem)}").ToList(),
            valor,
            semPreco,
            recebido)
        {
            TipoDeAtendimento = tipo,
            GuiaInterna = guiaInterna
        };
    }

    // ==================== por tipo de atendimento e por guia interna (nov/2026) ====================

    /// <summary>
    /// As CONSULTAS por tipo de atendimento, do mais frequente para o menos. Só consulta tem
    /// tipo; o que não é consulta fica de fora desta conta (e continua no total do relatório).
    /// </summary>
    public static IReadOnlyList<ContagemDoRelatorio> ContarPorTipo(IEnumerable<AtendimentoDoProfissional> atendimentos)
        => Contar(atendimentos.Where(a => a.TipoDeAtendimento.Length > 0), a => a.TipoDeAtendimento);

    /// <summary>Os atendimentos de infertilidade por guia interna em que foram lançados.</summary>
    public static IReadOnlyList<ContagemDoRelatorio> ContarPorGuiaInterna(IEnumerable<AtendimentoDoProfissional> atendimentos)
        => Contar(atendimentos.Where(a => a.GuiaInterna is not null), a => a.GuiaInterna!);

    private static IReadOnlyList<ContagemDoRelatorio> Contar(
        IEnumerable<AtendimentoDoProfissional> atendimentos, Func<AtendimentoDoProfissional, string> chave)
        => atendimentos.GroupBy(chave, StringComparer.Create(PtBr, true))
            .Select(g => new ContagemDoRelatorio(g.Key, g.Count(), g.Count(a => a.Particular), g.Sum(a => a.Recebido)))
            .OrderByDescending(c => c.Quantidade).ThenBy(c => c.Nome, StringComparer.Create(PtBr, true))
            .ToList();

    /// <summary>"Infertilidade 4 · Ginecologia 3 · Suplementação para gestante 1" — a linha do papel.</summary>
    public static string ResumoDasContagens(IReadOnlyList<ContagemDoRelatorio> contagens)
        => string.Join(" · ", contagens.Select(c => $"{c.Nome} {c.Quantidade.ToString(PtBr)}"));

    /// <summary>O procedimento com a guia interna ao lado, quando há — a linha do PDF e do detalhe.</summary>
    public static string ProcedimentoComGuiaInterna(AtendimentoDoProfissional a)
        => a.GuiaInterna is null ? a.Procedimento : $"{a.Procedimento} (guia interna: {a.GuiaInterna})";

    // ==================== o que a tela, o CSV e o papel escrevem ====================

    /// <summary>"R$ 1.234,56", ou a frase que assume a lacuna — nunca "R$ 0,00" por falta de preço.</summary>
    public static string ValorDeTabela(AtendimentoDoProfissional a) => a switch
    {
        { Particular: true } => "particular",
        { Guias.Count: 0 } => "sem guia",
        { ValorTabela: { } v } => v.ToString("C", PtBr),
        _ => a.GuiasSemPreco == 1 ? "sem preço (1 guia)" : $"sem preço ({a.GuiasSemPreco} guias)"
    };

    public static string Dinheiro(decimal v) => v.ToString("C", PtBr);

    public static string Recorte(RelatorioAtendimentosPorProfissional r, string? profissional)
        => $"Atendimentos realizados de {r.Inicio.ToString("dd/MM/yyyy", PtBr)} a {r.Fim.ToString("dd/MM/yyyy", PtBr)}"
           + (string.IsNullOrWhiteSpace(profissional) ? " · todos os profissionais" : $" · {profissional}");

    /// <summary>
    /// A ressalva que acompanha o total: o valor de tabela de quem tem guia sem preço está
    /// incompleto, e o papel precisa dizer — senão a folha sai menor com cara de conferida.
    /// </summary>
    public static string? RessalvaSemPreco(int semPreco)
        => semPreco == 0 ? null
            : $"{semPreco} atendimento(s) de convênio sem preço cadastrado para alguma guia — o valor de tabela deles não entra no total. Cadastre o preço em Gerente › Preços por convênio.";

    public static readonly IReadOnlyList<string> CabecalhoCsv =
        ["Profissional", "Data", "Hora", "Paciente", "Convênio", "Procedimento", "Guias", "Valor de tabela", "Recebido no caixa", "Nº atendimento",
         // No FIM (nov/2026): quem já tem planilha montada sobre as dez primeiras não a perde.
         "Tipo de atendimento", "Guia interna (infertilidade)"];

    public static IReadOnlyList<IReadOnlyList<string>> LinhasCsv(RelatorioAtendimentosPorProfissional r)
        => r.Profissionais.SelectMany(p => p.Atendimentos.Select(a => (IReadOnlyList<string>)
        [
            p.Profissional,
            a.DataHora.ToString("dd/MM/yyyy", PtBr),
            a.DataHora.ToString("HH:mm", PtBr),
            a.Paciente,
            a.Convenio,
            a.Procedimento,
            a.Guias.Count == 0 ? "—" : string.Join(" + ", a.Guias),
            // Número puro na planilha (o Excel soma), a lacuna por extenso.
            a.ValorTabela is { } v && !a.Particular ? v.ToString("0.00", PtBr) : ValorDeTabela(a),
            a.Recebido.ToString("0.00", PtBr),
            a.NumeroAtendimento ?? string.Empty,
            a.TipoDeAtendimento,
            a.GuiaInterna ?? string.Empty
        ])).ToList();

    public static string NomeArquivo(DateOnly inicio, DateOnly fim, string extensao)
        => $"Atendimentos-por-profissional-{inicio:yyyy-MM-dd}-a-{fim:yyyy-MM-dd}{extensao}";

    private static readonly IReadOnlyList<string> ColunasPdf =
        ["Data", "Paciente", "Convênio", "Procedimento", "Guias", "Valor de tabela", "Recebido"];
    private static readonly IReadOnlyList<float> PesosPdf = [1.4f, 3.2f, 2f, 2.6f, 2.6f, 1.6f, 1.4f];

    /// <summary>O relatório impresso: um bloco por profissional, com o total ao pé de cada um.</summary>
    public static byte[] Pdf(RelatorioAtendimentosPorProfissional r, string? profissional, string? nomeClinica)
    {
        var secoes = r.Profissionais.Select(p => new SecaoRelatorioPdf(
            $"{p.Profissional} — {p.Quantidade} atendimento(s), {p.Particulares} particular(es), {p.PacientesDistintos} paciente(s)",
            ColunasPdf, PesosPdf,
            p.Atendimentos.Select(a => (IReadOnlyList<string>)
            [
                a.DataHora.ToString("dd/MM/yyyy HH:mm", PtBr),
                a.Paciente,
                a.Convenio,
                ProcedimentoComGuiaInterna(a),
                a.Guias.Count == 0 ? "—" : string.Join(" + ", a.Guias),
                ValorDeTabela(a),
                Dinheiro(a.Recebido)
            ]).ToList(),
            ["Total", "", "", "", "", Dinheiro(p.TotalTabela) + (p.SemPreco > 0 ? " *" : ""), Dinheiro(p.TotalRecebido)])).ToList();

        var recorte = Recorte(r, profissional);
        if (r.PorTipo.Count > 0) recorte += $"  ·  Consultas por tipo: {ResumoDasContagens(r.PorTipo)}";
        if (r.PorGuiaInterna.Count > 0) recorte += $"  ·  Infertilidade por guia interna: {ResumoDasContagens(r.PorGuiaInterna)}";
        if (RessalvaSemPreco(r.SemPreco) is { } ressalva) recorte += $"  ·  * {ressalva}";

        return RelatorioListaPdf.Gerar(
            nomeClinica ?? string.Empty,
            "Atendimentos por profissional",
            recorte,
            [
                new("Atendimentos", r.Quantidade.ToString(PtBr)),
                new("Particulares", r.Particulares.ToString(PtBr)),
                new("Profissionais", r.Profissionais.Count.ToString(PtBr)),
                new("Valor de tabela (convênios)", Dinheiro(r.TotalTabela)),
                new("Recebido no caixa", Dinheiro(r.TotalRecebido)),
            ],
            secoes,
            "Nenhum atendimento realizado no período.");
    }
}
