using Clinica.Desktop.Shell.Modulos;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop;

/// <summary>Catálogo do executável Faturamento: apenas guias e suas operações.</summary>
public sealed class ModuloFaturamentoAplicativo : IModuloApp
{
    // Os componentes administrativos da ficha continuam disponíveis a partir de uma
    // guia. Registrar suas dependências não publica os menus dos outros aplicativos.
    private readonly IModuloApp[] _componentes =
    [
        new Clinica.Faturamento.Modulo.ModuloFaturamento(),
        new Clinica.Recepcao.Modulo.ModuloRecepcao(),
        new Clinica.Clinico.Modulo.ModuloClinico(),
        new Clinica.Gerente.Modulo.ModuloGerente()
    ];

    public string Nome => "Faturamento";
    public IReadOnlyList<ItemMenuModulo> Itens { get; }

    public ModuloFaturamentoAplicativo()
    {
        var operacoes = _componentes[0].Itens;
        var raiz = operacoes.Single(i => i.Chave == ChavesSuite.FaturamentoTiss);
        var resumo = _componentes[3].Itens.Single(i => i.Chave == "faturamento-resumo");
        var abas = raiz.Abas.Concat(operacoes
            .Where(i => i.Chave != raiz.Chave && !raiz.Abas.Any(a => a.Chave == i.Chave))
            .Select(i => new AbaMenu(i.Rotulo, i.Chave))).ToArray();
        Itens = operacoes.Append(resumo).Select(i => new ItemMenuModulo
        {
            Chave = i.Chave, Rotulo = i.Rotulo, Glifo = i.Glifo, Icone = i.Icone,
            Grupo = GrupoSidebar.Financeiro, Requer = i.Requer, RequerAlgum = i.RequerAlgum,
            PerfilExclusivo = i.PerfilExclusivo, Inicial = i.Chave == raiz.Chave,
            Oculto = i.Chave != raiz.Chave, Abas = i.Chave == raiz.Chave ? abas : i.Abas
        }).ToArray();
    }

    public void Registrar(IServiceCollection servicos)
    {
        foreach (var componente in _componentes) componente.Registrar(servicos);
    }

    public object? CriarTela(string chave, IServiceProvider servicos)
    {
        // Bloqueia também navegação direta: ocultar um menu não retira sua rota.
        if (!Itens.Any(i => i.Chave == chave)) return null;
        var componente = _componentes.FirstOrDefault(m => m.Itens.Any(i => i.Chave == chave));
        return componente?.CriarTela(chave, servicos);
    }
}
