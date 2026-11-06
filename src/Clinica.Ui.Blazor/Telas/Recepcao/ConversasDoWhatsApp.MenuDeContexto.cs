using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Clinica.Ui.Blazor.Telas;

/// <summary>
/// O BOTÃO DIREITO da aba WhatsApp (out/2026, pedido da direção): o menu numa conversa da lista e numa mensagem.
/// Qual item aparece, e por que um deles está desabilitado, é do <see cref="MenuDoWhatsApp"/> (Application, com
/// teste); aqui cada ação só chama o que a tela já faz — o mesmo handler do botão do topo, do aviso ou do balão.
/// Fica num arquivo à parte para a tela ganhar só os dois <c>@oncontextmenu</c> e os dois menus.
/// </summary>
public partial class ConversasDoWhatsApp
{
    private IReadOnlyList<ItemDeMenu<AcaoNaConversa>>? _menuDaConversa;
    private IReadOnlyList<ItemDeMenu<AcaoNaMensagem>>? _menuDaMensagem;
    private string? _conversaDoMenu;
    private ItemDaConversa? _mensagemDoMenu;
    private string _selecaoDoMenu = "";
    private double _menuX, _menuY;
    private IJSObjectReference? _jsDoMenu;

    private AcessoNoMenu AcessoDoMenu => new(PodeEscrever, Pode(Permissao.VerFichaPaciente),
        PodeAlgum(GuardarNaFichaService.Permissoes), SemDorLigado);

    private void AbrirMenuDaConversa(string conversaId, MouseEventArgs e)
    {
        if (_painel?.Conversas.FirstOrDefault(x => x.Id == conversaId) is not { } c) return;
        FecharMenus();
        _conversaDoMenu = conversaId;
        (_menuX, _menuY) = (e.ClientX, e.ClientY);
        _menuDaConversa = MenuDoWhatsApp.DaConversa(c, _painel.Sessao.AgentId, AcessoDoMenu);
    }

    private async Task AbrirMenuDaMensagemAsync(ConversaDoWhatsApp c, ItemDaConversa item, MouseEventArgs e)
    {
        FecharMenus();
        (_menuX, _menuY) = (e.ClientX, e.ClientY);
        _mensagemDoMenu = item;
        _selecaoDoMenu = item.Id is { } id ? await SelecaoNoBalaoAsync(id) : "";
        var fichaConferida = c.Conversa?.PatientId is not null;
        _menuDaMensagem = MenuDoWhatsApp.DaMensagem(item, _selecaoDoMenu, c.Janela.PodeEscreverLivre, c.Sms, c.Minha, fichaConferida, AcessoDoMenu);
    }

    private void FecharMenus()
    {
        _menuDaConversa = null;
        _menuDaMensagem = null;
    }

