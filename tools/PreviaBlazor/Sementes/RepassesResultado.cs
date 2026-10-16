// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// O que as telas de Repasses, Resultado do mês e Produção precisam além da base:
/// - regras de repasse (percentual e valor por atendimento, com vigência — inclusive uma
///   encerrada, que explica o mês que já foi pago); um profissional SEM regra de propósito;
/// - doze meses de sessões REALIZADAS na agenda, cada uma com o atendimento e os dois códigos
///   (1º e 2º) — baixados, em aberto e algumas não conformidades;
/// - receita de entrada LIGADA ao atendimento nos últimos meses (algumas com taxa de cartão,
///   que vira dedução; algumas "a receber", que o repasse NÃO conta);
/// - uma apuração já paga, uma cancelada, e os tetos de gasto do mês corrente.
/// Sem Id fixo: tudo se acha pelo que a base gravou.
/// </summary>
public sealed class SementeRepassesResultado : ISemente
{
    public int Ordem => 60;

    public void Semear(ClinicaDbContext db)
    {
        var profissionais = db.Set<Profissional>().OrderBy(p => p.Id).ToList();
        var pacientes = db.Set<Paciente>().OrderBy(p => p.Id).Select(p => p.Id).ToList();
        var categorias = db.Set<CategoriaFinanceira>().ToList();
        if (profissionais.Count < 3 || pacientes.Count == 0) return;
        int Categoria(string nome) => categorias.First(c => c.Nome == nome).Id;

        var helena = profissionais[0];
        var marcos = profissionais[1];
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);

        // ---- Regras ----
        db.Add(new RegraRepasse
        {
            ProfissionalId = helena.Id, Base = BaseRepasse.PercentualDaReceita, Percentual = 40m,
            VigenteDe = inicioMes.AddMonths(-12), Observacoes = "Contrato de 2025, revisto em janeiro."
        });
        db.Add(new RegraRepasse
        {
            ProfissionalId = marcos.Id, Base = BaseRepasse.ValorPorAtendimento, ValorPorAtendimento = 50m,
            VigenteDe = inicioMes.AddMonths(-12), VigenteAte = inicioMes.AddMonths(-5).AddDays(-1)
        });
        db.Add(new RegraRepasse
        {
            ProfissionalId = marcos.Id, Base = BaseRepasse.ValorPorAtendimento, ValorPorAtendimento = 60m,
            VigenteDe = inicioMes.AddMonths(-5)
        });
        // Bianca fica SEM regra: a tela tem de mostrá-la com valor zero e dizer por quê.
        db.SaveChanges();

        // ---- Doze meses de sessões realizadas ----
        var rnd = new Random(11);
        var agora = DateTime.Now;
        var receitaPorMesProfissional = new Dictionary<(DateOnly, int), (decimal Receita, int Sessoes)>();

