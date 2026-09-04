using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>
/// Uma linha do dia do profissional.
///
/// <c>required</c> em tudo o que a tela mostra é proposital (é a convenção das linhas de
/// lista do projeto): quando a linha ganhar um campo, o compilador cobra a fábrica em vez
/// de a tela mostrar vazio.
/// </summary>
public sealed class LinhaSessao
{
    public required int AgendamentoId { get; init; }

    /// <summary>Dia do horário — viaja para o posto porque o casamento sessão × evolução cai para paciente + data.</summary>
    public required DateOnly Data { get; init; }
    public required int PacienteId { get; init; }
    public required string Paciente { get; init; }
    public required string Hora { get; init; }
    public required string Modalidade { get; init; }
    public required string Local { get; init; }
    public required string Situacao { get; init; }
    public required bool EvolucaoEscrita { get; init; }
    public required bool RegistroPendente { get; init; }
    public required bool Encaixe { get; init; }
    public int? AtendimentoId { get; init; }

    /// <summary>Espera do paciente, já formatada. Vazio antes do check-in.</summary>
    public required string Espera { get; init; }

    /// <summary>Chamado e ainda não entrou — a linha fica em destaque na lista.</summary>
    public required string ChamadoHa { get; init; }

    public required EtapaFila Etapa { get; init; }

    /// <summary>
    /// Cancelado ou falta. A linha CONTINUA na lista, apagada — quem lê às 14h
    /// precisa saber que o horário das 15h vagou, e linha ausente se confunde com horário
    /// que nunca existiu (a regra da folha do dia).
    ///
    /// ⚠️ Mas ele precisa ficar MARCADO, e não ficava. A situação era calculada
    /// (<c>Situacao</c>, via <c>Rotular</c>) e o XAML nunca a leu — dado
    /// calculado sem leitor, na variante em que o estrago é ler ERRADO em vez de não ler:
    /// o médico contava cinco pessoas por vir e duas tinham desmarcado. A tela irmã do
    /// mesmo módulo, "Minha semana", já marcava com "Não aconteceu"; eram duas respostas
    /// para a mesma pergunta sobre o mesmo horário.
    /// </summary>
    public required bool ForaDaFila { get; init; }

    /// <summary>
    /// O recado que a recepção escreveu no horário. Vazio quando não há. O campo existia
    /// desde a parcela 1 e nunca chegava a quem atende — o médico trabalhava com uma foto
    /// parcial do que o balcão sabia.
    /// </summary>
    public required string Observacoes { get; init; }

    /// <summary>O que o botão de registro diz — "Escrever" e "Abrir" não são a mesma ação.</summary>
    public string RotuloRegistro => EvolucaoEscrita ? "Ver registro" : "Atender";

    /// <summary>
    /// A fila corre HOJE. Num dia passado ou futuro os botões de movimento somem: chamar
    /// alguém de ontem carimbaria a chamada num horário morto e a tela diria "a recepção
    /// já está vendo o aviso" sobre uma fila que só relê o dia corrente — afirmação falsa
    /// com cara de confirmação.
    /// </summary>
    public bool EhHoje => Data == DateOnly.FromDateTime(DateTime.Today);

    /// <summary>
    /// Só se chama quem já CHEGOU: chamar quem não fez check-in faria a recepção anunciar
    /// um nome para uma sala de espera onde a pessoa não está.
    /// </summary>
    public bool PodeChamar => Etapa == EtapaFila.Chegou && EhHoje;

    /// <summary>Já foi chamado — o consultório pode desistir, e o balcão para de anunciar.</summary>
    public bool PodeDesfazerChamada => Etapa == EtapaFila.Chamado && EhHoje;

    /// <summary>
    /// O paciente pode ENTRAR: já fez check-in (chamado ou não — quem entra sem ter sido
    /// chamado teve a chamada carimbada junto, ver <c>AgendaService.IniciarAtendimentoAsync</c>).
    /// </summary>
    public bool PodeEntrar => (Etapa is EtapaFila.Chegou or EtapaFila.Chamado) && EhHoje;

    /// <summary>
    /// Desfazer o "entrou" clicado por engano. Só de quem está EM ATENDIMENTO: a chamada tem
    /// o próprio desfazer, e o check-in é ato do balcão — não se desfaz daqui.
    /// </summary>
    public bool PodeVoltarEtapa => Etapa == EtapaFila.EmAtendimento && EhHoje;

    /// <summary>A chamada está pendente há tempo demais — vale insistir com o balcão.</summary>
    public bool ChamadaDemorada { get; init; }

    public DateTime? ChegadaEm { get; init; }
    public DateTime? InicioEm { get; init; }
    public DateTime? FimEm { get; init; }

    /// <summary>
    /// A coluna STATUS da lista (parcela 95): a etapa da fila em uma palavra. Para o
    /// cancelado e a falta é a situação — a linha fica, MARCADA, pela regra da folha do
    /// dia (quem lê às 14h precisa saber que as 15h vagaram).
    /// </summary>
    public string Status => ForaDaFila ? Situacao : Etapa switch
    {
        EtapaFila.Chegou => "No local",
        EtapaFila.Chamado => "Chamado",
        EtapaFila.EmAtendimento => "Em atendimento",
        EtapaFila.Finalizado => "Conclu\u00EDdo",
        _ => "Marcado"
    };

