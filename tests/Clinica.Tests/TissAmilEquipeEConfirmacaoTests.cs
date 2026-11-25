using System.Xml.Linq;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A GUIA SP/SADT DA AMIL, campo a campo contra os manuais e a lista de glosas (Shared/Convenios/amil/08,
/// nov/2026): a EQUIPE com o profissional executante pelo CPF (1203/992), a CONFIRMAÇÃO pelo TOKEN ou
/// nenhuma (3096, 991 — decisão do dono: a Amil é faturada só pelo sistema, com o token), as HORAS, o número da guia na operadora, a DUPLICIDADE (1807/882) e o prazo de
/// 90 dias como BLOQUEIO (1701/405). Só dados fictícios; os CPFs são gerados válidos, de ninguém.
/// </summary>
public sealed class TissAmilEquipeEConfirmacaoTests : IDisposable
{
    private static readonly XNamespace Ans = "http://www.ans.gov.br/padroes/tiss/schemas";
    private static readonly DateOnly Dia = new(2026, 11, 3);
    private const string Login = "000123";
    private static readonly string CpfFicticio = CpfValido("123456789");

    private readonly TissBancoDeTeste _b = new();

    public TissAmilEquipeEConfirmacaoTests() => _b.Agora = new DateTime(2026, 11, 3, 10, 0, 0);

    public void Dispose() => _b.Dispose();

    private static DadosGuiaConsultaAns GuiaAmil() => new()
    {
        Modelo = ModeloDaGuiaTiss.SpSadt, ExigeEquipe = true,
        RegistroAns = "326305", NumeroCarteira = TissBancoDeTeste.CarteiraFicticia, NomeBeneficiario = "Paciente de Teste",
        CodigoPrestadorNaOperadora = Login, NomeContratado = "Clínica de Teste Ltda", Cnes = "9999999",
        NomeProfissional = "Dra. Ana de Teste", ConselhoProfissional = "06", NumeroConselho = "123456", UfConselho = "33", Cbos = "225105",
        IndicacaoAcidente = "9", RegimeAtendimento = "01", DataAtendimento = Dia, CodigoTabela = "22",
        TipoConsulta = "1", CodigoProcedimento = "10101012", ValorProcedimento = 139.00m,
        Senha = "SENHA0001", DataAutorizacao = Dia, FormaConfirmacao = GuiaAnsNaBaixa.ConfirmacaoToken, CodigoValidacao = "123456",
        TipoAtendimento = "04", CaraterAtendimento = "1", DescricaoProcedimento = "Consulta em consultório",
        EquipeCpf = CpfFicticio, GrauParticipacao = DominiosTiss.GrauClinico,
        HoraInicial = new TimeOnly(14, 30), HoraFinal = new TimeOnly(15, 0)
    };

    // ==================== a equipe ====================

    [Fact]
    public void Sem_CPF_nem_codigo_do_profissional_a_guia_da_Amil_nao_passa_e_a_frase_diz_quem_cadastrar()
    {
        OperadorasTiss.Amil.EquipeComProfissional.Should().BeTrue();
        OperadorasTiss.SaudePetrobras.EquipeComProfissional.Should().BeFalse("a Petrobras não muda");

        GuiaAnsNaBaixa.Validar(GuiaAmil() with { EquipeCpf = null })[nameof(DadosGuiaConsultaAns.EquipeCpf)]
            .Should().Contain("Cadastre o CPF de Dra. Ana de Teste").And.Contain("pelo CPF").And.Contain("1203");
        GuiaAnsNaBaixa.Validar(GuiaAmil() with { EquipeCpf = "11111111111" }).Should().ContainKey(nameof(DadosGuiaConsultaAns.EquipeCpf), "CPF inválido");
        GuiaAnsNaBaixa.Validar(GuiaAmil()).Should().BeEmpty();
        GuiaAnsNaBaixa.Validar(GuiaAmil() with { EquipeCpf = null, ExigeEquipe = false }).Should().BeEmpty("quem não exige a equipe não a pede");
        GuiaAnsNaBaixa.Validar(GuiaAmil() with { GrauParticipacao = "99" }).Should().ContainKey(nameof(DadosGuiaConsultaAns.GrauParticipacao));
    }

    [Fact]
    public void A_equipe_vai_no_XML_pelo_CPF_e_valida_no_XSD_4_03_00()
    {
        var comCpf = Guia(GuiaAmil());
        var equipe = comCpf.Descendants(Ans + "equipeSadt").Single();
        equipe.Element(Ans + "grauPart")!.Value.Should().Be("12");
        equipe.Descendants(Ans + "cpfContratado").Single().Value.Should().Be(CpfFicticio);
        equipe.Element(Ans + "nomeProf")!.Value.Should().Be("Dra. Ana de Teste");
        equipe.Element(Ans + "CBOS")!.Value.Should().Be("225105");
        Valida(comCpf);


        Guia(GuiaAmil() with { EquipeCpf = null, ExigeEquipe = false }).Descendants(Ans + "equipeSadt").Should().BeEmpty();
    }

    // ==================== a confirmação ====================

    [Fact]
    public void So_ha_token_ou_nao_confirmou_e_o_token_tem_exatamente_seis_digitos()
    {
        GuiaAnsNaBaixa.FormasDeConfirmacao.Select(f => f.Codigo).Should().Equal(GuiaAnsNaBaixa.ConfirmacaoToken, GuiaAnsNaBaixa.ConfirmacaoNenhuma);
        GuiaAnsNaBaixa.Validar(GuiaAmil() with { CodigoValidacao = "12345" })[nameof(DadosGuiaConsultaAns.CodigoValidacao)].Should().Contain("6 dígitos");
        GuiaAnsNaBaixa.Validar(GuiaAmil() with { CodigoValidacao = "1234567" }).Should().ContainKey(nameof(DadosGuiaConsultaAns.CodigoValidacao));
        GuiaAnsNaBaixa.Validar(GuiaAmil() with { CodigoValidacao = "12345a" }).Should().ContainKey(nameof(DadosGuiaConsultaAns.CodigoValidacao));
        // Uma forma que saiu (QR Code) não é aceita: a guia não passa.
        GuiaAnsNaBaixa.Validar(GuiaAmil() with { FormaConfirmacao = "qrcode" }).Should().ContainKey(nameof(DadosGuiaConsultaAns.FormaConfirmacao));
        Guia(GuiaAmil()).Descendants(Ans + "codValidacao").Single().Value.Should().Be("123456");
    }

    [Fact]
    public void Os_motivos_sem_token_sao_os_de_consultorio_com_o_codigo_do_XSD()
    {
        GuiaAnsNaBaixa.MotivosSemToken.Select(m => m.Codigo).Should().Equal("07", "03", "04");
        GuiaAnsNaBaixa.MotivosSemToken.Should().OnlyContain(m => DominiosTiss.Contem(DominiosTiss.AusenciaCodigoValidacao, m.Codigo), "sem código inventado");
        var semToken = GuiaAmil() with { FormaConfirmacao = GuiaAnsNaBaixa.ConfirmacaoNenhuma, CodigoValidacao = null, AnexosNoPortal = true };
        GuiaAnsNaBaixa.Validar(semToken with { AusenciaCodigoValidacao = "01" }).Should().ContainKey(nameof(DadosGuiaConsultaAns.AusenciaCodigoValidacao),
            "internado não é motivo de consultório");
        GuiaAnsNaBaixa.Validar(semToken with { AusenciaCodigoValidacao = "07" }).Should().BeEmpty();
    }

    [Fact]
    public void Removido_QR_Code_ou_rosto_nao_existem_mais()
        => GuiaAnsNaBaixa.FormasDeConfirmacao.Should().NotContain(f => f.Termo.Contains("QR") || f.Termo.Contains("facial"));

    [Fact]
    public void Sem_confirmacao_exige_o_motivo_e_os_anexos_no_portal_e_sem_forma_nenhuma_nao_passa()
    {
        var nao = GuiaAmil() with { FormaConfirmacao = GuiaAnsNaBaixa.ConfirmacaoNenhuma, CodigoValidacao = null };
        var erros = GuiaAnsNaBaixa.Validar(nao);
        erros.Should().ContainKey(nameof(DadosGuiaConsultaAns.AusenciaCodigoValidacao)).And.ContainKey(nameof(DadosGuiaConsultaAns.AnexosNoPortal));
        erros[nameof(DadosGuiaConsultaAns.AnexosNoPortal)].Should().Contain("991");
        var completa = nao with { AusenciaCodigoValidacao = "07", AnexosNoPortal = true };
        GuiaAnsNaBaixa.Validar(completa).Should().BeEmpty();
        var xml = Guia(completa);
        xml.Descendants(Ans + "ausenciaCodValidacao").Single().Value.Should().Be("07");
        xml.Descendants(Ans + "codValidacao").Should().BeEmpty();

        GuiaAnsNaBaixa.Validar(GuiaAmil() with { FormaConfirmacao = null, CodigoValidacao = null })
            .Should().ContainKey(nameof(DadosGuiaConsultaAns.FormaConfirmacao));
    }

    // ==================== horas e número da operadora ====================

    [Fact]
    public void As_horas_e_o_numero_da_guia_na_operadora_vao_quando_existem()
    {
        var codigo = new CodigoFaturamento { Id = 41, NumeroGuiaReal = "AMX123" };
        GuiaAnsNaBaixa.Aplicar(codigo, GuiaAmil(), "faturista.teste", DateTime.Now);
        var xml = TissExportService.GuiaConferida(codigo, "C41");
        xml.Descendants(Ans + "numeroGuiaOperadora").Single().Value.Should().Be("AMX123");
        xml.Descendants(Ans + "horaInicial").Single().Value.Should().Be("14:30:00");
        xml.Descendants(Ans + "horaFinal").Single().Value.Should().Be("15:00:00");
        Valida(xml);

        var semNada = Guia(GuiaAmil() with { HoraInicial = null, HoraFinal = null });
        semNada.Descendants(Ans + "horaInicial").Should().BeEmpty();
        semNada.Descendants(Ans + "numeroGuiaOperadora").Should().BeEmpty();
        GuiaAnsNaBaixa.Validar(GuiaAmil() with { HoraFinal = new TimeOnly(14, 0) }).Should().ContainKey(nameof(DadosGuiaConsultaAns.HoraFinal));
    }

    [Fact]
    public void Gravar_e_reler_preserva_a_equipe_a_confirmacao_e_as_horas()
    {
        var codigo = new CodigoFaturamento { Id = 42 };
        var dados = GuiaAmil() with { FormaConfirmacao = GuiaAnsNaBaixa.ConfirmacaoNenhuma, CodigoValidacao = null, AusenciaCodigoValidacao = "07", AnexosNoPortal = true };
        GuiaAnsNaBaixa.Aplicar(codigo, dados, "faturista.teste", DateTime.Now);
        GuiaAnsNaBaixa.DaGuia(codigo.GuiaAns!).Should().BeEquivalentTo(dados, o => o
            .Excluding(d => d.ValidadeCarteira).Excluding(d => d.NomeSocial).Excluding(d => d.Observacao).Excluding(d => d.ValidadeSenha));
        var edicao = GuiaAnsEmEdicao.De(dados);
        edicao.FormaEscolhida.Should().Be(GuiaAnsNaBaixa.ConfirmacaoNenhuma);
        edicao.AnexosNoPortal = false;
        edicao.Erros().Should().ContainKey(nameof(DadosGuiaConsultaAns.AnexosNoPortal));
    }

    // ==================== a proposta da baixa ====================

    [Fact]
    public async Task A_proposta_traz_o_CPF_do_profissional_e_as_horas_do_consultorio_ou_da_agenda()
    {
        var (codigoId, profissional) = await GuiaAmilComHorarioAsync(inicioReal: null);
        await using (var db = _b.NovoContexto())
        {
            var p = await db.Profissionais.SingleAsync(x => x.Id == profissional);
            p.Cpf = CpfFicticio;
            await db.SaveChangesAsync();
        }

        var proposta = await Proposta().PrepararAsync(codigoId);
        proposta.Exige.Should().BeTrue();
        proposta.Dados!.ExigeEquipe.Should().BeTrue();
        proposta.Dados.EquipeCpf.Should().Be(CpfFicticio);
        proposta.Dados.FormaConfirmacao.Should().Be(GuiaAnsNaBaixa.ConfirmacaoToken);
        proposta.Dados.HoraInicial.Should().Be(new TimeOnly(14, 30));
        proposta.Avisos.Should().Contain(a => a.Contains("horário marcado na agenda"));

        // Com início e fim do consultório, as horas são as reais.
        var (outro, _) = await GuiaAmilComHorarioAsync(inicioReal: new DateTime(2026, 11, 3, 14, 40, 0), profissionalId: profissional);
        var segunda = await Proposta().PrepararAsync(outro);
        segunda.Dados!.EquipeCpf.Should().Be(CpfFicticio);
        segunda.Dados.HoraInicial.Should().Be(new TimeOnly(14, 40));
        segunda.Dados.HoraFinal.Should().Be(new TimeOnly(15, 10));
        segunda.Avisos.Should().Contain(a => a.Contains("registrados no consultório"));

        // Sem a prévia, a Amil desligada não pede a conferência (o app de hoje).
        (await Proposta(previa: false).PrepararAsync(outro)).Exige.Should().BeFalse();
    }

    [Fact]
    public void Nenhum_app_registra_a_Amil_ligada_so_na_previa()
    {
        var raiz = new DirectoryInfo(AppContext.BaseDirectory);
        while (raiz is not null && !File.Exists(Path.Combine(raiz.FullName, "Clinica.sln"))) raiz = raiz.Parent;
        var quemRegistra = Directory.GetFiles(Path.Combine(raiz!.FullName, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(f => File.ReadAllText(f).Contains("new EnvioTissLigadoNaPrevia(") || File.ReadAllText(f).Contains("new Clinica.Application.Servicos.EnvioTissLigadoNaPrevia("))
            .ToList();
        quemRegistra.Should().BeEmpty("só a prévia (tools/PreviaBlazor) e os testes mostram a baixa da Amil antes de ela ser ligada");
    }

    // ==================== o lote: duplicidade e 90 dias ====================

    [Fact]
    public async Task A_mesma_cobranca_no_mesmo_lote_ou_ja_enviada_a_Amil_nao_entra()
    {
        var a = await GuiaAmilNoBancoAsync(Dia);
        var b = await GuiaAmilNoBancoAsync(Dia); // mesmo paciente fictício (carteira), procedimento e dia

        var noMesmoLote = await _b.Fila().ConferirEnvioAsync("Amil", [a, b]);
        noMesmoLote.PorGuia.Should().ContainKey(b).WhoseValue.Should().Contain("1807");
        noMesmoLote.PorGuia.Should().NotContainKey(a);

        await _b.Credenciais().SalvarAsync("Amil", Login, "senha-ficticia", TissBancoDeTeste.Operador);
        await _b.Fila().EnfileirarEnvioAsync("Amil", [a], TissBancoDeTeste.Operador);
        (await _b.Fila().ConferirEnvioAsync("Amil", [b])).PorGuia[b].Should().Contain("Já há guia deste paciente").And.Contain($"guia {a}");

        // Outro dia: não é duplicidade.
        var c = await GuiaAmilNoBancoAsync(Dia.AddDays(-1));
        (await _b.Fila().ConferirEnvioAsync("Amil", [c])).PodeEnviar.Should().BeTrue();
    }

    [Fact]
    public async Task Na_Amil_a_guia_vencida_nao_entra_no_lote_e_antes_disso_e_aviso_vermelho()
    {
        _b.Agora = new DateTime(2026, 11, 17, 10, 0, 0);
        var vencida = await GuiaAmilNoBancoAsync(new DateOnly(2026, 8, 1));      // venceu em 30/10
        var vencendo = await GuiaAmilNoBancoAsync(new DateOnly(2026, 8, 25));    // vence em 23/11, antes de 01/12

        var conferencia = await _b.Fila().ConferirEnvioAsync("Amil", [vencida, vencendo]);

        conferencia.PorGuia[vencida].Should().Contain("Passou o prazo de cobrança da Amil").And.Contain("1701").And.Contain("não entra no lote");
        conferencia.PorGuia.Should().NotContainKey(vencendo);
        conferencia.PrazoGrave.Should().Contain(vencendo);
        PrazosTiss.BloqueadaPeloPrazo(OperadorasTiss.SaudePetrobras, new DateOnly(2026, 1, 1), DateOnly.FromDateTime(_b.Agora)).Should().BeFalse("na Petrobras nada muda");
    }

    // ==================== peças ====================

    private XElement Guia(DadosGuiaConsultaAns dados)
    {
        var codigo = new CodigoFaturamento { Id = 40 };
        GuiaAnsNaBaixa.Aplicar(codigo, dados, "faturista.teste", DateTime.Now);
        return TissExportService.GuiaConferida(codigo, "C40");
    }

    private static void Valida(XElement guia)
    {
        var mensagem = TissLoteGuiasWs.MontarPedido(new PedidoLoteGuiasTiss
        {
            Remetente = new RemetenteTissWs { RegistroAnsOperadora = "326305", CodigoPrestadorNaOperadora = Login, Login = Login,
                SenhaMd5 = TissElegibilidadeWs.SenhaEmMd5("senha-ficticia"), VersaoTiss = "4.03.00" },
            Sequencial = "000000001001", Momento = new DateTime(2026, 11, 3, 10, 0, 0), NumeroLote = "900000000040", Guias = [guia]
        });
        TissWsDeTeste.ValidarNoXsd(mensagem, "4.03.00").Should().BeEmpty();
    }

    private GuiaAnsNaBaixaService Proposta(bool previa = true)
    {
        var repo = new ClinicaRepositorio(_b.NovoContexto());
        return new GuiaAnsNaBaixaService(repo, new ParametrosService(repo, _b.Protecao), new PrecoConvenioService(repo), _b.Credenciais(repo),
            previa ? new EnvioTissLigadoNaPrevia(new HashSet<Convenio> { Convenio.Amil }) : null);
    }

    /// <summary>Uma guia Amil pendente, de um atendimento com horário às 14:30 (30 min) e um profissional fictício.</summary>
    private async Task<(int CodigoId, int ProfissionalId)> GuiaAmilComHorarioAsync(DateTime? inicioReal, int? profissionalId = null)
    {
        await using var db = _b.NovoContexto();
        var profissional = profissionalId is { } id ? await db.Profissionais.SingleAsync(p => p.Id == id)
            : new Profissional { Nome = "Dra. Ana de Teste", NomeCurto = "Dra. Ana", ConselhoProfissional = "06", NumeroConselho = "123456", UfConselho = "33", Cbos = "225105" };
        if (profissionalId is null) db.Profissionais.Add(profissional);
        var paciente = new Paciente { Nome = "Paciente de Teste", Convenio = Convenio.Amil, ConvenioCodigo = "Amil", Carteirinha = TissBancoDeTeste.CarteiraFicticia };
        db.Pacientes.Add(paciente);
        await db.SaveChangesAsync();
        var atendimento = new Atendimento { PacienteId = paciente.Id, Data = Dia, Modalidade = ModalidadeAtendimento.Consulta };
        db.Atendimentos.Add(atendimento);
        await db.SaveChangesAsync();
        db.Agendamentos.Add(new Agendamento
        {
            PacienteId = paciente.Id, ProfissionalId = profissional.Id, AtendimentoId = atendimento.Id, DataHora = Dia.ToDateTime(new TimeOnly(14, 30)),
            DuracaoMinutos = 30, ModalidadePrevista = ModalidadeAtendimento.Consulta, CriadoPor = "teste",
            InicioAtendimentoEm = inicioReal, FimAtendimentoEm = inicioReal?.AddMinutes(30)
        });
        var codigo = new CodigoFaturamento { AtendimentoId = atendimento.Id, Tipo = TipoCodigo.Consulta, Ordem = OrdemCodigo.Primeiro, DataPrevistaFaturamento = Dia };
        db.Codigos.Add(codigo);
        await db.SaveChangesAsync();
        return (codigo.Id, profissional.Id);
    }

    private async Task<int> GuiaAmilNoBancoAsync(DateOnly atendidoEm)
    {
        var (_, guia) = await _b.GuiaAsync(conferida: false, convenio: Convenio.Amil);
        await using var db = _b.NovoContexto();
        var codigo = await db.Codigos.SingleAsync(c => c.Id == guia.Id);
        GuiaAnsNaBaixa.Aplicar(codigo, GuiaAmil() with { DataAtendimento = atendidoEm, DataAutorizacao = atendidoEm }, TissBancoDeTeste.Operador, _b.Agora);
        await db.SaveChangesAsync();
        return guia.Id;
    }

    /// <summary>Um CPF com os dígitos verificadores certos a partir de nove dígitos inventados.</summary>
    private static string CpfValido(string nove)
    {
        int Dv(string n, int peso) { var s = n.Select((c, i) => (c - '0') * (peso - i)).Sum() % 11; return s < 2 ? 0 : 11 - s; }
        var d1 = Dv(nove, 10);
        return nove + d1 + Dv(nove + d1, 11);
    }
}
