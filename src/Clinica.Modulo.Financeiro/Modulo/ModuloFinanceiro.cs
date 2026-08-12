using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using Clinica.Financeiro.ViewModels;
using Clinica.Financeiro.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Financeiro.Modulo;

/// <summary>
/// Módulo Financeiro: Caixa (entradas e saídas), Contas a pagar e a receber (o que vence),
/// Fluxo de caixa (a série dos meses e para onde o dinheiro vai), Fechamento de caixa (a
/// conferência da gaveta), Recebíveis de cartão (o que a maquininha ainda deve
/// depositar), Conciliação (guias efetivadas que ainda não viraram receita),
/// Produção (volume do faturamento), Pacotes (o que a clínica vende), Estoque (insumos),
/// Repasses (o que cada profissional tem a receber), Taxas e impostos, e o Plano de contas.
///
/// A ordem é a do dia de trabalho: primeiro o dinheiro que entra e sai (Caixa), depois o
/// que ainda VAI entrar ou sair e quando (Contas), a tendência ao longo dos meses (Fluxo),
/// a conferência da gaveta no fim do expediente (Fechamento), o que já foi efetivado no
/// convênio e não entrou (Conciliação), o volume (Produção), o que foi vendido (Pacotes),
/// o que se gasta (Estoque), o que se paga (Repasses), o que descontam de cada recebimento
/// (Taxas) e, por último, o cadastro que classifica tudo — que se mexe raramente.
/// </summary>
public sealed class ModuloFinanceiro : IModuloApp
{
    public const string ChaveCaixa = ChavesSuite.Caixa;
    public const string ChaveContas = ChavesSuite.Contas;
    public const string ChaveInadimplencia = ChavesSuite.Inadimplencia;
    public const string ChaveFluxo = "fluxo-caixa";
    public const string ChaveResultado = ChavesSuite.Resultado;
    public const string ChaveFechamento = ChavesSuite.FechamentoCaixa;
    public const string ChaveRecebiveis = ChavesSuite.Recebiveis;
    public const string ChaveConciliacao = ChavesSuite.Conciliacao;
    public const string ChaveProducao = ChavesSuite.Producao;
    // A MESMA chave que a Recepção publica (parcela 60) — vem de ChavesSuite porque
    // atravessa módulo, e a dedupe do shell funde as duas linhas no Gerente.
    public const string ChavePacotes = ChavesSuite.Pacotes;
    public const string ChaveEstoque = "estoque";
    public const string ChaveRepasses = "repasses";
    public const string ChaveTaxas = "taxas";
    public const string ChavePlanoContas = "plano-contas";

    // ===== Itens COMPOSTOS (parcela 55) =====
    // Chave própria, e não a de uma das abas: o item pai e a sub-tela são dois destinos
    // de navegação diferentes, e reaproveitar a chave faria `NavegacaoSuite.Ir(Caixa)`
    // ficar ambíguo entre "abra o grupo" e "abra a tela do caixa".
    public const string ChaveGrupoCaixa = "financeiro-caixa";
    public const string ChaveGrupoContas = "financeiro-contas";
    public const string ChaveGrupoRecebimentos = "financeiro-recebimentos";

    public string Nome => "Financeiro";

