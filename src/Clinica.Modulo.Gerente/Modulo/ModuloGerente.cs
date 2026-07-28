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
/// (campanhas) e, por último, quem pode o quê — que se mexe raramente.
/// </summary>
public sealed class ModuloGerente : IModuloApp
{
    public const string ChaveIndicadores = "indicadores";
    public const string ChaveFaturamento = "faturamento-gerencial";
    public const string ChaveCampanhas = "campanhas";
    public const string ChaveAcessos = "acessos";

    public string Nome => "Direção";

    public IReadOnlyList<ItemMenuModulo> Itens { get; } =
    [
        new ItemMenuModulo
        {
            Chave = ChaveIndicadores, Rotulo = "Indicadores", Glifo = "\uE9D2",
            Requer = Permissao.VerIndicadores
        },
        new ItemMenuModulo
        {
            Chave = ChaveFaturamento, Rotulo = "Faturamento", Glifo = "\uE8C7",
            Requer = Permissao.VerFaturamento
        },
        new ItemMenuModulo
        {
            Chave = ChaveCampanhas, Rotulo = "Campanhas", Glifo = "\uE715",
            Requer = Permissao.GerenciarCampanhas
        },
        new ItemMenuModulo
        {
            Chave = ChaveAcessos, Rotulo = "Acessos", Glifo = "\uE72E",
            Requer = Permissao.GerenciarUsuarios
        }
    ];

    public void Registrar(IServiceCollection servicos)
    {
        servicos.AddTransient<IndicadoresViewModel>();
        servicos.AddTransient<FaturamentoGerencialViewModel>();
        servicos.AddTransient<CampanhasViewModel>();
        servicos.AddTransient<AcessosViewModel>();
        // UsuarioEdicaoViewModel é construído à mão pela tela: precisa receber o id do
        // usuário no construtor, como os demais formulários da suíte.
    }

    public object? CriarTela(string chave, IServiceProvider servicos) => chave switch
    {
        ChaveIndicadores => new IndicadoresView
        {
            DataContext = servicos.GetRequiredService<IndicadoresViewModel>()
        },
        ChaveFaturamento => new FaturamentoGerencialView
        {
            DataContext = servicos.GetRequiredService<FaturamentoGerencialViewModel>()
        },
        ChaveCampanhas => new CampanhasView
        {
            DataContext = servicos.GetRequiredService<CampanhasViewModel>()
        },
        ChaveAcessos => new AcessosView
        {
            DataContext = servicos.GetRequiredService<AcessosViewModel>()
        },
        _ => null
    };
}
