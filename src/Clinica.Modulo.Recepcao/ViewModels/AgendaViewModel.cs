using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>Um horário na coluna de um profissional.</summary>
public sealed class CartaoAgenda
{
    public required int AgendamentoId { get; init; }

    /// <summary>De quem é a coluna — é o que a lista de espera pergunta ao vagar.</summary>
    public required int? ProfissionalId { get; init; }

    /// <summary>Marcado em série (pacote de dez). Null = horário avulso.</summary>
    public required string? SerieId { get; init; }

    public bool EhSerie => !string.IsNullOrWhiteSpace(SerieId);

    public required string Faixa { get; init; }
    public required string Paciente { get; init; }
    public string? Telefone { get; init; }
    public required DateTime DataHora { get; init; }
    public required string Modalidade { get; init; }
    public required string Sala { get; init; }
    public required string StatusRotulo { get; init; }
    public required bool EhEncaixe { get; init; }
    public required bool EhRetornoDoSegundoCodigo { get; init; }
    public required bool VeioDaListaEspera { get; init; }

    /// <summary>Ainda dá para remarcar/cancelar (não virou atendimento).</summary>
    public required bool EmAberto { get; init; }

    public bool TemTelefone => !string.IsNullOrWhiteSpace(Telefone);
}

/// <summary>Uma coluna da grade — um profissional (ou o resíduo "sem profissional").</summary>
public sealed class ColunaAgenda
{
    public required int? ProfissionalId { get; init; }
    public required string Nome { get; init; }
    public required string Resumo { get; init; }
    public required ObservableCollection<CartaoAgenda> Horarios { get; init; }
    public bool Vazia => Horarios.Count == 0;
}

/// <summary>Um pedido na lista de espera.</summary>
public sealed class LinhaListaEspera
{
    public required int PedidoId { get; init; }
    public required string Paciente { get; init; }
    public required string Preferencias { get; init; }
    public required string Desde { get; init; }
    public required bool Prioritario { get; init; }
    public string? Observacoes { get; init; }
}

