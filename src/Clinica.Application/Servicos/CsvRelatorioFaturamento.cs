using System.Globalization;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>
/// O CSV dos Relatórios de faturamento (out/2026, subiu do RelatoriosViewModel do WPF quando
/// a tela virou Blazor): o mesmo arquivo de seções — resumo, por convênio, consultas por
/// especialidade, evolução mensal, envelhecimento e não conformidades —, agora com número e
/// data em pt-BR FIXO (o WPF usava a cultura da máquina, e dois postos escreveriam "66,7" e
/// "66.7" na mesma coluna).
///
/// O nome do convênio vem do CATÁLOGO e o tipo pelo rótulo, nunca o enum: este arquivo sai
/// da clínica para o Excel da direção (parcelas 41 e 50). O escape do separador fica com quem
/// grava (ExportacaoCsv), que põe aspas — a justificativa não é mais mutilada trocando ";".
/// </summary>
public static class CsvRelatorioFaturamento
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>O arquivo: o cabeçalho (título e período) e as linhas de todas as seções.</summary>
    public static (IReadOnlyList<string> Cabecalho, IReadOnlyList<IReadOnlyList<string>> Linhas) Montar(
        RelatorioFaturamento rel, IReadOnlyList<ResumoMensal> comparativo)
    {
        var cabecalho = new[] { "Relatório de faturamento", $"{Data(rel.Inicio)} a {Data(rel.Fim)}" };
        var linhas = new List<IReadOnlyList<string>>();
        void L(params string[] celulas) => linhas.Add(celulas);

        L();
        L("Resumo", "Códigos gerados", "Baixados", "Pendentes", "Não conformidades", "Taxa de baixa (%)", "Glosadas", "Taxa de glosa (%)", "Tempo médio de baixa (dias)");
        var r = rel.Resumo;
        L("", N(r.TotalCodigos), N(r.Baixados), N(r.Pendentes), N(r.NaoConformidades), Taxa(r.TaxaBaixa), N(r.Glosadas), Taxa(r.TaxaGlosa), Taxa(r.TempoMedioBaixaDias));

        L();
        L("Faturamento por convênio");
        L("Convênio", "Gerados", "Baixados", "Pendentes", "Não conformidades", "Taxa de baixa (%)", "Glosadas", "Taxa de glosa (%)", "Tempo médio de baixa (dias)");
        foreach (var c in rel.PorConvenio)
            L(c.ConvenioNome, N(c.TotalCodigos), N(c.Baixados), N(c.Pendentes), N(c.NaoConformidades), Taxa(c.TaxaBaixa), N(c.Glosadas), Taxa(c.TaxaGlosa), Taxa(c.TempoMedioBaixaDias));

        if (rel.ConsultasEspecialidades.Count > 0)
        {
            L();
            L("Consultas por especialidade");
            L("Especialidade", "Consultas", "Baixadas");
            foreach (var c in rel.ConsultasEspecialidades)
                L(c.Especialidade, N(c.Quantidade), N(c.Baixadas));
        }

        L();
        L("Evolução mensal (últimos 6 meses)");
        L("Mês", "Gerados", "Baixados", "Pendentes", "Taxa de baixa (%)");
        foreach (var m in comparativo)
            L(m.Rotulo, N(m.TotalCodigos), N(m.Baixados), N(m.Pendentes), Taxa(m.TaxaBaixa));

        L();
        L("Pendências em aberto (envelhecimento)");
        L("Faixa", "Quantidade");
        foreach (var f in rel.Envelhecimento)
            L(f.Faixa, N(f.Quantidade));

        if (rel.NaoConformidades.Count > 0)
        {
            L();
            L("Não conformidades (guias justificadas na rodada)");
            L("Paciente", "Convênio", "Tipo", "Data prevista", "Justificativa", "Registrada em");
            foreach (var n in rel.NaoConformidades)
                L(n.PacienteNome, CatalogoConvenios.Nome(n.ConvenioCodigo, n.Convenio), RotulosEnum.De(n.Tipo),
                  Data(n.DataPrevista), n.Justificativa ?? string.Empty,
                  n.Em is { } em ? em.ToString("dd/MM/yyyy HH:mm", PtBr) : string.Empty);
        }

        return (cabecalho, linhas);
    }

    /// <summary>Uma taxa ou média com uma casa no máximo ("66,7", "50"); nula fica em branco.</summary>
    public static string Taxa(double? valor) => valor is { } v ? v.ToString("0.#", PtBr) : string.Empty;

    /// <summary>O nome do arquivo, pelo período.</summary>
    public static string NomeDoArquivo(DateOnly inicio, DateOnly fim)
        => $"Relatorio-Faturamento-{inicio:yyyy-MM-dd}-a-{fim:yyyy-MM-dd}.csv";

    private static string N(int valor) => valor.ToString(PtBr);
    private static string Data(DateOnly d) => d.ToString("dd/MM/yyyy", PtBr);
}
