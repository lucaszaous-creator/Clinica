using Clinica.Desktop.Blazor;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using Clinica.Ui.Blazor.Telas;
using Microsoft.Extensions.DependencyInjection;
namespace Clinica.Faturamento.Modulo;

/// <summary>
/// Faturamento de guias — as mesmas operações no app do Faturamento e no Gerente.
///
/// Desde out/2026 é BLAZOR (docs/faturamento-blazor.md): cada destino é uma tela do
/// <c>Clinica.Ui.Blazor</c>, no navegador ÚNICO da janela (o mesmo do Financeiro). As chaves
/// não mudaram — são contrato de navegação entre módulos. O que continua WPF aqui é só a
/// abertura do app: o aviso de pendências e a rodada BLOQUEANTE (pasta Alertas), que travam o
/// sistema antes de a área de trabalho existir.
/// </summary>
public sealed class ModuloFaturamento : IModuloApp
{
    public string Nome => "Faturamento";
    private static readonly (string Chave, string Rotulo, Type Tela, Permissao Permissao, GrupoSidebar Grupo)[] Destinos =
    [
        ("faturamento-pendencias", "Pendências e baixas", typeof(PendenciasFaturamento), Permissao.VerFaturamento, GrupoSidebar.Financeiro),
        ("faturamento-guias", "Consultar guias", typeof(ConsultaDeGuias), Permissao.VerFaturamento, GrupoSidebar.Financeiro),
        ("faturamento-faturados", "Faturados", typeof(GuiasFaturadas), Permissao.VerFaturamento, GrupoSidebar.Financeiro),
        ("faturamento-glosas", "Glosas e recursos", typeof(GlosasERecursos), Permissao.VerFaturamento, GrupoSidebar.Financeiro),
        ("faturamento-nc", "Não conformidades", typeof(NaoConformidadesFaturamento), Permissao.VerFaturamento, GrupoSidebar.Financeiro),
        ("faturamento-tiss", "Lotes e XML TISS", typeof(LotesTiss), Permissao.VerFaturamento, GrupoSidebar.Financeiro),
        ("faturamento-relatorios", "Relatórios de guias", typeof(RelatoriosDeGuias), Permissao.VerIndicadores, GrupoSidebar.Inteligencia),
        ("faturamento-parametros", "Regras e catálogos TISS", typeof(ConfiguracoesFaturamento), Permissao.ConfigurarFaturamento, GrupoSidebar.Gestao)
    ];
    public IReadOnlyList<ItemMenuModulo> Itens { get; } = new[] {
        new ItemMenuModulo { Chave = ChavesSuite.FaturamentoTiss, Rotulo = "Faturamento de guias", Glifo = "", Icone = "recibo", Grupo = GrupoSidebar.Financeiro,
            Requer = Permissao.VerFaturamento, Abas = new[] { new AbaMenu("Resumo", "faturamento-resumo") }.Concat(Destinos.Take(6).Select(d => new AbaMenu(d.Rotulo,d.Chave))).ToArray() }
    }.Concat(Destinos.Select(d => new ItemMenuModulo { Chave = d.Chave, Rotulo = d.Rotulo, Glifo = "", Icone = "recibo", Requer = d.Permissao,
        Grupo = d.Grupo })).ToArray();

    public void Registrar(IServiceCollection s)
    {
        // Sem ViewModel: as telas Blazor abrem escopo por operação. Aqui só a hospedagem
        // (idempotente — o Gerente carrega o Financeiro e o Faturamento, e os dois chamam).
        s.AddHospedagemBlazor();
    }

    public object? CriarTela(string chave, IServiceProvider servicos)
    {
        var destino = Destinos.FirstOrDefault(d => d.Chave == chave);
        if (destino.Chave is null) return null;
        SessaoUsuario.Atual.Exigir(destino.Permissao, "abrir " + destino.Rotulo);
        return HospedagemBlazor.Tela(destino.Tela, servicos);
    }
}
