using Clinica.Domain.Entities;
using Clinica.Domain;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure;

public sealed record FiltroSessoesEnfermagem(string? Paciente = null, DateOnly? Inicio = null,
    DateOnly? Fim = null, string? Medico = null, string Situacao = "DiaEPendentes", int Pagina = 0);
public sealed record SessaoEnfermagemItem(int Id, int PacienteId, string Paciente, DateTime DataHora,
    string Medico, ModalidadeAtendimento Modalidade, int? AtendimentoId, bool Registrada, bool Concluida)
{
    public string ModalidadeTexto => Modalidade == ModalidadeAtendimento.BsvApenas ? "BSV" : "BSV + acupuntura";
    public string Situacao => Registrada ? "Evolução registrada" : "Evolução pendente";
    public string Atendimento => Concluida ? "Concluído — permite registro tardio" : "Aberto";
}
public sealed record SessoesEnfermagemPagina(IReadOnlyList<SessaoEnfermagemItem> Itens, bool Mais);

/// <summary>Fila exclusiva da enfermagem; mantém sessões concluídas com evolução faltante.</summary>
public sealed class SessoesEnfermagemService(ClinicaDbContext db)
{
    public async Task<SessoesEnfermagemPagina> ListarAsync(int usuarioId, FiltroSessoesEnfermagem filtro,
        DateOnly hoje, CancellationToken ct = default)
    {
        var usuario = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Id == usuarioId, ct);
        if (usuario is not { Ativo: true, Perfil: PerfilAcesso.Enfermagem }
            || !usuario.Pode(Permissao.VerAgenda | Permissao.VerProntuario | Permissao.RegistrarEvolucaoEnfermagem))
            throw new UnauthorizedAccessException("Esta lista é exclusiva do perfil Enfermagem.");
        if (filtro.Pagina is < 0 or > 10000 || filtro.Inicio > filtro.Fim)
            throw new InvalidOperationException("Confira o período e a página selecionados.");
        var inicioHoje = hoje.ToDateTime(TimeOnly.MinValue);
        var amanha = inicioHoje.AddDays(1);
        var consulta = db.Agendamentos.AsNoTracking()
            .Where(a => (a.ModalidadePrevista == ModalidadeAtendimento.BsvApenas || a.ModalidadePrevista == ModalidadeAtendimento.BsvComAcupuntura)
                && (a.Status == StatusAgendamento.Agendado || a.Status == StatusAgendamento.Realizado))
            .Select(a => new { a.Id, a.PacienteId, Paciente = a.Paciente!.Nome, a.DataHora,
                Medico = a.Profissional!.Nome, Modalidade = a.ModalidadePrevista, a.AtendimentoId,
                Registrada = db.EvolucoesEnfermagem.Any(e => e.AgendamentoId == a.Id && e.CanceladaEm == null
                    && !db.EvolucoesEnfermagem.Any(r => r.RetificaEvolucaoId == e.Id)), Concluida = a.FimAtendimentoEm != null });
        consulta = filtro.Situacao switch
        {
            "DiaEPendentes" => consulta.Where(a => a.DataHora >= inicioHoje && a.DataHora < amanha || a.DataHora < inicioHoje && !a.Registrada),
            "Hoje" => consulta.Where(a => a.DataHora >= inicioHoje && a.DataHora < amanha),
            "Pendentes" => consulta.Where(a => !a.Registrada && a.DataHora < amanha),
            "Registradas" => consulta.Where(a => a.Registrada),
            "Todas" => consulta,
            _ => throw new InvalidOperationException("Escolha uma situação válida.")
        };
        if (filtro.Inicio is {} desde) consulta = consulta.Where(a => a.DataHora >= desde.ToDateTime(TimeOnly.MinValue));
        if (filtro.Fim is {} ate) { var limite = ate.AddDays(1).ToDateTime(TimeOnly.MinValue); consulta = consulta.Where(a => a.DataHora < limite); }
        if (!string.IsNullOrWhiteSpace(filtro.Paciente)) { var nome = filtro.Paciente.Trim().ToLower(); consulta = consulta.Where(a => a.Paciente.ToLower().Contains(nome)); }
        if (!string.IsNullOrWhiteSpace(filtro.Medico)) { var nome = filtro.Medico.Trim().ToLower(); consulta = consulta.Where(a => a.Medico.ToLower().Contains(nome)); }
        var linhas = await consulta.OrderBy(a => a.Registrada).ThenByDescending(a => a.DataHora).ThenBy(a => a.Id)
            .Skip(filtro.Pagina * 50).Take(51).ToListAsync(ct);
        return new(linhas.Take(50).Select(a => new SessaoEnfermagemItem(a.Id, a.PacienteId, a.Paciente,
            a.DataHora, a.Medico, a.Modalidade, a.AtendimentoId, a.Registrada, a.Concluida)).ToList(), linhas.Count > 50);
    }
}