    // Todas exigem VerFinanceiro (parcela 5): dinheiro não aparece para quem só
    // trabalha o balcão, a menos que a direção conceda a permissão.
    // Tudo aqui \u00E9 da se\u00e7\u00e3o FINANCEIRO da proposta: \u00E9 o mesmo assunto \u2014 o dinheiro da
    // cl\u00EDnica \u2014, visto de sete \u00E2ngulos.
    public IReadOnlyList<ItemMenuModulo> Itens { get; } =
    [
        // ===== Os três grupos de dinheiro =====
        // Antes eram nove itens soltos. O critério da junção é a PERGUNTA: "quanto entrou
        // e saiu" (Caixa), "o que vence" (Contas) e "o que a operadora/adquirente ainda
        // deve" (Recebimentos). Quem trabalha o financeiro faz uma das três por vez.
        new ItemMenuModulo
        {
            Chave = ChaveGrupoCaixa, Rotulo = "Caixa", Glifo = "\uE8AE",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro,
            Abas =
            [
                new AbaMenu("Caixa", ChaveCaixa),
                new AbaMenu("Fechamento", ChaveFechamento),
                new AbaMenu("Fluxo", ChaveFluxo)
            ]
        },
        new ItemMenuModulo
        {
            Chave = ChaveGrupoContas, Rotulo = "Contas", Glifo = "\uE8F1",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro,
            Abas =
            [
                // "Quem me deve" vem logo depois de "A pagar/receber" pela razão que já
                // estava escrita aqui: são a mesma dívida vista de dois lados — uma
                // responde "o que vence", a outra "quem deve".
                new AbaMenu("A pagar/receber", ChaveContas),
                new AbaMenu("Quem me deve", ChaveInadimplencia),
                new AbaMenu("Plano de contas", ChavePlanoContas)
            ]
        },
        new ItemMenuModulo
        {
            Chave = ChaveGrupoRecebimentos, Rotulo = "Recebimentos", Glifo = "\uE896",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro,
            Abas =
            [
                new AbaMenu("Receb\u00EDveis de cart\u00E3o", ChaveRecebiveis),
                new AbaMenu("Concilia\u00E7\u00E3o", ChaveConciliacao),
                new AbaMenu("Taxas e impostos", ChaveTaxas)
            ]
        },

        // ===== Telas de assunto próprio =====
        new ItemMenuModulo
        {
            Chave = ChavePacotes, Rotulo = "Pacotes / Sess\u00F5es", Glifo = "\uE719",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VenderPacote
        },
        new ItemMenuModulo
        {
            Chave = ChaveEstoque, Rotulo = "Estoque", Glifo = "\uE7B8",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro
        },
        new ItemMenuModulo
        {
            Chave = ChaveRepasses, Rotulo = "Repasses", Glifo = "\uE8C8",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro
        },

        // ===== Sub-telas =====
        // Elas continuam sendo ITENS, e é isso que faz `NavegacaoSuite.Ir("caixa")`,
        // `Ir("fechamento-caixa")` e as outras continuarem funcionando — o shell acha o
        // item pai que as contém e abre a aba certa. Apagá-las daqui devolveria o botão
        // que não faz nada da 4ª rodada da parcela 37.
        //
        // Não levam `Oculto`: quem as esconde é o PAI, e só onde o pai existe. "Resultado
        // do mês" e "Produção" são abas de "Relatórios / BI", que a DIREÇÃO publica — no
        // `Clinica.Financeiro.exe` esse pai não existe, e as duas voltam a ser itens de
        // menu sozinhas, que é como o financeiro sempre as usou.
        new ItemMenuModulo
        {
            Chave = ChaveCaixa, Rotulo = "Caixa", Glifo = "\uE8C7",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro
        },
        new ItemMenuModulo
        {
            Chave = ChaveFechamento, Rotulo = "Fechamento de caixa", Glifo = "\uE8AE",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro
        },
        new ItemMenuModulo
        {
            Chave = ChaveFluxo, Rotulo = "Fluxo de caixa", Glifo = "\uEB05",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro
        },
        new ItemMenuModulo
        {
            Chave = ChaveContas, Rotulo = "Contas a pagar/receber", Glifo = "\uE8F1",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro
        },
        new ItemMenuModulo
        {
            Chave = ChaveInadimplencia, Rotulo = "Quem me deve", Glifo = "\uE8D1",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro
        },
        new ItemMenuModulo
        {
            Chave = ChavePlanoContas, Rotulo = "Plano de contas", Glifo = "\uE8FD",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro
        },
        new ItemMenuModulo
        {
            Chave = ChaveRecebiveis, Rotulo = "Receb\u00EDveis de cart\u00E3o", Glifo = "\uE896",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro
        },
        new ItemMenuModulo
        {
            Chave = ChaveConciliacao, Rotulo = "Concilia\u00e7\u00e3o", Glifo = "\uE8AB",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro
        },
        new ItemMenuModulo
        {
            Chave = ChaveTaxas, Rotulo = "Taxas e impostos", Glifo = "\uE9F9",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro
        },
        new ItemMenuModulo
        {
            Chave = ChaveResultado, Rotulo = "Resultado do m\u00EAs", Glifo = "\uE9D9",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro
        },
        new ItemMenuModulo
        {
            Chave = ChaveProducao, Rotulo = "Produ\u00e7\u00e3o", Glifo = "\uE9D2",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro
        }
    ];

