using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using Clinica.Gerente.ViewModels;
using Clinica.Gerente.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Gerente.Modulo;

/// <summary>
/// Módulo do Gerente Geral (parcela 5): Indicadores (BI), Faturamento consolidado
/// (leitura), Campanhas (confirmação, NPS e recall) e Acessos (usuários e permissões).
///
/// Só entram aqui as telas que SÓ fazem sentido para a direção — o Gerente já carrega
/// Recepção e Financeiro inteiros, e repetir o que eles fazem seria manter a mesma
/// tela em dois lugares.
///
/// A ordem é a da pergunta que a direção faz: "como a clínica está" (indicadores),
/// "estamos perdendo faturamento?" (faturamento), "o que estamos fazendo a respeito"
/// (campanhas), "quem fez isso?" (auditoria) e, por último, quem pode o quê — que se
/// mexe raramente.
/// </summary>
public sealed class ModuloGerente : IModuloApp
{
    public const string ChavePainel = ChavesSuite.PainelDirecao;
    public const string ChaveIndicadores = "indicadores";
    public const string ChaveFaturamento = ChavesSuite.FaturamentoTiss;
    public const string ChaveCusto = "custo-transacao";
    public const string ChaveRentabilidade = "rentabilidade-convenio";
    public const string ChavePrecos = "precos-convenio";
    public const string ChaveCampanhas = "campanhas";
    public const string ChaveAuditoria = "auditoria";

    /// <summary>
    /// A guarda do prontuário (parcela 52) — a tela que responde "como você garante 20
    /// anos?" a quem audita a clínica. Fica no Gerente porque quem responde a auditoria é
    /// a direção, e porque exportar a base inteira não é ato de balcão.
    /// </summary>
    public const string ChaveGuarda = "guarda-prontuario";
    public const string ChaveAcessos = "acessos";
    public const string ChaveConfiguracoes = "configuracoes";
    public const string ChaveMetas = "metas";
    public const string ChaveRetencao = "retencao";
    public const string ChaveOrigens = "origens-pacientes";

    /// <summary>
    /// Importar pacientes do sistema anterior (set/2026). Fica no Gerente e não na
    /// Recepção porque é ato de MIGRAÇÃO, feito uma vez pela direção — e porque cria
    /// centenas de fichas num clique, o que não é trabalho de balcão.
    /// </summary>
    public const string ChaveImportacao = "importar-pacientes";

    // ===== Itens COMPOSTOS (parcela 55) =====
    // A Direção é quem publica os que ATRAVESSAM módulo, porque ela é o único app que
    // carrega todos — e porque, sem ela carregada, cada sub-tela volta a ser item de
    // menu no app dono (ver o comentário da tela órfã no ShellViewModel).
    public const string ChaveGrupoPainel = "painel";
    public const string ChaveGrupoRelatorios = "relatorios";
    public const string ChaveGrupoRentabilidade = "rentabilidade";
    public const string ChaveGrupoMarketing = "marketing";
    public const string ChaveGrupoConformidade = "conformidade";

    /// <summary>
    /// Ajuda e suporte — tela do SHELL; os QUATRO módulos publicam a MESMA chave
    /// (<see cref="ChavesSuite.Ajuda"/>), e a dedupe do shell funde numa linha só.
    /// </summary>
    public const string ChaveAjuda = ChavesSuite.Ajuda;

    public string Nome => "Direção";

