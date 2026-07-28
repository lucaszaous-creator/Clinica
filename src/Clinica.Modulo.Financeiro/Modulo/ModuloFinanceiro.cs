using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using Clinica.Financeiro.ViewModels;
using Clinica.Financeiro.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Financeiro.Modulo;

/// <summary>
/// Módulo Financeiro: Caixa (entradas/saídas), Conciliação (guias efetivadas que
/// ainda não viraram receita) e Produção (volume do faturamento).
/// </summary>
public sealed class ModuloFinanceiro : IModuloApp
{
    public const string ChaveCaixa = "caixa";
    public const string ChaveConciliacao = "conciliacao";
    public const string ChaveProducao = "producao";

    public string Nome => "Financeiro";

    // Todas exigem VerFinanceiro (parcela 5): dinheiro não aparece para quem só
    // trabalha o balcão, a menos que a direção conceda a permissão.
    public IReadOnlyList<ItemMenuModulo> Itens { get; } =
    [
        new ItemMenuModulo { Chave = ChaveCaixa, Rotulo = "Caixa", Glifo = "\uE8C7", Requer = Permissao.VerFinanceiro },
        new ItemMenuModulo { Chave = ChaveConciliacao, Rotulo = "Conciliação", Glifo = "\uE8AB", Requer = Permissao.VerFinanceiro },
        new ItemMenuModulo { Chave = ChaveProducao, Rotulo = "Produção", Glifo = "\uE9D2", Requer = Permissao.VerFinanceiro }
    ];

    public void Registrar(IServiceCollection servicos)
    {
        servicos.AddTransient<CaixaViewModel>();
        // Transient de propósito: cada janela de lançamento abre com o formulário limpo.
        servicos.AddTransient<LancamentoEdicaoViewModel>();
        servicos.AddTransient<ConciliacaoViewModel>();
        servicos.AddTransient<ProducaoViewModel>();
    }

    public object? CriarTela(string chave, IServiceProvider servicos) => chave switch
    {
        ChaveCaixa => new CaixaView { DataContext = servicos.GetRequiredService<CaixaViewModel>() },
        ChaveConciliacao => new ConciliacaoView { DataContext = servicos.GetRequiredService<ConciliacaoViewModel>() },
        ChaveProducao => new ProducaoView { DataContext = servicos.GetRequiredService<ProducaoViewModel>() },
        _ => null
    };
}
