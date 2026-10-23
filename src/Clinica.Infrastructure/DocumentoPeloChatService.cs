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
        var pacientes = await PacientesDoPedidoAsync(db, profissional, pedido, momento, ct);
        if (pacientes.Count == 0) return new(DocumentoPeloChatRegras.NaoAchei(pedido));
        if (pacientes.Count > 1) return new(DocumentoPeloChatRegras.Varias(pedido, pacientes.Select(l => (l.Paciente, l.DataHora))));

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
    /// As pacientes da agenda DE QUEM PEDIU (60 dias para trás, 30 para a frente) que batem com o
    /// pedido, uma linha por paciente (o horário mais perto de agora). É a mesma busca do WhatsApp.
    /// Com CPF, vale o documento da ficha (comparado só pelos dígitos); com nome E CPF, os dois têm
    /// de bater — um nome que não é o da ficha daquele CPF é dúvida, e dúvida não emite.
    /// </summary>
    internal static async Task<List<SistemaEquipeService.LinhaDoHorario>> PacientesDoPedidoAsync(ClinicaDbContext db, int profissional,
        DocumentoPorTexto pedido, DateTime momento, CancellationToken ct)
    {
        var linhas = (await SistemaEquipeService.ConsultaHorarios(db, momento.Date.AddDays(-60), momento.Date.AddDays(31), profissional).ToListAsync(ct))
            .Where(l => l.Status is StatusAgendamento.Agendado or StatusAgendamento.Realizado).ToList();
        if (pedido.Cpf is { } cpf)
        {
            // Só as fichas da agenda dele: poucas, e o documento é comparado em memória (gravado
            // com ou sem pontuação).
            var ids = linhas.Select(l => l.PacienteId).Distinct().ToList();
            var documentos = await db.Pacientes.AsNoTracking().Where(p => ids.Contains(p.Id))
                .Select(p => new { p.Id, p.Documento }).ToListAsync(ct);
            var comOCpf = documentos.Where(p => Cpf.Normalizar(p.Documento) == cpf).Select(p => p.Id).ToHashSet();
            linhas = linhas.Where(l => comOCpf.Contains(l.PacienteId)).ToList();
        }
        return linhas.Where(l => pedido.Nome.Length == 0 || AssistenteWhatsAppRegras.NomeBate(pedido.Nome, l.Paciente))
            .GroupBy(l => l.PacienteId)
            .Select(g => g.OrderBy(l => Math.Abs((l.DataHora - momento).Ticks)).First())
            .ToList();
    }
}
