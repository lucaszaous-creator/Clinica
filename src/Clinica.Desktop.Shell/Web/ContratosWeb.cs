namespace Clinica.Desktop.Shell.Web;

// Contratos de apresentação: serializar com JsonNamingPolicy.CamelCase.
// Nenhum caminho de propriedade/comando recebido do navegador é refletido no modelo.
public sealed record OpcaoWebDto(string Valor, string Rotulo);
public sealed record CampoWebDto(string Chave, string Rotulo, string Tipo, object? Valor,
    IReadOnlyList<OpcaoWebDto> Opcoes, bool Visivel = true, bool Habilitado = true,
    bool Obrigatorio = false, string? Ajuda = null, int Maximo = 5000);
public sealed record AcaoWebDto(string Chave, string Rotulo, bool Habilitada = true,
    string Estilo = "secundario", bool Visivel = true);
public sealed record IndicadorWebDto(string Rotulo, string Valor, string? Detalhe = null);
public sealed record ColunaWebDto(string Chave, string Rotulo, string Tipo = "texto");
public sealed record LinhaWebDto(string Id, IReadOnlyDictionary<string, string> Celulas,
    IReadOnlyList<CampoWebDto> Campos, IReadOnlyList<AcaoWebDto> Acoes, bool Selecionada = false);
public sealed record TabelaWebDto(string Chave, string Titulo, IReadOnlyList<ColunaWebDto> Colunas,
    IReadOnlyList<LinhaWebDto> Linhas, string Vazio = "Nenhum registro neste período.");
public sealed record PontoWebDto(string Rotulo, double? Valor, string ValorFormatado);
public sealed record GraficoWebDto(string Chave, string Rotulo, string Tipo, string Unidade,
    IReadOnlyList<PontoWebDto> Pontos);
public sealed record SecaoWebDto(string Chave, string Titulo, string? Descricao,
    IReadOnlyList<CampoWebDto> Campos, IReadOnlyList<IndicadorWebDto> Indicadores,
    IReadOnlyList<TabelaWebDto> Tabelas, IReadOnlyList<AcaoWebDto> Acoes,
    IReadOnlyList<GraficoWebDto>? Graficos = null);
public sealed record PaginaWebDto(string Chave, string Titulo, string? Subtitulo,
    IReadOnlyList<CampoWebDto> Campos, IReadOnlyList<IndicadorWebDto> Indicadores,
    IReadOnlyList<SecaoWebDto> Secoes, IReadOnlyList<AcaoWebDto> Acoes,
    bool Carregando, bool NaoVerificado, string? Mensagem, bool MensagemEhErro, bool Truncado,
    string Contexto = "");
