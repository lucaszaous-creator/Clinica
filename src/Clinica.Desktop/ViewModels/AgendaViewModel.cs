using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>
/// Agenda da recepção. A visão de dia é uma grade de faixas de 30 minutos — o horário
/// livre é tão visível quanto o ocupado, e clicar nele já abre o agendamento com a hora
/// preenchida. A visão de semana empilha os dias lado a lado.
/// </summary>
public partial class AgendaViewModel : ObservableObject, IAtalhosDeTela
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    /// <summary>Faixa padrão exibida na grade do dia; expande sozinha se houver horário fora dela.</summary>
    private static readonly TimeOnly AberturaPadrao = new(7, 0);
    private static readonly TimeOnly FechamentoPadrao = new(20, 0);
    private const int MinutosPorFaixa = 30;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Controls.IDialogoService _dialogo;

    /// <summary>Grade de faixas de horário (visão de dia).</summary>
    public ObservableCollection<FaixaHorario> Faixas { get; } = new();

    /// <summary>Colunas de segunda a domingo (visão de semana).</summary>
    public ObservableCollection<ColunaDia> Semana { get; } = new();

    [ObservableProperty] private DateTime _dia = DateTime.Today;

    /// <summary>false = visão de dia; true = visão de semana (segunda a domingo do dia atual).</summary>
    [ObservableProperty] private bool _modoSemana;

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _ocupado;

    // Placar do período exibido
    [ObservableProperty] private int _totalAgendados;
    [ObservableProperty] private int _totalRealizados;
    [ObservableProperty] private int _totalFaltas;
    [ObservableProperty] private int _totalCancelados;

    /// <summary>
    /// Linha de contexto sobre as consultas renováveis do período: "3 pacientes deste dia
    /// com consulta a renovar". Nula quando não há nenhuma.
    ///
    /// É LINHA de texto, não faixa: contexto permanente virado faixa vira moldura, e uma
    /// faixa por assunto come a tela antes da grade começar. E o terceiro estado importa —
    /// quando a conferência não roda, ela diz isso em vez de deixar a agenda parecendo
    /// limpa.
    /// </summary>
    [ObservableProperty] private string? _resumoConsultas;

    /// <summary>Há ao menos uma consulta vencida no período (pinta a linha de vermelho).</summary>
    [ObservableProperty] private bool _consultaVencidaNoPeriodo;

    /// <summary>Nome da clínica (assinatura da mensagem de WhatsApp).</summary>
    private string? _nomeClinica;

    /// <summary>Segunda-feira da semana do dia selecionado.</summary>
    private DateTime InicioSemana => Dia.AddDays(-(((int)Dia.DayOfWeek + 6) % 7));

    public string TituloDia => ModoSemana
        ? $"Semana de {InicioSemana:dd/MM} a {InicioSemana.AddDays(6):dd/MM/yyyy}"
        : Dia.ToString("dddd, dd 'de' MMMM 'de' yyyy", PtBr);

    /// <summary>"Hoje", "Amanhã" ou vazio — contexto rápido ao lado do título.</summary>
    public string? SeloDia
    {
        get
        {
            if (ModoSemana) return null;
            var diferenca = (Dia.Date - DateTime.Today).Days;
            return diferenca switch { 0 => "Hoje", 1 => "Amanhã", -1 => "Ontem", _ => null };
        }
    }

    public AgendaViewModel(IServiceScopeFactory scopeFactory, Controls.IDialogoService dialogo)
    {
        _scopeFactory = scopeFactory;
        _dialogo = dialogo;
    }

    partial void OnDiaChanged(DateTime value)
    {
        OnPropertyChanged(nameof(TituloDia));
        OnPropertyChanged(nameof(SeloDia));
    }

    partial void OnModoSemanaChanged(bool value)
    {
        OnPropertyChanged(nameof(TituloDia));
        OnPropertyChanged(nameof(SeloDia));
        _ = Recarregar();
    }

    public async Task CarregarAsync()
    {
        await Recarregar();

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var prestador = await scope.ServiceProvider.GetRequiredService<ParametrosService>().ObterPrestadorAsync();
            _nomeClinica = string.IsNullOrWhiteSpace(prestador.NomeFantasia) ? prestador.RazaoSocial : prestador.NomeFantasia;
        }
        catch (Exception ex)
        {
            // Sem nome da clínica a mensagem sai sem assinatura; não impede a agenda.
            Configuracao.LogErros.Registrar("Agenda — nome da clínica não pôde ser lido", ex);
        }
    }

    [RelayCommand]
    private async Task Recarregar()
    {
        IReadOnlyList<Agendamento> lista;
        using (var scope = _scopeFactory.CreateScope())
        {
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
            lista = ModoSemana
                ? await agenda.NoPeriodoAsync(DateOnly.FromDateTime(InicioSemana), DateOnly.FromDateTime(InicioSemana.AddDays(6)))
                : await agenda.DoDiaAsync(DateOnly.FromDateTime(Dia));
        }

        TotalAgendados = lista.Count(a => a.Status == StatusAgendamento.Agendado);
        TotalRealizados = lista.Count(a => a.Status == StatusAgendamento.Realizado);
        TotalFaltas = lista.Count(a => a.Status == StatusAgendamento.Faltou);
        TotalCancelados = lista.Count(a => a.Status == StatusAgendamento.Cancelado);

        var consultas = await ConferirConsultasAsync(lista);

        if (ModoSemana) MontarSemana(lista, consultas);
        else MontarDia(lista, consultas);
    }

    /// <summary>
    /// Situação da consulta renovável dos pacientes do período exibido.
    ///
    /// A consulta a renovar só era lida em dois lugares — o painel de pendências e a aba
    /// Consultas —, e nenhum deles é onde a secretária está quando marca ou recebe o
    /// paciente. Aqui ela chega ao horário: renovar com a pessoa presente custa uma
    /// assinatura, e descobrir depois custa telefonema (ou a guia recusada).
    ///
    /// Uma leitura só para o período inteiro, pelos pacientes da grade — varrer a base de
    /// pacientes para responder sobre as vinte pessoas de hoje seria caro num banco remoto,
    /// e a agenda recarrega a cada navegação de dia.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, Clinica.Application.Modelos.StatusConsultaPaciente>>
        ConferirConsultasAsync(IReadOnlyList<Agendamento> lista)
    {
        ResumoConsultas = null;
        ConsultaVencidaNoPeriodo = false;

        var pacientes = lista
            .Select(a => a.Paciente)
            .OfType<Paciente>()
            .GroupBy(p => p.Id)
            .Select(g => g.First())
            .ToList();

        if (pacientes.Count == 0)
            return new Dictionary<int, Clinica.Application.Modelos.StatusConsultaPaciente>();

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<ConsultaService>();

            // A referência é o dia exibido, não hoje: quem abre a agenda da semana que vem
            // precisa saber quais consultas terão vencido LÁ — é para isso que se olha a
            // agenda com antecedência.
            var referencia = DateOnly.FromDateTime(ModoSemana ? InicioSemana : Dia);
            var situacao = await servico.SituacaoDeAsync(pacientes, referencia);

            var aRenovar = situacao.Values.Where(s => s.ARenovar).ToList();
            ConsultaVencidaNoPeriodo = aRenovar.Any(s => s.Vencida);
            ResumoConsultas = aRenovar.Count switch
            {
                0 => null,
                1 => $"1 paciente {(ModoSemana ? "desta semana" : "deste dia")} com consulta a renovar"
                     + $" — {aRenovar[0].PacienteNome}.",
                _ => $"{aRenovar.Count} pacientes {(ModoSemana ? "desta semana" : "deste dia")}"
                     + " com consulta a renovar — veja o selo no cartão."
            };

            return situacao;
        }
        catch (Exception ex)
        {
            // Degrada, mas não mente: sem o terceiro estado a agenda ficaria idêntica à de
            // um dia sem nenhuma consulta a renovar.
            Configuracao.LogErros.Registrar("Agenda — consultas a renovar não puderam ser conferidas", ex);
            ResumoConsultas = "Não foi possível conferir as consultas a renovar deste período.";
            return new Dictionary<int, Clinica.Application.Modelos.StatusConsultaPaciente>();
        }
    }

    /// <summary>Monta a grade de faixas do dia, incluindo as vazias (o horário livre é informação).</summary>
    private void MontarDia(
        IReadOnlyList<Agendamento> lista,
        IReadOnlyDictionary<int, Clinica.Application.Modelos.StatusConsultaPaciente> consultas)
    {
        Semana.Clear();
        Faixas.Clear();

        // A janela padrão abre em 07:00–20:00, mas nunca esconde um agendamento:
        // encaixes fora do expediente esticam a grade.
        var abertura = AberturaPadrao;
        var fechamento = FechamentoPadrao;
        if (lista.Count > 0)
        {
            var primeiro = TimeOnly.FromDateTime(lista.Min(a => a.DataHora));
            var ultimo = TimeOnly.FromDateTime(lista.Max(a => a.DataHora));
            if (primeiro < abertura) abertura = new TimeOnly(primeiro.Hour, 0);
            if (ultimo >= fechamento) fechamento = new TimeOnly(ultimo.Hour, 0).AddMinutes(MinutosPorFaixa);
        }

        var cursor = Dia.Date.Add(abertura.ToTimeSpan());
        var fim = Dia.Date.Add(fechamento.ToTimeSpan());
        while (cursor < fim)
        {
            var limite = cursor.AddMinutes(MinutosPorFaixa);
            var naFaixa = lista
                .Where(a => a.DataHora >= cursor && a.DataHora < limite)
                .OrderBy(a => a.DataHora)
                .Select(a => Montar(a, consultas))
                .ToList();

            Faixas.Add(new FaixaHorario(cursor.ToString("HH:mm"), cursor, naFaixa));
            cursor = limite;
        }
    }

    private void MontarSemana(
        IReadOnlyList<Agendamento> lista,
        IReadOnlyDictionary<int, Clinica.Application.Modelos.StatusConsultaPaciente> consultas)
    {
        Faixas.Clear();
        Semana.Clear();

        for (var i = 0; i < 7; i++)
        {
            var data = InicioSemana.Date.AddDays(i);
            var doDia = lista
                .Where(a => a.DataHora.Date == data)
                .OrderBy(a => a.DataHora)
                .Select(a => Montar(a, consultas))
                .ToList();

            Semana.Add(new ColunaDia(
                PtBr.DateTimeFormat.GetAbbreviatedDayName(data.DayOfWeek).TrimEnd('.').ToUpperInvariant(),
                data.ToString("dd/MM"),
                data,
                data == DateTime.Today,
                doDia));
        }
    }

    private static CartaoAgendamento Montar(
        Agendamento a,
        IReadOnlyDictionary<int, Clinica.Application.Modelos.StatusConsultaPaciente> consultas)
    {
        // Só o horário de pé cobra a renovação: num cancelado ou numa falta não há
        // ninguém para assinar nada, e o selo ali só encheria a grade de laranja.
        var consulta = a.Paciente is { } p
            && a.Status is StatusAgendamento.Agendado or StatusAgendamento.Realizado
            && consultas.TryGetValue(p.Id, out var s) && s.ARenovar
                ? s
                : null;

        return new CartaoAgendamento(
            a,
            a.DataHora.ToString("HH:mm"),
            a.Paciente?.Nome ?? "—",
            CatalogoModalidades.Nome(a.ModalidadeCodigo ?? a.ModalidadePrevista.ToString()),
            a.Status switch
            {
                StatusAgendamento.Realizado => "Atendido",
                StatusAgendamento.Faltou => "Faltou",
                StatusAgendamento.Cancelado => "Cancelado",
                _ => "Agendado"
            },
            a.Paciente?.FotoMiniatura,
            a.Paciente is null ? string.Empty : CatalogoConvenios.Nome(a.Paciente.ConvenioCodigo ?? a.Paciente.Convenio.ToString()),
            a.Paciente?.CarteirinhaVencida ?? false,
            a.Observacoes,
            a.Status == StatusAgendamento.Agendado,
            a.Status == StatusAgendamento.Realizado,
            a.Status == StatusAgendamento.Faltou,
            a.Status == StatusAgendamento.Cancelado,
            consulta?.SeloRenovacao,
            consulta?.AvisoRenovacao,
            consulta?.Vencida ?? false);
    }

    [RelayCommand] private async Task DiaAnterior() { Dia = Dia.AddDays(ModoSemana ? -7 : -1); await Recarregar(); }
    [RelayCommand] private async Task ProximoDia() { Dia = Dia.AddDays(ModoSemana ? 7 : 1); await Recarregar(); }
    [RelayCommand] private async Task Hoje() { Dia = DateTime.Today; await Recarregar(); }

    /// <summary>Abre o cadastro de agendamento; com faixa, já vai com data e hora preenchidas.</summary>
    private async Task AbrirCadastroAsync(DateTime? inicio, int? agendamentoId = null)
    {
        var janela = new Alertas.AgendamentoWindow(
            new AgendamentoEdicaoViewModel(_scopeFactory, _dialogo), inicio, agendamentoId)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (janela.ShowDialog() != true) return;

        Mensagem = agendamentoId is null ? "Agendamento criado." : "Agendamento remarcado.";
        await Recarregar();
    }

    /// <summary>
    /// Remarca o horário preservando o agendamento. Cancelar e recriar seria mentir no
    /// histórico: registraria um cancelamento que nunca aconteceu.
    /// </summary>
    [RelayCommand]
    private async Task Remarcar(CartaoAgendamento? cartao)
    {
        if (cartao is null) return;

        if (cartao.Realizado)
        {
            Mensagem = "Este horário já virou atendimento; estorne o atendimento antes de remarcar.";
            return;
        }

        await AbrirCadastroAsync(cartao.Item.DataHora, cartao.Item.Id);
    }

    [RelayCommand]
    private async Task Novo() => await AbrirCadastroAsync(ModoSemana ? null : Dia.Date.Add(AberturaPadrao.ToTimeSpan()));

    /// <summary>Clique numa faixa livre da grade: agenda naquele horário.</summary>
    [RelayCommand]
    private async Task AgendarNaFaixa(FaixaHorario? faixa)
    {
        if (faixa is not null) await AbrirCadastroAsync(faixa.Inicio);
    }

    /// <summary>Clique no cabeçalho de um dia da semana: abre aquele dia na visão de dia.</summary>
    [RelayCommand]
    private async Task AbrirDia(ColunaDia? coluna)
    {
        if (coluna is null) return;
        Dia = coluna.Data;
        ModoSemana = false;   // já dispara o recarregamento
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ConfirmarPresenca(CartaoAgendamento? cartao)
    {
        if (cartao is null) return;
        if (!_dialogo.Confirmar("Confirmar presença",
            $"Confirmar presença de {cartao.Paciente} e gerar o atendimento (códigos de faturamento)?")) return;

        if (Ocupado) return;
        Ocupado = true;
        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
                var resultado = await agenda.ConfirmarPresencaAsync(cartao.Item.Id);
                Mensagem = $"Atendimento gerado com {resultado.Atendimento.Codigos.Count} código(s).";
            }

            await Recarregar();
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível confirmar a presença: {ex.Message}";
        }
        finally
        {
            Ocupado = false;
        }
    }

    [RelayCommand]
    private async Task Cancelar(CartaoAgendamento? cartao)
    {
        if (cartao is null) return;
        if (!_dialogo.ConfirmarPerigo("Cancelar agendamento",
            $"Cancelar o horário de {cartao.Paciente} às {cartao.Hora}?")) return;

        using (var scope = _scopeFactory.CreateScope())
        {
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
            await agenda.CancelarAsync(cartao.Item.Id, SessaoUsuario.Atual.Operador);
        }
        await Recarregar();
    }

    [RelayCommand]
    private async Task Faltou(CartaoAgendamento? cartao)
    {
        if (cartao is null) return;
        using (var scope = _scopeFactory.CreateScope())
        {
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
            await agenda.MarcarFaltaAsync(cartao.Item.Id, SessaoUsuario.Atual.Operador);
        }
        await Recarregar();
    }

    /// <summary>
    /// Abre o WhatsApp (wa.me) com a mensagem de confirmação pronta para o paciente.
    /// Falta de paciente é sessão não faturada — confirmar na véspera é rotina da recepção.
    /// </summary>
    [RelayCommand]
    private void Whatsapp(CartaoAgendamento? cartao)
    {
        if (cartao?.Item.Paciente is null) return;
        var paciente = cartao.Item.Paciente;

        var fone = Telefone.Normalizar(paciente.Telefone);
        if (fone.Length is < 10 or > 13)
        {
            Mensagem = $"{paciente.Nome}: telefone ausente ou inválido no cadastro (edite em Pacientes).";
            return;
        }
        if (fone.Length is 10 or 11)
            fone = "55" + fone; // wa.me exige DDI

        var quando = cartao.Item.DataHora;
        var primeiroNome = paciente.Nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? paciente.Nome;
        var dia = quando.Date == DateTime.Today.AddDays(1) ? "amanhã" : quando.ToString("dd/MM", PtBr);
        var texto = $"Olá, {primeiroNome}! Estamos confirmando sua sessão {dia} às {quando:HH:mm}." +
                    " Se tiver algum imprevisto, é só responder por aqui." +
                    (string.IsNullOrWhiteSpace(_nomeClinica) ? string.Empty : $" — {_nomeClinica}");

        try
        {
            Process.Start(new ProcessStartInfo($"https://wa.me/{fone}?text={Uri.EscapeDataString(texto)}")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível abrir o WhatsApp: {ex.Message}";
        }
    }

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoAtualizar => RecarregarCommand;
}
