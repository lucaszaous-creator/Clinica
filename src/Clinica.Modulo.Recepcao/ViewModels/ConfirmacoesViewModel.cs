using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
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
    public required int? AgendamentoId { get; init; }
    public string Atendimento { get; init; } = "";
    public string Telefone => string.IsNullOrWhiteSpace(TelefoneCru) ? "sem telefone" : Clinica.Domain.Telefone.Formatar(TelefoneCru);

    /// <summary>Muda na tela assim que o WhatsApp abre — sem esperar recarregar a lista.</summary>
    [ObservableProperty] private string _situacao = "a enviar";

    /// <summary>"WhatsApp enviado" / "E-mail enviado" / "Não avisado" — a pílula da coluna Aviso.</summary>
    [ObservableProperty] private string _aviso = "Não avisado";

    [ObservableProperty, NotifyPropertyChangedFor(nameof(NaoAvisado), nameof(SemResposta), nameof(Respondeu), nameof(RotuloEnviar))]
    private SituacaoConfirmacao _estado;

    [ObservableProperty] private bool _enviado;

    public bool NaoAvisado => Estado == SituacaoConfirmacao.NaoAvisado;
    public bool SemResposta => Estado == SituacaoConfirmacao.AvisadoSemResposta;
    public bool Respondeu => Estado is SituacaoConfirmacao.Confirmou or SituacaoConfirmacao.PediuRemarcar or SituacaoConfirmacao.Dispensado;
    /// <summary>Quem já recebeu a mensagem é LEMBRADO; quem não recebeu é AVISADO.</summary>
    public string RotuloEnviar => NaoAvisado ? "Avisar" : "Lembrar de novo";

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

    /// <summary>A rodada inteira do dia — a tabela mostra o recorte do cartão escolhido.</summary>
    private List<LinhaConfirmacao> _todos = [];

    public ObservableCollection<LinhaConfirmacao> Contatos { get; } = [];

    /// <summary>Amanhã por padrão: é a véspera que evita o buraco na agenda.</summary>
    [ObservableProperty] private DateTime _dia = DateTime.Today.AddDays(1);

    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _carregando;

    // ===== O placar (os quatro cartões) =====
    [ObservableProperty] private int _confirmaram;
    [ObservableProperty] private int _semResposta;
    [ObservableProperty] private int _naoAvisados;
    [ObservableProperty] private int _pediramRemarcar;
    [ObservableProperty] private int _totalSessoes;
    [ObservableProperty] private int _percentualAvisado;
    [ObservableProperty] private double _fracaoAvisado;

    /// <summary>
    /// O cartão escolhido filtra a tabela (nulo = todas). Clicar no cartão já escolhido
    /// volta para todas — e o rodapé DIZ o recorte, senão a lista filtrada se lê como a
    /// rodada inteira.
    /// </summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(FiltroConfirmaram), nameof(FiltroSemResposta),
        nameof(FiltroNaoAvisados), nameof(FiltroRemarcar), nameof(RecorteTexto))]
    private SituacaoConfirmacao? _filtro;

    public bool FiltroConfirmaram => Filtro == SituacaoConfirmacao.Confirmou;
    public bool FiltroSemResposta => Filtro == SituacaoConfirmacao.AvisadoSemResposta;
    public bool FiltroNaoAvisados => Filtro == SituacaoConfirmacao.NaoAvisado;
    public bool FiltroRemarcar => Filtro == SituacaoConfirmacao.PediuRemarcar;
    public string RecorteTexto => Filtro switch
    {
        SituacaoConfirmacao.Confirmou => $"Mostrando: confirmaram ({Contatos.Count})",
        SituacaoConfirmacao.AvisadoSemResposta => $"Mostrando: avisados sem resposta ({Contatos.Count})",
        SituacaoConfirmacao.NaoAvisado => $"Mostrando: ainda não avisados ({Contatos.Count})",
        SituacaoConfirmacao.PediuRemarcar => $"Mostrando: pediram para remarcar ({Contatos.Count})",
        _ => $"Mostrando: todas as sessões ({Contatos.Count})"
    };

    public string TituloDoDia => RodadaDeConfirmacao.TituloDoDia(DateOnly.FromDateTime(Dia), DateOnly.FromDateTime(DateTime.Today));
    /// <summary>
    /// O rótulo do mockup aprovado. Cada clique abre o WhatsApp do PRÓXIMO que falta — o
    /// número é o da clínica, e disparo em massa automático por ali leva ao bloqueio.
    /// </summary>
    public string RotuloAvisarProximo => NaoAvisados == 0 ? "Todos avisados" : $"Avisar os que faltam · {NaoAvisados}";
    public bool TemQuemAvisar => NaoAvisados > 0 && PodeEditar;

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

    partial void OnDiaChanged(DateTime value) { OnPropertyChanged(nameof(TituloDoDia)); _ = CarregarAsync(); }
    partial void OnFiltroChanged(SituacaoConfirmacao? value) => Filtrar();
    partial void OnNaoAvisadosChanged(int value) { OnPropertyChanged(nameof(RotuloAvisarProximo)); OnPropertyChanged(nameof(TemQuemAvisar)); }

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

            var linhas = new List<LinhaConfirmacao>();
            foreach (var c in contatos.OrderBy(c => c.Agendamento?.DataHora))
            {
                var quando = c.Agendamento?.DataHora ?? Dia.Date;
                var linha = new LinhaConfirmacao
                {
                    ContatoId = c.Id,
                    AgendamentoId = c.AgendamentoId,
                    Paciente = c.Paciente?.Nome ?? "(paciente removido)",
                    NomeCru = c.Paciente?.Nome ?? string.Empty,
                    Horario = quando.ToString("HH:mm"),
                    Quando = quando,
                    TelefoneCru = c.Paciente?.Telefone,
                    EmailCru = c.Paciente?.Email,
                    Profissional = c.Agendamento?.Profissional?.Rotulo ?? "sem profissional",
                    Atendimento = c.Agendamento is { } a
                        ? CatalogoModalidades.NomeComEspecialidade(a.ModalidadeCodigo, a.ModalidadePrevista, a.EspecialidadeConsultaCodigo)
                        : "",
                    // Quem já foi contatado aparece marcado em vez de sumir: a lista é o
                    // retrato da rodada, e sumir com o enviado esconderia o que já rodou.
                    Situacao = Descrever(c),
                    Enviado = c.EnviadoEm is not null
                };
                linha.Estado = RodadaDeConfirmacao.Classificar(c.Status, c.EnviadoEm, c.Comentario);
                linha.Aviso = c.EnviadoEm is null ? "Não avisado" : RodadaDeConfirmacao.Canal(c.Canal);
                linhas.Add(linha);
            }
            _todos = linhas;
            Recontar();
            Filtrar();
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

    /// <summary>O placar sai SEMPRE da rodada inteira — nunca do recorte do cartão.</summary>
    private void Recontar()
    {
        Confirmaram = _todos.Count(l => l.Estado == SituacaoConfirmacao.Confirmou);
        SemResposta = _todos.Count(l => l.Estado == SituacaoConfirmacao.AvisadoSemResposta);
        NaoAvisados = _todos.Count(l => l.Estado == SituacaoConfirmacao.NaoAvisado);
        PediramRemarcar = _todos.Count(l => l.Estado == SituacaoConfirmacao.PediuRemarcar);
        var (baseCalc, _, pct) = RodadaDeConfirmacao.Progresso(_todos.Select(l => l.Estado));
        TotalSessoes = _todos.Count;
        PercentualAvisado = pct;
        FracaoAvisado = baseCalc == 0 ? 0 : pct / 100.0;

        var semTelefone = _todos.Count(c => !c.TemTelefone);
        var comEmail = _todos.Count(c => c.TemEmail);
        // Quantos o e-mail alcança entra no resumo: é o número que diz se vale a pena
        // clicar em "Enviar e-mails" — e, quando é zero, por que o botão não fez nada.
        Resumo = _todos.Count == 0
            ? "Nenhuma sessão gerada para este dia. Clique em \"Gerar rodada\"."
            : $"{_todos.Count} sessão(ões) · {comEmail} com e-mail"
              + (semTelefone > 0 ? $" · {semTelefone} SEM TELEFONE no cadastro." : ".");
    }

    /// <summary>Remonta a tabela do recorte. Entre o Clear e o último Add não há await.</summary>
    private void Filtrar()
    {
        Contatos.Clear();
        foreach (var l in _todos.Where(l => Filtro is null || l.Estado == Filtro)) Contatos.Add(l);
        OnPropertyChanged(nameof(RecorteTexto));
    }

    [RelayCommand]
    private void EscolherFiltro(string? qual)
    {
        SituacaoConfirmacao? novo = qual switch
        {
            "confirmaram" => SituacaoConfirmacao.Confirmou,
            "sem-resposta" => SituacaoConfirmacao.AvisadoSemResposta,
            "nao-avisados" => SituacaoConfirmacao.NaoAvisado,
            "remarcar" => SituacaoConfirmacao.PediuRemarcar,
            _ => null
        };
        Filtro = Filtro == novo ? null : novo;
    }

    [RelayCommand] private void DiaAnterior() => Dia = Dia.AddDays(-1);
    [RelayCommand] private void ProximoDia() => Dia = Dia.AddDays(1);

    private static string Descrever(ContatoCampanha c) => c.Status switch
    {
        StatusContato.Respondido => string.Equals(c.Comentario?.Trim(), RodadaDeConfirmacao.RespostaRemarcar, StringComparison.OrdinalIgnoreCase)
            ? "pediu para remarcar" : "confirmou",
        StatusContato.Dispensado => "dispensado",
        _ => c.EnviadoEm is { } quando
            ? $"{quando:dd/MM HH:mm}" + (string.IsNullOrWhiteSpace(c.EnviadoPor) ? "" : $" · por {c.EnviadoPor}")
            : "ainda não avisado"
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
            linha.Situacao = $"{DateTime.Now:dd/MM HH:mm} · por {SessaoUsuario.Atual.Operador}";
            linha.Aviso = RodadaDeConfirmacao.Canal(CanalContato.WhatsApp);
            if (linha.Estado == SituacaoConfirmacao.NaoAvisado) linha.Estado = SituacaoConfirmacao.AvisadoSemResposta;
            Recontar();
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

            linha.Situacao = "confirmou";
            linha.Enviado = true;
            linha.Estado = SituacaoConfirmacao.Confirmou;
            Recontar();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — resposta não pôde ser registrada", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>
    /// O paciente pediu para remarcar: registra a RESPOSTA (sai de "sem resposta" e entra no
    /// cartão "Pediram remarcar") e abre a janela do horário, onde se remarca — a mesma das
    /// listas da agenda, para remarcar não ganhar uma segunda porta com outra regra.
    /// </summary>
    [RelayCommand]
    private async Task RemarcarAsync(LinhaConfirmacao? linha)
    {
        if (linha is null) return;
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "registrar a resposta");
            using (var scope = _escopos.CreateScope())
            {
                var campanhas = scope.ServiceProvider.GetRequiredService<CampanhaService>();
                await campanhas.RegistrarRespostaAsync(linha.ContatoId, RodadaDeConfirmacao.RespostaRemarcar);
            }
            linha.Estado = SituacaoConfirmacao.PediuRemarcar;
            linha.Situacao = "pediu para remarcar";
            Recontar();

            if (linha.AgendamentoId is not { } agendamentoId) return;
            using var escopo = _escopos.CreateScope();
            var agenda = escopo.ServiceProvider.GetRequiredService<AgendaViewModel>();
            if (!await agenda.AbrirJanelaDoHorarioAsync(DateOnly.FromDateTime(linha.Quando), agendamentoId))
                Erro("A resposta foi registrada, mas o horário não está mais neste dia — ele pode ter sido remarcado em outra máquina.");
            else await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — pedido de remarcar não pôde ser registrado", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>
    /// Abre o WhatsApp do PRÓXIMO que ainda não foi avisado. Continua sendo um clique por
    /// paciente — o número é o WhatsApp da clínica, e disparo em massa leva ao bloqueio —,
    /// só que sem precisar caçar a linha: cada clique anda a fila.
    /// </summary>
    [RelayCommand]
    private async Task AvisarProximoAsync()
    {
        var proximo = _todos.FirstOrDefault(l => l.NaoAvisado && l.TemTelefone);
        if (proximo is null)
        {
            Erro(NaoAvisados > 0
                ? "Quem falta avisar está sem telefone no cadastro — ligue ou complete a ficha."
                : "Todas as sessões deste dia já foram avisadas.");
            return;
        }
        await EnviarAsync(proximo);
    }

    [RelayCommand]
    private void Amanha() => Dia = DateTime.Today.AddDays(1);

    private void Erro(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = true;
    }
}