    public IReadOnlyList<ItemMenuModulo> Itens { get; } =
    [
        // ===== GESTÃO =====
        // Os TRÊS painéis viraram um item. Cada app tinha o seu — a direção via o dela, o
        // balcão via "Início", quem atende via "Meu dia" —, e no Gerente Geral os três
        // apareciam como itens diferentes no mesmo grupo, obrigando a lembrar qual era
        // qual. `Inicial` fica AQUI, e a 1ª aba é a da direção: é o que a parcela 22
        // resolveu (o Gerente abria no painel da RECEPÇÃO) e não pode regredir.
        new ItemMenuModulo
        {
            Chave = ChaveGrupoPainel, Rotulo = "Painel", Glifo = "\uF246",
            Grupo = GrupoSidebar.Gestao, Requer = Permissao.VerIndicadores, Inicial = true,
            Abas =
            [
                new AbaMenu("Dire\u00E7\u00E3o", ChavePainel),
                new AbaMenu("Recep\u00E7\u00E3o", ChavesSuite.PainelRecepcao),
                new AbaMenu("Meu dia", ChavesSuite.ConsultorioMeuDia)
            ]
        },

        // ===== FINANCEIRO =====
        // O faturamento entra na seção FINANCEIRO, junto do caixa e dos pacotes: para
        // quem usa, é tudo dinheiro da clínica. Que a tela venha do módulo da Direção é
        // detalhe de arquitetura, e arquitetura não organiza menu.
        //
        // Ele NÃO virou aba de nada, e a tabela de preço também não: "Faturamento (TISS)"
        // já é uma tela de cinco abas por dentro, e pendurá-la sob outra régua daria
        // abas dentro de abas — duas réguas empilhadas no topo, que é pior do que os dois
        // itens que se queria economizar.
        new ItemMenuModulo
        {
            Chave = ChaveFaturamento, Rotulo = "Faturamento (TISS)", Glifo = "\uE8C7",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFaturamento
        },
        new ItemMenuModulo
        {
            Chave = ChavePrecos, Rotulo = "Tabela de pre\u00E7o (conv\u00EAnios)", Glifo = "\uE8EF",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro
        },

        // ===== INTELIGÊNCIA =====
        // "O que aconteceu" num item só. Metas fica com os relatórios de propósito: é a
        // mesma pergunta vista dos dois lados — o BI diz o que aconteceu, a meta diz o
        // que deveria ter acontecido. "Meus números" entra porque é o mesmo indicador de
        // produtividade, recortado por profissional.
        new ItemMenuModulo
        {
            Chave = ChaveGrupoRelatorios, Rotulo = "Relat\u00F3rios / BI", Glifo = "\uE9D2",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.VerIndicadores,
            Abas =
            [
                new AbaMenu("Indicadores", ChaveIndicadores),
                new AbaMenu("Metas", ChaveMetas),
                new AbaMenu("Resultado do m\u00EAs", ChavesSuite.Resultado),
                new AbaMenu("Produ\u00E7\u00E3o", ChavesSuite.Producao),
                new AbaMenu("Meus n\u00FAmeros", ChavesSuite.ConsultorioMeusNumeros)
            ]
        },
        new ItemMenuModulo
        {
            Chave = ChaveGrupoRentabilidade, Rotulo = "Rentabilidade e custos", Glifo = "\uE9F3",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.VerFinanceiro,
            Abas =
            [
                new AbaMenu("Por conv\u00EAnio", ChaveRentabilidade),
                new AbaMenu("Custo de taxas e impostos", ChaveCusto)
            ]
        },
        // As três primeiras falam com quem não está vindo, por caminhos diferentes: a
        // rodada automática, a lista para decidir caso a caso, e o telefonema do balcão.
        // A quarta é a outra ponta do mesmo assunto — de onde vêm os que CHEGAM, que é a
        // pergunta que decide onde vale gastar o dinheiro de trazer gente.
        new ItemMenuModulo
        {
            Chave = ChaveGrupoMarketing, Rotulo = "Marketing / Recall", Glifo = "\uE715",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.GerenciarCampanhas,
            Abas =
            [
                new AbaMenu("Campanhas", ChaveCampanhas),
                new AbaMenu("Quem parou de vir", ChaveRetencao),
                new AbaMenu("Retorno de pacientes", ChavesSuite.RetornoPacientes),
                new AbaMenu("De onde vêm os pacientes", ChaveOrigens)
            ]
        },
        // As três perguntas de quem fiscaliza, no mesmo lugar: quem MEXEU (auditoria), o
        // que está GUARDADO (guarda) e quem PODE (acessos). Elas já vinham lado a lado na
        // lista antiga, e a auditoria da cliente pergunta as três de uma vez.
        //
        // ⚠️ A permissão continua sendo POR ABA, não do grupo: `VerAuditoria` abre a
        // trilha e `GerenciarUsuarios` abre os acessos, e juntá-las daria poder de criar
        // usuário a quem só precisa conferir o que aconteceu — o oposto da parcela 49. O
        // grupo exige o menor dos dois, e as abas que a pessoa não pode ver não aparecem.
        new ItemMenuModulo
        {
            Chave = ChaveGrupoConformidade, Rotulo = "Conformidade e acessos", Glifo = "\uE81C",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.VerAuditoria,
            Abas =
            [
                new AbaMenu("Auditoria", ChaveAuditoria),
                new AbaMenu("Guarda do prontu\u00E1rio", ChaveGuarda),
                new AbaMenu("Acessos", ChaveAcessos)
            ]
        },
        new ItemMenuModulo
        {
            Chave = ChaveConfiguracoes, Rotulo = "Configura\u00E7\u00F5es", Glifo = "\uE713",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.GerenciarUsuarios
        },

        // ===== PACIENTE =====
        // A migração da carteira: o bit é o do ATO (cadastrar paciente), não o de gerir
        // usuários — a direção pode delegar a importação sem entregar os acessos.
        new ItemMenuModulo
        {
            Chave = ChaveImportacao, Rotulo = "Importar pacientes", Glifo = "\uE8B5",
            Grupo = GrupoSidebar.Paciente, Requer = Permissao.EditarPaciente
        },

        // ===== Sub-telas =====
        // Continuam sendo itens navegáveis — o painel da direção leva a várias delas por
        // chave. Sem `Oculto`: quem as esconde é o item pai, e só onde ele existe.
        new ItemMenuModulo
        {
            Chave = ChavePainel, Rotulo = "Painel da dire\u00E7\u00E3o", Glifo = "\uF246",
            Grupo = GrupoSidebar.Gestao, Requer = Permissao.VerIndicadores
        },
        new ItemMenuModulo
        {
            Chave = ChaveIndicadores, Rotulo = "Relat\u00F3rios / BI", Glifo = "\uE9D2",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.VerIndicadores
        },
        new ItemMenuModulo
        {
            Chave = ChaveMetas, Rotulo = "Metas", Glifo = "\uE7C1",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.VerIndicadores
        },
        new ItemMenuModulo
        {
            Chave = ChaveRentabilidade, Rotulo = "Rentabilidade por conv\u00EAnio", Glifo = "\uE9F3",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.VerFinanceiro
        },
        new ItemMenuModulo
        {
            Chave = ChaveCusto, Rotulo = "Custo de taxas e impostos", Glifo = "\uE9F9",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.VerFinanceiro
        },
        new ItemMenuModulo
        {
            Chave = ChaveCampanhas, Rotulo = "Campanhas", Glifo = "\uE715",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.GerenciarCampanhas
        },
        new ItemMenuModulo
        {
            Chave = ChaveRetencao, Rotulo = "Quem parou de vir", Glifo = "\uE8AF",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.GerenciarCampanhas
        },
        new ItemMenuModulo
        {
            Chave = ChaveOrigens, Rotulo = "De onde v\u00EAm os pacientes", Glifo = "\uE8AF",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.GerenciarCampanhas
        },
        new ItemMenuModulo
        {
            Chave = ChaveAuditoria, Rotulo = "Auditoria", Glifo = "\uE81C",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.VerAuditoria
        },
        new ItemMenuModulo
        {
            Chave = ChaveGuarda, Rotulo = "Guarda do prontu\u00E1rio", Glifo = "\uE7B8",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.VerAuditoria
        },
        new ItemMenuModulo
        {
            Chave = ChaveAcessos, Rotulo = "Acessos", Glifo = "\uE72E",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.GerenciarUsuarios
        },

        // AJUDA E SUPORTE — tela do shell, sem `Requer` de propósito (o padrão é
        // "sempre visível"): fechar o manual por permissão trancaria quem mais precisa.
        new ItemMenuModulo
        {
            Chave = ChaveAjuda, Rotulo = "Ajuda e suporte", Glifo = "\uE897",
            Grupo = GrupoSidebar.Gestao
        }
    ];

