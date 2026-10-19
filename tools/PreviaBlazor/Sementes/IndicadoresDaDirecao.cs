// Dados FICTÍCIOS da prévia — nenhuma pessoa, operadora contratada ou adquirente real.
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// As três telas da direção que saíram do WPF na etapa 6 (out/2026): Indicadores,
/// Rentabilidade por convênio e Custo de taxas e impostos. A base, as guias e os recebimentos
/// já vêm das outras sementes; esta acrescenta só o que nenhuma delas tinha e que cada tela
/// existe para mostrar:
/// - NPS RESPONDIDO no mês (promotores, neutros e detratores) e uma pesquisa enviada sem
///   resposta — sem isso o cartão do NPS fica sempre "—";
/// - evoluções com a EVA em PAR, para a coluna "Queda média da dor";
/// - receita de guias baixadas com RETENÇÃO na fonte e pagamento DIAS depois da baixa, com
///   valores diferentes por operadora — é o que dá "retido", "prazo" e a frase "rende mais ·
///   rende menos";
/// - uma taxa de tabela da Rede ABAIXO do que a clínica de fato pagou, para o vermelho de
///   "efetiva acima do teto" aparecer.
/// Sem Id fixo: tudo se acha pelo que as outras sementes gravaram.
/// </summary>
public sealed class SementeIndicadoresDaDirecao : ISemente
{
    public int Ordem => 95;

    public void Semear(ClinicaDbContext db)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        SemearNps(db, hoje);
        SemearEva(db, hoje);
        SemearReceitaDeGuias(db, hoje);
        SemearTaxaAbaixoDoEfetivo(db, hoje);
    }

    private static void SemearNps(ClinicaDbContext db, DateOnly hoje)
    {
        var pacientes = db.Set<Paciente>().OrderBy(p => p.Id).Select(p => p.Id).Take(12).ToList();
        if (pacientes.Count < 12) return;
        var notas = new int?[] { 10, 9, 10, 9, 8, 7, 9, 3, 10, 6, null, null };
        var inicioDoMes = new DateOnly(hoje.Year, hoje.Month, 1);
        for (var i = 0; i < notas.Length; i++)
        {
            var dia = inicioDoMes.AddDays(Math.Min(i, hoje.Day - 1));
            var enviado = dia.ToDateTime(new TimeOnly(18, 0));
            db.Add(new ContatoCampanha
            {
                PacienteId = pacientes[i], Tipo = TipoContato.Nps, Canal = CanalContato.WhatsApp,
                Origem = $"PREVIA-NPS:{i}", Referencia = dia, EnviadoEm = enviado, EnviadoPor = "Direção (prévia)",
                Status = notas[i] is null ? StatusContato.Enviado : StatusContato.Respondido,
                Nota = notas[i], RespondidoEm = notas[i] is null ? null : enviado.AddHours(3)
            });
        }
        db.SaveChanges();
    }

    private static void SemearEva(ClinicaDbContext db, DateOnly hoje)
    {
        var profissionais = db.Set<Profissional>().OrderBy(p => p.Id).Take(3).ToList();
        var pacientes = db.Set<Paciente>().OrderBy(p => p.Id).Select(p => p.Id).Skip(12).Take(9).ToList();
        if (profissionais.Count < 3 || pacientes.Count < 9) return;
        var inicioDoMes = new DateOnly(hoje.Year, hoje.Month, 1);
        // (antes, depois) por profissional: a Dra. Helena tira mais dor por sessão que os outros.
        int[][] pares = [[8, 3], [7, 2], [9, 4], [6, 4], [7, 5], [8, 5], [5, 4], [6, 5], [7, 6]];
        for (var i = 0; i < pares.Length; i++)
            db.Add(new Evolucao
            {
                PacienteId = pacientes[i], ProfissionalId = profissionais[i / 3].Id,
                Data = inicioDoMes.AddDays(Math.Min(i, hoje.Day - 1)),
                QueixaPrincipal = "Dor lombar", Conduta = "Acupuntura com eletro",
                EvaAntes = pares[i][0], EvaDepois = pares[i][1]
            });
        db.SaveChanges();
    }

    private static void SemearReceitaDeGuias(ClinicaDbContext db, DateOnly hoje)
    {
        var desde = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(-5);
        var semReceita = db.Set<CodigoFaturamento>()
            .Where(c => c.DataBaixa != null && c.DataBaixa >= desde && c.Glosa == StatusGlosa.SemGlosa)
            .Where(c => !db.Set<LancamentoFinanceiro>().Any(l => l.CodigoFaturamentoId == c.Id))
            .Select(c => new { c.Id, c.AtendimentoId, DataBaixa = c.DataBaixa!.Value, c.Atendimento!.PacienteId, c.Atendimento.Paciente!.Convenio, c.Atendimento.Paciente.ConvenioCodigo })
            .OrderBy(c => c.Id)
            .ToList();

        // Valor bruto, retenção e dias até o dinheiro cair, por FAMÍLIA — para a tabela ter
        // quem paga muito e tarde, quem paga pouco e rápido, e quem retém na fonte.
        static (decimal Valor, decimal Retencao, int Dias) Regra(Convenio c) => c switch
        {
            Convenio.UnimedIntercambio => (160m, 0.0465m, 45),
            Convenio.Amil => (120m, 0m, 20),
            Convenio.Petrobras => (190m, 0.0615m, 75),
            _ => (145m, 0.0465m, 30)
        };

        foreach (var g in semReceita.GroupBy(c => c.ConvenioCodigo ?? c.Convenio.ToString()))
            foreach (var c in g.Take(12))
            {
                var (valor, retencao, dias) = Regra(c.Convenio);
                var pago = c.DataBaixa.AddDays(dias);
                var realizado = pago <= hoje;
                db.Add(new LancamentoFinanceiro
                {
                    Data = c.DataBaixa, Tipo = TipoLancamento.Entrada, Valor = valor,
                    ValorImposto = retencao > 0 ? Math.Round(valor * retencao, 2) : null,
                    Status = realizado ? StatusLancamento.Realizado : StatusLancamento.Previsto,
                    DataPagamento = realizado ? pago : null, DataVencimento = realizado ? null : pago,
                    Descricao = "Receita de guia (prévia)", FormaPagamento = FormaPagamento.Convenio,
                    PacienteId = c.PacienteId, AtendimentoId = c.AtendimentoId, CodigoFaturamentoId = c.Id,
                    Convenio = c.Convenio, ConvenioCodigo = c.ConvenioCodigo
                });
            }
        db.SaveChanges();
    }

    private static void SemearTaxaAbaixoDoEfetivo(ClinicaDbContext db, DateOnly hoje)
    {
        // A Rede cobrou ~3,2% sobre o que passou; a tabela vigente diz 2,5% — a diferença é o
        // argumento da renegociação que a tela existe para mostrar.
        db.Add(new TaxaCartao
        {
            Adquirente = "Rede", Modalidade = ModalidadeCartao.CreditoAVista, Percentual = 2.5m, DiasParaReceber = 30,
            VigenteDe = new DateOnly(hoje.Year, 1, 1), Ativa = true, CriadoPor = "prévia"
        });
        db.SaveChanges();
    }
}
