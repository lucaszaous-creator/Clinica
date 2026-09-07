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
/// O circuito do PARTICULAR — Recepção → Financeiro, para quem não tem guia (set/2026).
///
/// O <c>CircuitoCompletoTests</c> prende o elo do convênio: a guia sai da conciliação porque
/// passou a TER receita. O particular não tinha elo nenhum para prender: a venda de pacote
/// não movia dinheiro, o particular de primeira vez saía do Finalizar sem que ninguém lhe
/// perguntasse como pagou, e a sessão sem receita não aparecia em tela alguma — nem no dia,
/// nem depois. Cada teste aqui é um desses buracos fechado, e o que eles têm em comum é a
/// assinatura de sempre: <b>nada falhava</b>. Build verde, 2266 testes verdes, e a clínica
/// atendendo de graça sem saber.
/// </summary>
public class CircuitoParticularTests : IDisposable
{
    private const string CodigoParticular = "Particular";

    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AgendaService _agenda;
    private readonly PacoteService _pacotes;
    private readonly FinanceiroService _financeiro;
    private readonly ContasService _contas;
    private readonly PrecoParticularService _precos;
    private readonly FechamentoSessaoService _fechamento;
    private readonly InadimplenciaService _inadimplencia;
    private readonly ElegibilidadeService _elegibilidade;
    private readonly int _profPadrao;
    private int _marcados;

    private static readonly DateTime Sessao = new(2026, 8, 20, 14, 0, 0);
    private static DateOnly Dia => DateOnly.FromDateTime(Sessao);

    public CircuitoParticularTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        var prof = new Profissional { Nome = "Dra. Padrão" };
        _db.Profissionais.Add(prof);
        _db.SaveChanges();
        _profPadrao = prof.Id;
        _repo = new ClinicaRepositorio(_db);

        _agenda = new AgendaService(_repo, new AtendimentoService(_repo));
        _pacotes = new PacoteService(_repo);
        _financeiro = new FinanceiroService(_repo);
        _contas = new ContasService(_repo);
        _precos = new PrecoParticularService(_repo);
        _fechamento = new FechamentoSessaoService(
            _repo, _agenda, _pacotes, new EstoqueService(_repo), _financeiro, _precos, _contas);
        _inadimplencia = new InadimplenciaService(_repo, _financeiro);
        _elegibilidade = new ElegibilidadeService(
            _repo, new AutorizacaoService(_repo), new ConsentimentoService(_repo),
            new ConsultaService(_repo), _inadimplencia, _pacotes);