/// <summary>
/// Agenda multiprofissional: uma coluna por profissional, o dia inteiro à vista.
///
/// É a resposta ao bloqueio de fundação — antes o agendamento não sabia com quem era,
/// então a agenda só podia ser uma lista única e ninguém conseguia ver dois
/// consultórios ao mesmo tempo. Ao lado dela fica a LISTA DE ESPERA: quando um horário
/// vaga, a recepção vê na mesma tela quem chamar, em vez de tentar lembrar.
/// </summary>
public sealed partial class AgendaViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<ColunaAgenda> Colunas { get; } = [];
    public ObservableCollection<LinhaListaEspera> Espera { get; } = [];

    [ObservableProperty] private DateTime _dia = DateTime.Today;

    /// <summary>
    /// Semana em vez de dia. As colunas deixam de ser os profissionais e passam a ser os
    /// DIAS: é a forma de responder "quando ele tem vaga?", que no modo dia se responde
    /// clicando de dia em dia até achar.
    ///
    /// Os cartões e os botões são os mesmos — muda só o que cada coluna agrupa.
    /// </summary>
    [ObservableProperty] private bool _modoSemana;

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private string _resumo = string.Empty;

    /// <summary>Feedback inline: erro de agenda fica na tela enquanto o usuário resolve.</summary>
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>
    /// Nenhum profissional cadastrado ainda: a grade não tem colunas e a tela precisa
    /// dizer o que fazer, em vez de aparecer vazia sem explicação.
    /// </summary>
    [ObservableProperty] private bool _semProfissionais;

    /// <summary>
    /// Habilita os botões de escrita da tela. É a metade VISÍVEL da permissão: o
    /// botão apagado explica por que não dá; a guarda no comando é que impede.
    /// Só desabilitar seria enfeite — um atalho de teclado passaria direto.
    /// </summary>
    public bool PodeEditarAgenda => SessaoUsuario.Atual.Pode(Permissao.EditarAgenda);

    /// <summary>
    /// Quem entrou é um profissional COM agenda — então dá para oferecer "só a minha".
    /// Falso no balcão, onde o usuário não está vinculado a ninguém.
    /// </summary>
    [ObservableProperty]
    private bool _soMinhaAgenda;

    /// <summary>
    /// Escapatória do filtro acima: o profissional que precisa ver a clínica inteira
    /// (para encaixar alguém na agenda do colega) marca isto e volta a ver tudo.
    /// </summary>
    [ObservableProperty]
    private bool _mostrarTodosOsProfissionais;

    /// <summary>
    /// Horário que acabou de vagar. Enquanto ele existe, a lista de espera mostra só
    /// quem serve para ele — é a pergunta do minuto seguinte a um cancelamento.
    /// </summary>
    [ObservableProperty] private DateTime? _sugestaoPara;

    [ObservableProperty] private int? _sugestaoProfissionalId;

    /// <summary>O que a lista está mostrando, escrito — filtro invisível é filtro que engana.</summary>
    [ObservableProperty] private string _tituloEspera = "Lista de espera";

    /// <summary>
    /// O que dizer quando a lista sai vazia. "Ninguém espera" e "ninguém serve para este
    /// horário" são respostas diferentes: trocá-las faria a recepção deixar de oferecer
    /// a lista a quem liga.
    /// </summary>
    [ObservableProperty] private string _esperaVazia = "Ninguém na lista de espera.";

    /// <summary>Há um horário em foco: a tela oferece voltar à lista inteira.</summary>
    public bool TemSugestao => SugestaoPara is not null;

    partial void OnSugestaoParaChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(TemSugestao));
        TituloEspera = value is { } horario
            ? $"Quem chamar para {horario:dd/MM HH:mm}"
            : "Lista de espera";
    }

    public AgendaViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _ = CarregarAsync();
    }

    partial void OnMostrarTodosOsProfissionaisChanged(bool value) => _ = CarregarAsync();

    partial void OnModoSemanaChanged(bool value)
    {
        // Sair do foco do horário: "quem chamar para as 14h" é pergunta do modo dia.
        SugestaoPara = null;
        SugestaoProfissionalId = null;
        _ = CarregarAsync();
    }

    partial void OnDiaChanged(DateTime value)
    {
        // Trocar de dia desfaz o foco: "quem chamar para as 14h de ontem" não é pergunta.
        SugestaoPara = null;
        SugestaoProfissionalId = null;
        _ = CarregarAsync();
    }

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Carregando = true;
            Mensagem = string.Empty;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
            var equipe = scope.ServiceProvider.GetRequiredService<EquipeService>();
            var espera = scope.ServiceProvider.GetRequiredService<ListaEsperaService>();

            var dia = DateOnly.FromDateTime(Dia);
            var doDia = await agenda.DoDiaAsync(dia);
            var profissionais = await equipe.ProfissionaisAtivosAsync();
            SemProfissionais = profissionais.Count == 0;

            Colunas.Clear();

            if (ModoSemana)
            {
                await MontarSemanaAsync(agenda, profissionais);
                await CarregarEsperaAsync(espera);
                return;
            }

            // Quem entrou como PROFISSIONAL abre na própria coluna. O usuário aponta
            // para o Profissional desde a parcela 5, e um fisioterapeuta que abre o app
            // quer ver a agenda DELE — não caçá-la entre seis colunas. O balcão (sem
            // profissional vinculado) continua vendo a clínica inteira.
            var meu = SessaoUsuario.Atual.ProfissionalId;
            SoMinhaAgenda = meu is not null && profissionais.Any(p => p.Id == meu);

            var visiveis = SoMinhaAgenda && !MostrarTodosOsProfissionais
                ? profissionais.Where(p => p.Id == meu).ToList()
                : profissionais;

            foreach (var p in visiveis)
                Colunas.Add(MontarColuna(p.Id, p.Rotulo, doDia.Where(a => a.ProfissionalId == p.Id)));

            // "Sem profissional" só aparece quando existe: é resíduo da agenda antiga
            // (e do faturamento, que marca sem informar quem atende), não uma pessoa.
            // Filtrando por "minha agenda", ele não é meu — fica de fora.
            var orfaos = doDia.Where(a => a.ProfissionalId is null).ToList();
            if (orfaos.Count > 0 && visiveis.Count == profissionais.Count)
                Colunas.Add(MontarColuna(null, "Sem profissional", orfaos));

            var ocupando = visiveis.Count == profissionais.Count
                ? doDia.Count(a => a.OcupaAgenda)
                : doDia.Count(a => a.OcupaAgenda && a.ProfissionalId == meu);
            Resumo = $"{ocupando} horário(s) no dia · {Colunas.Count} coluna(s)";

            await CarregarEsperaAsync(espera);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — agenda do dia não pôde ser carregada", ex);
            Mensagem = $"Não foi possível carregar a agenda: {ex.Message}";
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    /// <summary>
    /// A semana da data escolhida, uma coluna por dia (segunda a domingo).
    ///
    /// Começa na SEGUNDA e não no dia escolhido: a clínica pensa a semana em bloco, e
    /// uma grade que começasse na quarta faria o mesmo dia aparecer em posições
    /// diferentes conforme o dia em que se abre a tela.
    ///
    /// O filtro "só a minha agenda" continua valendo — quem entrou como profissional vê
    /// a semana DELE, que é a pergunta que ele faz.
    /// </summary>
    private async Task MontarSemanaAsync(
        AgendaService agenda, IReadOnlyList<Profissional> profissionais)
    {
        var meu = SessaoUsuario.Atual.ProfissionalId;
        SoMinhaAgenda = meu is not null && profissionais.Any(p => p.Id == meu);
        var soMeu = SoMinhaAgenda && !MostrarTodosOsProfissionais;

        // O casting vem ANTES da conta: `DayOfWeek + 6` continua sendo DayOfWeek (soma de
        // enum com int devolve o enum), e o resto de divisão não existe para enum.
        var segunda = Dia.Date.AddDays(-(((int)Dia.DayOfWeek + 6) % 7));
        var ocupando = 0;

        for (var i = 0; i < 7; i++)
        {
            var quando = segunda.AddDays(i);
            var doDia = await agenda.DoDiaAsync(DateOnly.FromDateTime(quando));

            var recorte = soMeu
                ? doDia.Where(a => a.ProfissionalId == meu).ToList()
                : doDia.ToList();

            ocupando += recorte.Count(a => a.OcupaAgenda);

            // A coluna do dia não tem "um profissional": o cartão já diz de quem é, e
            // amarrá-la a alguém faria o botão de chamar da lista de espera oferecer o
            // profissional errado.
            Colunas.Add(MontarColuna(
                null,
                $"{Dias[i]} {quando:dd/MM}",
                recorte));
        }

        Resumo = $"{ocupando} horário(s) na semana de {segunda:dd/MM} a {segunda.AddDays(6):dd/MM}";
    }

    private static readonly string[] Dias = ["seg", "ter", "qua", "qui", "sex", "sáb", "dom"];

    private static ColunaAgenda MontarColuna(
        int? profissionalId, string nome, IEnumerable<Agendamento> agendamentos)
    {
        var cartoes = new ObservableCollection<CartaoAgenda>();
        var ocupando = 0;

        foreach (var a in agendamentos.OrderBy(a => a.DataHora))
        {
            if (a.OcupaAgenda) ocupando++;
            cartoes.Add(new CartaoAgenda
            {
                AgendamentoId = a.Id,
                ProfissionalId = profissionalId,
                SerieId = a.SerieId,
                Faixa = $"{a.DataHora:HH:mm}–{a.FimPrevisto:HH:mm}",
                Paciente = a.Paciente?.Nome ?? "(paciente removido)",
                Telefone = a.Paciente?.Telefone,
                DataHora = a.DataHora,
                Modalidade = a.ModalidadePrevista.ToString(),
                Sala = a.Sala?.Nome ?? "—",
                StatusRotulo = Rotular(a.Status),
                EhEncaixe = a.Encaixe,
                EhRetornoDoSegundoCodigo = a.Origem == OrigemAgendamento.RetornoSugerido,
                VeioDaListaEspera = a.Origem == OrigemAgendamento.ListaEspera,
                EmAberto = a.Status == StatusAgendamento.Agendado
            });
        }

        return new ColunaAgenda
        {
            ProfissionalId = profissionalId,
            Nome = nome,
            Resumo = $"{ocupando} horário(s)",
            Horarios = cartoes
        };
    }

    /// <summary>
    /// A lista de espera do painel lateral. Quando um horário acabou de vagar, ela deixa
    /// de ser "todo mundo que espera" e passa a ser **quem serve para AQUELE horário** —
    /// respeitando a janela de datas, o turno e o profissional que cada pedido escolheu.
    ///
    /// <c>CandidatosParaAsync</c> existia desde a parcela 1 e nenhuma tela o chamava: a
    /// recepção via a lista inteira e tinha de cruzar preferência por preferência de
    /// cabeça, justamente no minuto em que o telefone precisa ser atendido.
    /// </summary>
    private async Task CarregarEsperaAsync(ListaEsperaService espera)
    {
        var pedidos = SugestaoPara is { } horario
            ? await espera.CandidatosParaAsync(horario, SugestaoProfissionalId)
            : await espera.AguardandoAsync();

        EsperaVazia = SugestaoPara is { } alvo
            ? $"Ninguém da lista serve para {alvo:dd/MM HH:mm} — turno, janela de datas ou "
              + "profissional pedido não batem."
            : "Ninguém na lista de espera.";

        Espera.Clear();
        foreach (var p in pedidos)
            Espera.Add(new LinhaListaEspera
            {
                PedidoId = p.Id,
                Paciente = p.Paciente?.Nome ?? "(paciente removido)",
                Preferencias = DescreverPreferencias(p),
                Desde = $"na lista desde {p.CriadoEm:dd/MM}",
                Prioritario = p.Prioritario,
                Observacoes = p.Observacoes
            });
    }

    private static string DescreverPreferencias(ListaEspera p)
    {
        var partes = new List<string>
        {
            p.Profissional?.Rotulo ?? "qualquer profissional",
            p.Periodo switch
            {
                PeriodoPreferido.Manha => "manhã",
                PeriodoPreferido.Tarde => "tarde",
                _ => "qualquer turno"
            }
        };

        if (p.DisponivelDe is not null || p.DisponivelAte is not null)
            partes.Add($"{p.DisponivelDe?.ToString("dd/MM") ?? "hoje"}"
                       + $" a {p.DisponivelAte?.ToString("dd/MM") ?? "sem limite"}");

        return string.Join(" · ", partes);
    }

    // ==================== Comandos ====================

    /// <summary>Abre o formulário de um horário novo.</summary>
    [RelayCommand]
    private async Task NovoHorarioAsync()
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na agenda");

        await AbrirFormularioAsync(new AgendamentoEdicaoViewModel(_escopos)
        {
            Data = Dia
        });
    }

    /// <summary>Remarca: move o horário preservando o registro (e o histórico).</summary>
    [RelayCommand]
    private async Task RemarcarAsync(CartaoAgenda? cartao)
    {
        if (cartao is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na agenda");
        await AbrirFormularioAsync(new AgendamentoEdicaoViewModel(_escopos, cartao.AgendamentoId));
    }

    /// <summary>Chama alguém da lista de espera para um horário que vagou.</summary>
    [RelayCommand]
    private async Task ChamarDaEsperaAsync(LinhaListaEspera? linha)
    {
        if (linha is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na agenda");

        var vm = new AgendamentoEdicaoViewModel(_escopos)
        {
            Data = SugestaoPara?.Date ?? Dia,
            Titulo = $"Chamar {linha.Paciente} da lista de espera",
            PedidoListaEsperaId = linha.PedidoId
        };

        // Quando a lista está apontada para um horário, o formulário já abre nele: é o
        // horário que a recepção acabou de ver vagar, e redigitá-lo é o passo em que a
        // hora sai errada.
        if (SugestaoPara is { } horario) vm.Hora = horario.ToString("HH:mm");

        await AbrirFormularioAsync(vm);
    }

    /// <summary>
    /// Aponta a lista de espera para um horário: quem serve para ELE, na ordem de chamada.
    ///
    /// O primeiro da lista é o primeiro a ligar. Quem não escolheu profissional serve
    /// para qualquer coluna; quem escolheu só aparece na dele.
    /// </summary>
    [RelayCommand]
    private async Task QuemChamarAsync(CartaoAgenda? cartao)
    {
        if (cartao is null) return;

        SugestaoPara = cartao.DataHora;
        SugestaoProfissionalId = cartao.ProfissionalId;

        await CarregarAsync();

        // Lista vazia aqui é resposta, não falha: ninguém que espera serve para este
        // horário, e a recepção precisa saber disso antes de começar a ligar.
        if (Espera.Count == 0)
        {
            Mensagem = $"Ninguém da lista de espera serve para {cartao.DataHora:dd/MM HH:mm} "
                       + "(turno, janela de datas ou profissional pedido).";
            MensagemEhErro = false;
        }
    }

    /// <summary>Tira o foco do horário e volta a mostrar todo mundo que espera.</summary>
    [RelayCommand]
    private async Task VerListaInteiraAsync()
    {
        SugestaoPara = null;
        SugestaoProfissionalId = null;
        await CarregarAsync();
    }

    /// <summary>
    /// A rodada de confirmação das sessões de amanhã.
    ///
    /// A capacidade existia desde a parcela 5 e morava só no Gerente Geral — mas quem
    /// confirma as sessões de amanhã é quem está no balcão. É a mesma rodada, o mesmo
    /// serviço e a mesma chave de idempotência: rodar aqui e lá no mesmo dia não manda
    /// duas mensagens para o mesmo paciente.
    /// </summary>
    [RelayCommand]
    private async Task ConfirmarSessoesAsync()
    {
        var vm = new ConfirmacoesViewModel(_escopos);
        var janela = new Janelas.ConfirmacoesWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        janela.ShowDialog();

        // A confirmação não muda a agenda do dia aberto, mas pode ter registrado
        // resposta de quem vem amanhã — recarregar mantém a tela honesta.
        await CarregarAsync();
    }

    /// <summary>
    /// Cancela o que ainda está marcado da série — o "o paciente desistiu no meio do
    /// tratamento". Sessão já atendida não é tocada: ela é fato, e apagá-la da agenda
    /// esconderia um atendimento que aconteceu.
    /// </summary>
    [RelayCommand]
    private async Task CancelarSerieAsync(CartaoAgenda? cartao)
    {
        if (cartao?.SerieId is not { } serieId) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na agenda");

        if (!_dialogo.ConfirmarPerigo("Cancelar a série",
                $"Cancelar TODAS as sessões ainda marcadas da série de {cartao.Paciente}? "
                + "As que já foram atendidas continuam no histórico.")) return;

        await ExecutarAsync(async scope =>
        {
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
            var quantas = await agenda.CancelarSerieAsync(serieId, SessaoUsuario.Atual.Operador);
            _snackbar.Info($"{quantas} sessão(ões) da série canceladas.");
        }, "cancelamento da série");
    }

    /// <summary>Coloca um paciente na lista de espera.</summary>
    [RelayCommand]
    private async Task NovoPedidoEsperaAsync()
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na agenda");

        var vm = new ListaEsperaEdicaoViewModel(_escopos);
        var janela = new Janelas.ListaEsperaWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (janela.ShowDialog() != true) return;
        _snackbar.Sucesso("Paciente entrou na lista de espera.");
        await CarregarAsync();
    }

    /// <summary>Tira o pedido da lista sem agendar (desistiu, resolveu em outro lugar).</summary>
    [RelayCommand]
    private async Task RemoverDaEsperaAsync(LinhaListaEspera? linha)
    {
        if (linha is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na agenda");

        var motivo = _dialogo.PerguntarTexto(
            "Sair da lista de espera",
            $"Por que {linha.Paciente} está saindo da lista? Fica registrado no pedido.");
        if (string.IsNullOrWhiteSpace(motivo)) return;

        await ExecutarAsync(async scope =>
        {
            var espera = scope.ServiceProvider.GetRequiredService<ListaEsperaService>();
            await espera.RemoverAsync(linha.PedidoId, motivo);
            _snackbar.Info($"{linha.Paciente} saiu da lista de espera.");
        }, "saída da lista de espera");
    }

    [RelayCommand]
    private async Task CancelarAsync(CartaoAgenda? cartao)
    {
        if (cartao is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na agenda");
        if (!_dialogo.Confirmar("Cancelar horário",
                $"Cancelar o horário de {cartao.Paciente} ({cartao.Faixa})?")) return;

        await ExecutarAsync(async scope =>
        {
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
            await agenda.CancelarAsync(cartao.AgendamentoId, SessaoUsuario.Atual.Operador);

            // O horário acabou de vagar: a pergunta seguinte é sempre "quem eu chamo?",
            // e a lista já responde antes de alguém precisar perguntar.
            ApontarEsperaPara(cartao);
            _snackbar.Info("Horário cancelado.");
        }, "cancelamento do horário");
    }

    [RelayCommand]
    private async Task MarcarFaltaAsync(CartaoAgenda? cartao)
    {
        if (cartao is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na agenda");

        await ExecutarAsync(async scope =>
        {
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
            await agenda.MarcarFaltaAsync(cartao.AgendamentoId, SessaoUsuario.Atual.Operador);

            ApontarEsperaPara(cartao);
            _snackbar.Info($"{cartao.Paciente} marcado como falta.");
        }, "marcação de falta");
    }

    /// <summary>
    /// Aponta a lista de espera para o horário que este cartão ocupava.
    ///
    /// Só vale para horário de HOJE em diante: sugerir quem chamar para uma falta de
    /// terça-feira passada seria oferecer um horário que já não existe.
    /// </summary>
    private void ApontarEsperaPara(CartaoAgenda cartao)
    {
        if (cartao.DataHora < DateTime.Now) return;

        SugestaoPara = cartao.DataHora;
        SugestaoProfissionalId = cartao.ProfissionalId;
    }

    /// <summary>
    /// Confirma a sessão pelo WhatsApp. Falta é sessão não faturada — confirmar na
    /// véspera é a rotina que mais evita buraco na agenda.
    /// </summary>
    [RelayCommand]
    private void Confirmar(CartaoAgenda? cartao)
    {
        if (cartao is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na agenda");

        var erro = Whatsapp.Abrir(
            cartao.Telefone, cartao.Paciente,
            Whatsapp.ConfirmacaoDeSessao(cartao.Paciente, cartao.DataHora));

        if (erro is null) return;
        Mensagem = erro;
        MensagemEhErro = true;
    }

    private async Task AbrirFormularioAsync(AgendamentoEdicaoViewModel vm)
    {
        var janela = new Janelas.AgendamentoWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (janela.ShowDialog() != true) return;

        // Marcou: o horário deixou de estar vago, e a lista volta a ser a lista inteira.
        SugestaoPara = null;
        SugestaoProfissionalId = null;

        _snackbar.Sucesso("Agenda atualizada.");
        await CarregarAsync();
    }

    private async Task ExecutarAsync(Func<IServiceScope, Task> acao, string contexto)
    {
        try
        {
            using var scope = _escopos.CreateScope();
            await acao(scope);
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar($"Recepção — falha em: {contexto}", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    // Em modo semana os botões andam de SETE em sete: avançar um dia numa grade que
    // mostra a semana inteira não mudaria quase nada na tela.
    [RelayCommand]
    private void DiaAnterior() => Dia = Dia.AddDays(ModoSemana ? -7 : -1);

    [RelayCommand]
    private void ProximoDia() => Dia = Dia.AddDays(ModoSemana ? 7 : 1);

    [RelayCommand]
    private void Hoje() => Dia = DateTime.Today;

    private static string Rotular(StatusAgendamento status) => status switch
    {
        StatusAgendamento.Agendado => "Marcado",
        StatusAgendamento.Realizado => "Atendido",
        StatusAgendamento.Cancelado => "Cancelado",
        StatusAgendamento.Faltou => "Faltou",
        _ => status.ToString()
    };
}
