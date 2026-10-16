using System.Collections.ObjectModel;
using System.Windows.Threading;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Recepcao.Modulo;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>Guia pendente de um paciente que vem hoje (a lista do contador "Guias do 2º código").</summary>
public sealed class LinhaPendenciaRecepcao
{
    public required int PacienteId { get; init; }
    public required string Paciente { get; init; }
    public required string Descricao { get; init; }
    public required string Atraso { get; init; }
    public string? Telefone { get; init; }
    public bool TemTelefone => !string.IsNullOrWhiteSpace(Telefone);
}

/// <summary>Um paciente que faz aniversário, com o contato para a clínica ligar.</summary>
public sealed class LinhaAniversariante
{
    public required int PacienteId { get; init; }
    public required string Paciente { get; init; }
    public required string Detalhe { get; init; }
    public string? Telefone { get; init; }

    /// <summary>Vem hoje: o parabéns é dado no balcão e não custa nem a ligação.</summary>
    public required bool VemHoje { get; init; }

    public bool TemTelefone => !string.IsNullOrWhiteSpace(Telefone);
}

/// <summary>Um contador de "O que resolver no balcão", com o destino do clique.</summary>
public sealed class ItemBalcao
{
    public required string Chave { get; init; }
    public required string Titulo { get; init; }
    public required string Valor { get; init; }
    public required string Detalhe { get; init; }

    /// <summary>Nome do ícone (o mesmo dicionário da navegação) e o tom do quadradinho.</summary>
    public required string Icone { get; init; }
    public required string Tom { get; init; }

    /// <summary>A chave de navegação; nula quando o clique abre a lista NA PRÓPRIA tela.</summary>
    public string? Destino { get; init; }
}

/// <summary>
/// O INÍCIO da Recepção (out/2026, mockup aprovado "1 · Início").
///
/// Era um painel de oito números e três listas — respondia "como está o dia" em números,
/// e o balcão tinha de ir a outra tela para AGIR. Virou três respostas: quantos (quatro
/// cartões), QUEM vem agora com o passo seguinte na linha, e o que está por resolver
/// (cinco contadores que levam cada um à tela dona). Sempre HOJE: o dia de outra data é
/// a Agenda.
///
/// ⚠️ Nada de serviço SCOPED no construtor (checagem 37): um escopo por carga, que é o que
/// faz a tela ver o que a outra máquina gravou.
/// </summary>
public sealed partial class PainelViewModel : ObservableObject, IAtualizarAoVoltar
{
    private const int JanelaAniversarioDias = 6;

    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;

    public ObservableCollection<ProximoAChegar> Proximos { get; } = [];
    public ObservableCollection<ItemBalcao> Balcao { get; } = [];
    public ObservableCollection<LinhaPendenciaRecepcao> Pendencias { get; } = [];
    public ObservableCollection<LinhaAniversariante> Aniversariantes { get; } = [];

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string _saudacao = InicioDoBalcao.Saudacao(DateTime.Now, SessaoUsuario.Atual.Nome);
    [ObservableProperty] private string _linhaDoDia = string.Empty;

    [ObservableProperty] private string _agendados = "—";
    [ObservableProperty] private double _ocupacaoFracao;
    [ObservableProperty] private bool _temOcupacao;
    [ObservableProperty] private string _agendadosDetalhe = string.Empty;
    [ObservableProperty] private string _aAtender = "—";
    [ObservableProperty] private string _aAtenderDetalhe = string.Empty;
    [ObservableProperty] private string _emAtendimento = "—";
    [ObservableProperty] private string _emAtendimentoDetalhe = string.Empty;
    [ObservableProperty] private string _atendidos = "—";
    [ObservableProperty] private string _atendidosDetalhe = string.Empty;

