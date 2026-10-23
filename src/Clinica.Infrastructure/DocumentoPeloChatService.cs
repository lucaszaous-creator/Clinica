using System.Text;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure;

/// <summary>
/// O documento pedido ao @SemDor no chat dos apps (out/2026 — decisão da direção de 23/10: "o
/// sistema faz todo o trâmite interno e traz o documento em PDF para a conversa"). A frase é a da
/// fase 4 (<see cref="DocumentoPeloWhatsAppRegras.Ler"/>): "atestado Maria Silva 2 dias",
/// "comparecimento Maria Silva", "receita Maria Silva: …". A paciente é procurada só na agenda de
/// quem pediu, como no WhatsApp; o documento nasce pela MESMA emissão do Consultório
/// (<see cref="DocumentoClinicoService.EmitirAsync"/>: número, código de conferência e auditoria no
/// mesmo SaveChanges) e volta anexado à resposta — o "Abrir" do chat é a 2ª via em PDF.
///
/// Sem assinatura digital (o A1 não está homologado): o documento é o mesmo que o Consultório
/// imprime para assinar à mão, e a resposta diz isso. Assinar continua na central de Documentos.
/// </summary>
public sealed class DocumentoPeloChatService
{
    private readonly ClinicaDbContext db;
    private readonly Func<DateTime> agora;

    public DocumentoPeloChatService(ClinicaDbContext db, Func<DateTime> agora)
    {
        this.db = db;
        this.agora = agora;
    }

    /// <summary>A resposta: o texto e, quando o documento nasceu, o anexo (documento e paciente).</summary>
    public sealed record Resposta(string Texto, int? DocumentoId = null, string? DocumentoRotulo = null,
        int? PacienteId = null, string? PacienteNome = null);

    /// <summary>Emite o documento pedido, ou devolve a pergunta do que falta. Nunca emite com dúvida sobre a paciente.</summary>
    public async Task<Resposta> EmitirAsync(UsuarioSistema eu, DocumentoPorTexto pedido, CancellationToken ct = default)
    {
        if (eu.ProfissionalId is not { } profissional || !eu.Pode(Permissao.Prescrever))
            return new(DocumentoPeloChatRegras.SoQuemPrescreve);
        if (DocumentoPeloWhatsAppRegras.OQueFalta(pedido) is { } falta) return new(falta);

        var momento = agora();
        var pacientes = await PacientesDoPedidoAsync(db, profissional, pedido.Nome, momento, ct);
        if (pacientes.Count == 0)
            return new($"Não achei \"{pedido.Nome}\" na sua agenda (60 dias para trás e 30 para a frente). Confira o nome; o documento por aqui é só de paciente seu.");
        if (pacientes.Count > 1)
        {
            var sb = new StringBuilder($"Achei {pacientes.Count} pacientes com \"{pedido.Nome}\" na sua agenda. Mande de novo com mais do nome:");
            foreach (var l in pacientes.Take(8)) sb.Append($"\n• {l.Paciente} ({l.DataHora:dd/MM})");
            return new(sb.ToString());
        }

        var achado = pacientes[0];
        var tipo = DocumentoPeloChatRegras.Tipo(pedido.Tipo);
        var repo = new ClinicaRepositorio(db);
        var documentos = new DocumentoClinicoService(repo, new ProntuarioService(repo), new ConsentimentoService(repo));
        var doc = await documentos.EmitirAsync(new DocumentoClinico
        {
            PacienteId = achado.PacienteId, ProfissionalId = profissional, AgendamentoId = achado.Id,
            Data = DateOnly.FromDateTime(momento), Tipo = tipo, Corpo = DocumentoPeloWhatsAppRegras.TextoProposto(pedido),
            DiasAfastamento = tipo == TipoDocumentoClinico.Atestado ? pedido.Dias : null,
            Itens = tipo == TipoDocumentoClinico.PedidoExame ? [new ItemDocumento { Descricao = "Solicitação conforme texto acima." }] : [],
        }, eu.Login, ct);
        foreach (var e in db.ChangeTracker.Entries().Where(e => e.Entity is DocumentoClinico or ItemDocumento or EventoAuditoria).ToList())
            e.State = EntityState.Detached;

        var rotulo = $"{TipoDocumentoInfo.Rotular(doc.Tipo)} {doc.Numero}";
        return new(DocumentoPeloChatRegras.Emitido(doc.Tipo, doc.Numero, achado.Paciente), doc.Id, rotulo, achado.PacienteId, achado.Paciente);
    }

    /// <summary>
    /// As pacientes da agenda DE QUEM PEDIU (60 dias para trás, 30 para a frente) cujo nome bate com
    /// o citado, uma linha por paciente (o horário mais perto de agora). É a mesma busca do WhatsApp.
    /// </summary>
    internal static async Task<List<SistemaEquipeService.LinhaDoHorario>> PacientesDoPedidoAsync(ClinicaDbContext db, int profissional,
        string nome, DateTime momento, CancellationToken ct)
    {
        var linhas = await SistemaEquipeService.ConsultaHorarios(db, momento.Date.AddDays(-60), momento.Date.AddDays(31), profissional).ToListAsync(ct);
        return linhas.Where(l => l.Status is StatusAgendamento.Agendado or StatusAgendamento.Realizado
                                 && AssistenteWhatsAppRegras.NomeBate(nome, l.Paciente))
            .GroupBy(l => l.PacienteId)
            .Select(g => g.OrderBy(l => Math.Abs((l.DataHora - momento).Ticks)).First())
            .ToList();
    }
}
