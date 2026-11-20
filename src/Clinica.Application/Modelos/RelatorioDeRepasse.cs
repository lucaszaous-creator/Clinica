using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

// ============================================================================================
// RELATÓRIO DE REPASSE (nov/2026) — o que a clínica EXPORTA PARA OS MÉDICOS.
//
// Lê só FECHAMENTOS GRAVADOS (conferidos), nunca a prévia: o papel que vai ao médico precisa
// ser o que alguém conferiu, e número de prévia muda a cada conciliação. Cancelado só entra
// quando o filtro pede, e sai marcado.
// ============================================================================================

/// <summary>Quais fechamentos entram.</summary>
public enum SituacaoNoRelatorioRepasse
{
    /// <summary>Conferidos e pagos (o padrão — tudo o que vale).</summary>
    ConferidosEPagos,
    SoPagos,
    /// <summary>Conferidos ainda não pagos (o que a clínica deve).</summary>
    SoAPagar,
    /// <summary>Tudo, inclusive cancelados (para auditoria; cancelado sai marcado e fora do total).</summary>
    IncluirCancelados
}

/// <summary>O filtro da tela. Competências pelo primeiro dia do mês, inclusive nas duas pontas.</summary>
public sealed record FiltroRelatorioRepasse(
    DateOnly CompetenciaDe,
    DateOnly CompetenciaAte,
    int? ProfissionalId = null,
    /// <summary>Código do convênio no catálogo ("PARTICULAR" para o particular). Nulo = todos.</summary>
    string? ConvenioCodigo = null,
    SituacaoNoRelatorioRepasse Situacao = SituacaoNoRelatorioRepasse.ConferidosEPagos,
    /// <summary>Trecho do nome do paciente (sem acento atrapalhar). Nulo/vazio = todos.</summary>
    string? Paciente = null);

/// <summary>Uma linha: um item de um fechamento, com o fechamento ao lado.</summary>
public sealed record LinhaRelatorioRepasse(
    int FechamentoId,
    int ProfissionalId,
    string Profissional,
    string? Registro,
    DateOnly Competencia,
    /// <summary>"Conferido", "Pago" ou "Cancelado".</summary>
    string Situacao,
    bool Cancelado,
    DateTime ConferidoEm,
    string ConferidoPor,
    DateOnly? PagoEm,
    OrigemItemRepasse Origem,
    NaturezaItemRepasse Natureza,
    string Paciente,
    DateOnly DataAtendimento,
    string ConvenioCodigo,
    string Convenio,
    string Descricao,
    string? NumeroGuia,
    string? ReferenciaPagamento,
    DateOnly DataRecebimento,
    decimal ValorRecebido,
    string Regra,
    decimal Valor);

/// <summary>Uma linha do resumo: médico × convênio, somando as competências do filtro.</summary>
public sealed record ResumoRelatorioRepasse(
    int ProfissionalId,
    string Profissional,
    string Convenio,
    int Quantidade,
    decimal? ValorUnitario,
    decimal Total);

/// <summary>Totais de um médico no filtro (o cabeçalho do PDF dele e a linha da tela).</summary>
public sealed record TotalProfissionalRelatorioRepasse(
    int ProfissionalId,
    string Profissional,
    string? Registro,
    int Fechamentos,
    /// <summary>Itens de crédito (consultas/guias) de fechamentos não cancelados.</summary>
    int Quantidade,
    decimal Total,
    decimal Pago,
    decimal APagar);

/// <summary>O relatório pronto: as linhas filtradas e os agregados que a tela e os arquivos mostram.</summary>
public sealed record RelatorioRepasse(
    FiltroRelatorioRepasse Filtro,
    string Clinica,
    IReadOnlyList<LinhaRelatorioRepasse> Linhas,
    IReadOnlyList<TotalProfissionalRelatorioRepasse> PorProfissional,
    IReadOnlyList<ResumoRelatorioRepasse> Resumo,
    int Quantidade,
    decimal Total,
    decimal Pago,
    decimal APagar);

/// <summary>Um arquivo gerado para exportar.</summary>
public sealed record ArquivoDoRelatorioRepasse(byte[] Conteudo, string NomeArquivo);
