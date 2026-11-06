using Clinica.Application.Abstracoes;
using Clinica.Crm.Contracts;

namespace Clinica.Application.Servicos;

/// <summary>A passagem do turno (25/10, D12): arquivo à parte para as outras frentes da aba não esbarrarem nele.</summary>
public sealed partial class WhatsAppDaRecepcaoService
{
    /// <summary>
    /// "Passar o turno": devolve cada conversa de <paramref name="minhas"/> à fila dela, com a MESMA nota, pela
    /// MESMA devolução do "Com você → Devolver". Conversa por conversa e SEM parar no primeiro erro: a que falha
    /// fica com a pessoa e entra no relato com a frase do CRM. No 409 (a Version mudou — quase sempre o recibo de
    /// entrega de uma mensagem anterior), relê a conversa e repete UMA vez, e só se ela ainda é de quem está
    /// passando o turno: se outra pessoa a assumiu no meio, ela não é tirada de lá.
    /// </summary>
    public async Task<ResultadoDaPassagem> PassarOTurnoAsync(IReadOnlyList<AppConversationSummary> minhas, string nota, string? meuAgente,
        CancellationToken ct = default)
    {
        if (TurnoNoWhatsApp.Impedimento(nota) is { } impedimento) throw new InvalidOperationException(impedimento);
        var passaram = new List<string>();
        var falharam = new List<FalhaDaPassagem>();
        foreach (var c in minhas)
        {
            var nome = ConversasNaTela.NomeDaConversa(c);
            try
            {
                try { await DevolverAsync(c.Id, c.Version, c.Queue, nota, c.PendingTransfer, ct); }
                catch (RecusaDoCrm r) when (r.Conflito)
                {
                    var atual = await crm.AbrirConversaAsync(UsuarioId, c.Id, ct);
                    if (atual.Closed || meuAgente is null || atual.AgentId != meuAgente)
                    {
                        falharam.Add(new(c.Id, nome, TurnoNoWhatsApp.JaNaoEraSua));
                        continue;
                    }
                    await DevolverAsync(atual.Id, atual.Version, atual.Queue, nota, atual.PendingTransfer, ct);
                }
                passaram.Add(nome);
            }
            catch (Exception e) when (e is RecusaDoCrm or CrmIndisponivel)
            {
                if (e is CrmIndisponivel) Diagnostico.Registrar($"WhatsApp — passar o turno: a conversa {c.Id} não voltou à fila", e);
                falharam.Add(new(c.Id, nome, e.Message));
            }
        }
        return new ResultadoDaPassagem(passaram, falharam);
    }
}
