using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Modelos;

/// <summary>
/// Uma célula do mapa: quantos horários SOBRAM num dia para aquele tipo de atendimento, de
/// quantos havia, e quantos já estão marcados. Nulo em <see cref="Livres"/> = ninguém que
/// faz esse tipo atende no dia.
/// </summary>
public sealed record CelulaDeOcupacao(DateOnly Dia, int? Livres, int Total, int Marcados = 0)
{
    public bool Atende => Livres is not null;
    public bool Lotado => Livres == 0;

    /// <summary>Até um quinto livre conta como "quase cheio" — o laranja do mapa.</summary>
    public bool QuaseCheio => Livres is { } l && l > 0 && Total > 0 && l * 5 <= Total;

    /// <summary>Quanto do dia já está tomado, de 0 a 100 — a barrinha da célula.</summary>
    public int PercentualOcupado => Livres is { } l && Total > 0 ? (int)Math.Round(100.0 * (Total - l) / Total) : 0;
}

/// <summary>
/// O mapa montado: as linhas e a ocupação do período inteiro, contando cada profissional que
/// aparece no mapa UMA vez — as linhas repetem o mesmo horário livre em todos os tipos que a
/// pessoa faz, e somar as linhas inflaria a conta.
/// </summary>
public sealed record MapaDeOcupacao(IReadOnlyList<LinhaDeOcupacao> Linhas, int Livres, int Total)
{
    public int PercentualOcupado => Total == 0 ? 0 : (int)Math.Round(100.0 * (Total - Livres) / Total);
}

/// <summary>Os dois blocos do mapa, na ordem em que aparecem.</summary>
public enum GrupoDeOcupacao { Tratamentos, Consultas }

/// <summary>
/// Uma linha do mapa: um TIPO DE ATENDIMENTO (a modalidade, ou a consulta de uma
/// especialidade) ou a coleta de preventivo de um profissional, logo abaixo da consulta dele.
/// </summary>
public sealed record LinhaDeOcupacao(
    string Titulo, string? Detalhe, bool Preventivo, int? ProfissionalId, IReadOnlyList<CelulaDeOcupacao> Dias)
{
    public GrupoDeOcupacao Grupo { get; init; }

    /// <summary>A família da modalidade — dá a cor do ponto, a mesma da agenda.</summary>
    public ModalidadeAtendimento Familia { get; init; } = ModalidadeAtendimento.Consulta;

    /// <summary>Quantos atendimentos desse tipo estão marcados no período.</summary>
    public int MarcadosNoPeriodo => Dias.Sum(d => d.Marcados);
}