        for (var i = 11; i >= 0; i--)
        {
            var mes = inicioMes.AddMonths(-i);
            var sessoes = 18 + rnd.Next(10);
            for (var s = 0; s < sessoes; s++)
            {
                var dia = 1 + rnd.Next(DateTime.DaysInMonth(mes.Year, mes.Month));
                var quando = mes.AddDays(dia - 1).ToDateTime(new TimeOnly(8 + rnd.Next(10), rnd.Next(2) * 30));
                if (quando >= agora) continue;

                var profissional = profissionais[s % 3];
                var data = DateOnly.FromDateTime(quando);
                var atendimento = new Atendimento
                {
                    PacienteId = pacientes[rnd.Next(pacientes.Count)],
                    Data = data,
                    Modalidade = ModalidadeAtendimento.AcupunturaComEletro,
                    ModalidadeCodigo = nameof(ModalidadeAtendimento.AcupunturaComEletro),
                    Categoria = Clinica.Domain.Categoria.Verde,
                    LancadoEm = quando, LancadoPor = "previa", RealizadoEm = quando
                };
                // Meses antigos quase todos baixados; os recentes ainda com guia em aberto.
                atendimento.Codigos.Add(Codigo(TipoCodigo.Acupuntura, OrdemCodigo.Primeiro, data, i, rnd));
                atendimento.Codigos.Add(Codigo(TipoCodigo.Eletroacupuntura, OrdemCodigo.Segundo, data.AddDays(1), i, rnd));
                db.Add(atendimento);
                db.Add(new Agendamento
                {
                    PacienteId = atendimento.PacienteId, DataHora = quando,
                    ModalidadePrevista = ModalidadeAtendimento.AcupunturaComEletro,
                    ModalidadeCodigo = nameof(ModalidadeAtendimento.AcupunturaComEletro),
                    Status = StatusAgendamento.Realizado, Atendimento = atendimento,
                    ProfissionalId = profissional.Id, DuracaoMinutos = 50
                });

                // Receita LIGADA ao atendimento nos últimos quatro meses: é a base do percentual.
                if (i > 3) continue;
                var sorteio = rnd.NextDouble();
                if (sorteio < 0.12) continue; // sessão de convênio: o dinheiro vem pela guia
                var cartao = sorteio > 0.7;
                var aReceber = sorteio < 0.2; // "fica a receber" — previsto, o repasse não conta
                var valor = 180m;
                db.Add(new LancamentoFinanceiro
                {
                    Data = data, Tipo = TipoLancamento.Entrada, Valor = valor,
                    Descricao = "Sessão particular — acupuntura com eletro",
                    Status = aReceber ? StatusLancamento.Previsto : StatusLancamento.Realizado,
                    DataPagamento = aReceber ? null : data,
                    DataVencimento = aReceber ? data.AddDays(10) : null,
                    CategoriaFinanceiraId = Categoria("Particular"),
                    PacienteId = atendimento.PacienteId, Atendimento = atendimento,
                    FormaPagamento = aReceber ? null : cartao ? FormaPagamento.CartaoCredito : FormaPagamento.Pix,
                    Adquirente = cartao && !aReceber ? "Stone" : null,
                    Bandeira = cartao && !aReceber ? "Visa" : null,
                    Parcelas = cartao && !aReceber ? 1 : null,
                    ValorTaxa = cartao && !aReceber ? Math.Round(valor * 0.031m, 2) : null,
                    ValorImposto = aReceber ? null : Math.Round(valor * 0.06m, 2)
                });
                if (!aReceber)
                {
                    var chave = (mes, profissional.Id);
                    var atual = receitaPorMesProfissional.GetValueOrDefault(chave);
                    receitaPorMesProfissional[chave] = (atual.Receita + valor, atual.Sessoes + 1);
                }
            }
        }
        db.SaveChanges();

        // ---- Uma apuração já PAGA (mês passado, Dra. Helena) e uma CANCELADA ----
        var mesPassado = inicioMes.AddMonths(-1);
        var fimMesPassado = inicioMes.AddDays(-1);
        var (receitaHelena, sessoesHelena) = receitaPorMesProfissional.GetValueOrDefault((mesPassado, helena.Id));
        var valorHelena = Math.Round(receitaHelena * 0.40m, 2);
        var pago = new LancamentoFinanceiro
        {
            Data = fimMesPassado, Tipo = TipoLancamento.Saida, Valor = valorHelena,
            Descricao = $"Repasse {helena.Rotulo} — {mesPassado:dd/MM/yyyy} a {fimMesPassado:dd/MM/yyyy}",
            Status = StatusLancamento.Realizado, DataPagamento = inicioMes.AddDays(4), DataVencimento = fimMesPassado,
            FormaPagamento = FormaPagamento.Pix, CategoriaFinanceiraId = Categoria("Folha de pagamento"),
            Observacoes = $"{sessoesHelena} atendimento(s) · 40% da receita"
        };
        db.Add(new RepasseApurado
        {
            ProfissionalId = helena.Id, Inicio = mesPassado, Fim = fimMesPassado,
            Atendimentos = sessoesHelena, BaseCalculo = receitaHelena, Valor = valorHelena,
            RegraDescricao = "40% da receita", Lancamento = pago, CriadoPor = "previa"
        });

