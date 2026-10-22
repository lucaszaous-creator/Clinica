using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure.Tablet;

public sealed partial class AtendimentoTabletService
{
    /// <summary>
    /// A emissão do documento pedido pelo WhatsApp (@SemDor fase 4): a MESMA de
    /// <see cref="EmitirConteudoAsync"/>, fora de um horário (o documento é avulso, datado de
    /// hoje), e só para paciente que tem ou teve horário com quem emite.
    /// </summary>
    public async Task<ResultadoDocumentoTablet> EmitirPeloLinkAsync(SessaoTablet s, int paciente, EmitirDocumentoTablet pedido, CancellationToken ct)
    {
        var u = await AutorizarAsync(s, ct, Permissao.Prescrever);
        if (pedido.Tipo is not ("atestado" or "comparecimento" or "receita" or "exame" or "relatorio"))
            throw ErroFormularioTablet.Criar("Escolha um tipo de documento disponível.");
        if (!await db.Agendamentos.AnyAsync(a => a.PacienteId == paciente && a.ProfissionalId == u.ProfissionalId
                && (a.Status == StatusAgendamento.Agendado || a.Status == StatusAgendamento.Realizado), ct))
            throw new RecursoClinicoIndisponivel();
        var r = await EmitirConteudoAsync(u, paciente, null, null, pedido, ct);
        await Auditar(u, paciente, "TabletLinkWhatsAppEmissao", $"Documento nº {r.Numero} emitido pelo link do WhatsApp", ct);
        await db.SaveChangesAsync(ct);
        return r;
    }
}
