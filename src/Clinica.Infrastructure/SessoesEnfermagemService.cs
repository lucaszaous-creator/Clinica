using Clinica.Domain.Entities;
using Clinica.Domain;
using Clinica.Application.Tablet;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure;

public sealed record FiltroSessoesEnfermagem(string? Paciente = null, DateOnly? Inicio = null,
    DateOnly? Fim = null, string? Medico = null, string Situacao = "Hoje", int Pagina = 0);
public sealed record SessaoEnfermagemItem(int Id, int PacienteId, string Paciente, DateTime DataHora,
    string Medico, ModalidadeAtendimento Modalidade, int? AtendimentoId, bool ChegadaRegistrada,
    bool AposAplicacaoRegistrada, bool RegistroLegado, bool Concluida)
{
    public bool Registrada => RegistroLegado || ChegadaRegistrada && AposAplicacaoRegistrada;
    public string ModalidadeTexto => Modalidade == ModalidadeAtendimento.BsvApenas ? "BSV" : "BSV + acupuntura";
    public string Situacao => RegistroLegado ? "Registro anterior às fases — conferir no prontuário"
        : Registrada ? "Chegada e após aplicação registrados"
        : ChegadaRegistrada ? "Chegada registrada · após aplicação pendente"
        : AposAplicacaoRegistrada ? "Após aplicação registrada · chegada pendente"
        : "Chegada e após aplicação pendentes";
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
        if (filtro is null || filtro.Pagina is < 0 or > 10000 || filtro.Inicio > filtro.Fim
            || filtro.Fim == DateOnly.MaxValue || filtro.Paciente?.Length > 120
            || filtro.Medico?.Length > 120 || filtro.Situacao?.Length > 20)
            throw ErroFormularioTablet.Criar("Confira os filtros da agenda da enfermagem.");
        var inicioHoje = hoje.ToDateTime(TimeOnly.MinValue);
        var amanha = inicioHoje.AddDays(1);
        var consulta = db.Agendamentos.AsNoTracking()
            .Where(a => (a.ModalidadePrevista == ModalidadeAtendimento.BsvApenas || a.ModalidadePrevista == ModalidadeAtendimento.BsvComAcupuntura)
                && (a.Status == StatusAgendamento.Agendado || a.Status == StatusAgendamento.Realizado))
            .Select(a => new { a.Id, a.PacienteId, Paciente = a.Paciente!.Nome, a.DataHora,
                Medico = a.Profissional!.Nome, Modalidade = a.ModalidadePrevista, a.AtendimentoId,
                ChegadaRegistrada = db.EvolucoesEnfermagem.Any(e => e.AgendamentoId == a.Id && e.FaseAtendimento == "Chegada"
                    && e.CanceladaEm == null && !db.EvolucoesEnfermagem.Any(r => r.RetificaEvolucaoId == e.Id && r.CanceladaEm == null)),
                AposAplicacaoRegistrada = db.EvolucoesEnfermagem.Any(e => e.AgendamentoId == a.Id && e.FaseAtendimento == "AposAplicacao"
                    && e.CanceladaEm == null && !db.EvolucoesEnfermagem.Any(r => r.RetificaEvolucaoId == e.Id && r.CanceladaEm == null)),
                RegistroLegado = !db.EvolucoesEnfermagem.Any(e => e.AgendamentoId == a.Id && e.FaseAtendimento != null)
                    && db.EvolucoesEnfermagem.Any(e => e.AgendamentoId == a.Id && e.FaseAtendimento == null
                    && e.CanceladaEm == null && !db.EvolucoesEnfermagem.Any(r => r.RetificaEvolucaoId == e.Id && r.CanceladaEm == null)),
                Concluida = a.FimAtendimentoEm != null });
        consulta = filtro.Situacao switch
        {
            "DiaEPendentes" => consulta.Where(a => a.DataHora >= inicioHoje && a.DataHora < amanha || a.DataHora < inicioHoje && !a.RegistroLegado && (!a.ChegadaRegistrada || !a.AposAplicacaoRegistrada)),
            "Hoje" => consulta.Where(a => a.DataHora >= inicioHoje && a.DataHora < amanha),
            "Pendentes" => consulta.Where(a => !a.RegistroLegado && (!a.ChegadaRegistrada || !a.AposAplicacaoRegistrada) && a.DataHora < amanha),
            "Registradas" => consulta.Where(a => a.RegistroLegado || a.ChegadaRegistrada && a.AposAplicacaoRegistrada),
            "Todas" => consulta,
            _ => throw ErroFormularioTablet.Criar("Escolha uma situação válida.")
        };
        if (filtro.Inicio is {} desde) consulta = consulta.Where(a => a.DataHora >= desde.ToDateTime(TimeOnly.MinValue));
        if (filtro.Fim is {} ate) { var limite = ate.AddDays(1).ToDateTime(TimeOnly.MinValue); consulta = consulta.Where(a => a.DataHora < limite); }
        if (!string.IsNullOrWhiteSpace(filtro.Paciente)) { var nome = filtro.Paciente.Trim().ToLower(); consulta = consulta.Where(a => a.Paciente.ToLower().Contains(nome)); }
        if (!string.IsNullOrWhiteSpace(filtro.Medico)) { var nome = filtro.Medico.Trim().ToLower(); consulta = consulta.Where(a => a.Medico.ToLower().Contains(nome)); }
        var linhas = await consulta.OrderBy(a => a.RegistroLegado || a.ChegadaRegistrada && a.AposAplicacaoRegistrada).ThenByDescending(a => a.DataHora).ThenBy(a => a.Id)
            .Skip(filtro.Pagina * 50).Take(51).ToListAsync(ct);
        return new(linhas.Take(50).Select(a => new SessaoEnfermagemItem(a.Id, a.PacienteId, a.Paciente,
            a.DataHora, a.Medico, a.Modalidade, a.AtendimentoId, a.ChegadaRegistrada,
            a.AposAplicacaoRegistrada, a.RegistroLegado, a.Concluida)).ToList(), linhas.Count > 50);
    }
}
