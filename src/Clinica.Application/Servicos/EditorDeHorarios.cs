using System.Globalization;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Uma faixa como a pessoa digita: os dois campos de hora ("HH:mm").</summary>
public sealed class FaixaEmEdicao
{
    public string? Inicio { get; set; }
    public string? Fim { get; set; }
}

/// <summary>Um dia no editor: atende ou não, e as faixas dele.</summary>
public sealed class DiaEmEdicao
{
    public DayOfWeek Dia { get; init; }
    public string Curto { get; init; } = "";
    public string Nome { get; init; } = "";
    public bool Atende { get; set; }
    public List<FaixaEmEdicao> Faixas { get; } = [];
}

/// <summary>
/// O EDITOR do horário de atendimento (out/2026 — <c>docs/recepcao-ux-out2026.md</c> §2): a
/// semana, segunda primeiro, cada dia com o interruptor "atende" e as faixas ("+ faixa" para a
/// tarde). Puro: a tela só desenha e chama; o que vira gravação é <see cref="Ler"/>, validado
/// pela MESMA regra do serviço (<see cref="HorariosDeAtendimento.Validar"/>), e a frase do
/// resultado é a do domínio (<see cref="Profissional.DescricaoJornada"/>) — a mesma da grade e
/// da equipe.
/// </summary>
public sealed class EditorDeHorarios
{
    /// <summary>Quando nenhum dia atende, o horário volta a "não cadastrado" — e a frase diz o efeito.</summary>
    public const string SemHorario = "Sem horário cadastrado — a agenda aceita qualquer dia e hora.";

    private static readonly FaixaDeAtendimento Manha = new(new TimeOnly(8, 0), new TimeOnly(12, 0));
    private static readonly FaixaDeAtendimento Tarde = new(new TimeOnly(14, 0), new TimeOnly(18, 0));

    public IReadOnlyList<DiaEmEdicao> Dias { get; }

    private EditorDeHorarios(IReadOnlyList<DiaEmEdicao> dias) => Dias = dias;

    /// <summary>
    /// Abre com o que o profissional tem. Sem horário cadastrado, nenhum dia vem ligado. Um
    /// cadastro antigo com dias e sem horas abre o dia ligado SEM faixa — e o salvar pede a faixa
    /// em vez de inventar uma hora.
    /// </summary>
    public static EditorDeHorarios De(Profissional p)
    {
        var dias = TelaDaEquipe.Semana.Select(s =>
        {
            var d = new DiaEmEdicao { Dia = s.Dia, Curto = s.Curto, Nome = s.Nome };
            if (p.JornadaDeclarada && p.FaixasEm(s.Dia) is var f && f is not { Count: 0 })
            {
                d.Atende = true;
                foreach (var x in f ?? []) d.Faixas.Add(Campos(x));
            }
            return d;
        }).ToList();
        return new EditorDeHorarios(dias);
    }

    public DiaEmEdicao Dia(DayOfWeek dia) => Dias.First(d => d.Dia == dia);

    /// <summary>
    /// Liga ou desliga o dia. Ligar um dia vazio traz as faixas do dia ligado mais próximo antes
    /// dele (ou manhã e tarde, 08:00–12:00 e 14:00–18:00): quem liga a terça quase sempre quer o
    /// mesmo da segunda. Desligar guarda as faixas — religar devolve o que estava.
    /// </summary>
    public void Alternar(DayOfWeek dia, bool atende)
    {
        var d = Dia(dia);
        d.Atende = atende;
        if (!atende || d.Faixas.Count > 0) return;
        var i = Indice(dia);
        var modelo = Dias.Take(i).Reverse().Concat(Dias.Skip(i + 1)).FirstOrDefault(x => x.Atende && x.Faixas.Count > 0);
        if (modelo is not null)
            foreach (var f in modelo.Faixas) d.Faixas.Add(new FaixaEmEdicao { Inicio = f.Inicio, Fim = f.Fim });
        else
        {
            d.Faixas.Add(Campos(Manha));
            d.Faixas.Add(Campos(Tarde));
        }
    }

