using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Configuracao;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>Um horário parado, como a fila o mostra.</summary>
public sealed partial class LinhaConciliacao : ObservableObject
{
    public required int AgendamentoId { get; init; }
    public required string Paciente { get; init; }
    public required DateTime Quando { get; init; }
    public required bool Importado { get; init; }
    public required string Profissional { get; init; }
    public required int DiasParado { get; init; }
    public required bool TemSessaoNoDia { get; init; }
    public required string Situacao { get; init; }

    /// <summary>Some da lista assim que é resolvida — sem esperar a recarga.</summary>
    [ObservableProperty] private bool _resolvida;

    [ObservableProperty] private string? _desfecho;

    public string Cabecalho => $"{Quando:dd/MM/yyyy HH:mm} · {Paciente}";

    public string Origem => Importado ? "Importado do sistema anterior" : "Marcado aqui";

    public string Parado => DiasParado == 1 ? "parado há 1 dia" : $"parado há {DiasParado} dias";

    /// <summary>
    /// A ação de LANÇAR só é oferecida quando não há sessão no dia. Com sessão, lançar
    /// criaria um SEGUNDO jogo de guias para a mesma sessão — a duplicata que esta tela
    /// existe para acabar. É a guarda mais importante desta ViewModel.
    /// </summary>
    public bool PodeLancar => !TemSessaoNoDia && !Resolvida;

    public bool PodeMarcarFalta => !Resolvida;

    partial void OnResolvidaChanged(bool value)
    {
        OnPropertyChanged(nameof(PodeLancar));
        OnPropertyChanged(nameof(PodeMarcarFalta));
    }
}

/// <summary>Um horário que diz "Finalizado" e não aponta para atendimento nenhum.</summary>
public sealed class LinhaOrfa
{
    public required int AgendamentoId { get; init; }
    public required string Paciente { get; init; }
    public required DateTime Quando { get; init; }
    public required string Profissional { get; init; }

    public string Cabecalho => $"{Quando:dd/MM/yyyy HH:mm} · {Paciente}";
}

