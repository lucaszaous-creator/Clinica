using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Clinica.Ui.Blazor.Telas;

/// <summary>
/// O ACABAMENTO DA TELA DO WHATSAPP (25/10/2026, docs/whatsapp-nativo-implementacao.md §15): o teclado do
/// WhatsApp Web (Enter envia, Shift+Enter quebra a linha, Esc fecha o que está por cima, o foco vai ao
/// campo), a ligação que cai no meio do uso (a faixa amarela) e os botões do modelo sob o balão. Num
/// arquivo à parte para as outras frentes da tela não esbarrarem; o que a tela AFIRMA vem de
/// <see cref="ConversasNaTela"/> e <see cref="ModeloDoWhatsApp"/>.
/// </summary>
public partial class ConversasDoWhatsApp
{
    private const string ModuloDaConversa = "./js/conversa-do-whatsapp.js";

    /// <summary>Desde quando o WhatsApp da clínica não responde (a faixa amarela e o rodapé); nulo = ligado.</summary>
    private DateTimeOffset? _semLigacaoDesde;

    /// <summary>O "Quem aparece aqui?" do modo Retenção aberto.</summary>
    private bool _quemAparece;

    private ElementReference _campoEl, _buscaEl;
    private bool _focarCampo, _focarBusca;
    private Task? _lendoModelosDosBotoes;

    /// <summary>Sem conversa no centro (nem carregando nem com falha): a coluna da ficha não ocupa lugar.</summary>
    private bool SemConversaNoCentro => AoLado is null && _aberta is null && !_carregandoConversa && _erroConversa is null;

    /// <summary>
    /// Enter (com Ctrl também) envia; Shift+Enter quebra a linha; Alt+Enter é do sistema. A MESMA regra do
    /// js/conversa-do-whatsapp.js, que impede a quebra de linha que o Enter faria.
    /// </summary>
    private static bool EnviaPeloEnter(KeyboardEventArgs e) => e.Key == "Enter" && !e.ShiftKey && !e.AltKey;

    /// <summary>A ligação com o WhatsApp caiu: a faixa amarela diz desde quando, até a próxima leitura que der certo.</summary>
    private void PerdeuLigacao(Exception ex)
    {
        if (ex is CrmIndisponivel and not CrmNaoConfigurado) _semLigacaoDesde ??= WhatsAppDaRecepcaoService.Agora;
    }

    // ---------------------------------------------------------------- Esc fecha o que está por cima

    private void FecharPorCima()
    {
        if (_popover == Popover.Nenhum) return;
        _popover = Popover.Nenhum;
        _focarCampo = true;
    }

    private void TeclaDoPopover(KeyboardEventArgs e)
    {
        if (e.Key == "Escape") FecharPorCima();
    }

    private void TeclaDaBusca(KeyboardEventArgs e)
    {
        if (e.Key != "Escape") return;
        _buscaConversa = null;
        _focarCampo = true;
    }

    private void TeclaDoQuemAparece(KeyboardEventArgs e)
    {
        if (e.Key == "Escape") _quemAparece = false;
    }

    // ---------------------------------------------------------------- depois do desenho: as teclas e o foco

    private async Task AcabamentoDepoisDoDesenhoAsync(bool primeira)
    {
        if (primeira)
        {
            try
            {
                // Sem o módulo (o teste, sem navegador) o Enter continua enviando; só a quebra de linha não é impedida.
                if (await JS.InvokeAsync<IJSObjectReference>("import", ModuloDaConversa) is { } modulo)
                    await modulo.InvokeVoidAsync("ligar");
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException)
            {
                Clinica.Application.Diagnostico.Registrar("Blazor — as teclas da conversa do WhatsApp não foram ligadas", ex);
            }
        }
        if (_focarBusca) { _focarBusca = false; await FocarAsync(_buscaEl); }
        else if (_focarCampo) { _focarCampo = false; await FocarAsync(_campoEl); }
    }

    // O campo pode não estar na tela (janela fechada, encerrada): sem ele, o foco fica onde estava. SEM ROLAR: no
    // celular a conversa ainda está deslizando para dentro quando o foco chega, e o foco comum rolava a página
    // (overflow:hidden também rola) até o campo — a tela ficava deslocada para o lado (foto 31 em 480).
    private static async Task FocarAsync(ElementReference el)
    {
        try { await el.FocusAsync(preventScroll: true); }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException) { }
    }

    // ---------------------------------------------------------------- os botões do modelo sob o balão

    /// <summary>
    /// A conversa tem mensagem que saiu por modelo: os modelos do número dela são lidos UMA vez (a mesma lista do
    /// popover "Modelos"), para desenhar os botões que a pessoa recebeu. Falhou, o balão fica sem eles (log).
    /// </summary>
    private void PedirModelosDosBotoes(ConversaDoWhatsApp c)
    {
        if (c.Sms || c.Conversa is not { } conversa || !c.LinhaDoTempo.Any(i => i.Modelo is not null)) return;
        if (_modelos is not null && _remetenteDosModelos == conversa.Sender || _lendoModelosDosBotoes is { IsCompleted: false }) return;
        _lendoModelosDosBotoes = LerModelosDosBotoesAsync(conversa.Sender);
    }

    private async Task LerModelosDosBotoesAsync(string remetente)
    {
        try
        {
            using var escopo = Escopo();
            var modelos = await escopo.ServiceProvider.GetRequiredService<WhatsAppDaRecepcaoService>().ModelosAsync(remetente);
            if (Descartada || _modelos is not null && _remetenteDosModelos == remetente) return;
            _modelos = modelos;
            _remetenteDosModelos = remetente;
            StateHasChanged();
        }
        catch (Exception ex) when (ex is RecusaDoCrm or CrmIndisponivel)
        {
            Clinica.Application.Diagnostico.Registrar("Blazor — os botões dos modelos do WhatsApp não puderam ser lidos", ex);
        }
    }

    /// <summary>Os botões do modelo por que a mensagem saiu (os do número da conversa), ou nenhum.</summary>
    private IReadOnlyList<string> BotoesDoModelo(ConversaDoWhatsApp c, ItemDaConversa item)
        => item.Tipo != TipoNaLinhaDoTempo.Enviada || item.Modelo is null || c.Conversa?.Sender != _remetenteDosModelos ? []
            : ModeloDoWhatsApp.Botoes(item.Modelo, _modelos);
}