    /// <summary>"+ faixa": a próxima começa uma hora depois do fim da última (o almoço), com quatro horas.</summary>
    public void AdicionarFaixa(DayOfWeek dia)
    {
        var d = Dia(dia);
        d.Atende = true;
        if (d.Faixas.Count == 0) { d.Faixas.Add(Campos(Manha)); return; }
        var fim = LerHora(d.Faixas[^1].Fim) ?? new TimeOnly(12, 0);
        var inicio = fim.Hour >= 22 ? new TimeOnly(22, 0) : fim.AddHours(1);
        var ate = inicio.Hour >= 19 ? new TimeOnly(23, 0) : inicio.AddHours(4);
        if (ate <= inicio) ate = new TimeOnly(23, 59);
        d.Faixas.Add(new FaixaEmEdicao { Inicio = Hora(inicio), Fim = Hora(ate) });
    }

    /// <summary>Remove a faixa. Sem faixa nenhuma, o dia deixa de atender (e diz isso no interruptor).</summary>
    public void RemoverFaixa(DayOfWeek dia, int indice)
    {
        var d = Dia(dia);
        if (indice < 0 || indice >= d.Faixas.Count) return;
        d.Faixas.RemoveAt(indice);
        if (d.Faixas.Count == 0) d.Atende = false;
    }

    /// <summary>"Copiar para os dias úteis": o dia escolhido (a segunda, na tela) vale de segunda a sexta.</summary>
    public void CopiarParaDiasUteis(DayOfWeek origem)
    {
        var o = Dia(origem);
        foreach (var d in Dias.Where(x => x.Dia is >= DayOfWeek.Monday and <= DayOfWeek.Friday && x.Dia != origem))
        {
            d.Atende = o.Atende;
            d.Faixas.Clear();
            foreach (var f in o.Faixas) d.Faixas.Add(new FaixaEmEdicao { Inicio = f.Inicio, Fim = f.Fim });
        }
    }

    /// <summary>
    /// O que vai para <see cref="ConfiguracaoAgendaService.SalvarHorariosAsync"/>. Nenhum dia
    /// atendendo = nulo (sem horário cadastrado). Erro escrito para a pessoa — o mesmo que o
    /// serviço recusaria, dito antes do clique.
    /// </summary>
    public (IReadOnlyDictionary<DayOfWeek, IReadOnlyList<FaixaDeAtendimento>>? Semana, string? Erro) Ler()
    {
        var semana = new Dictionary<DayOfWeek, IReadOnlyList<FaixaDeAtendimento>>();
        foreach (var d in Dias.Where(x => x.Atende))
        {
            if (d.Faixas.Count == 0)
                return (null, $"Em {d.Curto}, informe ao menos uma faixa de horário — ou desligue o dia.");
            var faixas = new List<FaixaDeAtendimento>();
            foreach (var f in d.Faixas)
            {
                if (LerHora(f.Inicio) is not { } de || LerHora(f.Fim) is not { } ate)
                    return (null, $"Em {d.Curto}, informe início e fim de cada faixa (ex.: 08:00 e 12:00).");
                faixas.Add(new FaixaDeAtendimento(de, ate));
            }
            semana[d.Dia] = faixas;
        }
        if (semana.Count == 0) return (null, null);
        return HorariosDeAtendimento.Validar(semana) is { } erro ? (null, erro) : (semana, null);
    }

    /// <summary>A frase do resultado, ao vivo: a do domínio, montada de um profissional temporário.</summary>
    public string Resultado()
    {
        var (semana, erro) = Ler();
        if (erro is not null) return erro;
        if (semana is null) return SemHorario;
        var temporario = new Profissional();
        temporario.DefinirHorarios(semana);
        return temporario.DescricaoJornada ?? SemHorario;
    }

    /// <summary>O resultado é um erro (a frase vem em vermelho e o Salvar fica apagado).</summary>
    public bool TemErro => Ler().Erro is not null;

    private int Indice(DayOfWeek dia)
    {
        for (var i = 0; i < Dias.Count; i++) if (Dias[i].Dia == dia) return i;
        return 0;
    }

    private static FaixaEmEdicao Campos(FaixaDeAtendimento f) => new() { Inicio = Hora(f.Inicio), Fim = Hora(f.Fim) };

    private static string Hora(TimeOnly t) => t.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static TimeOnly? LerHora(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var t = texto.Trim();
        return TimeOnly.TryParseExact(t, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var h)
               || TimeOnly.TryParseExact(t, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out h)
            ? h : null;
    }
}
