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

    /// <summary>O que o botão de registro diz — "Escrever" e "Abrir" não são a mesma ação.</summary>
    public string RotuloRegistro => EvolucaoEscrita ? "Ver registro" : "Atender";

    /// <summary>
    /// Só se chama quem já CHEGOU: chamar quem não fez check-in faria a recepção anunciar
    /// um nome para uma sala de espera onde a pessoa não está.
    /// </summary>
    public bool PodeChamar => Etapa == EtapaFila.Chegou;

    /// <summary>Já foi chamado — o consultório pode desistir, e o balcão para de anunciar.</summary>
    public bool PodeDesfazerChamada => Etapa == EtapaFila.Chamado;

    /// <summary>A chamada está pendente há tempo demais — vale insistir com o balcão.</summary>
    public bool ChamadaDemorada { get; init; }

    public static LinhaSessao De(SessaoDoDia s) => new()
    {
        AgendamentoId = s.AgendamentoId,
        PacienteId = s.PacienteId,
        Paciente = s.PacienteNome,
        Hora = s.DataHora.ToString("HH:mm"),
        Modalidade = s.Modalidade,
        Local = s.Sala ?? "—",
        Situacao = Rotular(s.Status, s.Etapa),
        EvolucaoEscrita = s.EvolucaoEscrita,
        RegistroPendente = s.RegistroPendente,
        Encaixe = s.Encaixe,
        AtendimentoId = s.AtendimentoId,
        Etapa = s.Etapa,
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
    public required int PacienteId { get; init; }
    public required string Paciente { get; init; }
    public required string Quando { get; init; }
    public required string Modalidade { get; init; }
    public required string Atraso { get; init; }

    public static LinhaRegistroPendente De(RegistroPendente r, DateOnly hoje)
    {
        var dias = r.DiasEmAberto(hoje);
        return new LinhaRegistroPendente
        {
            AgendamentoId = r.AgendamentoId,
            PacienteId = r.PacienteId,
            Paciente = r.PacienteNome,
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

    /// <summary>Sessões de dias anteriores ainda sem evolução — a pendência do consultório.</summary>
    public ObservableCollection<LinhaRegistroPendente> Pendentes { get; } = [];

    [ObservableProperty] private DateTime _dia = DateTime.Today;

    [ObservableProperty] private string _profissional = string.Empty;

    [ObservableProperty] private string _resumo = string.Empty;

    [ObservableProperty] private string _resumoPendentes = string.Empty;

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
    /// Quem seria chamado se o profissional clicasse agora: o primeiro da recepção, pelo
    /// horário. Vazio quando não há ninguém no balcão — e aí o botão fica desabilitado,
    /// em vez de chamar o ar.
    /// </summary>
    [ObservableProperty] private string _proximoNome = string.Empty;

    private int _proximoAgendamentoId;

    /// <summary>Há alguém no balcão para chamar.</summary>
    public bool TemProximo => _proximoAgendamentoId != 0;

    /// <summary>O quadro está vazio nas CINCO colunas — dia sem movimento nenhum.</summary>
    public bool QuadroVazio => Aguardando.Count == 0 && NaRecepcao.Count == 0
                               && Chamados.Count == 0 && EmAtendimento.Count == 0
                               && Finalizados.Count == 0;

    /// <summary>
    /// A chamada só faz sentido HOJE. Olhar a agenda de terça que vem e poder chamar
    /// alguém de lá seria mandar a recepção anunciar um nome com dois dias de antecedência.
    /// </summary>
    public bool EhHoje => Dia.Date == DateTime.Today;

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

    private async Task CarregarAsync(bool silencioso)
    {
        try
        {
            Carregando = !silencioso;
            NaoVerificado = false;
            // A recarga de fundo não apaga o recado da última ação: "Ana foi chamada —
            // a recepção já está vendo o aviso" sumindo sozinho um minuto depois faria
            // quem clicou duvidar de que o clique valeu.
            if (!silencioso)
            {
                Mensagem = null;
                MensagemEhErro = false;
            }
            foreach (var c in Colunas) c.Clear();
            Pendentes.Clear();

            var profissionalId = ProfissionalDaSessao;
            SemVinculo = profissionalId is null;

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var dia = DateOnly.FromDateTime(Dia);

            using var scope = _escopos.CreateScope();
            var consultorio = scope.ServiceProvider.GetRequiredService<ConsultorioService>();

            var doDia = await consultorio.DoDiaAsync(dia, profissionalId);
            Profissional = doDia.ProfissionalNome;

            foreach (var s in doDia.Sessoes) Coluna(s.Etapa).Add(LinhaSessao.De(s));

            _proximoAgendamentoId = doDia.ProximoAChamar?.AgendamentoId ?? 0;
            ProximoNome = doDia.ProximoAChamar?.PacienteNome ?? string.Empty;
            OnPropertyChanged(nameof(TemProximo));
            OnPropertyChanged(nameof(QuadroVazio));

            Resumo = doDia.Sessoes.Count == 0
                ? "Nenhum horário marcado neste dia."
                : $"{doDia.Sessoes.Count} horário(s) · {doDia.NaRecepcao} na recepção · "
                  + $"{doDia.Chamados} chamado(s) · {doDia.Atendidos} atendido(s) · "
                  + $"{doDia.RegistrosPendentes} sem registro.";

            // A pendência é sempre de DIAS ANTERIORES, mesmo quando se olha um dia passado
            // na agenda: ela é a fila de trabalho do profissional, não uma propriedade do
            // dia escolhido — trocá-la junto com o calendário faria a lista sumir na hora
            // em que ele foi conferir o que aconteceu na semana retrasada.
            var pendentes = await consultorio.RegistrosPendentesAsync(hoje, profissionalId);
            foreach (var p in pendentes) Pendentes.Add(LinhaRegistroPendente.De(p, hoje));

            ResumoPendentes = pendentes.Count == 0
                ? "Nenhuma sessão dos últimos dias sem evolução escrita."
                : $"{pendentes.Count} sessão(ões) atendida(s) nos últimos "
                  + $"{ConsultorioService.JanelaRegistroPendenteDias} dias continuam sem evolução escrita.";
        }
        catch (Exception ex)
        {
            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar("Consultório — o dia não pôde ser carregado", ex);

            // Recarga de fundo que falha não interrompe quem está atendendo com um aviso
            // vermelho: já foi para o log, e a tela segue com o quadro do minuto anterior.
            if (!silencioso)
            {
                Mensagem = ex.Message;
                MensagemEhErro = true;
            }
        }
        finally
        {
            Carregando = false;
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
        => _proximoAgendamentoId == 0
            ? Task.CompletedTask
            : ChamarPorIdAsync(_proximoAgendamentoId, ProximoNome);

    private async Task ChamarPorIdAsync(int agendamentoId, string paciente)
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.VerAgenda, "chamar o paciente");

            using var scope = _escopos.CreateScope();
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
            await agenda.ChamarAsync(agendamentoId);

            Mensagem = $"{paciente} foi chamado — a recepção já está vendo o aviso.";
            MensagemEhErro = false;
            await CarregarAsync();
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
            SessaoUsuario.Atual.Exigir(Permissao.VerAgenda, "desfazer a chamada");

            using var scope = _escopos.CreateScope();
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
            await agenda.DesfazerChamadaAsync(linha.AgendamentoId);

            Mensagem = $"Chamada de {linha.Paciente} desfeita.";
            MensagemEhErro = false;
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — chamada não pôde ser desfeita", ex);
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
    /// </summary>
    [RelayCommand]
    private void Atender(LinhaSessao? linha)
    {
        if (linha is null) return;

        _foco.Definir(linha.PacienteId, linha.Paciente, linha.AgendamentoId, linha.AtendimentoId);
        NavegacaoSuite.Ir(ModuloClinico.ChaveAtendimento);
    }

    /// <summary>Mesma coisa a partir da lista de pendências dos dias anteriores.</summary>
    [RelayCommand]
    private void EscreverPendente(LinhaRegistroPendente? linha)
    {
        if (linha is null) return;

        _foco.Definir(linha.PacienteId, linha.Paciente, linha.AgendamentoId);
        NavegacaoSuite.Ir(ModuloClinico.ChaveAtendimento);
    }

    /// <summary>Abre a curva de dor do paciente da linha, sem passar pelo atendimento.</summary>
    [RelayCommand]
    private void VerDor(LinhaSessao? linha)
    {
        if (linha is null) return;

        _foco.Definir(linha.PacienteId, linha.Paciente);
        NavegacaoSuite.Ir(ModuloClinico.ChaveEvolucaoDor);
    }
}