    [ObservableProperty] private string _resumoProximos = string.Empty;
    [ObservableProperty] private bool _pendenciasAbertas;
    [ObservableProperty] private bool _aniversariantesAbertos;
    [ObservableProperty] private string _rodapeAniversario = string.Empty;
    [ObservableProperty] private bool _aniversariantesNaoVerificados;

    /// <summary>Descarte de resposta fora de ordem (parcela 50).</summary>
    private int _geracaoCarga;

    private readonly DispatcherTimer _relogio;
    private readonly EscutaDoBanco _escutaAgenda;
    private List<Agendamento> _doDia = [];

    public bool PodeMarcar => SessaoUsuario.Atual.Pode(Permissao.EditarAgenda);
    public bool PodeMoverFila => SessaoUsuario.Atual.PodeAlgum(Permissao.EditarAgenda | Permissao.MovimentarFila);

    public PainelViewModel(IServiceScopeFactory escopos, ISnackbarService snackbar)
    {
        _escopos = escopos;
        _snackbar = snackbar;

        // Dois minutos: contagens do dia, e cada batida custa várias consultas ao banco
        // remoto. O aviso do banco (EscutaDoBanco) traz a mudança da outra máquina antes.
        _relogio = new DispatcherTimer { Interval = TimeSpan.FromMinutes(2) };
        _relogio.Tick += (_, _) => _ = RelerAoVoltarAsync();
        _escutaAgenda = new EscutaDoBanco(RelerAoVoltarAsync, "Agendamentos");

        _ = CarregarAsync();
    }

    [RelayCommand]
    public Task CarregarAsync() => CarregarAsync(silencioso: false);

