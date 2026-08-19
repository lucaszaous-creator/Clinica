using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// Uma célula da grade da semana do profissional: o cruzamento de uma faixa de horário
/// com um dia. É o mesmo desenho da linha do tempo do balcão (parcela 58), e pela mesma
/// razão: empilhados, o horário das 9h e o das 15h ficam colados, e "quando eu tenho
/// espaço?" — a pergunta que a tela existe para responder, com o paciente na frente — só
/// se responde lendo cartão por cartão. Na grade, o vazio TEM tamanho.
///
/// A diferença para a do balcão é o que a célula NÃO tem: clique de marcar. Quem marca é
/// a recepção; aqui o vão livre é informação, não formulário.
/// </summary>
public sealed record CelulaSemana(
    DateOnly Dia,
    DateTime Quando,
    IReadOnlyList<SessaoDoDia> Sessoes,
    bool Continuacao,
    string? Bloqueio,
    bool NoPassado)
{
    public bool Livre => Sessoes.Count == 0 && !Continuacao;

    public bool Bloqueada => Bloqueio is not null;

    /// <summary>Fechado E sem ninguém marcado: é o vão que escreve o motivo.</summary>
    public bool MostrarBloqueio => Bloqueada && Livre;
}

/// <summary>Uma faixa de horário — uma LINHA da grade, com uma célula por dia.</summary>
public sealed record FaixaSemana(
    TimeOnly Hora,
    string Rotulo,
    bool HoraCheia,
    IReadOnlyList<CelulaSemana> Celulas);

/// <summary>
/// A grade da semana, montada em memória a partir da leitura que já existia
/// (<see cref="SemanaDoProfissional"/>) mais os bloqueios do período.
///
/// O montador é PURO de propósito — recebe o relógio em vez de olhá-lo — porque a camada
/// de tela do WPF não compila nos testes: tudo o que decide o desenho da grade (janela
/// esticada, continuação, cancelado que não cobre, bloqueio por célula) precisa morar
/// onde o <c>dotnet test</c> alcança.
/// </summary>
public sealed record GradeSemana(IReadOnlyList<FaixaSemana> Faixas)
{
    /// <summary>
    /// Monta a grade. As regras são as da grade do balcão, e não por acaso — duas grades
    /// da mesma agenda com regras diferentes divergiriam sobre o mesmo horário:
    /// <list type="bullet">
    /// <item>a janela de horas é a padrão da clínica ESTICADA pelo que existe na semana
    /// (<see cref="Agendamento.AberturaPadraoGrade"/>) — uma sessão às 6h30 puxa a grade
    /// para cima em vez de ficar de fora;</item>
    /// <item>cada sessão ocupa a faixa em que COMEÇA e marca as seguintes como
    /// continuação até o fim previsto;</item>
    /// <item>cancelado e falta não cobrem nada — o horário vagou de verdade — e aparecem
    /// MARCADOS na faixa deles, nunca sumindo (a regra da folha do dia);</item>
    /// <item>bloqueio (férias, feriado, folga) escreve o motivo na célula: o vão fechado
    /// visualmente idêntico ao vão livre foi o defeito da parcela 63 no balcão, e a
    /// semana do consultório nunca mostrou férias nenhuma.</item>
    /// </list>
    /// </summary>
    public static GradeSemana Montar(
        SemanaDoProfissional semana,
        IReadOnlyList<BloqueioAgenda> bloqueios,
        DateTime agora)
    {
        const int passo = Agendamento.DuracaoPadraoMinutos;

        var inicio = Agendamento.AberturaPadraoGrade;
        var fim = Agendamento.FechamentoPadraoGrade;
        foreach (var s in semana.Dias.SelectMany(d => d.Sessoes))
        {
            var comeco = TimeOnly.FromDateTime(s.DataHora);
            var termino = TimeOnly.FromDateTime(s.FimPrevisto);
            if (comeco < inicio) inicio = comeco;
            // Sessão que atravessa a meia-noite não estica a grade para trás.
            if (termino > fim && termino > comeco) fim = termino;
        }

        var faixas = new List<FaixaSemana>();
        for (var minuto = Piso(inicio, passo); minuto <= Piso(fim, passo); minuto += passo)
        {
            var hora = new TimeOnly(minuto / 60, minuto % 60);
            var celulas = new List<CelulaSemana>();

            foreach (var dia in semana.Dias)
            {
                var quando = dia.Dia.ToDateTime(hora);
                var fimDaFaixa = quando.AddMinutes(passo);

                var naFaixa = dia.Sessoes
                    .Where(s => Piso(TimeOnly.FromDateTime(s.DataHora), passo) == minuto)
                    .ToList();

                // Continuação: sessão que começou ANTES desta faixa e ainda não terminou.
                var coberta = naFaixa.Count == 0 && dia.Sessoes.Any(s =>
                    !s.ForaDoDia
                    && Piso(TimeOnly.FromDateTime(s.DataHora), passo) < minuto
                    && s.FimPrevisto > quando);

                var bloqueio = bloqueios.FirstOrDefault(b =>
                        b.ColideCom(quando, fimDaFaixa)
                        && b.AlcancaRecurso(semana.ProfissionalId, null))
                    ?.Motivo;

                celulas.Add(new CelulaSemana(
                    dia.Dia, quando, naFaixa, coberta, bloqueio, quando < agora));
            }

            faixas.Add(new FaixaSemana(
                hora,
                hora.Minute == 0 ? hora.ToString("HH:mm") : string.Empty,
                hora.Minute == 0,
                celulas));
        }

        return new GradeSemana(faixas);
    }

    /// <summary>Minuto do dia arredondado para BAIXO no passo da grade.</summary>
    private static int Piso(TimeOnly hora, int passo)
    {
        var minutos = hora.Hour * 60 + hora.Minute;
        return minutos - minutos % passo;
    }
}
