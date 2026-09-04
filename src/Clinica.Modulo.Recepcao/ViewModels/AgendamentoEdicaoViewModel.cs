using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Desktop.Shell;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>
/// Formulário de um horário da agenda da Recepção: paciente, quando, com quem, onde e
/// por quanto tempo.
///
/// A diferença para o formulário do faturamento é o "com quem/onde": profissional e
/// sala são recursos disputados, então o serviço RECUSA o choque — a não ser que a
/// recepção marque <see cref="Encaixe"/>, que é a clínica decidindo atender por cima.
/// O choque aparece na tela ANTES de salvar, para a decisão ser informada.
/// </summary>
public sealed partial class AgendamentoEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;

    /// <summary>Preenchido na remarcação; nulo num horário novo.</summary>
    private readonly int? _agendamentoId;

    public SeletorPacienteViewModel Seletor { get; }

    public ObservableCollection<EntradaModalidade> Modalidades { get; } = [];
    public ObservableCollection<EntradaEspecialidade> Especialidades { get; } = [];
    public ObservableCollection<Profissional> Profissionais { get; } = [];
    public ObservableCollection<Sala> Salas { get; } = [];

    /// <summary>Choques detectados para o horário atual (vazio = livre).</summary>
    public ObservableCollection<string> Conflitos { get; } = [];

    /// <summary>
    /// Carteirinha, cota e consentimento do paciente escolhido — conferidos AQUI, na hora
    /// de marcar, e não só na ficha (que ninguém abre no balcão).
    ///
    /// É aviso, nunca impedimento: quem decide atender é a clínica. Mas marcar dez
    /// sessões para quem está com a cota estourada é combinar dez glosas de antemão, e
    /// isso precisava ser dito antes do clique — não na hora de faturar.
    /// </summary>
    public ObservableCollection<string> Elegibilidade { get; } = [];

    public bool TemAvisoElegibilidade => Elegibilidade.Count > 0;

    [ObservableProperty] private DateTime _data = DateTime.Today;
    [ObservableProperty] private string _hora = "09:00";
    [ObservableProperty] private EntradaModalidade? _modalidadeSelecionada;
    [ObservableProperty] private EntradaEspecialidade? _especialidadeSelecionada;
    [ObservableProperty] private Profissional? _profissional;
    [ObservableProperty] private Sala? _sala;
    [ObservableProperty] private string _duracao = string.Empty;
    [ObservableProperty] private bool _encaixe;

    // ---- Série (o pacote de dez marcado de uma vez) ----

    /// <summary>
    /// Marcar várias sessões de uma vez. Só na CRIAÇÃO: remarcar já é sobre um horário
    /// que existe, e repetir ali criaria sessões novas achando que está movendo uma.
    /// </summary>
    [ObservableProperty] private bool _emSerie;

    [ObservableProperty] private string _quantidadeSessoes = "10";

    /// <summary>Intervalo em dias. 7 = mesma hora, toda semana — o caso comum.</summary>
    [ObservableProperty] private string _intervaloDias = "7";

    /// <summary>O que a série marcou e o que ela pulou, escrito para a tela.</summary>
    public ObservableCollection<string> ResultadoSerie { get; } = [];

    public bool TemResultadoSerie => ResultadoSerie.Count > 0;

    /// <summary>Repetir só faz sentido em horário novo — não em remarcação.</summary>
    public bool PodeMarcarEmSerie => _agendamentoId is null && PedidoListaEsperaId is null;
    [ObservableProperty] private string? _observacoes;

    [ObservableProperty] private string _titulo = "Novo horário";
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _salvando;

    /// <summary>Pedido da lista de espera que originou este horário (fecha ao salvar).</summary>
    public int? PedidoListaEsperaId { get; set; }

    /// <summary>
    /// Profissional já escolhido por quem abriu o formulário — o clique num vão da coluna
    /// dele na grade.
    ///
    /// É um ID e não a entidade porque a lista de profissionais é carregada do banco
    /// DEPOIS do construtor: atribuir <see cref="Profissional"/> de fora nunca acharia o
    /// item, e o combo abriria em branco justamente na coluna que a pessoa apontou.
    /// </summary>
    public int? ProfissionalPreferidoId { get; set; }

    /// <summary>
    /// A sala da coluna clicada, na visão por SALA (parcela 63) — o par do de cima, e
    /// pela mesma razão: quem clicou no vão da Sala 2 às 14h não deve ter de escolher a
    /// Sala 2 num combo logo em seguida.
    /// </summary>
    public int? SalaPreferidaId { get; set; }

    /// <summary>A janela fecha quando isto dispara — o comando de salvar segue assíncrono.</summary>
    public event Action? Concluido;

    public bool TemConflito => Conflitos.Count > 0;

    public bool ModalidadeConsulta
        => (ModalidadeSelecionada?.Base ?? ModalidadeAtendimento.AcupunturaComEletro)
           == ModalidadeAtendimento.Consulta;

    public AgendamentoEdicaoViewModel(IServiceScopeFactory escopos, int? agendamentoId = null)
    {
        _escopos = escopos;
        _agendamentoId = agendamentoId;
        Seletor = new SeletorPacienteViewModel(escopos);
        // Trocar de paciente muda o aviso de choque com a própria agenda dele.
        Seletor.SelecaoMudou += AoTrocarPaciente;

        _ = CarregarAsync();
    }

    partial void OnModalidadeSelecionadaChanged(EntradaModalidade? value)
    {
        if (!ModalidadeConsulta) EspecialidadeSelecionada = null;
        OnPropertyChanged(nameof(ModalidadeConsulta));
    }

    private void AoTrocarPaciente(Paciente? paciente)
    {
        _ = ConferirConflitosAsync();
        _ = ConferirElegibilidadeAsync();
        _ = ConferirJaLancadoAsync();
    }

    /// <summary>
    /// A CAPA do que o paciente já tem no dia escolhido (parcela 70) — o mesmo ponto
    /// único do Novo atendimento (<see cref="AtendimentoService.CapasDoDiaAsync"/>).
    /// Este formulário continua sendo porta de criação (lista de espera, e o fallback de
    /// quem não alcança o Novo atendimento), e alerta que existe numa porta só é o
    /// defeito recorrente do projeto.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAvisoJaLancado))]
    private string? _avisoJaLancado;

    public bool TemAvisoJaLancado => !string.IsNullOrWhiteSpace(AvisoJaLancado);

    private int _geracaoJaLancado;

    private async Task ConferirJaLancadoAsync()
    {
        var geracao = ++_geracaoJaLancado;
        AvisoJaLancado = null;

        // Na remarcação o atendimento do próprio horário apareceria como "duplicata".
        if (_agendamentoId is not null) return;
        if (Seletor.Selecionado is not { } paciente) return;

        try
        {
            using var scope = _escopos.CreateScope();
            var atendimentos = scope.ServiceProvider.GetRequiredService<AtendimentoService>();
            var capas = await atendimentos.CapasDoDiaAsync(paciente.Id, DateOnly.FromDateTime(Data));

            if (geracao != _geracaoJaLancado) return;

            AvisoJaLancado = capas.Count == 0
                ? null
                : $"Já lançado em {Data:dd/MM}: "
                  + string.Join(" · ", capas.Select(c =>
                      $"nº {c.Numero} ({c.Modalidade}, {c.Lancamento} — {c.ResumoGuias})"))
                  + ". Marcar de novo cria OUTRO atendimento e outro jogo de guias.";
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoJaLancado) return;
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — atendimentos do dia do paciente não puderam ser lidos ao marcar", ex);
        }
    }

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): a conferência dispara a cada
    /// TECLA da hora/duração e a cada troca de combo — num banco remoto, a resposta de
    /// "14:0" chegando depois da de "14:30" apagaria o choque REAL (ou inventaria um que
    /// não existe), e a recepção marcaria em cima. A cópia do faturamento já tinha guarda
    /// equivalente; esta não tinha nenhuma. Quem começou primeiro perde.
    /// </summary>
    private int _geracaoConflitos;
    private int _geracaoElegibilidade;

    /// <summary>
    /// Conferência da elegibilidade do paciente escolhido, na data escolhida.
    ///
    /// Falha aqui não impede marcar — mas também não passa em branco: sem o aviso, a
    /// tela estaria dizendo "está tudo certo" sobre algo que não conseguiu conferir.
    /// </summary>
    private async Task ConferirElegibilidadeAsync()
    {
        var geracao = ++_geracaoElegibilidade;

        Elegibilidade.Clear();
        OnPropertyChanged(nameof(TemAvisoElegibilidade));

        if (Seletor.Selecionado is not { } paciente) return;

        try
        {
            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<ElegibilidadeService>();

            var resultado = await servico.ConferirAsync(paciente.Id, DateOnly.FromDateTime(Data));

            // Chegou tarde: a tela já está em outro paciente ou outra data.
            if (geracao != _geracaoElegibilidade) return;

            foreach (var alerta in resultado.Alertas) Elegibilidade.Add(alerta.Descricao);
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoElegibilidade) return;

            Clinica.Application.Diagnostico.Registrar(
                "Recepção — elegibilidade não pôde ser conferida ao marcar", ex);
            Elegibilidade.Add("Não foi possível conferir carteirinha e cota agora.");
        }
        finally
        {
            if (geracao == _geracaoElegibilidade)
                OnPropertyChanged(nameof(TemAvisoElegibilidade));
        }
    }

    partial void OnDataChanged(DateTime value)
    {
        _ = ConferirConflitosAsync();
        _ = ConferirJaLancadoAsync();
    }
    partial void OnHoraChanged(string value) => _ = ConferirConflitosAsync();
    partial void OnDuracaoChanged(string value) => _ = ConferirConflitosAsync();
    partial void OnProfissionalChanged(Profissional? value) => _ = ConferirConflitosAsync();
    partial void OnSalaChanged(Sala? value) => _ = ConferirConflitosAsync();

    private async Task CarregarAsync()
    {
        try
        {
            Modalidades.Clear();
            foreach (var m in CatalogoModalidades.Ativas) Modalidades.Add(m);
            ModalidadeSelecionada =
                Modalidades.FirstOrDefault(m => m.Base == ModalidadeAtendimento.AcupunturaComEletro)
                ?? Modalidades.FirstOrDefault();

            Especialidades.Clear();
            foreach (var e in CatalogoEspecialidades.Ativas) Especialidades.Add(e);

            using var scope = _escopos.CreateScope();
            var equipe = scope.ServiceProvider.GetRequiredService<EquipeService>();

            Profissionais.Clear();
            foreach (var p in await equipe.ProfissionaisAtivosAsync()) Profissionais.Add(p);

            if (ProfissionalPreferidoId is { } preferido)
                Profissional = Profissionais.FirstOrDefault(p => p.Id == preferido);

            Salas.Clear();
            foreach (var s in await equipe.SalasAtivasAsync()) Salas.Add(s);

            if (SalaPreferidaId is { } salaPreferida)
                Sala = Salas.FirstOrDefault(s => s.Id == salaPreferida);

            await Seletor.BuscarAsync(imediato: true);

            if (_agendamentoId is not null) await CarregarExistenteAsync(scope, _agendamentoId.Value);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — formulário de agendamento não pôde ser preparado", ex);
            Mensagem = $"Não foi possível carregar o formulário: {ex.Message}";
            MensagemEhErro = true;
        }
    }

    private async Task CarregarExistenteAsync(IServiceScope scope, int id)
    {
        var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
        var ag = await agenda.ObterAsync(id);
        if (ag is null) return;

        Titulo = "Remarcar horário";
        Data = ag.DataHora.Date;
        Hora = ag.DataHora.ToString("HH:mm");
        ModalidadeSelecionada = Modalidades.FirstOrDefault(m => m.Codigo == ag.ModalidadeCodigo)
                                ?? Modalidades.FirstOrDefault(m => m.Base == ag.ModalidadePrevista)
                                ?? ModalidadeSelecionada;
        EspecialidadeSelecionada =
            Especialidades.FirstOrDefault(e => e.Codigo == ag.EspecialidadeConsultaCodigo);
        Profissional = Profissionais.FirstOrDefault(p => p.Id == ag.ProfissionalId);
        Sala = Salas.FirstOrDefault(s => s.Id == ag.SalaId);
        Duracao = ag.DuracaoMinutos?.ToString() ?? string.Empty;
        Encaixe = ag.Encaixe;
        Observacoes = ag.Observacoes;

        if (ag.Paciente is not null)
        {
            Seletor.SelecionarGarantindoNaLista(ag.Paciente);
            // Remarcar move o horário; não troca de pessoa.
            Seletor.Travado = true;
        }

        await ConferirConflitosAsync();
    }

    /// <summary>
    /// Mostra o choque antes de salvar. Silencioso quanto a falhas: é um aviso, e uma
    /// consulta que não respondeu não pode impedir a recepção de marcar.
    /// </summary>
    private async Task ConferirConflitosAsync()
    {
        var geracao = ++_geracaoConflitos;

        Conflitos.Clear();
        OnPropertyChanged(nameof(TemConflito));

        if (!TentarMontarDataHora(out var dataHora)) return;

        try
        {
            using var scope = _escopos.CreateScope();
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();

            var achados = await agenda.ConflitosAsync(
                dataHora, DuracaoInformada(), Profissional?.Id, Sala?.Id,
                pacienteId: Seletor.Selecionado?.Id,
                ignorarAgendamentoId: _agendamentoId);

            // Chegou tarde: outra tecla já pediu uma conferência mais nova.
            if (geracao != _geracaoConflitos) return;

            foreach (var c in achados.Select(Descrever).Distinct())
                Conflitos.Add(c);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — conflitos da agenda não puderam ser conferidos", ex);
        }
        finally
        {
            if (geracao == _geracaoConflitos)
                OnPropertyChanged(nameof(TemConflito));
        }
    }

    private static string Descrever(ConflitoAgenda c) => c.Recurso switch
    {
        RecursoAgenda.Paciente => $"{c.Descricao} (aviso — não impede marcar)",
        _ => c.Descricao
    };

    [RelayCommand]
    private async Task SalvarAsync()
    {
        // A SEGUNDA barreira (parcela 51: "só se chega por ali" não é barreira). As
        // portas que abrem esta janela exigem `EditarAgenda`; a janela confere de novo,
        // porque atalho e corrida de carregamento passam pela porta sem o clique dela.
        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "mexer na agenda");

        Mensagem = string.Empty;
        MensagemEhErro = false;

        var paciente = Seletor.Selecionado;
        if (paciente is null)
        {
            Erro("Escolha o paciente.");
            return;
        }

        if (ModalidadeSelecionada is null)
        {
            Erro("Escolha a modalidade.");
            return;
        }

        if (ModalidadeConsulta && EspecialidadeSelecionada is null)
        {
            Erro("Consulta precisa de especialidade.");
            return;
        }

        if (!TentarMontarDataHora(out var dataHora))
        {
            Erro("Horário inválido. Use HH:mm (ex.: 14:30).");
            return;
        }

        if (!string.IsNullOrWhiteSpace(Duracao)
            && (!int.TryParse(Duracao, out var minutos) || minutos <= 0))
        {
            Erro("A duração precisa ser um número de minutos maior que zero.");
            return;
        }

        // A metade VISÍVEL da recusa do `AgendaService` (parcela 95): horário MARCADO
        // precisa de dono, porque é dele que a agenda do médico é feita. O encaixe fica
        // de fora — é o paciente já no balcão —, e a remarcação também: mover um horário
        // não escolhe quem atende, e os antigos podem não ter ninguém.
        if (!Encaixe && _agendamentoId is null && Profissional is null)
        {
            Erro("Escolha quem vai atender: sem profissional o horário não aparece na "
                 + "agenda de ninguém e fica fora do repasse.");
            return;
        }

        try
        {
            Salvando = true;
            using var scope = _escopos.CreateScope();
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();

            // Com a chave "guia no agendamento" ligada, CRIAR horário também cria o
            // atendimento e as guias — o ato que `LancarAtendimento` nomeia (parcela 69:
            // quando o momento do fato muda, a permissão vai junto). `Exigir` com bits
            // somados é um E; remarcar não cria atendimento e fica fora.
            if (_agendamentoId is null)
            {
                var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();
                if (await parametros.GuiaNoAgendamentoAsync())
                {
                    SessaoUsuario.Atual.Exigir(
                        Permissao.EditarAgenda | Permissao.LancarAtendimento,
                        "marcar atendimento gerando as guias");

                    // ===== SEM CONVÊNIO NÃO NASCE ATENDIMENTO (parcela 92) =====
                    //
                    // Só DENTRO da chave, e a condição é a mesma do `Exigir` logo acima:
                    // com ela ligada, marcar CRIA o atendimento e as guias — e é isso que
                    // `AtendimentoService.MontarAsync` recusa. Com ela desligada, marcar
                    // não cria atendimento nenhum, e exigir o convênio aqui travaria quem
                    // marca retorno por telefone numa resposta que não tem em mãos.
                    if (!VinculoDeConvenio.Garantir(
                            _escopos, paciente, SessaoUsuario.Atual.Operador))
                    {
                        Erro($"{paciente.Nome} está sem convênio, e com \"guia no agendamento\" "
                             + "ligada marcar já gera as guias. Escolha o convênio do paciente "
                             + "para marcar — quem paga do bolso entra no convênio particular.");
                        return;
                    }
                }
            }

            if (_agendamentoId is not null)
            {
                await agenda.RemarcarAsync(
                    _agendamentoId.Value, dataHora, Observacoes,
                    modalidadeCodigo: ModalidadeSelecionada.Codigo,
                    especialidadeConsultaCodigo: ModalidadeConsulta ? EspecialidadeSelecionada?.Codigo : null,
                    operador: SessaoUsuario.Atual.Operador,
                    profissionalId: Profissional?.Id, salaId: Sala?.Id,
                    duracaoMinutos: DuracaoInformada(),
                    manterRecursos: false, encaixe: Encaixe);
            }
            else if (PedidoListaEsperaId is { } pedidoId)
            {
                // Chamado da lista: o serviço da lista cria o horário e fecha o pedido
                // no mesmo caminho, para o pedido nunca ficar aberto com hora marcada.
                var espera = scope.ServiceProvider.GetRequiredService<ListaEsperaService>();
                await espera.ChamarAsync(
                    pedidoId, dataHora, ModalidadeSelecionada.Base,
                    profissionalId: Profissional?.Id, salaId: Sala?.Id,
                    duracaoMinutos: DuracaoInformada(), encaixe: Encaixe,
                    modalidadeCodigo: ModalidadeSelecionada.Codigo,
                    operador: SessaoUsuario.Atual.Operador,
                    // A mesma que o formulário EXIGE logo acima ("Consulta precisa de
                    // especialidade") — e que este caminho descartava em silêncio.
                    especialidadeConsultaCodigo: ModalidadeConsulta ? EspecialidadeSelecionada?.Codigo : null);
            }
            else if (EmSerie && PodeMarcarEmSerie)
            {
                if (!int.TryParse(QuantidadeSessoes, out var quantas) || quantas < 2)
                {
                    Erro("Quantas sessões? A partir de 2 — para uma só, desmarque \"repetir\".");
                    return;
                }
                if (!int.TryParse(IntervaloDias, out var intervalo) || intervalo < 1)
                {
                    Erro("De quantos em quantos dias? (7 = toda semana, mesma hora.)");
                    return;
                }

                var serie = await agenda.AgendarSerieAsync(
                    paciente.Id, dataHora, ModalidadeSelecionada.Base, quantas,
                    intervaloDias: intervalo, observacoes: Observacoes,
                    modalidadeCodigo: ModalidadeSelecionada.Codigo,
                    especialidadeConsultaCodigo: ModalidadeConsulta ? EspecialidadeSelecionada?.Codigo : null,
                    profissionalId: Profissional?.Id, salaId: Sala?.Id,
                    duracaoMinutos: DuracaoInformada(),
                    operador: SessaoUsuario.Atual.Operador);

                // A série que pulou datas NÃO fecha a janela em silêncio: a recepção
                // precisa ver quais não entraram para resolver agora, com o paciente
                // ainda na frente dela.
                if (!serie.TudoMarcado)
                {
                    ResultadoSerie.Clear();
                    ResultadoSerie.Add($"{serie.Marcados.Count} sessão(ões) marcada(s).");
                    foreach (var r in serie.Recusados)
                        ResultadoSerie.Add($"{r.Quando:dd/MM HH:mm} — não deu: {r.Motivo}");

                    OnPropertyChanged(nameof(TemResultadoSerie));
                    Mensagem = "Parte da série não entrou. Veja abaixo e resolva as datas que faltam.";
                    MensagemEhErro = true;
                    return;
                }
            }
            else
            {
                await agenda.AgendarAsync(
                    paciente.Id, dataHora, ModalidadeSelecionada.Base, Observacoes,
                    modalidadeCodigo: ModalidadeSelecionada.Codigo,
                    especialidadeConsultaCodigo: ModalidadeConsulta ? EspecialidadeSelecionada?.Codigo : null,
                    profissionalId: Profissional?.Id, salaId: Sala?.Id,
                    duracaoMinutos: DuracaoInformada(), encaixe: Encaixe,
                    operador: SessaoUsuario.Atual.Operador);
            }

            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — horário não pôde ser salvo", ex);
            Erro(ex.Message);
        }
        finally
        {
            Salvando = false;
        }
    }

    /// <summary>Assume o choque: marca por cima com o encaixe registrado.</summary>
    [RelayCommand]
    private async Task AssumirEncaixeAsync()
    {
        Encaixe = true;
        await SalvarAsync();
    }

    private int? DuracaoInformada()
        => int.TryParse(Duracao, out var m) && m > 0 ? m : null;

    private bool TentarMontarDataHora(out DateTime dataHora)
    {
        dataHora = default;
        if (!TimeOnly.TryParse(Hora, out var hora)) return false;
        dataHora = Data.Date.Add(hora.ToTimeSpan());
        return true;
    }

    private void Erro(string texto)
    {
        Mensagem = texto;
        MensagemEhErro = true;
    }
}
