using Clinica.Desktop.Shell.Componentes.Cadastro;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Application.Servicos;
using Clinica.Application.Modelos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>
/// Pacientes na Recepção: LISTA de largura inteira, e a ficha do paciente atrás de um
/// clique.
///
/// O que mudou, e por quê (parcela 47)
/// -----------------------------------
/// Até aqui era mestre-detalhe numa tela só: uma coluna de 360 px com a lista, grudada à
/// esquerda para sempre, e a ficha à direita como uma PILHA de oito cartões com moldura.
/// As duas metades são o defeito que o `README.md` proíbe em letras maiúsculas — a faixa
/// lateral permanente ("metade da largura útil gasta com a mesma lista") e a colcha de
/// retalhos ("cinco molduras lado a lado leem-se como cinco telas costuradas"). A regra
/// existe porque o cliente reprovou isso em voz alta, e a saída prescrita é literalmente
/// esta: <b>tela de LISTA (largura inteira) → tela do ITEM atrás de um clique, com abas</b>.
///
/// O padrão não foi inventado aqui: é o mesmo <c>PacienteWorkspaceView</c> que o
/// Consultório usa desde a parcela 37 — cabeçalho da pessoa uma vez, e as seções em abas.
/// Ter dois desenhos diferentes para "ficha do paciente" no mesmo sistema é o que faz a
/// recepcionista achar que abriu outro programa.
///
/// A busca NÃO é reescrita aqui: usa o <see cref="SeletorPacienteViewModel"/> da suíte,
/// que já resolve limite no SQL, agrupamento das teclas e descarte de resposta fora de
/// ordem. A listagem usa páginas de 100 pacientes, com busca e filtros no banco.
/// </summary>
public sealed partial class PacientesViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public SeletorPacienteViewModel Seletor { get; }

    private readonly IFabricaFichaPaciente _fabrica;
    [ObservableProperty] private object? _fichaUnica;
    /// <summary>
    /// O recorte escolhido nos chips (out/2026, mockup aprovado "3 · Pacientes"). Vale
    /// só com o campo VAZIO: digitar é buscar no cadastro inteiro, e o chip "Resultado da
    /// busca" passa a ser o aceso — dois filtros invisíveis somados fariam a busca
    /// responder "ninguém" sobre quem existe fora do recorte.
    /// </summary>
    [ObservableProperty] private FiltroListaPacientes _recorte = FiltroListaPacientes.Todos;

    private async Task<IReadOnlyList<Paciente>> BuscarListaAsync(string? termo, int ignorar, int quantidade, CancellationToken ct)
    {
        using var scope = _escopos.CreateScope();
        var criterio = string.IsNullOrWhiteSpace(termo) ? Recorte : FiltroListaPacientes.Todos;
        if (criterio == FiltroListaPacientes.EmTratamento)
            SessaoUsuario.Atual.Exigir(Permissao.VerProntuario, "consultar pacientes em tratamento");
        return await scope.ServiceProvider.GetRequiredService<PacienteService>()
            .BuscarPaginaAsync(termo, ignorar, quantidade, criterio, ct);
    }

    /// <summary>As linhas da tabela: o paciente e o que a tabela diz dele.</summary>
    public System.Collections.ObjectModel.ObservableCollection<LinhaPacienteLista> Linhas { get; } = [];

    [ObservableProperty] private string _subtitulo = "Fichas cadastradas · a lista aparece quando você busca ou escolhe um recorte";
    [ObservableProperty] private string _rodape = string.Empty;
    [ObservableProperty] private string _chipBusca = "Resultado da busca";
    [ObservableProperty] private string _chipHoje = "Com horário hoje";
    [ObservableProperty] private string _chipIncompleto = "Cadastro incompleto";
    [ObservableProperty] private string _chipSemConvenio = "Sem convênio definido";
    [ObservableProperty] private string _chipAniversario = "Aniversário nesta semana";

    public bool TemTermo => !string.IsNullOrWhiteSpace(Seletor.Termo);
    public bool RecorteHoje => !TemTermo && Seletor.AlgoFoiPedido && Recorte == FiltroListaPacientes.ComHorarioHoje;
    public bool RecorteIncompleto => !TemTermo && Seletor.AlgoFoiPedido && Recorte == FiltroListaPacientes.CadastroIncompleto;
    public bool RecorteSemConvenio => !TemTermo && Seletor.AlgoFoiPedido && Recorte == FiltroListaPacientes.SemConvenioDefinido;
    public bool RecorteAniversario => !TemTermo && Seletor.AlgoFoiPedido && Recorte == FiltroListaPacientes.AniversarioNaSemana;

    /// <summary>"Importar" só aparece onde a importação existe (o Gerente) — botão que não abre nada é a parcela 41.</summary>
    public bool PodeImportar => NavegacaoSuite.Existe(ChavesSuite.ImportarPacientes);

    public bool PodeMarcar => SessaoUsuario.Atual.Pode(Permissao.EditarAgenda);

    private int _geracaoVisitas;

    public int SecaoInicial { get; set; } = 2;

    public bool MostrarVoltar { get; init; } = true;

    [ObservableProperty] private string _resumo = string.Empty;

    /// <summary>
    /// Estamos na ficha de alguém? É o que troca a LISTA pela tela do paciente.
    ///
    /// Mora no ViewModel e não numa navegação do shell porque as duas telas são a mesma
    /// seção da sidebar: "Pacientes" continua sendo um item só, e voltar da ficha não
    /// pode custar reencontrar o item no menu.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrandoLista))]
    private bool _mostrandoFicha;

    /// <summary>Estamos na lista? O par existe porque XAML não nega booleano sem conversor.</summary>
    public bool MostrandoLista => !MostrandoFicha;

    /// <summary>
    /// Habilita os botões de escrita da tela. É a metade VISÍVEL da permissão: o
    /// botão apagado explica por que não dá; a guarda no comando é que impede.
    /// Só desabilitar seria enfeite — um atalho de teclado passaria direto.
    ///
    /// O nome casa com o do XAML e com o bit (EditarPaciente = CADASTRO, parcela 49).
    /// Com o nome antigo ("PodeEditarProntuario"), o binding do "Novo paciente" não
    /// resolvia, e IsEnabled ficava no padrão true — barreira visível morta em silêncio.
    /// </summary>
    public bool PodeEditarCadastro => SessaoUsuario.Atual.Pode(Permissao.EditarPaciente);

    public PacientesViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo, IFabricaFichaPaciente fabrica)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _fabrica = fabrica;

        // Listagem sob demanda: todo o cadastro continua acessível por páginas.
        //
        // ⚠️ `SemBuscaInicial` (set/2026 — o cliente: "a tela de pacientes ainda carrega
        // todos os pacientes"). Esta era a consulta mais cara do sistema: `limite: null`
        // com o termo VAZIO traz o cadastro INTEIRO — 2.284 fichas — a cada abertura da
        // tela, num banco remoto.
        //
        // Eu tinha deixado esta tela de fora com o argumento de que "numa tela de LISTAGEM
        // a lista É a resposta". O argumento continua valendo para o que a tela OFERECE, e
        // não para o que ela faz sozinha: a listagem completa continua a um clique
        // ("Ver todos"), e é justamente por ser cara que ela precisa ser PEDIDA.
        Seletor = new SeletorPacienteViewModel(escopos)
        {
            SemBuscaInicial = true, TamanhoPagina = 100, ConsultaPagina = BuscarListaAsync
        };
        Seletor.SelecaoMudou += AoTrocarPaciente;
        Seletor.Atualizou += AtualizarResumo;
        Seletor.Atualizou += MontarLinhas;
        Seletor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SeletorPacienteViewModel.Termo) or nameof(SeletorPacienteViewModel.Modo))
                AvisarRecortes();
        };
        _ = CarregarRecortesAsync();


        AtualizarResumo();
    }

    /// <summary>
    /// Escolher alguém na lista ABRE a ficha. A ficha carrega antes de a tela trocar
    /// (não se espera o await): o cabeçalho já tem nome e foto vindos da linha clicada, e
    /// segurar a troca até o banco responder faria o clique parecer engolido num banco
    /// remoto de quinze segundos.
    /// </summary>
    private void AoTrocarPaciente(Paciente? paciente)
    {
        if (paciente is null) return;

        MostrandoFicha = true;
        var foco = new PacienteEmFoco();
        foco.Definir(paciente.Id, paciente.Nome);
        FichaUnica = _fabrica.Criar(foco, Voltar, SecaoInicial);
    }

    /// <summary>
    /// Volta para a lista. LIMPA a seleção de propósito: sem isso, clicar de novo no mesmo
    /// paciente não dispararia `SelecaoMudou` (o item já estava selecionado) e a ficha não
    /// reabriria — botão que funciona uma vez e depois não.
    /// </summary>
    [RelayCommand]
    private void Voltar()
    {
        MostrandoFicha = false;
        FichaUnica = null;
        Seletor.Selecionado = null;
        if (!Seletor.Ocioso) _ = Seletor.BuscarAsync(imediato: true);
    }

    /// <summary>
    /// ⚠️ O OCIOSO responde primeiro, e é a correção que anda junto de `SemBuscaInicial`:
    /// "Nenhum paciente encontrado" numa tela recém-aberta seria uma afirmação FALSA sobre
    /// uma clínica de 2.284 fichas — e é a leitura que leva a cadastrar de novo quem já
    /// tem ficha, partindo o histórico em dois (parcela 57).
    /// </summary>
    private void AtualizarResumo()
    {
        Resumo = Seletor.Ocioso
            ? "Busque por nome ou CPF — ou veja todos os pacientes."
            : Seletor.Resultados.Count switch
            {
                0 => "Nenhum paciente encontrado.",
                1 => "1 paciente nesta página.",
                var n => $"{n} pacientes nesta página."
            };

        OnPropertyChanged(nameof(ListandoTudo));
    }

    /// <summary>
    /// A lista completa está no ar. É o que apaga o botão "Ver todos" quando ele já foi
    /// clicado: botão que continua aceso depois de fazer o que faz manda clicar de novo.
    /// </summary>
    public bool ListandoTudo => Seletor.ListandoTodos;

    partial void OnRecorteChanged(FiltroListaPacientes value) => AvisarRecortes();

    private void AvisarRecortes()
    {
        OnPropertyChanged(nameof(TemTermo));
        OnPropertyChanged(nameof(RecorteHoje));
        OnPropertyChanged(nameof(RecorteIncompleto));
        OnPropertyChanged(nameof(RecorteSemConvenio));
        OnPropertyChanged(nameof(RecorteAniversario));
    }

    /// <summary>A tela abre já num recorte (o "Pacientes da clínica" do Consultório abre em tratamento).</summary>
    public void IniciarComRecorte(FiltroListaPacientes recorte) => EscolherRecorte(recorte.ToString());

    /// <summary>Clique num chip: esvazia a busca e lista o recorte (o parâmetro é o nome do valor do enum).</summary>
    [RelayCommand]
    private void EscolherRecorte(string? nome)
    {
        if (!Enum.TryParse<FiltroListaPacientes>(nome, out var recorte) || recorte.ToString() != nome) return;
        Recorte = recorte;
        Seletor.DesligarSugestaoCommand.Execute(null);
        AvisarRecortes();
    }

    /// <summary>Os números dos chips. Falha não derruba a lista: o chip fica sem número.</summary>
    private async Task CarregarRecortesAsync()
    {
        try
        {
            using var scope = _escopos.CreateScope();
            var r = await scope.ServiceProvider.GetRequiredService<PacienteService>()
                .RecortesAsync(DateOnly.FromDateTime(DateTime.Today));
            var pt = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
            Subtitulo = $"{r.Total.ToString("N0", pt)} {(r.Total == 1 ? "ficha cadastrada" : "fichas cadastradas")} · busque por nome ou CPF, ou escolha um recorte";
            ChipHoje = $"Com horário hoje · {r.ComHorarioHoje.ToString("N0", pt)}";
            ChipIncompleto = $"Cadastro incompleto · {r.CadastroIncompleto.ToString("N0", pt)}";
            ChipSemConvenio = $"Sem convênio definido · {r.SemConvenio.ToString("N0", pt)}";
            ChipAniversario = $"Aniversário nesta semana · {r.AniversarioNaSemana.ToString("N0", pt)}";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — contagem dos recortes de pacientes", ex);
        }
    }

    /// <summary>
    /// Remonta as linhas a cada busca concluída (sem await entre o Clear e o último Add) e
    /// depois pede, em LOTE, a última visita e o próximo horário da página.
    /// </summary>
    private void MontarLinhas()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        Linhas.Clear();
        foreach (var p in Seletor.Resultados) Linhas.Add(new LinhaPacienteLista(p, hoje));
        ChipBusca = TemTermo ? $"Resultado da busca · {Linhas.Count}" : "Resultado da busca";
        var recorte = Recorte switch
        {
            FiltroListaPacientes.ComHorarioHoje => "com horário hoje",
            FiltroListaPacientes.CadastroIncompleto => "cadastro incompleto",
            FiltroListaPacientes.SemConvenioDefinido => "sem convênio definido",
            FiltroListaPacientes.AniversarioNaSemana => "aniversário nesta semana",
            FiltroListaPacientes.EmTratamento => "em tratamento",
            _ => "todos, em ordem de nome"
        };
        Rodape = ListaDePacientes.Rodape(Linhas.Count, Seletor.Termo, recorte);
        _ = CarregarVisitasAsync(Linhas.ToList(), ++_geracaoVisitas);
    }

    private async Task CarregarVisitasAsync(IReadOnlyList<LinhaPacienteLista> linhas, int geracao)
    {
        if (linhas.Count == 0) return;
        try
        {
            using var scope = _escopos.CreateScope();
            var visitas = await scope.ServiceProvider.GetRequiredService<PacienteService>()
                .VisitasAsync(linhas.Select(l => l.Paciente.Id).ToList(), DateTime.Now);
            if (geracao != _geracaoVisitas) return;
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            foreach (var l in linhas)
            {
                visitas.TryGetValue(l.Paciente.Id, out var v);
                l.UltimaVisita = ListaDePacientes.UltimaVisita(v, hoje);
                l.ProximoHorario = ListaDePacientes.ProximoHorario(v, hoje);
            }
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoVisitas) return;
            Clinica.Application.Diagnostico.Registrar("Recepção — visitas dos pacientes da lista", ex);
            foreach (var l in linhas) { l.UltimaVisita = "não foi possível conferir"; l.ProximoHorario = "—"; }
        }
    }

    [RelayCommand]
    private void AbrirFicha(LinhaPacienteLista? linha)
    {
        if (linha is not null) AoTrocarPaciente(linha.Paciente);
    }

    /// <summary>Enter na busca: abre a ficha quando sobra um só (o rodapé promete isso).</summary>
    public void AbrirSeUnico()
    {
        if (Linhas.Count == 1) AoTrocarPaciente(Linhas[0].Paciente);
    }

    [RelayCommand]
    private void Marcar(LinhaPacienteLista? linha)
    {
        if (linha is null) return;
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "marcar atendimento");
            using var scope = _escopos.CreateScope();
            scope.ServiceProvider.GetRequiredService<PreenchimentoNovoAtendimento>()
                .Definir(new PedidoNovoAtendimento(MarcarParaDepois: true, DataHora: DateTime.Today,
                    ProfissionalId: null, SalaId: null, PacienteId: linha.Paciente.Id));
            if (!NavegacaoSuite.Ir(Modulo.ModuloRecepcao.ChaveMarcarHorario))
                _snackbar.Info("A tela de marcar atendimento não está disponível para o seu acesso.");
        }
        catch (Exception ex)
        {
            _snackbar.Erro(ex.Message);
        }
    }

    [RelayCommand]
    private void Importar()
    {
        if (!NavegacaoSuite.Ir(ChavesSuite.ImportarPacientes))
            _snackbar.Info("A importação de pacientes fica no aplicativo do Gerente.");
    }

    [RelayCommand]
    private void NovoPaciente()
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarPaciente, "cadastrar paciente");

        var vm = new CadastroPacienteViewModel(_escopos);
        var janela = new CadastroPacienteWindow(vm)
        {
            Owner = JanelaDona.Atual()
        };

        if (janela.ShowDialog() != true) return;

        _snackbar.Sucesso("Paciente cadastrado.");

        // ⚠️ Busca pelo NOME de quem acabou de ser cadastrado, e não "recarrega a lista".
        //
        // Com a tela OCIOSA (set/2026) recarregar não faria nada — `BuscarAsync` sai cedo
        // sem consultar —, e a pessoa veria "Paciente cadastrado." com a tela em branco,
        // que se lê como "não salvou". Buscar pelo nome sai do ocioso E mostra exatamente
        // a ficha nova, em vez de trazer as 2.284 para ela se perder no meio.
        Seletor.Termo = vm.Nome;
        _ = CarregarRecortesAsync();
    }
}

/// <summary>Uma linha da tabela de Pacientes (out/2026, mockup aprovado "3 · Pacientes").</summary>
public sealed partial class LinhaPacienteLista : ObservableObject
{
    public LinhaPacienteLista(Paciente paciente, DateOnly hoje)
    {
        Paciente = paciente;
        Identificacao = ListaDePacientes.Identificacao(paciente, hoje);
        Telefone = string.IsNullOrWhiteSpace(paciente.Telefone) ? "—" : paciente.TelefoneFormatado;
        Convenio = paciente.ConvenioADefinir ? "sem convênio" : paciente.ConvenioNome;
    }

    public Paciente Paciente { get; }
    public string Nome => Paciente.Nome;
    public byte[]? Foto => Paciente.FotoMiniatura;
    public string Identificacao { get; }
    public string Telefone { get; }
    public string Convenio { get; }
    public string ConvenioCompleto => Paciente.ConvenioNome;
    public bool ConvenioADefinir => Paciente.ConvenioADefinir;

    /// <summary>"…" enquanto a leitura em lote não volta — nunca "—", que afirmaria "não há".</summary>
    [ObservableProperty] private string _ultimaVisita = "…";
    [ObservableProperty] private string _proximoHorario = "…";
}
