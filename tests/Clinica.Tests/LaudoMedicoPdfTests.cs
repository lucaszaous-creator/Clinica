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
/// O LAUDO NO PAPEL (out/2026 — "a cliente quer os laudos imprimíveis e PROFISSIONAIS").
///
/// Três coisas se fixam aqui, e nenhuma delas quebra build quando se perde:
/// a folha do laudo sai (com o texto formatado e o CID só quando autorizado), a 2ª via
/// sai do MESMO conteúdo gravado, e a PRÉVIA não gasta número nem deixa rastro — prévia
/// que numerasse faria a sequência do ano pular a cada conferência, e a direção leria o
/// buraco como documento sumido.
///
/// Os PDFs vão para a pasta de <c>CLINICA_DUMP_PDF</c> quando ela existe: renderizar é de
/// graça, e é o único jeito de ver o que só a folha montada mostra (parcela 68).
/// </summary>
public class LaudoMedicoPdfTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly DocumentoClinicoService _documentos;
    private readonly DocumentosClinicosPdfService _pdfs;

    private static readonly DateOnly Dia = new(2026, 10, 15);

    private static readonly DadosPrestador Prestador = new()
    {
        NomeFantasia = "Clínica SemDor",
        RazaoSocial = "SemDor Serviços Médicos Ltda.",
        Cnpj = "00.000.000/0001-00",
        Cnes = "1234567",
        Endereco = "Rua das Acácias, 120 — Centro — Macaé/RJ",
        Telefone = "(22) 2765-0000",
        Email = "contato@semdor.com.br"
    };

    // O laudo de verdade é texto longo, em parágrafos, com negrito nos rótulos — é assim
    // que a médica escreve, e é isso que a folha precisa aguentar sem virar um bloco cinza.
    private const string Corpo =
        "Declaro, para os devidos fins, que a paciente acima identificada está em acompanhamento "
        + "nesta clínica desde março de 2026, com sessões semanais de acupuntura e "
        + "acompanhamento médico, por dor lombar crônica.\n"
        + "Quadro clínico: dor lombar baixa de caráter mecânico, com irradiação ocasional para "
        + "a região glútea esquerda, piora aos esforços, ao carregar peso e ao permanecer "
        + "sentada por longos períodos. Escala visual analógica de dor (EVA) 7/10 na admissão "
        + "e 4/10 na última avaliação.\n"
        + "Achados: dor à palpação da musculatura paravertebral lombar bilateral; teste de "
        + "Lasègue negativo; força muscular e reflexos preservados. Ressonância magnética de "
        + "02/2026 com sinais de discopatia degenerativa em L4-L5, sem compressão radicular.\n"
        + "Conclusão: a paciente encontra-se em tratamento, com melhora parcial, e apresenta "
        + "limitação temporária para atividades que exijam carga ou permanência prolongada na "
        + "mesma posição. Recomenda-se reavaliação em 60 dias.";

    public LaudoMedicoPdfTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        var prontuario = new ProntuarioService(_repo);
        _documentos = new DocumentoClinicoService(_repo, prontuario, new ConsentimentoService(_repo));
        _pdfs = new DocumentosClinicosPdfService(_repo);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    private async Task<(int paciente, int profissional)> SemearAsync()
    {
        var paciente = new Paciente
        {
            Nome = "Maria Aparecida dos Santos",
            Documento = "12345678909",
            Convenio = Convenio.UnimedIntercambio,
            Sexo = Sexo.Feminino,
            DataNascimento = new DateOnly(1980, 5, 20)
        };
        var profissional = new Profissional { Nome = "Dra. Ana Beatriz Lima", RegistroConselho = "CRM-RJ 123456" };
        _db.Pacientes.Add(paciente);
        _db.Profissionais.Add(profissional);
        await _db.SaveChangesAsync();
        return (paciente.Id, profissional.Id);
    }

    private static DocumentoClinico Laudo(int pacienteId, int profissionalId, bool cidAutorizado = true)
    {
        var trechos = new List<TrechoTexto>();
        foreach (var (linha, indice) in Corpo.Split('\n').Select((l, i) => (l, i)))
        {
            if (indice > 0) trechos.Add(new("\n"));
            var doisPontos = linha.IndexOf(':');
            if (doisPontos > 0 && doisPontos < 20)
            {
                trechos.Add(new(linha[..(doisPontos + 1)], Negrito: true));
                trechos.Add(new(linha[(doisPontos + 1)..]));
            }
            else trechos.Add(new(linha));
        }
        // Um trecho em itálico, para a folha provar que o itálico também atravessa.
        trechos.Add(new("\n"));
        trechos.Add(new("Laudo emitido a pedido da paciente, para apresentação à perícia do INSS.", Italico: true));

        return new DocumentoClinico
        {
            Tipo = TipoDocumentoClinico.LaudoMedico,
            PacienteId = pacienteId,
            ProfissionalId = profissionalId,
            Data = Dia,
            Corpo = string.Concat(trechos.Select(t => t.Texto)),
            CorpoFormatado = TextoFormatado.Guardar(trechos),
            Cid = "M54.5",
            CidAutorizado = cidAutorizado
        };
    }

    /// <summary>Ambiente sem as bibliotecas nativas de fonte: o desenho é conferido no CI.</summary>
    private static bool SemPilhaGraficaNativa(Exception ex)
    {
        for (var atual = ex; atual is not null; atual = atual.InnerException)
            if (atual is DllNotFoundException or TypeInitializationException
                or EntryPointNotFoundException or BadImageFormatException)
                return true;
        return false;
    }

    private static void Guardar(string nome, byte[] pdf)
    {
        if (Environment.GetEnvironmentVariable("CLINICA_DUMP_PDF") is { Length: > 0 } pasta
            && Directory.Exists(pasta))
            File.WriteAllBytes(Path.Combine(pasta, nome), pdf);
    }

    [Fact]
    public async Task O_laudo_emitido_gera_a_folha_e_a_segunda_via_sai_do_mesmo_conteudo()
    {
        var (pacienteId, profissionalId) = await SemearAsync();
        var emitido = await _documentos.EmitirAsync(Laudo(pacienteId, profissionalId), "dra.ana");

        byte[] primeira, segunda;
        try
        {
            primeira = await _pdfs.GerarAsync(emitido.Id, Prestador);
            segunda = await _pdfs.GerarAsync(emitido.Id, Prestador);
        }
        catch (Exception ex) when (SemPilhaGraficaNativa(ex))
        {
            return;
        }

        System.Text.Encoding.ASCII.GetString(primeira, 0, 4).Should().Be("%PDF");
        Guardar("laudo-emitido.pdf", primeira);

        // A 2ª via é montada do que foi GRAVADO, e o que foi gravado não mudou: o texto,
        // a formatação e o CID são os da emissão.
        var relido = await _documentos.ObterAsync(emitido.Id);
        relido!.Corpo.Should().Be(emitido.Corpo);
        relido.CorpoFormatado.Should().Be(emitido.CorpoFormatado);
        relido.CidImpresso.Should().Be("M54.5");
        segunda.Length.Should().BeGreaterThan(0);
        TextoFormatado.Ler(relido.Corpo, relido.CorpoFormatado)
            .Should().Contain(t => t.Negrito).And.Contain(t => t.Italico);
    }

    [Fact]
    public async Task O_laudo_sem_autorizacao_nao_imprime_o_CID()
    {
        var (pacienteId, profissionalId) = await SemearAsync();
        var emitido = await _documentos.EmitirAsync(
            Laudo(pacienteId, profissionalId, cidAutorizado: false), "dra.ana");

        emitido.CidImpresso.Should().BeNull();
        try
        {
            Guardar("laudo-sem-cid.pdf", await _pdfs.GerarAsync(emitido.Id, Prestador));
        }
        catch (Exception ex) when (SemPilhaGraficaNativa(ex))
        {
        }
    }

    [Fact]
    public async Task A_previa_gera_a_folha_sem_numerar_sem_gravar_e_sem_trilha()
    {
        var (pacienteId, profissionalId) = await SemearAsync();

        var previa = await _documentos.PreverAsync(Laudo(pacienteId, profissionalId));

        // Nada do que faz do papel um FATO: sem número, sem código, fora do banco e da trilha.
        previa.Numero.Should().BeEmpty();
        previa.CodigoVerificacao.Should().BeEmpty();
        previa.Paciente!.Nome.Should().Be("Maria Aparecida dos Santos");
        previa.Profissional!.RegistroConselho.Should().Be("CRM-RJ 123456");
        previa.CidImpresso.Should().Be("M54.5");
        (await _db.DocumentosClinicos.CountAsync()).Should().Be(0);
        (await _db.Auditoria.CountAsync(a => a.Acao == "DocumentoClinicoEmitido")).Should().Be(0);

        try
        {
            var pdf = _pdfs.Gerar(previa, Prestador, previa: true);
            System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
            Guardar("laudo-previa.pdf", pdf);
        }
        catch (Exception ex) when (SemPilhaGraficaNativa(ex))
        {
        }

        // Conferir não gastou número: a primeira emissão do ano continua sendo a 0001, mesmo
        // com o mesmo contexto gravando depois (a prévia não ficou pendurada nele).
        var emitido = await _documentos.EmitirAsync(Laudo(pacienteId, profissionalId), "dra.ana");
        emitido.Numero.Should().Be("2026/0001");
        (await _db.DocumentosClinicos.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task A_previa_recusa_o_que_a_emissao_recusaria()
    {
        var (pacienteId, profissionalId) = await SemearAsync();
        var semTexto = Laudo(pacienteId, profissionalId);
        semTexto.Corpo = "   ";
        semTexto.CorpoFormatado = null;

        var prever = () => _documentos.PreverAsync(semTexto);

        await prever.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Escreva o laudo*");
    }
}
