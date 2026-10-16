using Clinica.Application.Abstracoes;
using Clinica.Domain;
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
        p.HorariosAtendimento = null; // a forma antiga (um horário para todos os dias) substitui a por dia
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

    /// <summary>
    /// O EDITOR ÚNICO dos horários de atendimento (out/2026): as faixas de cada dia e a trava.
    /// <paramref name="semana"/> nula ou sem dia nenhum = "sem horário cadastrado" (a agenda
    /// aceita qualquer dia e hora, como sempre). <paramref name="esperado"/> é o texto que a
    /// tela leu — outro posto que salvou no meio recusa em vez de ser sobrescrito em silêncio.
    /// </summary>
    public async Task SalvarHorariosAsync(int profissionalId, bool protegida,
        IReadOnlyDictionary<DayOfWeek, IReadOnlyList<FaixaDeAtendimento>>? semana, string operador,
        (bool Protegida, string? Horarios)? esperado = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(operador)) throw new InvalidOperationException("Entre no sistema para configurar a agenda.");
        if (semana is not null && HorariosDeAtendimento.Validar(semana) is { } erro)
            throw new InvalidOperationException(erro);
        var p = await repo.ObterProfissionalAsync(profissionalId, ct)
            ?? throw new InvalidOperationException("Profissional não encontrado.");
        if (esperado is { } e && (e.Protegida != p.AgendaProtegida || e.Horarios != Atual(p)))
            throw new InvalidOperationException("Outro posto alterou estes horários. Feche, abra de novo e confira antes de salvar.");
        var antes = p.DescricaoJornada ?? "sem horário cadastrado";
        var travaAntes = p.AgendaProtegida;
        p.AgendaProtegida = protegida;
        p.DefinirHorarios(semana);
        await repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = operador, Acao = "AgendaProfissionalConfigurada",
            Detalhe = $"{p.Rotulo}: {antes} (trava {(travaAntes ? "ligada" : "desligada")}) → "
                + $"{p.DescricaoJornada ?? "sem horário cadastrado"} (trava {(protegida ? "ligada" : "desligada")}). Horários marcados preservados."
        }, ct);
        await repo.SalvarAsync(ct);
    }

    /// <summary>O texto que identifica o horário gravado — o que a tela guarda para a conferência de concorrência.</summary>
    public static string Atual(Profissional p)
        => HorariosDeAtendimento.Escrever(p.Semana().Where(d => d.Value is { Count: > 0 })
            .ToDictionary(d => d.Key, d => d.Value!));
}
