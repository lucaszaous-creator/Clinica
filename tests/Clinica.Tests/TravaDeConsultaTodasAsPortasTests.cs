using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O caso de 18/11/2026: a Saúde Petrobras glosou uma consulta marcada 15 dias depois da anterior,
/// com a trava de consulta LIGADA e sem liberação da gerência. A causa não foi uma porta sem trava:
/// a linha da Petrobras nas regras da família estava com a validade em branco ("Não renova"), e o
/// prazo da trava vinha dessa linha — prazo nulo, trava apagada, calada.
///
/// Estes testes prendem: (1) o prazo em branco não desliga mais a trava (cai no do convênio);
/// (2) 0 desliga de propósito e um número muda o prazo; (3) a porta "lançar no horário", a única que
/// mudava médico/especialidade/modalidade de um horário sem perguntar à trava, agora pergunta;
/// (4) o que já entrou aparece na baixa como aviso de glosa, com o atendimento anterior.
/// Pacientes e profissionais fictícios.
/// </summary>
public sealed class TravaDeConsultaTodasAsPortasTests : IDisposable
{
    private static readonly DateTime Anterior = new(2026, 10, 20, 14, 30, 0);
    private static readonly DateTime Encaixe = new(2026, 11, 4, 8, 45, 0);

    private readonly TissBancoDeTeste _b = new();
    private readonly ParametrosService _parametros;
    private readonly AgendaService _agenda;

    public TravaDeConsultaTodasAsPortasTests()
    {
        _parametros = new ParametrosService(_b.Repo);
        _agenda = new AgendaService(_b.Repo, new AtendimentoService(_b.Repo, parametros: _parametros), _parametros);
    }

    public void Dispose() => _b.Dispose();

    private async Task<(Paciente Paciente, int Medico)> CenarioAsync(int? validadeNaLinhaDaFamilia, bool comLinha = true, bool travaLigada = true,
        Convenio convenio = Convenio.Petrobras)
    {
        var medico = new Profissional { Nome = "Dra. Helena Fictícia" };
        var paciente = new Paciente { Nome = "Paciente Fictício Trava", Convenio = convenio, Sexo = Sexo.Feminino, Carteirinha = TissBancoDeTeste.CarteiraFicticia };
        _b.Db.AddRange(medico, paciente);
        if (comLinha)
            _b.Db.Parametros.Add(new ParametroConvenio { Convenio = convenio, Ativo = true, ValidadeConsultaDias = validadeNaLinhaDaFamilia });
        await _b.Db.SaveChangesAsync();
        if (travaLigada) await _parametros.DefinirTravaDeConsultaAsync(true);
        return (paciente, medico.Id);
    }

    private Task<Agendamento> MarcarAsync(int paciente, DateTime quando, int? medico, bool encaixe = false)
        => _agenda.AgendarAsync(paciente, quando, ModalidadeAtendimento.Consulta, null,
            especialidadeConsultaCodigo: nameof(Especialidade.Psiquiatria), profissionalId: medico, encaixe: encaixe, operador: "recepcao");

    // ==================== a causa: "Não renova" apagava a trava ====================

    [Fact]
    public async Task Com_a_validade_da_Petrobras_em_branco_o_encaixe_15_dias_depois_e_recusado_e_pede_a_liberacao()
    {
        var (p, medico) = await CenarioAsync(validadeNaLinhaDaFamilia: null);
        await MarcarAsync(p.Id, Anterior, medico);

        var encaixe = () => MarcarAsync(p.Id, Encaixe, medico, encaixe: true);

        var recusa = (await encaixe.Should().ThrowAsync<TravaDeConsultaException>()).Which;
        recusa.Trava.PrazoDias.Should().Be(30, "linha em branco = o prazo do convênio (ConvenioInfo), não \"sem prazo\"");
        recusa.Message.Should().Contain("20/10/2026").And.Contain("peça a liberação à gerência");
        (await _b.Db.Agendamentos.CountAsync()).Should().Be(1, "nada foi gravado");
    }