/// <summary>
/// O MAPA DE OCUPAÇÃO POR TIPO DE ATENDIMENTO (out/2026), no jeito do mapa de quartos de um
/// hotel: as linhas são os tipos de atendimento da clínica — os tratamentos (acupuntura,
/// eletro, BSV…) e as consultas, uma por especialidade —, as colunas são os dias, e cada
/// célula diz quantos horários SOBRAM para aquele tipo. Logo abaixo da consulta de quem faz
/// coleta vem a agenda paralela de preventivo.
///
/// Quem conta numa linha é quem FAZ aquele tipo: o que o gerente habilitou em Profissionais e
/// salas e, sem habilitação cadastrada, o que o profissional de fato atende (consulta da
/// especialidade dele, ou o tipo que ele tem marcado no período). Sem essa regra a
/// enfermeira e o psiquiatra apareceriam com horários livres de acupuntura.
///
/// A conta de horário livre é a MESMA do "Próximas vagas" (<see cref="BuscaDeVagas"/>), com a
/// jornada e os bloqueios — o mapa não pode dizer "3 livres" num dia em que a busca de vagas
/// não acha nenhuma. A de preventivo é a de <see cref="VagasDePreventivoNoDia"/>, a mesma que
/// recusa a marcação. Puro: o serviço lê, isto monta (e o teste monta sem banco).
/// </summary>
public static class OcupacaoPorEspecialidade
{
    public static MapaDeOcupacao Montar(
        IReadOnlyList<Profissional> profissionais, IReadOnlyList<Agendamento> ocupados,
        IReadOnlyList<BloqueioAgenda> bloqueios, DateOnly de, DateOnly ate,
        IReadOnlyList<EntradaModalidade>? modalidades = null, IReadOnlyList<EntradaEspecialidade>? especialidades = null)
    {
        var ativos = profissionais.Where(p => p.Ativo).ToList();
        var dias = new List<DateOnly>();
        for (var d = de; d <= ate; d = d.AddDays(1)) dias.Add(d);
        var marcados = ocupados.Where(a => a.OcupaAgenda).ToList();

        var tratamentos = (modalidades is { Count: > 0 } ? modalidades : ModalidadesEmbutidas())
            .Where(m => m.Ativo && m.Base != ModalidadeAtendimento.Consulta).ToList();
        var consultas = especialidades is { Count: > 0 } ? especialidades.Where(e => e.Ativo).ToList() : EspecialidadesEmbutidas();
        var codigoConsulta = (modalidades ?? []).FirstOrDefault(m => m.Base == ModalidadeAtendimento.Consulta)?.Codigo
                             ?? ModalidadeAtendimento.Consulta.ToString();

        // Horários livres e totais por profissional e dia — total é a mesma conta sem ninguém marcado.
        var porProfissional = ativos.ToDictionary(p => p.Id, p =>
        {
            var duracao = p.DuracaoPadraoMinutos ?? Agendamento.DuracaoPadraoMinutos;
            var inicio = de.ToDateTime(TimeOnly.MinValue);
            var janela = ate.DayNumber - de.DayNumber + 1;
            var livres = BuscaDeVagas.Calcular(inicio, duracao, p, ocupados, bloqueios,
                    quantidade: int.MaxValue, diasMaximos: janela)
                .GroupBy(v => DateOnly.FromDateTime(v.Inicio)).ToDictionary(g => g.Key, g => g.Count());
            var totais = BuscaDeVagas.Calcular(inicio, duracao, p, [], bloqueios,
                    quantidade: int.MaxValue, diasMaximos: janela)
                .GroupBy(v => DateOnly.FromDateTime(v.Inicio)).ToDictionary(g => g.Key, g => g.Count());
            return (livres, totais);
        });

        List<CelulaDeOcupacao> Celulas(IReadOnlyList<Profissional> quem, Func<Agendamento, bool> doTipo)
            => dias.Select(d =>
            {
                var total = quem.Sum(p => porProfissional[p.Id].totais.GetValueOrDefault(d));
                var livres = quem.Sum(p => porProfissional[p.Id].livres.GetValueOrDefault(d));
                var doDia = marcados.Count(a => DateOnly.FromDateTime(a.DataHora) == d && doTipo(a));
                return new CelulaDeOcupacao(d, total == 0 ? null : livres, total, doDia);
            }).ToList();

        var linhas = new List<LinhaDeOcupacao>();
        var contados = new HashSet<int>();

        foreach (var m in tratamentos)
        {
            bool DoTipo(Agendamento a) => string.Equals(CodigoDoHorario(a), m.Codigo, StringComparison.OrdinalIgnoreCase);
            var quem = ativos.Where(p => p.HabilitacoesAtendimento is not null
                    ? p.Atende(m.Codigo)
                    : marcados.Any(a => a.ProfissionalId == p.Id && DoTipo(a)))
                .ToList();
            if (quem.Count == 0 && !marcados.Any(DoTipo)) continue;
            contados.UnionWith(quem.Select(p => p.Id));
            linhas.Add(new LinhaDeOcupacao(m.Nome, Rotulos(quem), false, null, Celulas(quem, DoTipo))
            {
                Grupo = GrupoDeOcupacao.Tratamentos,
                Familia = m.Base
            });
        }

        var comPreventivo = new HashSet<int>();
        foreach (var e in consultas)
        {
            bool DoTipo(Agendamento a) => a.ModalidadePrevista == ModalidadeAtendimento.Consulta
                                          && string.Equals(a.EspecialidadeConsultaCodigo, e.Codigo, StringComparison.OrdinalIgnoreCase);
            var quem = ativos.Where(p => p.HabilitacoesAtendimento is not null
                    ? p.Atende(codigoConsulta, e.Codigo)
                    : string.Equals(p.EspecialidadeCodigo, e.Codigo, StringComparison.OrdinalIgnoreCase)
                      || marcados.Any(a => a.ProfissionalId == p.Id && DoTipo(a)))
                .ToList();
            if (quem.Count == 0 && !marcados.Any(DoTipo)) continue;
            contados.UnionWith(quem.Select(p => p.Id));
            linhas.Add(new LinhaDeOcupacao($"Consulta · {e.Nome}", Rotulos(quem), false, null, Celulas(quem, DoTipo))
            {
                Grupo = GrupoDeOcupacao.Consultas
            });

            // A coleta de preventivo de quem a faz, embaixo da consulta da especialidade DELE
            // (a ginecologista que também atende Clínica da Dor tem a coleta sob Ginecologia).
            foreach (var p in quem.Where(p => FazPreventivo(p, marcados)
                                              && (p.EspecialidadeCodigo is null
                                                  || string.Equals(p.EspecialidadeCodigo, e.Codigo, StringComparison.OrdinalIgnoreCase))
                                              && comPreventivo.Add(p.Id))
                         .OrderBy(p => p.Rotulo, StringComparer.CurrentCulture))
                linhas.Add(LinhaDePreventivo(p, marcados, porProfissional[p.Id].totais, de, ate));
        }

        // Quem faz coleta mas não caiu em nenhuma consulta (sem especialidade declarada).
        foreach (var p in ativos.Where(p => FazPreventivo(p, marcados) && comPreventivo.Add(p.Id))
                     .OrderBy(p => p.Rotulo, StringComparer.CurrentCulture))
            linhas.Add(LinhaDePreventivo(p, marcados, porProfissional[p.Id].totais, de, ate));

        return new MapaDeOcupacao(linhas,
            contados.Sum(id => porProfissional[id].livres.Values.Sum()),
            contados.Sum(id => porProfissional[id].totais.Values.Sum()));
    }

