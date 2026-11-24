using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using Clinica.Domain.Entities;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Clinica.Ui.Blazor.Telas;

/// <summary>
/// A v3 E A v4 NA TELA (nov/2026, F3 — contrato da F2): reação, ★, fixar, etiquetas, "escrevendo", endereço/contato/serviço,
/// enviar mais tarde, pílulas do @SemDor, "Tentar de novo", saudação, colar print e arrastar arquivo, e as mensagens
/// automáticas compactas (pedido do dono, 24/11). O que cada recurso AFIRMA vem de <see cref="ConversasNaTela"/> e
/// <see cref="MenuDoWhatsApp"/>; aqui, as duas barreiras, o escopo por operação e o desenho. CRM antigo (404) = recurso
/// escondido. "digitando…" ao paciente e transcrição seguem DESLIGADOS (decisão do dono): nada deles aparece quando falsos.
/// </summary>
public partial class ConversasDoWhatsApp
{
    private AppLabel[] _etiquetas = [];
    private AppPins? _fixadas;
    private AppShareOptions? _compartilhar;
    private string? _etiquetaDoFiltro;
    private readonly HashSet<string> _automaticasAbertas = new(StringComparer.Ordinal);
    private string? _reagindoA;
    private AcaoDoClipe? _escolhendo;
    private (string Conversa, DateTimeOffset Ultima, string[] Opcoes)? _sugestoes;
    private bool _pedindoSugestoes;
    private DateTimeOffset _ultimoPulso;
    private string? _quandoAgendar;
    private AppGreeting? _saudacao;
    private int _achadoAtual, _contaAchados;
    private DotNetObjectReference<ConversasDoWhatsApp>? _refDaTela;

    private static bool PodeSaudacao => Pode(Permissao.GerenciarCampanhas);

    // ---------------------------------------------------------------- leitura (uma vez por leitura inteira)

    /// <summary>Etiquetas da clínica e as fixadas de quem entrou. CRM antigo (404) ou fora do ar: o recurso some, a aba segue.</summary>
    private async Task LerRecursosAsync()
    {
        try
        {
            using var escopo = Escopo();
            var s = escopo.ServiceProvider.GetRequiredService<WhatsAppDaRecepcaoService>();
            try { _etiquetas = await s.EtiquetasAsync(); } catch (Exception ex) when (ex is RecusaDoCrm or CrmIndisponivel) { _etiquetas = []; }
            try { _fixadas = await s.FixadasAsync(); } catch (Exception ex) when (ex is RecusaDoCrm or CrmIndisponivel) { _fixadas = null; }
        }
        catch (Exception ex) { Clinica.Application.Diagnostico.Registrar("Blazor — WhatsApp: etiquetas e fixadas", ex); }
    }

    /// <summary>As linhas da lista com as fixadas no topo (Tudo e Minhas) e o filtro da etiqueta escolhida.</summary>
    private IReadOnlyList<LinhaDaConversa> LinhasComRecursos(IReadOnlyList<LinhaDaConversa> linhas, PainelDoWhatsApp painel)
    {
        if (_etiquetaDoFiltro is { } e)
        {
            var com = ConversasNaTela.IdsComEtiqueta(painel.Conversas, e);
            linhas = linhas.Where(l => com.Contains(l.Id)).ToList();
        }
        return _fixadas is { } f && _filtro is FiltroDasConversas.Todas or FiltroDasConversas.Minhas
            ? ConversasNaTela.FixadasPrimeiro(linhas, l => l.Id, f.ConversationIds) : linhas;
    }

    private bool Fixada(string id) => _fixadas?.ConversationIds.Contains(id) == true;

    private string? RascunhoNaLista(string id)
        => _aberta?.Id == id || !_rascunhos.TryGetValue(id, out var r) || r.Nota ? null : ConversasNaTela.PreviaDoRascunho(r.Texto);

    // ---------------------------------------------------------------- fixar e etiquetas

