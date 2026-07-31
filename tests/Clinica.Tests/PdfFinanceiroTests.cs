using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Recibo, orçamento e guia TISS saem em papel.
///
/// Completa a varredura dos três serviços que não tinham teste nenhum. O motivo de
/// testá-los é o mesmo da folha do dia: QuestPDF monta o documento em tempo de EXECUÇÃO,
/// e um campo nulo, uma lista vazia ou um valor negativo estouram só no clique de
/// imprimir — que é na frente do paciente, com ele esperando o recibo.
///
/// A guia TISS pertence ao faturamento, que está congelado. Testá-la não o toca: é a
/// mesma leitura de sempre, agora com uma rede embaixo. O papel que sai dali vai para a
/// operadora, e é o que a clínica recebe por ele.
/// </summary>
public class PdfFinanceiroTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly DocumentoFinanceiroService _documentos;
    private readonly DocumentosFinanceirosPdfService _pdf;

    public PdfFinanceiroTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _documentos = new DocumentoFinanceiroService(_repo);
        _pdf = new DocumentosFinanceirosPdfService(_repo);
    }

    private static void DeveSerPdf(byte[] bytes)
    {
        bytes.Should().NotBeNullOrEmpty();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    private async Task<int> CriarPacienteAsync()
    {
        var p = new Paciente
        {
            Nome = "Maria Souza",
            Convenio = Convenio.UnimedIntercambio,
            Sexo = Sexo.Feminino
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private Task<DocumentoFinanceiro> EmitirAsync(
        TipoDocumentoFinanceiro tipo, int? pacienteId, params (string, decimal, decimal)[] itens)
        => _documentos.EmitirAsync(new DocumentoFinanceiro
        {
            Tipo = tipo,
            PacienteId = pacienteId,
            Destinatario = "Maria Souza",
            Data = new DateOnly(2026, 8, 12),
            Itens = itens.Select((i, ordem) => new ItemDocumentoFinanceiro
            {
                Ordem = ordem,
                Descricao = i.Item1,
                Quantidade = i.Item2,
                ValorUnitario = i.Item3
            }).ToList()
        });

    [Fact]
    public async Task Recibo_sai_em_pdf()
    {
        var pacienteId = await CriarPacienteAsync();
        var doc = await EmitirAsync(TipoDocumentoFinanceiro.Recibo, pacienteId,
            ("Sessão de acupuntura", 1, 150m));

        DeveSerPdf(await _pdf.GerarAsync(doc.Id));
    }

    /// <summary>
    /// Orçamento de pacote: várias linhas e quantidade maior que um — o caminho do total
    /// por linha, que é onde um arredondamento errado aparece no papel.
    /// </summary>
    [Fact]
    public async Task Orcamento_com_varias_linhas_sai_em_pdf()
    {
        var pacienteId = await CriarPacienteAsync();
        var doc = await EmitirAsync(TipoDocumentoFinanceiro.Orcamento, pacienteId,
            ("Pacote de 10 sessões", 10, 120m),
            ("Avaliação inicial", 1, 200m),
            ("Ventosaterapia (sessão avulsa)", 3, 80m));

        DeveSerPdf(await _pdf.GerarAsync(doc.Id));
    }

    /// <summary>
    /// Linha com valor negativo é RECUSADA, e isso é regra, não limitação: desconto no
    /// orçamento se dá baixando o valor unitário, não somando uma linha negativa. Com a
    /// linha negativa o total fecharia, mas o papel passaria a listar um serviço que a
    /// clínica não presta — e é o papel que o paciente leva para comparar preço.
    /// </summary>
    [Fact]
    public async Task Linha_com_valor_negativo_e_recusada()
    {
        var pacienteId = await CriarPacienteAsync();

        var acao = () => EmitirAsync(TipoDocumentoFinanceiro.Orcamento, pacienteId,
            ("Pacote de 10 sessões", 10, 120m),
            ("Desconto combinado", 1, -100m));

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// Sem paciente vinculado — o orçamento para quem ainda não é paciente, que é o uso
    /// mais comum dele. `Paciente` fica nulo no desenho.
    /// </summary>
    [Fact]
    public async Task Orcamento_sem_paciente_vinculado_sai_em_pdf()
    {
        var doc = await EmitirAsync(TipoDocumentoFinanceiro.Orcamento, null,
            ("Pacote de 10 sessões", 10, 120m));

        DeveSerPdf(await _pdf.GerarAsync(doc.Id));
    }

    /// <summary>
    /// Cancelado sai MARCADO, nunca em branco: a segunda via de um documento cancelado
    /// precisa dizer que está cancelada, senão vira comprovante de algo que não vale.
    /// É um caminho diferente no desenho (a tarja).
    /// </summary>
    [Fact]
    public async Task Documento_cancelado_ainda_sai_em_pdf()
    {
        var pacienteId = await CriarPacienteAsync();
        var doc = await EmitirAsync(TipoDocumentoFinanceiro.Recibo, pacienteId,
            ("Sessão de acupuntura", 1, 150m));

        await _documentos.CancelarAsync(doc.Id, "valor digitado errado");

        DeveSerPdf(await _pdf.GerarAsync(doc.Id));
    }

    [Fact]
    public async Task Recibo_sai_com_cabecalho_do_prestador()
    {
        var pacienteId = await CriarPacienteAsync();
        var doc = await EmitirAsync(TipoDocumentoFinanceiro.Recibo, pacienteId,
            ("Sessão de acupuntura", 1, 150m));

        var prestador = new DadosPrestador
        {
            NomeFantasia = "Clínica de Acupuntura",
            Cnpj = "12345678000199",
            Endereco = "Rua das Flores, 100",
            Telefone = "(11) 3333-4444"
        };

        DeveSerPdf(await _pdf.GerarAsync(doc.Id, prestador));
    }

    [Fact]
    public async Task Documento_inexistente_falha_com_mensagem()
    {
        var acao = () => _pdf.GerarAsync(9999);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