    private static LinhaDeOcupacao LinhaDePreventivo(Profissional p, IReadOnlyList<Agendamento> marcados,
        IReadOnlyDictionary<DateOnly, int> totais, DateOnly de, DateOnly ate)
    {
        var vagas = VagasDePreventivoNoDia.Contar(p, marcados, de, ate);
        var celulas = vagas.Select(v => new CelulaDeOcupacao(v.Dia,
            totais.GetValueOrDefault(v.Dia) == 0 ? null : v.Restantes, v.Limite, v.Marcados)).ToList();
        return new LinhaDeOcupacao("Coleta de preventivo", $"{p.Rotulo} · até {p.LimitePreventivoEfetivo} por dia", true, p.Id, celulas)
        {
            Grupo = GrupoDeOcupacao.Consultas
        };
    }

    /// <summary>O código da modalidade do horário — o do catálogo, ou o do enum nos horários antigos.</summary>
    private static string CodigoDoHorario(Agendamento a)
        => string.IsNullOrWhiteSpace(a.ModalidadeCodigo) ? a.ModalidadePrevista.ToString() : a.ModalidadeCodigo;

    /// <summary>"Dr. Marcos · Bianca" — quem faz o tipo (até três nomes, depois "+2").</summary>
    private static string? Rotulos(IReadOnlyList<Profissional> quem)
    {
        if (quem.Count == 0) return null;
        var nomes = quem.Select(p => p.Rotulo).OrderBy(n => n, StringComparer.CurrentCulture).ToList();
        return nomes.Count <= 3 ? string.Join(" · ", nomes) : $"{string.Join(" · ", nomes.Take(3))} +{nomes.Count - 3}";
    }

    /// <summary>Sem catálogo carregado (testes, prévia), as modalidades embutidas.</summary>
    private static IReadOnlyList<EntradaModalidade> ModalidadesEmbutidas()
        => Enum.GetValues<ModalidadeAtendimento>()
            .Select(m => new EntradaModalidade(m.ToString(), CatalogoModalidades.Nome(m.ToString(), m), m, true)).ToList();

    private static List<EntradaEspecialidade> EspecialidadesEmbutidas()
        => Enum.GetValues<Especialidade>()
            .Select(e => new EntradaEspecialidade(e.ToString(), CatalogoEspecialidades.Nome(e.ToString()), true))
            .OrderBy(e => e.Nome, StringComparer.CurrentCulture).ToList();

    /// <summary>
    /// Quem aparece com a linha de preventivo: quem declarou um limite, quem é da ginecologia
    /// ou quem já tem coleta marcada no período. O padrão (5) vale para todos, mas mostrar uma
    /// linha de preventivo para a acupuntura seria ruído.
    /// </summary>
    public static bool FazPreventivo(Profissional p, IEnumerable<Agendamento> ocupados)
        => p.LimitePreventivoEfetivo > 0
           && (p.LimitePreventivoPorDia is not null
               || string.Equals(p.EspecialidadeCodigo, "Ginecologia", StringComparison.OrdinalIgnoreCase)
               || ocupados.Any(a => a.ProfissionalId == p.Id && a.ColetaPreventivo && a.OcupaAgenda));
}