    private async Task FixarAsync(string conversaId, bool fixar)
    {
        try
        {
            using var escopo = Escopo();
            _fixadas = await escopo.ServiceProvider.GetRequiredService<WhatsAppDaRecepcaoService>().FixarAsync(conversaId, fixar);
            Notificacoes.Info(fixar ? "Fixada no topo da sua lista." : "Desafixada.");
        }
        catch (RecusaDoCrm ex) { Recado(ex.Message, "atencao"); }
        catch (CrmIndisponivel ex) { PerdeuLigacao(ex); Recado(ConversasNaTela.FalhaNaEscrita(ex, "Nada foi alterado."), "erro"); }
    }

    private Task AlternarEtiquetaAsync(ConversaDoWhatsApp c, string etiqueta)
    {
        if (c.Conversa is not { } conversa) return Task.CompletedTask;
        var atuais = (conversa.Labels ?? []).ToList();
        if (!atuais.Remove(etiqueta)) atuais.Add(etiqueta);
        return GravarNoCrmAsync(s => s.EtiquetarAsync(conversa, atuais), null, "etiquetar a conversa");
    }

    // ---------------------------------------------------------------- reação e ★

    private void AbrirReacoes(string mensagemId)
    {
        _reagindoA = mensagemId;
        _popover = Popover.Reacoes;
    }

    private async Task ReagirAsync(ConversaDoWhatsApp c, string emoji)
    {
        if (_reagindoA is not { } id || c.Conversa is not { } conversa) return;
        _popover = Popover.Nenhum;
        var jaTem = conversa.Messages.FirstOrDefault(m => m.Id == id)?.Reactions?.Any(r => !r.FromPatient && r.Emoji == emoji) == true;
        await GravarNoCrmAsync(s => s.ReagirAsync(conversa, id, jaTem ? "" : emoji), null, "reagir à mensagem");
        _reagindoA = null;
    }

    private Task EstrelarAsync(ConversaDoWhatsApp c, ItemDaConversa m)
        => c.Conversa is { } conversa && m.Id is { } id
            ? GravarNoCrmAsync(s => s.EstrelarAsync(conversa, id, !m.Estrela), m.Estrela ? "Estrela tirada." : "Marcada como importante ★.", "marcar a mensagem")
            : Task.CompletedTask;

    // ---------------------------------------------------------------- "escrevendo" (o pulso a cada ~3 s)

    private void AoDigitar(ConversaDoWhatsApp c)
    {
        if (c.Conversa is not { } conversa || Rascunho(c).Nota || DateTimeOffset.Now - _ultimoPulso < TimeSpan.FromSeconds(3)) return;
        _ultimoPulso = DateTimeOffset.Now;
        _ = PulsarAsync(conversa.Id);
    }

    private async Task PulsarAsync(string conversaId)
    {
        try
        {
            using var escopo = Escopo();
            await escopo.ServiceProvider.GetRequiredService<WhatsAppDaRecepcaoService>().EscrevendoAsync(conversaId);
        }
        catch (Exception ex) when (ex is RecusaDoCrm or CrmIndisponivel or InvalidOperationException) { /* "escrevendo" é ajuda: falhou, segue */ }
    }

    // ---------------------------------------------------------------- endereço, contato e serviço

    private async Task AbrirMenuDoClipeComOpcoesAsync(ConversaDoWhatsApp c, MouseEventArgs e)
    {
        if (_compartilhar is null && !c.Sms)
        {
            try
            {
                using var escopo = Escopo();
                _compartilhar = await escopo.ServiceProvider.GetRequiredService<WhatsAppDaRecepcaoService>().OpcoesDoClipeAsync();
            }
            catch (Exception ex) when (ex is RecusaDoCrm or CrmIndisponivel) { _compartilhar = null; }
        }
        AbrirMenuDoClipe(c, e);
    }

    private Task CompartilharAsync(ConversaDoWhatsApp c, string tipo, string? itemId, string sucesso)
    {
        if (c.Conversa is not { } conversa) return Task.CompletedTask;
        _popover = Popover.Nenhum;
        _escolhendo = null;
        return GravarNoCrmAsync(s => s.CompartilharAsync(conversa, tipo, itemId, Guid.NewGuid().ToString("D")), sucesso, "enviar pelo WhatsApp da clínica");
    }

    private Task EnviarContatoAsync(ConversaDoWhatsApp c, AppContactCard k) => CompartilharAsync(c, "contact", k.Id, $"Contato de {k.Name} enviado.");