    private async Task CarregarAsync(bool silencioso)
    {
        var geracao = ++_geracaoCarga;
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var agora = DateTime.Now;
        try
        {
            if (!silencioso) { Carregando = true; NaoVerificado = false; }

            using var escopo = _escopos.CreateScope();
            var sp = escopo.ServiceProvider;
            var painel = sp.GetRequiredService<PainelRecepcaoService>();
            var repo = sp.GetRequiredService<IClinicaRepositorio>();

            // Leituras SEQUENCIAIS: o mesmo DbContext não aceita duas operações ao mesmo tempo.
            var resumo = await painel.ResumoAsync(hoje);
            var doDia = (await sp.GetRequiredService<AgendaService>().DoDiaAsync(hoje)).ToList();

            var confirmacoes = await Tentar(() => repo.RespostasDeConfirmacaoAsync(doDia.Select(a => a.Id).ToList()),
                "confirmações do dia", (IReadOnlyDictionary<int, RespostaDeConfirmacao>?)null);
            var ocupacao = SessaoUsuario.Atual.Pode(Permissao.VerAgenda)
                ? await Tentar(() => sp.GetRequiredService<IOcupacaoAgendaService>()
                    .ConsultarAsync(SessaoUsuario.Atual.UsuarioId, hoje, hoje), "ocupação do dia", (OcupacaoAgenda?)null)
                : null;
            var pendencias = await Tentar(() => painel.PendenciasDosPacientesAsync(hoje, resumo.PacientesDoDia),
                "guias pendentes do dia", (IReadOnlyList<PendenciaCodigo>?)null);
            var amanha = await Tentar(() => sp.GetRequiredService<CampanhaService>()
                    .ContatosAsync(TipoContato.ConfirmacaoSessao, null, hoje.AddDays(1), hoje.AddDays(1)),
                "rodada de amanhã", (IReadOnlyList<ContatoCampanha>?)null);
            var retornos = await Tentar(() => sp.GetRequiredService<RetornosAMarcarService>().ListarAsync(),
                "retornos a marcar", (IReadOnlyList<RetornoAMarcar>?)null);
            var acompanhamento = await Tentar(() => sp.GetRequiredService<IAcompanhamentoPacienteService>()
                    .ListarAsync(SessaoUsuario.Atual.UsuarioId), "acompanhamento", (IReadOnlyList<LinhaAcompanhamento>?)null);

            if (geracao != _geracaoCarga) return;

            // ===== Cabeçalho e cartões =====
            Saudacao = InicioDoBalcao.Saudacao(agora, SessaoUsuario.Atual.Nome);
            var porChegar = InicioDoBalcao.PorChegar(doDia);
            LinhaDoDia = InicioDoBalcao.LinhaDoDia(hoje, resumo.Agendados, porChegar);

            Agendados = resumo.Agendados.ToString();
            var percentual = ocupacao?.Resumo.Percentual;
            TemOcupacao = percentual is not null;
            OcupacaoFracao = percentual is { } p ? Math.Clamp((double)p / 100.0, 0, 1) : 0;
            var encaixes = $"{resumo.Encaixes} {(resumo.Encaixes == 1 ? "encaixe" : "encaixes")}";
            AgendadosDetalhe = percentual is { } pct
                ? $"{Math.Round(pct):0}% da agenda ocupada · {encaixes}"
                : $"ocupação sem jornada declarada · {encaixes}";

            AAtender = resumo.Aguardando.ToString();
            var noLocal = doDia.Count(a => a.Etapa is EtapaFila.Chegou or EtapaFila.Chamado);
            AAtenderDetalhe = noLocal == 0 ? "ninguém no local ainda"
                : $"{noLocal} {(noLocal == 1 ? "já está" : "já estão")} no local";

            EmAtendimento = resumo.EmAtendimento.ToString();
            EmAtendimentoDetalhe = InicioDoBalcao.QuemAtende(doDia);
            Atendidos = resumo.Atendidos.ToString();
            AtendidosDetalhe = InicioDoBalcao.FaltasECancelamentos(resumo.Faltas, resumo.Cancelados);

            // ===== Próximos a chegar =====
            _doDia = doDia;
            var proximos = InicioDoBalcao.Proximos(doDia,
                confirmacoes ?? new Dictionary<int, RespostaDeConfirmacao>(), agora);
            Proximos.Clear();
            foreach (var l in proximos) Proximos.Add(l);
            ResumoProximos = $"{proximos.Count} {(proximos.Count == 1 ? "pessoa" : "pessoas")}";

            // ===== O que resolver no balcão =====
            var guias = pendencias?.Where(x => x.Ordem == OrdemCodigo.Segundo).ToList();
            Pendencias.Clear();
            foreach (var x in guias ?? [])
                Pendencias.Add(new LinhaPendenciaRecepcao
                {
                    PacienteId = x.PacienteId,
                    Paciente = x.PacienteNome,
                    Descricao = x.Descricao ?? $"{RotulosEnum.De(x.Tipo)} ({RotulosEnum.De(x.Ordem)})",
                    Atraso = x.DiasEmAtraso <= 0 ? "vence hoje" : $"{x.DiasEmAtraso} dia(s) em atraso",
                    Telefone = x.PacienteTelefone
                });

            var rodada = amanha?.Select(c => RodadaDeConfirmacao.Classificar(c.Status, c.EnviadoEm, c.Comentario)).ToList();
            var confirmar = rodada is null
                ? new ContadorDoBalcao("Confirmar amanhã", null, "não foi possível conferir")
                : InicioDoBalcao.ConfirmarAmanha(rodada);
            var recall = acompanhamento?.Count(a => a.Pendente && a.ProximoContato <= hoje);

            var itens = new List<ItemBalcao>
            {
                Item("guias", "Guias do 2º código", guias?.Count, "de pacientes que vêm hoje", "doc", "erro", null),
                new ItemBalcao
                {
                    Chave = "confirmar", Titulo = confirmar.Titulo, Valor = confirmar.ValorTexto, Detalhe = confirmar.Detalhe,
                    Icone = "balao", Tom = "info", Destino = ModuloRecepcao.ChaveConfirmacoes
                },
                Item("retornos", "Retornos pedidos pelo médico", retornos?.Count, "ainda sem horário marcado",
                    "volta", "info", ModuloRecepcao.ChaveRetornosAMarcar),
                Item("recall", "Recall de hoje", recall, "contatos combinados para hoje",
                    "telefone", "neutro", ModuloRecepcao.ChaveRetorno)
            };
            Balcao.Clear();
            foreach (var i in itens.Where(i => i.Destino is null || NavegacaoSuite.Existe(i.Destino))) Balcao.Add(i);
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;
            Clinica.Application.Diagnostico.Registrar("Recepção — Início não pôde ser carregado", ex);
            if (silencioso) return;
            NaoVerificado = true;
            _snackbar.Erro($"Não foi possível carregar o início: {ex.Message}");
        }
        finally
        {
            if (!silencioso && geracao == _geracaoCarga) Carregando = false;
        }

        await CarregarAniversariantesAsync(hoje, geracao, silencioso);
    }

