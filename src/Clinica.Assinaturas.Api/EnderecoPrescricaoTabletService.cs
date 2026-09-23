using System.Data;
using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Infrastructure.Tablet;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Assinaturas.Api;

/// <summary>Completa apenas endereço ausente, no contexto da receita do prescritor.</summary>
public sealed class EnderecoPrescricaoTabletService(AtendimentoTabletService acesso, ClinicaDbContext db)
{
    private async Task<(UsuarioSistema Usuario, DocumentoClinico Documento)> Documento(
        SessaoTablet sessao, int agendamento, int documento, CancellationToken ct)
    {
        var (usuario, _) = await acesso.ExigirDocumentoAsync(sessao, agendamento, "documento", documento, true, ct);
        var d = await db.DocumentosClinicos.Include(d => d.Paciente).SingleAsync(d => d.Id == documento, ct);
        if (d.Cancelado || d.AssinadoEletronicamente)
            throw new ConflitoClinicoTablet("O documento já foi assinado ou cancelado. Confira o histórico antes de continuar.");
        return (usuario, d);
    }

    public async Task<object> ConferirAsync(SessaoTablet sessao, int agendamento, int documento, CancellationToken ct)
    {
        var (_, d) = await Documento(sessao, agendamento, documento, ct);
        return new { precisaEndereco = d.Tipo == TipoDocumentoClinico.Receita && string.IsNullOrWhiteSpace(d.Paciente?.Endereco) };
    }

    public async Task CompletarAsync(SessaoTablet sessao, int agendamento, int documento, string? endereco, CancellationToken ct)
    {
        endereco = endereco?.Trim();
        if (endereco is null || endereco.Length is < 5 or > 300 || endereco.Any(char.IsControl))
            throw ErroFormularioTablet.Criar("Informe o endereço residencial do paciente, entre 5 e 300 caracteres.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var (_, inicial) = await Documento(sessao, agendamento, documento, ct);
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(20260917, {inicial.PacienteId})", ct);
        // Revalidar após o lock usado também na assinatura: não alterar via já assinada.
        db.ChangeTracker.Clear();
        var (usuario, d) = await Documento(sessao, agendamento, documento, ct);
        if (d.Tipo != TipoDocumentoClinico.Receita) throw new RecursoClinicoIndisponivel();
        var paciente = d.Paciente ?? throw new RecursoClinicoIndisponivel();
        if (string.Equals(paciente.Endereco, endereco, StringComparison.Ordinal))
        {
            await tx.CommitAsync(ct); // Reenvio após perda de resposta, sem auditoria duplicada.
            return;
        }
        if (!string.IsNullOrWhiteSpace(paciente.Endereco))
            throw new ConflitoClinicoTablet("O endereço já foi preenchido em outro acesso. Reabra a assinatura e confira o PDF atualizado.");
        var anterior = paciente.Endereco;
        var alterados = await db.Pacientes.Where(p => p.Id == paciente.Id && p.Endereco == anterior)
            .ExecuteUpdateAsync(p => p.SetProperty(c => c.Endereco, endereco), ct);
        if (alterados != 1)
            throw new ConflitoClinicoTablet("O cadastro mudou em outro acesso. Reabra a assinatura para conferir.");
        db.Auditoria.Add(new EventoAuditoria {
            Operador = usuario.Login, PacienteId = paciente.Id, Acao = "TabletEnderecoPrescricao",
            Detalhe = $"Endereço residencial ausente completado pelo prescritor antes da assinatura do documento {documento}."
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
