using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Clinica.Ui.Blazor.Telas;

/// <summary>
/// A CONVERSA COM CARA DE WHATSAPP (nov/2026, F1 — propostas v2 a v4 aprovadas pelo dono, docs em
/// /Shared/Clinica/crm-ux/IMPLEMENTACAO.md): o ⋮ do cabeçalho, o "Mais" da faixa fixada, o 📎 e o 😊 do campo e o "/" que
/// junta respostas rápidas e modelos. Qual item aparece e por que ele vem apagado é de <see cref="MenuDoWhatsApp"/> e
/// <see cref="ConversasNaTela"/> (Application, com teste); aqui cada escolha só chama o handler que a tela JÁ tinha — o
/// botão que saiu do cabeçalho, o botão do aviso, o Anexar, o popover de Modelos e o de Respostas rápidas.
/// </summary>
public partial class ConversasDoWhatsApp
{
    private IReadOnlyList<ItemDeMenu<AcaoNaConversa>>? _menuDoCabecalho;
    private IReadOnlyList<ItemDeMenu<BotaoPedido>>? _menuDaFaixa;
    private IReadOnlyList<ItemDeMenu<AcaoDoClipe>>? _menuDoClipe;

    /// <summary>O texto em que a pessoa apertou Esc no "/": a busca fica fechada até o texto mudar.</summary>
    private string? _barraDispensada;

    private void FecharMenusDoWhatsApp()
    {
        _menuDoCabecalho = null;
        _menuDaFaixa = null;
        _menuDoClipe = null;
    }

    // ---------------------------------------------------------------- o ⋮ do cabeçalho

    private void AbrirMenuDoCabecalho(ConversaDoWhatsApp c, MouseEventArgs e)
    {
        FecharMenus();
        (_menuX, _menuY) = (e.ClientX, e.ClientY);
        _menuDoCabecalho = MenuDoWhatsApp.DoCabecalho(c.Conversa, _painel?.Sessao.AgentId, c.Conversa?.Messages.Length ?? 0, AcessoDoMenu);
    }

    /// <summary>Cada item é o botão que estava no cabeçalho: o MESMO handler, com as mesmas duas barreiras.</summary>
    private async Task AoEscolherNoCabecalhoAsync(AcaoNaConversa acao)
    {
        if (_aberta is not { } a) return;
        switch (acao)
        {
            case AcaoNaConversa.Assumir: await AssumirAsync(); break;
            case AcaoNaConversa.Devolver: await DevolverAsync(); break;
            case AcaoNaConversa.Transferir: if (Exigir(Permissao.EditarAgenda, "transferir a conversa")) _form = Form.Transferir; break;
            case AcaoNaConversa.NotaInterna:
                if (Exigir(Permissao.EditarAgenda, "escrever nota interna")) { Rascunho(a).Nota = true; _focarCampo = true; }
                break;
            case AcaoNaConversa.Buscar: if (_buscaConversa is null) AlternarBusca(); else _focarBusca = true; break;
            case AcaoNaConversa.DadosDoContato: AbrirContato(); break;
            case AcaoNaConversa.Etiquetas: if (Exigir(Permissao.EditarAgenda, "etiquetar a conversa")) _popover = Popover.Etiquetas; break;
            case AcaoNaConversa.Resumir: await ResumirComSemDorAsync(a); break;
        }
    }

    // ---------------------------------------------------------------- o "Mais" da faixa fixada

    private void AbrirMenuDaFaixa(ConversaDoWhatsApp c, IReadOnlyList<BotaoPedido> botoes, MouseEventArgs e)
    {
        FecharMenus();
        (_menuX, _menuY) = (e.ClientX, e.ClientY);
        _menuDaFaixa = botoes.Select(b => new ItemDeMenu<BotaoPedido>(b, b.Rotulo, IconeDoBotao(b) ?? "direita",
            PodeDoBotao(b) ? null : "Seu acesso não alcança esta ação", Perigo: b.Perigo)).ToList();
    }

    private Task AoEscolherNaFaixaAsync(BotaoPedido b) => _aberta is { } a ? AcionarAsync(a, b) : Task.CompletedTask;

    // ---------------------------------------------------------------- o 📎 e o 😊 do campo

    private void AbrirMenuDoClipe(ConversaDoWhatsApp c, MouseEventArgs e)
    {
        FecharMenus();
        _popover = Popover.Nenhum;
        (_menuX, _menuY) = (e.ClientX, e.ClientY);
        _menuDoClipe = MenuDoWhatsApp.DoClipe(c.Sms, _painel?.Sessao.AttachmentsEnabled == true, AcessoDoMenu, _compartilhar, c.Janela.PodeEscreverLivre);
    }

    private async Task AoEscolherNoClipeAsync(AcaoDoClipe acao)
    {
        if (_aberta is not { } c) return;
        switch (acao)
        {
            case AcaoDoClipe.Arquivo: await AnexarAsync(c); break;
            case AcaoDoClipe.Modelos: await AbrirModelosAsync(false); break;
            case AcaoDoClipe.Rapidas: _popover = Popover.Rapidas; break;
            case AcaoDoClipe.Endereco: await CompartilharAsync(c, "location", null, "Endereço da clínica enviado."); break;
            case AcaoDoClipe.Contato or AcaoDoClipe.Servicos: _escolhendo = acao; _popover = Popover.Compartilhar; break;
        }
    }

    private void PorEmoji(ConversaDoWhatsApp c, string emoji)
    {
        var r = Rascunho(c);
        r.Texto = ConversasNaTela.ComEmoji(r.Texto, emoji);
        _popover = Popover.Nenhum;
        _focarCampo = true;
    }

