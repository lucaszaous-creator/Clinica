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
/// Backup e restauração da base (parcela 34).
///
/// O que estes testes protegem não é o formato do arquivo — é o que a clínica perde se
/// eles falharem:
///
/// 1. **O backup cobre a base INTEIRA.** O que existia antes salvava sete tabelas de
///    quarenta e nove, e ninguém notou porque backup incompleto tem exatamente a mesma
///    cara de backup completo até o dia em que se precisa dele.
/// 2. **O que sai volta igual.** Data que vira texto e não volta a ser data, decimal que
///    perde a casa, nulo que vira string vazia — o arquivo continua abrindo, e o dado
///    está errado.
/// 3. **Restaurar por cima é RECUSADO.** Misturar o de ontem com o de hoje não tem volta,
///    e é o erro que alguém comete às três da manhã tentando consertar outra coisa.
/// </summary>
public class BackupServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly BackupService _backup;

    public BackupServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = Criar(_conn);
        _db.Database.EnsureCreated();
        _backup = new BackupService(_db);
    }

    private static ClinicaDbContext Criar(SqliteConnection conexao)
        => new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options);

    private async Task<Paciente> SemearAsync()
    {
        var paciente = new Paciente
        {
            Nome = "Maria Souza",
            Convenio = Convenio.UnimedIntercambio,
            Sexo = Sexo.Feminino,
            Carteirinha = "0123456789",
            DataNascimento = new DateOnly(1980, 4, 3),
            Telefone = null
        };
        _db.Pacientes.Add(paciente);
        await _db.SaveChangesAsync();

        var atendimentos = new AtendimentoService(new ClinicaRepositorio(_db));
        await atendimentos.LancarAsync(paciente.Id, new DateOnly(2026, 8, 12),
            ModalidadeAtendimento.AcupunturaComEletro);

        _db.Lancamentos.Add(new LancamentoFinanceiro
        {
            Tipo = TipoLancamento.Entrada,
            Status = StatusLancamento.Realizado,
            Data = new DateOnly(2026, 8, 12),
            DataPagamento = new DateOnly(2026, 8, 12),
            Valor = 150.75m,
            Descricao = "Sessão",
            FormaPagamento = FormaPagamento.Pix
        });
        await _db.SaveChangesAsync();

        return paciente;
    }

    private async Task<MemoryStream> GerarAsync()
    {
        var ms = new MemoryStream();
        await _backup.GerarAsync(ms);
        ms.Position = 0;
        return ms;
    }

    /// <summary>
    /// O backup cobre a base inteira — todas as tabelas do modelo, não uma lista
    /// escrita à mão que envelhece a cada entidade nova.
    /// </summary>
    [Fact]
    public async Task Backup_cobre_todas_as_tabelas_do_modelo()
    {
        await SemearAsync();

        using var ms = await GerarAsync();
        var manifesto = await _backup.ConferirAsync(ms);

        var tabelasDoModelo = _db.Model.GetEntityTypes()
            .Select(t => t.GetTableName())
            .Where(n => n is not null)
            .Distinct()
            .Count();

        manifesto.TotalTabelas.Should().Be(tabelasDoModelo,
            "o backup antigo salvava sete tabelas de quarenta e nove, e backup incompleto "
            + "tem a mesma cara de backup completo até o dia em que se precisa dele");

        manifesto.TotalTabelas.Should().BeGreaterThan(40,
            "a suíte tem prontuário, financeiro, pacotes, estoque, acessos e auditoria");
    }

    /// <summary>
    /// As tabelas que a suíte inteira usa e que o backup do faturamento nunca cobriu.
    /// Cada nome aqui é uma coisa que a clínica perderia para sempre.
    /// </summary>
    [Theory]
    [InlineData("Evolucoes")]
    [InlineData("DocumentosClinicos")]
    [InlineData("Lancamentos")]
    [InlineData("PacotesPaciente")]
    [InlineData("MovimentosEstoque")]
    [InlineData("Usuarios")]
    [InlineData("Auditoria")]
    public async Task Backup_inclui_as_tabelas_que_o_antigo_deixava_de_fora(string tabela)
    {
        await SemearAsync();

        using var ms = await GerarAsync();
        var manifesto = await _backup.ConferirAsync(ms);

        manifesto.Tabela(tabela).Should().NotBeNull();
    }

    [Fact]
    public async Task Manifesto_conta_as_linhas_que_existem()
    {
        await SemearAsync();

        using var ms = await GerarAsync();
        var manifesto = await _backup.ConferirAsync(ms);

        manifesto.Vazio.Should().BeFalse();
        manifesto.Tabela("Pacientes")!.Linhas.Should().Be(1);
        manifesto.Tabela("Lancamentos")!.Linhas.Should().Be(1);
        manifesto.Tabela("Codigos")!.Linhas.Should().BeGreaterThan(1,
            "a eletroacupuntura gera dois códigos");
    }

    /// <summary>
    /// Base recém-instalada: o backup sai, e o manifesto **diz que está vazio** em vez de
    /// parecer bem-sucedido. Backup vazio é tecnicamente válido e praticamente inútil —
    /// a diferença precisa estar escrita.
    /// </summary>
    [Fact]
    public async Task Backup_de_base_vazia_sai_e_se_declara_vazio()
    {
        using var ms = await GerarAsync();
        var manifesto = await _backup.ConferirAsync(ms);

        manifesto.Vazio.Should().BeTrue();
        manifesto.TotalTabelas.Should().BeGreaterThan(40, "as tabelas existem, sem linhas");
    }

    /// <summary>
    /// O teste central: o que sai volta IGUAL, numa base limpa. Data, decimal, nulo e
    /// enum são justamente os quatro que se estragam em silêncio na ida e volta por texto.
    /// </summary>
    [Fact]
    public async Task O_que_sai_no_backup_volta_igual_na_restauracao()
    {
        var original = await SemearAsync();

        using var ms = await GerarAsync();

        // Base nova e vazia, como a de uma máquina recém-formatada.
        using var destinoConn = new SqliteConnection("DataSource=:memory:");
        destinoConn.Open();
        using var destino = Criar(destinoConn);
        destino.Database.EnsureCreated();

        var resultado = await new BackupService(destino)
            .RestaurarAsync(ms, confirmado: true, operador: "suporte");

        resultado.LinhasRestauradas.Should().BeGreaterThan(0);

        var voltou = await destino.Pacientes.AsNoTracking().SingleAsync();
        voltou.Nome.Should().Be(original.Nome);
        voltou.Carteirinha.Should().Be(original.Carteirinha);
        voltou.DataNascimento.Should().Be(original.DataNascimento,
            "data que vira texto e não volta a ser data é o defeito clássico do backup");
        voltou.Telefone.Should().BeNull("nulo tem de voltar nulo, nunca string vazia");
        voltou.Convenio.Should().Be(Convenio.UnimedIntercambio);

        var lancamento = await destino.Lancamentos.AsNoTracking().SingleAsync();
        lancamento.Valor.Should().Be(150.75m, "decimal não pode perder casa na ida e volta");
        lancamento.FormaPagamento.Should().Be(FormaPagamento.Pix);

        var atendimento = await destino.Atendimentos.AsNoTracking()
            .Include(a => a.Codigos).SingleAsync();
        atendimento.Codigos.Should().HaveCountGreaterThan(1,
            "os filhos voltam ligados ao pai — é para isso que a ordem de dependência existe");
    }

    /// <summary>
    /// Restaurar POR CIMA é recusado. É o erro que alguém comete às três da manhã
    /// tentando consertar outra coisa, e não tem volta.
    /// </summary>
    [Fact]
    public async Task Restaurar_em_base_com_dados_e_recusado()
    {
        await SemearAsync();
        using var ms = await GerarAsync();

        var acao = () => _backup.RestaurarAsync(ms, confirmado: true);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não está vazia*");
    }

    /// <summary>
    /// Sem confirmação explícita não restaura. A confirmação é do CHAMADOR, para uma tela
    /// não conseguir disparar a restauração por acidente de navegação.
    /// </summary>
    [Fact]
    public async Task Restaurar_sem_confirmacao_e_recusado()
    {
        using var ms = await GerarAsync();

        var acao = () => _backup.RestaurarAsync(ms, confirmado: false);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// Arquivo que não é backup falha com mensagem, não com exceção de serialização
    /// vazando para a tela.
    /// </summary>
    [Fact]
    public async Task Arquivo_que_nao_e_backup_falha_com_mensagem()
    {
        using var lixo = new MemoryStream("{\"qualquer\":1}"u8.ToArray());

        var acao = () => _backup.ConferirAsync(lixo);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// A restauração deixa RASTRO na auditoria. Substituir a base inteira sem registro de
    /// quem fez e de qual backup veio é o tipo de operação que ninguém consegue explicar
    /// depois — e é a mais grave que este sistema permite.
    /// </summary>
    [Fact]
    public async Task Restauracao_grava_auditoria_com_quem_fez_e_de_quando_veio()
    {
        await SemearAsync();
        using var ms = await GerarAsync();

        using var destinoConn = new SqliteConnection("DataSource=:memory:");
        destinoConn.Open();
        using var destino = Criar(destinoConn);
        destino.Database.EnsureCreated();

        await new BackupService(destino).RestaurarAsync(ms, confirmado: true, operador: "suporte");

        var evento = await destino.Auditoria.AsNoTracking()
            .SingleAsync(e => e.Acao == "BaseRestaurada");

        evento.Operador.Should().Be("suporte");
        evento.Detalhe.Should().Contain("linha(s)");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
