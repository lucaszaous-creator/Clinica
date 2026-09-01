using System.Text;
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
/// A importação de pacientes do sistema anterior (set/2026 — a clínica migrou do Smart
/// Clinic). O que se fixa: o leitor lê o que o Excel grava (separador, aspas, Latin-1),
/// a sugestão de colunas acerta os nomes comuns, a prévia classifica cada linha pelo
/// FATO (chave, CPF, nome+nascimento) e a execução é IDEMPOTENTE e nunca sobrescreve.
/// </summary>
public class ImportacaoPacientesTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ImportacaoPacientesService _servico;

    private static readonly ConvenioCadastro Unimed = new()
    {
        Codigo = "UnimedPadrao", Nome = "Unimed Costa do Sol", Familia = Convenio.UnimedPadrao
    };
    private static readonly ConvenioCadastro Particular = new()
    {
        Codigo = "PARTICULAR", Nome = "Particular", Familia = Convenio.Personalizado, GeraGuia = false
    };

    private static readonly Dictionary<string, ConvenioCadastro> Convenios = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Unimed"] = Unimed,
        [ImportacaoPacientesService.ConvenioEmBranco] = Particular
    };

    // CPFs válidos pelo dígito verificador.
    private const string Cpf1 = "52998224725";
    private const string Cpf2 = "11144477735";
    private const string Cpf3 = "12345678909";

    public ImportacaoPacientesTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _servico = new ImportacaoPacientesService(_repo, new PacienteService(_repo));
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    private static TabelaImportada Tabela(string csv) => LeitorCsv.Ler(csv);

    private static MapeamentoImportacao MapaPadrao()
    {
        var m = new MapeamentoImportacao();
        m.Definir(CampoImportacao.IdOrigem, 0);
        m.Definir(CampoImportacao.Nome, 1);
        m.Definir(CampoImportacao.Cpf, 2);
        m.Definir(CampoImportacao.Telefone, 3);
        m.Definir(CampoImportacao.DataNascimento, 4);
        m.Definir(CampoImportacao.Sexo, 5);
        m.Definir(CampoImportacao.Convenio, 6);
        return m;
    }

    private const string Cabecalho = "Id;Nome;CPF;Telefone;Nascimento;Sexo;Convenio";

    // ================================================================ leitor

    [Fact]
    public void Leitor_le_ponto_e_virgula_com_aspas_e_quebra_de_linha_dentro_do_campo()
    {
        var t = LeitorCsv.Ler("Nome;Obs\n\"Silva; João\";\"linha 1\nlinha 2\"\n\"Diz \"\"oi\"\"\";x\n");

        t.Separador.Should().Be(';');
        t.Colunas.Should().Equal("Nome", "Obs");
        t.Linhas.Should().HaveCount(2);
        t.Linhas[0][0].Should().Be("Silva; João");
        t.Linhas[0][1].Should().Be("linha 1\nlinha 2");
        t.Linhas[1][0].Should().Be("Diz \"oi\"");
    }

    [Fact]
    public void Leitor_detecta_virgula_e_apara_linha_curta()
    {
        var t = LeitorCsv.Ler("a,b,c\r\n1,2\r\n\r\n4,5,6,7\r\n");
        t.Separador.Should().Be(',');
        t.Linhas.Should().HaveCount(2, "linha em branco não conta");
        t.Linhas[0].Should().Equal("1", "2", "");
        t.Linhas[1].Should().Equal("4", "5", "6");
    }

    [Fact]
    public void Leitor_cai_para_Latin1_quando_o_arquivo_nao_e_UTF8()
    {
        var bytes = Encoding.Latin1.GetBytes("Nome\nConceição\n");
        var t = LeitorCsv.Ler(bytes);
        t.Codificacao.Should().Contain("Latin-1");
        t.Linhas[0][0].Should().Be("Conceição");

        var utf8 = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("Nome\nConceição\n")).ToArray();
        var t2 = LeitorCsv.Ler(utf8);
        t2.Codificacao.Should().Be("UTF-8");
        t2.Colunas[0].Should().Be("Nome", "o BOM não entra no nome da coluna");
        t2.Linhas[0][0].Should().Be("Conceição");
    }

    // ================================================================ sugestão

    [Fact]
    public void Sugestor_acerta_os_nomes_comuns_e_nao_confunde_nome_com_nome_do_convenio()
    {
        var colunas = new[]
        {
            "Código", "Nome do convênio", "Nome do paciente", "CPF", "Telefone celular", "Data de Nascimento",
            "Sexo", "Nº da carteirinha", "Validade", "Endereço", "Como conheceu", "Observações"
        };
        var m = SugestorDeMapeamento.Sugerir(colunas);

        m.ColunaDe(CampoImportacao.IdOrigem).Should().Be(0);
        m.ColunaDe(CampoImportacao.Convenio).Should().Be(1);
        m.ColunaDe(CampoImportacao.Nome).Should().Be(2);
        m.ColunaDe(CampoImportacao.Cpf).Should().Be(3);
        m.ColunaDe(CampoImportacao.Telefone).Should().Be(4);
        m.ColunaDe(CampoImportacao.DataNascimento).Should().Be(5);
        m.ColunaDe(CampoImportacao.Sexo).Should().Be(6);
        m.ColunaDe(CampoImportacao.Carteirinha).Should().Be(7);
        m.ColunaDe(CampoImportacao.ValidadeCarteirinha).Should().Be(8);
        m.ColunaDe(CampoImportacao.Endereco).Should().Be(9);
        m.ColunaDe(CampoImportacao.Origem).Should().Be(10);
        m.ColunaDe(CampoImportacao.Observacoes).Should().Be(11);
    }

    // ================================================================ prévia

    [Fact]
    public async Task Previa_classifica_nova_completar_por_cpf_com_mascara_ja_importada_e_problemas()
    {
        _db.Pacientes.Add(new Paciente
        {
            Nome = "Maria Antiga", Documento = "111.444.777-35", Convenio = Convenio.Amil,
            Sexo = Sexo.Feminino
        });
        _db.Pacientes.Add(new Paciente
        {
            Nome = "Já Importado", Convenio = Convenio.Amil, Sexo = Sexo.Masculino,
            ChaveImportacao = ImportacaoPacientesService.Chave("smartclinic", "77")
        });
        await _db.SaveChangesAsync();

        var csv = Cabecalho + "\n"
            + $"10;Ana Nova;{Cpf1};(22) 99999-1111;05/03/1980;F;Unimed\n"
            + $"11;Maria Antiga;{Cpf2};;1970-01-02;F;Unimed\n"
            + "77;Já Importado;;;;M;Unimed\n"
            + "12;CPF Ruim;12345678900;;;M;Unimed\n"
            + "13;Sem Convenio;;;;F;Bradesco\n"
            + "14;;;;;;Unimed\n"
            + "10;Id Repetido;;;;M;Unimed\n"
            + "15;Particular Sem Sexo;;;;;\n";

        var previa = await _servico.PreverAsync(Tabela(csv), MapaPadrao(), Convenios);

        previa.Linhas.Should().HaveCount(8);
        var porNumero = previa.Linhas.ToDictionary(l => l.Numero);

        porNumero[2].Destino.Should().Be(DestinoLinha.Criar);
        porNumero[2].Ficha!.Documento.Should().Be(Cpf1);
        porNumero[2].Ficha!.Telefone.Should().Be("(22) 99999-1111");
        porNumero[2].Ficha!.DataNascimento.Should().Be(new DateOnly(1980, 3, 5));
        porNumero[2].Ficha!.Sexo.Should().Be(Sexo.Feminino);
        porNumero[2].Ficha!.ConvenioCodigo.Should().Be("UnimedPadrao");
        porNumero[2].Ficha!.ChaveImportacao.Should().Be("IMPORT:smartclinic:10");

        porNumero[3].Destino.Should().Be(DestinoLinha.Completar, "o CPF com máscara na base é o mesmo CPF");
        porNumero[3].PacienteExistenteId.Should().NotBeNull();

        porNumero[4].Destino.Should().Be(DestinoLinha.JaImportada);
        porNumero[5].Destino.Should().Be(DestinoLinha.Problema);
        porNumero[5].Detalhe.Should().Contain("CPF inválido");
        porNumero[6].Destino.Should().Be(DestinoLinha.Problema);
        porNumero[6].Detalhe.Should().Contain("Bradesco");
        porNumero[7].Destino.Should().Be(DestinoLinha.Problema);
        porNumero[7].Detalhe.Should().Contain("sem nome");
        porNumero[8].Destino.Should().Be(DestinoLinha.Problema);
        porNumero[8].Detalhe.Should().Contain("se repete");

        porNumero[9].Destino.Should().Be(DestinoLinha.Criar, "convênio em branco mapeado para Particular");
        porNumero[9].Ficha!.ConvenioCodigo.Should().Be("PARTICULAR");
        porNumero[9].Avisos.Should().ContainSingle(a => a.Contains("Sexo não informado"));

        previa.Criar.Should().Be(2);
        previa.Completar.Should().Be(1);
        previa.JaImportadas.Should().Be(1);
        previa.Problemas.Should().Be(4);
    }

    [Fact]
    public async Task Previa_reconhece_pelo_nome_E_nascimento_e_so_avisa_no_homonimo()
    {
        _db.Pacientes.Add(new Paciente
        {
            Nome = "José da Silva", DataNascimento = new DateOnly(1960, 5, 5),
            Convenio = Convenio.Amil, Sexo = Sexo.Masculino
        });
        await _db.SaveChangesAsync();

        var csv = Cabecalho + "\n"
            + "1;JOSÉ DA SILVA;;;05/05/1960;M;Unimed\n"
            + "2;José da Silva;;;01/01/1990;M;Unimed\n";
        var previa = await _servico.PreverAsync(Tabela(csv), MapaPadrao(), Convenios);

        previa.Linhas[0].Destino.Should().Be(DestinoLinha.Completar);
        previa.Linhas[0].Avisos.Should().ContainSingle(a => a.Contains("nome e pela data"));
        previa.Linhas[1].Destino.Should().Be(DestinoLinha.Criar, "mesmo nome com outro nascimento é homônimo");
        previa.Linhas[1].Avisos.Should().ContainSingle(a => a.Contains("mesmo nome"));
    }

    [Fact]
    public async Task Previa_exige_a_coluna_do_nome()
    {
        var m = new MapeamentoImportacao();
        var act = () => _servico.PreverAsync(Tabela(Cabecalho + "\n1;x;;;;;Unimed\n"), m, Convenios);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*NOME*");
    }

    [Fact]
    public void Datas_sexo_e_origem_sao_lidos_com_tolerancia()
    {
        var avisos = new List<string>();
        ImportacaoPacientesService.LerData("05/03/1980 00:00:00", "nascimento", avisos, nascimento: true)
            .Should().Be(new DateOnly(1980, 3, 5));
        ImportacaoPacientesService.LerData("1980-03-05", "nascimento", avisos).Should().Be(new DateOnly(1980, 3, 5));
        ImportacaoPacientesService.LerData("31/02/1980", "nascimento", avisos).Should().BeNull();
        ImportacaoPacientesService.LerData("05/03/2090", "nascimento", avisos, nascimento: true).Should().BeNull();
        avisos.Should().HaveCount(2);

        ImportacaoPacientesService.LerSexo("Feminino", true, avisos).Should().Be(Sexo.Feminino);
        ImportacaoPacientesService.LerSexo("m", true, avisos).Should().Be(Sexo.Masculino);
        ImportacaoPacientesService.LerSexo("Homem", true, avisos).Should().Be(Sexo.Masculino);

        ImportacaoPacientesService.LerOrigem("Indicação: Maria Souza").Should()
            .Be((OrigemPaciente.Indicacao, "Maria Souza", (string?)null));
        ImportacaoPacientesService.LerOrigem("Instagram").Origem.Should().Be(OrigemPaciente.RedesSociais);
        ImportacaoPacientesService.LerOrigem("Google").Origem.Should().Be(OrigemPaciente.Internet);
        var outro = ImportacaoPacientesService.LerOrigem("Feira de saúde");
        outro.Origem.Should().Be(OrigemPaciente.Outro);
        outro.Observacao.Should().Contain("Feira de saúde");
        ImportacaoPacientesService.LerOrigem("").Origem.Should().BeNull("em branco é NÃO PERGUNTADO");

        ImportacaoPacientesService.LerTelefone("(22) 99999-1111 / (22) 3333-2222").Should().Be("(22) 99999-1111");
    }

    // ================================================================ execução

    [Fact]
    public async Task Executar_cria_completa_sem_sobrescrever_grava_auditoria_e_e_idempotente()
    {
        var antiga = new Paciente
        {
            Nome = "Maria Antiga", Documento = "111.444.777-35", Telefone = "(22) 1111-1111",
            Convenio = Convenio.Amil, ConvenioCodigo = "Amil", Sexo = Sexo.Feminino,
            Categoria = Categoria.Vermelha
        };
        _db.Pacientes.Add(antiga);
        await _db.SaveChangesAsync();

        var csv = Cabecalho + "\n"
            + $"10;Ana Nova;{Cpf1};(22) 99999-1111;05/03/1980;F;Unimed\n"
            + $"11;Maria Antiga;{Cpf2};(22) 9999-0000;1970-01-02;F;Unimed\n"
            + $"12;Sem Conv;{Cpf3};;;M;Bradesco\n";

        var previa = await _servico.PreverAsync(Tabela(csv), MapaPadrao(), Convenios);
        var resultado = await _servico.ExecutarAsync(previa, "direcao");

        resultado.Criados.Should().Be(1);
        resultado.Completados.Should().Be(1);
        resultado.Erros.Should().BeEmpty();

        var fichas = await _db.Pacientes.AsNoTracking().OrderBy(p => p.Id).ToListAsync();
        fichas.Should().HaveCount(2, "a linha com problema não entra");

        var maria = fichas[0];
        maria.Telefone.Should().Be("(22) 1111-1111", "campo preenchido na ficha NUNCA é sobrescrito");
        maria.DataNascimento.Should().Be(new DateOnly(1970, 1, 2), "campo vazio é completado");
        maria.Convenio.Should().Be(Convenio.Amil, "o convênio da ficha é o que fatura");
        maria.Categoria.Should().Be(Categoria.Vermelha, "a categoria da ficha não se recalcula pela importação");
        maria.ChaveImportacao.Should().Be("IMPORT:smartclinic:11");

        var ana = fichas[1];
        ana.Nome.Should().Be("Ana Nova");
        ana.Documento.Should().Be(Cpf1);
        ana.ChaveImportacao.Should().Be("IMPORT:smartclinic:10");
        ana.Categoria.Should().NotBe(default(Categoria), "ficha nova ganha a categoria do convênio");

        var trilha = await _db.Auditoria.AsNoTracking().ToListAsync();
        trilha.Should().Contain(e => e.Acao == "PacienteImportado" && e.Detalhe!.Contains("Ana Nova") && e.Operador == "direcao");
        trilha.Should().Contain(e => e.Acao == "PacienteCompletadoPorImportacao" && e.PacienteId == maria.Id
                                     && e.Detalhe!.Contains("nascimento"));
        trilha.Should().Contain(e => e.Acao == "ImportacaoPacientes");

        // ---- o MESMO arquivo de novo: ninguém duplica ----
        var previa2 = await _servico.PreverAsync(Tabela(csv), MapaPadrao(), Convenios);
        previa2.JaImportadas.Should().Be(2);
        previa2.Criar.Should().Be(0);
        previa2.Completar.Should().Be(0);

        var resultado2 = await _servico.ExecutarAsync(previa2, "direcao");
        resultado2.Criados.Should().Be(0);
        resultado2.Completados.Should().Be(0);
        (await _db.Pacientes.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Executar_reconfere_a_chave_no_instante_de_gravar()
    {
        var csv = Cabecalho + $"\n10;Ana Nova;{Cpf1};;;F;Unimed\n";
        var previa = await _servico.PreverAsync(Tabela(csv), MapaPadrao(), Convenios);

        // Outra máquina importou o mesmo arquivo entre a prévia e o clique.
        _db.Pacientes.Add(new Paciente
        {
            Nome = "Ana Nova", Convenio = Convenio.UnimedPadrao, Sexo = Sexo.Feminino,
            ChaveImportacao = "IMPORT:smartclinic:10"
        });
        await _db.SaveChangesAsync();

        var resultado = await _servico.ExecutarAsync(previa, "direcao");
        resultado.Criados.Should().Be(0);
        resultado.Pulados.Should().Be(1);
        (await _db.Pacientes.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Linha_que_falha_na_gravacao_vira_erro_e_nao_derruba_as_outras()
    {
        var csv = Cabecalho + "\n"
            + $"10;Ana Nova;{Cpf1};;;F;Unimed\n"
            + $"11;Bia;{Cpf2};;;F;Unimed\n";
        var previa = await _servico.PreverAsync(Tabela(csv), MapaPadrao(), Convenios);

        // O CPF da Bia entrou na base por outra porta depois da prévia: o PacienteService recusa.
        _db.Pacientes.Add(new Paciente { Nome = "Beatriz", Documento = Cpf2, Convenio = Convenio.Amil, Sexo = Sexo.Feminino });
        await _db.SaveChangesAsync();

        var resultado = await _servico.ExecutarAsync(previa, "direcao");
        resultado.Criados.Should().Be(1);
        resultado.Erros.Should().ContainSingle(e => e.Contains("Bia") && e.Contains("já está cadastrado"));
        (await _db.Pacientes.CountAsync(p => p.Nome == "Ana Nova")).Should().Be(1);

        // A trilha só afirma o que aconteceu: a linha recusada NÃO deixa "PacienteImportado".
        var trilha = await _db.Auditoria.AsNoTracking().ToListAsync();
        trilha.Should().NotContain(e => e.Acao == "PacienteImportado" && e.Detalhe!.Contains("Bia"));
        trilha.Should().ContainSingle(e => e.Acao == "PacienteImportado" && e.Detalhe!.Contains("Ana Nova")
                                            && e.PacienteId != null);
    }

    [Fact]
    public async Task Fichas_resumidas_trazem_o_que_a_importacao_compara()
    {
        _db.Pacientes.Add(new Paciente
        {
            Nome = "X", Documento = "111.444.777-35", DataNascimento = new DateOnly(2000, 1, 1),
            Convenio = Convenio.Amil, Sexo = Sexo.Feminino, ChaveImportacao = "IMPORT:a:1",
            FotoMiniatura = new byte[] { 1, 2, 3 }
        });
        await _db.SaveChangesAsync();

        var fichas = await _repo.FichasResumidasAsync();
        fichas.Should().ContainSingle();
        fichas[0].Documento.Should().Be("111.444.777-35", "vem como GRAVADO — quem normaliza é a importação");
        fichas[0].ChaveImportacao.Should().Be("IMPORT:a:1");
        fichas[0].DataNascimento.Should().Be(new DateOnly(2000, 1, 1));
    }

    [Fact]
    public void Chave_unica_de_importacao_impede_a_segunda_gravacao()
    {
        _db.Pacientes.Add(new Paciente { Nome = "A", Convenio = Convenio.Amil, ChaveImportacao = "IMPORT:s:1" });
        _db.SaveChanges();
        _db.Pacientes.Add(new Paciente { Nome = "B", Convenio = Convenio.Amil, ChaveImportacao = "IMPORT:s:1" });
        var act = () => _db.SaveChanges();
        act.Should().Throw<DbUpdateException>("o índice é único — dois cliques concorrentes não gravam a mesma ficha");
    }
}
