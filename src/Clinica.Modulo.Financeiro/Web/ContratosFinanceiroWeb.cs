namespace Clinica.Financeiro.Web;

// Contratos de apresentação: serializar com JsonNamingPolicy.CamelCase.
// Nenhum caminho de propriedade/comando recebido do navegador é refletido no modelo.
public sealed record OpcaoFinanceiroDto(string Valor, string Rotulo);
public sealed record CampoFinanceiroDto(string Chave, string Rotulo, string Tipo, object? Valor,
    IReadOnlyList<OpcaoFinanceiroDto> Opcoes, bool Visivel = true, bool Habilitado = true,
    bool Obrigatorio = false, string? Ajuda = null);
public sealed record AcaoFinanceiroDto(string Chave, string Rotulo, bool Habilitada = true,
    string Estilo = "secundario", bool Visivel = true);
public sealed record IndicadorFinanceiroDto(string Rotulo, string Valor, string? Detalhe = null);
public sealed record ColunaFinanceiroDto(string Chave, string Rotulo, string Tipo = "texto");
public sealed record LinhaFinanceiroDto(string Id, IReadOnlyDictionary<string, string> Celulas,
    IReadOnlyList<CampoFinanceiroDto> Campos, IReadOnlyList<AcaoFinanceiroDto> Acoes, bool Selecionada = false);
public sealed record TabelaFinanceiroDto(string Chave, string Titulo, IReadOnlyList<ColunaFinanceiroDto> Colunas,
    IReadOnlyList<LinhaFinanceiroDto> Linhas, string Vazio = "Nenhum registro neste período.");
public sealed record PontoFinanceiroDto(string Rotulo, double? Valor, string ValorFormatado);
public sealed record GraficoFinanceiroDto(string Chave, string Rotulo, string Tipo, string Unidade,
    IReadOnlyList<PontoFinanceiroDto> Pontos);
public sealed record SecaoFinanceiroDto(string Chave, string Titulo, string? Descricao,
    IReadOnlyList<CampoFinanceiroDto> Campos, IReadOnlyList<IndicadorFinanceiroDto> Indicadores,
    IReadOnlyList<TabelaFinanceiroDto> Tabelas, IReadOnlyList<AcaoFinanceiroDto> Acoes,
    IReadOnlyList<GraficoFinanceiroDto>? Graficos = null);
public sealed record PaginaFinanceiroDto(string Chave, string Titulo, string? Subtitulo,
    IReadOnlyList<CampoFinanceiroDto> Campos, IReadOnlyList<IndicadorFinanceiroDto> Indicadores,
    IReadOnlyList<SecaoFinanceiroDto> Secoes, IReadOnlyList<AcaoFinanceiroDto> Acoes,
    bool Carregando, bool NaoVerificado, string? Mensagem, bool MensagemEhErro, bool Truncado,
    string Contexto = "");
