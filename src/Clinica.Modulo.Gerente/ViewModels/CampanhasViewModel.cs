using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Gerente.ViewModels;

/// <summary>Uma linha da fila de contatos, já formatada para a tela.</summary>
public sealed class LinhaContato
{
    public required int Id { get; init; }
    public required string Paciente { get; init; }
    public required string Telefone { get; init; }
    public required string Tipo { get; init; }
    public required string Situacao { get; init; }
    public required string Referencia { get; init; }
    public required string Nota { get; init; }
    public required string Comentario { get; init; }

    /// <summary>Só o NPS registra nota — o botão some nos outros tipos.</summary>
    public required bool EhNps { get; init; }

    /// <summary>Sem telefone válido não há WhatsApp a abrir; a linha continua na tela.</summary>
    public required bool TemTelefone { get; init; }

    /// <summary>Já respondido ou dispensado não volta ao fluxo — as ações ficam desligadas.</summary>
    public required bool PodeAgir { get; init; }

    /// <summary>Só há WhatsApp a abrir com telefone válido e contato ainda em aberto.</summary>
    public bool PodeEnviar => PodeAgir && TemTelefone;

    // Dados crus, para montar a mensagem sem reparsear o que está na tela.
    public required string NomeCru { get; init; }
    public required string TelefoneCru { get; init; }
    public required TipoContato TipoCru { get; init; }
    public required DateTime? QuandoCru { get; init; }
    public required int DiasSemVir { get; init; }
}

