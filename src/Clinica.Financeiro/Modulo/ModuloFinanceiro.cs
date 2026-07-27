using Clinica.Desktop.Shell.Modulos;
using Clinica.Financeiro.ViewModels;
using Clinica.Financeiro.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Financeiro.Modulo;

/// <summary>
/// Módulo Financeiro. Nesta fase publica a Produção (conferência do faturamento e
/// base do repasse). Caixa e repasse com valores dependem de entidades monetárias
/// que ainda não existem no domínio — ver docs/arquitetura-multi-exe.md.
/// </summary>
public sealed class ModuloFinanceiro : IModuloApp
{
    public const string ChaveProducao = "producao";

    public string Nome => "Financeiro";

    public IReadOnlyList<ItemMenuModulo> Itens { get; } =
    [
        new ItemMenuModulo { Chave = ChaveProducao, Rotulo = "Produção", Glifo = "\uE9D2" }
    ];

    public void Registrar(IServiceCollection servicos)
    {
        servicos.AddTransient<ProducaoViewModel>();
    }

    public object? CriarTela(string chave, IServiceProvider servicos) => chave switch
    {
        ChaveProducao => new ProducaoView { DataContext = servicos.GetRequiredService<ProducaoViewModel>() },
        _ => null
    };
}