    [Fact]
    public async Task O_lancamento_avulso_do_balcao_tambem_e_recusado()
    {
        var (p, medico) = await CenarioAsync(validadeNaLinhaDaFamilia: null);
        await MarcarAsync(p.Id, Anterior, medico);

        var avulso = () => _agenda.LancarAvulsoAsync(p.Id, Encaixe, ModalidadeAtendimento.Consulta, null,
            especialidadeConsultaCodigo: nameof(Especialidade.Psiquiatria), profissionalId: medico, operador: "recepcao");

        await avulso.Should().ThrowAsync<TravaDeConsultaException>();
    }

    [Fact]
    public async Task Zero_na_linha_desliga_a_trava_da_familia_e_um_numero_muda_o_prazo()
    {
        var (p, medico) = await CenarioAsync(validadeNaLinhaDaFamilia: 0);
        await MarcarAsync(p.Id, Anterior, medico);
        (await MarcarAsync(p.Id, Encaixe, medico, encaixe: true)).Id.Should().BePositive("0 é a escolha explícita de não travar");

        var linha = await _b.Db.Parametros.SingleAsync();
        linha.ValidadeConsultaDias = 10;
        await _b.Db.SaveChangesAsync();
        var snapshot = await new ParametrosService(_b.Repo).ObterAsync();
        snapshot.PrazoDaTravaDeConsulta(Convenio.Petrobras).Should().Be(10);
        snapshot.ValidadeConsultaDias(Convenio.Petrobras).Should().Be(10, "a renovação continua lendo a mesma linha");
    }

    [Fact]
    public async Task Sem_linha_da_familia_vale_o_prazo_do_convenio_e_a_renovacao_nao_muda()
    {
        var (p, medico) = await CenarioAsync(validadeNaLinhaDaFamilia: null, comLinha: false);
        await MarcarAsync(p.Id, Anterior, medico);
        await FluentActions.Awaiting(() => MarcarAsync(p.Id, Encaixe, medico, encaixe: true)).Should().ThrowAsync<TravaDeConsultaException>();

        // A renovação ("consulta a renovar") continua tratando a linha em branco como "não renova".
        var comLinhaEmBranco = new ParametrosSnapshot(new Dictionary<Convenio, ParametroConvenio>
        {
            [Convenio.Petrobras] = new() { Convenio = Convenio.Petrobras, ValidadeConsultaDias = null }
        });
        comLinhaEmBranco.ValidadeConsultaDias(Convenio.Petrobras).Should().BeNull();
        comLinhaEmBranco.PrazoDaTravaDeConsulta(Convenio.Petrobras).Should().Be(30);
    }

    [Fact]
    public async Task Com_a_chave_de_Configuracoes_desligada_nada_trava()
    {
        var (p, medico) = await CenarioAsync(validadeNaLinhaDaFamilia: null, travaLigada: false);
        await MarcarAsync(p.Id, Anterior, medico);
        (await MarcarAsync(p.Id, Encaixe, medico, encaixe: true)).Id.Should().BePositive();
    }

    // ==================== a porta que mudava o horário sem perguntar ====================

    [Fact]
    public async Task Lancar_no_horario_que_ganha_o_medico_agora_passa_pela_trava()
    {
        // Num convênio que compara por médico (Amil, 30 dias): o encaixe sem médico não tem com quem comparar.
        var (p, medico) = await CenarioAsync(validadeNaLinhaDaFamilia: null, convenio: Convenio.Amil);
        await MarcarAsync(p.Id, Anterior, medico);
        // O encaixe sem profissional nasce sem trava (não há com quem comparar)…
        var semMedico = await MarcarAsync(p.Id, Encaixe, medico: null, encaixe: true);

        // …e ganhar o médico ao lançar é passar a disputar o prazo dele.
        var lancar = () => _agenda.LancarNoHorarioAsync(semMedico.Id, null, profissionalId: medico, operador: "recepcao");

        (await lancar.Should().ThrowAsync<TravaDeConsultaException>()).Which.Message.Should().Contain("peça a liberação à gerência");
        _b.Db.ChangeTracker.Clear();
        (await _b.Db.Agendamentos.SingleAsync(a => a.Id == semMedico.Id)).ProfissionalId.Should().BeNull("nada foi gravado");
    }

