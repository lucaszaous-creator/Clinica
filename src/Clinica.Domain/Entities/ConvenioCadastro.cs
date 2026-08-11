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

    /// <summary>
    /// Este convênio GERA GUIA para faturar? (parcela 60)
    ///
    /// Nasce <c>true</c>, que é o que toda linha já gravada passa a valer — e é a verdade
    /// delas: todo convênio cadastrado até aqui fatura.
    ///
    /// Desmarcado, é como a clínica cadastra o <b>PARTICULAR</b>: o paciente que vem sem
    /// convênio. Até esta parcela ele não tinha onde ser cadastrado — o enum
    /// <see cref="Convenio"/> não tem "sem convênio" —, e as duas saídas que sobravam eram
    /// ruins de jeitos diferentes:
    ///
    /// <list type="number">
    /// <item>cadastrá-lo sob um convênio qualquer, e aí o motor gera uma guia com data
    /// prevista que entra no painel de pendências, vence o prazo de decisão e abre a
    /// <b>rodada BLOQUEANTE</b> — travando a tela de quem fatura por uma guia que nunca
    /// vai a operadora nenhuma, porque não há operadora;</item>
    /// <item>não cadastrar o atendimento, e aí a sessão não existe em lugar nenhum: nem
    /// guia, nem prontuário, nem caixa.</item>
    /// </list>
    ///
    /// ⚠️ O atendimento particular <b>continua gerando código</b> — só que marcado
    /// <see cref="StatusCodigo.NaoAplicavel"/>. Não é preciosismo: (a) o invariante "não há
    /// atendimento sem guia" é o que prova que <c>AtendimentoService.LancarAsync</c>
    /// continua sendo ponto único, e ele está fixado em teste; (b)
    /// <c>CodigoFaturamento.EstaPendente</c> já ignora <c>NaoAplicavel</c>, então o
    /// particular sai das pendências e da rodada <b>sem uma linha de código nova</b>; e (c)
    /// o registro de que a sessão aconteceu, com modalidade e especialidade, é o que
    /// alimenta os indicadores — sumir com ele faria a clínica medir só o convênio.
    /// </summary>
    public bool GeraGuia { get; set; } = true;

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
