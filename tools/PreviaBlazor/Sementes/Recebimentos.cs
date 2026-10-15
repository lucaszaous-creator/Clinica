// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// A semente das telas de RECEBÍVEIS DE CARTÃO e de CONCILIAÇÃO (out/2026):
/// - vendas de cartão com previsão de depósito — atrasadas, a vencer e já confirmadas
///   (uma delas caiu depois do previsto, para o atraso aparecer na aba "Já caíram");
/// - guias de convênio BAIXADAS sem receita (com e sem preço na tabela, uma glosada);
/// - guias glosadas que JÁ viraram receita (prevista e realizada) — a aba do caminho de volta;
/// - sessões PARTICULARES realizadas sem dinheiro, com e sem preço na tabela do particular.
///
/// Sem Ids fixos e sem chave natural que outra semente possa repetir: tudo é acrescentado,
/// e o único cadastro com chave (o convênio "Sul América") só entra se ainda não existir.
/// </summary>
public sealed class SementeRecebimentos : ISemente
{
    public int Ordem => 30;

    public void Semear(ClinicaDbContext db)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
        // Dias do mês corrente que já passaram (pelo menos o dia 1): a conciliação lê o MÊS.
        var diasPassados = Math.Max(1, hoje.Day - 1);
        DateOnly DiaDoMes(int k) => inicioMes.AddDays(k % diasPassados);

        SemearCartao(db, hoje);

        // ===== Convênios e preços =====
        if (!db.Set<ConvenioCadastro>().Any(c => c.Codigo == "SulAmericaPrevia"))
            db.Add(new ConvenioCadastro { Codigo = "SulAmericaPrevia", Nome = "Sul América Saúde", Familia = Convenio.Personalizado, Ativo = true });
        db.Add(new PrecoConvenio { ConvenioCodigo = "UnimedPadrao", Tipo = TipoCodigo.Acupuntura, Valor = 145m, CriadoPor = "Prévia" });
        db.Add(new PrecoConvenio { ConvenioCodigo = "UnimedPadrao", Tipo = TipoCodigo.Eletroacupuntura, Valor = 62.5m, CriadoPor = "Prévia" });
        db.Add(new PrecoConvenio { ConvenioCodigo = "Amil", Tipo = TipoCodigo.Consulta, Valor = 210m, CriadoPor = "Prévia" });
        // A retenção na fonte da Amil, para o "Reter?" ter o que mostrar.
        db.Add(new Tributo { Sigla = "IRRF", Nome = "IRRF retido pela operadora", Percentual = 1.5m, ConvenioCodigo = "Amil", CriadoPor = "Prévia" });
        db.Add(new PrecoParticular { ModalidadeCodigo = nameof(ModalidadeAtendimento.AcupunturaSimples), Valor = 180m, CriadoPor = "Prévia" });
        db.SaveChanges();

        // O catálogo de convênios mora num cache em memória: recarregá-lo é o que faz a tela
        // escrever "Sul América Saúde" e "Particular" em vez do nome da família.
        new ConvenioCatalogoService(new ClinicaRepositorio(db)).RecarregarCacheAsync().GetAwaiter().GetResult();

        // ===== Pacientes desta semente =====
        Paciente P(string nome, Convenio familia, string codigo) =>
            db.Add(new Paciente { Nome = nome, Convenio = familia, ConvenioCodigo = codigo, Telefone = "(21) 98888-0000" }).Entity;
        var unimed = new[]
        {
            P("Beatriz Andrade Rocha", Convenio.UnimedPadrao, "UnimedPadrao"),
            P("José Antônio Pereira", Convenio.UnimedPadrao, "UnimedPadrao"),
            P("Cláudia Menezes", Convenio.UnimedPadrao, "UnimedPadrao")
        };
        var amil = new[] { P("Eduardo Salgado", Convenio.Amil, "Amil"), P("Patrícia Lemos", Convenio.Amil, "Amil") };
        var petrobras = P("Rogério Bastos", Convenio.Petrobras, "Petrobras");
        var sulAmerica = P("Helena Figueiredo", Convenio.Personalizado, "SulAmericaPrevia");
        var particulares = new[]
        {
            P("Marina Teixeira", Convenio.UnimedPadrao, ConvenioCadastro.CodigoParticular),
            P("Gustavo Rangel", Convenio.UnimedPadrao, ConvenioCadastro.CodigoParticular),
            P("Larissa Cunha", Convenio.UnimedPadrao, ConvenioCadastro.CodigoParticular)
        };
        db.SaveChanges();

