using System.Globalization;
using Clinica.Application.Formatacao;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Modelos;

/// <summary>
/// O filtro do RELATÓRIO DE ATENDIMENTOS (nov/2026 — "quais pacientes são de BSV do mês e quais são os
/// convênios deles"). Cada campo nulo é "todos". Período pela DATA DO HORÁRIO.
/// </summary>
/// <param name="Modalidade">Código do catálogo de modalidades (BSV, acupuntura, consulta e as personalizadas).</param>
/// <param name="Especialidade">Código do catálogo de especialidades (o tipo da consulta).</param>
/// <param name="Convenio">Código do catálogo de convênios, ou <see cref="RelatorioDeAtendimentosRegras.Particular"/>.</param>
/// <param name="Situacoes">Realizado, falta, cancelado (e "em aberto"); vazio = as três primeiras.</param>
/// <param name="Paciente">Parte do nome.</param>
public sealed record FiltroRelatorioAtendimentos(
    DateOnly Inicio,
    DateOnly Fim,
    string? Modalidade = null,
    string? Especialidade = null,
    string? Convenio = null,
    int? ProfissionalId = null,
    IReadOnlyList<StatusAgendamento>? Situacoes = null,
    string? Paciente = null)
{
    /// <summary>As situações que valem: as escolhidas, ou realizado, falta e cancelado.</summary>
    public IReadOnlyList<StatusAgendamento> SituacoesEfetivas
        => Situacoes is { Count: > 0 } s ? s : RelatorioDeAtendimentosRegras.SituacoesPadrao;
}

/// <summary>Uma guia (código de faturamento) do atendimento, como sai do banco.</summary>
public sealed record GuiaDoRelatorio(TipoCodigo Tipo, OrdemCodigo Ordem, StatusCodigo Status, string? NumeroGuia, DateOnly? DataBaixa);

/// <summary>
/// A linha como sai do banco — só colunas, sem regra (o nome do convênio, a modalidade pelo catálogo e o
/// particular são do <see cref="RelatorioDeAtendimentosRegras"/>). Nada clínico: nem evolução, nem diagnóstico.
/// </summary>
public sealed record LinhaBrutaRelatorioAtendimento(
    int AgendamentoId,
    DateTime DataHora,
    int PacienteId,
    string Paciente,
    Convenio ConvenioFamilia,
    string? ConvenioCodigo,
    string? Carteirinha,
    bool HorarioParticular,
    ModalidadeAtendimento ModalidadeFamilia,
    string? ModalidadeCodigo,
    Especialidade? EspecialidadeEnum,
    string? EspecialidadeCodigo,
    string? Profissional,
    StatusAgendamento Situacao,
    string? NumeroAtendimento,
    IReadOnlyList<GuiaDoRelatorio> Guias);

/// <summary>Uma linha do relatório, pronta para a tela e para a planilha.</summary>
public sealed record LinhaRelatorioAtendimento(
    int AgendamentoId,
    DateTime DataHora,
    int PacienteId,
    string Paciente,
    string Convenio,
    bool Particular,
    string? Carteirinha,
    string Modalidade,
    string? Especialidade,
    string? Profissional,
    StatusAgendamento Situacao,
    string? NumeroAtendimento,
    IReadOnlyList<GuiaDoRelatorio> Guias)
{
    public string SituacaoTexto => RelatorioDeAtendimentosRegras.Situacao(Situacao);
    public string GuiasTexto => RelatorioDeAtendimentosRegras.GuiasTexto(Guias);
    public string BaixaTexto => RelatorioDeAtendimentosRegras.BaixaTexto(Guias);
}

/// <summary>O relatório: o filtro e as linhas, com os dois totais que a lista mostra.</summary>
public sealed record RelatorioDeAtendimentos(FiltroRelatorioAtendimentos Filtro, IReadOnlyList<LinhaRelatorioAtendimento> Linhas)
{
    public int Atendimentos => Linhas.Count;
    public int PacientesDistintos => Linhas.Select(l => l.PacienteId).Distinct().Count();
}

