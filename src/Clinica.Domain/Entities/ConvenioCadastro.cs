namespace Clinica.Domain.Entities;

/// <summary>
/// Catálogo de convênios selecionáveis no cadastro (dado, não código). Inclui os 4
/// convênios embutidos (semeados) e permite adicionar variantes que reutilizam a
/// REGRA de faturamento de uma família existente (<see cref="Familia"/>), com nome
/// próprio e ativação independente. A lógica de faturamento permanece no código.
/// </summary>
public class ConvenioCadastro
{
    /// <summary>Código único (chave). Para os embutidos, é o nome da família (ex.: "Amil").</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Nome exibido nas telas e documentos.</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Família de regra de faturamento que este convênio usa.</summary>
    public Convenio Familia { get; set; }

    /// <summary>Disponível para novos cadastros. Inativo some das listas; o histórico é preservado.</summary>
    public bool Ativo { get; set; } = true;

    /// <summary>
    /// Que forma o número da guia tem no sistema DESTE convênio (parcela 45). Vale para
    /// qualquer entrada do catálogo, embutida ou não — é por isso que mora aqui e não na
    /// configuração da regra genérica logo abaixo: a Unimed é embutida e tem formato, e a
    /// Sul América é personalizada e também tem.
    ///
    /// Nasce <see cref="FormatoNumeroGuia.SemValidacao"/> (o zero do enum) porque é o que
    /// as linhas já gravadas passam a valer quando a coluna aparece — recusar baixa de
    /// convênio nenhum é o único comportamento honesto para um campo que ninguém preencheu.
    /// A migration semeia os embutidos; o resto a clínica marca em Configurações.
    /// </summary>
    public FormatoNumeroGuia FormatoNumeroGuia { get; set; } = FormatoNumeroGuia.SemValidacao;

    // ---- Configuração da regra GENÉRICA (usada apenas quando Familia == Personalizado) ----

    public bool FazEletro { get; set; }
    public bool TemSegundoCodigo { get; set; }
    public FormaObtencao FormaSegundoCodigo { get; set; } = FormaObtencao.Sistema;
    public bool SegundoCodigoDependeApp { get; set; }
    public int DiasSegundoCodigo { get; set; } = 1;
    public bool FaturaBsv { get; set; } = true;
    public bool InverteDatasBsv { get; set; }
    public int? ValidadeConsultaDias { get; set; }
    public Categoria CategoriaComApp { get; set; } = Categoria.Verde;
    public Categoria CategoriaSemApp { get; set; } = Categoria.Amarela;

    /// <summary>Extrai a configuração da regra genérica desta entrada do catálogo.</summary>
    public Clinica.Domain.Regras.ConfiguracaoRegraGenerica ParaConfig() => new()
    {
        FazEletro = FazEletro,
        TemSegundoCodigo = TemSegundoCodigo,
        FormaSegundoCodigo = FormaSegundoCodigo,
        SegundoCodigoDependeApp = SegundoCodigoDependeApp,
        DiasSegundoCodigo = DiasSegundoCodigo,
        FaturaBsv = FaturaBsv,
        InverteDatasBsv = InverteDatasBsv,
        ValidadeConsultaDias = ValidadeConsultaDias,
        CategoriaComApp = CategoriaComApp,
        CategoriaSemApp = CategoriaSemApp
    };
}