/// <summary>
/// A CONCILIAÇÃO DA AGENDA (parcela 93) — a fila de "isto aqui ficou pendurado".
///
/// A clínica ainda não trabalha o check-in pela agenda: a recepcionista vai direto ao Novo
/// atendimento e lança. Desde a parcela 91 o lançamento reconhece o horário DO DIA e nasce
/// pendurado nele — mas só quando a data bate, e a agenda importada do Smart Clinic trouxe
/// centenas de horários que nunca terão check-in aqui. Passada a carência, cada um deles é
/// uma pergunta que só quem estava no balcão responde.
///
/// ⚠️ A pergunta tem TRÊS respostas, e a do meio é a que mais aparece:
/// <list type="number">
/// <item><b>Faltou</b> — vira falta de verdade, e alimenta os indicadores.</item>
/// <item><b>Aconteceu e ninguém lançou</b> — lança retroativo PELO HORÁRIO, que já data o
/// atendimento pela data dele (a guia nasce com a data da sessão, não com a de hoje).</item>
/// <item><b>Aconteceu e já foi lançada por fora</b> — a linha DIZ qual é o atendimento, e o
/// botão de lançar fica APAGADO. Lançar aqui geraria um segundo jogo de guias para a mesma
/// sessão. Encerrar o horário apontando para a sessão que já existe pede um
/// <c>StatusAgendamento</c> novo (nem "cancelado", que inflaria o indicador de
/// cancelamento com sessões que aconteceram, nem "faltou", que culparia o paciente) — e
/// isso é a parcela seguinte. Até lá, a tela IMPEDE o estrago em vez de fingir que
/// resolve.</item>
/// </list>
/// </summary>
public sealed partial class ConciliacaoAgendaViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly IDialogoService _dialogo;

    public ConciliacaoAgendaViewModel(IServiceScopeFactory escopos, IDialogoService dialogo)
    {
        _escopos = escopos;
        _dialogo = dialogo;
    }

    public ObservableCollection<LinhaConciliacao> Parados { get; } = [];

    public ObservableCollection<LinhaOrfa> Orfaos { get; } = [];

    [ObservableProperty] private string _resumo = string.Empty;

    [ObservableProperty] private string _periodo = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemMensagem))]
    private string? _mensagem;

    [ObservableProperty] private bool _mensagemEhErro;

    public bool TemMensagem => !string.IsNullOrWhiteSpace(Mensagem);

    [ObservableProperty] private bool _carregando;

    [ObservableProperty] private bool _ocupado;

    /// <summary>
    /// A leitura FALHOU — o terceiro estado. Lista vazia por erro é idêntica a lista vazia
    /// por não haver nada pendurado, e as duas levam a conclusões opostas.
    /// </summary>
    [ObservableProperty] private bool _naoVerificado;

    public bool TemParados => Parados.Count > 0;

    public bool TemOrfaos => Orfaos.Count > 0;

    /// <summary>Metade VISÍVEL da permissão; quem impede é o <c>Exigir</c> nos comandos.</summary>
    public bool PodeResolver =>
        SessaoUsuario.Atual.Pode(Permissao.EditarAgenda)
        && SessaoUsuario.Atual.Pode(Permissao.LancarAtendimento);

    [RelayCommand]
    public async Task CarregarAsync()
    {
        Carregando = true;
        NaoVerificado = false;
        LimparMensagem();
        try
        {
            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<ConciliacaoAgendaService>();
            var c = await servico.LevantarAsync(DateOnly.FromDateTime(DateTime.Today));

            // Monta fora e publica de uma vez: entre o Clear() e o último Add não pode
            // haver await.
            var parados = c.Parados.Select(p => new LinhaConciliacao
            {
                AgendamentoId = p.AgendamentoId,
                Paciente = p.Paciente,
                Quando = p.DataHora,
                Importado = p.Importado,
                Profissional = string.IsNullOrWhiteSpace(p.Profissional) ? "—" : p.Profissional,
                DiasParado = p.DiasParado,
                TemSessaoNoDia = p.TemSessaoNoDia,
                Situacao = p.Situacao
            }).ToList();

            var orfaos = c.Orfaos.Select(o => new LinhaOrfa
            {
                AgendamentoId = o.AgendamentoId,
                Paciente = o.Paciente,
                Quando = o.DataHora,
                Profissional = string.IsNullOrWhiteSpace(o.Profissional) ? "—" : o.Profissional
            }).ToList();

            Parados.Clear();
            foreach (var l in parados) Parados.Add(l);
            Orfaos.Clear();
            foreach (var o in orfaos) Orfaos.Add(o);

            Periodo = $"De {c.Desde:dd/MM/yyyy} a {c.Ate:dd/MM/yyyy}.";
            Resumo = c.Vazio
                ? "Nada pendurado entre a agenda e os atendimentos."
                : $"{c.Parados.Count} horário(s) parado(s) — {c.ComSessaoNoDia} com sessão já lançada no dia, "
                  + $"{c.SemSessaoNoDia} sem sessão nenhuma."
                  + (c.Orfaos.Count > 0
                      ? $" E {c.Orfaos.Count} horário(s) marcado(s) como realizados SEM atendimento."
                      : string.Empty);
        }
        catch (Exception ex)
        {
            LogSuite.Registrar("Conciliação da agenda — levantamento falhou", ex);
            Parados.Clear();
            Orfaos.Clear();
            NaoVerificado = true;
            Resumo = string.Empty;
            Avisar("Não foi possível levantar a conciliação agora. A lista abaixo NÃO está "
                   + "vazia por não haver pendência — ela não pôde ser lida.", erro: true);
        }
        finally
        {
            Carregando = false;
            OnPropertyChanged(nameof(TemParados));
            OnPropertyChanged(nameof(TemOrfaos));
        }
    }

    /// <summary>Resposta (1): o paciente não veio.</summary>
    [RelayCommand]
    private async Task MarcarFaltaAsync(LinhaConciliacao? linha)
    {
        if (linha is null || Ocupado) return;
        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "marcar falta");

        if (!_dialogo.Confirmar("Marcar falta?",
                $"{linha.Paciente} — horário de {linha.Quando:dd/MM/yyyy HH:mm}.\n\n"
                + "Marcar como FALTA registra que o paciente não veio: entra nos indicadores "
                + "de falta e no histórico de relacionamento dele.\n\nConfirma?")) return;

        await ExecutarAsync(linha, async servicos =>
        {
            await servicos.GetRequiredService<AgendaService>()
                .MarcarFaltaAsync(linha.AgendamentoId, SessaoUsuario.Atual.Operador);
            return "Falta registrada.";
        });
    }

    /// <summary>
    /// Resposta (2): a sessão aconteceu e ninguém a lançou. O lançamento é PELO HORÁRIO —
    /// e é isso que faz o atendimento nascer datado do dia da sessão, e não de hoje.
    /// </summary>
    [RelayCommand]
    private async Task LancarRetroativoAsync(LinhaConciliacao? linha)
    {
        if (linha is null || Ocupado) return;
        SessaoUsuario.Atual.Exigir(
            Permissao.EditarAgenda | Permissao.LancarAtendimento, "lançar o atendimento");

        // A guarda que vale por toda a tela. O botão já nasce apagado (`PodeLancar`), mas
        // atalho de teclado e corrida de recarga passam pela primeira barreira.
        if (linha.TemSessaoNoDia)
        {
            Avisar($"{linha.Paciente} JÁ tem sessão lançada em {linha.Quando:dd/MM/yyyy}. "
                   + "Lançar de novo criaria um segundo jogo de guias para a mesma sessão.",
                erro: true);
            return;
        }

        if (!_dialogo.ConfirmarPerigo("Lançar atendimento retroativo?",
                $"{linha.Paciente} — horário de {linha.Quando:dd/MM/yyyy HH:mm}.\n\n"
                + "O atendimento nasce datado do DIA DO HORÁRIO, não de hoje — é a data em "
                + "que a sessão aconteceu. As guias nascem com a data prevista daquele dia, "
                + "então já entram no painel de pendências do faturamento.\n\n"
                + "A modalidade usada é a prevista no horário. Se ela estiver errada, "
                + "corrija pela agenda antes.\n\nLançar mesmo assim?")) return;

        await ExecutarAsync(linha, async servicos =>
        {
            var (_, lancamento) = await servicos.GetRequiredService<AgendaService>()
                .LancarNoHorarioAsync(
                    linha.AgendamentoId, null, operador: SessaoUsuario.Atual.Operador);
            return $"Atendimento nº {lancamento.Atendimento.Numero} lançado com "
                   + $"{lancamento.Atendimento.Codigos.Count} código(s).";
        });
    }

    /// <summary>
    /// Envelope comum: uma linha por vez, erro vira mensagem na tela (nunca derruba), e a
    /// linha resolvida some da fila sem esperar a recarga.
    /// </summary>
    private async Task ExecutarAsync(
        LinhaConciliacao linha, Func<IServiceProvider, Task<string>> acao)
    {
        Ocupado = true;
        try
        {
            using var scope = _escopos.CreateScope();
            var desfecho = await acao(scope.ServiceProvider);

            linha.Desfecho = desfecho;
            linha.Resolvida = true;
            Avisar(desfecho);
        }
        catch (Exception ex)
        {
            LogSuite.Registrar("Conciliação da agenda — ação falhou", ex);
            Avisar($"Não foi possível concluir: {ex.Message}", erro: true);
        }
        finally
        {
            Ocupado = false;
        }
    }

    private void Avisar(string texto, bool erro = false)
    {
        Mensagem = texto;
        MensagemEhErro = erro;
    }

    private void LimparMensagem()
    {
        Mensagem = null;
        MensagemEhErro = false;
    }
}
