using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>
/// Formulário de novo agendamento, usado pela janela <c>Alertas.AgendamentoWindow</c>.
/// Nasce com data e hora já preenchidas quando a secretária clica numa faixa livre da
/// agenda — o caminho normal é escolher o paciente e confirmar.
/// </summary>
public partial class AgendamentoEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Controls.IDialogoService _dialogo;

    /// <summary>Busca de paciente compartilhada com Pacientes e Novo atendimento.</summary>
    public SeletorPacienteViewModel Seletor { get; }

    public ObservableCollection<EntradaModalidade> Modalidades { get; } = new();
    public ObservableCollection<EntradaEspecialidade> Especialidades { get; } = new();

    /// <summary>
    /// Quem vai atender — a mesma pergunta do formulário da Recepção, e desde a parcela 95
    /// ela é OBRIGATÓRIA na marcação: <c>AgendaService.AgendarAsync</c> recusa horário sem
    /// dono, porque o horário existe para cair na agenda de um profissional.
    ///
    /// ⚠️ O campo NASCEU aqui nesta parcela, e não é enfeite: este formulário nunca passou
    /// <c>profissionalId</c> — marcava por esta janela e o horário sumia do "Meu dia" de
    /// quem ia atender, além de ficar fora do repasse. Sem o campo, a recusa nova
    /// simplesmente fecharia a agenda deste app; é a cópia que fica para trás quando uma
    /// regra sobe para o serviço compartilhado.
    /// </summary>
    public ObservableCollection<Profissional> Profissionais { get; } = new();

    [ObservableProperty] private DateTime _data = DateTime.Today;
    [ObservableProperty] private string _hora = "09:00";
    [ObservableProperty] private EntradaModalidade? _modalidadeSelecionada;
    [ObservableProperty] private EntradaEspecialidade? _especialidadeSelecionada;
    [ObservableProperty] private Profissional? _profissional;
    [ObservableProperty] private string? _observacoes;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _ocupado;

    /// <summary>Aviso de contexto do paciente escolhido (carteirinha vencida etc.).</summary>
    [ObservableProperty] private string? _avisoPaciente;

    /// <summary>
    /// Consulta renovável vencida ou a vencer na data que está sendo marcada. Separado do
    /// aviso acima porque os dois chegam juntos e são conversas diferentes: a carteirinha
    /// se resolve com o convênio, a consulta se resolve aqui.
    /// </summary>
    [ObservableProperty] private string? _avisoConsulta;

    /// <summary>A consulta já venceu (pinta o aviso de vermelho em vez de laranja).</summary>
    [ObservableProperty] private bool _consultaVencida;

    /// <summary>Id do agendamento em edição. Null = agendamento novo.</summary>
    [ObservableProperty] private int? _editandoId;

    /// <summary>Em edição o paciente não muda: remarcar é mover o horário, não trocar de pessoa.</summary>
    public bool Remarcando => EditandoId is not null;

    public string Titulo => Remarcando ? "Remarcar agendamento" : "Novo agendamento";
    public string RotuloConfirmar => Remarcando ? "Salvar alterações" : "Agendar";

    partial void OnEditandoIdChanged(int? value)
    {
        OnPropertyChanged(nameof(Remarcando));
        OnPropertyChanged(nameof(Titulo));
        OnPropertyChanged(nameof(RotuloConfirmar));
        Seletor.Travado = Remarcando;
    }

    /// <summary>Comportamento (base) da modalidade selecionada.</summary>
    private ModalidadeAtendimento Modalidade =>
        ModalidadeSelecionada?.Base ?? ModalidadeAtendimento.AcupunturaComEletro;

    /// <summary>Consulta avulsa: pede a especialidade (levada ao atendimento na confirmação).</summary>
    public bool ModalidadeConsulta => Modalidade == ModalidadeAtendimento.Consulta;

    partial void OnModalidadeSelecionadaChanged(EntradaModalidade? value)
    {
        if (Modalidade != ModalidadeAtendimento.Consulta)
            EspecialidadeSelecionada = null;
        OnPropertyChanged(nameof(ModalidadeConsulta));
    }

    // A consulta é conferida contra a data marcada: mudar a data muda a resposta.
    partial void OnDataChanged(DateTime value)
    {
        if (Seletor.Selecionado is { } paciente) _ = ConferirConsultaAsync(paciente.Id);
    }

    // Pré-preenche a modalidade com a habitual do paciente e avisa o que atrapalha a guia.
    private void AoTrocarPaciente(Paciente? value)
    {
        AvisoConsulta = null;
        ConsultaVencida = false;

        if (value is null)
        {
            AvisoPaciente = null;
            return;
        }

        ModalidadeSelecionada = Modalidades.FirstOrDefault(m => m.Codigo == value.ModalidadePreferidaCodigo)
            ?? Modalidades.FirstOrDefault(m => m.Base == value.ModalidadePreferida)
            ?? ModalidadeSelecionada;

        AvisoPaciente = value.CarteirinhaVencida
            ? $"Carteirinha vencida em {value.ValidadeCarteirinha:dd/MM/yyyy} — renove antes do atendimento, senão a guia é recusada."
            : null;

        _ = ConferirConsultaAsync(value.Id);
    }

    /// <summary>
    /// Consulta renovável do paciente NA DATA QUE ESTÁ SENDO MARCADA — não hoje. Marcar
    /// para daqui a três semanas com uma consulta que vence em cinco dias é combinar a
    /// renovação de antemão; conferir contra hoje diria que está tudo certo.
    ///
    /// Falha não impede marcar, mas não passa em branco: sem o aviso a tela estaria
    /// dizendo "não há nada a renovar" sobre o que não conseguiu conferir.
    /// </summary>
    private async Task ConferirConsultaAsync(int pacienteId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<ConsultaService>();
            var situacao = await servico.DoPacienteAsync(pacienteId, DateOnly.FromDateTime(Data));

            // A seleção pode ter mudado enquanto a consulta rodava.
            if (Seletor.Selecionado?.Id != pacienteId) return;

            AvisoConsulta = situacao?.AvisoRenovacao;
            ConsultaVencida = situacao?.Vencida ?? false;
        }
        catch (Exception ex)
        {
            Configuracao.LogErros.Registrar("Agendamento — consulta a renovar não pôde ser conferida", ex);
            AvisoConsulta = "Não foi possível conferir a consulta renovável deste paciente.";
            ConsultaVencida = false;
        }
    }

    /// <summary>Disparado quando o agendamento é criado (a janela fecha).</summary>
    public event Action? Agendou;

    public AgendamentoEdicaoViewModel(IServiceScopeFactory scopeFactory, Controls.IDialogoService dialogo)
    {
        _scopeFactory = scopeFactory;
        _dialogo = dialogo;
        Seletor = new SeletorPacienteViewModel(scopeFactory);
        Seletor.SelecaoMudou += AoTrocarPaciente;
    }

    /// <summary>
    /// Prepara o formulário. <paramref name="inicio"/> vem da faixa clicada na agenda;
    /// <paramref name="agendamentoId"/> abre em modo de remarcação.
    /// </summary>
    public async Task CarregarAsync(DateTime? inicio, int? agendamentoId = null)
    {
        Modalidades.Clear();
        foreach (var m in CatalogoModalidades.Ativas)
            Modalidades.Add(m);
        ModalidadeSelecionada = Modalidades.FirstOrDefault(m => m.Base == ModalidadeAtendimento.AcupunturaComEletro)
            ?? Modalidades.FirstOrDefault();

        Especialidades.Clear();
        foreach (var e in CatalogoEspecialidades.Ativas)
            Especialidades.Add(e);

        // A lista de quem atende. Carregada em lista LOCAL e publicada de uma vez: entre o
        // `Clear()` e o último `Add` não pode haver `await` (parcela 62).
        try
        {
            using var escopoEquipe = _scopeFactory.CreateScope();
            var equipe = escopoEquipe.ServiceProvider.GetRequiredService<EquipeService>();
            var ativos = await equipe.ProfissionaisAtivosAsync();

            Profissionais.Clear();
            foreach (var p in ativos) Profissionais.Add(p);
        }
        catch (Exception ex)
        {
            // Degradação com rastro: sem a lista o formulário não deixa marcar (o serviço
            // recusa), e a frase diz isso em vez de o Agendar falhar sem explicação.
            Clinica.Application.Diagnostico.Registrar(
                "Faturamento — lista de profissionais não pôde ser carregada", ex);
            Mensagem = "Não foi possível carregar a lista de profissionais, "
                       + "e sem escolher quem vai atender o horário não pode ser marcado.";
        }

        if (inicio is { } quando)
        {
            Data = quando.Date;
            Hora = quando.ToString("HH:mm");
        }

        await Seletor.BuscarAsync(imediato: true);

        if (agendamentoId is not int id) return;

        // Remarcação: traz o agendamento e trava o paciente (mover horário ≠ trocar de pessoa).
        using var scope = _scopeFactory.CreateScope();
        var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
        var ag = await agenda.ObterAsync(id);
        if (ag is null)
        {
            Mensagem = "Agendamento não encontrado.";
            return;
        }

        EditandoId = ag.Id;

        // O paciente entra ANTES da modalidade: escolher paciente pré-preenche a modalidade
        // habitual dele, e aqui quem manda é o que já estava marcado no agendamento.
        var paciente = ag.Paciente ?? Seletor.Resultados.FirstOrDefault(p => p.Id == ag.PacienteId);
        if (paciente is not null)
            Seletor.SelecionarGarantindoNaLista(paciente);

        Data = ag.DataHora.Date;
        Hora = ag.DataHora.ToString("HH:mm");
        Observacoes = ag.Observacoes;
        ModalidadeSelecionada = Modalidades.FirstOrDefault(m => m.Codigo == ag.ModalidadeCodigo)
            ?? Modalidades.FirstOrDefault(m => m.Base == ag.ModalidadePrevista)
            ?? ModalidadeSelecionada;
        EspecialidadeSelecionada = Especialidades.FirstOrDefault(e => e.Codigo == ag.EspecialidadeConsultaCodigo);
        // Horário antigo pode não ter dono (marcado antes de o campo existir): aí o combo
        // fica em branco, que é a verdade — e `RemarcarAsync` preserva o que está gravado.
        Profissional = Profissionais.FirstOrDefault(p => p.Id == ag.ProfissionalId);
    }

    [RelayCommand]
    private async Task Agendar()
    {
        // A SEGUNDA barreira (parcela 51: "só se chega por ali" não é barreira). A porta
        // (`AgendaViewModel.AbrirCadastroAsync`) exige o bit da agenda; esta janela não
        // conferia nada — e é a cópia do faturamento da janela que a suíte já guardava,
        // o formato exato da parcela 60 ("a cópia que ficou para trás é onde a permissão
        // vaza").
        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "marcar ou remarcar horário");

        if (Seletor.Selecionado is not { } paciente)
        {
            Mensagem = "Selecione o paciente.";
            return;
        }
        if (!TimeOnly.TryParse(Hora, out var hora))
        {
            Mensagem = "Hora inválida (use HH:mm, ex.: 14:30).";
            return;
        }
        if (ModalidadeSelecionada is null)
        {
            Mensagem = "Selecione a modalidade.";
            return;
        }
        if (ModalidadeConsulta && EspecialidadeSelecionada is null)
        {
            Mensagem = "Informe a especialidade da consulta.";
            return;
        }
        // A metade VISÍVEL da recusa que mora no serviço (parcela 41: o botão explica, a
        // guarda impede). Só na criação: remarcar não escolhe dono, e horário antigo pode
        // não ter um — cobrar aqui travaria a remarcação de tudo o que foi marcado antes.
        if (EditandoId is null && Profissional is null)
        {
            Mensagem = "Escolha quem vai atender — sem isso o horário não aparece na agenda "
                       + "do profissional nem entra no repasse.";
            return;
        }

        // Hora de parede (sem fuso) para casar com a coluna 'timestamp without time zone'.
        var dataHora = DateTime.SpecifyKind(Data.Date.Add(hora.ToTimeSpan()), DateTimeKind.Unspecified);

        if (Ocupado) return;
        Ocupado = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();

            // Com a chave "guia no agendamento" ligada, CRIAR horário também cria o
            // atendimento e as guias — o ato que `LancarAtendimento` nomeia (parcela 70;
            // a mesma guarda do formulário da suíte). `Exigir` com bits somados é um E;
            // remarcar não cria atendimento e fica fora.
            if (EditandoId is null)
            {
                var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();
                if (await parametros.GuiaNoAgendamentoAsync())
                    SessaoUsuario.Atual.Exigir(
                        Permissao.EditarAgenda | Permissao.LancarAtendimento,
                        "marcar atendimento gerando as guias");
            }

            // A PERGUNTA de duplicidade, informada pela capa (parcela 70): o mesmo ponto
            // único das portas da suíte (`CapasDoDiaAsync`). Sem ela, esta era a única
            // porta de criação em que marcar duas vezes o mesmo paciente no mesmo dia
            // gerava dois jogos de guias sem uma palavra — a cópia que ficou para trás,
            // no app que fatura. É PERGUNTA e não recusa: sessão de manhã e consulta à
            // tarde são legítimas, e recusar travaria o balcão sem saída.
            if (EditandoId is null)
            {
                var atendimentos = scope.ServiceProvider.GetRequiredService<AtendimentoService>();
                var capas = await atendimentos.CapasDoDiaAsync(paciente.Id, DateOnly.FromDateTime(Data));
                if (capas.Count > 0 &&
                    !_dialogo.ConfirmarPerigo("Paciente já tem atendimento neste dia",
                        $"{paciente.Nome} já tem em {Data:dd/MM}: "
                        + string.Join(" · ", capas.Select(c =>
                            $"nº {c.Numero} ({c.Modalidade}, {c.Lancamento} — {c.ResumoGuias})"))
                        + "\n\nMarcar de novo cria OUTRO atendimento e outro jogo de guias. Continuar?"))
                    return;
            }

            // Choque de horário: avisa quem já ocupa o slot e pede confirmação.
            // Numa remarcação, o próprio agendamento não conflita consigo mesmo.
            var conflito = await agenda.ConflitoAsync(dataHora, ignorarAgendamentoId: EditandoId);
            if (conflito is not null &&
                !_dialogo.Confirmar("Horário ocupado",
                    $"{conflito.Paciente?.Nome} já está agendado em {dataHora:dd/MM/yyyy} às {dataHora:HH:mm}.\n" +
                    "Agendar mesmo assim (encaixe)?"))
                return;

            if (EditandoId is int id)
                await agenda.RemarcarAsync(id, dataHora, Observacoes,
                    modalidadeCodigo: ModalidadeSelecionada.Codigo,
                    especialidadeConsultaCodigo: ModalidadeConsulta ? EspecialidadeSelecionada?.Codigo : null,
                    operador: SessaoUsuario.Atual.Operador);
            else
                await agenda.AgendarAsync(paciente.Id, dataHora, Modalidade, Observacoes,
                    modalidadeCodigo: ModalidadeSelecionada.Codigo,
                    especialidadeConsultaCodigo: ModalidadeConsulta ? EspecialidadeSelecionada?.Codigo : null,
                    profissionalId: Profissional?.Id,
                    operador: SessaoUsuario.Atual.Operador);
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível agendar: {ex.Message}";
            return;
        }
        finally
        {
            Ocupado = false;
        }

        Agendou?.Invoke();
    }
}
