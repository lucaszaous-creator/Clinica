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
/// A CONSULTA DE ENFERMAGEM — o Processo de Enfermagem em cinco etapas (parcela 73).
///
/// Por que estes testes existem
/// ----------------------------
/// A cliente olhou a tela e disse que estava CRU, e estava: a "consulta" de enfermagem era
/// UMA caixa de texto com sinais vitais ao lado. Isso não é consulta de enfermagem, é uma
/// anotação — e a COFEN 358/2009 torna o Processo de Enfermagem OBRIGATÓRIO em cinco
/// etapas (histórico, diagnóstico, planejamento, prescrição de enfermagem e avaliação).
///
/// ⚠️ O que estes testes protegem, mais do que os campos, são as DUAS decisões que os
/// tornam usáveis: a anotação curta continua existindo, e a consulta incompleta AVISA sem
/// impedir. Desfazer qualquer uma delas faz a clínica escrever "idem" em cinco campos ou
/// escrever tudo de memória no fim do dia.
/// </summary>
public class ProcessoDeEnfermagemTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly EvolucaoEnfermagemService _servico;

    private static readonly DateOnly Dia = new(2026, 8, 3);
    private static readonly DateTime FimDoDia = Dia.ToDateTime(new TimeOnly(20, 0));

    private static readonly IdentificacaoExecutante Enfermeira =
        new(null, "Ana Enfermeira", "COREN-SP 123456");

    public ProcessoDeEnfermagemTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(
            new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _servico = new EvolucaoEnfermagemService(_repo, () => FimDoDia);
    }

    private async Task<int> PacienteAsync()
    {
        var p = new Paciente { Nome = "Maria", Convenio = Convenio.UnimedIntercambio };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private static ProcessoDeEnfermagem ProcessoCompleto() => new(
        Historico: "Refere dor lombar há duas semanas. Mora sozinha, autocuidado preservado.",
        ExameFisico: "Consciente, corada. Acesso em MSD pérvio, sem sinais flogísticos.",
        Avaliacao: "Dor referida caiu de 7 para 2. Compreendeu as orientações de alta.",
        Diagnosticos:
        [
            new DiagnosticoEnfermagem
            {
                Codigo = "DOR-AGUDA",
                Titulo = "Dor aguda",
                RelacionadoA = "espasmo muscular",
                EvidenciadoPor = "relato de 7/10 e postura antálgica",
                ResultadoEsperado = "Paciente refere dor ≤ 3 ao final do atendimento."
            }
        ],
        Cuidados:
        [
            new CuidadoEnfermagem
            {
                Codigo = "DOR-AVALIAR",
                Descricao = "Avaliar a dor pela escala numérica (0–10)",
                Frequencia = "a cada 2h"
            }
        ]);

    // ---- As cinco etapas ----

    [Fact]
    public async Task A_consulta_grava_as_CINCO_etapas()
    {
        var pacienteId = await PacienteAsync();

        var e = await _servico.RegistrarAsync(
            pacienteId, Dia, new TimeOnly(9, 0), "Consulta de enfermagem.", Enfermeira,
            processo: ProcessoCompleto());

        var relida = await _repo.ObterEvolucaoEnfermagemAsync(e.Id);

        relida!.Historico.Should().NotBeNullOrWhiteSpace();      // 1
        relida.Diagnosticos.Should().HaveCount(1);               // 2
        relida.Diagnosticos[0].ResultadoEsperado.Should().NotBeNullOrWhiteSpace();  // 3
        relida.Cuidados.Should().HaveCount(1);                   // 4
        relida.Avaliacao.Should().NotBeNullOrWhiteSpace();       // 5
        relida.EhConsulta.Should().BeTrue();
    }

    [Fact]
    public async Task A_ANOTACAO_curta_continua_existindo()
    {
        var pacienteId = await PacienteAsync();

        // ⚠️ A técnica que troca um curativo não abre um processo de enfermagem. Obrigá-la
        // faria a clínica escrever "idem" em cinco campos — pior do que o campo vazio,
        // porque PARECE registro.
        var e = await _servico.RegistrarAsync(
            pacienteId, Dia, new TimeOnly(9, 0), "Curativo trocado, ferida limpa.", Enfermeira);

        e.EhConsulta.Should().BeFalse();
        e.EtapasEmFalta.Should().BeEmpty(
            "a anotação não é uma consulta incompleta — ela não deve nada");
    }

    [Fact]
    public async Task A_consulta_INCOMPLETA_grava_e_avisa()
    {
        var pacienteId = await PacienteAsync();

        // A enfermeira colhe o histórico e o diagnóstico agora, e fecha a avaliação depois
        // da infusão. É o processo CERTO — recusar aqui a faria escrever de memória no fim
        // do dia, que é o que o módulo do Consultório existe para combater.
        var e = await _servico.RegistrarAsync(
            pacienteId, Dia, new TimeOnly(9, 0), "Admissão.", Enfermeira,
            processo: new ProcessoDeEnfermagem(
                Historico: "Refere dor lombar há duas semanas.",
                Diagnosticos:
                [
                    new DiagnosticoEnfermagem { Titulo = "Dor aguda" }
                ]));

        e.EhConsulta.Should().BeTrue();
        e.EtapasEmFalta.Should().Contain(x => x.Contains("avaliação"));
        e.EtapasEmFalta.Should().Contain(x => x.Contains("prescrição"));
    }

    // ---- A redação em três partes ----

    [Fact]
    public void O_diagnostico_se_le_nas_TRES_partes()
    {
        var d = new DiagnosticoEnfermagem
        {
            Titulo = "Risco de infecção",
            RelacionadoA = "acesso venoso periférico",
            EvidenciadoPor = "presença de dispositivo intravenoso"
        };

        // É a terceira parte que permite avaliar depois se ele foi resolvido — sem ela, a
        // etapa 5 vira opinião.
        d.Redacao.Should().Be(
            "Risco de infecção, relacionado a acesso venoso periférico, "
            + "evidenciado por presença de dispositivo intravenoso");
    }

    [Fact]
    public void O_diagnostico_sem_as_partes_opcionais_ainda_se_le()
    {
        new DiagnosticoEnfermagem { Titulo = "Ansiedade" }.Redacao.Should().Be("Ansiedade");
    }

    // ---- O catálogo ----

    [Fact]
    public void O_catalogo_e_ATALHO_e_nao_lista_fechada()
    {
        // Diagnóstico escrito à mão grava com código NULO — e é legítimo. Recusar o que
        // está fora da lista seria a regra apertada demais que o projeto já rejeitou no
        // formato do número da guia e no CID-10.
        var escrito = new DiagnosticoEnfermagem { Titulo = "Algo que a clínica viu e o catálogo não tem" };

        escrito.Codigo.Should().BeNull();
        escrito.Redacao.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Todo_diagnostico_do_catalogo_tem_cuidados_que_EXISTEM()
    {
        // ⚠️ Um código de cuidado com erro de digitação some em silêncio: o diagnóstico
        // entraria e os cuidados sugeridos viriam vazios, sem nada avisar. É a família do
        // "dado gravado sem leitor", dentro do catálogo.
        foreach (var d in CatalogoEnfermagem.Diagnosticos)
        {
            d.Cuidados.Should().NotBeEmpty($"{d.Codigo} precisa sugerir algum cuidado");

            CatalogoEnfermagem.CuidadosDe(d.Codigo).Should().HaveCount(
                d.Cuidados.Count,
                $"algum código de cuidado de {d.Codigo} não existe no catálogo");
        }
    }

    [Fact]
    public void O_catalogo_nao_tem_codigo_repetido()
    {
        CatalogoEnfermagem.Diagnosticos.Select(d => d.Codigo).Should().OnlyHaveUniqueItems();
        CatalogoEnfermagem.Cuidados.Select(c => c.Codigo).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Todo_diagnostico_do_catalogo_traz_o_resultado_esperado()
    {
        // A etapa 3 sem sugestão faria a enfermeira pular o planejamento — e é contra ele
        // que a etapa 5 avalia.
        foreach (var d in CatalogoEnfermagem.Diagnosticos)
            d.ResultadoEsperado.Should().NotBeNullOrWhiteSpace(d.Codigo);
    }

    [Fact]
    public void A_busca_ignora_acento_e_caixa()
    {
        CatalogoEnfermagem.BuscarDiagnosticos("INFECCAO").Should()
            .Contain(d => d.Codigo == "INFECCAO-RISCO");
        CatalogoEnfermagem.BuscarDiagnosticos("ansiedade").Should()
            .Contain(d => d.Codigo == "ANSIEDADE");
    }

    // ---- A cópia ----

    [Fact]
    public async Task O_que_a_enfermeira_ajusta_fica_COPIADO_no_registro()
    {
        var pacienteId = await PacienteAsync();

        // Ela pegou o diagnóstico do catálogo e reescreveu a causa para ESTE paciente.
        var e = await _servico.RegistrarAsync(
            pacienteId, Dia, new TimeOnly(9, 0), "Consulta.", Enfermeira,
            processo: new ProcessoDeEnfermagem(
                Diagnosticos:
                [
                    new DiagnosticoEnfermagem
                    {
                        Codigo = "DOR-AGUDA",
                        Titulo = "Dor aguda",
                        RelacionadoA = "punção difícil, três tentativas"
                    }
                ]));

        var relida = await _repo.ObterEvolucaoEnfermagemAsync(e.Id);

        // ⚠️ COPIADO, nunca apontado: corrigir a redação do catálogo hoje não pode
        // reescrever o que ela registrou. Aqui isso não é desenho, é a Lei 13.787/2018.
        relida!.Diagnosticos[0].RelacionadoA.Should().Be("punção difícil, três tentativas");
        relida.Diagnosticos[0].Codigo.Should().Be("DOR-AGUDA",
            "o código fica como PROCEDÊNCIA — de onde a redação veio");
    }

    [Fact]
    public async Task A_ORDEM_da_tela_e_a_ordem_da_folha()
    {
        var pacienteId = await PacienteAsync();

        var e = await _servico.RegistrarAsync(
            pacienteId, Dia, new TimeOnly(9, 0), "Consulta.", Enfermeira,
            processo: new ProcessoDeEnfermagem(
                Diagnosticos:
                [
                    new DiagnosticoEnfermagem { Titulo = "Segundo na cabeça dela" },
                    new DiagnosticoEnfermagem { Titulo = "Primeiro na cabeça dela" }
                ]));

        var relida = await _repo.ObterEvolucaoEnfermagemAsync(e.Id);

        // A sequência é a em que ela pensou o caso; reordenar por id daria uma lista que
        // não é a dela.
        relida!.Diagnosticos.OrderBy(d => d.Ordem).First().Titulo
            .Should().Be("Segundo na cabeça dela");
    }

    [Fact]
    public async Task Linha_em_branco_nao_vira_diagnostico()
    {
        var pacienteId = await PacienteAsync();

        var e = await _servico.RegistrarAsync(
            pacienteId, Dia, new TimeOnly(9, 0), "Consulta.", Enfermeira,
            processo: new ProcessoDeEnfermagem(
                Diagnosticos:
                [
                    new DiagnosticoEnfermagem { Titulo = "  " },
                    new DiagnosticoEnfermagem { Titulo = "Dor aguda" }
                ],
                Cuidados:
                [
                    new CuidadoEnfermagem { Descricao = string.Empty }
                ]));

        // A tela deixa a pessoa acrescentar a linha e desistir dela; linha vazia gravada
        // ocuparia uma posição na folha impressa sem dizer nada.
        e.Diagnosticos.Should().HaveCount(1);
        e.Cuidados.Should().BeEmpty();
    }

    // ---- O circuito ----

    [Fact]
    public async Task A_consulta_APARECE_na_linha_do_tempo_do_medico()
    {
        var pacienteId = await PacienteAsync();

        await _servico.RegistrarAsync(
            pacienteId, Dia, new TimeOnly(9, 0), "Consulta de enfermagem.", Enfermeira,
            processo: ProcessoCompleto());

        var enfermagem = await _repo.EvolucoesEnfermagemDoPacienteAsync(pacienteId, int.MaxValue);

        var linhas = Clinica.Application.Modelos.LinhaDoTempoClinica.Montar(
            Permissao.VerProntuario, enfermagem: enfermagem);

        var linha = linhas[NaturezaRegistroClinico.EvolucaoEnfermagem].Single();

        // ⚠️ O diagnóstico de enfermagem é o que o médico NÃO tem noutro lugar. Uma linha
        // que só dissesse "consulta" o obrigaria a abrir uma por uma para achar a que
        // interessa — o defeito recorrente do projeto vestido de leitura.
        linha.Detalhe.Should().Contain("CONSULTA DE ENFERMAGEM");
        linha.Detalhe.Should().Contain("Dor aguda");
        linha.Detalhe.Should().Contain("cuidado(s) prescrito(s)");
    }

    [Fact]
    public async Task A_consulta_entra_na_EXPORTACAO_e_no_direito_do_titular()
    {
        var pacienteId = await PacienteAsync();

        await _servico.RegistrarAsync(
            pacienteId, Dia, new TimeOnly(9, 0), "Consulta de enfermagem.", Enfermeira,
            processo: ProcessoCompleto());

        // Regra 8 do compromisso: entidade clínica nova entra na exportação e na guarda.
        var arquivos = await new ExportacaoProntuarioService(_repo).ExportarAsync(pacienteId);

        arquivos.Single(a => a.Nome == "prontuario-enfermagem-diagnosticos.csv")
            .Conteudo.Should().Contain("Dor aguda");
        arquivos.Single(a => a.Nome == "prontuario-enfermagem-cuidados.csv")
            .Conteudo.Should().Contain("Avaliar a dor");

        var titular = await new TitularDadosService(
            _repo,
            new ConsentimentoService(_repo),
            new ProntuarioService(_repo),
            new DocumentoClinicoService(
                _repo, new ProntuarioService(_repo), new ConsentimentoService(_repo)))
            .ExportarAsync(pacienteId, "gerente");

        titular.Should().Contain("diagnóstico de enfermagem: Dor aguda");
        titular.Should().Contain("cuidado prescrito:");
        titular.Should().Contain("avaliação de enfermagem");
    }

    // ===== A CORREÇÃO (parcela 74, 2ª rodada) =====

    /// <summary>
    /// ⚠️ Retificar PRESERVA a consulta. Antes desta correção, <c>RetificarAsync</c> não
    /// tinha sequer o parâmetro do processo: corrigir uma vírgula do texto descartava as
    /// cinco etapas que a COFEN 358/2009 torna obrigatórias, e a tela dizia "Registrado".
    ///
    /// A lição: <b>quem RETIFICA precisa receber tudo o que quem REGISTRA recebe</b> — senão
    /// a correção vira uma reescrita mutilada.
    /// </summary>
    [Fact]
    public async Task Retificar_PRESERVA_o_processo_de_enfermagem()
    {
        var pacienteId = await PacienteAsync();
        var original = await _servico.RegistrarAsync(
            pacienteId, Dia, new TimeOnly(14, 20),
            "Pacinte estável", Enfermeira, processo: ProcessoCompleto());

        var corrigida = await _servico.RetificarAsync(
            original.Id, Dia, new TimeOnly(14, 20),
            "Paciente estável", Enfermeira, "erro de digitação",
            processo: ProcessoCompleto());

        corrigida.Historico.Should().StartWith("Refere dor lombar");
        corrigida.Diagnosticos.Should().HaveCount(1);
        corrigida.Cuidados.Should().HaveCount(1);
        corrigida.EhConsulta.Should().BeTrue();
        corrigida.RetificaEvolucaoId.Should().Be(original.Id);
    }

    /// <summary>
    /// A correção mantém a DATA DO FATO. A técnica que corrige na segunda um registro
    /// observado no sábado não pode mover o fato de dia: a folha do sábado perderia a linha,
    /// e a do dia da correção ganharia uma que não aconteceu nele.
    /// </summary>
    [Fact]
    public async Task Retificar_mantem_a_DATA_do_fato()
    {
        var pacienteId = await PacienteAsync();
        var sabado = Dia;
        var original = await _servico.RegistrarAsync(
            pacienteId, sabado, new TimeOnly(14, 20), "observação", Enfermeira);

        var corrigida = await _servico.RetificarAsync(
            original.Id, sabado, new TimeOnly(14, 20), "observação corrigida",
            Enfermeira, "erro de digitação");

        corrigida.Data.Should().Be(sabado);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
