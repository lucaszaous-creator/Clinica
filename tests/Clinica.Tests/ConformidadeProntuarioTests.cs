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
/// A CONFORMIDADE do prontuário eletrônico (parcela 52) — LGPD (Lei 13.709/2018) e
/// Lei 13.787/2018.
///
/// Estes testes nasceram de uma auditoria de fornecedor feita pela própria cliente, com
/// dez pontos por escrito. Cobrem os três que dependem de código e que o sistema não
/// atendia:
///
/// - <b>ponto 3</b> — saber quem ACESSOU o prontuário (a trilha só via escrita);
/// - <b>ponto 4</b> — o prontuário não podia ser apagado nem alterado em silêncio (era);
/// - <b>ponto 7</b> — guarda de 20 anos a partir do último registro (não existia).
///
/// O que se testa aqui não é comportamento de tela: é a promessa que a clínica vai
/// repetir para quem a auditar. Promessa sem teste é a "garantia aparente" que este
/// projeto recusa desde a parcela 3.
/// </summary>
public class ConformidadeProntuarioTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ProntuarioService _prontuario;
    private readonly GuardaProntuarioService _guarda;
    private readonly ExportacaoProntuarioService _exportacao;

    private static readonly DateOnly Dia = new(2026, 8, 3);

    public ConformidadeProntuarioTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _prontuario = new ProntuarioService(_repo);
        _guarda = new GuardaProntuarioService(_repo);
        _exportacao = new ExportacaoProntuarioService(_repo);
    }

    private async Task<int> CriarPacienteAsync(string nome = "Maria")
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private Task<Evolucao> SessaoAsync(int pacienteId, DateOnly data, string queixa = "dor lombar")
        => _prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = pacienteId,
            Data = data,
            QueixaPrincipal = queixa
        }, "dra.ana");

    // ==================================================== ponto 7 — guarda de 20 anos

    [Fact]
    public void O_prazo_conta_do_ULTIMO_registro_e_sao_20_anos()
    {
        GuardaProntuario.AnosDeGuarda.Should().Be(20, "é o piso da Lei 13.787/2018, art. 6º");
        GuardaProntuario.VenceEm(new DateOnly(2026, 8, 3))
            .Should().Be(new DateOnly(2046, 8, 3));
    }

    [Fact]
    public void Prontuario_dentro_do_prazo_NAO_e_elegivel_a_eliminacao()
    {
        var ultimo = new DateOnly(2026, 8, 3);

        GuardaProntuario.CumpriuPrazo(ultimo, new DateOnly(2046, 8, 2)).Should().BeFalse();
        GuardaProntuario.CumpriuPrazo(ultimo, new DateOnly(2046, 8, 3)).Should().BeTrue(
            "no dia exato o prazo está cumprido — 20 anos é piso, não 'mais de 20'");
    }

    [Fact]
    public async Task A_guarda_conta_do_registro_MAIS_RECENTE_e_nao_do_primeiro()
    {
        var paciente = await CriarPacienteAsync();
        await SessaoAsync(paciente, new DateOnly(2020, 1, 10));
        await SessaoAsync(paciente, new DateOnly(2026, 8, 3));

        var situacao = await _guarda.DoPacienteAsync(paciente);

        situacao.UltimoRegistro.Should().Be(new DateOnly(2026, 8, 3));
        situacao.VenceEm.Should().Be(new DateOnly(2046, 8, 3),
            "contar do primeiro atendimento faria o prontuário de quem ainda se trata "
            + "vencer com sessões recentes dentro dele");
    }

    [Fact]
    public async Task Sessao_CANCELADA_continua_sob_guarda_e_conta_para_o_prazo()
    {
        var paciente = await CriarPacienteAsync();
        var sessao = await SessaoAsync(paciente, Dia);
        await _prontuario.CancelarAsync(sessao.Id, "paciente trocado", "dra.ana");

        var situacao = await _guarda.DoPacienteAsync(paciente);

        situacao.De(NaturezaRegistroClinico.SessaoMedica).Should().Be(0);
        situacao.SessoesCanceladas.Should().Be(1);
        situacao.UltimoRegistro.Should().Be(Dia, "cancelada é registro guardado, não registro inexistente");
        situacao.TotalDeRegistros.Should().Be(1);
    }

    [Fact]
    public async Task Paciente_sem_nada_no_prontuario_nao_tem_prazo_a_contar()
    {
        var paciente = await CriarPacienteAsync();

        var situacao = await _guarda.DoPacienteAsync(paciente);

        situacao.SemRegistro.Should().BeTrue();
        situacao.VenceEm.Should().BeNull("zero não é uma data, e inventar uma daria prazo falso");
        situacao.Descrever(Dia).Should().Contain("Sem registro");
    }

    // ================================= ponto 4 — nada de apagar nem alterar em silêncio

    [Fact]
    public void O_repositorio_NAO_oferece_como_apagar_registro_clinico()
    {
        // É o teste que impede a regressão mais provável desta parcela: alguém reintroduz
        // um Remover*Async "só para a tela X", e a guarda de 20 anos deixa de valer sem
        // que nenhum outro teste perceba.
        var proibidos = new[]
        {
            "RemoverEvolucaoAsync", "RemoverAnexoAsync",
            "RemoverMedidaAsync", "RemoverAvaliacaoAsync",
            // ⚠️ Entra AQUI no mesmo commit em que a entidade nasce. A rede casa por LISTA
            // DE NOMES, e um método novo que ela não conhece a faria responder "está limpo"
            // — o ponto cego que a parcela 60 pagou caro para descobrir.
            "RemoverEvolucaoEnfermagemAsync",
            "RemoverResultadoExameAsync"
        };

        var metodos = typeof(Clinica.Application.Abstracoes.IClinicaRepositorio)
            .GetMethods().Select(m => m.Name).ToList();

        metodos.Should().NotIntersectWith(proibidos,
            "registro clínico se CANCELA com motivo; enquanto o método existir, "
            + "alguma tela futura vai chamá-lo");
    }

    [Fact]
    public async Task Excluir_paciente_com_registro_clinico_e_RECUSADO()
    {
        // A porta que o teste acima não via: RemoverPacienteAsync não apaga "só a ficha" —
        // as FKs clínicas apagam em CASCATA, e o prontuário inteiro iria junto. Ficha
        // vazia (cadastro por engano) pode sair; ficha com prontuário, nunca.
        var pacienteId = await CriarPacienteAsync();
        await SessaoAsync(pacienteId, Dia);

        var pacientes = new PacienteService(_repo);

        var recusa = await Assert.ThrowsAsync<InvalidOperationException>(
            () => pacientes.RemoverAsync(pacienteId));
        recusa.Message.Should().Contain("20 anos", "a recusa explica a guarda legal");
        recusa.Message.Should().Contain("anonimiza", "e aponta o caminho certo (LGPD art. 16, II)");

        (await _db.Pacientes.AnyAsync(p => p.Id == pacienteId)).Should().BeTrue();
        (await _db.Evolucoes.AnyAsync(e => e.PacienteId == pacienteId)).Should().BeTrue();
    }

    /// <summary>
    /// ⚠️ A sétima raiz clínica (parcela 71). Sem ela na conta de
    /// <c>PacienteTemRegistroClinicoAsync</c>, a ficha cujo ÚNICO registro é uma evolução
    /// de enfermagem continuaria REMOVÍVEL — e a exclusão a levaria por arrasto, com o
    /// teste da lista de métodos proibidos verde ao lado. É literalmente o buraco que a
    /// parcela 60 achou no botão de excluir paciente, um ano depois de ele existir.
    /// </summary>
    [Fact]
    public async Task Excluir_paciente_que_so_tem_evolucao_de_ENFERMAGEM_e_RECUSADO()
    {
        var pacienteId = await CriarPacienteAsync("Só Enfermagem");

        await new Clinica.Application.Servicos.EvolucaoEnfermagemService(_repo).RegistrarAsync(
            pacienteId, Dia, new TimeOnly(9, 0), "Curativo trocado.",
            new Clinica.Application.Servicos.IdentificacaoExecutante(
                null, "Joana Técnica", "COREN-SP 999999"));

        var recusa = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new PacienteService(_repo).RemoverAsync(pacienteId));

        recusa.Message.Should().Contain("20 anos");
        (await _db.EvolucoesEnfermagem.AnyAsync(e => e.PacienteId == pacienteId))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Excluir_ficha_VAZIA_continua_permitido()
    {
        // A recusa não pode virar trava geral: a duplicata cadastrada por engano, sem um
        // único registro clínico, é exatamente o caso em que remover é o certo.
        var pacienteId = await CriarPacienteAsync("Duplicada Sem Uso");

        await new PacienteService(_repo).RemoverAsync(pacienteId);

        (await _db.Pacientes.AnyAsync(p => p.Id == pacienteId)).Should().BeFalse();
    }

    [Fact]
    public async Task Corrigir_a_sessao_preserva_o_texto_anterior_recuperavel()
    {
        var paciente = await CriarPacienteAsync();
        var sessao = await SessaoAsync(paciente, Dia, "dor no ombro DIREITO");

        await _prontuario.SalvarAsync(new Evolucao
        {
            Id = sessao.Id,
            PacienteId = paciente,
            Data = Dia,
            QueixaPrincipal = "dor no ombro ESQUERDO"
        }, "dra.ana", "lado trocado na digitação");

        var versoes = await _prontuario.VersoesAsync(sessao.Id);

        versoes.Should().ContainSingle();
        versoes[0].QueixaPrincipal.Should().Be("dor no ombro DIREITO");
        versoes[0].Motivo.Should().Be("lado trocado na digitação");

        // E a versão vigente é a corrigida — as duas coexistem sem ambiguidade.
        (await _prontuario.DoPacienteAsync(paciente))[0]
            .QueixaPrincipal.Should().Be("dor no ombro ESQUERDO");
    }

    [Fact]
    public async Task Toda_alteracao_e_todo_cancelamento_deixam_rastro_na_auditoria()
    {
        var paciente = await CriarPacienteAsync();
        var sessao = await SessaoAsync(paciente, Dia);

        await _prontuario.SalvarAsync(new Evolucao
        {
            Id = sessao.Id, PacienteId = paciente, Data = Dia, QueixaPrincipal = "outra"
        }, "dra.ana");
        await _prontuario.CancelarAsync(sessao.Id, "engano", "dra.ana");

        var acoes = await _db.Auditoria.AsNoTracking().Select(e => e.Acao).ToListAsync();

        acoes.Should().Contain("EvolucaoRegistrada")
             .And.Contain("EvolucaoAlterada")
             .And.Contain("EvolucaoCancelada");
    }

    // ============================================= ponto 3 — quem ACESSOU o prontuário

    [Fact]
    public async Task Abrir_o_prontuario_deixa_rastro_de_QUEM_e_QUANDO()
    {
        var paciente = await CriarPacienteAsync();
        var acessos = new AcessoProntuarioService(_repo);

        (await acessos.RegistrarAsync(paciente, "recepcao.joana",
            OrigemAcessoProntuario.FichaPaciente)).Should().BeTrue();

        var trilha = await acessos.DoPacienteAsync(paciente);

        trilha.Should().ContainSingle();
        trilha[0].Operador.Should().Be("recepcao.joana");
        trilha[0].PacienteId.Should().Be(paciente);
        trilha[0].Detalhe.Should().Contain("Ficha");
    }

    [Fact]
    public async Task Reabrir_o_mesmo_prontuario_na_mesma_janela_nao_polui_a_trilha()
    {
        var paciente = await CriarPacienteAsync();
        var acessos = new AcessoProntuarioService(_repo);

        await acessos.RegistrarAsync(paciente, "dra.ana");
        var segunda = await acessos.RegistrarAsync(paciente, "dra.ana");

        segunda.Should().BeFalse("um atendimento entre quatro abas não são quatro acessos");
        (await acessos.DoPacienteAsync(paciente)).Should().ContainSingle();
    }

    [Fact]
    public async Task Passada_a_janela_o_acesso_volta_a_ser_registrado()
    {
        var paciente = await CriarPacienteAsync();
        var agora = new DateTime(2026, 8, 3, 9, 0, 0);

        await new AcessoProntuarioService(_repo, () => agora).RegistrarAsync(paciente, "dra.ana");

        // Mesma pessoa, mesmo paciente, à tarde: são DOIS acessos, e fundi-los esconderia
        // justamente o padrão que uma investigação procura.
        var depois = agora.AddHours(6);
        (await new AcessoProntuarioService(_repo, () => depois)
            .RegistrarAsync(paciente, "dra.ana")).Should().BeTrue();

        (await new AcessoProntuarioService(_repo, () => depois)
            .DoPacienteAsync(paciente)).Should().HaveCount(2);
    }

    [Fact]
    public async Task Pessoas_diferentes_nao_compartilham_a_janela_de_silencio()
    {
        var paciente = await CriarPacienteAsync();
        var acessos = new AcessoProntuarioService(_repo);

        await acessos.RegistrarAsync(paciente, "ana");
        // "ana" é trecho de "mariana": se a janela casasse por trecho, o acesso da segunda
        // pessoa desapareceria — que é exatamente o acesso que se quer enxergar.
        (await acessos.RegistrarAsync(paciente, "mariana")).Should().BeTrue();

        (await acessos.DoPacienteAsync(paciente)).Should().HaveCount(2);
    }

    [Fact]
    public async Task Origens_diferentes_do_mesmo_paciente_sao_acessos_diferentes()
    {
        var paciente = await CriarPacienteAsync();
        var acessos = new AcessoProntuarioService(_repo);

        await acessos.RegistrarAsync(paciente, "dra.ana", OrigemAcessoProntuario.Atendimento);
        (await acessos.RegistrarAsync(paciente, "dra.ana",
            OrigemAcessoProntuario.ProntuarioClinico)).Should().BeTrue(
            "ler a ficha e abrir o prontuário inteiro são acessos de natureza diferente");
    }

    // ========================= a folha de infusão também é prontuário (regra 8 do CLAUDE)

    /// <summary>
    /// Cria uma folha assinada com um item já checado — o estado em que ela fica depois
    /// que a enfermagem executou.
    /// </summary>
    private async Task<PrescricaoInterna> FolhaExecutadaAsync(int pacienteId, DateOnly data)
    {
        var folha = new PrescricaoInterna
        {
            Numero = $"PRE {data.Year}/0001",
            CodigoVerificacao = "ABCD2345",
            PacienteId = pacienteId,
            Data = data,
            Hora = new TimeOnly(14, 0),
            Situacao = SituacaoPrescricao.Encerrada,
            AssinadaEm = data.ToDateTime(new TimeOnly(13, 55)),
            EncerradaEm = data.ToDateTime(new TimeOnly(15, 30))
        };

        folha.Itens.Add(new ItemPrescricaoInterna
        {
            Ordem = 1,
            Descricao = "Dipirona 500mg",
            Dose = "1 ampola",
            Via = ViaAdministracao.Endovenosa,
            Checagens =
            {
                new ChecagemPrescricao
                {
                    Situacao = SituacaoChecagem.NaoRealizado,
                    HoraRealizacao = new TimeOnly(14, 20),
                    Justificativa = "paciente apresentou reação alérgica",
                    ExecutanteNome = "tec.carla",
                    ExecutanteConselho = "COREN 123456",
                    RegistradoEm = data.ToDateTime(new TimeOnly(14, 35))
                }
            }
        });

        _db.PrescricoesInternas.Add(folha);
        await _db.SaveChangesAsync();
        return folha;
    }

    [Fact]
    public async Task A_folha_de_infusao_conta_para_o_prazo_de_guarda()
    {
        var paciente = await CriarPacienteAsync();
        await SessaoAsync(paciente, new DateOnly(2026, 1, 10));
        await FolhaExecutadaAsync(paciente, new DateOnly(2026, 8, 3));

        var situacao = await _guarda.DoPacienteAsync(paciente);

        situacao.De(NaturezaRegistroClinico.PrescricaoInterna).Should().Be(1);
        situacao.UltimoRegistro.Should().Be(new DateOnly(2026, 8, 3),
            "a folha diz o que entrou no paciente — ignorá-la calcularia o prazo pelo "
            + "registro errado, e para MENOS");
        situacao.Origem.Should().Contain("prescrição");
    }

    [Fact]
    public async Task A_exportacao_leva_a_folha_os_itens_e_a_EXECUCAO()
    {
        var paciente = await CriarPacienteAsync("Joana");
        await FolhaExecutadaAsync(paciente, Dia);

        var arquivos = await _exportacao.ExportarAsync();

        arquivos.Single(a => a.Nome == "prontuario-prescricoes.csv")
            .Conteudo.Should().Contain("PRE 2026/0001").And.Contain("ABCD2345");

        arquivos.Single(a => a.Nome == "prontuario-prescricoes-itens.csv")
            .Conteudo.Should().Contain("Dipirona 500mg");

        // A execução é a parte que uma auditoria de enfermagem procura: o que entrou, a
        // que horas e por quem — e o horário INFORMADO ao lado do relógio do registro.
        var execucao = arquivos.Single(a => a.Nome == "prontuario-prescricoes-checagens.csv").Conteudo;
        execucao.Should().Contain("tec.carla")
                .And.Contain("COREN 123456")
                .And.Contain("14:20")
                .And.Contain("reação alérgica");
    }

    // ================================================ ponto 8 — exportação sem reféns

    [Fact]
    public async Task A_exportacao_leva_as_sessoes_canceladas_MARCADAS()
    {
        var paciente = await CriarPacienteAsync("Maria Silva");
        var vigente = await SessaoAsync(paciente, Dia, "queixa vigente");
        var cancelada = await SessaoAsync(paciente, Dia.AddDays(1), "queixa cancelada");
        await _prontuario.CancelarAsync(cancelada.Id, "duplicada", "dra.ana");

        var arquivos = await _exportacao.ExportarAsync();
        var sessoes = arquivos.Single(a => a.Nome == "prontuario-sessoes.csv").Conteudo;

        sessoes.Should().Contain("queixa vigente").And.Contain("Vigente");
        sessoes.Should().Contain("queixa cancelada").And.Contain("Cancelada",
            "omitir as canceladas entregaria ao próximo fornecedor um histórico higienizado");
        vigente.Id.Should().BePositive();
    }

    [Fact]
    public async Task A_exportacao_escapa_texto_com_ponto_e_virgula_e_quebra_de_linha()
    {
        var paciente = await CriarPacienteAsync();
        await _prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = paciente,
            Data = Dia,
            // Evolução é texto livre: sem escape, esta linha desalinharia todas as colunas
            // dali para baixo e o arquivo pareceria bom até alguém tentar importá-lo.
            TextoEvolucao = "agulhamento; sem intercorrência\nretorno em 7 dias"
        }, "dra.ana");

        var sessoes = (await _exportacao.ExportarAsync())
            .Single(a => a.Nome == "prontuario-sessoes.csv").Conteudo;

        sessoes.Should().Contain("\"agulhamento; sem intercorrência\nretorno em 7 dias\"");
    }

    [Fact]
    public async Task A_exportacao_leva_a_lista_de_problemas_com_as_ALERGIAS()
    {
        // A lacuna que a auditoria achou: o prontuário exportado saía SEM as alergias —
        // o próximo fornecedor receberia um paciente "sem alergia nenhuma".
        var paciente = await CriarPacienteAsync("Alérgica Conhecida");
        _db.ProblemasPaciente.Add(new ProblemaPaciente
        {
            PacienteId = paciente,
            Natureza = NaturezaProblema.Alergia,
            Descricao = "dipirona",
            CriadoPor = "dra.ana"
        });
        await _db.SaveChangesAsync();

        var arquivos = await _exportacao.ExportarAsync();

        var problemas = arquivos.Single(a => a.Nome == "prontuario-problemas.csv").Conteudo;
        problemas.Should().Contain("Alergia").And.Contain("dipirona");

        // E os arquivos novos existem mesmo vazios — coluna estável é contrato.
        arquivos.Should().Contain(a => a.Nome == "prontuario-mapa-corporal.csv");
        arquivos.Should().Contain(a => a.Nome == "prontuario-avaliacoes-respostas.csv");
    }

    [Fact]
    public async Task A_guarda_conta_o_prazo_pelo_problema_quando_ele_e_o_ultimo_registro()
    {
        var paciente = await CriarPacienteAsync();
        await SessaoAsync(paciente, Dia);

        var inicioDoProblema = Dia.AddMonths(3);
        _db.ProblemasPaciente.Add(new ProblemaPaciente
        {
            PacienteId = paciente,
            Natureza = NaturezaProblema.Diagnostico,
            Descricao = "lombalgia crônica",
            Inicio = inicioDoProblema
        });
        await _db.SaveChangesAsync();

        var situacao = await _guarda.DoPacienteAsync(paciente);

        situacao.UltimoRegistro.Should().Be(inicioDoProblema,
            "o prazo conta do ÚLTIMO registro de qualquer natureza — sem o problema na "
            + "conta, um paciente cujo último fato clínico é um diagnóstico anotado teria "
            + "o prazo calculado pelo registro errado");
    }

    [Fact]
    public async Task A_exportacao_explica_o_que_NAO_leva()
    {
        await CriarPacienteAsync();

        var leiaMe = (await _exportacao.ExportarAsync())
            .Single(a => a.Nome == "LEIA-ME.txt").Conteudo;

        leiaMe.Should().Contain("BYTES", "prometer que o CSV traz os laudos seria mentir sobre a entrega");
        leiaMe.Should().Contain("20 anos");
    }

    /// <summary>
    /// ⚠️ A ANAMNESE é raiz clínica, e a FK dela é CASCATA.
    ///
    /// Sem esta linha em <c>PacienteTemRegistroClinicoAsync</c>, a ficha cujo único registro
    /// clínico é a anamnese continuaria REMOVÍVEL, e a exclusão levaria por arrasto os
    /// antecedentes E as versões deles — que é exatamente a cascata que a parcela 60 achou no
    /// botão de excluir paciente, com o teste verde ao lado.
    ///
    /// A lição que este teste fixa: <b>raiz clínica nova entra nesta lista no MESMO commit</b>.
    /// Ela não quebra build quando é esquecida.
    /// </summary>
    [Fact]
    public async Task Paciente_com_ANAMNESE_nao_pode_ser_removido()
    {
        var p = new Paciente
        {
            Nome = "Marisa",
            Convenio = Clinica.Domain.Convenio.UnimedIntercambio,
            Sexo = Clinica.Domain.Sexo.Feminino
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();

        await new AnamneseService(_repo).SalvarAsync(p.Id, new AnamnesePaciente
        {
            AntecedentesPessoais = "Apendicectomia em 2015."
        }, "dra.ana");

        (await _repo.PacienteTemRegistroClinicoAsync(p.Id)).Should().BeTrue();
    }

    /// <summary>
    /// A NONA raiz (ago/2026): a ficha cujo único registro clínico é um resultado de
    /// exame estruturado não pode ser removível — mesma regra, mesma razão da anamnese
    /// logo acima.
    /// </summary>
    [Fact]
    public async Task Paciente_com_RESULTADO_DE_EXAME_nao_pode_ser_removido()
    {
        var p = new Paciente
        {
            Nome = "Otávio",
            Convenio = Clinica.Domain.Convenio.UnimedIntercambio,
            Sexo = Clinica.Domain.Sexo.Masculino
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();

        await new ResultadoExameService(_repo).RegistrarAsync(new ResultadoExame
        {
            PacienteId = p.Id,
            Data = DateOnly.FromDateTime(DateTime.Today),
            Nome = "Hemoglobina glicada",
            Valor = "6,1",
            Unidade = "%"
        }, "dra.ana");

        (await _repo.PacienteTemRegistroClinicoAsync(p.Id)).Should().BeTrue();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
