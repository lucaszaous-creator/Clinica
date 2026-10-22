using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure;

/// <summary>
/// A mensagem que NÃO nasce de alguém digitando (out/2026): a resposta do Sistema SemDor e o cartão
/// do pedido à recepção. Grava pelas mesmas regras do <see cref="ChatEquipeService.EnviarAsync"/> —
/// o texto de busca junto, e a LISTA de conversas (<see cref="ConversaEquipe"/>) atualizada no mesmo
/// <c>SaveChanges</c> —, porque a lista, as não lidas e a busca leem essas colunas, e uma mensagem
/// gravada sem elas não apareceria para ninguém (docs/chat-da-equipe.md, DX).
/// </summary>
internal static class GravacaoDoChat
{
    /// <summary>A mensagem montada. <paramref name="doSistema"/> diz que não foi a pessoa quem escreveu.</summary>
    public static MensagemEquipe Mensagem(string conversa, int autorId, string autorNome, string texto, CartaoChat? cartao,
        bool doSistema, DateTime momento, int? pacienteId = null, string? pacienteNome = null)
        => new()
        {
            Conversa = conversa, AutorId = autorId,
            AutorNome = doSistema ? ChatEquipeRegras.NomeDoSistema : autorNome,
            Texto = texto, TextoBusca = ChatEquipeRegras.TextoDeBusca(texto), EnviadaEm = momento,
            DoSistema = doSistema, Cartao = cartao?.ParaJson(),
            PacienteId = pacienteId, PacienteNome = pacienteNome,
        };

    /// <summary>
    /// Põe a mensagem e o resumo da conversa no contexto (sem gravar): o resumo que já existe é
    /// atualizado, o primeiro é criado. Quem chama faz o <c>SaveChanges</c>, junto com o resto do ato.
    /// Uma ida (a leitura do resumo).
    /// </summary>
    public static async Task AdicionarAsync(ClinicaDbContext db, MensagemEquipe m, CancellationToken ct)
    {
        db.MensagensEquipe.Add(m);
        var previa = ChatEquipeRegras.Previa(m.Texto, ChatEquipeRegras.AnexoNaPrevia(m.DocumentoRotulo, m.PacienteNome, m.ArquivoNome), 120);
        var resumo = db.ConversasEquipe.Local.FirstOrDefault(c => c.Conversa == m.Conversa)
                     ?? await db.ConversasEquipe.FirstOrDefaultAsync(c => c.Conversa == m.Conversa, ct);
        if (resumo is null)
        {
            var direta = ChatEquipeRegras.ParticipantesDaDireta(m.Conversa);
            resumo = new ConversaEquipe
            {
                Conversa = m.Conversa,
                ParticipanteA = direta?.A ?? ChatEquipeRegras.DonoDoSistema(m.Conversa),
                ParticipanteB = direta?.B,
            };
            db.ConversasEquipe.Add(resumo);
        }
        resumo.UltimaMensagem = m;
        resumo.UltimaEm = m.EnviadaEm;
        resumo.UltimoAutorId = m.AutorId;
        resumo.UltimoAutorNome = m.AutorNome;
        resumo.UltimaPrevia = previa;
    }

    /// <summary>
    /// Troca o cartão (e o texto) de uma mensagem que já existe — a situação do pedido mudou. Marca
    /// <see cref="MensagemEquipe.EditadaEm"/> para a releitura de quem está com a conversa aberta
    /// trazer o cartão novo; a tela não escreve "(editada)" em cartão, e não há versão a guardar
    /// (quem mudou foi o sistema, e a história do pedido está nos eventos dele).
    /// </summary>
    public static async Task AtualizarCartaoAsync(ClinicaDbContext db, int? mensagemId, string texto, CartaoChat cartao, DateTime momento,
        CancellationToken ct)
    {
        if (mensagemId is not { } id) return;
        var json = cartao.ParaJson();
        var busca = ChatEquipeRegras.TextoDeBusca(texto);
        await db.MensagensEquipe.Where(m => m.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Cartao, json).SetProperty(m => m.Texto, texto)
                .SetProperty(m => m.TextoBusca, busca).SetProperty(m => m.EditadaEm, momento), ct);
    }
}
