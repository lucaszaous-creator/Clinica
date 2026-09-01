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
/// As duas telas planas do handoff (set/2026) — Prontuários e Exames — e o vínculo
/// resultado→pedido que sustenta a situação da segunda.
///
/// As consultas novas são EXECUTADAS aqui (método de repositório sem chamador em teste é
/// código que ninguém rodou — parcela 74), e o que a tela AFIRMA (situações, ordem,
/// bandeiras) é fixado nos montadores puros da Application.
/// </summary>
public class ProntuariosEExamesTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;

    public ProntuariosEExamesTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
    }

    private async Task<int> PacienteAsync(string nome = "Paciente")
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<int> PedidoAsync(
        int pacienteId, DateOnly data, bool cancelado = false, params string[] itens)
    {
        var d = new DocumentoClinico
        {
            Numero = $"2026/{Random.Shared.Next(1000, 9999)}",
            CodigoVerificacao = Guid.NewGuid().ToString("N")[..8],
            Tipo = TipoDocumentoClinico.PedidoExame,
            PacienteId = pacienteId,
            Data = data,
            CanceladoEm = cancelado ? DateTime.Now : null,
            MotivoCancelamento = cancelado ? "engano" : null
        };
        var ordem = 1;
        foreach (var i in itens)
            d.Itens.Add(new ItemDocumento { Ordem = ordem++, Descricao = i });
        _db.DocumentosClinicos.Add(d);
        await _db.SaveChangesAsync();
        return d.Id;
    }

    // ============================================================
    // Exames — a situação é DERIVADA do vínculo
    // ============================================================

    [Fact]
    public async Task Pedidos_derivam_a_situacao_dos_resultados_VINCULADOS()
    {
        var paciente = await PacienteAsync("Maria da Silva");
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        var aguardando = await PedidoAsync(paciente, hoje.AddDays(-5),
            itens: ["Ressonância — coluna lombar", "Raio-X — joelho"]);
        var respondido = await PedidoAsync(paciente, hoje.AddDays(-3),
            itens: ["Hemograma completo"]);
        var cancelado = await PedidoAsync(paciente, hoje.AddDays(-1),
            cancelado: true, itens: ["Ultrassom"]);

        // Receita NÃO entra — a tela é de pedidos de exame.
        _db.DocumentosClinicos.Add(new DocumentoClinico
        {
            Numero = "2026/9999",
            CodigoVerificacao = "abc12345",
            Tipo = TipoDocumentoClinico.Receita,
            PacienteId = paciente,
            Data = hoje
        });
        await _db.SaveChangesAsync();

        var servico = new ResultadoExameService(_repo);
        await servico.RegistrarAsync(new ResultadoExame
        {
            PacienteId = paciente,
            PedidoDocumentoId = respondido,
            Data = hoje,
            Nome = "Hemograma",
            Valor = "normal"
        });
        // Resultado CANCELADO não dá baixa em pedido nenhum.
        var desdito = await servico.RegistrarAsync(new ResultadoExame
        {
            PacienteId = paciente,
            PedidoDocumentoId = aguardando,
            Data = hoje,
            Nome = "Raio-X",
            Valor = "laudo trocado"
        });
        await servico.CancelarAsync(desdito.Id, "laudo de outra pessoa");

        var linhas = await _repo.PedidosDeExameAsync();

        linhas.Should().HaveCount(3, "a receita fica de fora");
        var l1 = linhas.Single(l => l.DocumentoId == aguardando);
        l1.Situacao.Should().Be(SituacaoPedidoExame.AguardandoResultado,
            "o único resultado amarrado a ele foi CANCELADO");
        l1.ExameRotulo.Should().Be("Ressonância — coluna lombar e mais 1");
        l1.MostraRegistrar.Should().BeTrue();

        var l2 = linhas.Single(l => l.DocumentoId == respondido);
        l2.Situacao.Should().Be(SituacaoPedidoExame.ResultadoDisponivel);
        l2.ResultadosVinculados.Should().Be(1);
        l2.MostraVerResultados.Should().BeTrue();
        l2.MostraRegistrar.Should().BeFalse();

        var l3 = linhas.Single(l => l.DocumentoId == cancelado);
        l3.Situacao.Should().Be(SituacaoPedidoExame.Cancelado,
            "pedido numerado nunca some — aparece marcado");
    }

    [Fact]
    public async Task O_combo_de_vinculo_lista_por_PACIENTE()
    {
        var maria = await PacienteAsync("Maria");
        var joao = await PacienteAsync("João");
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        await PedidoAsync(maria, hoje, itens: ["Hemograma"]);
        await PedidoAsync(joao, hoje, itens: ["Ultrassom"]);

        var doJoao = await _repo.PedidosDeExameAsync(pacienteId: joao);

        doJoao.Should().HaveCount(1);
        doJoao[0].Paciente.Should().Be("João");
        doJoao[0].RotuloDoCombo.Should().Contain("Ultrassom");
    }

    [Fact]
    public async Task Registrar_recusa_o_vinculo_com_pedido_de_OUTRO_paciente()
    {
        var maria = await PacienteAsync("Maria");
        var joao = await PacienteAsync("João");
        var pedidoDoJoao = await PedidoAsync(joao, DateOnly.FromDateTime(DateTime.Today),
            itens: ["Hemograma"]);

        var servico = new ResultadoExameService(_repo);
        var acao = () => servico.RegistrarAsync(new ResultadoExame
        {
            PacienteId = maria,
            PedidoDocumentoId = pedidoDoJoao,
            Data = DateOnly.FromDateTime(DateTime.Today),
            Nome = "Hemograma",
            Valor = "normal"
        });

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*OUTRO paciente*");
    }

    [Fact]
    public async Task Registrar_recusa_o_vinculo_com_documento_que_NAO_e_pedido()
    {
        var paciente = await PacienteAsync();
        var receita = new DocumentoClinico
        {
            Numero = "2026/0100",
            CodigoVerificacao = "xyz98765",
            Tipo = TipoDocumentoClinico.Receita,
            PacienteId = paciente,
            Data = DateOnly.FromDateTime(DateTime.Today)
        };
        _db.DocumentosClinicos.Add(receita);
        await _db.SaveChangesAsync();

        var servico = new ResultadoExameService(_repo);
        var acao = () => servico.RegistrarAsync(new ResultadoExame
        {
            PacienteId = paciente,
            PedidoDocumentoId = receita.Id,
            Data = DateOnly.FromDateTime(DateTime.Today),
            Nome = "Hemograma",
            Valor = "normal"
        });

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não é um pedido de exame*");
    }

    // ============================================================
    // Prontuários — a projeção das evoluções
    // ============================================================

    [Fact]
    public async Task Evolucoes_projetam_sem_cancelada_e_com_a_modalidade_do_horario()
    {
        var paciente = await PacienteAsync("Carlos Nunes");
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        var ag = new Agendamento
        {
            PacienteId = paciente,
            DataHora = hoje.AddDays(-2).ToDateTime(new TimeOnly(9, 0)),
            ModalidadePrevista = ModalidadeAtendimento.Consulta,
            EspecialidadeConsulta = Especialidade.Psiquiatria
        };
        _db.Agendamentos.Add(ag);
        await _db.SaveChangesAsync();

        _db.Evolucoes.Add(new Evolucao
        {
            PacienteId = paciente, Data = hoje.AddDays(-2), AgendamentoId = ag.Id,
            TextoEvolucao = "sessão com horário"
        });
        _db.Evolucoes.Add(new Evolucao
        {
            PacienteId = paciente, Data = hoje.AddDays(-1),
            TextoEvolucao = "avulsa, sem horário"
        });
        _db.Evolucoes.Add(new Evolucao
        {
            PacienteId = paciente, Data = hoje,
            TextoEvolucao = "desdita",
            CanceladaEm = DateTime.Now, MotivoCancelamento = "paciente errado"
        });
        await _db.SaveChangesAsync();

        var linhas = await _repo.EvolucoesParaProntuariosAsync(hoje.AddDays(-30), hoje);

        linhas.Should().HaveCount(2, "a cancelada é registro desdito — fica no histórico, não na lista");
        var comHorario = linhas.Single(l => l.AgendamentoId == ag.Id);
        comHorario.Paciente.Should().Be("Carlos Nunes");
        comHorario.Modalidade.Should().Be(ModalidadeAtendimento.Consulta);
        comHorario.EspecialidadeConsulta.Should().Be(Especialidade.Psiquiatria);

        ListaDeProntuarios.DetalheDaSessao(comHorario).Should().Contain("Psiquiatria",
            "a Consulta mostra a especialidade — o caminho de baixo da consulta de guias");

        var avulsa = linhas.Single(l => l.AgendamentoId is null);
        ListaDeProntuarios.DetalheDaSessao(avulsa).Should().Be("—",
            "evolução sem horário não afirma modalidade nenhuma");
    }

    [Fact]
    public async Task O_filtro_de_profissional_NAO_esconde_a_evolucao_sem_profissional()
    {
        var paciente = await PacienteAsync();
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        var ana = new Profissional { Nome = "Ana" };
        var breno = new Profissional { Nome = "Breno" };
        _db.Profissionais.AddRange(ana, breno);
        await _db.SaveChangesAsync();

        _db.Evolucoes.Add(new Evolucao
            { PacienteId = paciente, Data = hoje, ProfissionalId = ana.Id, TextoEvolucao = "da Ana" });
        _db.Evolucoes.Add(new Evolucao
            { PacienteId = paciente, Data = hoje, ProfissionalId = breno.Id, TextoEvolucao = "do Breno" });
        _db.Evolucoes.Add(new Evolucao
            { PacienteId = paciente, Data = hoje, TextoEvolucao = "sem equipe cadastrada" });
        await _db.SaveChangesAsync();

        var daAna = await _repo.EvolucoesParaProntuariosAsync(hoje, hoje, ana.Id);

        daAna.Should().HaveCount(2,
            "a evolução sem profissional entra — escondê-la faria a lista cobrar de novo um registro que existe");
        daAna.Should().NotContain(l => l.Profissional == "Breno");
    }

    // ============================================================
    // O montador puro — o que a tela AFIRMA
    // ============================================================

    private static LinhaEvolucaoProntuarios Linha(int id, DateOnly data, int versoes = 0)
        => new(id, 1, "Maria", data, null, null, null, null, versoes, null);

    private static DocumentoClinico Anamnese(
        int id, DateOnly data, bool assinada = false, bool cancelada = false) => new()
    {
        Id = id,
        Numero = $"2026/{id:0000}",
        Tipo = TipoDocumentoClinico.Anamnese,
        PacienteId = 1,
        Paciente = new Paciente { Nome = "Maria" },
        Data = data,
        AssinaturaTipo = assinada ? TipoAssinatura.IcpBrasil : null,
        CanceladoEm = cancelada ? DateTime.Now : null
    };

    [Fact]
    public void Montar_da_a_situacao_HONESTA_a_cada_natureza()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var pendente = new RegistroPendente(
            10, hoje.AddDays(-1).ToDateTime(new TimeOnly(9, 0)), 1, "Maria", "Acupuntura", null);

        var linhas = ListaDeProntuarios.Montar(
            [pendente],
            [Linha(1, hoje.AddDays(-3)), Linha(2, hoje.AddDays(-2), versoes: 1)],
            [Anamnese(3, hoje.AddDays(-4)), Anamnese(4, hoje.AddDays(-5), assinada: true),
             Anamnese(5, hoje.AddDays(-6), cancelada: true)]);

        linhas.Should().HaveCount(6);

        linhas.Single(l => l.AgendamentoId == 10).Situacao
            .Should().Be(SituacaoLinhaProntuario.AEscrever,
                "o pendente REAL da evolução é a sessão sem registro — nunca uma assinatura inventada");
        linhas.Single(l => l.EvolucaoId == 1).Situacao.Should().Be(SituacaoLinhaProntuario.Registrada);
        linhas.Single(l => l.EvolucaoId == 2).Situacao.Should().Be(SituacaoLinhaProntuario.Corrigida);
        linhas.Single(l => l.DocumentoId == 3).Situacao.Should().Be(SituacaoLinhaProntuario.AAssinar);
        linhas.Single(l => l.DocumentoId == 4).Situacao.Should().Be(SituacaoLinhaProntuario.Assinada);
        linhas.Single(l => l.DocumentoId == 5).Situacao.Should().Be(SituacaoLinhaProntuario.Cancelada,
            "anamnese cancelada aparece MARCADA, nunca sumindo");

        // Evolução NUNCA ganha o botão de assinar — não há assinatura de evolução no domínio.
        linhas.Where(l => l.Natureza != NaturezaLinhaProntuario.Anamnese)
            .Should().OnlyContain(l => !l.MostraAssinar);
    }

    [Fact]
    public void Montar_poe_quem_pede_ACAO_primeiro_e_o_resto_por_data()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var pendente = new RegistroPendente(
            10, hoje.AddDays(-9).ToDateTime(new TimeOnly(9, 0)), 1, "Maria", "Acupuntura", null);

        var linhas = ListaDeProntuarios.Montar(
            [pendente],
            [Linha(1, hoje)],
            [Anamnese(3, hoje.AddDays(-2))]);

        // A sessão sem registro é de 9 dias atrás e MESMO ASSIM vem antes da evolução de
        // hoje: pendência escondida embaixo da lista é pendência que ninguém resolve.
        linhas[0].PedeAcao.Should().BeTrue();
        linhas[1].PedeAcao.Should().BeTrue();
        linhas[2].Situacao.Should().Be(SituacaoLinhaProntuario.Registrada);

        // Ids são POR TABELA: a linha de evolução e a de anamnese carregam ids em campos
        // SEPARADOS — a lição da parcela 71.
        linhas.Single(l => l.DocumentoId == 3).EvolucaoId.Should().BeNull();
        linhas.Single(l => l.EvolucaoId == 1).DocumentoId.Should().BeNull();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