/// <summary>
/// As regras do relatório de atendimentos — o que a tela, a planilha e a trilha AFIRMAM mora aqui, com teste.
/// </summary>
public static class RelatorioDeAtendimentosRegras
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>O valor do filtro "Particular" (não é um código de convênio).</summary>
    public const string Particular = "particular";

    /// <summary>O mesmo teto das outras conferências: um ano por leitura.</summary>
    public const int MaximoDias = 366;

    public static IReadOnlyList<StatusAgendamento> SituacoesPadrao { get; } =
        [StatusAgendamento.Realizado, StatusAgendamento.Faltou, StatusAgendamento.Cancelado];

    /// <summary>As situações que o filtro oferece, na ordem da tela.</summary>
    public static IReadOnlyList<StatusAgendamento> SituacoesDoFiltro { get; } =
        [StatusAgendamento.Realizado, StatusAgendamento.Faltou, StatusAgendamento.Cancelado, StatusAgendamento.Agendado];

    /// <summary>Recusa o período impossível antes de ir ao banco; nulo = válido.</summary>
    public static string? Recusa(FiltroRelatorioAtendimentos f)
        => f.Fim < f.Inicio ? "A data final está antes da inicial."
            : f.Fim.DayNumber - f.Inicio.DayNumber + 1 > MaximoDias ? $"Escolha um período de até {MaximoDias} dias."
            : null;

    /// <summary>"Este mês" e "mês passado": do dia 1 ao último dia.</summary>
    public static (DateOnly Inicio, DateOnly Fim) EsteMes(DateOnly hoje)
    {
        var inicio = new DateOnly(hoje.Year, hoje.Month, 1);
        return (inicio, inicio.AddMonths(1).AddDays(-1));
    }

    public static (DateOnly Inicio, DateOnly Fim) MesPassado(DateOnly hoje) => EsteMes(new DateOnly(hoje.Year, hoje.Month, 1).AddDays(-1));

    public static string Situacao(StatusAgendamento s) => s switch
    {
        StatusAgendamento.Realizado => "Realizado",
        StatusAgendamento.Faltou => "Falta",
        StatusAgendamento.Cancelado => "Cancelado",
        StatusAgendamento.Agendado => "Em aberto",
        _ => RotulosEnum.De(s)
    };

    /// <summary>A linha pronta: convênio pelo catálogo (particular pela ficha ou pela marca do horário), modalidade e especialidade pelos catálogos.</summary>
    public static LinhaRelatorioAtendimento Montar(LinhaBrutaRelatorioAtendimento b)
    {
        var codigoConvenio = b.ConvenioCodigo ?? b.ConvenioFamilia.ToString();
        var particular = b.HorarioParticular || !CatalogoConvenios.GeraGuia(codigoConvenio);
        var especialidade = b.EspecialidadeCodigo ?? b.EspecialidadeEnum?.ToString();
        return new LinhaRelatorioAtendimento(
            b.AgendamentoId, b.DataHora, b.PacienteId, b.Paciente,
            particular ? "Particular" : CatalogoConvenios.Nome(b.ConvenioCodigo, b.ConvenioFamilia),
            particular,
            string.IsNullOrWhiteSpace(b.Carteirinha) ? null : b.Carteirinha.Trim(),
            CatalogoModalidades.Nome(b.ModalidadeCodigo, b.ModalidadeFamilia),
            especialidade is null ? null : CatalogoEspecialidades.Nome(especialidade),
            b.Profissional, b.Situacao, b.NumeroAtendimento,
            b.Guias.OrderBy(g => g.Ordem).ToList());
    }

    /// <summary>
    /// O convênio pedido no filtro, depois do banco: "Particular" pega a marca do horário e a ficha cujo
    /// convênio não gera guia; um convênio pega só quem NÃO foi atendido como particular.
    /// </summary>
    public static bool BateConvenio(LinhaRelatorioAtendimento linha, string? convenio)
        => string.IsNullOrWhiteSpace(convenio) || (convenio == Particular ? linha.Particular : !linha.Particular);

    /// <summary>"Consulta · 1º (nº 123456); Consulta · 2º" — as guias do atendimento.</summary>
    public static string GuiasTexto(IReadOnlyList<GuiaDoRelatorio> guias)
        => guias.Count == 0 ? string.Empty
            : string.Join("; ", guias.Select(g => $"{RotulosEnum.De(g.Tipo)} · {RotulosEnum.De(g.Ordem)}"
                + (string.IsNullOrWhiteSpace(g.NumeroGuia) ? string.Empty : $" (nº {g.NumeroGuia.Trim()})")));

    /// <summary>"Baixada em 05/11"; "1 de 2 baixadas"; "Em aberto"; vazio sem guia.</summary>
    public static string BaixaTexto(IReadOnlyList<GuiaDoRelatorio> guias)
    {
        var cobraveis = guias.Where(g => g.Status is StatusCodigo.Aberto or StatusCodigo.Baixado).ToList();
        if (cobraveis.Count == 0) return guias.Count == 0 ? string.Empty : "Sem guia a baixar";
        var baixadas = cobraveis.Where(g => g.Status == StatusCodigo.Baixado).ToList();
        if (baixadas.Count == 0) return "Em aberto";
        if (baixadas.Count < cobraveis.Count) return $"{baixadas.Count} de {cobraveis.Count} baixadas";
        var ultima = baixadas.Max(g => g.DataBaixa);
        return ultima is { } d ? $"Baixada em {d.ToString("dd/MM/yyyy", PtBr)}" : "Baixada";
    }

    // ==================== a planilha ====================

    /// <summary>As colunas da planilha, na ordem pedida.</summary>
    public static IReadOnlyList<string> Colunas { get; } =
    [
        "Data", "Hora", "Paciente", "Convênio", "Carteirinha", "Modalidade / tipo", "Especialidade",
        "Profissional", "Situação", "Nº do atendimento", "Guias / códigos", "Qtd. de guias", "Status da baixa"
    ];

    /// <summary>A linha da planilha com o TIPO de cada célula: data como data, hora como hora, quantidade como número.</summary>
    public static IReadOnlyList<CelulaXlsx> Celulas(LinhaRelatorioAtendimento l) =>
    [
        CelulaXlsx.Data(DateOnly.FromDateTime(l.DataHora)),
        CelulaXlsx.Hora(TimeOnly.FromDateTime(l.DataHora)),
        CelulaXlsx.Texto(l.Paciente),
        CelulaXlsx.Texto(l.Convenio),
        CelulaXlsx.Texto(l.Carteirinha),
        CelulaXlsx.Texto(l.Modalidade),
        CelulaXlsx.Texto(l.Especialidade),
        CelulaXlsx.Texto(l.Profissional),
        CelulaXlsx.Texto(l.SituacaoTexto),
        CelulaXlsx.Texto(l.NumeroAtendimento),
        CelulaXlsx.Texto(l.GuiasTexto),
        CelulaXlsx.Numero(l.Guias.Count),
        CelulaXlsx.Texto(l.BaixaTexto)
    ];

    public static string NomeArquivo(FiltroRelatorioAtendimentos f)
        => $"atendimentos-{f.Inicio:yyyy-MM-dd}-a-{f.Fim:yyyy-MM-dd}.xlsx";

    /// <summary>O detalhe da linha da trilha: o filtro inteiro e quantas linhas saíram (cabe nos 500 da coluna).</summary>
    public static string DetalheDaTrilha(FiltroRelatorioAtendimentos f, int linhas, string? profissional)
    {
        var partes = new List<string>
        {
            $"Período {f.Inicio.ToString("dd/MM/yyyy", PtBr)} a {f.Fim.ToString("dd/MM/yyyy", PtBr)}",
            $"situação {string.Join("/", f.SituacoesEfetivas.Select(Situacao))}"
        };
        if (!string.IsNullOrWhiteSpace(f.Modalidade)) partes.Add($"modalidade {CatalogoModalidades.Nome(f.Modalidade)}");
        if (!string.IsNullOrWhiteSpace(f.Especialidade)) partes.Add($"especialidade {CatalogoEspecialidades.Nome(f.Especialidade)}");
        if (!string.IsNullOrWhiteSpace(f.Convenio)) partes.Add($"convênio {(f.Convenio == Particular ? "Particular" : CatalogoConvenios.Nome(f.Convenio))}");
        if (f.ProfissionalId is not null) partes.Add($"profissional {profissional ?? f.ProfissionalId.ToString()}");
        if (!string.IsNullOrWhiteSpace(f.Paciente)) partes.Add("busca por paciente");
        var texto = $"Relatório de atendimentos exportado em .xlsx: {linhas} linha(s). {string.Join("; ", partes)}.";
        return texto.Length <= 500 ? texto : texto[..497] + "...";
    }

    /// <summary>"132 atendimentos · 97 pacientes" — o resumo acima da lista.</summary>
    public static string Resumo(RelatorioDeAtendimentos r)
        => $"{r.Atendimentos.ToString(PtBr)} atendimento{(r.Atendimentos == 1 ? "" : "s")} · "
           + $"{r.PacientesDistintos.ToString(PtBr)} paciente{(r.PacientesDistintos == 1 ? "" : "s")}";
}
