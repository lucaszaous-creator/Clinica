using System.Collections.ObjectModel;
using Clinica.Desktop.Shell.Modulos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell;

/// <summary>
/// Navegação do shell: monta a sidebar a partir dos módulos carregados e troca a
/// tela ativa. Equivale ao MainViewModel do faturamento, mas sem conhecer nenhuma
/// tela — quem resolve a View é o próprio módulo (<see cref="IModuloApp.CriarTela"/>).
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly IReadOnlyList<IModuloApp> _modulos;
    private readonly IServiceProvider _servicos;

    /// <summary>Título da janela (nome do app, ex.: "Recepção").</summary>
    public string Titulo { get; }

    public IReadOnlyList<GrupoMenuModulo> Grupos { get; }

    /// <summary>Todos os itens, em ordem — usado para achar o item por chave.</summary>
    public ObservableCollection<ItemMenuModulo> Itens { get; } = [];

    [ObservableProperty]
    private object? _telaAtual;

    [ObservableProperty]
    private string _tituloTela = string.Empty;

    /// <summary>Módulo da tela ativa — primeiro nível do breadcrumb.</summary>
    [ObservableProperty]
    private string _moduloAtual = string.Empty;

    /// <summary>Quem está usando o app — vai no canto da barra superior.</summary>
    public string UsuarioRotulo { get; }

    /// <summary>Data de hoje na barra superior — a mesma referência de todas as telas.</summary>
    public string DataHoje { get; } = DateTime.Today.ToString("ddd, dd/MM/yyyy");

    /// <summary>
    /// Sidebar recolhida (248 → 56px). Mesmo comportamento do app de faturamento: em
    /// 1366×768 recolher é o modo confortável, e o atalho é Ctrl+B.
    /// </summary>
    [ObservableProperty]
    private bool _menuRecolhido;

    // ===== Pesquisa global =====

    [ObservableProperty]
    private string _textoPesquisa = string.Empty;

    [ObservableProperty]
    private bool _pesquisaAberta;

    /// <summary>Seções que casam com o que foi digitado (paleta de comandos).</summary>
    public ObservableCollection<ItemMenuModulo> ResultadosPesquisa { get; } = [];

    public ShellViewModel(string titulo, IEnumerable<IModuloApp> modulos, IServiceProvider servicos)
    {
        Titulo = titulo;
        _modulos = modulos.ToList();
        _servicos = servicos;

        // Sem sessão (teste, ou app aberto por um caminho que não passa pelo login) o
        // menu aparece inteiro: filtrar por uma permissão que ninguém tem esconderia
        // tudo, e sidebar vazia parece defeito, não segurança.
        var sessao = servicos.GetService<SessaoUsuario>();
        UsuarioRotulo = sessao?.Rotulo ?? Environment.UserName;

        foreach (var modulo in _modulos)
        {
            // Pode() já libera quando não há sessão autenticada — a regra mora nela.
            foreach (var item in modulo.Itens.Where(i => sessao?.Pode(i.Requer) != false))
            {
                item.ModuloNome = modulo.Nome;
                Itens.Add(item);
            }
        }

        // Agrupa por SEÇÃO TEMÁTICA, não por módulo: no Gerente, que carrega os três,
        // agrupar por módulo daria cabeçalhos que explicam a arquitetura em vez do
        // trabalho. A ordem dos grupos é a do enum; dentro do grupo, a ordem em que os
        // módulos foram carregados — que já é a ordem do dia de trabalho.
        // `Itens` guarda TUDO o que é navegável; a sidebar mostra só o que não é oculto.
        // A separação é o que permite uma tela ser destino de NavegacaoSuite sem ocupar
        // linha no menu — ver ItemMenuModulo.Oculto.
        Grupos = Itens
            .Where(i => !i.Oculto)
            .GroupBy(i => i.Grupo)
            .OrderBy(g => g.Key)
            .Select(g => new GrupoMenuModulo(GruposSidebar.Rotulo(g.Key), g.ToList()))
            .ToList();

        // Uma tela pode pedir para abrir outra (o painel da direção leva ao assunto do
        // alerta). Ligado aqui porque é este objeto que sabe navegar.
        NavegacaoSuite.Ligar(IrPara);

        // Abre no item marcado como inicial; sem ele — ou sem permissão para vê-lo —, no
        // primeiro disponível, como sempre. Existe porque o Gerente Geral carrega os três
        // módulos e abria no painel da RECEPÇÃO: quem manda na clínica entrava no sistema
        // e via a fila do balcão.
        // A abertura sai do que é VISÍVEL: cair numa tela oculta seria abrir o app numa
        // tela que não se sabe como alcançar de novo.
        var visiveis = Itens.Where(i => !i.Oculto).ToList();
        var abertura = visiveis.FirstOrDefault(i => i.Inicial) ?? visiveis.FirstOrDefault();
        if (abertura is not null) Navegar(abertura);
    }

    /// <summary>
    /// Navega por CHAVE, atendendo <see cref="NavegacaoSuite"/>. Devolve false quando o
    /// destino não está na sidebar desta pessoa — módulo não carregado neste executável,
    /// ou permissão que ela não tem.
    /// </summary>
    private bool IrPara(string chave, bool apenasConferir)
    {
        var item = Itens.FirstOrDefault(i => i.Chave == chave);
        if (item is null) return false;
        if (!apenasConferir) Navegar(item);
        return true;
    }

    [RelayCommand]
    public void Navegar(ItemMenuModulo? item)
    {
        if (item is null) return;

        foreach (var i in Itens) i.EstaAtivo = ReferenceEquals(i, item);

        // O módulo dono é quem sabe construir a tela.
        foreach (var modulo in _modulos)
        {
            if (modulo.Nome != item.ModuloNome) continue;
            var tela = modulo.CriarTela(item.Chave, _servicos);
            if (tela is null) continue;

            TelaAtual = tela;
            TituloTela = item.Rotulo;
            ModuloAtual = GruposSidebar.Rotulo(item.Grupo);
            return;
        }
    }

    [RelayCommand]
    private void AlternarMenu() => MenuRecolhido = !MenuRecolhido;

    /// <summary>
    /// Pesquisa global: paleta de seções. Digitar e dar Enter navega para a primeira —
    /// num app de 15 telas, achar pelo nome é mais rápido do que caçar na sidebar,
    /// sobretudo com ela recolhida.
    ///
    /// Só seções, de propósito: buscar paciente daqui exigiria o shell saber qual tela
    /// de qual módulo abre uma ficha, e o shell não conhece tela nenhuma. Quem busca
    /// paciente é o <see cref="Componentes.SeletorPacienteViewModel"/>, dentro das telas.
    /// </summary>
    partial void OnTextoPesquisaChanged(string value)
    {
        ResultadosPesquisa.Clear();

        var termo = value.Trim();
        if (termo.Length == 0)
        {
            PesquisaAberta = false;
            return;
        }

        foreach (var item in Itens.Where(i =>
                     i.Rotulo.Contains(termo, StringComparison.OrdinalIgnoreCase)
                     || GruposSidebar.Rotulo(i.Grupo).Contains(termo, StringComparison.OrdinalIgnoreCase)))
            ResultadosPesquisa.Add(item);

        PesquisaAberta = ResultadosPesquisa.Count > 0;
    }

    [RelayCommand]
    private void NavegarResultado(ItemMenuModulo? item)
    {
        item ??= ResultadosPesquisa.FirstOrDefault();
        if (item is null) return;

        FecharPesquisa();
        Navegar(item);
    }

    [RelayCommand]
    private void FecharPesquisa()
    {
        PesquisaAberta = false;
        TextoPesquisa = string.Empty;
    }

}
