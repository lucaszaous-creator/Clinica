using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed class ReceituarioLivreTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly DocumentoClinicoService _documentos;

    public ReceituarioLivreTests()
    {
        _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _documentos = new DocumentoClinicoService(_repo, new ProntuarioService(_repo), new ConsentimentoService(_repo));
    }

    private async Task<(Paciente, Profissional)> PessoasAsync(string? endereco = "Rua de teste, 10, Centro, Cidade")
    {
        var paciente = new Paciente { Nome = "Paciente de teste", Endereco = endereco, Convenio = Convenio.UnimedIntercambio };
        var profissional = new Profissional { Nome = "Profissional de teste", RegistroConselho = "CRM-SP 123456" };
        _db.AddRange(paciente, profissional);
        await _db.SaveChangesAsync();
        return (paciente, profissional);
    }

    [Fact]
    public async Task Texto_livre_longo_persiste_e_gera_pdf_sem_itens_artificiais()
    {
        var (paciente, profissional) = await PessoasAsync();
        var texto = string.Join("\n\n", Enumerable.Range(1, 100).Select(i => $"Parágrafo {i}: texto escrito pelo profissional, com acentos e orientações de uso."));
        var emitida = await _documentos.EmitirAsync(new DocumentoClinico
        {
            Tipo = TipoDocumentoClinico.Receita, PacienteId = paciente.Id,
            ProfissionalId = profissional.Id, Corpo = texto
        }, "profissional.teste");
        _db.ChangeTracker.Clear();
        var salva = (await _documentos.ObterAsync(emitida.Id))!;
        salva.Corpo.Should().Be(texto);
        salva.Itens.Should().BeEmpty();
        ConformidadeDocumentoClinico.Conferir(salva, assinaturaEletronica: true).Should().BeEmpty();
        var pdf = await new DocumentosClinicosPdfService(_repo).GerarAsync(salva.Id, null);
        pdf.Length.Should().BeGreaterThan(5000);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \r\n ")]
    public async Task Receita_vazia_nao_e_numerada(string? texto)
    {
        var (paciente, profissional) = await PessoasAsync();
        var acao = () => _documentos.EmitirAsync(new DocumentoClinico
        {
            Tipo = TipoDocumentoClinico.Receita, PacienteId = paciente.Id,
            ProfissionalId = profissional.Id, Corpo = texto
        });
        await acao.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Escreva a prescrição*");
        (await _db.DocumentosClinicos.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Endereco_completado_na_emissao_preserva_outros_dados_e_registra_autoria()
    {
        var (paciente, _) = await PessoasAsync(null);
        paciente.Carteirinha = "carteirinha preservada";
        paciente.Telefone = "11999999999";
        await _db.SaveChangesAsync();
        await new PacienteService(_repo).CompletarEnderecoAsync(paciente.Id, "  Rua Nova, 42, Centro, Cidade  ", "medica.teste");
        _db.ChangeTracker.Clear();
        var salvo = (await _repo.ObterPacienteAsync(paciente.Id))!;
        salvo.Endereco.Should().Be("Rua Nova, 42, Centro, Cidade");
        salvo.Carteirinha.Should().Be("carteirinha preservada");
        salvo.Telefone.Should().Be("11999999999");
        salvo.Convenio.Should().Be(Convenio.UnimedIntercambio);
        var evento = await _db.Set<EventoAuditoria>().SingleAsync(e => e.Acao == "EnderecoCompletadoNaEmissao");
        evento.Operador.Should().Be("medica.teste");
        evento.PacienteId.Should().Be(paciente.Id);
    }

    [Fact]
    public async Task Endereco_ja_preenchido_por_outro_operador_nao_e_sobrescrito()
    {
        var (paciente, _) = await PessoasAsync("Endereço atualizado pelo balcão");
        await new PacienteService(_repo).CompletarEnderecoAsync(paciente.Id, "Resposta em janela antiga", "medica.teste");
        _db.ChangeTracker.Clear();
        (await _repo.ObterPacienteAsync(paciente.Id))!.Endereco.Should().Be("Endereço atualizado pelo balcão");
        (await _db.Set<EventoAuditoria>().CountAsync(e => e.Acao == "EnderecoCompletadoNaEmissao")).Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(301)]
    public async Task Endereco_invalido_nao_altera_cadastro(int tamanho)
    {
        var (paciente, _) = await PessoasAsync(null);
        var acao = () => new PacienteService(_repo).CompletarEnderecoAsync(paciente.Id, new string('x', tamanho), "medica.teste");
        await acao.Should().ThrowAsync<InvalidOperationException>();
        paciente.Endereco.Should().BeNull();
    }

    [Fact]
    public void Modelo_antigo_conserva_corpo_quantidade_e_orientacoes_no_texto_livre()
    {
        var modelo = new ModeloDocumento
        {
            Corpo = "Cabeçalho\nSegunda linha",
            Itens = [new ItemModelo { Ordem = 2, Descricao = "Segundo", Quantidade = "Q2", Detalhe = "Uso 2" },
                     new ItemModelo { Ordem = 1, Descricao = "Primeiro", Quantidade = "Q1", Detalhe = "Uso 1" }]
        };
        var texto = TextoReceituario.DoModelo(modelo);
        texto.Should().StartWith("Cabeçalho\nSegunda linha");
        texto.Should().EndWith(string.Join(Environment.NewLine,
            "Primeiro", "Q1", "Uso 1", "", "Segundo", "Q2", "Uso 2"));
        modelo.Itens[0].Descricao.Should().Be("Segundo");
    }

    [Fact]
    public void Sugestao_acrescentada_preserva_texto_manual_e_quebras_de_linha()
    {
        const string manual = "Texto já escrito.\n\nCom espaçamento.  ";
        var combinado = TextoReceituario.Acrescentar(manual, "Sugestão da clínica");
        combinado.Should().StartWith(manual).And.EndWith("Sugestão da clínica");
        TextoReceituario.Acrescentar(manual, " ").Should().Be(manual);
    }

    [Fact]
    public async Task Alerta_de_alergia_encontra_termo_no_meio_do_receituario_livre()
    {
        var (paciente, _) = await PessoasAsync();
        _db.Add(new ProblemaPaciente { PacienteId = paciente.Id, Natureza = NaturezaProblema.Alergia,
            Situacao = SituacaoProblema.Ativo, Descricao = "dipirona" });
        await _db.SaveChangesAsync();
        var conferencia = await new PrescricaoService(_repo).ConferirAsync(paciente.Id,
            ["Texto livre com parágrafos.\n\nDipirona\nModo de usar escrito pelo profissional."]);
        conferencia.ExigeConfirmacao.Should().BeTrue();
    }

    [Fact]
    public async Task Infusao_aceita_texto_longo_e_reabre_com_os_parametros_prescritos()
    {
        var (paciente, profissional) = await PessoasAsync();
        var servico = new PrescricaoInternaService(_repo, new PrescricaoService(_repo));
        var prescricao = await servico.CriarAsync(paciente.Id, profissional.Id);
        var texto = string.Join("\n", Enumerable.Repeat("Prescrição livre escrita pelo profissional.", 150));
        await servico.SalvarRascunhoAsync(prescricao.Id, null, null,
            [new ItemPrescricaoInterna { Descricao = texto, Diluente = "Diluente informado", TempoInfusao = "Tempo informado" }]);
        _db.ChangeTracker.Clear();
        var salva = (await servico.ObterAsync(prescricao.Id))!;
        salva.Itens.Single().Descricao.Should().Be(texto);
        salva.Itens.Single().Diluente.Should().Be("Diluente informado");
        salva.Itens.Single().TempoInfusao.Should().Be("Tempo informado");
        salva.Situacao.Should().Be(SituacaoPrescricao.Rascunho);
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }
}
