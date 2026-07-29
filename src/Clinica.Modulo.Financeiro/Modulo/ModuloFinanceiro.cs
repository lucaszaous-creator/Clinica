using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using Clinica.Financeiro.ViewModels;
using Clinica.Financeiro.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Financeiro.Modulo;

/// <summary>
/// Módulo Financeiro: Caixa (entradas/saídas), Contas a pagar e a receber (o que vence),
/// Fluxo de caixa (a série dos meses e para onde o dinheiro vai), Conciliação (guias efetivadas que ainda não viraram receita), Produção (volume do
/// faturamento), Pacotes (o que a clínica vende), Estoque (insumos), Repasses (o que cada
/// profissional tem a receber), Taxas e impostos, e o Plano de contas.
///
/// A ordem é a do dia de trabalho: primeiro o dinheiro que entra e sai (Caixa), depois o
/// que ainda VAI entrar ou sair e quando (Contas), a tendência (Fluxo), o que já foi
/// efetivado no convênio e
/// não entrou (Conciliação), o volume (Produção), o que foi vendido (Pacotes), o que se
/// gasta (Estoque), o que se paga (Repasses), o que descontam de cada recebimento (Taxas)
/// e, por último, o cadastro que classifica tudo — que se mexe raramente.
/// </summary>
public sealed class ModuloFinanceiro : IModuloApp
{
    public const string ChaveCaixa = "caixa";
    public const string ChaveContas = "contas";
    public const string ChaveFluxo = "fluxo-caixa";
    public const string ChaveConciliacao = "conciliacao";
    public const string ChaveProducao = "producao";
    public const string ChavePacotes = "pacotes";
    public const string ChaveEstoque = "estoque";
    public const string ChaveRepasses = "repasses";
    public const string ChaveTaxas = "taxas";
    public const string ChavePlanoContas = "plano-contas";

    public string Nome => "Financeiro";

    // Todas exigem VerFinanceiro (parcela 5): dinheiro não aparece para quem só
    // trabalha o balcão, a menos que a direção conceda a permissão.
    // Tudo aqui \u00E9 da se\u00e7\u00e3o FINANCEIRO da proposta: \u00E9 o mesmo assunto \u2014 o dinheiro da
    // cl\u00EDnica \u2014, visto de sete \u00E2ngulos.
    public IReadOnlyList<ItemMenuModulo> Itens { get; } =
    [
        new ItemMenuModulo
        {
            Chave = ChavePacotes, Rotulo = "Pacotes / Sess\u00F5es", Glifo = "\uE719",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro
        },
        new ItemMenuModulo
        {
            Chave = ChaveCaixa, Rotulo = "Financeiro", Glifo = "\uE8C7",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro
        },
        new ItemMenuModulo
        {
            Chave = ChaveContas, Rotulo = "Contas a pagar/receber", Glifo = "\uE8F1",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro
        },
        new ItemMenuModulo
        {
            Chave = ChaveFluxo, Rotulo = "Fluxo de caixa", Glifo = "\uEB05",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro
        },
        new ItemMenuModulo
        {
            Chave = ChaveConciliacao, Rotulo = "Concilia\u00e7\u00e3o", Glifo = "\uE8AB",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro
        },
        new ItemMenuModulo
        {
            Chave = ChaveProducao, Rotulo = "Produ\u00e7\u00e3o", Glifo = "\uE9D2",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro
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
        new ItemMenuModulo
        {
            Chave = ChaveTaxas, Rotulo = "Taxas e impostos", Glifo = "\uE9D9",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro
        },
        new ItemMenuModulo
        {
            Chave = ChavePlanoContas, Rotulo = "Plano de contas", Glifo = "\uE8FD",
            Grupo = GrupoSidebar.Financeiro, Requer = Permissao.VerFinanceiro
        }
    ];

    public void Registrar(IServiceCollection servicos)
    {
        servicos.AddTransient<CaixaViewModel>();
        // Transient de propósito: cada janela de lançamento abre com o formulário limpo.
        servicos.AddTransient<LancamentoEdicaoViewModel>();
        servicos.AddTransient<ContasViewModel>();
        servicos.AddTransient<FluxoCaixaViewModel>();
        servicos.AddTransient<ConciliacaoViewModel>();
        servicos.AddTransient<ProducaoViewModel>();
        servicos.AddTransient<PacotesViewModel>();
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
        ChaveFluxo => new FluxoCaixaView { DataContext = servicos.GetRequiredService<FluxoCaixaViewModel>() },
        ChaveConciliacao => new ConciliacaoView { DataContext = servicos.GetRequiredService<ConciliacaoViewModel>() },
        ChaveProducao => new ProducaoView { DataContext = servicos.GetRequiredService<ProducaoViewModel>() },
        ChavePacotes => new PacotesView { DataContext = servicos.GetRequiredService<PacotesViewModel>() },
        ChaveEstoque => new EstoqueView { DataContext = servicos.GetRequiredService<EstoqueViewModel>() },
        ChaveRepasses => new RepassesView { DataContext = servicos.GetRequiredService<RepassesViewModel>() },
        ChaveTaxas => new TaxasView { DataContext = servicos.GetRequiredService<TaxasViewModel>() },
        ChavePlanoContas => new PlanoContasView { DataContext = servicos.GetRequiredService<PlanoContasViewModel>() },
        _ => null
    };
}
