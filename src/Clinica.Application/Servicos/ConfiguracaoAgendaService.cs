using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

public sealed record JornadaAnterior(bool Protegida, int? Dias, TimeOnly? Das, TimeOnly? Ate);

/// <summary>Altera somente disponibilidade e trava, sem editar identidade ou acessos da equipe.</summary>
public sealed class ConfiguracaoAgendaService(IClinicaRepositorio repo)
{
    public async Task SalvarAsync(int profissionalId, bool protegida, int? dias,
        TimeOnly? das, TimeOnly? ate, string operador, JornadaAnterior? esperada = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(operador)) throw new InvalidOperationException("Entre no sistema para configurar a agenda.");
        if ((das is null) != (ate is null) || (das is not null && ate <= das))
            throw new InvalidOperationException("Informe início e fim do expediente, com o fim após o início.");
        if (dias is < 0 or > Profissional.TodosOsDias)
            throw new InvalidOperationException("Selecione dias válidos da semana.");
        var p = await repo.ObterProfissionalAsync(profissionalId, ct)
            ?? throw new InvalidOperationException("Profissional não encontrado.");
        if (esperada is not null && esperada != new JornadaAnterior(p.AgendaProtegida, p.DiasDeAtendimento, p.AtendeDas, p.AtendeAte))
            throw new InvalidOperationException("Outro posto alterou esta configuração. Atualize a lista e confira os horários antes de salvar novamente.");
        var anterior = $"trava={p.AgendaProtegida}; dias={p.DiasDeAtendimento}; das={p.AtendeDas}; ate={p.AtendeAte}; duração={p.DuracaoPadraoMinutos}";
        p.AgendaProtegida = protegida;
        p.DiasDeAtendimento = dias is 0 ? null : dias;
        p.AtendeDas = das;
        p.AtendeAte = ate;
        await repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = operador, Acao = "AgendaProfissionalConfigurada",
            Detalhe = $"Profissional {p.Id}: {anterior} → trava={protegida}; dias={p.DiasDeAtendimento}; das={das}; ate={ate}. Horários existentes preservados."
        }, ct);
        await repo.SalvarAsync(ct);
    }
}