    // O texto selecionado dentro DESTE balão no instante do clique: seleção noutro lugar não vira "Copiar a seleção".
    private async Task<string> SelecaoNoBalaoAsync(string mensagemId)
    {
        try
        {
            _jsDoMenu ??= await JS.InvokeAsync<IJSObjectReference>("import", "./js/menu-de-contexto.js");
            return await _jsDoMenu.InvokeAsync<string>("selecaoEm", $"[data-mensagem=\"{mensagemId.Replace("\"", "")}\"]") ?? "";
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException) { return ""; }
    }

    // ---------------------------------------------------------------- a conversa da lista

    private async Task AoEscolherNaConversaAsync(AcaoNaConversa acao)
    {
        if (_conversaDoMenu is not { } id || _painel?.Conversas.FirstOrDefault(x => x.Id == id) is not { } linha) return;
        switch (acao)
        {
            case AcaoNaConversa.CopiarTelefone:
                await CopiarAsync(ConversasNaTela.TelefoneNaTela(linha.Contact), "Telefone copiado.");
                return;
            case AcaoNaConversa.MarcarNaoLida:
                await MarcarLeituraAsync(id, lida: false);
                return;
            case AcaoNaConversa.MarcarLida:
                await MarcarLeituraAsync(id, lida: true);
                return;
            case AcaoNaConversa.AbrirFicha when linha.PatientId is { } pid:
                await AbrirFichaAsync(pid);
                return;
        }

        // O resto é o mesmo que os botões da conversa aberta fazem: abre a conversa e chama o MESMO handler.
        if (_aberta?.Id != id) await AbrirConversaAsync(id, linha.Unread, null);
        if (_aberta?.Id != id) return;
        switch (acao)
        {
            case AcaoNaConversa.Assumir: await AssumirAsync(); break;
            case AcaoNaConversa.Devolver: await DevolverAsync(); break;
            case AcaoNaConversa.Reabrir: await ReabrirAsync(); break;
            case AcaoNaConversa.Transferir: if (Exigir(Permissao.EditarAgenda, "transferir a conversa")) _form = Form.Transferir; break;
            case AcaoNaConversa.Encerrar: if (Exigir(Permissao.EditarAgenda, "encerrar o atendimento")) _form = Form.Encerrar; break;
            case AcaoNaConversa.NotaInterna when _aberta is { } a:
                if (Exigir(Permissao.EditarAgenda, "escrever nota interna")) { Rascunho(a).Nota = true; _focarCampo = true; }
                break;
            case AcaoNaConversa.ConferirFicha when _aberta is { } a:
                if (a.Dados.Candidatas.Count > 0) AbrirConferirFicha(); else _fichaAberta = true;
                break;
            case AcaoNaConversa.Resumir when _aberta is { } a: await ResumirComSemDorAsync(a); break;
        }
    }

    private async Task MarcarLeituraAsync(string conversaId, bool lida)
    {
        try
        {
            using var escopo = Escopo();
            var servico = escopo.ServiceProvider.GetRequiredService<WhatsAppDaRecepcaoService>();
            if (lida) await servico.MarcarLidaAsync(conversaId); else await servico.MarcarNaoLidaAsync(conversaId);
            // A conversa ABERTA que vira não lida sai da tela: aberta, a próxima leitura dela a marcaria lida de novo.
            if (!lida && _aberta?.Id == conversaId) _aberta = null;
            // A lista acompanha na hora; a próxima releitura confirma (a marca de leitura não muda o carimbo da conversa).
            if (_painel is { } painel)
                _painel = painel with { Conversas = painel.Conversas.Select(x => x.Id != conversaId ? x : x with { Unread = lida ? 0 : Math.Max(1, x.Unread) }).ToList() };
            Notificacoes.Info(lida ? "Marcada como lida." : "Marcada como não lida: ela volta a contar até você abrir.");
        }
        catch (Exception ex)
        {
            Recado(ConversasNaTela.FalhaNaEscrita(ex, "Nada foi alterado; tente de novo em instantes."), "erro");
        }
    }

    // ---------------------------------------------------------------- a mensagem

    private async Task AoEscolherNaMensagemAsync(AcaoNaMensagem acao)
    {
        if (_mensagemDoMenu is not { } m || _aberta is not { } c) return;
        var r = Rascunho(c);
        switch (acao)
        {
            case AcaoNaMensagem.Responder:
                if (!Exigir(Permissao.EditarAgenda, "responder citando") || m.Id is null || c.Conversa?.Messages.FirstOrDefault(x => x.Id == m.Id) is not { } original) return;
                r.Nota = false;
                r.Citada = ConversasNaTela.Citacao(original, c.Nome, _painel?.Agentes);
                _focarCampo = true;
                _rolarAoFim = true; // a barra da citação cresce o campo: a última mensagem não pode sumir atrás dele
                break;
            case AcaoNaMensagem.CopiarSelecao: await CopiarAsync(_selecaoDoMenu, "Seleção copiada."); break;
            case AcaoNaMensagem.CopiarTexto: await CopiarAsync(m.Texto, "Texto copiado."); break;
            case AcaoNaMensagem.CopiarComHora: await CopiarAsync(MenuDoWhatsApp.ComDataEHora(m, c.Nome, WhatsAppDaRecepcaoService.Agora), "Copiado com data e hora."); break;
            case AcaoNaMensagem.Reenviar: await ReenviarAsync(c, m); break;
            case AcaoNaMensagem.UsarComoRascunho:
                if (!Exigir(Permissao.EditarAgenda, "usar a mensagem como rascunho")) return;
                r.Nota = false;
                r.Texto = m.Texto;
                _focarCampo = true;
                break;
            case AcaoNaMensagem.SalvarArquivo when m.Midia is { } md: await BaixarMidiaAsync(md); break;
            case AcaoNaMensagem.GuardarNaFicha when m.Midia is { } md: AbrirGuardar(md); break;
            case AcaoNaMensagem.NotaSobre:
                if (!Exigir(Permissao.EditarAgenda, "escrever nota interna")) return;
                r.Nota = true;
                r.Citada = null;
                if (string.IsNullOrWhiteSpace(r.Texto)) r.Texto = MenuDoWhatsApp.ComecoDaNota(m);
                _focarCampo = true;
                break;
            case AcaoNaMensagem.DetalhesDaEntrega:
                await Dialogos.AvisoAsync("Detalhes da entrega",
                    string.Join("\n", MenuDoWhatsApp.DetalhesDaEntrega(m).Select(l => $"{l.Rotulo}: {l.Texto}")));
                break;
        }
    }

    // Reenviar é um envio NOVO do mesmo texto (o CRM nunca repete sozinho um envio incerto): se a primeira chegou, a
    // pessoa recebe duas — por isso a pergunta, e por isso só depois de conferir com ela (a frase sob o balão).
    private async Task ReenviarAsync(ConversaDoWhatsApp c, ItemDaConversa m)
    {
        if (!Exigir(Permissao.EditarAgenda, "reenviar a mensagem") || c.Conversa is not { } conversa) return;
        if (MenuDoWhatsApp.MotivoParaNaoReenviar(m, c.Janela.PodeEscreverLivre, c.Minha) is { } motivo) { Recado(motivo, "info"); return; }
        if (!await Dialogos.ConfirmarAsync("Reenviar a mensagem",
                "Se a primeira chegou, a pessoa recebe duas vezes. Confira com ela antes, se puder. Reenviar agora?", confirmar: "Reenviar")) return;
        await GravarNoCrmAsync(s => s.ResponderAsync(conversa, m.Texto, null, Guid.NewGuid().ToString("D")), "Mensagem reenviada.", "reenviar a mensagem");
    }

    private async Task CopiarAsync(string texto, string confirmacao)
    {
        if (string.IsNullOrWhiteSpace(texto)) return;
        if (await Host.CopiarAsync(texto)) Notificacoes.Info(confirmacao);
    }
}
