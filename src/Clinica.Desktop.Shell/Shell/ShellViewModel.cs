using Clinica.Desktop.Controls;
using Clinica.Domain.Entities;
using System.Collections.ObjectModel;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell;

/// <summary>
/// Uma linha da paleta de busca (Ctrl+F).
///
/// Não é mais o <see cref="ItemMenuModulo"/> direto porque uma tela pode ser uma ABA
/// dentro de um item: achar "Fechamento de caixa" tem de levar a "Caixa" já na 2ª aba, e
/// para isso o resultado precisa carregar o índice junto.
/// </summary>
/// <param name="Item">O item de menu a abrir.</param>
/// <param name="Aba">Índice da aba dentro do item; 0 quando o item é uma tela só.</param>
/// <param name="Rotulo">O que casou com o termo — o nome da tela procurada.</param>
/// <param name="Caminho">O item pai, quando o achado é uma aba. Nulo no item de menu.</param>
public sealed record ResultadoPesquisa(ItemMenuModulo Item, int Aba, string Rotulo, string? Caminho);

/// <summary>
/// Navegação do shell: monta a sidebar a partir dos módulos carregados e troca a
/// tela ativa. Equivale ao MainViewModel do faturamento, mas sem conhecer nenhuma
/// tela — quem resolve a View é o próprio módulo (<see cref="IModuloApp.CriarTela"/>).
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly IReadOnlyList<IModuloApp> _modulos;
    private readonly IServiceProvider _servicos;

    /// <summary>
    /// O item cuja tela está montada. Guardado para que voltar ao MESMO item composto
    /// troque de aba em vez de reconstruir tudo.
    /// </summary>
    private ItemMenuModulo? _itemAtual;

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

    // ===== Rail de categorias + painel (parcela 55) =====

    /// <summary>
    /// A categoria cujo painel está aberto — <c>null</c> = painel fechado.
    ///
    /// Substitui o <c>MenuRecolhido</c> (248 ↔ 56px) porque o problema deixou de ser
    /// largura: no Gerente Geral a sidebar tinha <b>46 itens</b>, pedia 1.824px de altura
    /// e a janela dava 610 — recolher para 56px não mostrava um item a mais, só trocava
    /// rótulo por ícone na mesma lista rolante.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PainelDocado))]
    [NotifyPropertyChangedFor(nameof(PainelFlutuante))]
    private GrupoMenuModulo? _categoriaAberta;

    /// <summary>
    /// O painel está FIXADO (empurra o conteúdo) em vez de flutuar por cima.
    ///
    /// É a mitigação do defeito conhecido do modelo de flyout: painel que só existe
    /// enquanto o mouse está em cima é um alvo móvel — sair do corredor entre o ícone e o
    /// painel fecha o que se estava lendo, e a lista de 7 itens obriga a atravessar meia
    /// tela sem escapar. Passar o mouse ESPIA; o clique FIXA, e aí o painel não foge.
    /// Ctrl+B continua sendo o atalho, agora de fixar/soltar.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PainelDocado))]
    [NotifyPropertyChangedFor(nameof(PainelFlutuante))]
    private bool _painelFixado;

    /// <summary>Painel ancorado — ocupa coluna e empurra o conteúdo.</summary>
    public bool PainelDocado => CategoriaAberta is not null && PainelFixado;

    /// <summary>Painel espiado — flutua por cima do conteúdo e some sozinho.</summary>
    public bool PainelFlutuante => CategoriaAberta is not null && !PainelFixado;

    // ===== Pesquisa global =====

    [ObservableProperty]
    private string _textoPesquisa = string.Empty;

    [ObservableProperty]
    private bool _pesquisaAberta;

    /// <summary>Seções que casam com o que foi digitado (paleta de comandos).</summary>
    public ObservableCollection<ResultadoPesquisa> ResultadosPesquisa { get; } = [];

    /// <summary>
    /// O snackbar da janela (parcela 53). <b>Existia como serviço e não tinha onde
    /// aparecer.</b>
    ///
    /// O <c>SnackbarService</c> era registrado no <c>ShellBootstrap</c>, injetado em quase
    /// toda ViewModel da suíte e chamado 143 vezes — e o <c>ShellWindow.xaml</c> nunca
    /// renderizou o host. Só o faturamento tinha (<c>MainWindow.xaml</c>), que é de onde o
    /// componente veio. Resultado: todo "salvo com sucesso" da Recepção, do Financeiro, do
    /// Gerente e do Consultório caía no vazio desde que a suíte nasceu.
    ///
    /// É o defeito recorrente do projeto numa variante nova: não é dado gravado sem
    /// leitor nem capacidade sem porta — é <b>saída sem tela</b>. E ele é especialmente
    /// traiçoeiro porque nada falha: o serviço marshala pelo Dispatcher, atualiza o
    /// próprio estado e ninguém nunca observa esse estado. Build verde, teste verde, e a
    /// única forma de notar é clicar em Salvar e reparar que a tela não respondeu.
    ///
    /// Null quando não há serviço (testes, ou app aberto fora do bootstrap): o binding
    /// simplesmente não acha nada e a janela segue.
    /// </summary>
    public SnackbarService? Snackbar { get; }

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

        // A MESMA instância que as ViewModels recebem por ISnackbarService — o registro é
        // singleton, e é isso que faz o que elas escrevem chegar a esta janela.
        Snackbar = servicos.GetService<SnackbarService>();

        foreach (var modulo in _modulos)
        {
            // Pode() já libera quando não há sessão autenticada — a regra mora nela.
            foreach (var item in modulo.Itens.Where(i => sessao?.Pode(i.Requer) != false))
            {
                // Dois módulos podem publicar a MESMA chave quando a tela subiu para o
                // shell e pertence aos dois (a Sala de infusão, parcela 48: a enfermagem
                // alcança pela Recepção, o profissional pelo Consultório). No Gerente, que
                // carrega todos, isso duplicaria a linha na sidebar — e item repetido faz
                // a pessoa achar que são telas diferentes e clicar nas duas para descobrir
                // que não são. Vence o PRIMEIRO módulo carregado, que é a ordem do dia de
                // trabalho.
                if (Itens.Any(i => i.Chave == item.Chave)) continue;

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
        //
        // Um item COMPOSTO só entra se sobrar alguma aba: num executável que não carrega
        // o módulo das abas dele — ou para quem não tem a permissão de nenhuma —, o item
        // abriria uma régua de abas vazia, que se lê como tela quebrada. Este filtro roda
        // DEPOIS do laço acima porque só então `Itens` conhece todas as chaves.
        var compostos = Itens
            .Where(i => !i.Oculto && i.Abas.Count > 0 && AbasDisponiveis(i).Count > 0)
            .ToList();

        // ⚠️ A TELA ÓRFÃ — a armadilha desta parcela, e ela é a nona ocorrência do defeito
        // recorrente do projeto vestida de arquitetura nova.
        //
        // Um item composto é declarado por UM módulo, mas compõe telas de vários: quem
        // publica "Relatórios / BI" é a Direção, e duas das abas dele ("Resultado do mês"
        // e "Produção") são telas do FINANCEIRO. No Gerente Geral, que carrega todos, isso
        // funciona. No `Clinica.Financeiro.exe`, que não carrega a Direção, o item pai
        // simplesmente não existe — e se as sub-telas fossem escondidas por decreto, elas
        // sumiriam do único app onde alguém as usa todo dia.
        //
        // Por isso quem esconde a sub-tela NÃO é uma marca nela: é o PAI, e só enquanto o
        // pai está presente. Sem pai, a tela volta a ser um item de menu comum, no grupo
        // que ela sempre declarou. `Oculto` continua significando outra coisa e não se
        // confunde com isto — é a tela que nunca deve aparecer sozinha (as cinco telas
        // clínicas, que só existem com paciente escolhido).
        var reivindicadas = compostos
            .SelectMany(i => i.Abas.Select(a => a.Chave))
            .ToHashSet(StringComparer.Ordinal);

        Grupos = Itens
            .Where(i => !i.Oculto
                        && !reivindicadas.Contains(i.Chave)
                        && (i.Abas.Count == 0 || AbasDisponiveis(i).Count > 0))
            .GroupBy(i => i.Grupo)
            .OrderBy(g => g.Key)
            // Dentro do grupo vale a ordem de carregamento dos módulos — que já é a ordem
            // do dia de trabalho —, com UMA exceção: a tela de ABERTURA vem primeira.
            //
            // Sem isto ela cai onde o módulo dono foi carregado, e o dono da abertura é a
            // Direção, que é a ÚLTIMA da lista: no Gerente Geral o "Painel" abria o app e
            // aparecia no fim de GESTÃO, depois de quatro itens do balcão. É a mesma
            // desordem que a parcela 22 corrigiu com `Inicial`, reaparecendo um nível
            // abaixo — lá era em qual tela o app abria, aqui é onde essa tela fica.
            .Select(g => new GrupoMenuModulo(
                g.Key, g.OrderByDescending(i => i.Inicial).ToList()))
            .ToList();

        // Uma tela pode pedir para abrir outra (o painel da direção leva ao assunto do
        // alerta). Ligado aqui porque é este objeto que sabe navegar.
        NavegacaoSuite.Ligar(IrPara);

        // Abre no item marcado como inicial; sem ele — ou sem permissão para vê-lo —, no
        // primeiro disponível, como sempre. Existe porque o Gerente Geral carrega os três
        // módulos e abria no painel da RECEPÇÃO: quem manda na clínica entrava no sistema
        // e via a fila do balcão.
        // A abertura sai do que é VISÍVEL: cair numa tela oculta seria abrir o app numa
        // tela que não se sabe como alcançar de novo. Sai de `Grupos`, e não de `Itens`,
        // porque é `Grupos` que já descartou o item composto sem aba nenhuma.
        var visiveis = Grupos.SelectMany(g => g.Itens).ToList();
        var abertura = visiveis.FirstOrDefault(i => i.Inicial) ?? visiveis.FirstOrDefault();
        if (abertura is not null) Navegar(abertura);
    }

    /// <summary>
    /// As abas de um item composto que EXISTEM neste executável, na ordem declarada.
    ///
    /// Uma aba some quando o módulo dono dela não foi carregado (a Recepção não carrega o
    /// Consultório) ou quando quem está logado não tem a permissão da sub-tela — as duas
    /// coisas já estão respondidas por `Itens`, que é a lista do que esta pessoa alcança.
    /// </summary>
    private List<(AbaMenu Aba, ItemMenuModulo Item)> AbasDisponiveis(ItemMenuModulo item)
    {
        var achadas = new List<(AbaMenu, ItemMenuModulo)>();
        foreach (var aba in item.Abas)
        {
            var alvo = Itens.FirstOrDefault(i => i.Chave == aba.Chave);
            if (alvo is not null) achadas.Add((aba, alvo));
        }
        return achadas;
    }

    /// <summary>
    /// Navega por CHAVE, atendendo <see cref="NavegacaoSuite"/>. Devolve false quando o
    /// destino não está na sidebar desta pessoa — módulo não carregado neste executável,
    /// ou permissão que ela não tem.
    ///
    /// ⚠️ A chave de uma tela que virou SUB-ABA continua valendo, e é isto que a faz
    /// valer: procura-se primeiro um item que a contenha, e abre-se o item pai já na aba
    /// certa. Sem este caminho, consolidar telas em abas quebraria toda a navegação entre
    /// módulos — o painel da direção leva ao "fechamento-caixa" por chave, e essa chave
    /// deixou de ser um item da sidebar para virar a 2ª aba de "Caixa".
    /// </summary>
    private bool IrPara(string chave, bool apenasConferir)
    {
        var pai = Grupos
            .SelectMany(g => g.Itens)
            .FirstOrDefault(i => i.Abas.Any(a => a.Chave == chave));

        if (pai is not null)
        {
            if (!apenasConferir)
            {
                var indice = AbasDisponiveis(pai).FindIndex(p => p.Aba.Chave == chave);
                Navegar(pai, indice < 0 ? 0 : indice);
            }
            return true;
        }

        var item = Itens.FirstOrDefault(i => i.Chave == chave);
        if (item is null) return false;
        if (!apenasConferir) Navegar(item);
        return true;
    }

    [RelayCommand]
    public void Navegar(ItemMenuModulo? item) => Navegar(item, 0);

    public void Navegar(ItemMenuModulo? item, int abaInicial)
    {
        if (item is null) return;

        // Clicar de novo no item já aberto só troca de aba — remontar a tela do zero
        // jogaria fora o que a pessoa tivesse digitado nas outras abas.
        if (ReferenceEquals(_itemAtual, item) && TelaAtual is TelaComAbas jaAberta)
        {
            jaAberta.Selecionar(abaInicial);
            return;
        }

        _itemAtual = item;
        foreach (var i in Itens) i.EstaAtivo = ReferenceEquals(i, item);
        foreach (var g in Grupos) g.ContemAtual = g.Itens.Contains(item);

        var tela = item.Abas.Count > 0 ? MontarComposta(item, abaInicial) : MontarTela(item);
        if (tela is null) return;

        TelaAtual = tela;
        TituloTela = item.Rotulo;
        ModuloAtual = GruposSidebar.Rotulo(item.Grupo);

        // Escolhida a tela, o painel flutuante já cumpriu o papel dele. Fixado, ele fica:
        // quem fixou quer a lista à mão para ir à próxima.
        if (!PainelFixado) CategoriaAberta = null;
    }

    /// <summary>Monta a tela de um item simples pedindo ao módulo dono.</summary>
    private object? MontarTela(ItemMenuModulo item)
    {
        foreach (var modulo in _modulos)
        {
            if (modulo.Nome != item.ModuloNome) continue;
            var tela = modulo.CriarTela(item.Chave, _servicos);
            if (tela is null) continue;

            // A ViewModel que declara ICarregarAoAbrir é carregada aqui — o ponto único por
            // onde TODA tela da suíte passa. Sem isto, uma tela cuja carga dependia da
            // navegação do app de origem abre em branco, e branco se lê como defeito.
            if (tela is System.Windows.FrameworkElement { DataContext: ICarregarAoAbrir carregavel })
                _ = carregavel.CarregarAsync();

            return tela;
        }
        return null;
    }

    /// <summary>
    /// Monta um item COMPOSTO: uma aba por sub-tela, cada uma criada só quando aberta.
    ///
    /// As fábricas fecham sobre <see cref="MontarTela"/>, que resolve o módulo dono pela
    /// chave — é por aqui que um item junta telas de módulos diferentes sem que nenhum
    /// deles conheça o outro.
    /// </summary>
    private object? MontarComposta(ItemMenuModulo item, int abaInicial)
    {
        var disponiveis = AbasDisponiveis(item);
        if (disponiveis.Count == 0) return null;

        // UMA aba não é aba: é a tela. Acontece o tempo todo nos executáveis menores — a
        // "Agenda" da Recepção compõe a agenda do balcão com "Minha semana" do
        // Consultório, e no `Clinica.Recepcao.exe` sobra a primeira. Desenhar uma régua
        // com um rótulo só ocuparia 50px para não oferecer escolha nenhuma.
        if (disponiveis.Count == 1) return MontarTela(disponiveis[0].Item);

        var abas = disponiveis
            .Select(p => (p.Aba.Rotulo, (Func<object?>)(() => MontarTela(p.Item))))
            .ToList();

        return new TelaComAbas(abas, abaInicial);
    }

    // ===== Rail =====

    [RelayCommand]
    private void AbrirCategoria(GrupoMenuModulo? categoria)
    {
        // Clicar na categoria já fixada solta o painel — o mesmo botão fecha o que abriu.
        if (PainelFixado && ReferenceEquals(CategoriaAberta, categoria))
        {
            PainelFixado = false;
            CategoriaAberta = null;
            return;
        }

        CategoriaAberta = categoria;
        PainelFixado = true;
    }

    /// <summary>Espiar: o painel abre flutuando e some sozinho. Não fixa nada.</summary>
    public void EspiarCategoria(GrupoMenuModulo? categoria)
    {
        if (PainelFixado) return;
        CategoriaAberta = categoria;
    }

    [RelayCommand]
    private void FecharPainel()
    {
        CategoriaAberta = null;
        PainelFixado = false;
    }

    /// <summary>
    /// Ctrl+B: fixa ou solta o painel. Sem categoria escolhida, abre a da tela ativa —
    /// atalho que abre um painel vazio faria a pessoa concluir que ele não funciona.
    /// </summary>
    [RelayCommand]
    private void AlternarMenu()
    {
        if (PainelFixado)
        {
            PainelFixado = false;
            CategoriaAberta = null;
            return;
        }

        CategoriaAberta ??= Grupos.FirstOrDefault(g => g.ContemAtual) ?? Grupos.FirstOrDefault();
        if (CategoriaAberta is not null) PainelFixado = true;
    }

    partial void OnCategoriaAbertaChanged(GrupoMenuModulo? oldValue, GrupoMenuModulo? newValue)
    {
        foreach (var g in Grupos) g.EstaAberta = ReferenceEquals(g, newValue);
    }

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

        bool Casa(string? t) => t is not null && t.Contains(termo, StringComparison.OrdinalIgnoreCase);

        foreach (var item in Grupos.SelectMany(g => g.Itens))
        {
            if (Casa(item.Rotulo) || Casa(GruposSidebar.Rotulo(item.Grupo)))
            {
                ResultadosPesquisa.Add(new ResultadoPesquisa(item, 0, item.Rotulo, null));
                continue;
            }

            // ⚠️ A busca tem de enxergar DENTRO das abas. Ao consolidar 46 itens em 23
            // (parcela 55), "Fechamento de caixa" deixou de ser linha da sidebar e virou a
            // 2ª aba de "Caixa" — e uma consolidação que tira telas da busca troca um
            // problema de rolagem por um problema pior, o de não achar mais o que se
            // achava. O resultado diz o caminho ("Caixa › Fechamento"), senão a pessoa
            // clica e não entende por que caiu noutra tela.
            var abas = AbasDisponiveis(item);
            for (var i = 0; i < abas.Count; i++)
            {
                if (!Casa(abas[i].Aba.Rotulo)) continue;
                ResultadosPesquisa.Add(
                    new ResultadoPesquisa(item, i, abas[i].Aba.Rotulo, item.Rotulo));
            }
        }

        PesquisaAberta = ResultadosPesquisa.Count > 0;
    }

    [RelayCommand]
    private void NavegarResultado(ResultadoPesquisa? resultado)
    {
        resultado ??= ResultadosPesquisa.FirstOrDefault();
        if (resultado is null) return;

        FecharPesquisa();
        Navegar(resultado.Item, resultado.Aba);
    }

    [RelayCommand]
    private void FecharPesquisa()
    {
        PesquisaAberta = false;
        TextoPesquisa = string.Empty;
    }

    /// <summary>
    /// Trocar de usuário REABRE o app, em vez de só trocar a sessão em memória.
    ///
    /// É a mesma decisão do faturamento (parcela 45), pela mesma razão: as ViewModels leem
    /// a permissão quando são CONSTRUÍDAS — é o que acende ou apaga cada botão —, e metade
    /// delas já está viva quando alguém pede para trocar. Repontar a sessão deixaria a tela
    /// da colega anterior aberta com os botões dela, e permissão que parece aplicada e não
    /// está é pior do que permissão nenhuma, porque ninguém vai conferir.
    ///
    /// Sem esta porta, os quatro apps da suíte pediam login e não tinham saída: no balcão,
    /// onde duas pessoas dividem a máquina, a segunda seguia trabalhando com o login da
    /// primeira e a auditoria assinava o nome errado — que é exatamente o defeito que a
    /// parcela 45 existiu para corrigir, com `SessaoUsuario` no lugar de
    /// `Environment.UserName`.
    /// </summary>
    [RelayCommand]
    private void TrocarUsuario()
    {
        var dialogo = _servicos.GetService<Clinica.Desktop.Controls.IDialogoService>();
        if (dialogo is not null && !dialogo.Confirmar("Trocar usuário",
                "O sistema vai fechar e abrir de novo na tela de entrada. "
                + "Salve o que estiver editando antes de continuar.")) return;

        try
        {
            var executavel = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(executavel))
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(executavel) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            // Não conseguiu reabrir: NÃO fecha, e diz o que fazer. Derrubar o app sem ter
            // conseguido abrir o outro deixaria a pessoa sem sistema nenhum no balcão.
            Configuracao.LogSuite.Registrar("Trocar usuário — reabertura do app falhou", ex);
            _servicos.GetService<Clinica.Desktop.Controls.ISnackbarService>()?.Erro(
                "Não foi possível reabrir o sistema automaticamente. "
                + "Feche e abra de novo para entrar com outro usuário.");
            return;
        }

        System.Windows.Application.Current.Shutdown();
    }
}
