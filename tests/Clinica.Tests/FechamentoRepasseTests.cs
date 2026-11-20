using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Repasse por GUIA RECEBIDA (nov/2026). A regra da clínica: o repasse só conta depois de a
/// guia ser CONCILIADA — receita de entrada REALIZADA e viva ligada à guia. A competência é a
/// do PAGAMENTO, a regra é a da DATA DO ATENDIMENTO, e o banco trava pagar duas vezes.
/// </summary>
public class FechamentoRepasseTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly DbContextOptions<ClinicaDbContext> _options;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly FinanceiroService _financeiro;
    private readonly RepasseService _regras;
    private readonly FechamentoRepasseService _servico;

    private static readonly DateOnly Setembro = new(2026, 9, 1);
    private static readonly DateOnly Outubro = new(2026, 10, 1);

    public FechamentoRepasseTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(_options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _financeiro = new FinanceiroService(_repo);
        _regras = new RepasseService(_repo);
        _servico = new FechamentoRepasseService(_repo);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    // ==================== Montagem ====================

    private async Task<int> ProfissionalAsync(string nome = "Dra. Ana Souza")
    {
        var p = new Profissional { Nome = nome, RegistroConselho = "CRM-RJ 123456" };
        _db.Profissionais.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<Paciente> PacienteAsync(string nome, Convenio familia, string? codigo = null)
    {
        var p = new Paciente { Nome = nome, Convenio = familia, ConvenioCodigo = codigo, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p;
    }

    /// <summary>Sessão de convênio realizada, com UMA guia baixada e o horário de quem atendeu.</summary>
    private async Task<CodigoFaturamento> GuiaAsync(
        Paciente paciente, int? profissionalId, DateOnly atendidoEm, TipoCodigo tipo = TipoCodigo.Consulta,
        string? numeroGuia = null, bool horarioSubstituido = false)
    {
        var atendimento = new Atendimento
        {
            PacienteId = paciente.Id, Data = atendidoEm, Modalidade = ModalidadeAtendimento.Consulta,
            RealizadoEm = atendidoEm.ToDateTime(new TimeOnly(10, 0))
        };
        _db.Atendimentos.Add(atendimento);
        await _db.SaveChangesAsync();

        var codigo = new CodigoFaturamento
        {
            AtendimentoId = atendimento.Id, Tipo = tipo, Ordem = OrdemCodigo.Primeiro,
            DataPrevistaFaturamento = atendidoEm, Status = StatusCodigo.Baixado, DataBaixa = atendidoEm,
            NumeroGuiaReal = numeroGuia ?? $"G{atendimento.Id:0000}"
        };
        _db.Codigos.Add(codigo);
        _db.Agendamentos.Add(new Agendamento
        {
            PacienteId = paciente.Id,
            ProfissionalId = horarioSubstituido ? null : profissionalId,
            AtendimentoId = atendimento.Id,
            DataHora = atendidoEm.ToDateTime(new TimeOnly(10, 0)),
            Status = StatusAgendamento.Realizado
        });
        if (horarioSubstituido)
            _db.Agendamentos.Add(new Agendamento
            {
                PacienteId = paciente.Id, ProfissionalId = profissionalId, AtendimentoSubstitutoId = atendimento.Id,
                DataHora = atendidoEm.ToDateTime(new TimeOnly(9, 0)), Status = StatusAgendamento.Substituido
            });
        await _db.SaveChangesAsync();
        codigo.Atendimento = atendimento;
        return codigo;
    }

    /// <summary>
    /// O fluxo real da Conciliação: a receita nasce PREVISTA e é realizada quando o dinheiro
    /// cai, na data em que caiu. <paramref name="pagoEm"/> nulo = fica prevista.
    /// </summary>
    private async Task<LancamentoFinanceiro> ReceberAsync(
        CodigoFaturamento codigo, Paciente paciente, decimal valor, DateOnly? pagoEm, string? referencia = null)
    {
        var guia = new GuiaSemLancamento(codigo.Id, codigo.AtendimentoId, paciente.Id, paciente.Nome,
            codigo.DataBaixa!.Value, codigo.NumeroGuiaReal, paciente.Convenio, codigo.Tipo)
        { ConvenioCodigo = paciente.ConvenioCodigo ?? paciente.Convenio.ToString() };
        var lancamento = await _financeiro.LancarReceitaDaGuiaAsync(
            guia, valor, StatusLancamento.Previsto, operador: "financeiro", referenciaPagamento: referencia);
        if (pagoEm is { } dia)
            await _financeiro.RealizarAsync(lancamento.Id, dia, FormaPagamento.Convenio, "financeiro");
        return lancamento;
    }

    /// <summary>Sessão particular (todos os códigos NaoAplicavel) com o dinheiro que entrou.</summary>
    private async Task<int> ParticularAsync(Paciente paciente, int profissionalId, DateOnly dia, params decimal[] pagamentos)
    {
        var atendimento = new Atendimento
        {
            PacienteId = paciente.Id, Data = dia, Modalidade = ModalidadeAtendimento.Consulta, Particular = true,
            RealizadoEm = dia.ToDateTime(new TimeOnly(15, 0))
        };
        _db.Atendimentos.Add(atendimento);
        await _db.SaveChangesAsync();
        _db.Codigos.Add(new CodigoFaturamento
        {
            AtendimentoId = atendimento.Id, Tipo = TipoCodigo.Consulta, Ordem = OrdemCodigo.Primeiro,
            DataPrevistaFaturamento = dia, Status = StatusCodigo.NaoAplicavel
        });
        _db.Agendamentos.Add(new Agendamento
        {
            PacienteId = paciente.Id, ProfissionalId = profissionalId, AtendimentoId = atendimento.Id,
            DataHora = dia.ToDateTime(new TimeOnly(15, 0)), Status = StatusAgendamento.Realizado, Particular = true
        });
        await _db.SaveChangesAsync();
        foreach (var valor in pagamentos)
            await _financeiro.LancarAsync(dia, TipoLancamento.Entrada, "sessão particular", valor,
                StatusLancamento.Realizado, FormaPagamento.Pix, atendimentoId: atendimento.Id, pacienteId: paciente.Id);
        return atendimento.Id;
    }

    private Task<RegraRepasse> RegraAsync(
        int profissionalId, decimal valor, string? convenio = null, TipoCodigo? tipo = null,
        DateOnly? de = null, DateOnly? ate = null)
        => _regras.SalvarRegraAsync(new RegraRepasse
        {
            ProfissionalId = profissionalId, Base = BaseRepasse.ValorPorAtendimento, ValorPorAtendimento = valor,
            ConvenioCodigo = convenio, TipoGuia = tipo, VigenteDe = de, VigenteAte = ate, Ativa = true
        }, "gerente");

    private async Task<PreviaRepasseProfissional> PreviaDeAsync(int profissionalId, DateOnly competencia)
        => (await _servico.PreviaAsync(competencia)).Single(p => p.ProfissionalId == profissionalId);

    // ==================== Só o que o convênio PAGOU ====================

    [Fact]
    public async Task So_a_receita_realizada_e_viva_entra_prevista_e_cancelada_aguardam_o_convenio()
    {
        var medica = await ProfissionalAsync();
        await RegraAsync(medica, 65m, "Amil");
        var p = await PacienteAsync("Beatriz", Convenio.Amil, "Amil");

        var paga = await GuiaAsync(p, medica, new DateOnly(2026, 9, 2));
        var prevista = await GuiaAsync(p, medica, new DateOnly(2026, 9, 3));
        var cancelada = await GuiaAsync(p, medica, new DateOnly(2026, 9, 4));
        await ReceberAsync(paga, p, 120m, new DateOnly(2026, 9, 20));
        await ReceberAsync(prevista, p, 120m, null);
        var desfeita = await ReceberAsync(cancelada, p, 120m, new DateOnly(2026, 9, 20));
        await _financeiro.CancelarAsync(desfeita.Id, "lançada na guia errada", "financeiro");

        var previa = await PreviaDeAsync(medica, Setembro);

        previa.Itens.Should().ContainSingle().Which.CodigoFaturamentoId.Should().Be(paga.Id);
        previa.ValorAFechar.Should().Be(65m);
        previa.Aguardando.Select(a => a.CodigoFaturamentoId).Should().BeEquivalentTo([prevista.Id, cancelada.Id]);
        previa.Aguardando.Single(a => a.CodigoFaturamentoId == prevista.Id).ReceitaPrevista.Should().BeTrue();
        previa.Aguardando.Single(a => a.CodigoFaturamentoId == cancelada.Id).ReceitaPrevista.Should().BeFalse();
    }

    [Fact]
    public async Task Exemplo_da_clinica_competencia_e_do_pagamento_e_o_demonstrativo_agrupa_por_convenio_e_GRD()
    {
        var medica = await ProfissionalAsync("Dra. Ana Souza");
        await RegraAsync(medica, 65m, "Amil");
        await RegraAsync(medica, 80m, "Petrobras");
        await RegraAsync(medica, 72m, "SULAMERICA");

        var amil = await PacienteAsync("Ana Amil", Convenio.Amil, "Amil");
        var amil2 = await PacienteAsync("Bruno Amil", Convenio.Amil, "Amil");
        var petro = await PacienteAsync("Carla Petro", Convenio.Petrobras, "Petrobras");
        var sul = await PacienteAsync("Diego Sul", Convenio.Personalizado, "SULAMERICA");

        await ReceberAsync(await GuiaAsync(amil, medica, new DateOnly(2026, 8, 20)), amil, 110m,
            new DateOnly(2026, 9, 10), "GRD/Conta 5950322456");
        await ReceberAsync(await GuiaAsync(amil2, medica, new DateOnly(2026, 8, 21)), amil2, 110m,
            new DateOnly(2026, 9, 25), "GRD/Conta 5950399999");
        await ReceberAsync(await GuiaAsync(petro, medica, new DateOnly(2026, 9, 1)), petro, 150m,
            new DateOnly(2026, 9, 15));
        // Atendidas em OUTUBRO, pagas no pagamento que a operadora lançou em setembro: é a
        // competência do PAGAMENTO que manda (a SulAmérica 09/2026 da clínica trazia 06/10).
        await ReceberAsync(await GuiaAsync(sul, medica, new DateOnly(2026, 10, 6)), sul, 130m,
            new DateOnly(2026, 9, 30), "Lote de envio 14927685");
        await ReceberAsync(await GuiaAsync(sul, medica, new DateOnly(2026, 10, 7)), sul, 130m,
            new DateOnly(2026, 9, 30), "Lote de envio 14927685");
        // Paga em outubro: fica para a competência de outubro.
        await ReceberAsync(await GuiaAsync(sul, medica, new DateOnly(2026, 9, 8)), sul, 130m,
            new DateOnly(2026, 10, 2), "Lote 2");

        var previa = await PreviaDeAsync(medica, Setembro);
        previa.QuantidadeAFechar.Should().Be(5);
        previa.ValorAFechar.Should().Be(65m * 2 + 80m + 72m * 2);
        previa.Itens.Should().OnlyContain(i => i.Pronto);
        previa.Itens.Where(i => i.ConvenioCodigo == "SULAMERICA").Select(i => i.DataAtendimento.Month)
            .Should().AllBeEquivalentTo(10);

        var fechamento = await _servico.FecharAsync(medica, new DateOnly(2026, 9, 17), "Marina (financeiro)");

        fechamento.Competencia.Should().Be(Setembro);
        fechamento.RecebidosAte.Should().Be(new DateOnly(2026, 9, 30));
        fechamento.Valor.Should().Be(354m);
        fechamento.Quantidade.Should().Be(5);
        fechamento.ConferidoPor.Should().Be("Marina (financeiro)");

        var saida = await _repo.ObterLancamentoAsync(fechamento.LancamentoFinanceiroId!.Value);
        saida!.Tipo.Should().Be(TipoLancamento.Saida);
        saida.Status.Should().Be(StatusLancamento.Previsto);
        saida.Data.Should().Be(new DateOnly(2026, 9, 30));
        saida.Valor.Should().Be(354m);
        saida.Contraparte.Should().Be("Dra. Ana Souza");
        saida.Descricao.Should().Be("Repasse Dra. Ana Souza — competência 09/2026");
        (await _db.Auditoria.AnyAsync(a => a.Acao == "RepasseFechado" && a.Operador == "Marina (financeiro)"))
            .Should().BeTrue();

        var d = await _servico.DemonstrativoAsync(fechamento.Id);
        d.Situacao.Should().Be("Conferido");
        d.Total.Should().Be(354m);
        d.Resumo.Should().HaveCount(3);
        d.Resumo.Select(r => (r.Quantidade, r.ValorUnitario, r.Total))
            .Should().BeEquivalentTo(new (int, decimal?, decimal)[] { (2, 65m, 130m), (1, 80m, 80m), (2, 72m, 144m) });
        d.RegrasAplicadas.Should().Contain(r => r.EndsWith("R$ 65,00 por atendimento"));
        // Amil em DUAS GRDs = dois blocos; a SulAmérica num lote só.
        d.Blocos.Should().HaveCount(4);
        d.Blocos.Select(b => b.ReferenciaPagamento).Should().Contain(["GRD/Conta 5950322456", "GRD/Conta 5950399999", "Lote de envio 14927685"]);
        d.Blocos.Single(b => b.ReferenciaPagamento == "Lote de envio 14927685").Total.Should().Be(144m);
        d.Estornos.Should().BeEmpty();

        var pdf = DemonstrativoRepassePdf.Gerar(d);
        pdf.Length.Should().BeGreaterThan(1000);
        System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
        DemonstrativoRepassePdf.NomeDoArquivo(d).Should().Be("Demonstrativo_Repasse_Dra_Ana_Souza_09-2026.pdf");
        var csv = DemonstrativoRepassePdf.Csv(d).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        csv.Should().HaveCount(6);
        csv[0].Should().StartWith("Competencia;Profissional;");
        csv.Skip(1).Should().Contain(l => l.Contains("Diego Sul") && l.Contains("06/10/2026") && l.TrimEnd().EndsWith(";72,00"));

        // O de outubro continua esperando a competência dele.
        (await PreviaDeAsync(medica, Outubro)).Itens.Should().ContainSingle().Which.Valor.Should().Be(72m);
    }

    [Fact]
    public async Task Regra_por_tipo_de_guia_vence_a_do_convenio_que_vence_a_geral()
    {
        var medica = await ProfissionalAsync();
        await RegraAsync(medica, 40m);
        await RegraAsync(medica, 50m, "Amil");
        await RegraAsync(medica, 65m, "Amil", TipoCodigo.Consulta);
        var amil = await PacienteAsync("Ana", Convenio.Amil, "Amil");
        var petro = await PacienteAsync("Bia", Convenio.Petrobras, "Petrobras");

        var consulta = await GuiaAsync(amil, medica, new DateOnly(2026, 9, 2));
        var acupuntura = await GuiaAsync(amil, medica, new DateOnly(2026, 9, 3), TipoCodigo.Acupuntura);
        var outra = await GuiaAsync(petro, medica, new DateOnly(2026, 9, 4));
        foreach (var (g, pac) in new[] { (consulta, amil), (acupuntura, amil), (outra, petro) })
            await ReceberAsync(g, pac, 100m, new DateOnly(2026, 9, 20));

        var itens = (await PreviaDeAsync(medica, Setembro)).Itens;
        itens.Single(i => i.CodigoFaturamentoId == consulta.Id).Valor.Should().Be(65m);
        itens.Single(i => i.CodigoFaturamentoId == consulta.Id).RegraDescricao.Should().Be("R$ 65,00 por consulta");
        itens.Single(i => i.CodigoFaturamentoId == consulta.Id).Descricao.Should().Be("Consulta");
        itens.Single(i => i.CodigoFaturamentoId == acupuntura.Id).Valor.Should().Be(50m);
        itens.Single(i => i.CodigoFaturamentoId == outra.Id).Valor.Should().Be(40m);
    }

    [Fact]
    public async Task Sem_regra_o_item_aparece_bloqueado_nao_soma_e_nao_fecha()
    {
        var medica = await ProfissionalAsync();
        var p = await PacienteAsync("Ana", Convenio.Amil, "Amil");
        await ReceberAsync(await GuiaAsync(p, medica, new DateOnly(2026, 9, 2)), p, 100m, new DateOnly(2026, 9, 20));

        var previa = await PreviaDeAsync(medica, Setembro);

        previa.Itens.Should().ContainSingle().Which.Bloqueio.Should().Be(BloqueioItemRepasse.SemRegra);
        previa.Itens[0].Valor.Should().Be(0m);
        previa.ValorAFechar.Should().Be(0m);
        previa.PodeFechar.Should().BeFalse();
        var fechar = () => _servico.FecharAsync(medica, Setembro, "gerente");
        await fechar.Should().ThrowAsync<InvalidOperationException>().WithMessage("Não há recebimento pronto*");
    }

    [Fact]
    public async Task Horario_sem_profissional_bloqueia_e_cai_no_grupo_sem_profissional()
    {
        var p = await PacienteAsync("Ana", Convenio.Amil, "Amil");
        await ReceberAsync(await GuiaAsync(p, null, new DateOnly(2026, 9, 2)), p, 100m, new DateOnly(2026, 9, 20));

        var grupo = await PreviaDeAsync(0, Setembro);

        grupo.Profissional.Should().Be("Sem profissional no horário");
        grupo.Itens.Should().ContainSingle().Which.Bloqueio.Should().Be(BloqueioItemRepasse.SemProfissional);
        var fechar = () => _servico.FecharAsync(0, Setembro, "gerente");
        await fechar.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Sessao_lancada_por_fora_usa_o_profissional_do_horario_substituido()
    {
        var medica = await ProfissionalAsync();
        await RegraAsync(medica, 65m);
        var p = await PacienteAsync("Ana", Convenio.Amil, "Amil");
        await ReceberAsync(await GuiaAsync(p, medica, new DateOnly(2026, 9, 2), horarioSubstituido: true),
            p, 100m, new DateOnly(2026, 9, 20));

        (await PreviaDeAsync(medica, Setembro)).Itens.Should().ContainSingle().Which.Pronto.Should().BeTrue();
    }

    // ==================== Vigência e reajuste ====================

    [Fact]
    public async Task Reajuste_vale_pela_data_do_atendimento_e_nao_reescreve_o_fechado()
    {
        var medica = await ProfissionalAsync();
        var regra = await RegraAsync(medica, 65m, "Amil", de: new DateOnly(2026, 1, 1));
        var p = await PacienteAsync("Ana", Convenio.Amil, "Amil");

        var fechada = await GuiaAsync(p, medica, new DateOnly(2026, 8, 10));
        await ReceberAsync(fechada, p, 100m, new DateOnly(2026, 9, 5));
        var setembro = await _servico.FecharAsync(medica, Setembro, "gerente");
        setembro.Valor.Should().Be(65m);

        var nova = await _regras.ReajustarRegraAsync(regra.Id, 70m, null, new DateOnly(2026, 9, 1), "direção");

        var antiga = await _repo.ObterRegraRepasseAsync(regra.Id);
        antiga!.VigenteAte.Should().Be(new DateOnly(2026, 8, 31));
        nova.VigenteDe.Should().Be(new DateOnly(2026, 9, 1));
        nova.VigenteAte.Should().BeNull();
        nova.ConvenioCodigo.Should().Be("Amil");
        nova.ValorPorAtendimento.Should().Be(70m);
        (await _db.Auditoria.AnyAsync(a => a.Acao == "RegraRepasseReajustada" && a.Operador == "direção"
                                           && a.Detalhe!.Contains("R$ 65,00") && a.Detalhe.Contains("R$ 70,00")))
            .Should().BeTrue();

        var antesDoReajuste = await GuiaAsync(p, medica, new DateOnly(2026, 8, 25));
        var depoisDoReajuste = await GuiaAsync(p, medica, new DateOnly(2026, 9, 10));
        await ReceberAsync(antesDoReajuste, p, 100m, new DateOnly(2026, 10, 5));
        await ReceberAsync(depoisDoReajuste, p, 100m, new DateOnly(2026, 10, 6));

        var itens = (await PreviaDeAsync(medica, Outubro)).Itens;
        itens.Single(i => i.CodigoFaturamentoId == antesDoReajuste.Id).Valor.Should().Be(65m);
        itens.Single(i => i.CodigoFaturamentoId == depoisDoReajuste.Id).Valor.Should().Be(70m);
        itens.Should().NotContain(i => i.CodigoFaturamentoId == fechada.Id);
        (await _servico.ObterAsync(setembro.Id))!.Itens.Single().Valor.Should().Be(65m);
    }

    [Fact]
    public async Task Reajuste_recusa_data_no_inicio_regra_desativada_valor_invalido_e_fora_da_vigencia()
    {
        var medica = await ProfissionalAsync();
        var regra = await RegraAsync(medica, 65m, "Amil", de: new DateOnly(2026, 3, 1), ate: new DateOnly(2026, 12, 31));

        var noInicio = () => _regras.ReajustarRegraAsync(regra.Id, 70m, null, new DateOnly(2026, 3, 1), "g");
        await noInicio.Should().ThrowAsync<InvalidOperationException>().WithMessage("*edite a regra*");
        var foraDaVigencia = () => _regras.ReajustarRegraAsync(regra.Id, 70m, null, new DateOnly(2027, 1, 5), "g");
        await foraDaVigencia.Should().ThrowAsync<InvalidOperationException>().WithMessage("*fora da vigência*");
        var semValor = () => _regras.ReajustarRegraAsync(regra.Id, null, 30m, new DateOnly(2026, 6, 1), "g");
        await semValor.Should().ThrowAsync<InvalidOperationException>().WithMessage("*novo valor*");
        var inexistente = () => _regras.ReajustarRegraAsync(9999, 70m, null, new DateOnly(2026, 6, 1), "g");
        await inexistente.Should().ThrowAsync<InvalidOperationException>();

        regra.Ativa = false;
        await _regras.SalvarRegraAsync(regra, "g");
        var desativada = () => _regras.ReajustarRegraAsync(regra.Id, 70m, null, new DateOnly(2026, 6, 1), "g");
        await desativada.Should().ThrowAsync<InvalidOperationException>().WithMessage("*desativada*");
        (await _db.RegrasRepasse.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Salvar_regra_copia_o_tipo_de_guia_e_deixa_trilha_de_cada_mudanca_de_valor()
    {
        var medica = await ProfissionalAsync();
        var regra = await RegraAsync(medica, 65m, "Amil", TipoCodigo.Consulta);

        // Como a tela faz: um objeto NOVO com o Id da regra (o formulário não edita a entidade rastreada).
        RegraRepasse Copia(decimal valor, bool ativa) => new()
        {
            Id = regra.Id, ProfissionalId = medica, Base = BaseRepasse.ValorPorAtendimento, ValorPorAtendimento = valor,
            ConvenioCodigo = "Amil", TipoGuia = TipoCodigo.Consulta, Ativa = ativa
        };
        await _regras.SalvarRegraAsync(Copia(70m, true), "direção");
        await _regras.SalvarRegraAsync(Copia(70m, false), "direção");

        var salva = await _db.RegrasRepasse.AsNoTracking().SingleAsync();
        salva.TipoGuia.Should().Be(TipoCodigo.Consulta);
        var acoes = await _db.Auditoria.Where(a => a.Acao.StartsWith("RegraRepasse")).Select(a => a.Acao).ToListAsync();
        acoes.Should().Equal("RegraRepasseCriada", "RegraRepasseAlterada", "RegraRepasseDesativada");
        (await _db.Auditoria.SingleAsync(a => a.Acao == "RegraRepasseAlterada")).Detalhe
            .Should().Contain("R$ 65,00 por consulta").And.Contain("R$ 70,00 por consulta");
    }

    [Fact]
    public async Task Regra_usada_num_fechamento_vivo_nao_se_exclui()
    {
        var medica = await ProfissionalAsync();
        var regra = await RegraAsync(medica, 65m);
        var livre = await RegraAsync(medica, 10m, "Petrobras");
        var p = await PacienteAsync("Ana", Convenio.Amil, "Amil");
        await ReceberAsync(await GuiaAsync(p, medica, new DateOnly(2026, 9, 2)), p, 100m, new DateOnly(2026, 9, 20));
        await _servico.FecharAsync(medica, Setembro, "gerente");

        var excluir = () => _regras.ExcluirRegraAsync(regra.Id, "gerente");
        await excluir.Should().ThrowAsync<InvalidOperationException>().WithMessage("*desative-a*");

        await _regras.ExcluirRegraAsync(livre.Id, "gerente");
        (await _db.RegrasRepasse.CountAsync()).Should().Be(1);
        (await _db.Auditoria.AnyAsync(a => a.Acao == "RegraRepasseExcluida")).Should().BeTrue();
    }

    [Fact]
    public void Descricao_da_regra_por_tipo_de_guia_em_ptBR()
    {
        new RegraRepasse { Base = BaseRepasse.ValorPorAtendimento, ValorPorAtendimento = 65m, TipoGuia = TipoCodigo.Consulta }
            .Descricao.Should().Be("R$ 65,00 por consulta");
        new RegraRepasse { Base = BaseRepasse.ValorPorAtendimento, ValorPorAtendimento = 30m, TipoGuia = TipoCodigo.Bsv }
            .Descricao.Should().Be("R$ 30,00 por BSV");
        new RegraRepasse { Base = BaseRepasse.ValorPorAtendimento, ValorPorAtendimento = 50m }
            .Descricao.Should().Be("R$ 50,00 por atendimento");
        new RegraRepasse { Base = BaseRepasse.PercentualDaReceita, Percentual = 40m, TipoGuia = TipoCodigo.Acupuntura }
            .Descricao.Should().Be("40% da receita de acupuntura");
    }

    // ==================== Trava de pagar duas vezes ====================

    [Fact]
    public async Task Fechar_duas_vezes_nao_duplica_e_a_segunda_vez_e_recusada()
    {
        var medica = await ProfissionalAsync();
        await RegraAsync(medica, 65m);
        var p = await PacienteAsync("Ana", Convenio.Amil, "Amil");
        await ReceberAsync(await GuiaAsync(p, medica, new DateOnly(2026, 9, 2)), p, 100m, new DateOnly(2026, 9, 20));

        await _servico.FecharAsync(medica, Setembro, "gerente");
        var deNovo = () => _servico.FecharAsync(medica, Setembro, "gerente");
        await deNovo.Should().ThrowAsync<InvalidOperationException>().WithMessage("Não há recebimento pronto*");

        (await _db.ItensRepasse.CountAsync()).Should().Be(1);
        (await _db.RepassesFechamentos.CountAsync()).Should().Be(1);
        var previa = await PreviaDeAsync(medica, Setembro);
        previa.Itens.Should().BeEmpty();
        previa.JaFechados.Should().ContainSingle();
    }

    [Fact]
    public async Task Indice_unico_vale_entre_duas_instancias_de_servico_e_de_contexto()
    {
        var medica = await ProfissionalAsync();
        await RegraAsync(medica, 65m);
        var p = await PacienteAsync("Ana", Convenio.Amil, "Amil");
        await ReceberAsync(await GuiaAsync(p, medica, new DateOnly(2026, 9, 2)), p, 100m, new DateOnly(2026, 9, 20));

        await using var outroDb = new ClinicaDbContext(_options);
        var outroPosto = new FechamentoRepasseService(new ClinicaRepositorio(outroDb));
        (await outroPosto.PreviaAsync(Setembro)).Single().PodeFechar.Should().BeTrue();

        await _servico.FecharAsync(medica, Setembro, "posto 1");

        var segundo = () => outroPosto.FecharAsync(medica, Setembro, "posto 2");
        await segundo.Should().ThrowAsync<InvalidOperationException>();

        // E a trava é do BANCO: a mesma chave gravada à mão por outro contexto é recusada.
        var item = await _db.ItensRepasse.AsNoTracking().SingleAsync();
        outroDb.ItensRepasse.Add(new ItemRepasse
        {
            RepasseFechamentoId = item.RepasseFechamentoId, Chave = item.Chave, AtendimentoId = item.AtendimentoId,
            PacienteId = item.PacienteId, PacienteNome = "x", ConvenioCodigo = "x", ConvenioNome = "x",
            Descricao = "x", RegraDescricao = "x", DataAtendimento = item.DataAtendimento,
            DataRecebimento = item.DataRecebimento
        });
        var duplicar = () => outroDb.SaveChangesAsync();
        await duplicar.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Corrida_entre_postos_vira_frase_para_a_tela_e_nada_fica_gravado()
    {
        var medica = await ProfissionalAsync();
        await RegraAsync(medica, 65m);
        var p = await PacienteAsync("Ana", Convenio.Amil, "Amil");
        await ReceberAsync(await GuiaAsync(p, medica, new DateOnly(2026, 9, 2)), p, 100m, new DateOnly(2026, 9, 20));

        var corrida = new OutroPostoGravaAntes(medica);
        var opcoes = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).AddInterceptors(corrida).Options;
        await using var db = new ClinicaDbContext(opcoes);
        var servico = new FechamentoRepasseService(new ClinicaRepositorio(db));

        var fechar = () => servico.FecharAsync(medica, Setembro, "posto 2");

        await fechar.Should().ThrowAsync<InvalidOperationException>().WithMessage(FechamentoRepasseService.MensagemConcorrencia);
        corrida.Disparou.Should().BeTrue();
        (await _db.Lancamentos.CountAsync(l => l.Tipo == TipoLancamento.Saida)).Should().Be(0);
        (await _db.ItensRepasse.CountAsync()).Should().Be(0);
    }

    /// <summary>
    /// Simula o outro posto: no instante do SaveChanges, um fechamento com a MESMA chave já
    /// está gravado (dentro da mesma transação, para valer nos dois bancos dos testes).
    /// </summary>
    private sealed class OutroPostoGravaAntes(int profissionalId) : SaveChangesInterceptor
    {
        public bool Disparou { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var item = eventData.Context!.ChangeTracker.Entries<ItemRepasse>()
                .FirstOrDefault(e => e.State == EntityState.Added)?.Entity;
            if (!Disparou && item is not null)
            {
                Disparou = true;
                var db = eventData.Context.Database;
                var dia = new DateOnly(2026, 9, 30);
                await db.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "RepassesFechamentos" ("ProfissionalId", "Competencia", "RecebidosAte", "ProfissionalNome",
                        "Quantidade", "Valor", "ConferidoPor", "ConferidoEm")
                    VALUES ({profissionalId}, {dia}, {dia}, {"outro"}, {1}, {65m}, {"posto 1"}, '2026-10-01 09:00:00')
                    """, cancellationToken);
                await db.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "ItensRepasse" ("RepasseFechamentoId", "Origem", "Natureza", "Chave", "AtendimentoId",
                        "PacienteId", "PacienteNome", "DataAtendimento", "ConvenioCodigo", "ConvenioNome", "Descricao",
                        "DataRecebimento", "ValorRecebido", "RegraDescricao", "Valor")
                    VALUES ((SELECT MAX("Id") FROM "RepassesFechamentos"), {"Convenio"}, {"Credito"}, {item.Chave},
                        {item.AtendimentoId}, {item.PacienteId}, {"x"}, {dia}, {"x"}, {"x"}, {"x"}, {dia}, {100m}, {"x"}, {65m})
                    """, cancellationToken);
            }
            return result;
        }
    }

    // ==================== Cancelar e pagar ====================

    [Fact]
    public async Task Cancelar_libera_os_itens_cancela_a_saida_e_permite_fechar_de_novo()
    {
        var medica = await ProfissionalAsync();
        await RegraAsync(medica, 65m);
        var p = await PacienteAsync("Ana", Convenio.Amil, "Amil");
        await ReceberAsync(await GuiaAsync(p, medica, new DateOnly(2026, 9, 2)), p, 100m, new DateOnly(2026, 9, 20));
        var fechamento = await _servico.FecharAsync(medica, Setembro, "gerente");

        var semMotivo = () => _servico.CancelarAsync(fechamento.Id, "  ", "gerente");
        await semMotivo.Should().ThrowAsync<InvalidOperationException>();

        await _servico.CancelarAsync(fechamento.Id, "regra errada", "gerente");

        var cancelado = await _db.RepassesFechamentos.AsNoTracking().Include(f => f.Itens).Include(f => f.Lancamento)
            .SingleAsync(f => f.Id == fechamento.Id);
        cancelado.CanceladoEm.Should().NotBeNull();
        cancelado.CanceladoPor.Should().Be("gerente");
        cancelado.MotivoCancelamento.Should().Be("regra errada");
        cancelado.Itens.Should().OnlyContain(i => i.CanceladoEm != null);
        cancelado.Lancamento!.Status.Should().Be(StatusLancamento.Cancelado);
        (await _servico.DemonstrativoAsync(fechamento.Id)).Situacao.Should().Be("Cancelado");
        DemonstrativoRepassePdf.Gerar(await _servico.DemonstrativoAsync(fechamento.Id)).Should().NotBeEmpty();

        (await PreviaDeAsync(medica, Setembro)).PodeFechar.Should().BeTrue();
        var refeito = await _servico.FecharAsync(medica, Setembro, "gerente");
        refeito.Valor.Should().Be(65m);
        (await _db.ItensRepasse.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Pagamento_realiza_a_saida_e_fechamento_pago_nao_se_cancela()
    {
        var medica = await ProfissionalAsync();
        await RegraAsync(medica, 65m);
        var p = await PacienteAsync("Ana", Convenio.Amil, "Amil");
        await ReceberAsync(await GuiaAsync(p, medica, new DateOnly(2026, 9, 2)), p, 100m, new DateOnly(2026, 9, 20));
        var fechamento = await _servico.FecharAsync(medica, Setembro, "gerente");

        await _servico.RegistrarPagamentoAsync(fechamento.Id, new DateOnly(2026, 10, 5), FormaPagamento.Pix, "financeiro");

        var saida = await _db.Lancamentos.AsNoTracking().SingleAsync(l => l.Id == fechamento.LancamentoFinanceiroId);
        saida.Status.Should().Be(StatusLancamento.Realizado);
        saida.DataPagamento.Should().Be(new DateOnly(2026, 10, 5));
        saida.FormaPagamento.Should().Be(FormaPagamento.Pix);
        (await _db.Auditoria.AnyAsync(a => a.Acao == "RepassePago" && a.Operador == "financeiro")).Should().BeTrue();
        (await _servico.DemonstrativoAsync(fechamento.Id)).Situacao.Should().Be("Pago em 05/10/2026");

        var pagarDeNovo = () => _servico.RegistrarPagamentoAsync(fechamento.Id, new DateOnly(2026, 10, 6), FormaPagamento.Pix, "financeiro");
        await pagarDeNovo.Should().ThrowAsync<InvalidOperationException>().WithMessage("*já foi pago*");
        var cancelar = () => _servico.CancelarAsync(fechamento.Id, "engano", "gerente");
        await cancelar.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Estorne o pagamento no Caixa*");
    }

    // ==================== Estorno ====================

    [Fact]
    public async Task Receita_cancelada_depois_do_fechamento_entra_negativa_no_proximo()
    {
        var medica = await ProfissionalAsync();
        await RegraAsync(medica, 65m, "Amil");
        await RegraAsync(medica, 80m, "Petrobras");
        var amil = await PacienteAsync("Ana", Convenio.Amil, "Amil");
        var petro = await PacienteAsync("Bia", Convenio.Petrobras, "Petrobras");
        var receita = await ReceberAsync(await GuiaAsync(amil, medica, new DateOnly(2026, 9, 2)), amil, 100m, new DateOnly(2026, 9, 20));
        var setembro = await _servico.FecharAsync(medica, Setembro, "gerente");

        await _financeiro.CancelarAsync(receita.Id, "pagamento devolvido pela operadora", "financeiro");
        await ReceberAsync(await GuiaAsync(petro, medica, new DateOnly(2026, 10, 2)), petro, 150m, new DateOnly(2026, 10, 15));

        var previa = await PreviaDeAsync(medica, Outubro);
        var estorno = previa.Itens.Single(i => i.Natureza == NaturezaItemRepasse.Estorno);
        estorno.Valor.Should().Be(-65m);
        estorno.Chave.Should().StartWith("E:G:");
        estorno.ItemEstornadoId.Should().Be(setembro.Itens.Single().Id);
        previa.ValorAFechar.Should().Be(15m);
        previa.QuantidadeAFechar.Should().Be(1);

        var outubro = await _servico.FecharAsync(medica, Outubro, "gerente");
        outubro.Valor.Should().Be(15m);
        var d = await _servico.DemonstrativoAsync(outubro.Id);
        d.Estornos.Should().ContainSingle().Which.Valor.Should().Be(-65m);
        d.Total.Should().Be(15m);
        DemonstrativoRepassePdf.Csv(d).Should().Contain(";Estorno;");
        // O fechamento de setembro, já conferido, não é reescrito.
        (await _servico.ObterAsync(setembro.Id))!.Valor.Should().Be(65m);
        (await PreviaDeAsync(medica, Outubro)).Itens.Should().BeEmpty();
    }

    // ==================== Particular ====================

    [Fact]
    public async Task Particular_soma_o_que_entrou_e_dentro_de_periodo_apurado_no_modelo_antigo_e_bloqueado()
    {
        var medica = await ProfissionalAsync();
        await _regras.SalvarRegraAsync(new RegraRepasse
        {
            ProfissionalId = medica, Base = BaseRepasse.PercentualDaReceita, Percentual = 50m,
            ConvenioCodigo = FechamentoRepasseService.CodigoParticular, Ativa = true
        }, "gerente");
        var p = await PacienteAsync("Ana", Convenio.Amil, "Amil");

        var antigo = await ParticularAsync(p, medica, new DateOnly(2026, 8, 10), 200m);
        var novo = await ParticularAsync(p, medica, new DateOnly(2026, 9, 10), 150m, 50m);
        _db.RepassesApurados.Add(new RepasseApurado
        {
            ProfissionalId = medica, Inicio = new DateOnly(2026, 8, 1), Fim = new DateOnly(2026, 8, 31), Valor = 100m
        });
        await _db.SaveChangesAsync();

        var itens = (await PreviaDeAsync(medica, Setembro)).Itens;

        itens.Single(i => i.AtendimentoId == antigo).Bloqueio.Should().Be(BloqueioItemRepasse.ApuradoNoModeloAntigo);
        var item = itens.Single(i => i.AtendimentoId == novo);
        item.Pronto.Should().BeTrue();
        item.Chave.Should().Be($"P:{novo}");
        item.ConvenioNome.Should().Be("Particular");
        item.ValorRecebido.Should().Be(200m);
        item.Valor.Should().Be(100m);

        var fechamento = await _servico.FecharAsync(medica, Setembro, "gerente");
        fechamento.Valor.Should().Be(100m);
        fechamento.Itens.Should().ContainSingle(i => i.Origem == OrigemItemRepasse.Particular);
    }

    [Fact]
    public async Task Recebimentos_antes_do_marco_da_virada_nao_entram()
    {
        var medica = await ProfissionalAsync();
        await RegraAsync(medica, 65m);
        var p = await PacienteAsync("Ana", Convenio.Amil, "Amil");
        await ReceberAsync(await GuiaAsync(p, medica, new DateOnly(2026, 6, 2)), p, 100m, new DateOnly(2026, 6, 20));
        await ReceberAsync(await GuiaAsync(p, medica, new DateOnly(2026, 9, 2)), p, 100m, new DateOnly(2026, 9, 20));
        (await PreviaDeAsync(medica, Setembro)).Itens.Should().HaveCount(2);

        await _servico.DefinirRecebidosDesdeAsync(new DateOnly(2026, 9, 1), "direção");

        (await _servico.RecebidosDesdeAsync()).Should().Be(new DateOnly(2026, 9, 1));
        (await PreviaDeAsync(medica, Setembro)).Itens.Should().ContainSingle()
            .Which.DataRecebimento.Should().Be(new DateOnly(2026, 9, 20));
    }
}