/// <summary>
/// A tela das campanhas (features 11 e 02): confirmação de sessão, NPS e recall numa
/// fila só de trabalho.
///
/// GERAR e ENVIAR são passos separados de propósito. Gerar é a parte automática — o
/// sistema descobre quem contatar, aplica a LGPD e não duplica ninguém. Enviar é um
/// clique por paciente, porque o WhatsApp é o número da clínica: disparo em massa
/// automatizado por ali termina com o número bloqueado, e aí a clínica perde o canal
/// inteiro para economizar alguns cliques.
///
/// O resultado de cada rodada diz quantos foram gerados E quantos ficaram de fora, por
/// quê. Campanha que filtra em silêncio parece quebrada.
/// </summary>
public sealed partial class CampanhasViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;
    private readonly SessaoUsuario _sessao;

    public ObservableCollection<LinhaContato> Contatos { get; } = [];

    public IReadOnlyList<string> Tipos { get; } = ["Todos", "Confirmação", "NPS", "Recall"];

    /// <summary>
    /// Janelas de tempo da fila.
    ///
    /// NÃO usa o <see cref="PeriodoGerencial"/> das outras telas de propósito: lá o
    /// período termina hoje, porque indicador é sobre o que já aconteceu. Aqui metade
    /// do trabalho está no FUTURO — a confirmação da sessão de amanhã é o item mais
    /// importante da tela, e um período que acaba hoje a esconderia.
    /// </summary>
    public IReadOnlyList<string> Janelas { get; } =
        ["Semana que vem", "Últimos 30 dias", "Últimos 3 meses"];
    public IReadOnlyList<string> Situacoes { get; } = ["A enviar", "Todos", "Enviados", "Respondidos"];

    [ObservableProperty] private string _tipoSelecionado = "Todos";
    [ObservableProperty] private string _situacaoSelecionada = "A enviar";
    [ObservableProperty] private string _janelaSelecionada = "Semana que vem";
    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Resultado da última rodada gerada — fica na tela, não é snackbar que some.</summary>
    [ObservableProperty] private string _resultadoRodada = string.Empty;

    /// <summary>O intervalo que a fila está mostrando, no cabeçalho.</summary>
    [ObservableProperty] private string _intervaloFormatado = string.Empty;

    public CampanhasViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar,
        IDialogoService dialogo, SessaoUsuario sessao)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _sessao = sessao;
        _ = CarregarAsync();
    }

    partial void OnTipoSelecionadoChanged(string value) => _ = CarregarAsync();
    partial void OnSituacaoSelecionadaChanged(string value) => _ = CarregarAsync();
    partial void OnJanelaSelecionadaChanged(string value) => _ = CarregarAsync();

    /// <summary>Intervalo da janela escolhida. Sempre inclui alguns dias à frente.</summary>
    private (DateOnly Inicio, DateOnly Fim) Intervalo(DateOnly hoje) => JanelaSelecionada switch
    {
        "Últimos 30 dias" => (hoje.AddDays(-30), hoje.AddDays(7)),
        "Últimos 3 meses" => (hoje.AddDays(-90), hoje.AddDays(7)),
        // O padrão olha para frente: é a fila de trabalho, não o histórico.
        _ => (hoje.AddDays(-7), hoje.AddDays(7))
    };

    // ------------------------------------------------------------------ leitura

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Carregando = true;
            Mensagem = string.Empty;
            MensagemEhErro = false;

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var (inicio, fim) = Intervalo(hoje);
            IntervaloFormatado = $"{inicio:dd/MM} a {fim:dd/MM}";

            using var scope = _escopos.CreateScope();
            var campanhas = scope.ServiceProvider.GetRequiredService<CampanhaService>();

            var lista = await campanhas.ContatosAsync(TipoFiltro(), StatusFiltro(), inicio, fim);

            Contatos.Clear();
            foreach (var c in lista) Contatos.Add(Converter(c, hoje));

            if (Contatos.Count == 0)
                Mensagem = "Nada nesta fila. Gere uma rodada acima para começar.";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — campanhas não puderam ser carregadas", ex);
            Mensagem = $"Não foi possível carregar as campanhas: {ex.Message}";
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    private static LinhaContato Converter(ContatoCampanha c, DateOnly hoje)
    {
        var nome = c.Paciente?.Nome ?? "(paciente removido)";
        var fone = c.Paciente?.Telefone;

        return new LinhaContato
        {
            Id = c.Id,
            Paciente = nome,
            Telefone = string.IsNullOrWhiteSpace(fone) ? "sem telefone" : Clinica.Domain.Telefone.Formatar(fone),
            Tipo = CampanhaService.Rotular(c.Tipo),
            Situacao = CampanhaService.Rotular(c.Status),
            Referencia = c.Referencia.ToString("dd/MM/yyyy"),
            Nota = c.Nota is { } n ? $"{n} · {CampanhaService.Rotular(c.Classe!.Value)}" : "—",
            Comentario = c.Comentario ?? string.Empty,
            EhNps = c.Tipo == TipoContato.Nps,
            TemTelefone = Clinica.Domain.Telefone.Normalizar(fone).Length is >= 10 and <= 13,
            PodeAgir = !c.Encerrado,
            NomeCru = nome,
            TelefoneCru = fone ?? string.Empty,
            TipoCru = c.Tipo,
            QuandoCru = c.Agendamento?.DataHora,
            DiasSemVir = hoje.DayNumber - c.Referencia.DayNumber
        };
    }

    private TipoContato? TipoFiltro() => TipoSelecionado switch
    {
        "Confirmação" => TipoContato.ConfirmacaoSessao,
        "NPS" => TipoContato.Nps,
        "Recall" => TipoContato.Recall,
        _ => null
    };

    private StatusContato? StatusFiltro() => SituacaoSelecionada switch
    {
        "A enviar" => StatusContato.Pendente,
        "Enviados" => StatusContato.Enviado,
        "Respondidos" => StatusContato.Respondido,
        _ => null
    };

    // ------------------------------------------------------------------ rodadas

    [RelayCommand]
    private async Task GerarConfirmacoesAsync()
        => await GerarAsync(async campanhas =>
            await campanhas.GerarConfirmacoesAsync(DateOnly.FromDateTime(DateTime.Today.AddDays(1))));

    [RelayCommand]
    private async Task GerarNpsAsync()
        => await GerarAsync(async campanhas =>
            await campanhas.GerarNpsAsync(DateOnly.FromDateTime(DateTime.Today.AddDays(-1))));

    [RelayCommand]
    private async Task GerarRecallAsync()
        => await GerarAsync(async campanhas =>
        {
            using var scope = _escopos.CreateScope();
            var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();
            var dias = await parametros.ObterDiasInatividadeRecallAsync();
            return await campanhas.GerarRecallAsync(DateOnly.FromDateTime(DateTime.Today), dias);
        });

    private async Task GerarAsync(Func<CampanhaService, Task<Clinica.Application.Modelos.ResultadoCampanha>> rodada)
    {
        try
        {
            Carregando = true;
            using var scope = _escopos.CreateScope();
            var campanhas = scope.ServiceProvider.GetRequiredService<CampanhaService>();

            var r = await rodada(campanhas);

            var partes = new List<string> { $"{r.Gerados} gerado(s)" };
            if (r.JaExistiam > 0) partes.Add($"{r.JaExistiam} já existia(m)");
            if (r.SemConsentimento > 0)
                partes.Add($"{r.SemConsentimento} sem consentimento de comunicação (LGPD)");
            if (r.SemTelefone > 0) partes.Add($"{r.SemTelefone} sem telefone no cadastro");

            ResultadoRodada = $"{CampanhaService.Rotular(r.Tipo)}: {string.Join(" · ", partes)}.";
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — rodada de campanha falhou", ex);
            Mensagem = $"Não foi possível gerar a rodada: {ex.Message}";
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    // -------------------------------------------------------------------- ações

    /// <summary>Abre o WhatsApp com a mensagem pronta e registra o envio.</summary>
    [RelayCommand]
    private async Task EnviarAsync(LinhaContato? linha)
    {
        if (linha is null) return;

        var erro = Whatsapp.Abrir(linha.TelefoneCru, linha.NomeCru, MontarMensagem(linha));
        if (erro is not null)
        {
            // Não abriu: NÃO marcar como enviado. Registrar envio que não aconteceu é
            // exatamente "falha exibida como sucesso".
            Mensagem = erro;
            MensagemEhErro = true;
            return;
        }

        await ExecutarAsync(async campanhas =>
        {
            await campanhas.RegistrarEnvioAsync(linha.Id, _sessao.Operador);
            _snackbar.Sucesso("Envio registrado.");
        });
    }

    private static string MontarMensagem(LinhaContato linha) => linha.TipoCru switch
    {
        TipoContato.ConfirmacaoSessao => Whatsapp.ConfirmacaoDeSessao(
            linha.NomeCru, linha.QuandoCru ?? DateTime.Today),
        TipoContato.Nps => Whatsapp.PesquisaDeSatisfacao(linha.NomeCru),
        _ => Whatsapp.Recall(linha.NomeCru, linha.DiasSemVir)
    };

    [RelayCommand]
    private async Task RegistrarNotaAsync(LinhaContato? linha)
    {
        if (linha is null || !linha.EhNps) return;

        var texto = _dialogo.PerguntarTexto(
            "Nota do NPS",
            $"Que nota {linha.NomeCru} deu, de 0 a 10? (pode acrescentar o comentário depois de um espaço)");
        if (texto is null) return;

        var partes = texto.Trim().Split(' ', 2);
        if (!int.TryParse(partes[0], out var nota) || !ContatoCampanha.NotaValida(nota))
        {
            Mensagem = "A nota precisa ser um número de 0 a 10.";
            MensagemEhErro = true;
            return;
        }

        await ExecutarAsync(async campanhas =>
        {
            await campanhas.RegistrarNotaAsync(linha.Id, nota, partes.Length > 1 ? partes[1] : null);
            _snackbar.Sucesso("Nota registrada.");
        });
    }

    [RelayCommand]
    private async Task RegistrarRespostaAsync(LinhaContato? linha)
    {
        if (linha is null) return;

        var comentario = _dialogo.PerguntarTexto(
            "Resposta do paciente", $"O que {linha.NomeCru} respondeu?");
        if (comentario is null) return;

        await ExecutarAsync(async campanhas =>
        {
            await campanhas.RegistrarRespostaAsync(linha.Id, comentario);
            _snackbar.Sucesso("Resposta registrada.");
        });
    }

    [RelayCommand]
    private async Task DispensarAsync(LinhaContato? linha)
    {
        if (linha is null) return;

        var motivo = _dialogo.PerguntarTexto(
            "Dispensar contato",
            $"Por que {linha.NomeCru} sai desta campanha? (o motivo fica registrado)");
        if (motivo is null) return;

        await ExecutarAsync(async campanhas =>
        {
            await campanhas.DispensarAsync(linha.Id, motivo);
            _snackbar.Sucesso("Contato dispensado.");
        });
    }

    private async Task ExecutarAsync(Func<CampanhaService, Task> acao)
    {
        try
        {
            Carregando = true;
            using (var scope = _escopos.CreateScope())
                await acao(scope.ServiceProvider.GetRequiredService<CampanhaService>());

            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — ação de campanha falhou", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }
}