    private Task EnviarServicoAsync(ConversaDoWhatsApp c, AppService s) => CompartilharAsync(c, "service", s.Id, $"{s.Title} enviado.");

    // ---------------------------------------------------------------- enviar mais tarde

    private async Task AgendarAsync(ConversaDoWhatsApp c, DateTimeOffset quando)
    {
        var r = Rascunho(c);
        if (c.Conversa is not { } conversa || !r.TemConteudo) return;
        var texto = r.Texto;
        var anexo = r.Anexo?.Id;
        var nova = await GravarNoCrmAsync(s => s.AgendarAsync(conversa, texto, quando, anexo, r.IdDoEnvio),
            $"Agendada para {quando.ToString("dd/MM 'às' HH:mm", Formato.PtBr)}.", "agendar a mensagem");
        if (nova is null) return;
        r.Saiu();
        _popover = Popover.Nenhum;
        _quandoAgendar = null;
    }

    private Task AgendarNoCampoAsync(ConversaDoWhatsApp c)
        => DateTime.TryParse(_quandoAgendar, Formato.PtBr, System.Globalization.DateTimeStyles.None, out var d)
           || DateTime.TryParse(_quandoAgendar, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out d)
            ? AgendarAsync(c, new DateTimeOffset(d, WhatsAppDaRecepcaoService.Agora.Offset))
            : Task.CompletedTask;

    private Task CancelarAgendadaAsync(ConversaDoWhatsApp c, string agendada)
        => c.Conversa is { } conversa
            ? GravarNoCrmAsync(s => s.CancelarAgendamentoAsync(conversa, agendada), "Mensagem agendada cancelada.", "cancelar a mensagem agendada")
            : Task.CompletedTask;

    // ---------------------------------------------------------------- pílulas do @SemDor

    /// <summary>
    /// Pede as 2–3 respostas curtas quando a última fala é do paciente e a janela está aberta — uma vez por mensagem dele.
    /// Nunca envia: a escolhida vai para o campo.
    /// </summary>
    private async Task PedirSugestoesAsync(ConversaDoWhatsApp c)
    {
        if (!SemDorLigado || _pedindoSugestoes || c.Conversa is not { } conversa || !c.Janela.PodeEscreverLivre
            || conversa.Messages.LastOrDefault(m => string.IsNullOrEmpty(m.ReactionTo)) is not { Direction: "in" }
            || _sugestoes is { } s && s.Conversa == conversa.Id && s.Ultima == conversa.LastIncoming) return;
        _pedindoSugestoes = true;
        try
        {
            using var escopo = Escopo();
            var opcoes = await escopo.ServiceProvider.GetRequiredService<WhatsAppDaRecepcaoService>().OpcoesDoSemDorAsync(conversa);
            if (Descartada || _aberta?.Conversa?.Id != conversa.Id) return;
            _sugestoes = (conversa.Id, conversa.LastIncoming, opcoes.Options.Where(o => !string.IsNullOrWhiteSpace(o)).Take(3).ToArray());
            StateHasChanged();
        }
        catch (Exception ex) when (ex is RecusaDoCrm or CrmIndisponivel) { _sugestoes = (conversa.Id, conversa.LastIncoming, []); }
        finally { _pedindoSugestoes = false; }
    }

    private string[] SugestoesDa(ConversaDoWhatsApp c)
        => _sugestoes is { } s && c.Conversa is { } conversa && s.Conversa == conversa.Id && s.Ultima == conversa.LastIncoming
           && conversa.Messages.LastOrDefault(m => string.IsNullOrEmpty(m.ReactionTo)) is { Direction: "in" } && string.IsNullOrWhiteSpace(Rascunho(c).Texto)
            ? s.Opcoes : [];

    // ---------------------------------------------------------------- a saudação (só a gestão)

    private async Task AbrirSaudacaoAsync()
    {
        if (!Exigir(Permissao.GerenciarCampanhas, "mudar a saudação")) return;
        try
        {
            using var escopo = Escopo();
            _saudacao = await escopo.ServiceProvider.GetRequiredService<WhatsAppDaRecepcaoService>().SaudacaoAsync();
        }
        catch (RecusaDoCrm ex) when (ex.Status == 404) { Notificacoes.Info("Este CRM ainda não tem a saudação. Ela chega com a próxima versão dele."); }
        catch (Exception ex) when (ex is RecusaDoCrm or CrmIndisponivel) { Notificacoes.Erro(ConversasNaTela.FalhaNaEscrita(ex, "A saudação não pôde ser lida.")); }
    }

