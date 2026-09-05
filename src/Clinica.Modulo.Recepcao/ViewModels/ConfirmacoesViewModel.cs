using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>Uma sessão a confirmar, como a lista da rodada a mostra.</summary>
public sealed partial class LinhaConfirmacao : ObservableObject
{
    public required int ContatoId { get; init; }
    public required string Paciente { get; init; }
    public required string NomeCru { get; init; }
    public required string Horario { get; init; }
    public required DateTime Quando { get; init; }
    public required string? TelefoneCru { get; init; }
    public required string? EmailCru { get; init; }
    public required string Profissional { get; init; }

    /// <summary>Muda na tela assim que o WhatsApp abre — sem esperar recarregar a lista.</summary>
    [ObservableProperty] private string _situacao = "a enviar";

    [ObservableProperty] private bool _enviado;

    public bool TemTelefone => !string.IsNullOrWhiteSpace(TelefoneCru);

    /// <summary>Tem e-mail VÁLIDO na ficha — é quem o lembrete automático alcança.</summary>
    public bool TemEmail => EnderecoDeEmail.Valido(EmailCru);
}

/// <summary>
/// A rodada de confirmação das sessões de amanhã, DENTRO da Recepção.
///
/// A capacidade existia desde a parcela 5 e morava só no Gerente Geral: descobrir quem
/// confirmar, escrever a mensagem, aplicar a LGPD e não repetir ninguém. Só que quem
/// confirma as sessões de amanhã é a recepcionista — e o app dela não abria essa tela.
/// Na Recepção sobrava o clique um a um, no dia que estivesse aberto na agenda.
///
/// Não é uma segunda implementação: chama o mesmo <see cref="CampanhaService"/>, com a
/// mesma chave de idempotência. Rodar aqui e no Gerente no mesmo dia não manda duas
/// mensagens para o mesmo paciente.
///
/// **Confirmar a própria sessão é transacional** e não exige consentimento de marketing:
/// o paciente pediu o horário, e avisar sobre ele não é propaganda. NPS e recall, que
/// exigem, continuam só no Gerente.
///
/// **Gerar e enviar são passos separados**: gerar é o trabalho automático; enviar é um
/// clique por paciente, porque o número é o WhatsApp da clínica e disparo em massa por
/// ali termina com o número bloqueado.
/// </summary>
public sealed partial class ConfirmacoesViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;

    public ObservableCollection<LinhaConfirmacao> Contatos { get; } = [];

    /// <summary>Amanhã por padrão: é a véspera que evita o buraco na agenda.</summary>
    [ObservableProperty] private DateTime _dia = DateTime.Today.AddDays(1);

    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _carregando;

    /// <summary>Metade visível da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeEditar => SessaoUsuario.Atual.Pode(Permissao.EditarAgenda);

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): trocar o dia dispara uma carga por
    /// mudança, e a resposta do dia anterior pode chegar depois da do novo — a lista diria
    /// "avisado" sobre as sessões do dia errado. Só a carga mais nova escreve na tela.
    /// </summary>
    private int _geracaoCarga;

    public ConfirmacoesViewModel(IServiceScopeFactory escopos)
    {
        _escopos = escopos;
        _ = CarregarAsync();
    }

    partial void OnDiaChanged(DateTime value) => _ = CarregarAsync();

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        Carregando = true;
        try
        {
            Mensagem = null;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var campanhas = scope.ServiceProvider.GetRequiredService<CampanhaService>();

            var dia = DateOnly.FromDateTime(Dia);
            var contatos = await campanhas.ContatosAsync(
                TipoContato.ConfirmacaoSessao, null, dia, dia);

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Contatos.Clear();
            foreach (var c in contatos.OrderBy(c => c.Agendamento?.DataHora))
            {
                var quando = c.Agendamento?.DataHora ?? Dia.Date;
                Contatos.Add(new LinhaConfirmacao
                {
                    ContatoId = c.Id,
                    Paciente = c.Paciente?.Nome ?? "(paciente removido)",
                    NomeCru = c.Paciente?.Nome ?? string.Empty,
                    Horario = quando.ToString("HH:mm"),
                    Quando = quando,
                    TelefoneCru = c.Paciente?.Telefone,
                    EmailCru = c.Paciente?.Email,
                    Profissional = c.Agendamento?.Profissional?.Rotulo ?? "—",
                    // Quem já foi contatado aparece marcado em vez de sumir: a lista é o
                    // retrato da rodada, e sumir com o enviado esconderia o que já rodou.
                    Situacao = Descrever(c),
                    Enviado = c.EnviadoEm is not null
                });
            }

            var enviados = Contatos.Count(c => c.Enviado);
            var semTelefone = Contatos.Count(c => !c.TemTelefone);
            var comEmail = Contatos.Count(c => c.TemEmail);

            // Quantos o e-mail alcança entra no resumo: é o número que diz se vale a pena
            // clicar em "Enviar e-mails" — e, quando é zero, por que o botão não fez nada.
            Resumo = Contatos.Count == 0
                ? "Nenhuma sessão gerada para este dia. Clique em \"Gerar rodada\"."
                : $"{Contatos.Count} sessão(ões) · {enviados} já avisada(s) · {comEmail} com e-mail"
                  + (semTelefone > 0 ? $" · {semTelefone} SEM TELEFONE no cadastro." : ".");
        }
        catch (Exception ex)
        {
            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Clinica.Application.Diagnostico.Registrar(
                "Recepção — rodada de confirmação não pôde ser lida", ex);
            Erro($"Não foi possível ler a rodada: {ex.Message}");
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    private static string Descrever(ContatoCampanha c) => c.Status switch
    {
        StatusContato.Respondido => "respondeu",
        StatusContato.Dispensado => "dispensado",
        // O CANAL entra na frase: "avisado por e-mail" e "avisado pelo WhatsApp" são
        // respostas diferentes para "vale mandar de novo pelo outro?".
        _ => c.EnviadoEm is { } quando
            ? $"avisado em {quando:dd/MM HH:mm}{Canal(c.Canal)}"
            : "a enviar"
    };

    private static string Canal(CanalContato canal) => canal switch
    {
        CanalContato.Email => " por e-mail",
        CanalContato.Telefone => " por telefone",
        CanalContato.Presencial => " no balcão",
        _ => string.Empty
    };

    /// <summary>
    /// Manda o lembrete por e-mail a quem ainda está pendente e tem endereço — o MESMO
    /// serviço que roda na abertura do app (set/2026). O botão existe para o dia em que a
    /// abertura não rodou (o app ficou aberto desde ontem) ou em que alguém cadastrou o
    /// e-mail depois. Desligado, a frase diz onde ligar.
    /// </summary>
    [RelayCommand]
    private async Task EnviarEmailsAsync()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "enviar os lembretes por e-mail");

            using var scope = _escopos.CreateScope();
            var lembretes = scope.ServiceProvider.GetRequiredService<LembreteEmailService>();

            var r = await lembretes.EnviarConfirmacoesAsync(DateOnly.FromDateTime(Dia));

            // A frase vem DEPOIS do recarregar (a carga começa zerando `Mensagem`).
            await CarregarAsync();

            Mensagem = r.Descricao;
            MensagemEhErro = r.Desligado || r.TeveFalha;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — lembretes por e-mail não puderam ser enviados", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>
    /// Descobre quem tem sessão no dia e cria o contato de cada um. É idempotente:
    /// clicar duas vezes não gera contato repetido, porque a chave é o agendamento.
    /// </summary>
    [RelayCommand]
    private async Task GerarAsync()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "gerar a rodada de confirmação");

            using var scope = _escopos.CreateScope();
            var campanhas = scope.ServiceProvider.GetRequiredService<CampanhaService>();

            var r = await campanhas.GerarConfirmacoesAsync(DateOnly.FromDateTime(Dia));

            // A CONFIRMAÇÃO VEM DEPOIS DO RECARREGAR (a regra da ConsultasViewModel): a
            // carga começa zerando `Mensagem`, então escrita antes ela nunca aparecia.
            await CarregarAsync();

            Mensagem = r.Gerados > 0
                ? $"{r.Gerados} sessão(ões) para avisar."
                  + (r.JaExistiam > 0 ? $" {r.JaExistiam} já estavam na rodada." : string.Empty)
                : r.JaExistiam > 0
                    // "Nada a fazer" e "não rodou" precisam ser distinguíveis.
                    ? $"Nada novo — as {r.JaExistiam} sessão(ões) do dia já estavam na rodada."
                    : "Nenhuma sessão marcada para este dia.";
            MensagemEhErro = false;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — rodada de confirmação não pôde ser gerada", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>
    /// Abre o WhatsApp com a mensagem pronta e registra o envio.
    ///
    /// Um clique por paciente de propósito: o número é o da clínica, e disparo em massa
    /// automatizado por ali leva ao bloqueio — perder o canal inteiro para economizar
    /// cliques é mau negócio.
    /// </summary>
    [RelayCommand]
    private async Task EnviarAsync(LinhaConfirmacao? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "avisar o paciente");

            var erro = Whatsapp.Abrir(
                linha.TelefoneCru, linha.NomeCru,
                Whatsapp.ConfirmacaoDeSessao(linha.NomeCru, linha.Quando));

            if (erro is not null)
            {
                // Sem telefone não há o que registrar: marcar como enviado aqui faria a
                // rodada dizer que o paciente foi avisado quando ninguém falou com ele.
                Erro(erro);
                return;
            }

            using var scope = _escopos.CreateScope();
            var campanhas = scope.ServiceProvider.GetRequiredService<CampanhaService>();
            await campanhas.RegistrarEnvioAsync(linha.ContatoId, SessaoUsuario.Atual.Operador);

            linha.Enviado = true;
            linha.Situacao = $"avisado em {DateTime.Now:dd/MM HH:mm}";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — confirmação não pôde ser enviada", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>O paciente respondeu que vem — tira a linha da fila de cobrança.</summary>
    [RelayCommand]
    private async Task ConfirmouAsync(LinhaConfirmacao? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "registrar a resposta");

            using var scope = _escopos.CreateScope();
            var campanhas = scope.ServiceProvider.GetRequiredService<CampanhaService>();
            await campanhas.RegistrarRespostaAsync(linha.ContatoId, "confirmou");

            linha.Situacao = "respondeu";
            linha.Enviado = true;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — resposta não pôde ser registrada", ex);
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private void Amanha() => Dia = DateTime.Today.AddDays(1);

    private void Erro(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = true;
    }
}