        // ===== Guias baixadas sem receita (aba "A lançar") =====
        var n = 4100;
        CodigoFaturamento Guia(Paciente p, int k, ModalidadeAtendimento modalidade, TipoCodigo tipo,
            StatusCodigo status = StatusCodigo.Baixado, bool baixada = true)
        {
            var data = DiaDoMes(k);
            var atendimento = db.Add(new Atendimento
            {
                PacienteId = p.Id, Data = data, Modalidade = modalidade, LancadoPor = "Prévia",
                LancadoEm = data.ToDateTime(new TimeOnly(9, 0)), RealizadoEm = data.ToDateTime(new TimeOnly(10, 0))
            }).Entity;
            db.SaveChanges();
            var codigo = db.Add(new CodigoFaturamento
            {
                AtendimentoId = atendimento.Id, Tipo = tipo, DataPrevistaFaturamento = data, Status = status,
                DataBaixa = baixada ? data : null, NumeroGuiaReal = baixada ? $"{++n:D6}{k}" : null,
                UsuarioBaixa = baixada ? "Prévia" : null
            }).Entity;
            db.SaveChanges();
            return codigo;
        }

        Guia(unimed[0], 1, ModalidadeAtendimento.AcupunturaSimples, TipoCodigo.Acupuntura);
        Guia(unimed[1], 2, ModalidadeAtendimento.AcupunturaComEletro, TipoCodigo.Acupuntura);
        Guia(unimed[1], 2, ModalidadeAtendimento.AcupunturaComEletro, TipoCodigo.Eletroacupuntura);
        Guia(unimed[2], 4, ModalidadeAtendimento.AcupunturaSimples, TipoCodigo.Acupuntura);
        Guia(amil[0], 3, ModalidadeAtendimento.Consulta, TipoCodigo.Consulta);
        Guia(amil[1], 6, ModalidadeAtendimento.Consulta, TipoCodigo.Consulta);
        Guia(petrobras, 5, ModalidadeAtendimento.AcupunturaSimples, TipoCodigo.ConsultaEspecialidade);
        Guia(sulAmerica, 7, ModalidadeAtendimento.AcupunturaSimples, TipoCodigo.Acupuntura);

        // Glosada SEM receita: aparece em "A lançar", marcada.
        var glosadaSemReceita = Guia(unimed[2], 8, ModalidadeAtendimento.AcupunturaSimples, TipoCodigo.Acupuntura);
        glosadaSemReceita.Glosa = StatusGlosa.Glosada;
        glosadaSemReceita.DataGlosa = DiaDoMes(9);
        glosadaSemReceita.MotivoGlosa = "Carteirinha vencida na data do atendimento";
        glosadaSemReceita.DataLimiteRecurso = DiaDoMes(9).AddDays(30);

        // ===== Glosadas COM receita (aba "Glosadas") =====
        void GlosadaComReceita(Paciente p, int k, StatusLancamento status, string motivo, int prazoDias)
        {
            var c = Guia(p, k, ModalidadeAtendimento.AcupunturaSimples, TipoCodigo.Acupuntura);
            c.Glosa = StatusGlosa.Glosada;
            c.DataGlosa = DiaDoMes(k + 1);
            c.MotivoGlosa = motivo;
            c.DataLimiteRecurso = hoje.AddDays(prazoDias);
            db.Add(new LancamentoFinanceiro
            {
                Data = c.DataBaixa!.Value, Tipo = TipoLancamento.Entrada, Valor = 145m, Status = status,
                DataPagamento = status == StatusLancamento.Realizado ? c.DataBaixa : null,
                Descricao = $"{p.Convenio} — guia {c.NumeroGuiaReal} ({p.Nome})",
                FormaPagamento = FormaPagamento.Convenio, PacienteId = p.Id, AtendimentoId = c.AtendimentoId,
                CodigoFaturamentoId = c.Id, Convenio = p.Convenio, ConvenioCodigo = p.ConvenioCodigo
            });
        }
        GlosadaComReceita(unimed[0], 3, StatusLancamento.Previsto, "Procedimento sem autorização prévia", 12);
        GlosadaComReceita(amil[1], 2, StatusLancamento.Previsto, "Quantidade de sessões acima da cota", -3);
        GlosadaComReceita(unimed[1], 5, StatusLancamento.Realizado, "Duplicidade de cobrança", 20);
        db.SaveChanges();