    // ---------------------------------------------------------------- o "/" do campo

    /// <summary>O campo começa com "/" (e a pessoa não fechou a busca com Esc): as opções aparecem por cima do campo.</summary>
    private bool BarraAberta(ConversaDoWhatsApp c)
    {
        var r = Rascunho(c);
        return !r.Nota && c.Janela.PodeEscreverLivre && ConversasNaTela.TermoDaBarra(r.Texto) is not null
            && !string.Equals(r.Texto, _barraDispensada, StringComparison.Ordinal);
    }

    /// <summary>As opções do "/". Os modelos do número são lidos UMA vez (a mesma leitura dos botões do modelo sob o balão).</summary>
    private IReadOnlyList<OpcaoDaBarra> OpcoesDaBarra(ConversaDoWhatsApp c, string termo)
    {
        var remetente = c.Conversa?.Sender ?? _painel?.Sessao.Inboxes.FirstOrDefault()?.Sender;
        if (!c.Sms && remetente is not null && (_modelos is null || _remetenteDosModelos != remetente) && _lendoModelosDosBotoes is not { IsCompleted: false })
            _lendoModelosDosBotoes = LerModelosDosBotoesAsync(remetente);
        var modelos = _remetenteDosModelos == remetente ? _modelos : null;
        return ConversasNaTela.OpcoesDaBarra(termo, _painel?.Area.QuickReplies ?? [], modelos, c.Sms);
    }

    /// <summary>A resposta rápida TROCA o "/termo" pelo texto dela; o modelo abre o popover de Modelos já nele (variáveis e prévia).</summary>
    private async Task EscolherNaBarraAsync(ConversaDoWhatsApp c, OpcaoDaBarra o)
    {
        if (!o.Habilitada) return;
        var r = Rascunho(c);
        if (o.Rapida is { } q)
        {
            r.Texto = q.Text;
            _focarCampo = true;
            return;
        }
        if (o.Modelo is not { } modelo || !Exigir(Permissao.EditarAgenda, "enviar pelo WhatsApp da clínica")) return;
        r.Texto = "";
        await AbrirModelosAsync(false);
        if (_popover == Popover.Modelos && _modelos is not null) EscolherModelo(c, modelo);
    }

    /// <summary>
    /// O Enter com o "/" aberto escolhe a primeira opção que cabe — nunca envia o "/termo" ao paciente. Devolve verdadeiro
    /// quando a tecla foi do "/". (O Esc que fecha o "/" é o atalho Fechar, abaixo.)
    /// </summary>
    private async Task<bool> TeclaDaBarraAsync(ConversaDoWhatsApp c, KeyboardEventArgs e)
    {
        if (!BarraAberta(c) || !EnviaPeloEnter(e)) return false;
        var r = Rascunho(c);
        if (ConversasNaTela.TermoDaBarra(r.Texto) is { } termo && OpcoesDaBarra(c, termo).FirstOrDefault(o => o.Habilitada) is { } primeira)
            await EscolherNaBarraAsync(c, primeira);
        return true;
    }

    // ---------------------------------------------------------------- os atalhos do WhatsApp Web (F1b, docs/crm-cara-de-whatsapp-f1b.md)

    private ElementReference _buscaListaEl;

    /// <summary>
    /// Ctrl+F busca (na conversa aberta; sem conversa, na lista), Ctrl+Alt+N começa uma conversa e Esc fecha UMA coisa por vez:
    /// o menu, o "/" ou o popover, o formulário da tela, a busca na conversa, os Dados do contato e, por último, a conversa.
    /// O Esc só se trata aqui (o script o repassa na captura, antes do campo): tratar também no campo fechava duas.
    /// </summary>
    private async Task AtalhoDoWhatsAppAsync(AtalhoDeTela atalho)
    {
        if (!Pode(Permissao.EditarAgenda) || _painel is null) return;
        switch (atalho)
        {
            case AtalhoDeTela.Buscar when _aberta?.Conversa is not null:
                FecharMenus();
                if (_buscaConversa is null) AlternarBusca(); else _focarBusca = true;
                break;
            case AtalhoDeTela.Buscar:
                await FocarAsync(_buscaListaEl);
                return;
            case AtalhoDeTela.Nova:
                FecharMenus();
                AbrirNovaConversa();
                break;
            case AtalhoDeTela.Fechar:
                FecharUmaCoisa();
                break;
        }
        StateHasChanged();
    }

    /// <summary>
    /// Uma coisa por Esc. O menu e o formulário (o <c>Modal</c>) fecham com o PRÓPRIO Esc, porque o foco está neles: com
    /// um deles aberto, o atalho não faz nada — fechar aqui também fechava o que está por baixo no mesmo Esc.
    /// </summary>
    private void FecharUmaCoisa()
    {
        if (_menuDaConversa is not null || _menuDaMensagem is not null || _menuDoCabecalho is not null || _menuDaFaixa is not null || _menuDoClipe is not null)
            return;
        if (_form != Form.Nenhum || _horarioAberto is not null || _midiaAmpliada is not null || FormularioQueResolveAberto) return;
        if (_aberta is { } c && BarraAberta(c)) { _barraDispensada = Rascunho(c).Texto; _focarCampo = true; return; }
        if (_popover != Popover.Nenhum) { _popover = Popover.Nenhum; _focarCampo = true; return; }
        if (_buscaConversa is not null) { _buscaConversa = null; _focarCampo = true; return; }
        if (_fichaAberta) { _fichaAberta = false; return; }
        if (_aberta is not null && AoLado is null) FecharConversa();
    }
}
