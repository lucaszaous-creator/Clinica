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

    /// <summary>Chamado e ainda não entrou — o cartão fica em destaque no quadro.</summary>
    public required string ChamadoHa { get; init; }

    public required EtapaFila Etapa { get; init; }

    /// <summary>
    /// Cancelado ou falta. O cartão CONTINUA na coluna "aguardando" — quem lê às 14h
    /// precisa saber que o horário das 15h vagou, e linha ausente se confunde com horário
    /// que nunca existiu (a regra da folha do dia).
    ///
    /// ⚠️ Mas ele precisa ficar MARCADO, e não ficava. A situação era calculada
    /// (<c>Situacao</c>, via <c>Rotular</c>) e o XAML do quadro nunca a leu — dado
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
    /// Desfazer o "entrou" clicado por engano. Só da coluna EM ATENDIMENTO: a chamada tem
    /// o próprio desfazer, e o check-in é ato do balcão — não se desfaz daqui.
    /// </summary>
    public bool PodeVoltarEtapa => Etapa == EtapaFila.EmAtendimento && EhHoje;

    /// <summary>A chamada está pendente há tempo demais — vale insistir com o balcão.</summary>
    public bool ChamadaDemorada { get; init; }

    public static LinhaSessao De(SessaoDoDia s) => new()
    {
        AgendamentoId = s.AgendamentoId,
        Data = DateOnly.FromDateTime(s.DataHora),
        PacienteId = s.PacienteId,
        Paciente = s.PacienteNome,
        Hora = s.DataHora.ToString("HH:mm"),
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
        ChamadaDemorada = s.ChamadoHaMinutos >= ChamadaDemoradaMinutos
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
    /// As COLUNAS do quadro. O dia deixou de ser uma lista corrida: quem atende não
    /// pergunta "quem está marcado", pergunta "quem já está aí e quem posso chamar".
    ///
    /// As colunas saem de <c>Agendamento.Etapa</c>, que é derivada dos carimbos de hora —
    /// as MESMAS que a fila do balcão usa. Não há sincronização nenhuma entre os dois
    /// módulos: eles leem a mesma tabela, e é isso que faz o quadro do médico e o da
    /// recepção nunca divergirem.
    /// </summary>
    public ObservableCollection<LinhaSessao> Aguardando { get; } = [];
    public ObservableCollection<LinhaSessao> NaRecepcao { get; } = [];
    public ObservableCollection<LinhaSessao> Chamados { get; } = [];
    public ObservableCollection<LinhaSessao> EmAtendimento { get; } = [];
    public ObservableCollection<LinhaSessao> Finalizados { get; } = [];

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
    [ObservableProperty] private bool _semVinculo;

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
    /// ⚠️ SEM VÍNCULO ele fica desligado, e é decisão: nesse modo a tela mostra a clínica
    /// inteira, e "o primeiro da recepção" pode ser paciente de OUTRO profissional — o
    /// clique cego anunciaria um nome para a sala do colega. Chamar por um CARTÃO
    /// específico continua liberado: ali a escolha é de quem olhou o nome antes de clicar.
    /// </summary>
    public bool PodeChamarProximo => TemProximo && PodeMovimentarFila && !SemVinculo;

    partial void OnSemVinculoChanged(bool value)
        => OnPropertyChanged(nameof(PodeChamarProximo));

    /// <summary>
    /// Metade visível da permissão de abrir prontuário: "Atender" e a dívida de sessões
    /// sem evolução levam a telas que exigem <c>VerProntuario</c>, e
    /// <c>NavegacaoSuite.Ir</c> devolve false EM SILÊNCIO quando o destino não existe
    /// para quem está logado — o botão aceso que não faz nada é o defeito da parcela 41.
    /// </summary>
    public bool PodeVerProntuario => SessaoUsuario.Atual.Pode(Permissao.VerProntuario);

    /// <summary>O quadro está vazio nas CINCO colunas — dia sem movimento nenhum.</summary>
    public bool QuadroVazio => Aguardando.Count == 0 && NaRecepcao.Count == 0
                               && Chamados.Count == 0 && EmAtendimento.Count == 0
                               && Finalizados.Count == 0;

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

    private static int? ProfissionalDaSessao => SessaoUsuario.Atual.ProfissionalId;

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
            SemVinculo = profissionalId is null;

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
            // Limpar as colunas na entrada esvaziava o quadro durante todo o roundtrip ao
            // banco remoto, e a releitura de fundo (1 min, sem "Carregando") fazia isso
            // debaixo do olho de quem atende. Pior: quando a leitura falhava, o catch
            // encontrava as colunas JÁ vazias — e o comentário logo acima promete o
            // contrário ("a tela segue com o quadro do minuto anterior"). Comentário que
            // descreve degradação sem o código que a realiza é o defeito da parcela 67.
            //
            // É a regra da parcela 62 aplicada aqui: entre o Clear() e o último Add não
            // pode haver await. O FilaViewModel, o quadro irmão do balcão, já fazia assim.
            foreach (var c in Colunas) c.Clear();

            foreach (var s in doDia.Sessoes) Coluna(s.Etapa).Add(LinhaSessao.De(s));

            _proximoAgendamentoId = doDia.ProximoAChamar?.AgendamentoId ?? 0;
            ProximoNome = doDia.ProximoAChamar?.PacienteNome ?? string.Empty;
            OnPropertyChanged(nameof(TemProximo));
            OnPropertyChanged(nameof(PodeChamarProximo));
            OnPropertyChanged(nameof(QuadroVazio));

            // Não há linha de "resumo" abaixo do título: cada coluna do quadro carrega a
            // própria contagem no cabeçalho, e repetir os mesmos cinco números numa frase
            // acima dele é uma faixa de texto a mais entre o profissional e o trabalho.

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
            // cima de um quadro CHEIO (as colunas só são limpas depois do await, desde a
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

    private IEnumerable<ObservableCollection<LinhaSessao>> Colunas
    {
        get
        {
            yield return Aguardando;
            yield return NaRecepcao;
            yield return Chamados;
            yield return EmAtendimento;
            yield return Finalizados;
        }
    }

    private ObservableCollection<LinhaSessao> Coluna(EtapaFila etapa) => etapa switch
    {
        EtapaFila.Chegou => NaRecepcao,
        EtapaFila.Chamado => Chamados,
        EtapaFila.EmAtendimento => EmAtendimento,
        EtapaFila.Finalizado => Finalizados,
        // Cancelado e falta ficam em "aguardando" pelo mesmo motivo da folha do dia: quem
        // lê às 14h precisa saber que o horário das 15h vagou, e linha ausente se confunde
        // com horário que nunca existiu.
        _ => Aguardando
    };

    /// <summary>
    /// AVISA A RECEPÇÃO que este paciente pode entrar (parcela 38).
    ///
    /// Não é o médico que chama pelo nome na sala de espera — ele está na sala, com a
    /// porta fechada. O que este botão faz é o recado atravessar: o cartão vai para a
    /// coluna "chamado" e aparece destacado na fila do balcão, que anuncia a pessoa.
    ///
    /// A sincronização entre os dois módulos é o BANCO, como todo o resto da suíte: não
    /// há fila de mensagens nem evento. O consultório carimba a hora, a recepção lê a
    /// mesma linha — e é por isso que os dois quadros nunca divergem.
    /// </summary>
    [RelayCommand]
    private Task ChamarAsync(LinhaSessao? linha)
        => linha is null ? Task.CompletedTask : ChamarPorIdAsync(linha.AgendamentoId, linha.Paciente);

    /// <summary>Chama o primeiro da recepção, sem procurar o cartão no quadro.</summary>
    [RelayCommand]
    private Task ChamarProximoAsync()
    {
        if (_proximoAgendamentoId == 0) return Task.CompletedTask;

        // A segunda barreira do modo sem vínculo (a primeira é o IsEnabled): atalho e
        // corrida de carregamento passam pelo comando, e a guarda diz por quê em vez de
        // voltar calada.
        if (SemVinculo)
        {
            Mensagem = "Sem profissional vinculado ao seu login, o primeiro da fila pode "
                       + "ser paciente de outro profissional. Chame pelo cartão dele no "
                       + "quadro — ou peça à direção para ligar o seu usuário ao seu "
                       + "cadastro.";
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
    /// a coluna EM ATENDIMENTO do quadro dele só enchia se o BALCÃO clicasse — e quem
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
    /// FINALIZAR continua não existindo aqui, e é decisão: concluir a sessão são quatro
    /// fatos do mesmo ato (guia, pacote, insumo, caixa — <c>FechamentoSessaoService</c>),
    /// e três deles são do balcão. O médico encerra escrevendo a evolução; quem fecha a
    /// sessão é quem fecha a conta.
    /// </summary>
    [RelayCommand]
    private async Task VoltarEtapaAsync(LinhaSessao? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.ExigirAlgum(
                Permissao.EditarAgenda | Permissao.MovimentarFila, "voltar o cartão");

            using var scope = _escopos.CreateScope();
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
            await agenda.VoltarEtapaAsync(linha.AgendamentoId, SessaoUsuario.Atual.Operador);

            Mensagem = $"{linha.Paciente} devolvido à coluna anterior.";
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

    /// <summary>
    /// ARRASTAR o cartão entre as raias — o mesmo gesto da fila do balcão (parcela 58),
    /// que ficou seis parcelas sem chegar ao quadro de quem atende.
    ///
    /// As transições legais são EXATAMENTE as dos botões — não uma segunda regra escrita
    /// aqui. O que o quadro do médico NÃO faz continua não fazendo pelo arrasto: check-in
    /// (é do balcão, com a conferência de elegibilidade) e conclusão (quatro fatos do
    /// mesmo ato, três do balcão). Movimento impossível não é silêncio — a tela DIZ.
    /// </summary>
    public async Task MoverParaAsync(LinhaSessao? linha, EtapaFila alvo)
    {
        if (linha is null || linha.Etapa == alvo) return;

        // A fila corre só HOJE — os botões do cartão já somem noutro dia, e o arrasto
        // precisa da mesma recusa dita, não calada (parcela 41).
        if (!linha.EhHoje)
        {
            Mensagem = "A fila corre só no dia de hoje — este quadro é de outro dia.";
            MensagemEhErro = true;
            return;
        }

        var legal = (linha.Etapa, alvo) switch
        {
            (EtapaFila.Chegou, EtapaFila.Chamado) => true,
            (EtapaFila.Chegou or EtapaFila.Chamado, EtapaFila.EmAtendimento) => true,
            // Um passo para trás: o inverso exato do que o serviço sabe desfazer.
            (EtapaFila.Chamado, EtapaFila.Chegou) => true,
            (EtapaFila.EmAtendimento, EtapaFila.Chamado) => true,
            _ => false
        };

        if (!legal)
        {
            Mensagem = alvo switch
            {
                EtapaFila.Finalizado =>
                    "Concluir a sessão é do balcão (Finalizar, na fila): junto da guia "
                    + "saem o pacote, o insumo e o caixa.",
                EtapaFila.Chegou when linha.Etapa == EtapaFila.Aguardando =>
                    "O check-in é do balcão — é lá que carteirinha e cota são conferidas "
                    + "com o paciente na frente.",
                _ => "Este cartão não anda direto para essa coluna. Um passo por vez."
            };
            MensagemEhErro = true;
            return;
        }

        if (alvo == EtapaFila.Chegou)
        {
            await DesfazerChamadaAsync(linha);
        }
        else if (alvo == EtapaFila.Chamado && linha.Etapa == EtapaFila.EmAtendimento)
        {
            await VoltarEtapaAsync(linha);
        }
        else if (alvo == EtapaFila.Chamado)
        {
            await ChamarAsync(linha);
        }
        else
        {
            await EntrarAsync(linha);
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
    /// </summary>
    [RelayCommand]
    private void Atender(LinhaSessao? linha)
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

        _foco.Definir(linha.PacienteId, linha.Paciente, linha.AgendamentoId,
                      linha.AtendimentoId, linha.Data);
        NavegacaoSuite.Ir(ModuloClinico.ChaveAtendimento);
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
