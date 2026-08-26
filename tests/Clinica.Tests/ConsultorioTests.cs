using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Avaliacoes;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O consultório do profissional (parcela 36): o dia de quem atende, a carteira de
/// pacientes e as escalas por especialidade.
///
/// A regra que carrega a parcela — e que estes testes existem para travar — é a mesma que
/// vale para o protocolo do mapa corporal e para o preço por convênio: **o que descreve o
/// instrumento é COPIADO na aplicação, nunca referenciado**. Prontuário descreve o que
/// aconteceu, e uma redação corrigida amanhã não pode reescrever o que o paciente
/// respondeu hoje.
/// </summary>
public class ConsultorioTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ProntuarioService _prontuario;
    private readonly ConsultorioService _consultorio;
    private readonly AvaliacaoClinicaService _avaliacoes;

    private static readonly DateOnly Hoje = new(2026, 8, 10);

    public ConsultorioTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _prontuario = new ProntuarioService(_repo);
        _consultorio = new ConsultorioService(_repo);
        _avaliacoes = new AvaliacaoClinicaService(_repo);
    }

    // ---------------------------------------------------------------- apoio

    private async Task<int> CriarPacienteAsync(string nome = "Paciente")
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<int> CriarProfissionalAsync(string nome = "Dra. Ana", string? especialidade = null)
    {
        var p = new Profissional { Nome = nome, EspecialidadeCodigo = especialidade };
        _db.Profissionais.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<int> AgendarAsync(
        int pacienteId, int? profissionalId, DateOnly dia, int hora,
        StatusAgendamento status = StatusAgendamento.Realizado)
    {
        var ag = new Agendamento
        {
            PacienteId = pacienteId,
            ProfissionalId = profissionalId,
            DataHora = dia.ToDateTime(new TimeOnly(hora, 0)),
            ModalidadePrevista = ModalidadeAtendimento.AcupunturaSimples,
            Status = status
        };
        _db.Agendamentos.Add(ag);
        await _db.SaveChangesAsync();
        return ag.Id;
    }

    private Task<Evolucao> EscreverAsync(
        int pacienteId, DateOnly dia, int? profissionalId = null, int? agendamentoId = null,
        int? antes = null, int? depois = null)
        => _prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = pacienteId,
            ProfissionalId = profissionalId,
            AgendamentoId = agendamentoId,
            Data = dia,
            EvaAntes = antes,
            EvaDepois = depois,
            QueixaPrincipal = "dor lombar"
        }, "dra.ana");

    // ---------------------------------------------------------------- o dia

    [Fact]
    public async Task O_dia_traz_so_os_horarios_do_profissional()
    {
        var ana = await CriarProfissionalAsync("Dra. Ana");
        var bruno = await CriarProfissionalAsync("Dr. Bruno");
        var p1 = await CriarPacienteAsync("Um");
        var p2 = await CriarPacienteAsync("Dois");

        await AgendarAsync(p1, ana, Hoje, 9);
        await AgendarAsync(p2, bruno, Hoje, 10);

        var dia = await _consultorio.DoDiaAsync(Hoje, ana);

        dia.Sessoes.Should().HaveCount(1);
        dia.Sessoes[0].PacienteNome.Should().Be("Um");
        dia.ProfissionalNome.Should().Be("Dra. Ana");
    }

    [Fact]
    public async Task Sem_profissional_vinculado_o_dia_e_o_da_clinica_inteira()
    {
        // Tela vazia se lê como defeito, não como cadastro faltando: quem não tem vínculo
        // vê o dia da casa, e a tela DIZ que está fazendo isso.
        var ana = await CriarProfissionalAsync();
        var p1 = await CriarPacienteAsync("Um");
        var p2 = await CriarPacienteAsync("Dois");

        await AgendarAsync(p1, ana, Hoje, 9);
        await AgendarAsync(p2, null, Hoje, 10);

        var dia = await _consultorio.DoDiaAsync(Hoje, null);

        dia.Sessoes.Should().HaveCount(2);
        dia.ProfissionalNome.Should().Be("Todos os profissionais");
    }

    [Fact]
    public async Task Sessao_atendida_sem_evolucao_conta_como_registro_pendente()
    {
        var ana = await CriarProfissionalAsync();
        var paciente = await CriarPacienteAsync();
        await AgendarAsync(paciente, ana, Hoje, 9);

        var dia = await _consultorio.DoDiaAsync(Hoje, ana);

        dia.Atendidos.Should().Be(1);
        dia.RegistrosPendentes.Should().Be(1);
        dia.Sessoes[0].EvolucaoEscrita.Should().BeFalse();
    }

    [Fact]
    public async Task Evolucao_ligada_ao_agendamento_tira_a_sessao_da_pendencia()
    {
        // É o vínculo que faz o circuito fechar: sem ele o consultório cobraria para
        // sempre um registro que acabou de ser escrito.
        var ana = await CriarProfissionalAsync();
        var paciente = await CriarPacienteAsync();
        var agendamento = await AgendarAsync(paciente, ana, Hoje, 9);

        await EscreverAsync(paciente, Hoje, ana, agendamento);

        var dia = await _consultorio.DoDiaAsync(Hoje, ana);

        dia.RegistrosPendentes.Should().Be(0);
        dia.Sessoes[0].EvolucaoEscrita.Should().BeTrue();
    }

    [Fact]
    public async Task Evolucao_escrita_fora_da_fila_casa_por_paciente_e_data()
    {
        // A evolução escrita direto no prontuário não conhece o agendamento. Sem este
        // caminho de baixo, o consultório cobraria de novo um registro que já existe.
        var ana = await CriarProfissionalAsync();
        var paciente = await CriarPacienteAsync();
        await AgendarAsync(paciente, ana, Hoje, 9);

        await EscreverAsync(paciente, Hoje, ana);

        var dia = await _consultorio.DoDiaAsync(Hoje, ana);

        dia.RegistrosPendentes.Should().Be(0);
    }

    [Fact]
    public async Task Cancelado_e_falta_nao_pedem_evolucao()
    {
        var ana = await CriarProfissionalAsync();
        var p1 = await CriarPacienteAsync("Um");
        var p2 = await CriarPacienteAsync("Dois");

        await AgendarAsync(p1, ana, Hoje, 9, StatusAgendamento.Cancelado);
        await AgendarAsync(p2, ana, Hoje, 10, StatusAgendamento.Faltou);

        var dia = await _consultorio.DoDiaAsync(Hoje, ana);

        dia.Sessoes.Should().HaveCount(2, "quem cancelou continua aparecendo na agenda");
        dia.RegistrosPendentes.Should().Be(0, "não houve sessão para descrever");
    }

    [Fact]
    public async Task Pendencia_olha_dias_anteriores_e_nunca_o_de_hoje()
    {
        // A sessão das 14h ainda não tem evolução às 14h05 porque o paciente está na
        // sala. Cobrar isso transformaria a lista numa contagem do trabalho em curso.
        var ana = await CriarProfissionalAsync();
        var paciente = await CriarPacienteAsync();

        await AgendarAsync(paciente, ana, Hoje, 9);
        await AgendarAsync(paciente, ana, Hoje.AddDays(-3), 9);

        var pendentes = await _consultorio.RegistrosPendentesAsync(Hoje, ana);

        pendentes.Should().HaveCount(1);
        pendentes[0].DiasEmAberto(Hoje).Should().Be(3);
    }

    [Fact]
    public async Task Pendencia_nao_alcanca_o_que_e_velho_demais()
    {
        // Passada a janela o registro não se escreve mais de memória, e uma lista que
        // cresce sem fim vira ruído que a pessoa aprende a fechar sem ler.
        var ana = await CriarProfissionalAsync();
        var paciente = await CriarPacienteAsync();

        await AgendarAsync(paciente, ana,
            Hoje.AddDays(-ConsultorioService.JanelaRegistroPendenteDias - 5), 9);

        var pendentes = await _consultorio.RegistrosPendentesAsync(Hoje, ana);

        pendentes.Should().BeEmpty();
    }

    // ---------------------------------------------------------------- carteira

    [Fact]
    public async Task Carteira_traz_quem_o_profissional_atendeu_com_a_leitura_da_dor()
    {
        var ana = await CriarProfissionalAsync();
        var paciente = await CriarPacienteAsync("Joana");

        await AgendarAsync(paciente, ana, Hoje.AddDays(-14), 9);
        await AgendarAsync(paciente, ana, Hoje.AddDays(-7), 9);

        await EscreverAsync(paciente, Hoje.AddDays(-14), ana, antes: 8, depois: 5);
        await EscreverAsync(paciente, Hoje.AddDays(-7), ana, antes: 6, depois: 3);

        var carteira = await _consultorio.MeusPacientesAsync(ana);

        carteira.Should().HaveCount(1);
        carteira[0].Nome.Should().Be("Joana");
        carteira[0].Sessoes.Should().Be(2);
        carteira[0].DorInicial.Should().Be(8, "a dor ANTES da primeira sessão medida");
        carteira[0].UltimaDor.Should().Be(3, "a dor DEPOIS da última sessão medida");
        carteira[0].GanhoAcumulado.Should().Be(5);
    }

    [Fact]
    public async Task Carteira_nao_inventa_dor_sem_o_par_eva()
    {
        // "Não medido" e "zero" são coisas diferentes — a regra vale no consultório como
        // vale no resto do sistema.
        var ana = await CriarProfissionalAsync();
        var paciente = await CriarPacienteAsync();

        await AgendarAsync(paciente, ana, Hoje.AddDays(-7), 9);
        await EscreverAsync(paciente, Hoje.AddDays(-7), ana, antes: 7);

        var carteira = await _consultorio.MeusPacientesAsync(ana);

        carteira[0].UltimaDor.Should().BeNull();
        carteira[0].GanhoAcumulado.Should().BeNull();
    }

    [Fact]
    public async Task Quem_so_marcou_e_nao_veio_nao_entra_na_carteira()
    {
        var ana = await CriarProfissionalAsync();
        var faltou = await CriarPacienteAsync("Faltou");

        await AgendarAsync(faltou, ana, Hoje.AddDays(-2), 9, StatusAgendamento.Faltou);

        (await _consultorio.MeusPacientesAsync(ana)).Should().BeEmpty();
    }

    /// <summary>
    /// ⚠️ O BURACO SIMÉTRICO AO DA ENFERMAGEM (parcela 88). A carteira do profissional traz
    /// só quem ELE já atendeu — então o paciente de PRIMEIRA CONSULTA, o do colega e o que
    /// o balcão acabou de cadastrar eram inalcançáveis do Consultório: não havia segunda
    /// porta. A cliente estendeu a quem consulta o pedido que fez para a enfermagem — "ver
    /// todos os pacientes e clicar em atender" —, e é este caminho que o atende.
    /// </summary>
    [Fact]
    public async Task A_carteira_da_CLINICA_alcanca_quem_o_profissional_nunca_atendeu()
    {
        var ana = await CriarProfissionalAsync();
        var meu = await CriarPacienteAsync("Joana Atendida");
        await AgendarAsync(meu, ana, Hoje.AddDays(-7), 9);
        await EscreverAsync(meu, Hoje.AddDays(-7), ana, antes: 6, depois: 3);

        // Nunca atendido por ninguém — o caso da primeira consulta.
        var novo = await CriarPacienteAsync("Pedro Primeira Consulta");

        (await _consultorio.MeusPacientesAsync(ana))
            .Should().ContainSingle(p => p.PacienteId == meu,
                "a carteira dele é a de quem ele atendeu — e isso não muda");

        var daClinica = await _consultorio.MeusPacientesAsync(profissionalId: null);

        daClinica.Should().Contain(p => p.PacienteId == novo,
            "é o segundo clique que torna o paciente de primeira consulta alcançável");
        daClinica.Should().Contain(p => p.PacienteId == meu);
    }

    /// <summary>
    /// ⚠️ O TERMO PRECISA DESCER PARA O SQL na carteira da clínica. Ela é o cadastro
    /// inteiro cortado no limite, e filtrar em memória o que já veio cortado faz a busca
    /// responder "não existe" para todo paciente além do teto — que é a resposta errada
    /// mais cara que uma busca de paciente pode dar, porque leva a cadastrar a pessoa de
    /// novo (o CPF duplicado da parcela 57).
    /// </summary>
    [Fact]
    public async Task A_busca_da_carteira_da_clinica_alcanca_alem_do_teto()
    {
        // Mais gente do que o limite pedido, e o procurado FORA da primeira página: em
        // ordem de nome, "Zulmira" é a última.
        for (var i = 0; i < 5; i++) await CriarPacienteAsync($"Ana {i:00}");
        var procurado = await CriarPacienteAsync("Zulmira Escondida");

        var primeiraPagina = await _consultorio.MeusPacientesAsync(
            profissionalId: null, limite: 3, comDor: false);

        primeiraPagina.Should().HaveCount(3);
        primeiraPagina.Should().NotContain(p => p.PacienteId == procurado,
            "ela está fora do teto — é exatamente o caso que o filtro de memória perderia");

        var achada = await _consultorio.MeusPacientesAsync(
            profissionalId: null, limite: 3, comDor: false, termo: "Zulmira");

        achada.Should().ContainSingle(p => p.PacienteId == procurado);
    }

    /// <summary>
    /// E o termo NÃO altera a carteira própria: ali quem filtra é a tela, em memória,
    /// porque a lista tem teto e cabe nela — ir ao banco a cada tecla daria uma consulta
    /// por letra digitada. A assimetria é declarada, e este teste a fixa.
    /// </summary>
    [Fact]
    public async Task O_termo_nao_mexe_na_carteira_propria()
    {
        var ana = await CriarProfissionalAsync();
        var paciente = await CriarPacienteAsync("Joana");
        await AgendarAsync(paciente, ana, Hoje.AddDays(-7), 9);
        await EscreverAsync(paciente, Hoje.AddDays(-7), ana, antes: 6, depois: 3);

        (await _consultorio.MeusPacientesAsync(ana, termo: "nome que não existe"))
            .Should().ContainSingle(p => p.PacienteId == paciente);
    }

    // ---------------------------------------------------------------- avaliações

    [Fact]
    public async Task Aplicar_copia_nome_enunciado_e_faixa_para_a_avaliacao()
    {
        var paciente = await CriarPacienteAsync();
        IInstrumentoAvaliacao gad7 = new InstrumentoGad7();

        var salva = await _avaliacoes.AplicarAsync(
            paciente, gad7.Codigo,
            gad7.Itens.ToDictionary(i => i.Codigo, _ => 2),
            Hoje, operador: "dra.ana");

        salva.Pontuacao.Should().Be(14);
        salva.InstrumentoNome.Should().Be(gad7.Nome, "o nome é copiado, não referenciado");
        salva.FaixaNome.Should().Be("Ansiedade moderada");
        salva.FaixaGravidade.Should().Be(GravidadeFaixa.Atencao);
        salva.Respostas.Should().HaveCount(7);
        salva.Respostas[0].Enunciado.Should().Be(gad7.Itens[0].Enunciado);
        salva.Respostas[0].OpcaoRotulo.Should().Be("Mais da metade dos dias");
    }

    [Fact]
    public async Task Escala_incompleta_e_recusada()
    {
        // Numa escala de soma, o item em branco vira zero — e zero significa "não tenho
        // esse sintoma": a pergunta não respondida viraria resposta não dada.
        var paciente = await CriarPacienteAsync();
        IInstrumentoAvaliacao phq9 = new InstrumentoPhq9();

        var incompleta = phq9.Itens.Take(4).ToDictionary(i => i.Codigo, _ => 1);

        var acao = () => _avaliacoes.AplicarAsync(paciente, phq9.Codigo, incompleta, Hoje);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*escala completa*");
    }

    [Fact]
    public async Task Oswestry_aceita_secao_em_branco_porque_a_regra_dele_preve()
    {
        var paciente = await CriarPacienteAsync();
        IInstrumentoAvaliacao odi = new InstrumentoOswestry();

        var semUma = odi.Itens.Skip(1).ToDictionary(i => i.Codigo, _ => 5);

        var salva = await _avaliacoes.AplicarAsync(paciente, odi.Codigo, semUma, Hoje);

        salva.Pontuacao.Should().Be(100);
        salva.Unidade.Should().Be("%");
        salva.PontuacaoFormatada.Should().Be("100 %");
        salva.Respostas.Should().HaveCount(8, "a seção não respondida não vira resposta");
    }

    [Fact]
    public async Task Peso_fora_das_alternativas_e_recusado()
    {
        // Aceitar produziria um escore que a escala não sabe interpretar, com aparência
        // de escore válido.
        var paciente = await CriarPacienteAsync();
        IInstrumentoAvaliacao gad7 = new InstrumentoGad7();

        var invalidas = gad7.Itens.ToDictionary(i => i.Codigo, _ => 9);

        var acao = () => _avaliacoes.AplicarAsync(paciente, gad7.Codigo, invalidas, Hoje);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Resposta inválida*");
    }

    [Fact]
    public async Task Item_desconhecido_e_ignorado_sem_derrubar_a_aplicacao()
    {
        var paciente = await CriarPacienteAsync();
        IInstrumentoAvaliacao gad7 = new InstrumentoGad7();

        var respostas = gad7.Itens.ToDictionary(i => i.Codigo, _ => 1);
        respostas["ITEM_DE_UMA_VERSAO_ANTIGA"] = 3;

        var salva = await _avaliacoes.AplicarAsync(paciente, gad7.Codigo, respostas, Hoje);

        salva.Pontuacao.Should().Be(7, "só os itens do instrumento entram no escore");
        salva.Respostas.Should().HaveCount(7);
    }

    [Fact]
    public async Task Alerta_do_item_9_e_gravado_junto_da_avaliacao()
    {
        // Gravado, e não recalculado na leitura: a lista precisa mostrá-lo sem reabrir
        // item por item, e ele é parte do que aconteceu naquele dia.
        var paciente = await CriarPacienteAsync();
        IInstrumentoAvaliacao phq9 = new InstrumentoPhq9();

        var respostas = phq9.Itens.ToDictionary(i => i.Codigo, _ => 0);
        respostas[InstrumentoPhq9.ItemIdeacao] = 2;

        var salva = await _avaliacoes.AplicarAsync(paciente, phq9.Codigo, respostas, Hoje);

        salva.Pontuacao.Should().Be(2);
        salva.FaixaNome.Should().Be("Sintomas mínimos");
        salva.TemAlertaDeItem.Should().BeTrue("o item mais grave não pode sumir dentro do total");
    }

    [Fact]
    public async Task Aplicar_grava_auditoria_no_mesmo_savechanges()
    {
        var paciente = await CriarPacienteAsync();
        IInstrumentoAvaliacao katz = new InstrumentoKatz();

        await _avaliacoes.AplicarAsync(
            paciente, katz.Codigo,
            katz.Itens.ToDictionary(i => i.Codigo, _ => 1),
            Hoje, operador: "dra.ana");

        var eventos = await _db.Auditoria.ToListAsync();
        eventos.Should().ContainSingle(e => e.Acao == "AvaliacaoAplicada" && e.Operador == "dra.ana");
    }

    [Fact]
    public async Task Instrumento_fora_do_catalogo_e_recusado()
    {
        var paciente = await CriarPacienteAsync();

        var acao = () => _avaliacoes.AplicarAsync(
            paciente, "ESCALA-INVENTADA", new Dictionary<string, int> { ["X"] = 1 }, Hoje);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não existe no catálogo*");
    }

    [Fact]
    public async Task Curva_do_escore_sai_em_ordem_cronologica_com_o_ganho_resolvido()
    {
        var paciente = await CriarPacienteAsync();
        IInstrumentoAvaliacao phq9 = new InstrumentoPhq9();

        async Task Aplicar(DateOnly dia, int valorPorItem) => await _avaliacoes.AplicarAsync(
            paciente, phq9.Codigo,
            phq9.Itens.ToDictionary(i => i.Codigo, _ => valorPorItem), dia);

        await Aplicar(Hoje.AddDays(-60), 2); // 18 pontos
        await Aplicar(Hoje.AddDays(-30), 1); // 9 pontos
        await Aplicar(Hoje, 0);              // 0 pontos

        var curva = await _avaliacoes.EvolucaoDoEscoreAsync(paciente, phq9.Codigo);

        curva.Pontos.Select(p => p.Pontuacao).Should().Equal(18, 9, 0);
        curva.Primeiro.Should().Be(18);
        curva.Atual.Should().Be(0);
        curva.Ganho.Should().Be(18, "no PHQ-9 cair é melhorar");
    }

    [Fact]
    public async Task Curva_do_katz_inverte_o_sinal_da_melhora()
    {
        // O Katz é o único em que subir é melhorar. Sem a inversão, a recuperação
        // funcional do paciente apareceria como piora.
        var paciente = await CriarPacienteAsync();
        IInstrumentoAvaliacao katz = new InstrumentoKatz();

        await _avaliacoes.AplicarAsync(paciente, katz.Codigo,
            katz.Itens.ToDictionary(i => i.Codigo, _ => 0), Hoje.AddDays(-90));
        await _avaliacoes.AplicarAsync(paciente, katz.Codigo,
            katz.Itens.ToDictionary(i => i.Codigo, _ => 1), Hoje);

        var curva = await _avaliacoes.EvolucaoDoEscoreAsync(paciente, katz.Codigo);

        curva.MelhorQuandoMenor.Should().BeFalse();
        curva.Primeiro.Should().Be(0);
        curva.Atual.Should().Be(6);
        curva.Ganho.Should().Be(6, "positivo é sempre melhora, nos dois sentidos de escala");
    }

    [Fact]
    public async Task Uma_aplicacao_so_nao_e_evolucao_de_nada()
    {
        // Mesma regra do par EVA: uma medida solta é linha de base, não evolução.
        var paciente = await CriarPacienteAsync();
        IInstrumentoAvaliacao gad7 = new InstrumentoGad7();

        await _avaliacoes.AplicarAsync(paciente, gad7.Codigo,
            gad7.Itens.ToDictionary(i => i.Codigo, _ => 1), Hoje);

        var curva = await _avaliacoes.EvolucaoDoEscoreAsync(paciente, gad7.Codigo);

        curva.Aplicacoes.Should().Be(1);
        curva.Ganho.Should().BeNull();
    }

    [Fact]
    public async Task Curva_de_um_instrumento_nao_mistura_o_escore_de_outro()
    {
        // Escores de escalas diferentes não se somam nem compartilham eixo.
        var paciente = await CriarPacienteAsync();
        IInstrumentoAvaliacao phq9 = new InstrumentoPhq9();
        IInstrumentoAvaliacao gad7 = new InstrumentoGad7();

        await _avaliacoes.AplicarAsync(paciente, phq9.Codigo,
            phq9.Itens.ToDictionary(i => i.Codigo, _ => 1), Hoje);
        await _avaliacoes.AplicarAsync(paciente, gad7.Codigo,
            gad7.Itens.ToDictionary(i => i.Codigo, _ => 1), Hoje);

        (await _avaliacoes.EvolucaoDoEscoreAsync(paciente, phq9.Codigo)).Aplicacoes.Should().Be(1);
        (await _avaliacoes.DoPacienteAsync(paciente)).Should().HaveCount(2, "a lista é o prontuário inteiro");
    }

    [Fact]
    public async Task Cancelar_avaliacao_tira_da_serie_e_guarda_a_linha()
    {
        var paciente = await CriarPacienteAsync();
        IInstrumentoAvaliacao gad7 = new InstrumentoGad7();

        var salva = await _avaliacoes.AplicarAsync(paciente, gad7.Codigo,
            gad7.Itens.ToDictionary(i => i.Codigo, _ => 1), Hoje);

        await _avaliacoes.CancelarAsync(salva.Id, "aplicada no paciente errado", "dra.ana");

        // Sai da série e do gráfico…
        (await _avaliacoes.DoPacienteAsync(paciente)).Should().BeEmpty();

        // …e continua guardada, com as respostas. É o escore que decidiu (ou não)
        // encaminhar alguém, e a Lei 13.787/2018 pede que ele sobreviva à opinião
        // posterior de quem quer apagá-lo.
        (await _db.RespostasAvaliacao.CountAsync()).Should().BeGreaterThan(0);
        (await _db.AvaliacoesClinicas.AsNoTracking().SingleAsync(a => a.Id == salva.Id))
            .MotivoCancelamento.Should().Be("aplicada no paciente errado");
        (await _db.Auditoria.ToListAsync())
            .Should().Contain(e => e.Acao == "AvaliacaoCancelada");
    }

    [Fact]
    public async Task Avaliacao_sobrevive_ao_cancelamento_da_sessao_a_que_pertencia()
    {
        // Antes da parcela 52 a sessão era APAGADA e o vínculo caía para null (SetNull).
        // Agora ela é cancelada, então o escore mantém a procedência — que é melhor: a
        // curva do tratamento continua inteira E continua sabendo de onde veio.
        var paciente = await CriarPacienteAsync();
        var evolucao = await EscreverAsync(paciente, Hoje);
        IInstrumentoAvaliacao gad7 = new InstrumentoGad7();

        await _avaliacoes.AplicarAsync(paciente, gad7.Codigo,
            gad7.Itens.ToDictionary(i => i.Codigo, _ => 1), Hoje, evolucaoId: evolucao.Id);

        await _prontuario.CancelarAsync(evolucao.Id, "sessão duplicada", "dra.ana");

        var restantes = await _avaliacoes.DoPacienteAsync(paciente);
        restantes.Should().HaveCount(1);
        restantes[0].EvolucaoId.Should().Be(evolucao.Id);
    }

    [Fact]
    public async Task Neurocirurgia_NAO_entra_no_catalogo_embutido()
    {
        // TRAVA CONTRA REGRESSÃO NO APP CONGELADO.
        //
        // A primeira versão da parcela 36 acrescentou Neurocirurgia ao enum
        // `Especialidade`, e isso vazava para o faturamento em produção: ele chama
        // `RecarregarCacheAsync()` na abertura, e `ListarAsync` GARANTE as embutidas
        // percorrendo `Enum.GetValues<Especialidade>()` — a especialidade nova apareceria
        // sozinha no seletor do lançamento de atendimento.
        //
        // Base vazia tem de devolver EXATAMENTE as seis de sempre. Se este teste falhar
        // com "sete", alguém pôs um valor no enum e mudou uma tela que fatura a clínica.
        var catalogo = new EspecialidadeCatalogoService(_repo);

        var embutidas = await catalogo.ListarAsync();

        embutidas.Should().HaveCount(6);
        embutidas.Should().NotContain(e => e.Codigo == InstrumentoOswestry.CodigoNeurocirurgia,
            "especialidade nova entra pelo CATÁLOGO, quando a clínica a cadastrar");
    }

    [Fact]
    public async Task Neurocirurgia_cadastrada_pela_clinica_passa_a_receber_o_oswestry()
    {
        // O outro lado da mesma decisão: o caminho existe e funciona — só depende de a
        // clínica cadastrar a especialidade, que é uma escolha dela e não do código.
        var catalogo = new EspecialidadeCatalogoService(_repo);

        await catalogo.SalvarAsync([new EspecialidadeCadastro
        {
            Codigo = InstrumentoOswestry.CodigoNeurocirurgia,
            Nome = "Neurocirurgia",
            Ativo = true
        }]);

        (await catalogo.ListarAsync()).Should()
            .Contain(e => e.Codigo == InstrumentoOswestry.CodigoNeurocirurgia);

        AvaliacaoClinicaService.Disponiveis(InstrumentoOswestry.CodigoNeurocirurgia)
            .Should().Contain(i => i.Codigo == InstrumentoOswestry.CodigoInstrumento);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
