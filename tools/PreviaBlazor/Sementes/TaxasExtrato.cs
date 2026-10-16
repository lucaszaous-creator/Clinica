// Dados FICTÍCIOS da prévia — nenhuma pessoa, contrato ou conta bancária real.
using System.IO;
using System.Globalization;
using System.Text;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace PreviaBlazor.Sementes;

/// <summary>
/// As telas "Taxas e impostos" e "Extrato do banco" (out/2026).
///
/// Taxas: Stone e Cielo, débito / crédito à vista / parcelado com faixa, uma expirada, uma
/// futura e uma inativa. Tributos: o Lucro Presumido vigente (com IRPJ e CSLL sobre base de
/// 32%), o Simples ENCERRADO no ano passado e uma retenção na fonte da Unimed.
///
/// Extrato: o OFX só existe depois de a pessoa escolher o arquivo, e o hospedeiro da prévia
/// devolve null em <c>EscolherArquivoAsync</c>. Para a tela ter o que mostrar, esta semente
/// cria os lançamentos que o extrato vai encontrar (um par casado, um ambíguo, um lote da
/// maquininha, um já conferido…) e MONTA o OFX correspondente em <see cref="ExemploOfx"/>,
/// com as datas do mês anterior. Ele também é gravado em <c>extrato-exemplo.ofx</c>, ao lado
/// da DLL da prévia — é o arquivo a escolher quando o hospedeiro abrir o seletor de verdade.
/// </summary>
public sealed class SementeTaxasExtrato : ISemente
{
    public int Ordem => 40;

    /// <summary>O OFX de exemplo, coerente com os lançamentos semeados (null antes de semear).</summary>
    public static string? ExemploOfx { get; private set; }

