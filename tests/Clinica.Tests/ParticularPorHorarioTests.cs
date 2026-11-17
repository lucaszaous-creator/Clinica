using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// PARTICULAR POR HORÁRIO (nov/2026) — o pedido das médicas, em vídeo: "essa paciente tem
/// plano, mas o atendimento dela por mim foi particular, porque eu não atendo o plano dela…
/// uma forma de já no agendamento sinalizar… para a gente ter um relatório de quantas
/// consultas particulares têm".
///
/// Até aqui particular era só o CONVÊNIO DA FICHA sem guia (parcela 60). A saída que sobrava
/// era trocar o convênio da ficha para "Particular" — e a acupuntura da mesma paciente, que
/// É pelo plano, parava de gerar guia. Nada falhava; a clínica só descobria no faturamento.
///
/// O que estes testes prendem é o que a marca do horário PROMETE, ponta a ponta: sem guia
/// (nem pendência de 2º código), ficha intacta, pergunta de dinheiro no Finalizar, linha na
/// conciliação dos particulares, contagem no relatório por médico — e que quem NÃO marca
/// continua exatamente como estava.
/// </summary>
public class ParticularPorHorarioTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ParametrosService _parametros;
    private readonly AtendimentoService _atendimentos;
    private readonly AgendaService _agenda;
    private readonly FinanceiroService _financeiro;
    private readonly PrecoParticularService _precos;
    private readonly FechamentoSessaoService _fechamento;
    private readonly int _medica;
    private int _marcados;

    private static readonly DateTime Sessao = new(2026, 8, 20, 9, 0, 0);
    private static DateOnly Dia => DateOnly.FromDateTime(Sessao);

    public ParticularPorHorarioTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        var medica = new Profissional { Nome = "Dra. Endócrino" };
        _db.Profissionais.Add(medica);
        _db.SaveChanges();
        _medica = medica.Id;
        _repo = new ClinicaRepositorio(_db);
        _parametros = new ParametrosService(_repo);
        _atendimentos = new AtendimentoService(
            _repo, parametros: _parametros, consultas: new ConsultaService(_repo));
        _agenda = new AgendaService(_repo, _atendimentos, _parametros);
        _financeiro = new FinanceiroService(_repo);
        _precos = new PrecoParticularService(_repo);
        _fechamento = new FechamentoSessaoService(
            _repo, _agenda, new PacoteService(_repo), new EstoqueService(_repo), _financeiro, _precos,
            new ContasService(_repo));

        // O catálogo é cache estático: cada teste monta o seu. A Unimed fatura; o
        // "Particular" é o cadastro sem guia; o "a definir" é a ficha importada sem convênio.
        CatalogoConvenios.Atualizar(
        [
            new EntradaConvenio("UnimedIntercambio", "Unimed Intercâmbio", Convenio.UnimedIntercambio, true, GeraGuia: true),
            new EntradaConvenio(ConvenioCadastro.CodigoParticular, "Particular", Convenio.Personalizado, true,
                new ConfiguracaoRegraGenerica { FazEletro = true, TemSegundoCodigo = true },
                FormatoNumeroGuia.SemValidacao, GeraGuia: false),
            new EntradaConvenio(ConvenioCadastro.CodigoADefinir, "A definir", Convenio.Personalizado, true,
                new ConfiguracaoRegraGenerica(), FormatoNumeroGuia.SemValidacao, GeraGuia: false)
        ]);
    }

    public void Dispose()
    {
        CatalogoConvenios.Atualizar([]);
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    // ==================== Cenário ====================

    private async Task<Paciente> ComUnimedAsync(string nome = "Mariane")
    {
        var p = new Paciente
        {
            Nome = nome, Convenio = Convenio.UnimedIntercambio, ConvenioCodigo = "UnimedIntercambio",
            Sexo = Sexo.Feminino, Categoria = Categoria.Verde
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p;
    }

    private Task<Agendamento> MarcarConsultaAsync(int pacienteId, bool particular, DateTime? quando = null)
        => _agenda.AgendarAsync(
            pacienteId, quando ?? Sessao.AddHours(_marcados++), ModalidadeAtendimento.Consulta, null,
            especialidadeConsultaCodigo: nameof(Especialidade.Endocrinologia),
            profissionalId: _medica, operador: "recepcao", particular: particular);

    private async Task<Atendimento> AtendimentoDoHorarioAsync(int agendamentoId)
    {
        _db.ChangeTracker.Clear();
        var ag = await _db.Agendamentos.AsNoTracking().SingleAsync(a => a.Id == agendamentoId);
        return await _db.Atendimentos.AsNoTracking().Include(a => a.Codigos)
            .SingleAsync(a => a.Id == ag.AtendimentoId);
    }

    // ==================== A regra, num lugar só ====================

    [Fact]
    public void A_pergunta_esta_sessao_e_particular_tem_duas_fontes_e_uma_resposta()
    {
        var comPlano = new Paciente { Convenio = Convenio.UnimedIntercambio, ConvenioCodigo = "UnimedIntercambio" };
        var deFicha = new Paciente { Convenio = Convenio.Personalizado, ConvenioCodigo = ConvenioCadastro.CodigoParticular };

        SessaoParticular.Eh(comPlano, marcadaComoParticular: false).Should().BeFalse();
        SessaoParticular.Eh(comPlano, marcadaComoParticular: true).Should().BeTrue("a marca do horário basta");
        SessaoParticular.Eh(deFicha, marcadaComoParticular: false).Should().BeTrue("a ficha particular continua valendo");

        // "Apesar do convênio" é só o caso que as telas precisam DIZER: ficha com plano + marca.
        SessaoParticular.ApesarDoConvenio(comPlano, true).Should().BeTrue();
        SessaoParticular.ApesarDoConvenio(deFicha, true).Should().BeFalse();
        SessaoParticular.Pagador(comPlano, true).Should().Be("Particular");
        SessaoParticular.Pagador(comPlano, false).Should().Be("Unimed Intercâmbio");
        SessaoParticular.PagadorComFicha(comPlano, true).Should().Be("Particular (ficha: Unimed Intercâmbio)");
    }

    // ==================== Marcar ====================

    [Fact]
    public async Task Horario_particular_de_quem_tem_plano_nao_gera_guia_e_nao_mexe_na_ficha()
    {
        var paciente = await ComUnimedAsync();

        var ag = await MarcarConsultaAsync(paciente.Id, particular: true);
        await _agenda.ConfirmarPresencaAsync(ag.Id, operador: "recepcao");

        var atendimento = await AtendimentoDoHorarioAsync(ag.Id);
        atendimento.Particular.Should().BeTrue("a marca do horário viaja para o atendimento");
        atendimento.Codigos.Should().NotBeEmpty("o registro da sessão fica — é o lastro dela")
            .And.OnlyContain(c => c.Status == StatusCodigo.NaoAplicavel, "particular não fatura");
        atendimento.Codigos.Should().NotContain(c => c.EstaPendente(Dia.AddDays(60)),
            "guia dispensada não pode virar pendência de 2º código");

        // A ficha é a mesma: o plano continua lá, e o semáforo do plano não foi reescrito
        // por uma sessão que não o usou.
        var ficha = await _db.Pacientes.AsNoTracking().SingleAsync(p => p.Id == paciente.Id);
        ficha.ConvenioCodigo.Should().Be("UnimedIntercambio");
        ficha.Categoria.Should().Be(Categoria.Verde);
    }

    [Fact]
    public async Task Quem_nao_marca_particular_continua_gerando_guia_como_sempre()
    {
        var paciente = await ComUnimedAsync();

        var ag = await MarcarConsultaAsync(paciente.Id, particular: false);
        await _agenda.ConfirmarPresencaAsync(ag.Id, operador: "recepcao");

        var atendimento = await AtendimentoDoHorarioAsync(ag.Id);
        atendimento.Particular.Should().BeFalse();
        atendimento.Codigos.Should().Contain(c => c.Status != StatusCodigo.NaoAplicavel,
            "o convênio da ficha continua faturando quando ninguém marcou particular");
    }

    [Fact]
    public async Task A_mesma_paciente_tem_consulta_particular_e_acupuntura_pelo_plano()
    {
        // O motivo de a marca ser do HORÁRIO e não da ficha: a endócrino não atende a Unimed,
        // a acupuntura atende — e é a mesma pessoa, na mesma semana.
        var paciente = await ComUnimedAsync();

        var consulta = await MarcarConsultaAsync(paciente.Id, particular: true);
        var acupuntura = await _agenda.AgendarAsync(
            paciente.Id, Sessao.AddDays(1), ModalidadeAtendimento.AcupunturaSimples, null,
            profissionalId: _medica, operador: "recepcao");
        await _agenda.ConfirmarPresencaAsync(consulta.Id, operador: "recepcao");
        await _agenda.ConfirmarPresencaAsync(acupuntura.Id, operador: "recepcao");

        (await AtendimentoDoHorarioAsync(consulta.Id)).Codigos
            .Should().OnlyContain(c => c.Status == StatusCodigo.NaoAplicavel);
        (await AtendimentoDoHorarioAsync(acupuntura.Id)).Codigos
            .Should().Contain(c => c.Status == StatusCodigo.Aberto, "a sessão pelo plano não foi contaminada");
    }

    [Fact]
    public async Task Ficha_sem_convenio_pode_ser_atendida_quando_o_horario_e_particular()
    {
        // A recusa da parcela 92 impede a sessão sem guia CALADA. Aqui a falta de guia é a
        // escolha registrada no horário — e o dinheiro é perguntado no Finalizar.
        var p = new Paciente
        {
            Nome = "Importada", Convenio = Convenio.Personalizado,
            ConvenioCodigo = ConvenioCadastro.CodigoADefinir, Sexo = Sexo.Feminino
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();

        var semMarca = await MarcarConsultaAsync(p.Id, particular: false);
        var recusa = async () => await _agenda.ConfirmarPresencaAsync(semMarca.Id, operador: "recepcao");
        await recusa.Should().ThrowAsync<ConvenioNaoDefinidoException>("sem a marca, a recusa continua valendo");

        _db.ChangeTracker.Clear();
        var marcado = await MarcarConsultaAsync(p.Id, particular: true);
        var lancado = await _agenda.ConfirmarPresencaAsync(marcado.Id, operador: "recepcao");
        lancado.Atendimento.Particular.Should().BeTrue();
    }

    // ==================== O dinheiro ====================

    [Fact]
    public async Task O_Finalizar_pergunta_do_dinheiro_e_propoe_o_preco_da_tabela_do_particular()
    {
        var paciente = await ComUnimedAsync();
        await _precos.SalvarAsync(new PrecoParticular
        {
            ModalidadeCodigo = nameof(ModalidadeAtendimento.Consulta),
            EspecialidadeCodigo = nameof(Especialidade.Endocrinologia),
            Valor = 450m
        });

        var particular = await MarcarConsultaAsync(paciente.Id, particular: true);
        var peloPlano = await MarcarConsultaAsync(paciente.Id, particular: false, quando: Sessao.AddDays(2));

        var doParticular = await _fechamento.RegistrarAtendimentoAsync(particular.Id, "recepcao");
        doParticular.Proposta.EhParticular.Should().BeTrue();
        doParticular.Proposta.SugereLancamento.Should().BeTrue("o caixa é o único dinheiro desta sessão");
        doParticular.Proposta.ValorSugerido.Should().Be(450m);
        doParticular.Proposta.ProcedenciaDoValor.Should().StartWith("tabela do particular:");
        doParticular.GuiasGeradas.Should().Be(0);

        // A consulta da MESMA paciente pelo plano não ganha pergunta de dinheiro.
        var doPlano = await _fechamento.RegistrarAtendimentoAsync(peloPlano.Id, "recepcao");
        doPlano.Proposta.EhParticular.Should().BeFalse();
        doPlano.Proposta.SugereLancamento.Should().BeFalse();
    }

    [Fact]
    public async Task Sessao_particular_sem_receita_aparece_na_conciliacao_e_a_receita_nao_leva_o_plano()
    {
        var paciente = await ComUnimedAsync();
        var ag = await MarcarConsultaAsync(paciente.Id, particular: true);
        await _agenda.ConfirmarPresencaAsync(ag.Id, operador: "recepcao");

        var pendente = (await _financeiro.SessoesParticularesSemReceitaAsync(Dia, Dia)).Single();
        pendente.ParticularDaSessao.Should().BeTrue();
        pendente.ConvenioNome.Should().Be("Particular (ficha: Unimed Intercâmbio)",
            "a aba Particulares mostrando só \"Unimed\" pareceria engano");

        var receita = await _financeiro.LancarReceitaDaSessaoAsync(
            pendente, 450m, FormaPagamento.Pix, operador: "recepcao");
        receita.AtendimentoId.Should().Be(pendente.AtendimentoId, "é o vínculo que tira a linha da conciliação");
        receita.Convenio.Should().BeNull("quem pagou foi a paciente, não a Unimed");
        receita.ConvenioCodigo.Should().BeNull();

        (await _financeiro.SessoesParticularesSemReceitaAsync(Dia, Dia)).Should().BeEmpty();
    }

    [Fact]
    public async Task Consulta_particular_nao_renova_a_consulta_do_convenio()
    {
        // A validade (22 dias na Unimed) é do PLANO. Renovar por uma consulta que a médica
        // não atende pelo plano apagaria da pendência uma consulta que continua vencendo.
        var paciente = await ComUnimedAsync();

        var particular = await MarcarConsultaAsync(paciente.Id, particular: true);
        var avisos = (await _agenda.ConfirmarPresencaAsync(particular.Id, operador: "recepcao")).Avisos;
        avisos.Should().NotContain(a => a.Contains("Renovação registrada"));
        (await _db.Consultas.AsNoTracking().CountAsync(c => c.PacienteId == paciente.Id)).Should().Be(0);

        var peloPlano = await MarcarConsultaAsync(paciente.Id, particular: false, quando: Sessao.AddDays(3));
        await _agenda.ConfirmarPresencaAsync(peloPlano.Id, operador: "recepcao");
        (await _db.Consultas.AsNoTracking().CountAsync(c => c.PacienteId == paciente.Id)).Should().Be(1);
    }

    // ==================== Guia no agendamento, remarcar e corrigir depois ====================

    [Fact]
    public async Task Com_a_guia_nascendo_na_marcacao_o_horario_particular_ja_nasce_sem_guia_a_faturar()
    {
        await _parametros.DefinirGuiaNoAgendamentoAsync(true);
        var paciente = await ComUnimedAsync();

        var ag = await MarcarConsultaAsync(paciente.Id, particular: true, quando: DateTime.Today.AddDays(7).AddHours(9));

        var atendimento = await AtendimentoDoHorarioAsync(ag.Id);
        atendimento.Particular.Should().BeTrue();
        atendimento.Codigos.Should().OnlyContain(c => c.Status == StatusCodigo.NaoAplicavel);
    }

    [Fact]
    public async Task Editar_o_horario_para_particular_dispensa_as_guias_e_a_volta_as_devolve()
    {
        await _parametros.DefinirGuiaNoAgendamentoAsync(true);
        var paciente = await ComUnimedAsync();
        var quando = DateTime.Today.AddDays(7).AddHours(9);
        var ag = await MarcarConsultaAsync(paciente.Id, particular: false, quando: quando);
        (await AtendimentoDoHorarioAsync(ag.Id)).Codigos.Should().Contain(c => c.Status == StatusCodigo.Aberto);

        var avisos = new List<string>();
        await _agenda.RemarcarAsync(ag.Id, quando, null, operador: "recepcao",
            manterRecursos: true, avisosGuia: avisos, particular: true);

        var depois = await AtendimentoDoHorarioAsync(ag.Id);
        depois.Particular.Should().BeTrue();
        depois.Codigos.Should().OnlyContain(c => c.Status == StatusCodigo.NaoAplicavel,
            "as guias abertas do convênio são dispensadas — nada é apagado");
        avisos.Should().ContainSingle().Which.Should().Contain("PARTICULAR");

        // Quem remarca SEM saber da marca (o Empurrar, a grade, o faturamento) a preserva.
        _db.ChangeTracker.Clear();
        await _agenda.RemarcarAsync(ag.Id, quando.AddDays(1), null, operador: "recepcao");
        (await _db.Agendamentos.AsNoTracking().SingleAsync(a => a.Id == ag.Id)).Particular.Should().BeTrue(
            "nulo é \"não sei\", nunca \"desligue\"");

        // E a volta ao convênio gera as guias de novo.
        _db.ChangeTracker.Clear();
        await _agenda.RemarcarAsync(ag.Id, quando.AddDays(1), null, operador: "recepcao", particular: false);
        var deVolta = await AtendimentoDoHorarioAsync(ag.Id);
        deVolta.Particular.Should().BeFalse();
        deVolta.Codigos.Should().Contain(c => c.Status == StatusCodigo.Aberto);
    }

    [Fact]
    public async Task Consulta_ja_atendida_pelo_plano_pode_ser_corrigida_para_particular_sem_estorno()
    {
        // O caso do vídeo: a consulta JÁ aconteceu e ficou registrada pela Unimed.
        var paciente = await ComUnimedAsync();
        var ag = await MarcarConsultaAsync(paciente.Id, particular: false);
        await _agenda.ConfirmarPresencaAsync(ag.Id, operador: "recepcao");
        var antes = await AtendimentoDoHorarioAsync(ag.Id);
        antes.Codigos.Should().Contain(c => c.Status == StatusCodigo.Aberto);

        _db.ChangeTracker.Clear();
        var avisos = await _agenda.DefinirParticularAsync(ag.Id, particular: true, operador: "recepcao");

        var depois = await AtendimentoDoHorarioAsync(ag.Id);
        depois.Id.Should().Be(antes.Id, "é a MESMA sessão — nada foi estornado nem recriado");
        depois.RealizadoEm.Should().NotBeNull();
        depois.Particular.Should().BeTrue();
        depois.Codigos.Should().OnlyContain(c => c.Status == StatusCodigo.NaoAplicavel);
        depois.Codigos.Count.Should().BeGreaterThan(antes.Codigos.Count, "as guias antigas ficam, dispensadas");
        avisos.Should().Contain(a => a.Contains("PARTICULAR"));
        avisos.Should().Contain(a => a.Contains("aba Consultas"), "a renovação da consulta do plano não é desfeita sozinha");

        // A trilha, no mesmo ato.
        (await _db.Auditoria.AsNoTracking().AnyAsync(e => e.Acao == "SessaoMarcadaParticular"
                                                          && e.PacienteId == paciente.Id)).Should().BeTrue();
        // E a sessão passa a ser cobrada: entra na conciliação dos particulares.
        (await _financeiro.SessoesParticularesSemReceitaAsync(Dia, Dia)).Should().ContainSingle();
    }

    [Fact]
    public async Task Guia_ja_baixada_impede_a_troca_para_particular()
    {
        var paciente = await ComUnimedAsync();
        var ag = await MarcarConsultaAsync(paciente.Id, particular: false);
        await _agenda.ConfirmarPresencaAsync(ag.Id, operador: "recepcao");
        var guia = await _db.Codigos.FirstAsync(c => c.Atendimento!.PacienteId == paciente.Id
                                                     && c.Status == StatusCodigo.Aberto);
        guia.Status = StatusCodigo.Baixado;
        guia.DataBaixa = Dia;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var troca = async () => await _agenda.DefinirParticularAsync(ag.Id, true, "recepcao");

        (await troca.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("baixada");
        _db.ChangeTracker.Clear();
        (await _db.Agendamentos.AsNoTracking().SingleAsync(a => a.Id == ag.Id)).Particular.Should().BeFalse(
            "a recusa não pode deixar a marca gravada pela metade");
    }

    // ==================== O relatório que a médica pediu ====================

    [Fact]
    public async Task O_relatorio_por_medico_conta_as_consultas_particulares()
    {
        var comPlano = await ComUnimedAsync("Mariane");
        var outra = await ComUnimedAsync("Patrícia");
        var a = await MarcarConsultaAsync(comPlano.Id, particular: true);
        var b = await MarcarConsultaAsync(outra.Id, particular: false);
        await _agenda.ConfirmarPresencaAsync(a.Id, operador: "recepcao");
        await _agenda.ConfirmarPresencaAsync(b.Id, operador: "recepcao");

        var relatorio = await new AtendimentosPorProfissionalService(_repo, new PrecoConvenioService(_repo))
            .GerarAsync(Dia, Dia);

        var medica = relatorio.Profissionais.Single();
        medica.Quantidade.Should().Be(2);
        medica.Particulares.Should().Be(1);
        relatorio.Particulares.Should().Be(1);
        var linha = medica.Atendimentos.Single(l => l.Paciente == "Mariane");
        linha.Particular.Should().BeTrue();
        linha.Convenio.Should().Be("Particular");
        medica.Atendimentos.Single(l => l.Paciente == "Patrícia").Convenio.Should().Be("Unimed Intercâmbio");
    }

    // ==================== O que as telas afirmam ====================

    [Fact]
    public void A_escolha_so_aparece_quando_ha_o_que_escolher()
    {
        var comPlano = new Paciente { Convenio = Convenio.UnimedIntercambio, ConvenioCodigo = "UnimedIntercambio" };
        var deFicha = new Paciente { Convenio = Convenio.Personalizado, ConvenioCodigo = ConvenioCadastro.CodigoParticular };
        var aDefinir = new Paciente { Convenio = Convenio.Personalizado, ConvenioCodigo = ConvenioCadastro.CodigoADefinir };

        FormaDeAtendimentoNaTela.OfereceEscolha(comPlano).Should().BeTrue();
        FormaDeAtendimentoNaTela.OfereceEscolha(aDefinir).Should().BeTrue("marcar particular é a saída de quem ainda não tem convênio");
        FormaDeAtendimentoNaTela.OfereceEscolha(deFicha).Should().BeFalse("ficha particular: pergunta com uma resposta só");
        FormaDeAtendimentoNaTela.OfereceEscolha(null).Should().BeFalse();

        FormaDeAtendimentoNaTela.RotuloConvenio(comPlano).Should().Be("Unimed Intercâmbio");
        FormaDeAtendimentoNaTela.Explicacao(comPlano, particular: true)
            .Should().Contain("Unimed Intercâmbio").And.Contain("não muda");
        FormaDeAtendimentoNaTela.Explicacao(comPlano, particular: false).Should().BeEmpty();
    }

    [Fact]
    public void Sessao_particular_cala_os_avisos_do_plano_e_mantem_os_do_paciente()
    {
        AlertaElegibilidade[] alertas =
        [
            new(ImpedimentoElegibilidade.CarteirinhaVencida, NivelUrgencia.Vermelho, "Carteirinha vencida"),
            new(ImpedimentoElegibilidade.CotaEsgotada, NivelUrgencia.Vermelho, "Cota esgotada"),
            new(ImpedimentoElegibilidade.PacienteEmDebito, NivelUrgencia.Vermelho, "Deve R$ 200,00")
        ];

        FormaDeAtendimentoNaTela.AlertasDaSessao(alertas, particular: false).Should().HaveCount(3);
        FormaDeAtendimentoNaTela.AlertasDaSessao(alertas, particular: true)
            .Should().ContainSingle().Which.Motivo.Should().Be(ImpedimentoElegibilidade.PacienteEmDebito,
                "dívida é do paciente, não do plano — e é justamente no particular que ela importa");
    }

    [Fact]
    public async Task O_consultorio_avisa_que_o_atendimento_aberto_e_particular()
    {
        var paciente = await ComUnimedAsync();
        var particular = await MarcarConsultaAsync(paciente.Id, particular: true);
        var peloPlano = await MarcarConsultaAsync(paciente.Id, particular: false, quando: Sessao.AddDays(1));

        TelaDoPacienteNaTela.SeloDeSessaoParticular(await _repo.ObterAgendamentoAsync(particular.Id))
            .Should().Contain("PARTICULAR");
        TelaDoPacienteNaTela.SeloDeSessaoParticular(await _repo.ObterAgendamentoAsync(peloPlano.Id)).Should().BeNull();
        TelaDoPacienteNaTela.SeloDeSessaoParticular(null).Should().BeNull();
    }
}