    private static ItemBalcao Item(string chave, string titulo, int? valor, string detalhe, string icone, string tom, string? destino)
        => new()
        {
            Chave = chave, Titulo = titulo, Valor = valor?.ToString() ?? "—",
            Detalhe = valor is null ? "não foi possível conferir" : detalhe,
            Icone = icone, Tom = tom, Destino = destino
        };

    /// <summary>
    /// Cada bloco falha SOZINHO: um contador que não pôde ser lido mostra "—" e diz isso,
    /// e o resto da tela continua. Zero por falha seria indistinguível de "nada a resolver".
    /// </summary>
    private static async Task<T> Tentar<T>(Func<Task<T>> leitura, string oque, T seFalhar)
    {
        try { return await leitura(); }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar($"Recepção — Início: {oque} não pôde ser lido", ex);
            return seFalhar;
        }
    }

    private async Task CarregarAniversariantesAsync(DateOnly dia, int geracao, bool silencioso)
    {
        try
        {
            using var escopo = _escopos.CreateScope();
            var lista = await escopo.ServiceProvider.GetRequiredService<RelacionamentoService>()
                .AniversariantesAsync(dia, JanelaAniversarioDias, _doDia.Select(a => a.PacienteId).ToHashSet());
            if (geracao != _geracaoCarga) return;

            Aniversariantes.Clear();
            foreach (var a in lista)
                Aniversariantes.Add(new LinhaAniversariante
                {
                    PacienteId = a.PacienteId,
                    Paciente = a.Nome,
                    Telefone = a.Telefone,
                    VemHoje = a.VemHoje,
                    Detalhe = (a.Idade is { } idade ? $"{idade} anos · " : string.Empty)
                              + a.DiaEMes + (a.VemHoje ? " · vem hoje" : string.Empty)
                });
            AniversariantesNaoVerificados = false;
            RodapeAniversario = lista.Count switch
            {
                0 => "Nenhum aniversariante nesta semana",
                1 => "1 aniversariante nesta semana",
                var n => $"{n} aniversariantes nesta semana"
            };
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;
            Clinica.Application.Diagnostico.Registrar("Recepção — aniversariantes não puderam ser lidos", ex);
            if (silencioso) return;
            Aniversariantes.Clear();
            AniversariantesNaoVerificados = true;
            RodapeAniversario = "Aniversariantes: não foi possível conferir";
        }
    }

    // ============================ Releitura ============================

    public void AoEntrarEmCena() { _relogio.Start(); _escutaAgenda.Ligar(); }
    public void AoSairDeCena() { _relogio.Stop(); _escutaAgenda.Desligar(); }
    public void AtualizarAoVoltar() => _ = RelerAoVoltarAsync();

    private async Task RelerAoVoltarAsync()
    {
        if (Carregando) return;
        try { await CarregarAsync(silencioso: true); }
        catch (Exception ex) { Clinica.Application.Diagnostico.Registrar("Recepção — releitura do Início falhou", ex); }
    }

    // ============================ Ações ============================

    [RelayCommand]
    private void MarcarAtendimento()
    {
        if (!NavegacaoSuite.Ir(ModuloRecepcao.ChaveMarcarHorario))
            _snackbar.Info("Marcar atendimento não está disponível para o seu acesso.");
    }

    [RelayCommand]
    private void VerAgendaDoDia()
    {
        if (!NavegacaoSuite.Ir(ModuloRecepcao.ChaveFila))
            _snackbar.Info("A agenda do dia não está disponível para o seu acesso.");
    }

    /// <summary>Clique num contador: leva à tela dona; as guias abrem a lista aqui mesmo.</summary>
    [RelayCommand]
    private void AbrirItem(ItemBalcao? item)
    {
        if (item is null) return;
        if (item.Destino is null) { PendenciasAbertas = true; return; }
        if (!NavegacaoSuite.Ir(item.Destino))
            _snackbar.Info($"{item.Titulo}: esta tela não está disponível para o seu acesso.");
    }

    [RelayCommand] private void VerAniversariantes() => AniversariantesAbertos = true;

    /// <summary>
    /// O passo da linha: abrir (já está no local) ou registrar a chegada. O termo não é
    /// colhido no balcão (out/2026, decisão da clínica) — quem colhe é a enfermagem.
    /// </summary>
    [RelayCommand]
    private async Task AcaoDaLinhaAsync(ProximoAChegar? linha)
    {
        if (linha is null) return;
        try
        {
            switch (linha.Situacao)
            {
                case SituacaoChegada.NoLocal:
                    await AbrirHorarioAsync(linha);
                    break;
                default:
                    SessaoUsuario.Atual.ExigirAlgum(Permissao.EditarAgenda | Permissao.MovimentarFila, "registrar a chegada");
                    using (var escopo = _escopos.CreateScope())
                        await escopo.ServiceProvider.GetRequiredService<AgendaService>()
                            .RegistrarChegadaAsync(linha.AgendamentoId, SessaoUsuario.Atual.Operador);
                    _snackbar.Sucesso($"{linha.Paciente} está no local.");
                    break;
            }
            await CarregarAsync(silencioso: true);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — ação da linha do Início", ex);
            _snackbar.Erro(ex.Message);
        }
    }

    private async Task AbrirHorarioAsync(ProximoAChegar linha)
    {
        using var scope = _escopos.CreateScope();
        var agenda = scope.ServiceProvider.GetRequiredService<AgendaViewModel>();
        if (!await agenda.AbrirJanelaDoHorarioAsync(DateOnly.FromDateTime(linha.Hora), linha.AgendamentoId))
            _snackbar.Info("Este horário não está mais neste dia — ele pode ter sido remarcado em outra máquina.");
        else if (!string.IsNullOrWhiteSpace(agenda.Mensagem))
        {
            if (agenda.MensagemEhErro) _snackbar.Erro(agenda.Mensagem); else _snackbar.Info(agenda.Mensagem);
        }
    }

    [RelayCommand]
    private void CobrarGuia(LinhaPendenciaRecepcao? linha)
    {
        if (linha is null) return;
        var erro = Whatsapp.Abrir(linha.Telefone, linha.Paciente, Whatsapp.CobrancaDeGuia(linha.Paciente));
        if (erro is not null) _snackbar.Erro(erro);
    }

    [RelayCommand]
    private void Parabenizar(LinhaAniversariante? linha)
    {
        if (linha?.Telefone is not { } telefone) return;
        var primeiro = linha.Paciente.Split(' ').FirstOrDefault() ?? linha.Paciente;
        var erro = Whatsapp.Abrir(telefone, linha.Paciente,
            $"Feliz aniversário, {primeiro}! Toda a equipe da clínica deseja um ótimo dia.");
        if (erro is not null) _snackbar.Erro(erro);
    }
}