    private async Task<string?> SalvarSaudacaoAsync(bool ligada, string texto)
    {
        if (_saudacao is not { } atual || !Exigir(Permissao.GerenciarCampanhas, "mudar a saudação")) return "Seu acesso não permite mudar a saudação.";
        try
        {
            using var escopo = Escopo();
            await escopo.ServiceProvider.GetRequiredService<WhatsAppDaRecepcaoService>().SalvarSaudacaoAsync(atual with { Enabled = ligada, Text = texto });
            _saudacao = null;
            Notificacoes.Sucesso(ligada ? "Saudação salva e ligada." : "Saudação salva e desligada.");
            StateHasChanged();
            return null;
        }
        catch (Exception ex) when (ex is RecusaDoCrm or InvalidOperationException) { return ex.Message; }
        catch (CrmIndisponivel ex) { return ConversasNaTela.FalhaNaEscrita(ex, "Nada foi alterado; tente de novo em instantes."); }
    }

    // ---------------------------------------------------------------- "Tentar de novo"

    private Task TentarDeNovoAsync(ConversaDoWhatsApp c, ItemDaConversa m)
        => c.Conversa is { } conversa && m.Id is { } id
            ? GravarNoCrmAsync(s => s.TentarDeNovoAsync(conversa, id), "Enviada de novo.", "enviar de novo")
            : Task.CompletedTask;

    // ---------------------------------------------------------------- colar print e arrastar arquivo (js/conversa-do-whatsapp.js)

    /// <summary>O print colado (Ctrl+V) ou o arquivo arrastado: vira o anexo do rascunho, como o "Documento, foto ou áudio".</summary>
    [JSInvokable]
    public async Task ColarArquivo(string nome, string mime, IJSStreamReference arquivo)
    {
        if (_aberta is not { Conversa: { } conversa } c || c.Sms || _painel?.Sessao.AttachmentsEnabled != true
            || !Exigir(Permissao.EditarAgenda, "anexar um arquivo")) return;
        _ocupado = true;
        try
        {
            await using var fluxo = await arquivo.OpenReadStreamAsync(16 * 1024 * 1024);
            using var copia = new MemoryStream();
            await fluxo.CopyToAsync(copia);
            copia.Position = 0;
            using var escopo = Escopo();
            Rascunho(c).Anexo = await escopo.ServiceProvider.GetRequiredService<WhatsAppDaRecepcaoService>()
                .AnexarAsync(conversa, copia, string.IsNullOrWhiteSpace(nome) ? "print.png" : nome, string.IsNullOrWhiteSpace(mime) ? Mime(nome) : mime);
            _focarCampo = true;
        }
        catch (Exception ex) when (ex is RecusaDoCrm or InvalidOperationException) { Recado(ex.Message, "erro"); }
        catch (CrmIndisponivel ex) { PerdeuLigacao(ex); Recado(ConversasNaTela.FalhaNaEscrita(ex, "O arquivo não foi anexado; tente de novo."), "erro"); }
        catch (IOException) { Recado("O arquivo passa de 16 MB, o limite do WhatsApp.", "erro"); }
        finally { _ocupado = false; StateHasChanged(); }
    }

    private async Task LigarColarEArrastarAsync(IJSObjectReference modulo)
    {
        _refDaTela ??= DotNetObjectReference.Create(this);
        await modulo.InvokeVoidAsync("ligarArquivos", _refDaTela);
    }

    // ---------------------------------------------------------------- busca ↑↓

    private async Task IrAoAchadoAsync(int passo, int total)
    {
        if (total == 0) return;
        _achadoAtual = ((_achadoAtual + passo) % total + total) % total;
        StateHasChanged();
        try
        {
            var modulo = await JS.InvokeAsync<IJSObjectReference>("import", ModuloDaConversa);
            await modulo.InvokeVoidAsync("irAoAchado");
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException) { }
    }
}
