using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
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
    /// A auditoria do atendimento das secretárias no WhatsApp (26/10, pedido da direção): o @SemDor pré-avalia a
    /// amostra da semana e a gerente confirma ou corrige. Aba "Atendimento" do Desempenho.
    /// </summary>
    public const string ChaveAuditoriaAtendimento = "auditoria-atendimento";

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
    /// Administração (out/2026): Configurações · Acessos · Importar pacientes — o que se mexe
    /// raramente e muda a clínica inteira. Chave NOVA de composto; as chaves das três telas
    /// continuam as de sempre (contrato de navegação entre módulos).
    /// </summary>
    public const string ChaveGrupoAdministracao = "direcao-administracao";

    /// <summary>
    /// Ajuda e suporte — tela do SHELL; os QUATRO módulos publicam a MESMA chave
    /// (<see cref="ChavesSuite.Ajuda"/>), e a dedupe do shell funde numa linha só.
    /// </summary>
    public const string ChaveAjuda = ChavesSuite.Ajuda;

    public string Nome => "Direção";

    public IReadOnlyList<ItemMenuModulo> Itens { get; } =
    [
        // ===== DIREÇÃO (reorganizada em out/2026) =====
        // Sete entradas, uma pergunta cada, na ordem do uso da direção:
        //   Mesa ............ como estamos e o que decidir hoje;
        //   Dinheiro ........ quanto entra, sai e sobra — e quanto custa receber (todas as abas
        //                     são do bit VerFinanceiro: o resultado, a projeção, a margem, a
        //                     rentabilidade por convênio e o custo de taxas e impostos);
        //   Desempenho ...... quanto a clínica produz e se bate o que combinou (indicadores,
        //                     tendências, produção e metas — as metas medem sessões e ocupação
        //                     além de faturamento, por isso saíram do Dinheiro) e como a equipe
        //                     atende no WhatsApp (a auditoria do atendimento, 26/10);
        //   Pacientes ....... de onde vêm, como trazê-los de volta e quanto isso custa;
        //   Tabela de preços  o que a clínica cobra (convênio e particular);
        //   Conformidade .... o que a auditoria pergunta: quem mexeu, o que saiu, o que está guardado;
        //   Administração ... o que se mexe raramente: configurações, acessos e a importação.
        // Nenhuma tela some e nenhuma CHAVE muda: cada tela é aba do item que responde a pergunta
        // dela. Os compostos não têm `Requer`: aparecem quando a pessoa alcança ao menos uma aba,
        // e cada aba confere o bit dela (o "bit mais frouxo" de cada grupo é a própria união).
        new ItemMenuModulo
        {
            Chave = ChavePainel, Rotulo = "Mesa da direção", Glifo = "\uF246", Icone = "painel",
            Grupo = GrupoSidebar.Direcao, Requer = Permissao.VerIndicadores, Inicial = true
        },
        new ItemMenuModulo
        {
            Chave = ChaveGrupoDinheiro, Rotulo = "Dinheiro", Glifo = "\uE825", Icone = "cofre",
            Grupo = GrupoSidebar.Direcao,
            Abas =
            [
                new AbaMenu("Resultado do mês", ChavesSuite.Resultado),
                new AbaMenu("Próximos 90 dias", ChaveProjecaoCaixa),
                new AbaMenu("Margem por sessão", ChaveMargem),
                new AbaMenu("Por convênio", ChaveRentabilidade),
                new AbaMenu("Custo de receber", ChaveCusto)
            ]
        },
        new ItemMenuModulo
        {
            Chave = ChaveGrupoProducao, Rotulo = "Desempenho", Glifo = "\uE9D2", Icone = "grafico",
            Grupo = GrupoSidebar.Direcao,
            Abas =
            [
                new AbaMenu("Indicadores", ChaveIndicadores),
                new AbaMenu("Tendências", ChavesSuite.Tendencias),
                new AbaMenu("Produção", ChavesSuite.Producao),
                new AbaMenu("Metas", ChaveMetas),
                new AbaMenu("Atendimento", ChaveAuditoriaAtendimento)
            ]
        },
        // O Acompanhamento NÃO entra aqui: ele é aba do "Relacionamento" da Recepção, e dois
        // compostos com a mesma tela seriam dois caminhos para o mesmo lugar.
        new ItemMenuModulo
        {
            Chave = ChaveGrupoPacientes, Rotulo = "Pacientes e campanhas", Glifo = "\uE716", Icone = "equipe",
            Grupo = GrupoSidebar.Direcao,
            Abas =
            [
                new AbaMenu("De onde vêm", ChaveOrigens),
                new AbaMenu("Campanhas", ChaveCampanhas),
                new AbaMenu("Comissão de recuperação", ChaveComissaoRecuperacao)
            ]
        },
        new ItemMenuModulo
        {
            Chave = ChaveGrupoPrecos, Rotulo = "Tabela de preços", Glifo = "\uE8EF", Icone = "etiqueta",
            Grupo = GrupoSidebar.Direcao,
            Abas =
            [
                new AbaMenu("Convênios", ChavePrecos),
                new AbaMenu("Particular", ChavesSuite.PrecosParticular)
            ]
        },
        // As perguntas de quem fiscaliza: quem MEXEU (auditoria), o que SAIU (documentos) e o
        // que está GUARDADO (guarda). Sem `Requer` (era VerAuditoria): quem tem SÓ
        // VerDocumentos passou a alcançar "Documentos emitidos" pelo menu também.
        new ItemMenuModulo
        {
            Chave = ChaveGrupoConformidade, Rotulo = "Conformidade", Glifo = "\uE72E", Icone = "escudo",
            Grupo = GrupoSidebar.Direcao,
            Abas =
            [
                new AbaMenu("Auditoria", ChaveAuditoria),
                new AbaMenu("Documentos emitidos", ChaveDocumentosEmitidos),
                new AbaMenu("Guarda do prontuário", ChaveGuarda)
            ]
        },
        // Configurações (GerenciarUsuarios), Acessos (GerenciarUsuarios) e Importar pacientes
        // (EditarPaciente — a direção pode delegar a migração sem entregar os acessos).
        new ItemMenuModulo
        {
            Chave = ChaveGrupoAdministracao, Rotulo = "Administração", Glifo = "\uE713", Icone = "engr",
            Grupo = GrupoSidebar.Direcao,
            Abas =
            [
                new AbaMenu("Configurações", ChaveConfiguracoes),
                new AbaMenu("Acessos", ChaveAcessos),
                new AbaMenu("Importar pacientes", ChaveImportacao)
            ]
        },
        // O resumo do faturamento é aba do composto "Faturamento de guias" (módulo do
        // faturamento); declarado aqui para continuar navegável onde ele não carrega.
        new ItemMenuModulo
        {
            Chave = ChaveFaturamento, Rotulo = "Resumo de faturamento", Glifo = "\uE8C7", Icone = "recibo",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFaturamento
        },

        // ===== Sub-telas =====
        new ItemMenuModulo
        {
            Chave = ChaveConfiguracoes, Rotulo = "Configurações da clínica", Glifo = "\uE713", Icone = "engr",
            Grupo = GrupoSidebar.Direcao, Requer = Permissao.GerenciarUsuarios
        },
        new ItemMenuModulo
        {
            Chave = ChaveImportacao, Rotulo = "Importar pacientes", Glifo = "\uE8B5", Icone = "importar",
            Grupo = GrupoSidebar.Direcao, Requer = Permissao.EditarPaciente
        },
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
        // A auditoria do atendimento: VerAuditoria, o bit da FISCALIZAÇÃO (só da direção nos perfis padrão). A
        // tela lê conversas de pacientes e julga a equipe — a mesma natureza da trilha de auditoria —, e os 31 bits
        // do inteiro já estão em uso (um bit próprio pediria trocar o tipo gravado). O CRM tem a segunda tranca:
        // só a gestão (administrador ou supervisor) lê /api/quality.
        new ItemMenuModulo
        {
            Chave = ChaveAuditoriaAtendimento, Rotulo = "Auditoria do atendimento", Glifo = "\uE8BD", Icone = "balao",
            Grupo = GrupoSidebar.Inteligencia, Requer = Permissao.VerAuditoria
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
        // o bit sobrecarregado que a parcela 49 desfez. O composto "Conformidade" não tem
        // `Requer` (out/2026): quem tiver SÓ `VerDocumentos` vê o grupo com esta aba só.
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
    }

    public object? CriarTela(string chave, IServiceProvider servicos) => chave switch
    {
        // A Mesa da direção (out/2026) substitui o painel WPF: Blazor, no navegador único da janela.
        ChavePainel => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.MesaDaDirecao), servicos),
        // Etapa 6 (out/2026): TODAS as telas do módulo saíram do WPF — Blazor, no navegador
        // único da janela (docs/gerente-blazor.md). "Preços do particular" também, desde a
        // frente "Blazor total": a mesma tela que a Recepção publica pela mesma chave.
        ChaveIndicadores => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.IndicadoresDaClinica), servicos),
        // Blazor desde out/2026 (docs/faturamento-blazor.md): é a aba "Resumo" do faturamento,
        // e as abas vizinhas também são Blazor — mesmo navegador, sem flash entre elas.
        ChaveFaturamento => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.ResumoFaturamento), servicos),
        ChaveComissaoRecuperacao => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.ComissaoRecuperacao), servicos),
        ChavePrecos => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.PrecosPorConvenio), servicos),
        // Blazor desde out/2026 (a mesma tela que a Recepção publica pela mesma chave).
        ChavesSuite.PrecosParticular => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.PrecosDoParticular), servicos),
        ChaveCusto => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.CustoDeTaxasEImpostos), servicos),
        ChaveRentabilidade => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.RentabilidadePorConvenio), servicos),
        ChaveCampanhas => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.CampanhasDaClinica), servicos),
        ChaveAuditoria => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.TrilhaDeAuditoria), servicos),
        ChaveGuarda => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.GuardaDoProntuario), servicos),
        ChaveAcessos => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.AcessosDaClinica), servicos),
        ChaveConfiguracoes => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.ConfiguracoesDaClinica), servicos),
        ChaveMetas => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.MetasDaClinica), servicos),
        ChaveAuditoriaAtendimento => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.AuditoriaDoAtendimento), servicos),
        ChaveProjecaoCaixa => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.CaixaDosProximosDias), servicos),
        ChaveMargem => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.MargemPorSessao), servicos),
        ChaveDocumentosEmitidos => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.DocumentosEmitidos), servicos),
        ChaveOrigens => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.OrigensDosPacientes), servicos),
        ChaveImportacao => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.ImportacaoDePacientes), servicos),
        // Tela do shell, ESTÁTICA: conteúdo literal, sem ViewModel — não há o que resolver.
        ChaveAjuda => Clinica.Desktop.Blazor.HospedagemBlazor.Tela(typeof(Clinica.Ui.Blazor.Telas.AjudaESuporte), servicos),
        _ => null
    };
}