        // O catálogo é um cache estático: cada teste monta o seu. O "Particular" é o cadastro
        // que NÃO gera guia (parcela 60); a Amil é o convênio de controle.
        CatalogoConvenios.Atualizar(
        [
            new EntradaConvenio(CodigoParticular, "Particular", Convenio.Personalizado, true,
                new ConfiguracaoRegraGenerica { FazEletro = true, TemSegundoCodigo = true },
                FormatoNumeroGuia.SemValidacao, GeraGuia: false),
            new EntradaConvenio("Amil", "Amil", Convenio.Amil, true, GeraGuia: true)
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

    private async Task<int> ParticularAsync(string nome = "Maria")
    {
        var p = new Paciente
        {
            Nome = nome, Convenio = Convenio.Personalizado, ConvenioCodigo = CodigoParticular,
            Sexo = Sexo.Feminino
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<int> ConveniadoAsync(string nome = "João")
    {
        var p = new Paciente
        {
            Nome = nome, Convenio = Convenio.Amil, ConvenioCodigo = "Amil", Sexo = Sexo.Masculino
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    /// <summary>Marca uma sessão; cada chamada anda uma hora (dois horários no mesmo instante são choque).</summary>
    private async Task<int> AgendarAsync(int pacienteId)
    {
        var ag = await _agenda.AgendarAsync(
            pacienteId, Sessao.AddHours(_marcados++), ModalidadeAtendimento.AcupunturaComEletro,
            null, profissionalId: _profPadrao);
        return ag.Id;
    }

    /// <summary>A sessão ACONTECE: presença confirmada, atendimento e códigos existem.</summary>
    private async Task<Atendimento> RealizarAsync(int pacienteId)
    {
        var agendamentoId = await AgendarAsync(pacienteId);
        var registro = await _fechamento.RegistrarAtendimentoAsync(agendamentoId, "recepcao");
        return registro.Atendimento;
    }

    private async Task<PacoteCatalogo> CatalogoAsync(decimal valor = 1000m)
        => await _pacotes.SalvarCatalogoAsync(new PacoteCatalogo
        {
            Nome = "Pacote 10 sessões", Tipo = TipoPacote.Sessoes, SessoesIncluidas = 10, Valor = valor
        });

    private IReadOnlyList<LancamentoFinanceiro> LancamentosDoPacote(int pacoteId)
        => _db.Lancamentos.AsNoTracking().Where(l => l.PacotePacienteId == pacoteId).OrderBy(l => l.Id).ToList();

    // ==================== A venda do pacote move dinheiro ====================

    [Fact]
    public async Task Venda_a_vista_entra_no_caixa_ligada_ao_pacote_e_ao_paciente()
    {
        var pacienteId = await ParticularAsync();
        var catalogo = await CatalogoAsync(1200m);

        var venda = await _pacotes.VenderAsync(
            pacienteId, catalogo.Id, Dia, operador: "balcao",
            pagamento: PagamentoDaVenda.AVista(FormaPagamento.Pix));

        var lancamentos = LancamentosDoPacote(venda.Id);
        lancamentos.Should().ContainSingle();
        var l = lancamentos[0];
        l.Status.Should().Be(StatusLancamento.Realizado);
        l.Valor.Should().Be(1200m);
        l.FormaPagamento.Should().Be(FormaPagamento.Pix);
        l.PacienteId.Should().Be(pacienteId);
        l.Tipo.Should().Be(TipoLancamento.Entrada);

        // O CAIXA vê a venda no dia — era isso que não acontecia.
        (await _financeiro.ResumoAsync(Dia, Dia)).EntradasRealizadas.Should().Be(1200m);

        var saldo = (await _pacotes.DoPacienteAsync(pacienteId, Dia)).Single();
        saldo.PagamentoRotulo.Should().Be("pago");
    }

    [Fact]
    public async Task Venda_a_prazo_vira_contas_a_receber_com_dono_e_a_inadimplencia_as_ve_quando_vencem()
    {
        var pacienteId = await ParticularAsync();
        var catalogo = await CatalogoAsync(1000m);

        var venda = await _pacotes.VenderAsync(
            pacienteId, catalogo.Id, Dia, operador: "balcao",
            pagamento: PagamentoDaVenda.APrazo(
                parcelas: 3, primeiroVencimento: Dia.AddMonths(1),
                entradaAgora: 100m, formaDaEntrada: FormaPagamento.Dinheiro));

        var lancamentos = LancamentosDoPacote(venda.Id);
        lancamentos.Should().HaveCount(4);
        lancamentos.Sum(l => l.Valor).Should().Be(1000m);
        lancamentos.Count(l => l.Status == StatusLancamento.Realizado).Should().Be(1);
        lancamentos.Where(l => l.Status == StatusLancamento.Previsto)
            .Select(l => l.DataVencimento)
            .Should().Equal(Dia.AddMonths(1), Dia.AddMonths(2), Dia.AddMonths(3));
        lancamentos.Should().OnlyContain(l => l.PacienteId == pacienteId);

        // Antes do vencimento ninguém deve nada.
        (await _inadimplencia.DoPacienteAsync(pacienteId, Dia.AddDays(10))).Should().BeNull();

        // Vencida a 1ª parcela, o paciente aparece em "quem me deve" — com a parcela certa.
        var devedor = await _inadimplencia.DoPacienteAsync(pacienteId, Dia.AddMonths(1).AddDays(3));
        devedor.Should().NotBeNull();
        devedor!.Total.Should().Be(300m);
        devedor.Contas.Should().Be(1);

        // E a lista de pacotes DIZ o que falta receber, com a parcela vencida contada.
        var saldo = (await _pacotes.DoPacienteAsync(pacienteId, Dia.AddMonths(1).AddDays(3))).Single();
        saldo.ValorPago.Should().Be(100m);
        saldo.ValorAReceber.Should().Be(900m);
        saldo.ParcelasEmAberto.Should().Be(3);
        saldo.ParcelasVencidas.Should().Be(1);
        saldo.PagamentoRotulo.Should().Contain("a receber").And.Contain("1 vencida");
    }

    [Fact]
    public async Task Venda_sem_decisao_de_pagamento_continua_possivel_e_a_lista_DIZ_que_nao_ha_lancamento()
    {
        // O caminho antigo (e o dos testes que vendem sem pagamento) não quebra — mas a
        // ausência é dita, nunca mostrada como "R$ 0,00 pago".
        var pacienteId = await ParticularAsync();
        var catalogo = await CatalogoAsync();

        await _pacotes.VenderAsync(pacienteId, catalogo.Id, Dia);

        var saldo = (await _pacotes.DoPacienteAsync(pacienteId, Dia)).Single();
        saldo.TemLancamento.Should().BeFalse();
        saldo.PagamentoRotulo.Should().Be("sem lançamento no caixa");
    }

    [Fact]
    public async Task Cancelar_o_pacote_derruba_as_parcelas_previstas_e_mantem_o_que_ja_foi_recebido()
    {
        var pacienteId = await ParticularAsync();
        var catalogo = await CatalogoAsync(900m);
        var venda = await _pacotes.VenderAsync(
            pacienteId, catalogo.Id, Dia,
            pagamento: PagamentoDaVenda.APrazo(2, Dia.AddMonths(1), 300m, FormaPagamento.Pix));

        var avisos = await _pacotes.CancelarAsync(venda.Id, "desistiu do tratamento", "balcao");

        var lancamentos = LancamentosDoPacote(venda.Id);
        lancamentos.Where(l => l.DataVencimento != null)
            .Should().OnlyContain(l => l.Status == StatusLancamento.Cancelado,
                "venda cancelada não é dívida do paciente");
        lancamentos.Single(l => l.DataVencimento == null).Status
            .Should().Be(StatusLancamento.Realizado, "dinheiro que entrou não se cancela — devolução é saída");

        avisos.Should().ContainSingle().Which.Should().Contain("300").And.Contain("continuam no caixa");

        // Ninguém devendo depois do cancelamento.
        (await _inadimplencia.DoPacienteAsync(pacienteId, Dia.AddMonths(2))).Should().BeNull();
    }

    // ==================== O particular no Finalizar ====================

    [Fact]
    public async Task Particular_de_primeira_vez_SEMPRE_tem_decisao_e_convenio_sem_pacote_continua_sem()
    {
        var particular = await ParticularAsync();
        var conveniado = await ConveniadoAsync();

        var doParticular = await _fechamento.RegistrarAtendimentoAsync(await AgendarAsync(particular));
        var doConveniado = await _fechamento.RegistrarAtendimentoAsync(await AgendarAsync(conveniado));

        // Sem histórico, sem pacote, sem insumo: até set/2026 isto dava FALSE e a janela não
        // abria — a sessão saía registrada e sem uma linha de dinheiro.
        doParticular.Proposta.EhParticular.Should().BeTrue();
        doParticular.Proposta.SugereLancamento.Should().BeTrue();
        doParticular.Proposta.ParticularSemPreco.Should().BeTrue();
        doParticular.TemDecisao.Should().BeTrue();
        doParticular.GuiasGeradas.Should().Be(0);

        // O convênio sem pacote não mudou: o dinheiro dele vem pela conciliação da guia, e
        // abrir a janela seria pedir confirmação de uma tela vazia.
        doConveniado.Proposta.EhParticular.Should().BeFalse();
        doConveniado.Proposta.SugereLancamento.Should().BeFalse();
        doConveniado.TemDecisao.Should().BeFalse();
        doConveniado.GuiasGeradas.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Particular_com_pacote_debita_o_pacote_e_NAO_sugere_cobranca()
    {
        var pacienteId = await ParticularAsync();
        var catalogo = await CatalogoAsync();
        await _pacotes.VenderAsync(pacienteId, catalogo.Id, Dia.AddDays(-10),
            pagamento: PagamentoDaVenda.AVista(FormaPagamento.Pix));

        var registro = await _fechamento.RegistrarAtendimentoAsync(await AgendarAsync(pacienteId));

        registro.Proposta.TemPacote.Should().BeTrue();
        registro.Proposta.SugereLancamento.Should().BeFalse("sessão comprada já foi paga");
    }

    [Fact]
    public async Task A_tabela_de_preco_do_particular_preenche_o_valor_com_a_procedencia()
    {
        var pacienteId = await ParticularAsync();
        await _precos.SalvarAsync(new PrecoParticular
        {
            ModalidadeCodigo = nameof(ModalidadeAtendimento.AcupunturaComEletro), Valor = 180m
        });

        var agendamentoId = await AgendarAsync(pacienteId);
        var registro = await _fechamento.RegistrarAtendimentoAsync(agendamentoId);

        // Já no REGISTRO: modalidade e especialidade estão no horário, então a proposta
        // não precisa esperar o atendimento existir.
        registro.Proposta.ValorSugerido.Should().Be(180m);
        registro.Proposta.ProcedenciaDoValor.Should().StartWith("tabela do particular:");
        registro.Proposta.ParticularSemPreco.Should().BeFalse();

        // E a Conciliação propõe o MESMO número para a mesma sessão.
        var pendente = (await _financeiro.SessoesParticularesSemReceitaAsync(Dia, Dia)).Single();
        var proposto = await _precos.ProporAsync(
            pendente.CodigoDaModalidade, pendente.Modalidade, pendente.CodigoDaEspecialidade, pendente.Data);
        proposto.Valor.Should().Be(180m);
    }

    [Fact]
    public async Task O_preco_da_consulta_segue_a_ESPECIALIDADE_atendida()
    {
        var pacienteId = await ParticularAsync();
        await _precos.SalvarAsync(new PrecoParticular
        {
            ModalidadeCodigo = nameof(ModalidadeAtendimento.Consulta), Valor = 300m
        });
        await _precos.SalvarAsync(new PrecoParticular
        {
            ModalidadeCodigo = nameof(ModalidadeAtendimento.Consulta),
            EspecialidadeCodigo = nameof(Especialidade.Psiquiatria), Valor = 450m
        });

        var psiquiatria = await _agenda.AgendarAsync(
            pacienteId, Sessao.AddHours(_marcados++), ModalidadeAtendimento.Consulta, null,
            especialidadeConsulta: Especialidade.Psiquiatria, profissionalId: _profPadrao);
        var geriatria = await _agenda.AgendarAsync(
            pacienteId, Sessao.AddHours(_marcados++), ModalidadeAtendimento.Consulta, null,
            especialidadeConsulta: Especialidade.Geriatria, profissionalId: _profPadrao);

        (await _fechamento.PrepararAsync(psiquiatria.Id)).ValorSugerido.Should().Be(450m,
            "o preço com especialidade vence o genérico da modalidade");
        (await _fechamento.PrepararAsync(geriatria.Id)).ValorSugerido.Should().Be(300m,
            "sem preço próprio, a especialidade cai no genérico da consulta");
    }

    [Fact]
    public async Task Fica_a_receber_cria_conta_prevista_ligada_a_sessao_e_o_balcao_e_avisado_quando_vence()
    {
        var pacienteId = await ParticularAsync();
        var agendamentoId = await AgendarAsync(pacienteId);
        await _fechamento.RegistrarAtendimentoAsync(agendamentoId);

        var resultado = await _fechamento.ConcluirAsync(new DecisaoFechamento(
            agendamentoId, GerarLancamento: true, Valor: 150m,
            FicaAReceber: true, Vencimento: Dia.AddDays(15)), "recepcao");

        resultado.TudoCerto.Should().BeTrue();
        var conta = resultado.Lancamento!;
        conta.Status.Should().Be(StatusLancamento.Previsto);
        conta.DataVencimento.Should().Be(Dia.AddDays(15));
        conta.AtendimentoId.Should().Be(resultado.Atendimento.Id);
        conta.PacienteId.Should().Be(pacienteId);
        conta.FormaPagamento.Should().BeNull();
        conta.Descricao.Should().Contain("Maria");

        // Não entrou no caixa (é previsto) — e não some da conciliação por engano: some
        // porque TEM lançamento (o teste da conciliação abaixo prende isso).
        (await _financeiro.ResumoAsync(Dia, Dia)).EntradasRealizadas.Should().Be(0m);

        // Vencida, vira alerta amarelo no check-in seguinte.
        var check = await _elegibilidade.ConferirAsync(pacienteId, Dia.AddDays(15 + 10));
        check.Alertas.Should().Contain(a => a.Motivo == ImpedimentoElegibilidade.PacienteEmDebito);
    }

    [Fact]
    public async Task Fica_a_receber_sem_vencimento_e_recusado_e_a_guia_continua_de_pe()
    {
        var pacienteId = await ParticularAsync();
        var agendamentoId = await AgendarAsync(pacienteId);

        var resultado = await _fechamento.ConcluirAsync(new DecisaoFechamento(
            agendamentoId, GerarLancamento: true, Valor: 150m, FicaAReceber: true));

        resultado.Lancamento.Should().BeNull();
        resultado.Avisos.Should().ContainSingle().Which.Should().Contain("conta a receber NÃO foi registrada");
        resultado.Atendimento.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task O_lancamento_da_sessao_diz_quem_e_o_que_no_extrato()
    {
        var pacienteId = await ParticularAsync("Ana Souza");
        var agendamentoId = await AgendarAsync(pacienteId);

        var resultado = await _fechamento.ConcluirAsync(new DecisaoFechamento(
            agendamentoId, GerarLancamento: true, Valor: 150m, Forma: FormaPagamento.Pix));

        // "Sessão de 20/08/2026" era o extrato inteiro: dez sessões do dia, dez linhas iguais.
        resultado.Lancamento!.Descricao.Should().Contain("Ana Souza").And.Contain("20/08/2026");
    }

    // ==================== A conciliação do particular ====================

    [Fact]
    public async Task Sessao_particular_realizada_sem_dinheiro_aparece_na_conciliacao_e_o_convenio_nao()
    {
        var particular = await ParticularAsync();
        var conveniado = await ConveniadoAsync();
        var sessao = await RealizarAsync(particular);
        await RealizarAsync(conveniado);

        var pendentes = await _financeiro.SessoesParticularesSemReceitaAsync(Dia, Dia);

        var linha = pendentes.Should().ContainSingle().Subject;
        linha.AtendimentoId.Should().Be(sessao.Id);
        linha.PacienteId.Should().Be(particular);
        linha.ConvenioNome.Should().Be("Particular");
        linha.Tipo.Should().BeOneOf(TipoCodigo.Acupuntura, TipoCodigo.Eletroacupuntura);
    }

    [Fact]
    public async Task A_sessao_sai_da_conciliacao_por_passar_a_TER_lancamento_recebido_ou_a_receber()
    {
        var recebe = await ParticularAsync("Recebe");
        var deve = await ParticularAsync("Deve");
        await RealizarAsync(recebe);
        await RealizarAsync(deve);

        var pendentes = await _financeiro.SessoesParticularesSemReceitaAsync(Dia, Dia);
        pendentes.Should().HaveCount(2);

        await _financeiro.LancarReceitaDaSessaoAsync(
            pendentes.Single(p => p.PacienteId == recebe), 150m, FormaPagamento.Pix, operador: "fin");
        await _financeiro.LancarReceitaDaSessaoAsync(
            pendentes.Single(p => p.PacienteId == deve), 150m, vencimento: Dia.AddDays(30), operador: "fin");

        (await _financeiro.SessoesParticularesSemReceitaAsync(Dia, Dia)).Should().BeEmpty();

        // O "a receber" é conta do paciente, com dono e vencimento — a inadimplência a vê.
        (await _inadimplencia.DoPacienteAsync(deve, Dia.AddDays(40)))!.Total.Should().Be(150m);
        // O recebido entrou no caixa, ligado à sessão.
        (await _financeiro.ResumoAsync(Dia, Dia)).EntradasRealizadas.Should().Be(150m);
    }

    [Fact]
    public async Task Sessao_de_pacote_estornada_ou_ainda_nao_realizada_NAO_e_pendencia()
    {
        // Pacote: a sessão foi paga na compra.
        var comPacote = await ParticularAsync("Com pacote");
        var catalogo = await CatalogoAsync();
        await _pacotes.VenderAsync(comPacote, catalogo.Id, Dia.AddDays(-10),
            pagamento: PagamentoDaVenda.AVista(FormaPagamento.Pix));
        var agPacote = await AgendarAsync(comPacote);
        await _fechamento.RegistrarAtendimentoAsync(agPacote);
        await _fechamento.ConcluirAsync(new DecisaoFechamento(agPacote, DebitarPacote: true));

        // Estornada: desdita.
        var estornado = await ParticularAsync("Estornado");
        var sessao = await RealizarAsync(estornado);
        var entidade = await _db.Atendimentos.SingleAsync(a => a.Id == sessao.Id);
        entidade.EstornadoEm = DateTime.Now;
        await _db.SaveChangesAsync();

        // Só marcada (nunca realizada): não aconteceu.
        var marcado = await ParticularAsync("Marcado");
        await AgendarAsync(marcado);

        (await _financeiro.SessoesParticularesSemReceitaAsync(Dia, Dia)).Should().BeEmpty();
    }

    [Fact]
    public async Task Sessao_particular_anterior_sem_dinheiro_avisa_o_balcao_no_check_in_seguinte()
    {
        var pacienteId = await ParticularAsync();
        await RealizarAsync(pacienteId);

        // No próprio dia não: a sessão está sendo fechada agora.
        var hoje = await _elegibilidade.ConferirAsync(pacienteId, Dia);
        hoje.Alertas.Should().NotContain(a => a.Motivo == ImpedimentoElegibilidade.SessaoParticularSemReceita);

        // Na visita seguinte, sim — amarelo, com a data e a modalidade da sessão.
        var depois = await _elegibilidade.ConferirAsync(pacienteId, Dia.AddDays(7));
        var alerta = depois.Alertas.Single(a => a.Motivo == ImpedimentoElegibilidade.SessaoParticularSemReceita);
        alerta.Urgencia.Should().Be(NivelUrgencia.Amarelo);
        alerta.Descricao.Should().Contain("20/08/2026");

        // Registrado o dinheiro, o aviso some.
        var pendente = (await _financeiro.SessoesParticularesSemReceitaAsync(Dia, Dia)).Single();
        await _financeiro.LancarReceitaDaSessaoAsync(pendente, 150m, FormaPagamento.Pix);
        var resolvido = await _elegibilidade.ConferirAsync(pacienteId, Dia.AddDays(7));
        resolvido.Alertas.Should().NotContain(a => a.Motivo == ImpedimentoElegibilidade.SessaoParticularSemReceita);
    }
}
