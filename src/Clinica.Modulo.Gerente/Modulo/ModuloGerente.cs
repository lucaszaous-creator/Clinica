using Clinica.Desktop.Shell.Componentes;
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
    public const string ChaveFaturamento = "faturamento-resumo";
    public const string ChaveCusto = "custo-transacao";
    public const string ChaveRentabilidade = "rentabilidade-convenio";
    public const string ChavePrecos = "precos-convenio";
    /// <summary>O item composto "Tabela de preço": Convênios (deste módulo) · Particular (shell, via Recepção).</summary>
    public const string ChaveGrupoPrecos = "precos";
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

    /// <summary>Caixa dos próximos 90 dias (etapa "Dinheiro", out/2026), aba do Dinheiro.</summary>
    public const string ChaveProjecaoCaixa = "projecao-caixa";
    public const string ChaveMargem = "margem-por-sessao";
    public const string ChaveOrigens = "origens-pacientes";

    /// <summary>
    /// A verificação da comissão de recuperação (out/2026, pedido da clínica): o VALOR e a
    /// VALIDAÇÃO moram aqui, na gestão; a recepção e o faturamento só assumem e falam com o
    /// paciente na fila do Acompanhamento, sem ver dinheiro.
    /// </summary>
    public const string ChaveComissaoRecuperacao = "comissao-recuperacao";

    /// <summary>
    /// O que SAIU da clínica no período (set/2026, tela 4 do mockup aprovado
    /// "documentos nas quatro telas") — a leitura agregada dos papéis emitidos.
    /// </summary>
    public const string ChaveDocumentosEmitidos = "documentos-emitidos";

    /// <summary>
    /// Importar pacientes do sistema anterior (set/2026). Fica no Gerente e não na
    /// Recepção porque é ato de MIGRAÇÃO, feito uma vez pela direção — e porque cria
    /// centenas de fichas num clique, o que não é trabalho de balcão.
    /// </summary>
    public const string ChaveImportacao = ChavesSuite.ImportarPacientes;

    // ===== Itens COMPOSTOS (parcela 55) =====
    // A Direção é quem publica os que ATRAVESSAM módulo, porque ela é o único app que
    // carrega todos — e porque, sem ela carregada, cada sub-tela volta a ser item de
    // menu no app dono (ver o comentário da tela órfã no ShellViewModel).
    public const string ChaveGrupoDinheiro = "direcao-dinheiro";
    public const string ChaveGrupoProducao = "direcao-producao";
    public const string ChaveGrupoPacientes = "direcao-pacientes";
    public const string ChaveGrupoConformidade = "conformidade";

    /// <summary>
    /// Ajuda e suporte — tela do SHELL; os QUATRO módulos publicam a MESMA chave
    /// (<see cref="ChavesSuite.Ajuda"/>), e a dedupe do shell funde numa linha só.
    /// </summary>
    public const string ChaveAjuda = ChavesSuite.Ajuda;

    public string Nome => "Direção";

    public IReadOnlyList<ItemMenuModulo> Itens { get; } =
    [
        // ===== DIREÇÃO (out/2026, mockup aprovado "mesa da direção v2") =====
        // Seis entradas, uma pergunta cada, num grupo próprio que abre a barra do topo:
        // como estamos e o que decidir hoje (a Mesa), o dinheiro, a produção, os pacientes,
        // os preços e a conformidade. O operacional dos outros módulos continua nos grupos
        // de sempre. Nenhuma tela some: cada uma virou aba da entrada que responde a pergunta
        // dela. Os compostos sem `Requer` aparecem quando a pessoa alcança ao menos uma aba,
        // e cada aba confere o bit dela.
        new ItemMenuModulo
        {
            Chave = ChavePainel, Rotulo = "Mesa da dire\u00E7\u00E3o", Glifo = "\uF246", Icone = "painel",
            Grupo = GrupoSidebar.Direcao, Requer = Permissao.VerIndicadores, Inicial = true
        },
        new ItemMenuModulo
        {
            Chave = ChaveGrupoDinheiro, Rotulo = "Dinheiro", Glifo = "\uE825", Icone = "cofre",
            Grupo = GrupoSidebar.Direcao,
            Abas =
            [
                new AbaMenu("Resultado do m\u00EAs", ChavesSuite.Resultado),
                new AbaMenu("Pr\u00F3ximos 90 dias", ChaveProjecaoCaixa),
                new AbaMenu("Metas", ChaveMetas)
            ]
        },
        new ItemMenuModulo
        {
            Chave = ChaveGrupoProducao, Rotulo = "Produ\u00E7\u00E3o e margem", Glifo = "\uE9D2", Icone = "grafico",
            Grupo = GrupoSidebar.Direcao,
            Abas =
            [
                new AbaMenu("Margem por sessão", ChaveMargem),
                new AbaMenu("Indicadores", ChaveIndicadores),
                new AbaMenu("Tend\u00EAncias", ChavesSuite.Tendencias),
                new AbaMenu("Produ\u00E7\u00E3o", ChavesSuite.Producao),
                new AbaMenu("Rentabilidade por conv\u00EAnio", ChaveRentabilidade)
            ]
        },
        // O Acompanhamento NÃO entra aqui: ele é aba do "Relacionamento" da Recepção, e dois
        // compostos com a mesma tela seriam dois caminhos para o mesmo lugar.
        new ItemMenuModulo
        {
            Chave = ChaveGrupoPacientes, Rotulo = "Pacientes e relacionamento", Glifo = "\uE716", Icone = "equipe",
            Grupo = GrupoSidebar.Direcao,
            Abas =
            [
                new AbaMenu("De onde v\u00EAm os pacientes", ChaveOrigens),
                new AbaMenu("Comiss\u00E3o de recupera\u00E7\u00E3o", ChaveComissaoRecuperacao),
                new AbaMenu("Campanhas", ChaveCampanhas)
            ]
        },
        // Quanto vale a sessão (convênio e particular) e quanto custa receber (taxas e
        // impostos, o custo efetivo): a mesma pergunta — o que a clínica cobra e o que fica.
        new ItemMenuModulo
        {
            Chave = ChaveGrupoPrecos, Rotulo = "Pre\u00E7os e regras", Glifo = "\uE8EF", Icone = "etiqueta",
            Grupo = GrupoSidebar.Direcao,
            Abas =
            [
                new AbaMenu("Conv\u00EAnios", ChavePrecos),
                new AbaMenu("Particular", ChavesSuite.PrecosParticular),
                new AbaMenu("Custo de taxas e impostos", ChaveCusto)
            ]
        },
        // As perguntas de quem fiscaliza, no mesmo lugar: quem MEXEU (auditoria), o que SAIU
        // (documentos), o que está GUARDADO (guarda) e quem PODE (acessos). A permissão
        // continua sendo por aba (parcela 49).
        new ItemMenuModulo
        {
            Chave = ChaveGrupoConformidade, Rotulo = "Conformidade e acessos", Glifo = "\uE72E", Icone = "escudo",
            Grupo = GrupoSidebar.Direcao, Requer = Permissao.VerAuditoria,
            Abas =
            [
                new AbaMenu("Auditoria", ChaveAuditoria),
                new AbaMenu("Documentos emitidos", ChaveDocumentosEmitidos),
                new AbaMenu("Guarda do prontu\u00E1rio", ChaveGuarda),
                new AbaMenu("Acessos", ChaveAcessos)
            ]
        },
        new ItemMenuModulo
        {
            Chave = ChaveConfiguracoes, Rotulo = "Configura\u00E7\u00F5es", Glifo = "\uE713", Icone = "engr",
            Grupo = GrupoSidebar.Direcao, Requer = Permissao.GerenciarUsuarios
        },
        // A migração da carteira: o bit é o do ATO (cadastrar paciente), não o de gerir
        // usuários — a direção pode delegar a importação sem entregar os acessos.
        new ItemMenuModulo
        {
            Chave = ChaveImportacao, Rotulo = "Importar pacientes", Glifo = "\uE8B5", Icone = "importar",
            Grupo = GrupoSidebar.Direcao, Requer = Permissao.EditarPaciente
        },
        // O resumo do faturamento é aba do composto "Faturamento de guias" (módulo do
        // faturamento); declarado aqui para continuar navegável onde ele não carrega.
        new ItemMenuModulo
        {
            Chave = ChaveFaturamento, Rotulo = "Resumo de faturamento", Glifo = "\uE8C7", Icone = "recibo",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFaturamento
        },

        // ===== Sub-telas =====
        // Continuam sendo itens navegáveis — o painel da direção leva a várias delas por
        // chave. Sem `Oculto`: quem as esconde é o item pai, e só onde ele existe.
        new ItemMenuModulo
        {
            Chave = ChaveIndicadores, Rotulo = "Relat\u00F3rios / BI", Glifo = "\uE9D2", Icone = "grafico",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.VerIndicadores
        },
        new ItemMenuModulo
        {
            Chave = ChavePrecos, Rotulo = "Tabela de pre\u00E7o por conv\u00EAnio", Glifo = "\uE8EF", Icone = "etiqueta",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro
        },
        // A tela do shell, declarada também aqui e OCULTA: o composto acima a reivindica,
        // e declarar garante a aba mesmo num executável que não carregue a Recepção. No
        // Gerente Geral a dedupe por chave mantém uma só.
        new ItemMenuModulo
        {
            Chave = ChavesSuite.PrecosParticular, Rotulo = "Pre\u00E7os do particular", Glifo = "\uE8EF", Icone = "etiqueta",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro, Oculto = true
        },
        new ItemMenuModulo
        {
            Chave = ChaveProjecaoCaixa, Rotulo = "Caixa dos pr\u00F3ximos 90 dias", Glifo = "\uE787", Icone = "cal",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro
        },
        new ItemMenuModulo
        {
            Chave = ChaveMargem, Rotulo = "Margem por sessão", Glifo = "\uE9D9", Icone = "grafico",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.VerFinanceiro
        },
        new ItemMenuModulo
        {
            Chave = ChaveMetas, Rotulo = "Metas", Glifo = "\uE7C1", Icone = "alvo",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.VerIndicadores
        },
        new ItemMenuModulo
        {
            Chave = ChaveRentabilidade, Rotulo = "Rentabilidade por conv\u00EAnio", Glifo = "\uE9F3", Icone = "pizza",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.VerFinanceiro
        },
        new ItemMenuModulo
        {
            Chave = ChaveCusto, Rotulo = "Custo de taxas e impostos", Glifo = "\uE9F9", Icone = "pizza",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.VerFinanceiro
        },
        new ItemMenuModulo
        {
            Chave = ChaveCampanhas, Rotulo = "Campanhas", Glifo = "\uE715", Icone = "mega",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.GerenciarCampanhas
        },
        new ItemMenuModulo
        {
            Chave = ChaveOrigens, Rotulo = "De onde v\u00EAm os pacientes", Glifo = "\uE8AF", Icone = "mega",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.GerenciarCampanhas
        },
        // Dinheiro: ver é VerFinanceiro (a régua do repasse dos profissionais); definir o
        // valor e validar exigem EditarFinanceiro, conferido na tela (as duas barreiras).
        new ItemMenuModulo
        {
            Chave = ChaveComissaoRecuperacao, Rotulo = "Comiss\u00E3o de recupera\u00E7\u00E3o", Glifo = "\uE8C7", Icone = "recibo",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.VerFinanceiro
        },
        new ItemMenuModulo
        {
            Chave = ChaveAuditoria, Rotulo = "Auditoria", Glifo = "\uE81C", Icone = "escudo",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.VerAuditoria
        },
        // ⚠️ O bit é o da SEÇÃO de documentos (parcela 59), não o da auditoria: quem lê a
        // trilha e quem lê os papéis emitidos são perguntas diferentes, e juntá-las seria
        // o bit sobrecarregado que a parcela 49 desfez. O grupo "Conformidade e acessos"
        // pede o bit mais frouxo dele (`VerAuditoria`), então quem tiver SÓ `VerDocumentos`
        // alcança esta tela pela navegação, não pela sidebar — a direção, que é quem a
        // usa, tem os dois.
        new ItemMenuModulo
        {
            Chave = ChaveDocumentosEmitidos, Rotulo = "Documentos emitidos",
            Glifo = "\uE8A5", Icone = "documento",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.VerDocumentos
        },
        new ItemMenuModulo
        {
            Chave = ChaveGuarda, Rotulo = "Guarda do prontu\u00E1rio", Glifo = "\uE7B8", Icone = "estoque",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.VerAuditoria
        },
        new ItemMenuModulo
        {
            Chave = ChaveAcessos, Rotulo = "Acessos", Glifo = "\uE72E", Icone = "escudo",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.GerenciarUsuarios
        },

        // AJUDA E SUPORTE — tela do shell, sem `Requer` de propósito (o padrão é
        // "sempre visível"): fechar o manual por permissão trancaria quem mais precisa.
        new ItemMenuModulo
        {
            Chave = ChaveAjuda, Rotulo = "Ajuda e suporte", Glifo = "\uE897", Icone = "ajuda",
            Grupo = GrupoSidebar.Gestao
        }
    ];

    public void Registrar(IServiceCollection servicos)
    {
        Clinica.Desktop.Blazor.HospedagemBlazor.AddHospedagemBlazor(servicos);
        servicos.AddTransient<PrecosConvenioViewModel>();
        servicos.AddTransient<PrecosParticularViewModel>();
        servicos.AddTransient<CampanhasViewModel>();
        servicos.AddTransient<AuditoriaViewModel>();
        servicos.AddTransient<GuardaProntuarioViewModel>();
        servicos.AddTransient<AcessosViewModel>();
        servicos.AddTransient<ConfiguracoesViewModel>();
        servicos.AddTransient<OrigensViewModel>();
        servicos.AddTransient<DocumentosEmitidosViewModel>();
        servicos.AddTransient<ImportacaoPacientesViewModel>();
        // UsuarioEdicaoViewModel é construído à mão pela tela: precisa receber o id do
        // usuário no construtor, como os demais formulários da suíte.
    }

    public object? CriarTela(string chave, IServiceProvider servicos) => chave switch
    {
        // A Mesa da direção (out/2026) substitui o painel WPF: Blazor, no navegador único da janela.
        ChavePainel => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.MesaDaDirecao), servicos),
        // Etapa 6 (out/2026): Indicadores, Rentabilidade e Custo saíram do WPF — Blazor, no
        // navegador único da janela, como as abas vizinhas de "Produção e margem".
        ChaveIndicadores => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.IndicadoresDaClinica), servicos),
        // Blazor desde out/2026 (docs/faturamento-blazor.md): é a aba "Resumo" do faturamento,
        // e as abas vizinhas também são Blazor — mesmo navegador, sem flash entre elas.
        ChaveFaturamento => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.ResumoFaturamento), servicos),
        ChaveComissaoRecuperacao => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.ComissaoRecuperacao), servicos),
        ChavePrecos => new PrecosConvenioView
        {
            DataContext = servicos.GetRequiredService<PrecosConvenioViewModel>()
        },
        ChavesSuite.PrecosParticular => new PrecosParticularView
        {
            DataContext = servicos.GetRequiredService<PrecosParticularViewModel>()
        },
        ChaveCusto => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.CustoDeTaxasEImpostos), servicos),
        ChaveRentabilidade => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.RentabilidadePorConvenio), servicos),
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
        ChaveMetas => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.MetasDaClinica), servicos),
        ChaveProjecaoCaixa => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.CaixaDosProximosDias), servicos),
        ChaveMargem => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.MargemPorSessao), servicos),
        ChaveDocumentosEmitidos => new DocumentosEmitidosView
        {
            DataContext = servicos.GetRequiredService<DocumentosEmitidosViewModel>()
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