    [Fact]
    public async Task Lancar_no_horario_ja_marcado_com_o_mesmo_medico_nao_e_conferido_de_novo()
    {
        var (p, medico) = await CenarioAsync(validadeNaLinhaDaFamilia: 0);
        await MarcarAsync(p.Id, Anterior, medico);
        var marcado = await MarcarAsync(p.Id, Encaixe, medico);
        // A trava liga depois da marcação: o horário que já existia é lançado como sempre.
        var linha = await _b.Db.Parametros.SingleAsync();
        linha.ValidadeConsultaDias = null;
        await _b.Db.SaveChangesAsync();

        var (ag, _) = await new AgendaService(_b.Repo, new AtendimentoService(_b.Repo, parametros: new ParametrosService(_b.Repo)), new ParametrosService(_b.Repo))
            .LancarNoHorarioAsync(marcado.Id, null, operador: "recepcao");
        ag.Status.Should().Be(StatusAgendamento.Realizado);
    }

    // ==================== o que já entrou: aviso na baixa ====================

    [Fact]
    public async Task Na_baixa_a_consulta_a_menos_de_30_dias_da_anterior_vem_com_o_aviso_de_glosa_e_o_atendimento_anterior()
    {
        var (p, medico) = await CenarioAsync(validadeNaLinhaDaFamilia: null, travaLigada: false);
        var primeira = await MarcarAsync(p.Id, Anterior, medico);
        var segunda = await MarcarAsync(p.Id, Encaixe, medico, encaixe: true);
        await AtenderAsync(primeira, "2026-000101");
        var guia = await AtenderAsync(segunda, "2026-000102");

        var proposta = await new GuiaAnsNaBaixaService(_b.Repo, new ParametrosService(_b.Repo), new PrecoConvenioService(_b.Repo), _b.Credenciais(),
            new EnvioTissLigadoNaPrevia(new HashSet<Convenio> { Convenio.Petrobras })).PrepararAsync(guia.Id);

        proposta.Exige.Should().BeTrue();
        proposta.Avisos.Should().Contain(a => a.StartsWith($"Consulta {p.ConvenioNome} a menos de 30 dias da anterior (atendimento 2026-000101 em 20/10) — risco de glosa", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Na_baixa_a_primeira_consulta_e_a_de_outra_especialidade_nao_tem_aviso_e_outro_psiquiatra_tem()
    {
        var (p, medico) = await CenarioAsync(validadeNaLinhaDaFamilia: null, travaLigada: false);
        var outro = await OutroMedicoAsync();
        var primeira = await MarcarAsync(p.Id, Anterior, medico);
        var outraEspecialidade = await _agenda.AgendarAsync(p.Id, Encaixe.AddHours(1), ModalidadeAtendimento.Consulta, null,
            especialidadeConsultaCodigo: nameof(Especialidade.Endocrinologia), profissionalId: outro, encaixe: true, operador: "recepcao");
        var outroPsiquiatra = await MarcarAsync(p.Id, Encaixe, outro, encaixe: true);
        var guiaDaPrimeira = await AtenderAsync(primeira, "2026-000201");
        var guiaDaOutraEspecialidade = await AtenderAsync(outraEspecialidade, "2026-000202");
        var guiaDoOutroPsiquiatra = await AtenderAsync(outroPsiquiatra, "2026-000203");
        var servico = new GuiaAnsNaBaixaService(_b.Repo, new ParametrosService(_b.Repo), new PrecoConvenioService(_b.Repo), _b.Credenciais(),
            new EnvioTissLigadoNaPrevia(new HashSet<Convenio> { Convenio.Petrobras }));

        (await servico.PrepararAsync(guiaDaPrimeira.Id)).Avisos.Should().NotContain(a => a.Contains("risco de glosa", StringComparison.Ordinal));
        (await servico.PrepararAsync(guiaDaOutraEspecialidade.Id)).Avisos.Should().NotContain(a => a.Contains("risco de glosa", StringComparison.Ordinal));
        (await servico.PrepararAsync(guiaDoOutroPsiquiatra.Id)).Avisos.Should().Contain(a => a.Contains("(atendimento 2026-000201 em 20/10) — risco de glosa", StringComparison.Ordinal),
            "na Petrobras o prazo é por especialidade, com qualquer médico");
    }

    // ==================== o critério da janela é do convênio (adendo do dono, 18/11) ====================

    [Fact]
    public async Task Na_Petrobras_outro_psiquiatra_dentro_dos_30_dias_e_barrado_e_outra_especialidade_passa()
    {
        var (p, medicoA) = await CenarioAsync(validadeNaLinhaDaFamilia: null);
        var medicoB = await OutroMedicoAsync();
        await MarcarAsync(p.Id, Anterior, medicoA);

        var mesmaEspecialidadeOutroMedico = () => MarcarAsync(p.Id, Encaixe, medicoB);
        var recusa = (await mesmaEspecialidadeOutroMedico.Should().ThrowAsync<TravaDeConsultaException>()).Which;
        recusa.Trava.Profissional.Should().BeNull("na Petrobras o médico não é parte do que trava: a frase fala da especialidade");
        recusa.Message.Should().Contain("Psiquiatria");

        var outraEspecialidade = await _agenda.AgendarAsync(p.Id, Encaixe, ModalidadeAtendimento.Consulta, null,
            especialidadeConsultaCodigo: nameof(Especialidade.Endocrinologia), profissionalId: medicoB, operador: "recepcao");
        outraEspecialidade.Id.Should().BePositive();

        ConvenioInfo.JanelaDaTravaDeConsulta(Convenio.Petrobras).Should().Be(JanelaDaTravaDeConsulta.PorEspecialidade);
        ConvenioInfo.JanelaDaTravaDeConsulta(Convenio.Amil).Should().Be(JanelaDaTravaDeConsulta.PorEspecialidadeEProfissional, "quem já usa o critério antigo continua com ele");
    }

    [Fact]
    public async Task Na_Petrobras_o_encaixe_sem_medico_ja_e_barrado_porque_o_medico_nao_e_preciso_para_comparar()
    {
        var (p, medico) = await CenarioAsync(validadeNaLinhaDaFamilia: null);
        await MarcarAsync(p.Id, Anterior, medico);
        await FluentActions.Awaiting(() => MarcarAsync(p.Id, Encaixe, medico: null, encaixe: true)).Should().ThrowAsync<TravaDeConsultaException>();
    }

    private async Task<int> OutroMedicoAsync()
    {
        var outro = new Profissional { Nome = "Dr. Outro Fictício" };
        _b.Db.Add(outro);
        await _b.Db.SaveChangesAsync();
        return outro.Id;
    }

    [Fact]
    public void A_tela_de_regras_diz_o_prazo_da_trava_quando_a_validade_esta_em_branco()
    {
        ConfiguracoesFaturamentoNaTela.TravaComValidadeEmBranco(Convenio.Petrobras, null).Should().Be("Trava: 30 dias");
        ConfiguracoesFaturamentoNaTela.TravaComValidadeEmBranco(Convenio.Petrobras, 20).Should().BeNull("o número da linha é o que vale");
        ConfiguracoesFaturamentoNaTela.TravaComValidadeEmBranco(Convenio.Personalizado, null).Should().BeNull();
        ConfiguracoesFaturamentoNaTela.NotaDaValidadeDaConsulta.Should().Contain("escreva 0");
    }

    /// <summary>O horário vira atendimento realizado, com número e a guia de consulta (o atalho dos testes de TISS).</summary>
    private async Task<CodigoFaturamento> AtenderAsync(Agendamento horario, string numero)
    {
        var ag = await _b.Db.Agendamentos.SingleAsync(x => x.Id == horario.Id);
        var atendimento = new Atendimento { PacienteId = ag.PacienteId, Data = DateOnly.FromDateTime(ag.DataHora), Modalidade = ModalidadeAtendimento.Consulta, Numero = numero };
        _b.Db.Add(atendimento);
        await _b.Db.SaveChangesAsync();
        ag.AtendimentoId = atendimento.Id;
        ag.Status = StatusAgendamento.Realizado;
        var guia = new CodigoFaturamento { AtendimentoId = atendimento.Id, Tipo = TipoCodigo.Consulta, Ordem = OrdemCodigo.Primeiro, DataPrevistaFaturamento = atendimento.Data };
        _b.Db.Add(guia);
        await _b.Db.SaveChangesAsync();
        _b.Db.ChangeTracker.Clear();
        return guia;
    }
}
