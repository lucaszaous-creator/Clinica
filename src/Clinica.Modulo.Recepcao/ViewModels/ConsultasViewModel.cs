using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Configuracao;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>
/// Uma linha da lista, com o semáforo e o nome do convênio JÁ RESOLVIDOS.
///
/// A suíte resolve isso no ViewModel e não por conversor de XAML (é o padrão do
/// <c>LinhaPendencia</c> do Gerente): os conversores <c>UrgenciaParaCor</c> e
/// <c>EnumDescricao</c> são do design system do faturamento e não existem aqui — e
/// duplicá-los para uma tela só seria pagar o débito do design system uma terceira vez.
/// </summary>
public sealed class LinhaConsulta
{
    public required StatusConsultaPaciente Status { get; init; }

    public required string Paciente { get; init; }
    public required string Convenio { get; init; }
    public required string UltimaEmissao { get; init; }
    public required string Vencimento { get; init; }
    public required string Situacao { get; init; }

    public bool EhVermelha => Status.Urgencia == NivelUrgencia.Vermelho;
    public bool EhAmarela => Status.Urgencia == NivelUrgencia.Amarelo;
    public bool EhVerde => Status.Urgencia == NivelUrgencia.Verde;

    /// <summary>Convênio sem consulta renovável não tem o que renovar — o botão fica apagado.</summary>
    public bool TemRenovacao => Status.UsaConsulta;

    public static LinhaConsulta De(StatusConsultaPaciente s) => new()
    {
        Status = s,
        Paciente = s.PacienteNome,
        Convenio = CatalogoConvenios.Nome(s.Convenio),
        UltimaEmissao = s.UltimaEmissao?.ToString("dd/MM/yyyy") ?? "nunca emitida",
        Vencimento = s.Vencimento?.ToString("dd/MM/yyyy") ?? "—",

        // A frase do modelo (AvisoRenovacao) é a mesma que a agenda e o balcão mostram —
        // é ela que impede o faturamento e a suíte de escreverem duas versões da mesma
        // cobrança. Quando não há o que renovar, ela é nula e a linha diz o estado.
        Situacao = s.AvisoRenovacao
                   ?? (!s.UsaConsulta ? "O convênio não usa consulta renovável."
                       : s.UltimaEmissao is null ? "Nunca emitiu consulta."
                       : "Em dia.")
    };
}

/// <summary>
/// Consultas renováveis: quem está vencido, quem vence logo, e o botão que renova.
///
/// Veio do app de FATURAMENTO na parcela 46, junto do lançamento de atendimento. O motivo
/// não é arrumação de menu: <b>renovar a consulta é uma assinatura, e assinatura se colhe
/// com o paciente presente</b>. A tela morava no posto do faturamento, que é onde a pessoa
/// NÃO está — a recepção via o selo "consulta a renovar" na agenda desde a parcela 44 e não
/// tinha por onde renovar. Descobrir a consulta vencida na hora de faturar é ligar para quem
/// já foi embora, com a guia recusada na mão.
///
/// Ela não deixou de existir para o faturamento: <see cref="ConsultaService"/> é
/// compartilhado, e a consulta renovada aqui é a MESMA linha que
/// <c>PendenciaService.ConsultasAVencerAsync</c> lê do outro lado. O que mudou foi a porta.
/// </summary>
public partial class ConsultasViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaConsulta> Consultas { get; } = new();

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _carregando;

    /// <summary>
    /// A leitura FALHOU — o terceiro estado. Sem ele, lista vazia por erro fica idêntica a
    /// lista vazia por não haver nada a renovar, e a recepção conclui que está tudo em dia.
    /// </summary>
    [ObservableProperty] private bool _naoVerificado;

    [ObservableProperty] private string _resumo = string.Empty;

    /// <summary>Quantos precisam renovar HOJE — o número que decide se vale abrir a tela.</summary>
    [ObservableProperty] private int _totalAlerta;

    /// <summary>
    /// Metade VISÍVEL da permissão. Renovar consulta CRIA a linha que sustenta o
    /// faturamento do convênio, e por isso pede o mesmo bit de lançar atendimento — os
    /// dois são "o que habilita faturar", e separá-los daria à direção duas caixinhas
    /// para a mesma decisão.
    /// </summary>
    public bool PodeRenovar => SessaoUsuario.Atual.Pode(Permissao.LancarAtendimento);

    public ConsultasViewModel(IServiceScopeFactory escopos, IDialogoService dialogo)
    {
        _escopos = escopos;
        _dialogo = dialogo;
    }

    public Task CarregarAsync() => RecarregarAsync();

    [RelayCommand]
    public async Task RecarregarAsync()
    {
        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = null;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ConsultaService>();
            var hoje = DateOnly.FromDateTime(DateTime.Today);

            var lista = await service.ListarAsync(hoje);

            Consultas.Clear();
            // Quem precisa renovar primeiro: a lista existe para agir, e ordenar por nome
            // faria a recepção varrer trinta linhas em dia para achar as três que pedem
            // assinatura.
            foreach (var c in lista.OrderByDescending(c => c.PrecisaRenovar)
                                   .ThenBy(c => c.DiasParaVencer ?? int.MaxValue)
                                   .ThenBy(c => c.PacienteNome))
                Consultas.Add(LinhaConsulta.De(c));

            TotalAlerta = lista.Count(c => c.PrecisaRenovar);
            Resumo = Consultas.Count == 0
                ? "Nenhum paciente de convênio com consulta renovável."
                : TotalAlerta == 0
                    ? $"{Consultas.Count} paciente(s) — nenhum precisa renovar hoje."
                    : $"{TotalAlerta} de {Consultas.Count} paciente(s) precisam renovar a consulta.";
        }
        catch (Exception ex)
        {
            NaoVerificado = true;
            LogSuite.Registrar("Consultas — a situação não pôde ser lida", ex);
            Erro($"Não foi possível ler as consultas: {ex.Message}");
        }
        finally
        {
            Carregando = false;
        }
    }

    /// <summary>
    /// Gera/renova a consulta do paciente para hoje. A validade sai da configuração do
    /// convênio (<see cref="ConsultaService"/>), não daqui.
    /// </summary>
    [RelayCommand]
    private async Task RenovarAsync(LinhaConsulta? linha)
    {
        if (linha is null) return;
        var item = linha.Status;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.LancarAtendimento, "renovar a consulta");

            // Convênio que não usa consulta renovável não tem o que renovar. É AVISO e não
            // erro: quem clicou não fez nada de errado, só escolheu a linha que não pede
            // ação — e a linha aparece na lista porque a lista é de todos os pacientes.
            if (!item.UsaConsulta)
            {
                Info($"{item.PacienteNome}: o convênio não usa consulta renovável.");
                return;
            }

            if (!_dialogo.Confirmar("Renovar consulta",
                    $"Gerar/renovar a consulta de {item.PacienteNome} para hoje?")) return;

            using var scope = _escopos.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ConsultaService>();
            await service.RenovarAsync(item.PacienteId, DateOnly.FromDateTime(DateTime.Today));

            Info($"Consulta de {item.PacienteNome} renovada.");
        }
        catch (Exception ex)
        {
            LogSuite.Registrar("Consultas — renovação não pôde ser registrada", ex);
            Erro(ex.Message);
            return;
        }

        await RecarregarAsync();
    }

    private void Erro(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = true;
    }

    private void Info(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = false;
    }
}