    public void Registrar(IServiceCollection servicos)
    {
        servicos.AddTransient<PainelDirecaoViewModel>();
        servicos.AddTransient<IndicadoresViewModel>();
        // FaturamentoGerencialViewModel NÃO se registra: é sub-aba construída à mão pelo
        // FaturamentoTissViewModel — registro sem resolvedor é código morto que sugere
        // um caminho de DI que não existe.
        servicos.AddTransient<FaturamentoTissViewModel>();
        servicos.AddTransient<PrecosConvenioViewModel>();
        servicos.AddTransient<CustoTransacaoViewModel>();
        servicos.AddTransient<RentabilidadeConvenioViewModel>();
        servicos.AddTransient<CampanhasViewModel>();
        servicos.AddTransient<AuditoriaViewModel>();
        servicos.AddTransient<GuardaProntuarioViewModel>();
        servicos.AddTransient<AcessosViewModel>();
        servicos.AddTransient<ConfiguracoesViewModel>();
        servicos.AddTransient<MetasViewModel>();
        servicos.AddTransient<RetencaoViewModel>();
        servicos.AddTransient<OrigensViewModel>();
        servicos.AddTransient<ImportacaoPacientesViewModel>();
        // UsuarioEdicaoViewModel é construído à mão pela tela: precisa receber o id do
        // usuário no construtor, como os demais formulários da suíte.
    }