        // ===== Sessões particulares sem dinheiro (aba "Particulares") =====
        void Particular(Paciente p, int k, ModalidadeAtendimento modalidade)
        {
            var data = DiaDoMes(k);
            var a = db.Add(new Atendimento
            {
                PacienteId = p.Id, Data = data, Modalidade = modalidade, LancadoPor = "Prévia",
                LancadoEm = data.ToDateTime(new TimeOnly(14, 0)), RealizadoEm = data.ToDateTime(new TimeOnly(15, 0))
            }).Entity;
            db.SaveChanges();
            db.Add(new CodigoFaturamento
            {
                AtendimentoId = a.Id, Tipo = modalidade == ModalidadeAtendimento.Consulta ? TipoCodigo.Consulta : TipoCodigo.Acupuntura,
                DataPrevistaFaturamento = data, Status = StatusCodigo.NaoAplicavel
            });
        }
        Particular(particulares[0], 1, ModalidadeAtendimento.AcupunturaSimples);
        Particular(particulares[1], 3, ModalidadeAtendimento.AcupunturaSimples);
        Particular(particulares[2], 5, ModalidadeAtendimento.BsvApenas);
        db.SaveChanges();
    }

    /// <summary>Vendas de cartão: atrasadas, a vencer e confirmadas (uma caiu com atraso).</summary>
    private static void SemearCartao(ClinicaDbContext db, DateOnly hoje)
    {
        void Venda(string adquirente, decimal valor, DateOnly venda, DateOnly previsao, DateOnly? caiu = null,
            FormaPagamento forma = FormaPagamento.CartaoCredito)
        {
            var taxa = Math.Round(valor * (forma == FormaPagamento.CartaoDebito ? 0.0149m : 0.0319m), 2);
            db.Add(new LancamentoFinanceiro
            {
                Data = venda, Tipo = TipoLancamento.Entrada, Valor = valor, Status = StatusLancamento.Realizado,
                DataPagamento = venda, Descricao = $"Sessão particular no cartão — {adquirente}",
                FormaPagamento = forma, Adquirente = adquirente, Bandeira = "Visa",
                TaxaPercentual = forma == FormaPagamento.CartaoDebito ? 1.49m : 3.19m, ValorTaxa = taxa,
                PrevisaoRecebimento = previsao, RecebimentoConfirmadoEm = caiu
            });
        }

        // Atrasados: passaram da data e não caíram.
        Venda("Stone", 180m, hoje.AddDays(-35), hoje.AddDays(-5));
        Venda("Stone", 220m, hoje.AddDays(-35), hoje.AddDays(-5));
        Venda("Cielo", 350m, hoje.AddDays(-10), hoje.AddDays(-2), forma: FormaPagamento.CartaoDebito);
        // A vencer.
        Venda("Stone", 180m, hoje.AddDays(-1), hoje);
        Venda("Cielo", 540m, hoje.AddDays(-29), hoje.AddDays(1));
        Venda("Stone", 1_250m, hoje.AddDays(-12), hoje.AddDays(18));
        Venda("Stone", 360m, hoje.AddDays(-12), hoje.AddDays(18));
        Venda("Rede", 2_400m, hoje.AddDays(-3), hoje.AddDays(27));
        Venda("Cielo", 180m, hoje, hoje.AddDays(45));
        // Já caíram: uma no dia, uma com 3 dias de atraso, uma adiantada.
        Venda("Stone", 420m, hoje.AddDays(-40), hoje.AddDays(-10), caiu: hoje.AddDays(-10));
        Venda("Cielo", 760m, hoje.AddDays(-36), hoje.AddDays(-6), caiu: hoje.AddDays(-3));
        Venda("Rede", 300m, hoje.AddDays(-20), hoje.AddDays(-12), caiu: hoje.AddDays(-13));
        db.SaveChanges();
    }
}
