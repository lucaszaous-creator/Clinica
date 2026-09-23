using System.Data;
using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure.Tablet;

public sealed partial class PostoTabletService
{
    private const long DuracaoEdicaoEnfermagem = 120_000;

    public async Task<object> ReservarEdicaoEnfermagemAsync(SessaoTablet s, int paciente,
        ReservarEdicaoEnfermagemTablet pedido, CancellationToken ct)
    {
        if (pedido.EditorId == Guid.Empty) throw ErroFormularioTablet.Criar("Abra novamente a evolução para iniciar a edição.");
        var u = await Autorizar(s, ct, Permissao.RegistrarEvolucaoEnfermagem);
        var a = await db.Agendamentos.AsNoTracking().SingleOrDefaultAsync(a => a.Id == pedido.AgendamentoId && a.PacienteId == paciente, ct)
            ?? throw new RecursoClinicoIndisponivel();
        if (a.ModalidadePrevista is not (ModalidadeAtendimento.BsvApenas or ModalidadeAtendimento.BsvComAcupuntura))
            throw ErroFormularioTablet.Criar("Escolha uma sessão BSV para registrar a evolução de enfermagem.");
        await using var tx = await db.Database.BeginTransactionAsync(db.Database.IsNpgsql() ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable, ct);
        if (db.Database.IsNpgsql()) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(20260917, {paciente})", ct);
        var reserva = await db.Set<EdicaoEnfermagemTablet>().SingleOrDefaultAsync(e => e.AgendamentoId == a.Id, ct);
        bool Minha() => reserva is not null && reserva.UsuarioId == u.Id && reserva.SessaoId == s.Id && reserva.EditorId == pedido.EditorId;
        if (pedido.Liberar)
        {
            if (Minha()) db.Remove(reserva!);
        }
        else
        {
            if (reserva is not null && !Minha() && await EdicaoAtiva(reserva, ct)) await RecusarEdicao(reserva, ct);
            if (reserva is null) { reserva = new() { AgendamentoId = a.Id, PacienteId = paciente }; db.Add(reserva); }
            reserva.UsuarioId = u.Id; reserva.SessaoId = s.Id; reserva.EditorId = pedido.EditorId;
            reserva.ExpiraEm = acesso.Agora + DuracaoEdicaoEnfermagem;
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new { liberada = pedido.Liberar, expiraEm = pedido.Liberar ? 0 : reserva!.ExpiraEm };
    }

    private async Task<bool> EdicaoAtiva(EdicaoEnfermagemTablet r, CancellationToken ct)
        => r.ExpiraEm > acesso.Agora && await db.SessoesTablet.AnyAsync(s => s.Id == r.SessaoId && s.Modo == "equipe" && s.ExpiraEm > acesso.Agora, ct);

    private async Task RecusarEdicao(EdicaoEnfermagemTablet r, CancellationToken ct)
    {
        var nome = await db.Usuarios.Where(u => u.Id == r.UsuarioId).Select(u => u.Nome).SingleAsync(ct);
        throw new ConflitoClinicoTablet($"Esta sessão está aberta para edição de enfermagem por {nome}. Aguarde a liberação. Seu texto permanece nesta tela.");
    }

    // Chamado sob o mesmo lock transacional do salvamento, evitando troca de
    // responsável enquanto uma gravação está em andamento.
    private async Task ConferirEdicaoEnfermagem(SessaoTablet s, UsuarioSistema u, int agendamento, Guid? editor, CancellationToken ct)
    {
        var r = await db.Set<EdicaoEnfermagemTablet>().AsNoTracking().SingleOrDefaultAsync(e => e.AgendamentoId == agendamento, ct);
        if (r is not null && await EdicaoAtiva(r, ct))
        {
            if (r.UsuarioId != u.Id || r.SessaoId != s.Id || r.EditorId != editor) await RecusarEdicao(r, ct);
            return;
        }
        if (editor is not null) throw new ConflitoClinicoTablet("A reserva de edição expirou. Clique em Retomar edição antes de salvar. Seu texto permanece nesta tela.");
    }
}
