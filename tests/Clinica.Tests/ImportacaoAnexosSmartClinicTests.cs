using System.IO.Compression;
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
/// A importação do ZIP DE ARQUIVOS do Smart Clinic (set/2026): a pasta com os PDFs
/// nomeados pelo id e o índice <c>relacao_arquivos.csv</c> que diz de quem é cada um. O
/// ZIP sintético abaixo tem a FORMA medida no real (pasta numerada, índice com
/// <c>url;id_arquivo;nome_paciente;id_paciente;titulo;data</c>, datas "aaaa-mm-dd
/// hh:mm:ss", títulos "Receita #número"); nenhum dado real.
/// </summary>
public class ImportacaoAnexosSmartClinicTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ImportacaoAnexosSmartClinicService _servico;

    private static readonly byte[] Pdf1 = Encoding.ASCII.GetBytes("%PDF-1.4 um");
    private static readonly byte[] Pdf2 = Encoding.ASCII.GetBytes("%PDF-1.4 dois");

    public ImportacaoAnexosSmartClinicTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _servico = new ImportacaoAnexosSmartClinicService(_repo);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    private async Task<int> PacienteAsync(string nome, string? idAntigo)
    {
        var p = new Paciente
        {
            Nome = nome, Convenio = Convenio.Personalizado, Sexo = Sexo.Feminino,
            ChaveImportacao = idAntigo is null ? null : $"IMPORT:smartclinic:{idAntigo}"
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private static byte[] Zip(params (string Nome, byte[] Bytes)[] arquivos)
    {
        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (nome, bytes) in arquivos)
            {
                var entrada = zip.CreateEntry(nome);
                using var w = entrada.Open();
                w.Write(bytes, 0, bytes.Length);
            }
        return memoria.ToArray();
    }

    private const string Cabecalho = "url;id_arquivo;nome_paciente;id_paciente;titulo;data\n";

    private static byte[] Indice(string linhas) => Encoding.UTF8.GetBytes(Cabecalho + linhas);

    /// <summary>O ZIP com todos os destinos possíveis — o que a prévia precisa DIZER.</summary>
    private static byte[] ZipCompleto() => Zip(
        ("705/relacao_arquivos.csv", Indice(
            "https://x/a1.pdf;a1;Paciente Um;p1;Receita #1001;2024-10-26 10:53:19\n"
            + "https://x/a2.pdf;a2;Paciente Um;p1;Receita #1002;2099-01-01 00:00:00\n"   // data futura
            + "https://x/a3.pdf;a3;Paciente Dois;p2x;Laudo RM;2025-02-01 08:00:00\n"      // id desconhecido, nome ÚNICO
            + "https://x/a4.pdf;a4;Paciente Tres;p3x;Receita #1004;2025-02-01 08:00:00\n" // homônima
            + "https://x/a5.pdf;a5;Ninguem Cadastrado;p9;Receita #1005;2025-02-01 08:00:00\n"
            + "https://x/a6.pdf;a6;Paciente Um;p1;Receita #1006;2025-02-01 08:00:00\n"    // não está no ZIP
            + "https://x/a7.pdf;;Paciente Um;p1;Receita #1007;2025-02-01 08:00:00\n"      // sem id_arquivo
            + "https://x/a8.pdf;a8;Paciente Um;p1;Receita #1008;2025-02-01 08:00:00\n")), // vazio no ZIP
        ("705/a1.pdf", Pdf1),
        ("705/a2.pdf", Pdf2),
        ("705/a3.pdf", Pdf1),
        ("705/a4.pdf", Pdf1),
        ("705/a5.pdf", Pdf1),
        ("705/a8.pdf", []),
        ("705/999.pdf", Pdf1));                                                        // sem linha no índice

    private async Task CenarioAsync()
    {
        await PacienteAsync("Paciente Um", "p1");
        await PacienteAsync("Paciente Dois", null);
        await PacienteAsync("Paciente Tres", null);
        await PacienteAsync("PACIENTE   TRES", null); // a mesma pessoa digitada por outra recepcionista
    }

    // ================================================================== o pacote

    [Fact]
    public void O_pacote_le_o_indice_em_qualquer_pasta_e_diz_os_arquivos_sem_linha()
    {
        var pacote = PacoteAnexosSmartClinic.Abrir(ZipCompleto());

        pacote.Relacao.Linhas.Should().HaveCount(8);
        pacote.Arquivos.Should().Be(7, "sete arquivos fora o índice");
        pacote.Arquivo("a1")!.Value.Bytes.Should().Equal(Pdf1);
        pacote.Arquivo(" a1 ")!.Value.Nome.Should().Be("a1.pdf", "o id é o nome sem extensão, sem a pasta");
        pacote.Arquivo("a6").Should().BeNull();
        pacote.SemLinhaNoIndice.Should().Equal("999.pdf");
    }

    [Fact]
    public void ZIP_sem_indice_ou_sem_a_coluna_do_paciente_e_RECUSADO()
    {
        var semIndice = Assert.Throws<ArgumentException>(() => PacoteAnexosSmartClinic.Abrir(Zip(("705/a1.pdf", Pdf1))));
        semIndice.Message.Should().Contain("relacao_arquivos.csv");

        var semColuna = Assert.Throws<ArgumentException>(() => PacoteAnexosSmartClinic.Abrir(Zip(
            ("relacao_arquivos.csv", Encoding.UTF8.GetBytes("id_arquivo;titulo\na1;Receita\n")),
            ("a1.pdf", Pdf1))));
        semColuna.Message.Should().Contain("id_paciente");
    }

    // ================================================================== prévia

    [Fact]
    public async Task A_previa_diz_o_destino_de_cada_linha_e_nao_grava_nada()
    {
        await CenarioAsync();
        var pacote = PacoteAnexosSmartClinic.Abrir(ZipCompleto());

        var previa = await _servico.PreverAsync(pacote);

        previa.Novos.Should().Be(3, "a1, a2 (data futura entra com hoje) e a3 (pelo nome único)");
        previa.SemPaciente.Should().Be(2, "a homônima e a desconhecida");
        previa.SemArquivo.Should().Be(1);
        previa.Invalidos.Should().Be(2, "sem id_arquivo e vazio no ZIP");
        previa.JaImportados.Should().Be(0);
        previa.PacientesQueRecebem.Should().Be(2);
        previa.BytesNovos.Should().Be(Pdf1.Length * 2 + Pdf2.Length);
        previa.TemTrabalho.Should().BeTrue();
        previa.Problemas.Should().Be(5);

        var porId = previa.Linhas.ToDictionary(l => l.Numero);
        porId[2].Destino.Should().Be(DestinoAnexo.Novo);
        porId[2].Data.Should().Be(DateOnly.FromDateTime(DateTime.Today));
        porId[2].Detalhe.Should().Contain("data futura", "a data não pode ser o que impede o arquivo de entrar — mas a prévia DIZ");
        porId[3].Destino.Should().Be(DestinoAnexo.Novo);
        porId[4].Destino.Should().Be(DestinoAnexo.SemPaciente);
        porId[4].Detalhe.Should().Contain("2 fichas", "casar com uma de duas seria pôr a receita na ficha errada, calado");
        porId[5].Detalhe.Should().Contain("não encontrada");
        porId[6].Destino.Should().Be(DestinoAnexo.SemArquivo);
        porId[7].Destino.Should().Be(DestinoAnexo.Invalido);
        porId[8].Destino.Should().Be(DestinoAnexo.Invalido);
        porId[8].Detalhe.Should().Contain("vazio");

        previa.Avisos.Should().Contain(a => a.Contains("sem ficha") && a.Contains("pacote de pacientes"));
        previa.Avisos.Should().Contain(a => a.Contains("sem linha no índice") && a.Contains("999.pdf"));
        previa.Avisos.Should().Contain(a => a.Contains("data"));

        (await _db.AnexosPaciente.CountAsync()).Should().Be(0, "prévia não grava");
    }

    // ================================================================== execução

    [Fact]
    public async Task Executar_grava_na_ficha_certa_com_bytes_chave_data_e_procedencia()
    {
        await CenarioAsync();
        var pacote = PacoteAnexosSmartClinic.Abrir(ZipCompleto());
        var previa = await _servico.PreverAsync(pacote);

        var resultado = await _servico.ExecutarAsync(previa, pacote, "gerente");

        resultado.Criados.Should().Be(3);
        resultado.TeveErro.Should().BeFalse();

        var um = await _db.Pacientes.SingleAsync(p => p.Nome == "Paciente Um");
        var doUm = await _repo.AnexosDaFichaAsync(um.Id);
        doUm.Should().HaveCount(2);

        var a1 = doUm.Single(a => a.Titulo == "Receita #1001");
        a1.ChaveImportacao.Should().Be("IMPORT:smartclinic:arquivo:a1");
        a1.Importado.Should().BeTrue();
        a1.Data.Should().Be(new DateOnly(2024, 10, 26), "a data é a do DOCUMENTO, como o sistema anterior a gravou");
        a1.NomeArquivo.Should().Be("receita-1001.pdf", "o nome que a pessoa reconhece, com a extensão de verdade");
        a1.TipoConteudo.Should().Be("application/pdf");
        a1.Tamanho.Should().Be(Pdf1.Length);
        a1.CriadoPor.Should().Be("gerente");
        a1.Observacoes.Should().Be("Importado do Smart Clinic · registrado lá em 2024-10-26 10:53:19");
        (await _repo.ConteudoDoAnexoPacienteAsync(a1.Id)).Should().Equal(Pdf1);

        var a2 = doUm.Single(a => a.Titulo == "Receita #1002");
        a2.Observacoes.Should().Contain("data futura", "a observação diz por que a data é a de hoje");
        (await _repo.ConteudoDoAnexoPacienteAsync(a2.Id)).Should().Equal(Pdf2);

        var dois = await _db.Pacientes.SingleAsync(p => p.Nome == "Paciente Dois");
        (await _repo.AnexosDaFichaAsync(dois.Id)).Should().ContainSingle(a => a.Titulo == "Laudo RM",
            "sem o id de lá, o nome resolve — quando é ÚNICO");

        var trilha = await _db.Set<EventoAuditoria>().AsNoTracking().ToListAsync();
        trilha.Count(e => e.Acao == "AnexoFichaImportado").Should().Be(1, "uma linha por LOTE, não por arquivo");
        trilha.Should().ContainSingle(e => e.Acao == "ImportacaoAnexosSmartClinic" && e.Detalhe!.Contains("3 importado(s)"));
    }

    [Fact]
    public async Task Importar_o_mesmo_ZIP_duas_vezes_NAO_duplica()
    {
        await CenarioAsync();
        var pacote = PacoteAnexosSmartClinic.Abrir(ZipCompleto());
        await _servico.ExecutarAsync(await _servico.PreverAsync(pacote), pacote, "gerente");
        var antes = await _db.AnexosPaciente.CountAsync();

        var releitura = await _servico.PreverAsync(pacote);
        releitura.JaImportados.Should().Be(3);
        releitura.Novos.Should().Be(0);
        releitura.TemTrabalho.Should().BeFalse();

        var segunda = await _servico.ExecutarAsync(releitura, pacote, "gerente");

        segunda.Criados.Should().Be(0);
        (await _db.AnexosPaciente.CountAsync()).Should().Be(antes);
    }

    [Fact]
    public async Task A_conferencia_fecha_quando_tudo_entrou_e_NAO_fecha_com_linha_de_fora()
    {
        await CenarioAsync();

        var limpo = PacoteAnexosSmartClinic.Abrir(Zip(
            ("705/relacao_arquivos.csv", Indice(
                "https://x/a1.pdf;a1;Paciente Um;p1;Receita #1001;2024-10-26 10:53:19\n"
                + "https://x/a2.pdf;a2;Paciente Um;p1;Receita #1002;2024-11-26 10:53:19\n")),
            ("705/a1.pdf", Pdf1), ("705/a2.pdf", Pdf2)));
        ConferenciaAnexosSmartClinic.Fechou(ConferenciaAnexosSmartClinic.Montar(await _servico.PreverAsync(limpo)))
            .Should().BeFalse("antes de importar, os dois ainda não estão no sistema");

        await _servico.ExecutarAsync(await _servico.PreverAsync(limpo), limpo, "gerente");

        var itens = ConferenciaAnexosSmartClinic.Montar(await _servico.PreverAsync(limpo));
        itens.Should().ContainSingle();
        itens[0].NoArquivo.Should().Be(2);
        itens[0].NoSistema.Should().Be(2);
        ConferenciaAnexosSmartClinic.Fechou(itens).Should().BeTrue();

        // Com linha de fora (paciente sem ficha), a conferência fecha SÓ porque cada uma
        // tem o motivo escrito — a regra do pacote: o que não entrou é listado com a razão,
        // nunca omitido. E o resumo DIZ que há linhas de fora.
        var completo = PacoteAnexosSmartClinic.Abrir(ZipCompleto());
        await _servico.ExecutarAsync(await _servico.PreverAsync(completo), completo, "gerente");
        var comProblemas = ConferenciaAnexosSmartClinic.Montar(await _servico.PreverAsync(completo));
        ConferenciaAnexosSmartClinic.Fechou(comProblemas).Should().BeTrue();
        comProblemas[0].Completo.Should().BeFalse("cinco linhas ficaram de fora, cada uma com o motivo");
        comProblemas[0].ForaComMotivo.Should().Contain(m => m.Contains("Ninguem Cadastrado"));
        comProblemas[0].ForaComMotivo.Should().NotContain(m => m.Contains("ainda não gravado"),
            "o que podia entrar, entrou");
        comProblemas[0].Resumo.Should().Contain("de fora");
    }

    [Fact]
    public async Task Mais_de_um_lote_fecha_com_uma_linha_de_trilha_por_lote()
    {
        await PacienteAsync("Paciente Um", "p1");
        var linhas = new StringBuilder();
        var arquivos = new List<(string, byte[])>();
        for (var i = 1; i <= 41; i++)
        {
            linhas.Append($"https://x/f{i}.pdf;f{i};Paciente Um;p1;Receita #{i};2025-01-{(i % 28) + 1:00} 08:00:00\n");
            arquivos.Add(($"705/f{i}.pdf", Pdf1));
        }
        arquivos.Insert(0, ("705/relacao_arquivos.csv", Indice(linhas.ToString())));
        var pacote = PacoteAnexosSmartClinic.Abrir(Zip(arquivos.ToArray()));
        var progresso = new List<string>();

        var resultado = await _servico.ExecutarAsync(
            await _servico.PreverAsync(pacote), pacote, "gerente", new Progress<string>(progresso.Add));

        resultado.Criados.Should().Be(41);
        (await _db.AnexosPaciente.CountAsync()).Should().Be(41);
        (await _db.Set<EventoAuditoria>().CountAsync(e => e.Acao == "AnexoFichaImportado")).Should().Be(2,
            "40 no primeiro lote e 1 no segundo");
    }

    // ================================================================== ajudantes

    [Fact]
    public void A_data_do_indice_e_lida_e_a_ilegivel_ou_futura_vira_hoje_com_o_motivo()
    {
        var hoje = new DateOnly(2026, 9, 2);

        ImportacaoAnexosSmartClinicService.LerData("2024-10-26 10:53:19", hoje).Should().Be((new DateOnly(2024, 10, 26), (string?)null));
        ImportacaoAnexosSmartClinicService.LerData("26/10/2024", hoje).Should().Be((new DateOnly(2024, 10, 26), (string?)null));

        var (data, detalhe) = ImportacaoAnexosSmartClinicService.LerData("ontem", hoje);
        data.Should().Be(hoje);
        detalhe.Should().Contain("ilegível").And.Contain("ontem");

        ImportacaoAnexosSmartClinicService.LerData("2030-01-01", hoje).Detalhe.Should().Contain("futura");
        ImportacaoAnexosSmartClinicService.LerData("", hoje).Detalhe.Should().Contain("sem data");
    }

    [Fact]
    public void O_nome_do_arquivo_sai_do_titulo_com_a_extensao_de_verdade()
    {
        ImportacaoAnexosSmartClinicService.NomeDoArquivo("Receita #164001527", "abc.pdf").Should().Be("receita-164001527.pdf");
        ImportacaoAnexosSmartClinicService.NomeDoArquivo("Laudo/RM — joelho", "x.JPG").Should().Be("laudo-rm-joelho.jpg");
        ImportacaoAnexosSmartClinicService.NomeDoArquivo("###", "77.pdf").Should().Be("77.pdf", "título sem letra cai no nome do ZIP");
        ImportacaoAnexosSmartClinicService.NomeDoArquivo("Receita", "semextensao").Should().Be("receita.pdf");
    }

    [Fact]
    public void Nomes_digitados_por_duas_recepcionistas_normalizam_igual()
    {
        ImportacaoAnexosSmartClinicService.NormalizarNome("  maria  da SILVA ")
            .Should().Be(ImportacaoAnexosSmartClinicService.NormalizarNome("Maria da Silva"));
        ImportacaoAnexosSmartClinicService.NormalizarNome("José")
            .Should().Be(ImportacaoAnexosSmartClinicService.NormalizarNome("JOSE"));
    }
}
