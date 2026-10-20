using System.Globalization;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// O que a seção "Exames e anexos" da tela do paciente afirma (out/2026: subido da
/// AnexosPacienteViewModel e da ResultadoExameEdicaoViewModel WPF na migração para Blazor).
/// Puro, com teste.
/// </summary>
public static class ExamesDoPacienteNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public const string Aguardando = "Aguardando";
    public const string Resultados = "Resultados";
    public const string Arquivos = "Arquivos";
    public const string Sessoes = "Sessoes";

    public sealed record Resultado(int Id, string Data, string Nome, string Valor, string? Contexto, string? Observacoes, string? ArquivoNome)
    {
        public bool TemArquivo => !string.IsNullOrWhiteSpace(ArquivoNome);
    }

    public sealed record ArquivoDaFicha(int Id, string Titulo, string NomeArquivo, string Contexto, string? Observacoes);

    /// <summary>Um item do combo "responde ao pedido" do formulário de resultado.</summary>
    public sealed record OpcaoPedido(int? DocumentoId, string Rotulo);

    public static Resultado ResultadoDe(ResultadoExame r) => new(
        r.Id, r.Data.ToString("dd/MM/yyyy", PtBr), r.Nome, r.ResumoDoResultado,
        string.Join(" · ", new[] { string.IsNullOrWhiteSpace(r.Referencia) ? null : $"ref.: {r.Referencia}", r.Laboratorio }
            .Where(p => !string.IsNullOrWhiteSpace(p))) is { Length: > 0 } c ? c : null,
        r.Observacoes, r.ArquivoNome);

    public static ArquivoDaFicha ArquivoDe(AnexoPaciente a) => new(
        a.Id, a.Titulo, a.NomeArquivo,
        string.Join(" · ", new[] { a.Data.ToString("dd/MM/yyyy", PtBr), a.NomeArquivo, a.TamanhoLegivel, a.Importado ? "sistema anterior" : null }
            .Where(p => !string.IsNullOrWhiteSpace(p))),
        a.Observacoes);

    /// <summary>A linha de cima: só o que existe, unido por " · ".</summary>
    public static string Resumo(int aguardando, int resultados, int arquivos, int anexosDeSessao)
    {
        var partes = new List<string>();
        if (aguardando > 0) partes.Add($"{aguardando} pedido(s) aguardando resultado");
        if (resultados > 0) partes.Add($"{resultados} resultado(s) registrado(s)");
        if (arquivos > 0) partes.Add($"{arquivos} arquivo(s) da ficha");
        if (anexosDeSessao > 0) partes.Add($"{anexosDeSessao} arquivo(s) anexado(s) a sessões");
        return partes.Count == 0 ? "Nenhum exame nem arquivo deste paciente ainda." : string.Join(" · ", partes);
    }

    /// <summary>Abre no que pede ação (pedido aguardando); sem isso, nos resultados.</summary>
    public static string SecaoInicial(int aguardando) => aguardando > 0 ? Aguardando : Resultados;

    /// <summary>
    /// O combo do formulário: o resultado avulso primeiro, depois os pedidos vivos do paciente —
    /// dizendo quando o pedido já tem resultado (um pedido de vários exames recebe vários laudos).
    /// </summary>
    public static IReadOnlyList<OpcaoPedido> OpcoesDePedido(IEnumerable<PedidoDeExameLinha> pedidos)
    {
        var lista = new List<OpcaoPedido> { new(null, "— resultado avulso (sem pedido) —") };
        lista.AddRange(pedidos.Where(p => !p.Cancelado).Select(p => new OpcaoPedido(p.DocumentoId,
            p.ResultadosVinculados > 0 ? $"{p.RotuloDoCombo} (já tem {p.ResultadosVinculados})" : p.RotuloDoCombo)));
        return lista;
    }

    /// <summary>O tipo do laudo pela extensão; desconhecido = nulo (o serviço decide).</summary>
    public static string? TipoDoLaudo(string nomeDoArquivo) => Path.GetExtension(nomeDoArquivo).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        _ => null
    };

    public static string Peso(int bytes) => bytes >= 1024 * 1024
        ? $"{(bytes / 1024d / 1024d).ToString("0.#", PtBr)} MB"
        : $"{Math.Max(1, bytes / 1024)} KB";

    /// <summary>A data do documento anexado à ficha: em branco = hoje; dd/mm/aaaa; outro formato = nulo (recusa).</summary>
    public static DateOnly? LerData(string? texto, DateOnly hoje)
    {
        if (string.IsNullOrWhiteSpace(texto)) return hoje;
        return DateOnly.TryParseExact(texto.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d : null;
    }
}