    public void Semear(ClinicaDbContext db)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        SemearTaxas(db, hoje);
        SemearTributos(db, hoje);
        SemearExtrato(db, hoje);
    }

    private static void SemearTaxas(ClinicaDbContext db, DateOnly hoje)
    {
        var inicioDoAno = new DateOnly(hoje.Year, 1, 1);
        TaxaCartao T(string adq, ModalidadeCartao m, decimal p, int dias, string? bandeira = null,
            int? de = null, int? ate = null, bool mensal = false, DateOnly? desde = null, DateOnly? fim = null, bool ativa = true)
            => new()
            {
                Adquirente = adq, Modalidade = m, Percentual = p, DiasParaReceber = dias, Bandeira = bandeira,
                ParcelasDe = de, ParcelasAte = ate, LiquidacaoMensal = mensal, VigenteDe = desde, VigenteAte = fim,
                Ativa = ativa, CriadoPor = "prévia"
            };

        db.AddRange(
            T("Stone", ModalidadeCartao.Debito, 1.39m, 1, desde: inicioDoAno),
            T("Stone", ModalidadeCartao.CreditoAVista, 3.15m, 30, desde: inicioDoAno),
            T("Stone", ModalidadeCartao.CreditoParcelado, 4.2m, 30, de: 2, ate: 6, mensal: true, desde: inicioDoAno),
            T("Stone", ModalidadeCartao.CreditoParcelado, 5.1m, 30, de: 7, ate: 12, mensal: true, desde: inicioDoAno),
            // Renegociação: a regra antiga ENCERRADA fica, e a nova vale desde o dia seguinte.
            T("Cielo", ModalidadeCartao.CreditoAVista, 3.49m, 30, desde: inicioDoAno.AddYears(-1), fim: inicioDoAno.AddDays(-1)),
            T("Cielo", ModalidadeCartao.CreditoAVista, 2.99m, 30, bandeira: "Visa", desde: inicioDoAno),
            T("Cielo", ModalidadeCartao.Debito, 1.6m, 2, desde: hoje.AddMonths(1)),
            T("Rede", ModalidadeCartao.CreditoAVista, 3.8m, 30, ativa: false));
        db.SaveChanges();
    }

    private static void SemearTributos(ClinicaDbContext db, DateOnly hoje)
    {
        var inicioDoAno = new DateOnly(hoje.Year, 1, 1);
        Tributo P(string sigla, string nome, decimal p, decimal b = 100m, string? convenio = null,
            DateOnly? desde = null, DateOnly? fim = null)
            => new()
            {
                Sigla = sigla, Nome = nome, Percentual = p, BasePercentual = b, ConvenioCodigo = convenio,
                VigenteDe = desde, VigenteAte = fim, CriadoPor = "prévia"
            };

        db.AddRange(
            // Simples Nacional até o fim do ano passado — a troca de regime entra como vigência.
            P("SIMPLES", "Simples Nacional (anexo III)", 6m, desde: inicioDoAno.AddYears(-2), fim: inicioDoAno.AddDays(-1)),
            // Lucro Presumido desde janeiro.
            P("ISS", "Imposto Sobre Serviços", 3m, desde: inicioDoAno),
            P("PIS", "Programa de Integração Social", 0.65m, desde: inicioDoAno),
            P("COFINS", "Contribuição para o Financiamento da Seguridade Social", 3m, desde: inicioDoAno),
            P("IRPJ", "Imposto de Renda Pessoa Jurídica", 15m, 32m, desde: inicioDoAno),
            P("CSLL", "Contribuição Social sobre o Lucro Líquido", 9m, 32m, desde: inicioDoAno),
            // A Unimed retém na fonte e SUBSTITUI os gerais nos recebimentos dela.
            P("IRRF", "IR retido na fonte pela operadora", 1.5m, convenio: "UnimedPadrao", desde: inicioDoAno),
            P("CSRF", "CSLL/PIS/COFINS retidos pela operadora", 4.65m, convenio: "UnimedPadrao", desde: inicioDoAno));

        // Um recebimento da Unimed NESTE mês, para a apuração mostrar as duas naturezas
        // (o retido na fonte ao lado do que a clínica ainda vai recolher).
        var dia = new DateOnly(hoje.Year, hoje.Month, 1).AddDays(Math.Max(0, hoje.Day - 3));
        db.Add(new LancamentoFinanceiro
        {
            Data = dia, DataPagamento = dia, Tipo = TipoLancamento.Entrada, Valor = 4_320m,
            Descricao = "Repasse Unimed — guias de setembro", Status = StatusLancamento.Realizado,
            FormaPagamento = FormaPagamento.Convenio, ConvenioCodigo = "UnimedPadrao", CriadoPor = "prévia"
        });
        db.SaveChanges();
    }

    private static void SemearExtrato(ClinicaDbContext db, DateOnly hoje)
    {
        // O mês ANTERIOR, que a semente-base já encheu de lançamentos realizados.
        var mes = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(-1);
        var de = mes;
        var ate = mes.AddDays(13);
        var conta = ("341", "1234", "56789-0");

        var basicos = db.Set<LancamentoFinanceiro>().AsNoTracking()
            .Where(l => l.Data >= mes && l.Data < mes.AddMonths(1) && l.Status == StatusLancamento.Realizado)
            .ToList();
        var folha = basicos.First(l => l.Descricao.StartsWith("Folha"));
        var aluguel = basicos.First(l => l.Descricao.StartsWith("Aluguel"));
        var convenio = basicos.Where(l => l.Descricao.StartsWith("Repasse Unimed")).OrderBy(l => l.Data).First();

        LancamentoFinanceiro L(string descricao, TipoLancamento tipo, decimal valor, DateOnly data, FormaPagamento forma)
            => new()
            {
                Data = data, DataPagamento = data, Tipo = tipo, Valor = valor, Descricao = descricao,
                Status = StatusLancamento.Realizado, FormaPagamento = forma, CriadoPor = "prévia"
            };

        // Dois PIX iguais no mesmo dia: a linha do extrato é AMBÍGUA, e a pessoa escolhe.
        db.Add(L("Sessão avulsa — Ana Beatriz Souza", TipoLancamento.Entrada, 150m, mes.AddDays(2), FormaPagamento.Pix));
        db.Add(L("Sessão avulsa — Tiago Fernandes", TipoLancamento.Entrada, 150m, mes.AddDays(2), FormaPagamento.Pix));

        // O lote da maquininha: três créditos Stone que caem juntos num depósito só.
        var diaDoLote = mes.AddDays(6);
        decimal[] brutos = [180m, 240m, 320m];
        var liquidoDoLote = 0m;
        foreach (var (b, i) in brutos.Select((b, i) => (b, i)))
        {
            var taxa = Math.Round(b * 3.15m / 100m, 2, MidpointRounding.AwayFromZero);
            liquidoDoLote += b - taxa;
            db.Add(new LancamentoFinanceiro
            {
                Data = mes.AddDays(1 + i), DataPagamento = mes.AddDays(1 + i), Tipo = TipoLancamento.Entrada, Valor = b,
                Descricao = $"Pacote de acupuntura — crédito Stone ({i + 1}/3 do lote)", Status = StatusLancamento.Realizado,
                FormaPagamento = FormaPagamento.CartaoCredito, Adquirente = "Stone", ModalidadeCartao = ModalidadeCartao.CreditoAVista,
                Parcelas = 1, TaxaPercentual = 3.15m, ValorTaxa = taxa, PrevisaoRecebimento = diaDoLote, CriadoPor = "prévia"
            });
        }

        // Uma linha JÁ CONFERIDA numa importação anterior do mesmo extrato.
        var identificacao = $"{conta.Item1}|{conta.Item2}|{conta.Item3}";
        var jaConferido = L("Sessões particulares — PIX Fernanda Ribeiro", TipoLancamento.Entrada, 980m, mes.AddDays(11), FormaPagamento.Pix);
        jaConferido.ConciliadoEm = DateTime.Now.AddDays(-3);
        jaConferido.IdBancario = "FIT-0009";
        jaConferido.ContaBancariaConciliacao = identificacao;
        jaConferido.DataExtrato = mes.AddDays(11);
        db.Add(jaConferido);
        db.SaveChanges();

        // O OFX que o banco exportaria para essas mesmas datas.
        var transacoes = new List<(string? Id, DateOnly Data, decimal Valor, string Memo)>
        {
            ("FIT-0001", mes.AddDays(2), 150m, "PIX RECEBIDO - SESSAO AVULSA"),
            ("FIT-0002", folha.Data.AddDays(1), -folha.Valor, "PAGTO FOLHA - LOTE 0001"),
            ("FIT-0003", mes.AddDays(4), -39.90m, "TARIFA PACOTE DE SERVIÇOS"),
            ("FIT-0004", diaDoLote, liquidoDoLote, "STONE PAGAMENTOS - CRÉDITO"),
            ("FIT-0005", mes.AddDays(8), 220m, "PIX RECEBIDO JOSE C SILVA"),
            ("FIT-0006", aluguel.Data.AddDays(1), -aluguel.Valor, "PAGTO BOLETO - IMOBILIARIA FLORES"),
            ("FIT-0007", convenio.Data, convenio.Valor, "TED RECEBIDA - UNIMED"),
            ("FIT-0009", mes.AddDays(11), 980m, "PIX RECEBIDO FERNANDA R"),
            (null, mes.AddDays(12), 75m, "DEPOSITO EM ESPECIE")
        };

        var ofx = new StringBuilder();
        ofx.AppendLine("OFXHEADER:100").AppendLine("DATA:OFXSGML").AppendLine("VERSION:102")
            .AppendLine("ENCODING:USASCII").AppendLine("CHARSET:1252").AppendLine()
            .AppendLine("<OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS><CURDEF>BRL")
            .AppendLine($"<BANKACCTFROM><BANKID>{conta.Item1}<BRANCHID>{conta.Item2}<ACCTID>{conta.Item3}<ACCTTYPE>CHECKING</BANKACCTFROM>")
            .AppendLine($"<BANKTRANLIST><DTSTART>{de:yyyyMMdd}<DTEND>{ate:yyyyMMdd}");
        foreach (var t in transacoes)
        {
            ofx.AppendLine("<STMTTRN>")
                .AppendLine($"<TRNTYPE>{(t.Valor > 0 ? "CREDIT" : "DEBIT")}")
                .AppendLine($"<DTPOSTED>{t.Data:yyyyMMdd}120000[-3:BRT]")
                .AppendLine($"<TRNAMT>{t.Valor.ToString("0.00", CultureInfo.InvariantCulture)}");
            if (t.Id is not null) ofx.AppendLine($"<FITID>{t.Id}");
            ofx.AppendLine($"<MEMO>{t.Memo}").AppendLine("</STMTTRN>");
        }
        ofx.AppendLine("</BANKTRANLIST></STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>");
        ExemploOfx = ofx.ToString();

        try
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "extrato-exemplo.ofx"), ExemploOfx, Encoding.Latin1);
        }
        catch (IOException)
        {
            // Pasta somente leitura: a prévia continua; só não há o arquivo para escolher.
        }
    }
}
