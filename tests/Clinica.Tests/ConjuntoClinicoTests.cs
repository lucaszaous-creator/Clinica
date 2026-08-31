using System.Text;
using Clinica.Application.Assinatura;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Prontuario;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O CONJUNTO CLÍNICO — quatro leitores, UMA lista de naturezas (parcela 72).
///
/// Por que estes testes existem
/// ----------------------------
/// O defeito recorrente do projeto já foi cometido DUAS vezes neste exato assunto, e os
/// comentários confessam: a folha de infusão ficou de fora da primeira versão da GUARDA
/// ("o que contrariava a própria regra 8 do CLAUDE.md") e a lista de problemas — onde
/// moram as ALERGIAS — ficou de fora da primeira versão da EXPORTAÇÃO. Uma terceira
/// versão do mesmo esquecimento estava de pé: <c>SituacaoGuarda</c> contava CINCO
/// naturezas enquanto o prazo de 20 anos era calculado sobre SETE, e o documento do art.
/// 18, II cobria TRÊS de nove.
///
/// ⚠️ O que faz este esquecimento perigoso é o formato do erro: <b>ele aparece como lista
/// LIMPA</b>, indistinguível de "não houve nada". Nada estoura, nada avisa, e quem
/// descobre é o auditor.
///
/// ⚠️ E os testes são COMPORTAMENTAIS, não declarativos. Cada um grava um registro de
/// CADA natureza e exige que ele APAREÇA na saída — uma declaração conferida contra outra
/// declaração ficaria verde com as duas erradas do mesmo jeito.
///
/// <b>Eles falham no commit em que a próxima entidade clínica nascer</b>, que é meses
/// antes de a clínica esbarrar.
/// </summary>
public class ConjuntoClinicoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;

    private static readonly DateOnly Dia = new(2026, 8, 3);
    private static readonly DateTime MeioDia = Dia.ToDateTime(new TimeOnly(12, 0));

    private static readonly IdentificacaoExecutante Tecnica =
        new(null, "Joana Técnica", "COREN-SP 999999");

    public ConjuntoClinicoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(
            new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
    }

    /// <summary>
    /// Um paciente com UM registro de CADA natureza do catálogo. É a fixture que faz o
    /// teste responder por natureza, e não por amostra.
    /// </summary>
    private async Task<int> PacienteCompletoAsync()
    {
        var paciente = new Paciente
        {
            Nome = "Maria Completa", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino
        };
        var medica = new Profissional
        {
            Nome = "Dra. Ana", RegistroConselho = "CRM 1", Cpf = "12345678909"
        };
        _db.Pacientes.Add(paciente);
        _db.Profissionais.Add(medica);
        await _db.SaveChangesAsync();

        // 1. Sessão médica
        var sessao = new Evolucao
        {
            PacienteId = paciente.Id,
            Data = Dia,
            QueixaPrincipal = "dor lombar",
            Conduta = "acupuntura",
            TextoEvolucao = "melhora após a sessão",
            EvaAntes = 8,
            EvaDepois = 3,
            CriadoPor = "dra.ana"
        };
        await _repo.AdicionarEvolucaoAsync(sessao);
        await _repo.SalvarAsync();

        // 2. Anexo (pende da sessão)
        await _repo.AdicionarAnexoAsync(new AnexoProntuario
        {
            EvolucaoId = sessao.Id,
            NomeArquivo = "laudo-ressonancia.pdf",
            Tipo = TipoAnexo.Documento,
            Conteudo = [1, 2, 3],
            Tamanho = 3
        });

        // 3. Mapa corporal (pende da sessão)
        await _repo.AdicionarMapaAsync(new MapaCorporal
        {
            EvolucaoId = sessao.Id,
            Pontos = [new PontoMapa { Face = FaceCorpo.Frente, X = 0.5, Y = 0.5, Nome = "IG4" }]
        });

        // 4. Avaliação clínica
        await _repo.AdicionarAvaliacaoAsync(new AvaliacaoClinica
        {
            PacienteId = paciente.Id,
            Data = Dia,
            InstrumentoCodigo = "PHQ9",
            InstrumentoNome = "PHQ-9",
            Pontuacao = 12,
            PontuacaoMaxima = 27,
            Unidade = "pontos",
            FaixaNome = "sintomas moderados"
        });

        // 5. Medida clínica
        await _repo.AdicionarMedidaAsync(new MedidaClinica
        {
            PacienteId = paciente.Id,
            Data = Dia,
            TipoCodigo = "PESO",
            TipoNome = "Peso",
            Unidade = "kg",
            Valor = 78.5m
        });

        // 6. Problema (a ALERGIA — a que já ficou de fora uma vez)
        await _repo.AdicionarProblemaAsync(new ProblemaPaciente
        {
            PacienteId = paciente.Id,
            Natureza = NaturezaProblema.Alergia,
            Descricao = "Dipirona",
            Situacao = SituacaoProblema.Ativo,
            Inicio = Dia
        });

        // 7. Documento clínico — DOIS, e a diferença entre eles é o assunto do teste de
        // acesso: a receita é dado de saúde (`VerProntuario`), a declaração de
        // comparecimento não é (`VerFichaPaciente`) e sai do balcão o dia inteiro.
        await _repo.AdicionarDocumentoAsync(new DocumentoClinico
        {
            PacienteId = paciente.Id,
            ProfissionalId = medica.Id,
            Tipo = TipoDocumentoClinico.Receita,
            Numero = "2026/0001",
            Data = Dia,
            CodigoVerificacao = "ABC123",
            Corpo = "Dipirona 500mg"
        });
        await _repo.AdicionarDocumentoAsync(new DocumentoClinico
        {
            PacienteId = paciente.Id,
            Tipo = TipoDocumentoClinico.Comparecimento,
            Numero = "2026/0002",
            Data = Dia,
            CodigoVerificacao = "DEF456",
            Corpo = "Esteve na clínica das 9h às 10h."
        });
        await _repo.SalvarAsync();

        // 8. Prescrição de infusão, assinada e com execução
        var conferencia = new PrescricaoService(_repo);
        var prescricoes = new PrescricaoInternaService(_repo, conferencia);
        var folha = await prescricoes.CriarAsync(paciente.Id, medica.Id);
        await prescricoes.SalvarRascunhoAsync(
            folha.Id, "Crise álgica", null,
            [new ItemPrescricaoInterna { Descricao = "Soro fisiológico", Dose = "500 ml" }]);

        var carregada = (await _repo.ObterPrescricaoInternaAsync(folha.Id))!;
        carregada.Data = Dia;
        carregada.Situacao = SituacaoPrescricao.Assinada;
        carregada.Assinaturas.Add(new AssinaturaDocumento
        {
            Papel = PapelAssinatura.Prescritor,
            NomeAssinante = "Dra. Ana",
            CpfAssinante = "12345678909",
            AssinadoEm = MeioDia
        });
        await _repo.SalvarAsync();

        var checagens = new ChecagemPrescricaoService(_repo, () => MeioDia, conferencia);
        await checagens.ChecarAsync(
            carregada.Itens.Single().Id, SituacaoChecagem.Realizado,
            new TimeOnly(9, 30), Tecnica);

        // 9. Evolução de enfermagem
        await new EvolucaoEnfermagemService(_repo, () => MeioDia).RegistrarAsync(
            paciente.Id, Dia, new TimeOnly(9, 45),
            "Paciente tolerou bem a infusão.", Tecnica,
            prescricaoInternaId: folha.Id);

        // A ANAMNESE do paciente (parcela 75) — a natureza que existe UMA vez por pessoa.
        await new AnamneseService(_repo).SalvarAsync(paciente.Id, new AnamnesePaciente
        {
            AntecedentesPessoais = "Apendicectomia em 2015.",
            AntecedentesFamiliares = "Mãe hipertensa.",
            HabitosDeVida = "Nega tabagismo. Sedentária."
        }, "dra.ana");

        // O resultado de exame estruturado (ago/2026) — a décima primeira natureza.
        await new ResultadoExameService(_repo).RegistrarAsync(new ResultadoExame
        {
            PacienteId = paciente.Id,
            Data = Dia,
            Nome = "Hemoglobina glicada",
            Valor = "6,1",
            Unidade = "%",
            Referencia = "4,0 a 5,6",
            Laboratorio = "Lab Vida"
        }, "dra.ana");

        return paciente.Id;
    }

    // ---- Os quatro leitores ----

    [Fact]
    public async Task A_guarda_conta_TODAS_as_naturezas_do_catalogo()
    {
        var pacienteId = await PacienteCompletoAsync();

        var situacao = await new GuardaProntuarioService(_repo).DoPacienteAsync(pacienteId);

        foreach (var info in CatalogoRegistroClinico.Todas)
            situacao.De(info.Natureza).Should().BeGreaterThan(0,
                $"a guarda precisa contar {info.Plural} — o prazo de 20 anos é calculado "
                + "sobre o último registro de QUALQUER natureza, e contagem incompleta "
                + "numa tela de conformidade parece conferida");
    }

    [Fact]
    public async Task A_frase_da_guarda_NOMEIA_todas_as_naturezas_guardadas()
    {
        var pacienteId = await PacienteCompletoAsync();
        var situacao = await new GuardaProntuarioService(_repo).DoPacienteAsync(pacienteId);

        var frase = situacao.DescreverContagens();

        // É a frase que a tela mostra ao auditor. Natureza contada e não escrita é o
        // mesmo buraco, uma camada acima.
        foreach (var info in CatalogoRegistroClinico.Todas)
            frase.Should().Contain(
                CatalogoRegistroClinico.Contar(info.Natureza, situacao.De(info.Natureza)),
                $"a tela de guarda precisa dizer quantos {info.Plural} há");
    }

    [Fact]
    public async Task A_exportacao_cobre_TODAS_as_naturezas_do_catalogo()
    {
        var pacienteId = await PacienteCompletoAsync();

        var arquivos = await new ExportacaoProntuarioService(_repo).ExportarAsync(pacienteId);

        foreach (var info in CatalogoRegistroClinico.Todas)
        {
            var nome = ExportacaoProntuarioService.ArquivoPorNatureza[info.Natureza];
            var arquivo = arquivos.SingleOrDefault(a => a.Nome == nome);

            arquivo.Should().NotBeNull(
                $"a exportação precisa ter o arquivo de {info.Plural}");

            // Cabeçalho + pelo menos uma linha: arquivo só com cabeçalho é a lista limpa
            // que este teste existe para pegar.
            arquivo!.Conteudo.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Should().HaveCountGreaterThan(1,
                    $"a exportação de {info.Plural} saiu só com o cabeçalho");
        }
    }

    [Fact]
    public async Task O_direito_do_titular_cobre_TODAS_as_naturezas_do_catalogo()
    {
        var pacienteId = await PacienteCompletoAsync();

        var texto = await new TitularDadosService(
            _repo,
            new ConsentimentoService(_repo),
            new ProntuarioService(_repo),
            new DocumentoClinicoService(
                _repo, new ProntuarioService(_repo), new ConsentimentoService(_repo)))
            .ExportarAsync(pacienteId, "gerente");

        foreach (var info in CatalogoRegistroClinico.Todas)
        {
            var secao = TitularDadosService.SecaoPorNatureza[info.Natureza];
            texto.Should().Contain(secao,
                $"o documento do art. 18, II precisa ter a seção de {info.Plural}");
        }

        // E a seção não pode estar VAZIA para quem tem o registro: "(nenhuma)" na seção de
        // um paciente que fez PHQ-9 é a mentira mais cara deste documento.
        texto.Should().Contain("PHQ-9");
        texto.Should().Contain("Dipirona");
        texto.Should().Contain("laudo-ressonancia.pdf");
        texto.Should().Contain("mapa corporal");
        texto.Should().Contain("Peso");
        texto.Should().Contain("tolerou bem a infusão");
    }

    [Fact]
    public async Task A_linha_do_tempo_mostra_o_que_o_catalogo_permite_a_quem_le()
    {
        var pacienteId = await PacienteCompletoAsync();

        var sessoes = await _repo.EvolucoesDoPacienteAsync(pacienteId, true);
        var enfermagem = await _repo.EvolucoesEnfermagemDoPacienteAsync(pacienteId, int.MaxValue);
        var infusoes = await _repo.PrescricoesInternasDoPacienteAsync(pacienteId, int.MaxValue);
        var documentos = await _repo.DocumentosDoPacienteAsync(pacienteId);

        var comAcesso = LinhaDoTempoClinica.Montar(
            Permissao.VerProntuario | Permissao.VerFichaPaciente,
            sessoes, null, enfermagem, infusoes, documentos);

        comAcesso[NaturezaRegistroClinico.SessaoMedica].Should().NotBeEmpty();
        comAcesso[NaturezaRegistroClinico.EvolucaoEnfermagem].Should().NotBeEmpty();
        comAcesso[NaturezaRegistroClinico.PrescricaoInterna].Should().NotBeEmpty();
        comAcesso[NaturezaRegistroClinico.DocumentoClinico].Should().NotBeEmpty();

        // ⚠️ Nem ler nem desenhar (art. 5º, II): sem `VerProntuario` nada é MONTADO — não
        // é escondido depois de montado, que deixaria o dado de saúde na memória de quem
        // não pode vê-lo.
        var semAcesso = LinhaDoTempoClinica.Montar(
            Permissao.VerFichaPaciente,
            sessoes, null, enfermagem, infusoes, documentos);

        semAcesso[NaturezaRegistroClinico.SessaoMedica].Should().BeEmpty();
        semAcesso[NaturezaRegistroClinico.EvolucaoEnfermagem].Should().BeEmpty();
        semAcesso[NaturezaRegistroClinico.PrescricaoInterna].Should().BeEmpty();

        // ⚠️ O DOCUMENTO é a exceção, e é por causa dela que o `PermissaoVer` do catálogo é
        // PISO e não teto: a receita some (dado de saúde), e a declaração de comparecimento
        // FICA — ela não carrega dado de saúde e sai do balcão o dia inteiro (parcela 59).
        // Com o teto, o portão de natureza engolia as duas ANTES de o filtro por folha
        // rodar, e quem tem só o cadastro recebia lista vazia.
        var papeis = semAcesso[NaturezaRegistroClinico.DocumentoClinico];
        papeis.Should().HaveCount(1);
        papeis[0].Titulo.Should().Contain("2026/0002");
    }

    [Fact]
    public async Task A_linha_de_enfermagem_42_nao_encosta_na_sessao_42()
    {
        // ⚠️ O bug que os chips de seção existem para impedir. Os ids são POR TABELA: uma
        // lista fundida que carregasse só o id faria o comando de cancelar da sessão
        // apagar a evolução de enfermagem de outra pessoa. Não estoura, não avisa.
        var pacienteId = await PacienteCompletoAsync();

        var sessoes = await _repo.EvolucoesDoPacienteAsync(pacienteId, true);
        var enfermagem = await _repo.EvolucoesEnfermagemDoPacienteAsync(pacienteId, int.MaxValue);

        var linhas = LinhaDoTempoClinica.Montar(
            Permissao.VerProntuario, sessoes, null, enfermagem);

        var daSessao = linhas[NaturezaRegistroClinico.SessaoMedica].Single();
        var daEnfermagem = linhas[NaturezaRegistroClinico.EvolucaoEnfermagem].Single();

        // Os dois têm id 1 na base de teste — e é exatamente esse o caso perigoso.
        daSessao.Id.Should().Be(daEnfermagem.Id);
        daSessao.Natureza.Should().NotBe(daEnfermagem.Natureza);

        // O par (Natureza, Id) é o que distingue. Sem ele, "42" é ambíguo.
        (daSessao.Natureza, daSessao.Id).Should().NotBe((daEnfermagem.Natureza, daEnfermagem.Id));
    }

    [Fact]
    public void Toda_natureza_do_enum_esta_no_catalogo()
    {
        // A lista mora em UM lugar, e o enum é a fonte. Natureza declarada sem entrada no
        // catálogo estouraria no primeiro `Obter` — em produção, não aqui.
        foreach (var natureza in Enum.GetValues<NaturezaRegistroClinico>())
        {
            var info = CatalogoRegistroClinico.Obter(natureza);
            info.Singular.Should().NotBeNullOrWhiteSpace();
            info.Plural.Should().NotBeNullOrWhiteSpace();
            info.PermissaoVer.Should().NotBe(Permissao.Nenhuma,
                "permissão Nenhuma faz `Pode` LIBERAR — a natureza nasceria aberta a todos");
        }

        CatalogoRegistroClinico.Todas.Should()
            .HaveCount(Enum.GetValues<NaturezaRegistroClinico>().Length);
    }

    [Fact]
    public void As_declaracoes_dos_leitores_cobrem_o_catalogo_inteiro()
    {
        // A metade declarativa, ao lado das comportamentais: leitor novo que esqueça uma
        // natureza não compila a declaração sem alguém reparar.
        foreach (var info in CatalogoRegistroClinico.Todas)
        {
            ExportacaoProntuarioService.ArquivoPorNatureza.Should().ContainKey(info.Natureza);
            TitularDadosService.SecaoPorNatureza.Should().ContainKey(info.Natureza);
        }
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
