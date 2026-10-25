using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using Clinica.Domain.Entities;
using Microsoft.AspNetCore.Components;

namespace Clinica.Ui.Blazor.Telas;

/// <summary>
/// O MODO PAINEL da tela de conversa (out/2026, fase 2 do WhatsApp nativo — "a conversa ao lado"): a
/// MESMA tela da aba WhatsApp, aberta pela <c>CamadaDaConversaAoLado</c> à direita da Agenda do dia, da
/// Ficha, das Confirmações e do Acompanhamento. Sem a lista e sem a coluna da ficha (o CSS as esconde:
/// a tela de trás já é a ficha), e direto na conversa do paciente pedido. Fica num arquivo à parte para
/// a tela (que outras frentes também mexem) ganhar só o parâmetro e três pontos de ligação.
/// </summary>
public partial class ConversasDoWhatsApp
{
    /// <summary>
    /// O paciente do painel ao lado. Nulo: a aba WhatsApp de sempre (lista, conversa e ficha). Com ele, a
    /// tela abre a conversa da FICHA (<see cref="ConversaAoLadoNaTela.DoPaciente"/>); sem conversa, o caso
    /// do Acompanhamento (o convite da retenção); sem os dois, o "Começar com um modelo".
    /// </summary>
    [Parameter] public PedidoDeConversa? AoLado { get; set; }

    private string? ClasseAoLado => AoLado is null ? null : "wa-ao-lado";

    /// <summary>As conversas do paciente do painel, lidas da fotografia da lista (a releitura de 7 s as atualiza).</summary>
    private ConversasDaFicha DoPacienteAoLado => _painel is { } painel && AoLado is { } p
        ? ConversaAoLadoNaTela.DoPaciente(painel.Conversas, p.PacienteId, p.Telefone)
        : new ConversasDaFicha([], false);

    /// <summary>
    /// A abertura do painel: a conversa mais recente da ficha (ou do telefone, com o aviso de conferir —
    /// a ficha NÃO se liga sozinha aqui, diferente do pedido que leva à aba); sem nenhuma, o caso do
    /// Acompanhamento; sem caso, nada abre e o painel oferece começar por modelo. O texto pedido vira rascunho.
    /// </summary>
    private async Task AbrirAoLadoAsync(PedidoDeConversa pedido)
    {
        if (_painel is not { } painel) return;
        if (DoPacienteAoLado.Escolhida is { } conversa)
        {
            await AbrirConversaAsync(conversa.Id, conversa.Unread, null);
            if (_aberta is { } a && !string.IsNullOrWhiteSpace(pedido.Texto))
            {
                var r = Rascunho(a);
                if (string.IsNullOrWhiteSpace(r.Texto)) r.Texto = pedido.Texto.Trim();
            }
            return;
        }
        if (pedido.PacienteId is { } id && painel.RetencaoPermitida && ConversasNaTela.CasoDoPaciente(id, painel.Retencao) is { } caso)
            await AbrirCasoAsync(caso);
    }

    private Task EscolherAoLadoAsync(AppConversationSummary c) => AbrirConversaAsync(c.Id, c.Unread, null);

    private Task AbrirChegadaAoLadoAsync()
        => DoPacienteAoLado.Escolhida is { } c ? AbrirConversaAsync(c.Id, c.Unread, null) : Task.CompletedTask;

    /// <summary>"Começar com um modelo": a MESMA "Nova conversa" da aba, com o telefone, o nome e a ficha do pedido.</summary>
    private void ComecarAoLado()
    {
        if (AoLado is not { } p || !Exigir(Permissao.EditarAgenda, "começar uma conversa")) return;
        _novaTelefone = p.Telefone;
        _novaNome = p.Nome;
        _novaFicha = p.PacienteId;
        _form = Form.Nova;
    }
}