    public object? CriarTela(string chave, IServiceProvider servicos) => chave switch
    {
        ChavePainel => new PainelDirecaoView
        {
            DataContext = servicos.GetRequiredService<PainelDirecaoViewModel>()
        },
        ChaveIndicadores => new IndicadoresView
        {
            DataContext = servicos.GetRequiredService<IndicadoresViewModel>()
        },
        ChaveFaturamento => new FaturamentoTissView
        {
            DataContext = servicos.GetRequiredService<FaturamentoTissViewModel>()
        },
        ChavePrecos => new PrecosConvenioView
        {
            DataContext = servicos.GetRequiredService<PrecosConvenioViewModel>()
        },
        ChaveCusto => new CustoTransacaoView
        {
            DataContext = servicos.GetRequiredService<CustoTransacaoViewModel>()
        },
        ChaveRentabilidade => new RentabilidadeConvenioView
        {
            DataContext = servicos.GetRequiredService<RentabilidadeConvenioViewModel>()
        },
        ChaveCampanhas => new CampanhasView
        {
            DataContext = servicos.GetRequiredService<CampanhasViewModel>()
        },
        ChaveAuditoria => new AuditoriaView
        {
            DataContext = servicos.GetRequiredService<AuditoriaViewModel>()
        },
        ChaveGuarda => new GuardaProntuarioView
        {
            DataContext = servicos.GetRequiredService<GuardaProntuarioViewModel>()
        },
        ChaveAcessos => new AcessosView
        {
            DataContext = servicos.GetRequiredService<AcessosViewModel>()
        },
        ChaveConfiguracoes => new ConfiguracoesView
        {
            DataContext = servicos.GetRequiredService<ConfiguracoesViewModel>()
        },
        ChaveMetas => new MetasView
        {
            DataContext = servicos.GetRequiredService<MetasViewModel>()
        },
        ChaveRetencao => new RetencaoView
        {
            DataContext = servicos.GetRequiredService<RetencaoViewModel>()
        },
        ChaveOrigens => new OrigensView
        {
            DataContext = servicos.GetRequiredService<OrigensViewModel>()
        },
        ChaveImportacao => new ImportacaoPacientesView
        {
            DataContext = servicos.GetRequiredService<ImportacaoPacientesViewModel>()
        },
        // Tela do shell, ESTÁTICA: conteúdo literal, sem ViewModel — não há o que resolver.
        ChaveAjuda => new Clinica.Desktop.Shell.Componentes.AjudaView(),
        _ => null
    };
}