    public void Registrar(IServiceCollection servicos)
    {
        servicos.AddTransient<CaixaViewModel>();
        // Transient de propósito: cada janela de lançamento abre com o formulário limpo.
        servicos.AddTransient<LancamentoEdicaoViewModel>();
        servicos.AddTransient<ContasViewModel>();
        servicos.AddTransient<InadimplenciaViewModel>();
        servicos.AddTransient<FluxoCaixaViewModel>();
        servicos.AddTransient<ResultadoViewModel>();
        servicos.AddTransient<FechamentoCaixaViewModel>();
        servicos.AddTransient<RecebiveisViewModel>();
        servicos.AddTransient<ConciliacaoViewModel>();
        servicos.AddTransient<ProducaoViewModel>();
        servicos.AddTransient<Clinica.Desktop.Shell.Componentes.PacotesViewModel>();
        servicos.AddTransient<EstoqueViewModel>();
        servicos.AddTransient<RepassesViewModel>();
        servicos.AddTransient<TaxasViewModel>();
        servicos.AddTransient<PlanoContasViewModel>();
        // Os ViewModels de formulário (venda de pacote, movimento de estoque, regra de
        // repasse) são construídos à mão pelas telas: cada janela abre limpa e precisa
        // receber o contexto (o item, o pacote) no construtor.
    }

    public object? CriarTela(string chave, IServiceProvider servicos) => chave switch
    {
        ChaveCaixa => new CaixaView { DataContext = servicos.GetRequiredService<CaixaViewModel>() },
        ChaveContas => new ContasView { DataContext = servicos.GetRequiredService<ContasViewModel>() },
        ChaveInadimplencia => new InadimplenciaView { DataContext = servicos.GetRequiredService<InadimplenciaViewModel>() },
        ChaveFluxo => new FluxoCaixaView { DataContext = servicos.GetRequiredService<FluxoCaixaViewModel>() },
        ChaveResultado => new ResultadoView { DataContext = servicos.GetRequiredService<ResultadoViewModel>() },
        ChaveFechamento => new FechamentoCaixaView { DataContext = servicos.GetRequiredService<FechamentoCaixaViewModel>() },
        ChaveRecebiveis => new RecebiveisView { DataContext = servicos.GetRequiredService<RecebiveisViewModel>() },
        ChaveConciliacao => new ConciliacaoView { DataContext = servicos.GetRequiredService<ConciliacaoViewModel>() },
        ChaveProducao => new ProducaoView { DataContext = servicos.GetRequiredService<ProducaoViewModel>() },
        // A tela SUBIU para o shell (parcela 60), como a sala de infusão na 48: quem
        // vende as dez sessões ao paciente é a RECEPÇÃO, no balcão, e a única porta estava
        // no app do Financeiro. Os dois módulos publicam a MESMA chave — a dedupe do
        // ShellViewModel faz o Gerente, que carrega os dois, mostrar uma linha só.
        ChavePacotes => new Clinica.Desktop.Shell.Componentes.PacotesView
        {
            DataContext = servicos
                .GetRequiredService<Clinica.Desktop.Shell.Componentes.PacotesViewModel>()
        },
        ChaveEstoque => new EstoqueView { DataContext = servicos.GetRequiredService<EstoqueViewModel>() },
        ChaveRepasses => new RepassesView { DataContext = servicos.GetRequiredService<RepassesViewModel>() },
        ChaveTaxas => new TaxasView { DataContext = servicos.GetRequiredService<TaxasViewModel>() },
        ChavePlanoContas => new PlanoContasView { DataContext = servicos.GetRequiredService<PlanoContasViewModel>() },
        _ => null
    };
}