    /// <summary>
    /// A hora do fato abaixo do status — "chegou às 14:40 · espera 12 min", "chamado há 4
    /// min", "desde 14:52", "às 15:20". É o que faz a linha responder "quem eu posso
    /// chamar agora" sem precisar de cinco colunas. Vazio quando não há fato.
    /// </summary>
    public string StatusDetalhe => ForaDaFila ? string.Empty : Etapa switch
    {
        EtapaFila.Chegou => string.Join(" \u00B7 ", new[]
        {
            ChegadaEm is { } c ? $"chegou \u00E0s {c:HH\\:mm}" : null,
            Espera.Length > 0 ? Espera : null
        }.Where(x => x is not null)),
        EtapaFila.Chamado => ChamadoHa,
        EtapaFila.EmAtendimento => InicioEm is { } i ? $"desde {i:HH\\:mm}" : string.Empty,
        EtapaFila.Finalizado => FimEm is { } f ? $"\u00E0s {f:HH\\:mm}" : string.Empty,
        _ => string.Empty
    };

    /// <summary>
    /// A coluna PRONTUÁRIO — a do print do Smart Clinic. "Pendente" é o que ainda não
    /// tem evolução, inclusive o horário que ainda vai acontecer: a coluna responde "o
    /// que falta escrever", e o que vai acontecer também falta. Cancelado e falta não
    /// têm registro a escrever.
    /// </summary>
    public string Prontuario => ForaDaFila ? "\u2014" : EvolucaoEscrita ? "Escrito" : "Pendente";

    /// <summary>Pinta o "Pendente" — só quando há sessão para escrever.</summary>
    public bool ProntuarioPendente => !ForaDaFila && !EvolucaoEscrita;

    /// <summary>Cancelado e falta não se atendem — o botão SOME, não fica apagado.</summary>
    public bool PodeAtender => !ForaDaFila;

    public static LinhaSessao De(SessaoDoDia s) => new()
    {
        AgendamentoId = s.AgendamentoId,
        Data = DateOnly.FromDateTime(s.DataHora),
        PacienteId = s.PacienteId,
        Paciente = s.PacienteNome,
        // Com o término (redesenho de ago/2026): "quando a sala vaga" é a metade que o
        // início sozinho calava — e é a que decide se cabe um encaixe.
        Hora = $"{s.DataHora:HH:mm}–{s.FimPrevisto:HH:mm}",
        Modalidade = s.Modalidade,
        Local = s.Sala ?? "—",
        Situacao = Rotular(s.Status, s.Etapa),
        ForaDaFila = s.Status is StatusAgendamento.Cancelado or StatusAgendamento.Faltou,
        EvolucaoEscrita = s.EvolucaoEscrita,
        RegistroPendente = s.RegistroPendente,
        Encaixe = s.Encaixe,
        AtendimentoId = s.AtendimentoId,
        Etapa = s.Etapa,
        Observacoes = s.Observacoes ?? string.Empty,
        Espera = s.EsperaMinutos is { } m ? $"espera {m} min" : string.Empty,
        ChamadoHa = s.ChamadoHaMinutos is { } c
            ? (c == 0 ? "chamado agora" : $"chamado há {c} min")
            : string.Empty,
        ChamadaDemorada = s.ChamadoHaMinutos >= ChamadaDemoradaMinutos,
        ChegadaEm = s.ChegadaEm,
        InicioEm = s.InicioAtendimentoEm,
        FimEm = s.FimAtendimentoEm
    };

    /// <summary>
    /// A partir daqui a chamada está demorando. Três minutos é o tempo de alguém se
    /// levantar e andar até a sala; passando disso, ou a pessoa não ouviu ou saiu — e
    /// quem está parado esperando é o profissional.
    /// </summary>
    public const int ChamadaDemoradaMinutos = 3;

    /// <summary>
    /// A situação como o consultório a lê. A etapa da fila (chegou, está na sala) só
    /// interessa enquanto o horário está de pé; depois de realizado o que importa é que a
    /// sessão aconteceu.
    /// </summary>
    private static string Rotular(StatusAgendamento status, EtapaFila etapa) => status switch
    {
        StatusAgendamento.Realizado => "Atendido",
        StatusAgendamento.Cancelado => "Cancelado",
        StatusAgendamento.Faltou => "Faltou",
        _ => etapa switch
        {
            EtapaFila.Chegou => "Na recepção",
            EtapaFila.Chamado => "Chamado",
            EtapaFila.EmAtendimento => "Na sala",
            _ => "Aguardando"
        }
    };
}

/// <summary>Uma sessão de dia anterior que ficou sem evolução escrita.</summary>
public sealed class LinhaRegistroPendente
{
    public required int AgendamentoId { get; init; }

    /// <summary>Dia do horário — viaja para o posto porque o casamento sessão × evolução cai para paciente + data.</summary>
    public required DateOnly Data { get; init; }
    public required int PacienteId { get; init; }
    public required string Paciente { get; init; }
    public required string Quando { get; init; }
    public required string Modalidade { get; init; }
    public required string Atraso { get; init; }

