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
/// Documentos clínicos (parcela 3): os sete papéis da página 21 da proposta.
///
/// Duas regras mandam aqui: documento emitido é FATO (cancela-se com motivo, não se
/// apaga) e o conteúdo fica GRAVADO na emissão — a segunda via tem de sair igual à que
/// o paciente levou, mesmo que o prontuário tenha andado no meio tempo.
/// </summary>
public class DocumentosClinicosTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ProntuarioService _prontuario;
    private readonly ConsentimentoService _consentimentos;
    private readonly DocumentoClinicoService _documentos;
    private readonly DocumentosClinicosPdfService _pdfs;

    private static readonly DateOnly Dia = new(2026, 8, 12);

    public DocumentosClinicosTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _prontuario = new ProntuarioService(_repo);
        _consentimentos = new ConsentimentoService(_repo);
        _documentos = new DocumentoClinicoService(_repo, _prontuario, _consentimentos);
        _pdfs = new DocumentosClinicosPdfService(_repo);
    }

    private async Task<int> CriarPacienteAsync(string nome = "Maria")
    {
        var p = new Paciente
        {
            Nome = nome,
            Convenio = Convenio.UnimedIntercambio,
            Sexo = Sexo.Feminino,
            DataNascimento = new DateOnly(1980, 5, 20)
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<int> CriarProfissionalAsync()
    {
        var p = new Profissional { Nome = "Dra. Ana", RegistroConselho = "CRM-SP 123456" };
        _db.Profissionais.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private Task<DocumentoClinico> EmitirReceitaAsync(int pacienteId, int profissionalId)
        => _documentos.EmitirAsync(new DocumentoClinico
        {
            Tipo = TipoDocumentoClinico.Receita,
            PacienteId = pacienteId,
            ProfissionalId = profissionalId,
            Data = Dia,
            Itens =
            [
                new ItemDocumento
                {
                    Descricao = "Dipirona 500mg",
                    Quantidade = "1 caixa",
                    Detalhe = "1 comprimido de 8 em 8 horas por 3 dias"
                }
            ]
        }, "secretaria");

    // ==================== Numeração e identidade ====================

    [Fact]
    public async Task Numera_em_sequencia_dentro_do_ano()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();

        var primeiro = await EmitirReceitaAsync(pacienteId, profissionalId);
        var segundo = await EmitirReceitaAsync(pacienteId, profissionalId);

        primeiro.Numero.Should().Be("2026/0001");
        segundo.Numero.Should().Be("2026/0002");
    }

    [Fact]
    public async Task O_codigo_impresso_no_rodape_acha_o_documento()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();
        var emitido = await EmitirReceitaAsync(pacienteId, profissionalId);

        var achado = await _documentos.PorCodigoAsync(emitido.CodigoVerificacao);

        achado.Should().NotBeNull();
        achado!.Numero.Should().Be(emitido.Numero);
        achado.Itens.Should().HaveCount(1);
    }

    // ==================== Validações por tipo ====================

    [Fact]
    public async Task Receita_sem_nenhum_item_e_recusada()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();

        var emitir = () => _documentos.EmitirAsync(new DocumentoClinico
        {
            Tipo = TipoDocumentoClinico.Receita,
            PacienteId = pacienteId,
            ProfissionalId = profissionalId,
            Data = Dia
        });

        await emitir.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*sem nenhum item*");
    }

    [Fact]
    public async Task Receita_sem_quem_assina_e_recusada()
    {
        var pacienteId = await CriarPacienteAsync();

        var emitir = () => _documentos.EmitirAsync(new DocumentoClinico
        {
            Tipo = TipoDocumentoClinico.Receita,
            PacienteId = pacienteId,
            Data = Dia,
            Itens = [new ItemDocumento { Descricao = "Dipirona" }]
        });

        await emitir.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*profissional que assina*");
    }

    [Fact]
    public async Task Atestado_sem_dias_nem_periodo_e_recusado()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();

        var emitir = () => _documentos.EmitirAsync(new DocumentoClinico
        {
            Tipo = TipoDocumentoClinico.Atestado,
            PacienteId = pacienteId,
            ProfissionalId = profissionalId,
            Data = Dia
        });

        await emitir.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*dias de afastamento*");
    }

    [Fact]
    public async Task Declaracao_com_saida_antes_da_chegada_e_recusada()
    {
        var pacienteId = await CriarPacienteAsync();

        var emitir = () => _documentos.EmitirAsync(new DocumentoClinico
        {
            Tipo = TipoDocumentoClinico.Comparecimento,
            PacienteId = pacienteId,
            Data = Dia,
            HoraChegada = new TimeOnly(15, 0),
            HoraSaida = new TimeOnly(14, 0)
        });

        await emitir.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*saída é anterior*");
    }

    [Fact]
    public async Task Cid_sem_autorizacao_do_paciente_nao_sai_impresso()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();

        var atestado = await _documentos.EmitirAsync(new DocumentoClinico
        {
            Tipo = TipoDocumentoClinico.Atestado,
            PacienteId = pacienteId,
            ProfissionalId = profissionalId,
            Data = Dia,
            DiasAfastamento = 2,
            Cid = "M54.5",
            CidAutorizado = false
        }, "secretaria");

        // O CID fica GRAVADO (é dado clínico), mas não vai para o papel.
        atestado.Cid.Should().Be("M54.5");
        atestado.CidImpresso.Should().BeNull();

        atestado.CidAutorizado = true;
        atestado.CidImpresso.Should().Be("M54.5");
    }

    // ==================== Cancelamento ====================

    [Fact]
    public async Task Cancelar_exige_motivo_e_nao_apaga_o_documento()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();
        var emitido = await EmitirReceitaAsync(pacienteId, profissionalId);

        var semMotivo = () => _documentos.CancelarAsync(emitido.Id, "  ", "secretaria");
        await semMotivo.Should().ThrowAsync<InvalidOperationException>();

        await _documentos.CancelarAsync(emitido.Id, "paciente trocou de medicação", "secretaria");

        var depois = await _documentos.ObterAsync(emitido.Id);
        depois.Should().NotBeNull();
        depois!.Cancelado.Should().BeTrue();
        depois.MotivoCancelamento.Should().Be("paciente trocou de medicação");

        // A lista do paciente continua mostrando o documento cancelado.
        (await _documentos.DoPacienteAsync(pacienteId)).Should().HaveCount(1);

        var deNovo = () => _documentos.CancelarAsync(emitido.Id, "de novo", "secretaria");
        await deNovo.Should().ThrowAsync<InvalidOperationException>().WithMessage("*já foi cancelado*");
    }

    // ==================== Documentos montados do prontuário ====================

    [Fact]
    public async Task Relatorio_de_evolucao_lista_as_sessoes_e_so_afirma_a_dor_com_o_par_de_EVA()
    {
        var pacienteId = await CriarPacienteAsync();

        await RegistrarSessaoAsync(pacienteId, Dia.AddDays(-14), 8, 4);
        await RegistrarSessaoAsync(pacienteId, Dia.AddDays(-7), null, null);
        await RegistrarSessaoAsync(pacienteId, Dia, 6, 2);

        var relatorio = await _documentos.EmitirRelatorioEvolucaoAsync(pacienteId, operador: "secretaria");

        relatorio.Tipo.Should().Be(TipoDocumentoClinico.RelatorioEvolucao);
        relatorio.Itens.Should().HaveCount(3);
        relatorio.PeriodoInicio.Should().Be(Dia.AddDays(-14));
        relatorio.PeriodoFim.Should().Be(Dia);

        // A sessão sem o par entra na lista (aconteceu), mas não no que se afirma da dor.
        relatorio.Itens.Should().Contain(i => i.Descricao.Contains("EVA não medida"));
        relatorio.Corpo.Should().Contain("2 sessão(ões) têm a EVA medida");
        relatorio.Corpo.Should().Contain("8/10");
        relatorio.Corpo.Should().Contain("2/10");
    }

    [Fact]
    public async Task Relatorio_sem_nenhuma_sessao_no_periodo_e_recusado()
    {
        var pacienteId = await CriarPacienteAsync();

        var emitir = () => _documentos.EmitirRelatorioEvolucaoAsync(pacienteId);

        await emitir.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Não há sessão registrada*");
    }

    [Fact]
    public async Task Relatorio_reimpresso_nao_muda_quando_o_prontuario_anda()
    {
        var pacienteId = await CriarPacienteAsync();
        await RegistrarSessaoAsync(pacienteId, Dia.AddDays(-7), 8, 4);

        var relatorio = await _documentos.EmitirRelatorioEvolucaoAsync(pacienteId);
        relatorio.Itens.Should().HaveCount(1);

        // Sessão nova DEPOIS da emissão: a via já entregue continua com uma linha só.
        await RegistrarSessaoAsync(pacienteId, Dia, 5, 1);

        var recarregado = await _documentos.ObterAsync(relatorio.Id);
        recarregado!.Itens.Should().HaveCount(1);
    }

    [Fact]
    public async Task Anamnese_sai_com_o_roteiro_inteiro_mesmo_sem_prontuario()
    {
        var pacienteId = await CriarPacienteAsync();

        var anamnese = await _documentos.EmitirAnamneseAsync(pacienteId, operador: "secretaria");

        anamnese.Itens.Should().HaveCount(DocumentoClinicoService.RoteiroAnamnese.Count);
        anamnese.Itens.Select(i => i.Descricao).Should()
            .BeEquivalentTo(DocumentoClinicoService.RoteiroAnamnese);
        // Sem prontuário, todas as seções saem em branco para a entrevista.
        anamnese.Itens.Should().OnlyContain(i => i.Detalhe == null);
    }

    [Fact]
    public async Task Anamnese_traz_preenchido_o_que_a_primeira_sessao_registrou()
    {
        var pacienteId = await CriarPacienteAsync();
        await RegistrarSessaoAsync(pacienteId, Dia.AddDays(-30), 7, 3, "dor no ombro direito");
        await RegistrarSessaoAsync(pacienteId, Dia, 5, 2, "melhor, mas dói ao dormir");

        var anamnese = await _documentos.EmitirAnamneseAsync(pacienteId);

        anamnese.Itens.Single(i => i.Descricao == "Queixa principal").Detalhe
            .Should().Be("dor no ombro direito");
        anamnese.Itens.Single(i => i.Descricao == "Alergias").Detalhe.Should().BeNull();
    }

    [Fact]
    public async Task Termo_de_consentimento_traz_uma_linha_por_finalidade_com_a_situacao_atual()
    {
        var pacienteId = await CriarPacienteAsync();
        await _consentimentos.RegistrarAsync(
            pacienteId, FinalidadeConsentimento.TratamentoDeDados, concedido: true,
            operador: "secretaria");

        var termo = await _documentos.EmitirTermoConsentimentoAsync(pacienteId, operador: "secretaria");

        termo.Itens.Should().HaveCount(ConsentimentoService.Finalidades.Count);
        termo.Corpo.Should().Contain("13.709");

        var tratamento = termo.Itens.Single(
            i => i.Descricao == ConsentimentoService.Rotular(FinalidadeConsentimento.TratamentoDeDados));
        tratamento.Quantidade.Should().Be("Autorizado");

        var imagem = termo.Itens.Single(
            i => i.Descricao == ConsentimentoService.Rotular(FinalidadeConsentimento.UsoDeImagem));
        imagem.Quantidade.Should().Be("Pendente");
        imagem.Detalhe.Should().Be("Nunca perguntado");
    }

    // ==================== Modelos ====================

    [Fact]
    public async Task Modelo_com_nome_repetido_corrige_o_anterior_em_vez_de_duplicar()
    {
        await _documentos.SalvarModeloAsync(new ModeloDocumento
        {
            Tipo = TipoDocumentoClinico.Receita,
            Nome = "Analgesia pós-sessão",
            Corpo = "Primeira versão",
            Itens = [new ItemModelo { Descricao = "Dipirona 500mg" }]
        }, "secretaria");

        await _documentos.SalvarModeloAsync(new ModeloDocumento
        {
            Tipo = TipoDocumentoClinico.Receita,
            Nome = "Analgesia pós-sessão",
            Corpo = "Segunda versão",
            Itens =
            [
                new ItemModelo { Descricao = "Dipirona 500mg" },
                new ItemModelo { Descricao = "Compressa morna" }
            ]
        }, "secretaria");

        var modelos = await _documentos.ModelosAsync(TipoDocumentoClinico.Receita);

        modelos.Should().HaveCount(1);
        modelos.Single().Corpo.Should().Be("Segunda versão");
        modelos.Single().Itens.Should().HaveCount(2);
    }

    [Fact]
    public async Task Modelo_de_um_tipo_nao_aparece_na_lista_de_outro()
    {
        await _documentos.SalvarModeloAsync(new ModeloDocumento
        {
            Tipo = TipoDocumentoClinico.PedidoExame,
            Nome = "Investigação de lombalgia",
            Itens = [new ItemModelo { Descricao = "Raio-X de coluna lombar" }]
        });

        (await _documentos.ModelosAsync(TipoDocumentoClinico.Receita)).Should().BeEmpty();
        (await _documentos.ModelosAsync(TipoDocumentoClinico.PedidoExame)).Should().HaveCount(1);
    }

    [Fact]
    public async Task Modelo_vazio_nao_e_guardado()
    {
        var salvar = () => _documentos.SalvarModeloAsync(new ModeloDocumento
        {
            Tipo = TipoDocumentoClinico.Receita,
            Nome = "Nada aqui"
        });

        await salvar.Should().ThrowAsync<InvalidOperationException>();
    }

    // ==================== PDF ====================

    [Fact]
    public async Task Os_sete_documentos_geram_PDF()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();
        await RegistrarSessaoAsync(pacienteId, Dia, 7, 3);

        var emitidos = new List<DocumentoClinico>
        {
            await EmitirReceitaAsync(pacienteId, profissionalId),
            await _documentos.EmitirAsync(new DocumentoClinico
            {
                Tipo = TipoDocumentoClinico.Atestado,
                PacienteId = pacienteId,
                ProfissionalId = profissionalId,
                Data = Dia,
                DiasAfastamento = 3,
                Cid = "M54.5",
                CidAutorizado = true
            }),
            await _documentos.EmitirAsync(new DocumentoClinico
            {
                Tipo = TipoDocumentoClinico.Comparecimento,
                PacienteId = pacienteId,
                Data = Dia,
                HoraChegada = new TimeOnly(14, 0),
                HoraSaida = new TimeOnly(15, 0)
            }),
            await _documentos.EmitirAsync(new DocumentoClinico
            {
                Tipo = TipoDocumentoClinico.PedidoExame,
                PacienteId = pacienteId,
                ProfissionalId = profissionalId,
                Data = Dia,
                Itens = [new ItemDocumento { Descricao = "Raio-X de coluna lombar" }]
            }),
            await _documentos.EmitirRelatorioEvolucaoAsync(pacienteId),
            await _documentos.EmitirTermoConsentimentoAsync(pacienteId),
            await _documentos.EmitirAnamneseAsync(pacienteId)
        };

        emitidos.Select(d => d.Tipo).Should().BeEquivalentTo(TipoDocumentoInfo.Todos);

        var prestador = new DadosPrestador { NomeFantasia = "Clínica SemDor", Cnpj = "00.000.000/0001-00" };

        foreach (var documento in emitidos)
        {
            byte[] pdf;
            try
            {
                pdf = await _pdfs.GerarAsync(documento.Id, prestador);
            }
            catch (Exception ex) when (SemPilhaGraficaNativa(ex))
            {
                // Ambiente sem as bibliotecas nativas de fonte (Linux sem fontconfig):
                // o CI que valida o desenho é o runner Windows. Erro de LEIAUTE do
                // QuestPDF continua estourando aqui, que é o que este teste guarda.
                return;
            }

            pdf.Should().NotBeEmpty();
            System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
        }
    }

    /// <summary>
    /// A exceção é de ambiente (SkiaSharp/fontconfig ausentes), não de leiaute. Erro de
    /// leiaute do QuestPDF precisa continuar quebrando o teste — é justamente o que ele
    /// existe para pegar, já que o desenho é escrito às cegas fora do Windows.
    /// </summary>
    private static bool SemPilhaGraficaNativa(Exception ex)
    {
        for (var atual = ex; atual is not null; atual = atual.InnerException)
            if (atual is DllNotFoundException or TypeInitializationException
                or EntryPointNotFoundException or BadImageFormatException)
                return true;

        return false;
    }

    private Task<Evolucao> RegistrarSessaoAsync(
        int pacienteId, DateOnly data, int? antes, int? depois, string queixa = "dor lombar")
        => _prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = pacienteId,
            Data = data,
            EvaAntes = antes,
            EvaDepois = depois,
            QueixaPrincipal = queixa,
            Conduta = "acupuntura sistêmica",
            TextoEvolucao = "paciente refere melhora"
        }, "secretaria");

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