        var mesRetrasado = inicioMes.AddMonths(-2);
        var fimRetrasado = inicioMes.AddMonths(-1).AddDays(-1);
        var (_, sessoesMarcos) = receitaPorMesProfissional.GetValueOrDefault((mesRetrasado, marcos.Id));
        var cancelado = new LancamentoFinanceiro
        {
            Data = fimRetrasado, Tipo = TipoLancamento.Saida, Valor = 60m * Math.Max(sessoesMarcos, 1),
            Descricao = $"Repasse {marcos.Rotulo} — {mesRetrasado:dd/MM/yyyy} a {fimRetrasado:dd/MM/yyyy}",
            Status = StatusLancamento.Cancelado, DataVencimento = fimRetrasado,
            Observacoes = "Cancelado: apurado antes de lançar as sessões da última semana"
        };
        db.Add(new RepasseApurado
        {
            ProfissionalId = marcos.Id, Inicio = mesRetrasado, Fim = fimRetrasado,
            Atendimentos = Math.Max(sessoesMarcos, 1), BaseCalculo = 0m, Valor = cancelado.Valor,
            RegraDescricao = "R$ 60,00 por atendimento", Lancamento = cancelado, CriadoPor = "previa",
            CanceladoEm = DateTime.Now.AddDays(-20), MotivoCancelamento = "Apurado antes de lançar as sessões da última semana."
        });

        // ---- Tetos de gasto do mês corrente ----
        void Teto(string categoria, decimal teto) => db.Add(new OrcamentoCategoria
        {
            Ano = inicioMes.Year, Mes = inicioMes.Month, CategoriaFinanceiraId = Categoria(categoria), Teto = teto, CriadoPor = "previa"
        });
        Teto("Folha de pagamento", 27_000m);
        Teto("Aluguel", 9_000m);
        Teto("Insumos e medicamentos", 6_000m);
        Teto("Impostos", 7_500m);
        Teto("Marketing", 3_000m);
        db.SaveChanges();
    }

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

    private static CodigoFaturamento Codigo(TipoCodigo tipo, OrdemCodigo ordem, DateOnly prevista, int mesesAtras, Random rnd)
    {
        var sorteio = rnd.NextDouble();
        var chanceBaixa = mesesAtras switch { 0 => 0.35, 1 => 0.7, 2 => 0.86, _ => 0.95 };
        var status = sorteio < chanceBaixa ? StatusCodigo.Baixado
            : sorteio > 0.985 && mesesAtras > 0 ? StatusCodigo.NaoConformidade
            : StatusCodigo.Aberto;
        return new CodigoFaturamento
        {
            Tipo = tipo, Ordem = ordem, DataPrevistaFaturamento = prevista,
            FormaObtencao = ordem == OrdemCodigo.Primeiro ? FormaObtencao.NaoAplica : FormaObtencao.Sistema,
            Status = status,
            DataBaixa = status == StatusCodigo.Baixado ? Min(prevista.AddDays(rnd.Next(1, 6)), DateOnly.FromDateTime(DateTime.Today)) : null,
            NumeroGuiaReal = status == StatusCodigo.Baixado ? $"{rnd.Next(10_000_000, 99_999_999)}" : null,
            UsuarioBaixa = status == StatusCodigo.Baixado ? "previa" : null,
            NaoConformidadeJustificativa = status == StatusCodigo.NaoConformidade ? "Paciente sem carteirinha válida no dia." : null,
            NaoConformidadeEm = status == StatusCodigo.NaoConformidade ? DateTime.Now.AddDays(-10) : null
        };
    }
}