    /// <summary>Quem atendeu — vazio quando o agendamento não tem profissional. É o que alimenta o filtro do modo sem vínculo.</summary>
    public string Profissional { get; init; } = string.Empty;

    public static LinhaRegistroPendente De(RegistroPendente r, DateOnly hoje)
    {
        var dias = r.DiasEmAberto(hoje);
        return new LinhaRegistroPendente
        {
            AgendamentoId = r.AgendamentoId,
            Data = DateOnly.FromDateTime(r.DataHora),
            PacienteId = r.PacienteId,
            Paciente = r.PacienteNome,
            Profissional = r.Profissional ?? string.Empty,
            Quando = r.DataHora.ToString("dd/MM 'às' HH:mm"),
            Modalidade = r.Modalidade,
            Atraso = dias == 1 ? "ontem" : $"há {dias} dias"
        };
    }
}

/// <summary>
/// A abertura do consultório: o dia de quem atende.
///
/// A tela responde três coisas, nesta ordem — quem vem hoje, o que já foi atendido e
/// **o que eu ainda não escrevi**. A terceira é a que justifica o módulo: a agenda da
/// recepção já mostrava as duas primeiras, e nenhuma tela do sistema mostrava a terceira.
/// </summary>
public sealed partial class MeuDiaViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly PacienteEmFoco _foco;

    /// <summary>
    /// O dia como LISTA, na ordem da hora (parcela 95).
    ///
    /// O quadro de cinco raias (parcela 38) foi feito por paridade com a fila do balcão —
    /// e as raias respondem à pergunta do BALCÃO, que administra vinte pessoas em cinco
    /// estados. O médico tem oito horários e uma pergunta: "quem é o próximo e o que eu
    /// já escrevi". A cliente mandou o print da agenda que ela usava (Smart Clinic): uma
    /// linha por horário, o status ali, a coluna PRONTUÁRIO e o botão ATENDER. A regra
    /// da direção é "quanto mais simples, melhor", e cinco colunas para oito linhas não
    /// é simples.
    ///
    /// O que a lista NÃO perdeu: a etapa continua saindo de <c>Agendamento.Etapa</c>, os
    /// mesmos carimbos que a fila do balcão lê (nenhuma sincronização — os dois leem a
    /// mesma tabela); "quem posso chamar agora" é a coluna STATUS com a hora da chegada;
    /// e as transições (chamar, desfazer, entrou, voltar) continuam na linha, na etapa
    /// em que servem. O que se perdeu foi o ARRASTO, que numa lista não tem para onde ir.
    /// </summary>
    public ObservableCollection<LinhaSessao> Sessoes { get; } = [];

    /// <summary>
    /// Quantas sessões de dias anteriores continuam sem evolução escrita.
    ///
    /// Aqui fica só o NÚMERO. A lista saiu desta tela e virou tela própria: são dezenas
    /// de linhas respondendo a uma pergunta que não é "o que acontece hoje", e ela vinha
    /// espremida numa caixa de 180 px que cortava um nome ao meio. Contagem no botão,
    /// lista na tela dela — ver <see cref="ModuloClinico.ChaveRegistrosPendentes"/>.
    /// </summary>
    [ObservableProperty] private int _pendentesCount;

    /// <summary>Há dívida de prontuário — o botão do cabeçalho fica em destaque.</summary>
    public bool TemPendentes => PendentesCount > 0;

    /// <summary>O que o botão diz. Zero não é "0 pendências", é "prontuário em dia".</summary>
    public string RotuloPendentes => PendentesCount switch
    {
        0 => "Prontuário em dia",
        1 => "1 sessão sem evolução",
        var n => $"{n} sessões sem evolução"
    };

    partial void OnPendentesCountChanged(int value)
    {
        OnPropertyChanged(nameof(TemPendentes));
        OnPropertyChanged(nameof(RotuloPendentes));
    }

    [ObservableProperty] private DateTime _dia = DateTime.Today;

    [ObservableProperty] private string _profissional = string.Empty;

    [ObservableProperty] private bool _carregando;

    /// <summary>A leitura FALHOU — nunca desenhar falha como "não há nada".</summary>
    [ObservableProperty] private bool _naoVerificado;

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>
    /// O usuário não tem <c>Profissional</c> vinculado ao login, e a tela está mostrando o
    /// dia da clínica inteira. Dito na tela de propósito: um dia com o dobro de horários
    /// sem explicação se lê como defeito.
    /// </summary>
    [ObservableProperty] private bool _listaDaClinica;

    /// <summary>POR QUE a lista é a da clínica — a frase certa para cada motivo.</summary>
    [ObservableProperty] private string? _motivoDaLista;

    /// <summary>
    /// "Agenda fechada neste dia: Férias" — o bloqueio que alcança este profissional no
    /// dia aberto. Nulo = agenda aberta. Férias e feriado eram invisíveis no Consultório:
    /// o dia fechado aparecia como dia vazio, que se lê como "ninguém marcou".
    /// </summary>
    [ObservableProperty] private string? _agendaFechada;

    /// <summary>
    /// Quem seria chamado se o profissional clicasse agora: o primeiro da recepção, pelo
    /// horário. Vazio quando não há ninguém no balcão — e aí o botão fica desabilitado,
    /// em vez de chamar o ar.
    /// </summary>
    [ObservableProperty] private string _proximoNome = string.Empty;

    private int _proximoAgendamentoId;

    /// <summary>Há alguém no balcão para chamar.</summary>
    public bool TemProximo => _proximoAgendamentoId != 0;

    /// <summary>
    /// O botão grande do dia: precisa de alguém no balcão, da permissão de mover a fila
    /// E de um profissional vinculado. Composto no VM porque o botão só liga UMA
    /// propriedade — as duas condições em MultiBinding no XAML seriam a versão frágil
    /// disto.
    ///
    /// ⚠️ NO MODO "LISTA DA CLÍNICA" ele fica desligado, e é decisão: nesse modo a lista
    /// mostra a clínica inteira, e "o primeiro da recepção" pode ser paciente de OUTRO
    /// profissional — o clique cego anunciaria um nome para a sala do colega. Chamar por uma
    /// LINHA específica continua liberado: ali a escolha é de quem olhou o nome antes de
    /// clicar.
    ///
    /// São DOIS os caminhos até esse modo (ver <see cref="PostoClinico"/>): não haver
    /// cadastro vinculado, e não haver agenda própria — que é o caso da enfermagem.
    /// </summary>
    public bool PodeChamarProximo => TemProximo && PodeMovimentarFila && !ListaDaClinica;

    partial void OnListaDaClinicaChanged(bool value)
        => OnPropertyChanged(nameof(PodeChamarProximo));

    /// <summary>
    /// Metade visível da permissão de abrir prontuário: "Atender" e a dívida de sessões
    /// sem evolução levam a telas que exigem <c>VerProntuario</c>, e
    /// <c>NavegacaoSuite.Ir</c> devolve false EM SILÊNCIO quando o destino não existe
    /// para quem está logado — o botão aceso que não faz nada é o defeito da parcela 41.
    /// </summary>
    public bool PodeVerProntuario => SessaoUsuario.Atual.Pode(Permissao.VerProntuario);

    /// <summary>Dia sem horário nenhum.</summary>
    public bool QuadroVazio => Sessoes.Count == 0;

    /// <summary>
    /// A chamada só faz sentido HOJE. Olhar a agenda de terça que vem e poder chamar
    /// alguém de lá seria mandar a recepção anunciar um nome com dois dias de antecedência.
    /// </summary>
    public bool EhHoje => Dia.Date == DateTime.Today;

    /// <summary>
    /// Metade visível da permissão de mover a fila; a que impede é o <c>ExigirAlgum</c>
    /// nos comandos. A regra é UMA nos dois quadros (balcão e consultório):
    /// <c>EditarAgenda</c> OU <c>MovimentarFila</c> — mover a fila grava carimbo de hora,
    /// e escrita sob <c>VerAgenda</c> era a divergência que a parcela 61 corrigiu.
    /// </summary>
    public bool PodeMovimentarFila => SessaoUsuario.Atual.PodeAlgum(
        Permissao.EditarAgenda | Permissao.MovimentarFila);

    private readonly System.Windows.Threading.DispatcherTimer _relogio;

    public MeuDiaViewModel(IServiceScopeFactory escopos, PacienteEmFoco foco)
    {
        _escopos = escopos;
        _foco = foco;

        // O outro lado do circuito. O consultório sabe o que ele mesmo clicou, mas quem
        // faz o check-in é o BALCÃO — e sem esta batida "Chamar próximo" continuaria
        // dizendo "ninguém aguardando" com o paciente já sentado na sala de espera,
        // porque a tela só releria por clique em Atualizar. É o mesmo relógio da fila da
        // recepção, pelo mesmo motivo e no mesmo intervalo.
        //
        // Quem liga e desliga é a View (Loaded/Unloaded): o shell cria uma tela nova a
        // cada navegação, e um timer rodando manteria vivo cada ViewModel já trocado.
        _relogio = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _relogio.Tick += (_, _) => _ = ReconferirAsync();

        _ = CarregarAsync();
    }

    /// <summary>Liga a releitura periódica (chamada quando a tela entra em cena).</summary>
    public void IniciarRelogio() => _relogio.Start();

    /// <summary>Desliga a releitura (chamada quando a tela sai de cena).</summary>
    public void PararRelogio() => _relogio.Stop();

    /// <summary>
    /// A batida do relógio: relê o dia por baixo.
    ///
    /// Não acende o "Carregando" nem escreve mensagem de erro: é recarga de fundo, e
    /// quem está com um paciente na frente não pode ver o quadro piscar em branco a
    /// cada minuto nem levar um aviso vermelho porque o banco demorou uma vez. A falha
    /// vai para o log e a tela segue com o quadro do minuto anterior.
    ///
    /// Só relê HOJE: quem está conferindo a agenda de terça que vem não tem fila
    /// correndo, e o quadro se mexendo sozinho enquanto ele lê seria só ruído.
    /// </summary>
    private async Task ReconferirAsync()
    {
        if (!EhHoje || Carregando) return;

        try
        {
            await CarregarAsync(silencioso: true);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — releitura automática do dia falhou", ex);
        }
    }

    partial void OnDiaChanged(DateTime value)
    {
        OnPropertyChanged(nameof(EhHoje));
        _ = CarregarAsync();
    }

    /// <summary>
    /// De quem é o dia mostrado. A resposta mora num lugar só (<see cref="PostoClinico"/>):
    /// a enfermagem NÃO tem agenda própria — os horários são de quem consulta, e ela passa
    /// por todos —, e filtrar por ela devolvia o quadro VAZIO justamente para quem está
    /// cadastrado certo.
    /// </summary>
    private static int? ProfissionalDaSessao => PostoClinico.ProfissionalDaLista();

    [RelayCommand]
    public Task CarregarAsync() => CarregarAsync(silencioso: false);

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): a batida do relógio e o stepper
    /// de dia concorrem, e a resposta de HOJE chegando por último deixaria o quadro de um
    /// dia sob o título de outro. A meia-guarda por <c>Carregando</c> não cobria — a
    /// recarga silenciosa roda com ele desligado.
    /// </summary>
    private int _geracaoCarga;

    private async Task CarregarAsync(bool silencioso)
    {
        var geracao = ++_geracaoCarga;

        try
        {
            Carregando = !silencioso;
            // A recarga de fundo não apaga o recado da última ação: "Ana foi chamada —
            // a recepção já está vendo o aviso" sumindo sozinho um minuto depois faria
            // quem clicou duvidar de que o clique valeu.
            //
            // E não apaga o "não verificado" pendente: a batida silenciosa que também
            // falha deixaria a tela limpa, afirmando um dia conferido que ninguém
            // conferiu. Quem levanta e quem baixa esse estado é a carga que a PESSOA
            // pediu — as três telas irmãs do balcão já fazem assim.
            if (!silencioso)
            {
                NaoVerificado = false;
                Mensagem = null;
                MensagemEhErro = false;
            }
            var profissionalId = ProfissionalDaSessao;
            ListaDaClinica = profissionalId is null;
            MotivoDaLista = PostoClinico.MotivoDaListaAmpla();

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var dia = DateOnly.FromDateTime(Dia);

            using var scope = _escopos.CreateScope();
            var consultorio = scope.ServiceProvider.GetRequiredService<ConsultorioService>();

            var doDia = await consultorio.DoDiaAsync(dia, profissionalId);

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Profissional = doDia.ProfissionalNome;

            // ⚠️ O Clear vem DEPOIS do await, junto dos Adds — nunca antes.
            //
            // Limpar a lista na entrada esvaziava a tela durante todo o roundtrip ao
            // banco remoto, e a releitura de fundo (1 min, sem "Carregando") fazia isso
            // debaixo do olho de quem atende. Pior: quando a leitura falhava, o catch
            // encontrava a lista JÁ vazia — e o comentário logo acima promete o
            // contrário ("a tela segue com o quadro do minuto anterior"). Comentário que
            // descreve degradação sem o código que a realiza é o defeito da parcela 67.
            //
            // É a regra da parcela 62 aplicada aqui: entre o Clear() e o último Add não
            // pode haver await. O FilaViewModel, o quadro irmão do balcão, já fazia assim.
            var linhas = doDia.Sessoes
                .OrderBy(x => x.DataHora).ThenBy(x => x.AgendamentoId)
                .Select(LinhaSessao.De)
                .ToList();
            Sessoes.Clear();
            foreach (var l in linhas) Sessoes.Add(l);

            _proximoAgendamentoId = doDia.ProximoAChamar?.AgendamentoId ?? 0;
            ProximoNome = doDia.ProximoAChamar?.PacienteNome ?? string.Empty;
            OnPropertyChanged(nameof(TemProximo));
            OnPropertyChanged(nameof(PodeChamarProximo));
            OnPropertyChanged(nameof(QuadroVazio));

            // Não há linha de "resumo" abaixo do título: a lista é curta o bastante para
            // ser lida inteira, e uma frase de contagens acima dela é uma faixa de texto a
            // mais entre o profissional e o trabalho.

            // A pendência é sempre de DIAS ANTERIORES, mesmo quando se olha um dia passado
            // na agenda: ela é a fila de trabalho do profissional, não uma propriedade do
            // dia escolhido — trocá-la junto com o calendário faria a lista sumir na hora
            // em que ele foi conferir o que aconteceu na semana retrasada.
            //
            // Aqui só se conta. A LISTA mora na tela dela.
            //
            // ⚠️ E a batida do relógio NÃO reconta: são os agendamentos e as evoluções de
            // 30 dias relidos a cada minuto para atualizar um número que só muda quando
            // alguém escreve uma evolução — e quem escreve está NESTA máquina, que
            // recarrega ao voltar para a tela. A recarga silenciosa relê só o quadro de
            // hoje, que é o que a outra máquina muda por baixo.
            if (!silencioso)
            {
                var pendentes = (await consultorio.RegistrosPendentesAsync(hoje, profissionalId)).Count;
                if (geracao != _geracaoCarga) return;
                PendentesCount = pendentes;

                // Férias e feriado eram invisíveis aqui: o dia fechado aparecia como dia
                // vazio, que se lê como "ninguém marcou". Falhar não derruba o quadro —
                // segue sem o aviso, como antes — mas vai para o log.
                try
                {
                    var bloqueio = (await scope.ServiceProvider
                            .GetRequiredService<BloqueioAgendaService>()
                            .NoPeriodoAsync(Dia.Date, Dia.Date.AddDays(1)))
                        .FirstOrDefault(b => b.AlcancaRecurso(profissionalId, null));
                    if (geracao != _geracaoCarga) return;
                    AgendaFechada = bloqueio is null
                        ? null
                        : $"Agenda fechada neste dia: {bloqueio.Motivo}";
                }
                catch (Exception ex)
                {
                    if (geracao != _geracaoCarga) return;
                    AgendaFechada = null;
                    Clinica.Application.Diagnostico.Registrar(
                        "Consultório — bloqueios do dia não puderam ser lidos", ex);
                }
            }
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;

            Clinica.Application.Diagnostico.Registrar("Consultório — o dia não pôde ser carregado", ex);

            // Recarga de fundo que falha não interrompe quem está atendendo: já foi para o
            // log, e a tela segue com o quadro do minuto anterior.
            //
            // ⚠️ `NaoVerificado` entra JUNTO da mensagem, e não antes dela. Ele acende a
            // sobreposição do `EstadoDaTela` — que, numa recarga silenciosa, aparecia por
            // cima de uma lista CHEIA (ela só é limpa depois do await, desde a
            // parcela 68) dizendo que a tela estava vazia por falha de leitura. O
            // comentário aqui prometia o contrário havia parcelas: comentário que descreve
            // degradação sem o código que a realiza é o defeito da parcela 67.
            //
            // As três telas irmãs do balcão — Agenda, Fila e Painel — já saíam do catch
            // sem tocar em nada quando a carga era silenciosa. Esta era a que ninguém
            // releu.
            if (silencioso) return;

            NaoVerificado = true;
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    /// <summary>
    /// AVISA A RECEPÇÃO que este paciente pode entrar (parcela 38).
    ///
    /// Não é o médico que chama pelo nome na sala de espera — ele está na sala, com a
    /// porta fechada. O que este botão faz é o recado atravessar: a linha passa a
    /// "Chamado" e o cartão aparece destacado na fila do balcão, que anuncia a pessoa.
    ///
    /// A sincronização entre os dois módulos é o BANCO, como todo o resto da suíte: não
    /// há fila de mensagens nem evento. O consultório carimba a hora, a recepção lê a
    /// mesma linha — e é por isso que os dois quadros nunca divergem.
    /// </summary>
    [RelayCommand]
    private Task ChamarAsync(LinhaSessao? linha)
        => linha is null ? Task.CompletedTask : ChamarPorIdAsync(linha.AgendamentoId, linha.Paciente);

    /// <summary>Chama o primeiro da recepção, sem procurar a linha na lista.</summary>
    [RelayCommand]
    private Task ChamarProximoAsync()
    {
        if (_proximoAgendamentoId == 0) return Task.CompletedTask;

        // A segunda barreira do modo sem vínculo (a primeira é o IsEnabled): atalho e
        // corrida de carregamento passam pelo comando, e a guarda diz por quê em vez de
        // voltar calada.
        if (ListaDaClinica)
        {
            // ⚠️ A frase sai do PONTO ÚNICO, e não é uma escrita à mão aqui. Ela dizia
            // "peça à direção para ligar o seu usuário ao seu cadastro" — verdade para
            // quem não tem vínculo, e MENTIRA para a enfermagem, que está vinculada e
            // simplesmente não tem agenda própria. Instrução errada com cara de instrução
            // certa manda o suporte procurar um defeito que não existe.
            Mensagem = MotivoDaLista
                       + " Como a lista é a da clínica, o primeiro da fila pode ser "
                       + "paciente de outro profissional: chame pela linha dele.";
            MensagemEhErro = true;
            return Task.CompletedTask;
        }

        return ChamarPorIdAsync(_proximoAgendamentoId, ProximoNome);
    }

    private async Task ChamarPorIdAsync(int agendamentoId, string paciente)
    {
        try
        {
            SessaoUsuario.Atual.ExigirAlgum(
                Permissao.EditarAgenda | Permissao.MovimentarFila, "chamar o paciente");

            using var scope = _escopos.CreateScope();
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
            await agenda.ChamarAsync(agendamentoId, SessaoUsuario.Atual.Operador);

            Mensagem = $"{paciente} foi chamado — a recepção já está vendo o aviso.";
            MensagemEhErro = false;
            // ⚠️ SILENCIOSA. A carga que a PESSOA pede começa zerando `Mensagem` — e o
            // recado que acabou de ser escrito duas linhas acima ("Fulano foi chamado — a
            // recepção já está vendo o aviso") era apagado no mesmo instante, antes de
            // qualquer olho o alcançar. O comentário da própria carga já dizia que a
            // recarga de fundo não apaga o recado da última ação; o que faltava era
            // ESTA chamada usá-la.
            await CarregarAsync(silencioso: true);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — paciente não pôde ser chamado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>Desfaz a chamada: enganou-se de paciente, ou vai demorar mais.</summary>
    [RelayCommand]
    private async Task DesfazerChamadaAsync(LinhaSessao? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.ExigirAlgum(
                Permissao.EditarAgenda | Permissao.MovimentarFila, "desfazer a chamada");

            using var scope = _escopos.CreateScope();
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
            await agenda.DesfazerChamadaAsync(linha.AgendamentoId, SessaoUsuario.Atual.Operador);

            Mensagem = $"Chamada de {linha.Paciente} desfeita.";
            MensagemEhErro = false;
            // ⚠️ SILENCIOSA. A carga que a PESSOA pede começa zerando `Mensagem` — e o
            // recado que acabou de ser escrito duas linhas acima ("Fulano foi chamado — a
            // recepção já está vendo o aviso") era apagado no mesmo instante, antes de
            // qualquer olho o alcançar. O comentário da própria carga já dizia que a
            // recarga de fundo não apaga o recado da última ação; o que faltava era
            // ESTA chamada usá-la.
            await CarregarAsync(silencioso: true);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — chamada não pôde ser desfeita", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// O paciente ENTROU na sala. Era a transição que faltava do lado do médico: sem ela,
    /// o "Em atendimento" do lado dele só acontecia se o BALCÃO clicasse — e quem
    /// abre a porta para o paciente é o profissional, não a recepção. Entrar sem ter sido
    /// chamado carimba a chamada junto (linha do tempo com entrada e sem chamada não
    /// existe — regra do <c>AgendaService</c>).
    /// </summary>
    [RelayCommand]
    private async Task EntrarAsync(LinhaSessao? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.ExigirAlgum(
                Permissao.EditarAgenda | Permissao.MovimentarFila, "marcar a entrada");

            using var scope = _escopos.CreateScope();
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
            await agenda.IniciarAtendimentoAsync(linha.AgendamentoId, SessaoUsuario.Atual.Operador);

            Mensagem = $"{linha.Paciente} em atendimento.";
            MensagemEhErro = false;
            // ⚠️ SILENCIOSA. A carga que a PESSOA pede começa zerando `Mensagem` — e o
            // recado que acabou de ser escrito duas linhas acima ("Fulano foi chamado — a
            // recepção já está vendo o aviso") era apagado no mesmo instante, antes de
            // qualquer olho o alcançar. O comentário da própria carga já dizia que a
            // recarga de fundo não apaga o recado da última ação; o que faltava era
            // ESTA chamada usá-la.
            await CarregarAsync(silencioso: true);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — entrada não pôde ser marcada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Desfaz o "entrou" clicado por engano — um passo por vez, como no balcão: apagar
    /// mais de um carimbo de uma vez inventaria uma linha do tempo que não aconteceu.
    ///
    /// FINALIZAR não fica no QUADRO, e continua sendo decisão: finalizar é o desfecho de
    /// um atendimento que se acabou de escrever, e o botão dele mora na tela do paciente,
    /// ao lado do que foi registrado. Desde a parcela 95 ele CONCLUI a sessão (carimba a
    /// presença e gera as guias); o que segue no balcão é o dinheiro — pacote, insumo e
    /// caixa —, que aparece na fila como fechamento pendente.
    /// </summary>
    [RelayCommand]
    private async Task VoltarEtapaAsync(LinhaSessao? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.ExigirAlgum(
                Permissao.EditarAgenda | Permissao.MovimentarFila, "voltar a etapa");

            using var scope = _escopos.CreateScope();
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
            await agenda.VoltarEtapaAsync(linha.AgendamentoId, SessaoUsuario.Atual.Operador);

            Mensagem = $"{linha.Paciente} devolvido à etapa anterior.";
            MensagemEhErro = false;
            // ⚠️ SILENCIOSA. A carga que a PESSOA pede começa zerando `Mensagem` — e o
            // recado que acabou de ser escrito duas linhas acima ("Fulano foi chamado — a
            // recepção já está vendo o aviso") era apagado no mesmo instante, antes de
            // qualquer olho o alcançar. O comentário da própria carga já dizia que a
            // recarga de fundo não apaga o recado da última ação; o que faltava era
            // ESTA chamada usá-la.
            await CarregarAsync(silencioso: true);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — etapa não pôde ser desfeita", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    [RelayCommand]
    private void DiaAnterior() => Dia = Dia.AddDays(-1);

    [RelayCommand]
    private void DiaSeguinte() => Dia = Dia.AddDays(1);

    [RelayCommand]
    private void Hoje() => Dia = DateTime.Today;

    /// <summary>
    /// Chama o paciente para o atendimento: fixa o foco do posto e abre a tela de
    /// atendimento. É o único caminho em que a evolução nasce ligada ao AGENDAMENTO —
    /// e é esse vínculo que faz a sessão sair da lista de pendências depois de escrita.
    ///
    /// ⚠️ ELE CARIMBA A ENTRADA NA SALA (parcela 95). O fluxo que a direção pediu é de um
    /// clique — "cai na agenda do médico, ele clica em atender e faz o atendimento" —, e
    /// até aqui este botão só abria o prontuário: a linha ficava parada em "Marcado"
    /// enquanto o paciente estava na cadeira, e mover a fila era um segundo clique que
    /// ninguém dava. O quadro do balcão então mentia sobre quem estava ocupado, e a
    /// espera do paciente continuava correndo depois de ele ter entrado.
    ///
    /// O que se carimba é FATO: quem clica em Atender está com a pessoa na frente. É o
    /// mesmo <c>IniciarAtendimentoAsync</c> do botão "Entrou" (que continua existindo, para
    /// quem quer mover a fila sem abrir o prontuário), e o desfazer é o "Voltar" de sempre.
    ///
    /// ⚠️ Falhar o carimbo NÃO impede abrir o prontuário: ler o registro do paciente que
    /// está na sala não pode depender de o banco ter respondido a uma escrita de fila. A
    /// falha vira aviso na tela e linha no log — nunca silêncio, que faria o médico
    /// atender achando que o balcão foi avisado.
    /// </summary>
    [RelayCommand]
    private async Task AtenderAsync(LinhaSessao? linha)
    {
        if (linha is null) return;

        // A tela de destino exige VerProntuario, e sem o bit ela nem existe na navegação
        // — NavegacaoSuite.Ir voltaria false EM SILÊNCIO e o botão não faria nada. A
        // guarda diz por quê (parcela 41); a metade visível é o IsEnabled do botão.
        if (!PodeVerProntuario)
        {
            Mensagem = "Abrir o atendimento mostra o prontuário, e o seu acesso não tem "
                       + "essa permissão. A direção libera em Acessos.";
            MensagemEhErro = true;
            return;
        }

        // A ENTRADA NA SALA, antes de abrir a tela. Só quando há o que carimbar: o
        // horário é de HOJE (a fila corre só hoje — abrir a sessão de ontem pela dívida
        // de prontuário não pode mover fila nenhuma), está em aberto e ainda não começou.
        if (linha.EhHoje && linha.Etapa is EtapaFila.Aguardando or EtapaFila.Chegou or EtapaFila.Chamado)
            await CarimbarEntradaAsync(linha);

        _foco.Definir(linha.PacienteId, linha.Paciente, linha.AgendamentoId,
                      linha.AtendimentoId, linha.Data);
        // A seção de escrita de QUEM clicou: quem consulta cai no S-O-A-P, quem
        // executa cai na passagem de enfermagem. Uma palavra, dois destinos certos.
        NavegacaoSuite.Ir(PostoClinico.ChaveDoAtendimento());
    }

    /// <summary>
    /// O carimbo de entrada do <see cref="AtenderAsync"/> — separado porque ele não pode
    /// derrubar a abertura do prontuário, e porque a permissão dele é a da FILA, que não é
    /// a mesma de abrir o registro.
    /// </summary>
    private async Task CarimbarEntradaAsync(LinhaSessao linha)
    {
        // Sem o bit da fila, atender continua valendo — o que não acontece é o recado ao
        // balcão. Recusa CALADA aqui seria o pior dos dois mundos: o médico atenderia
        // achando que o quadro andou.
        if (!SessaoUsuario.Atual.PodeAlgum(Permissao.EditarAgenda | Permissao.MovimentarFila))
        {
            Mensagem = "Prontuário aberto. O balcão não foi avisado de que o paciente "
                       + "entrou: o seu acesso não move a fila do dia.";
            MensagemEhErro = false;
            return;
        }

        try
        {
            using var scope = _escopos.CreateScope();
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
            await agenda.IniciarAtendimentoAsync(linha.AgendamentoId, SessaoUsuario.Atual.Operador);
            await CarregarAsync(silencioso: true);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — entrada não pôde ser carimbada ao atender", ex);
            Mensagem = "Prontuário aberto, mas o balcão não foi avisado de que o paciente "
                       + $"entrou na sala: {ex.Message}";
            MensagemEhErro = true;
        }
    }

    /// <summary>Abre a tela das sessões sem evolução — a dívida de prontuário.</summary>
    [RelayCommand]
    private void AbrirPendentes()
    {
        // Mesmo caso do Atender: o destino exige VerProntuario, e Ir falha calado sem ele.
        if (!PodeVerProntuario)
        {
            Mensagem = "A lista de sessões sem evolução é prontuário, e o seu acesso não "
                       + "tem essa permissão. A direção libera em Acessos.";
            MensagemEhErro = true;
            return;
        }

        NavegacaoSuite.Ir(ModuloClinico.ChaveRegistrosPendentes);
    }

    // O atalho "Dor" saiu do cartão do quadro e o comando foi removido junto — comando sem
    // chamador em produção é o defeito que o projeto documenta desde a parcela 25, e
    // deixá-lo aqui só para "não perder" seria cometê-lo de propósito.
    //
    // A capacidade não se perdeu: "Atender" abre a tela do paciente, onde a dor é uma ABA
    // (o desenho da parcela 37 — lista → tela do item), e a carteira em "Meus pacientes"
    // continua com o atalho direto. O que se ganhou foi um cartão com UMA ação principal:
    // quatro botões acesos em cada um dos vinte cartões de uma raia faziam a coluna
    // parecer um formulário, não uma fila.
}
