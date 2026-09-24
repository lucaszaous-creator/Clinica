using Clinica.Desktop.Shell.Modulos;
using Clinica.Desktop.ViewModels;
using Clinica.Desktop.Views;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
namespace Clinica.Faturamento.Modulo;

/// <summary>Mesmas operações e componentes de cobrança no Faturamento e no Gerente.</summary>
public sealed class ModuloFaturamento : IModuloApp
{
    public string Nome => "Faturamento";
    private static readonly (string Chave, string Rotulo, Secao Secao, Permissao Permissao)[] Destinos =
    [
        ("faturamento-pendencias", "Pendências e baixas", Secao.Pendencias, Permissao.VerFaturamento),
        ("faturamento-guias", "Consultar guias", Secao.ConsultaGuias, Permissao.VerFaturamento),
        ("faturamento-faturados", "Faturados", Secao.Faturados, Permissao.VerFaturamento),
        ("faturamento-glosas", "Glosas e recursos", Secao.Glosas, Permissao.VerFaturamento),
        ("faturamento-nc", "Não conformidades", Secao.NaoConformidades, Permissao.VerFaturamento),
        ("faturamento-tiss", "Lotes e XML TISS", Secao.Tiss, Permissao.VerFaturamento),
        ("faturamento-relatorios", "Relatórios de guias", Secao.Relatorios, Permissao.VerIndicadores),
        ("faturamento-parametros", "Regras e catálogos TISS", Secao.Parametros, Permissao.ConfigurarFaturamento)
    ];
    public IReadOnlyList<ItemMenuModulo> Itens { get; } = new[] {
        new ItemMenuModulo { Chave = ChavesSuite.FaturamentoTiss, Rotulo = "Faturamento de guias", Glifo = "\uE8C7", Icone = "recibo", Grupo = GrupoSidebar.Financeiro,
            Requer = Permissao.VerFaturamento, Abas = new[] { new AbaMenu("Resumo", "faturamento-resumo") }.Concat(Destinos.Take(6).Select(d => new AbaMenu(d.Rotulo,d.Chave))).ToArray() }
    }.Concat(Destinos.Select(d => new ItemMenuModulo { Chave = d.Chave, Rotulo = d.Rotulo, Glifo = "\uE8C7", Icone = "recibo", Requer = d.Permissao,
        Grupo = d.Secao == Secao.Parametros ? GrupoSidebar.Gestao : d.Secao == Secao.Relatorios ? GrupoSidebar.Inteligencia : GrupoSidebar.Financeiro })).ToArray();
    public void Registrar(IServiceCollection s)
    {
        s.AddTransient<MainViewModel>();
        s.AddTransient<DashboardViewModel>();
        s.AddTransient<NaoConformidadesViewModel>();
        s.AddTransient<BaixaViewModel>();
        s.AddTransient<RelatoriosViewModel>();
        s.AddTransient<FaturadosViewModel>();
        s.AddTransient<GlosasViewModel>();
        s.AddTransient<TissViewModel>();
        s.AddTransient<ConsultaGuiasViewModel>();
        s.AddTransient<ParametrosViewModel>();
    }
    public object? CriarTela(string chave, IServiceProvider servicos)
    {
        var destino = Destinos.FirstOrDefault(d => d.Chave == chave);
        if (destino.Chave is null) return null;
        SessaoUsuario.Atual.Exigir(destino.Permissao, "abrir " + destino.Rotulo);
        var vm = servicos.GetRequiredService<MainViewModel>();
        vm.NavegarCommand.Execute(destino.Secao);
        return new FaturamentoHostView { DataContext = vm };
    }
}
