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
/// A EVOLUÇÃO DE ENFERMAGEM (parcela 71) — o registro de quem executa.
///
/// A clínica disse que <b>todo paciente passa pela enfermagem</b>, e é isso que estes
/// testes fixam antes de qualquer tela: o dono é o PACIENTE, a folha de infusão é
/// procedência opcional, e as regras de segurança são as mesmas da checagem — hora
/// informada, futuro recusado, nada se apaga.
/// </summary>
public class EvolucaoEnfermagemTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly EvolucaoEnfermagemService _servico;

    /// <summary>Relógio congelado ao MEIO-DIA: a recusa de hora futura precisa de um "agora"
    /// conhecido, senão o teste vira loteria perto da meia-noite.</summary>
    private static DateTime MeioDia => DateTime.Today.AddHours(12);
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    private static readonly IdentificacaoExecutante Tecnica =
        new(UsuarioId: null, Nome: "Joana Técnica", Conselho: "COREN-SP 999999");

    public EvolucaoEnfermagemTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(
            new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _servico = new EvolucaoEnfermagemService(_repo, () => MeioDia);
    }

    private async Task<int> PacienteAsync()
    {
        var p = new Paciente { Nome = "Maria de Teste", Convenio = Convenio.UnimedIntercambio };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    // ==================== O registro ====================

    [Fact]
    public async Task Registra_com_a_hora_do_FATO_e_o_relogio_ao_lado()
    {
        var pacienteId = await PacienteAsync();

        var e = await _servico.RegistrarAsync(
            pacienteId, Hoje, new TimeOnly(9, 20),
            "Paciente admitido, acesso em MSD, sem queixas.", Tecnica);

        e.Hora.Should().Be(new TimeOnly(9, 20), "é a hora em que a técnica observou");
        e.RegistradoEm.Should().Be(MeioDia,
            "o relógio fica AO LADO, e a diferença entre os dois é o que uma auditoria "
            + "de enfermagem procura — e é o relógio INJETADO, senão a asserção mede a "
            + "hora da máquina que roda o teste");
        e.AutorNome.Should().Be("Joana Técnica");
        e.AutorConselho.Should().Be("COREN-SP 999999",
            "evolução de enfermagem sem o registro no conselho não é evolução de enfermagem");
    }

    [Fact]
    public async Task O_paciente_e_o_dono_e_a_folha_e_opcional()
    {
        var pacienteId = await PacienteAsync();

        // Curativo, sala de observação, triagem: todo paciente passa pela enfermagem, e a
        // maioria dessas passagens não tem folha de infusão nenhuma.
        var e = await _servico.RegistrarAsync(
            pacienteId, Hoje, new TimeOnly(8, 0), "Curativo trocado, ferida limpa.", Tecnica);

        e.PacienteId.Should().Be(pacienteId);
        e.PrescricaoInternaId.Should().BeNull();

        (await _servico.DoPacienteAsync(pacienteId)).Should().ContainSingle();
    }

    [Fact]
    public async Task Registro_em_branco_e_recusado()
    {
        var pacienteId = await PacienteAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _servico.RegistrarAsync(pacienteId, Hoje, new TimeOnly(9, 0), "   ", Tecnica));

        erro.Message.Should().Contain("Escreva o que foi observado");
    }

    /// <summary>
    /// ⚠️ Regra de SEGURANÇA, não de formulário: registrar adiantado é o hábito que faz
    /// aparecer como observado um paciente que saiu antes.
    /// </summary>
    [Fact]
    public async Task Hora_no_futuro_e_recusada()
    {
        var pacienteId = await PacienteAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _servico.RegistrarAsync(
                pacienteId, Hoje, new TimeOnly(14, 0), "Sem queixas.", Tecnica));

        erro.Message.Should().Contain("está no futuro");
    }

    [Fact]
    public async Task Registro_atrasado_de_ontem_e_legitimo()
    {
        var pacienteId = await PacienteAsync();

        // A técnica não conseguiu digitar no dia. O fato continua sendo de ontem.
        var e = await _servico.RegistrarAsync(
            pacienteId, Hoje.AddDays(-1), new TimeOnly(16, 0), "Alta sem intercorrências.", Tecnica);

        e.Data.Should().Be(Hoje.AddDays(-1));
        e.AtrasoDoRegistro.Should().BeGreaterThan(TimeSpan.FromHours(12));
    }

    // ==================== Sinais vitais ====================

    [Fact]
    public async Task Grava_e_resume_os_sinais_vitais()
    {
        var pacienteId = await PacienteAsync();

        var e = await _servico.RegistrarAsync(
            pacienteId, Hoje, new TimeOnly(9, 0), "Admissão.", Tecnica,
            sinais: new SinaisVitais(
                PressaoSistolica: 120, PressaoDiastolica: 80,
                FrequenciaCardiaca: 78, Temperatura: 36.4m,
                SaturacaoOxigenio: 97, Dor: 3));

        e.PressaoArterial.Should().Be("120x80");
        e.TemSinaisVitais.Should().BeTrue();
        e.SinaisVitaisResumidos.Should().Be("PA 120x80 · FC 78 · T 36,4 °C · SpO₂ 97% · dor 3/10",
            "o resumo é GRAVADO e impresso — pt-BR fixo, nunca a cultura da máquina");
    }

    [Fact]
    public async Task Sem_aferir_nada_o_resumo_e_NULO_e_nao_zero()
    {
        var pacienteId = await PacienteAsync();

        var e = await _servico.RegistrarAsync(
            pacienteId, Hoje, new TimeOnly(9, 0), "Só conversa.", Tecnica);

        e.TemSinaisVitais.Should().BeFalse();
        e.SinaisVitaisResumidos.Should().BeNull("nulo e zero são coisas diferentes");
    }

    /// <summary>
    /// ⚠️ A única recusa é a IMPLAUSIBILIDADE, nunca a anormalidade — a regra do
    /// CatalogoMedidas. FC 180 é taquicardia e existe; FC 1800 é dedo no teclado.
    /// </summary>
    [Theory]
    [InlineData(180, null, "taquicardia é anormal e POSSÍVEL")]
    [InlineData(40, null, "bradicardia também")]
    public async Task Frequencia_anormal_mas_possivel_passa(int fc, int? _, string porque)
    {
        var pacienteId = await PacienteAsync();

        var e = await _servico.RegistrarAsync(
            pacienteId, Hoje, new TimeOnly(9, 0), "Aferição.", Tecnica,
            sinais: new SinaisVitais(FrequenciaCardiaca: fc));

        e.FrequenciaCardiaca.Should().Be(fc, porque);
    }

    [Fact]
    public async Task Frequencia_implausivel_e_recusada()
    {
        var pacienteId = await PacienteAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _servico.RegistrarAsync(
                pacienteId, Hoje, new TimeOnly(9, 0), "Aferição.", Tecnica,
                sinais: new SinaisVitais(FrequenciaCardiaca: 1800)));

        erro.Message.Should().Contain("não é plausível");
    }

    [Fact]
    public async Task Meia_pressao_arterial_nao_existe()
    {
        var pacienteId = await PacienteAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _servico.RegistrarAsync(
                pacienteId, Hoje, new TimeOnly(9, 0), "Aferição.", Tecnica,
                sinais: new SinaisVitais(PressaoSistolica: 120)));

        erro.Message.Should().Contain("os dois números");
    }

    [Fact]
    public async Task Diastolica_maior_que_a_sistolica_e_campo_trocado()
    {
        var pacienteId = await PacienteAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _servico.RegistrarAsync(
                pacienteId, Hoje, new TimeOnly(9, 0), "Aferição.", Tecnica,
                sinais: new SinaisVitais(PressaoSistolica: 80, PressaoDiastolica: 120)));

        erro.Message.Should().Contain("trocados");
    }

    // ==================== Corrigir sem apagar ====================

    [Fact]
    public async Task Retificar_grava_linha_NOVA_e_a_anterior_FICA()
    {
        var pacienteId = await PacienteAsync();
        var original = await _servico.RegistrarAsync(
            pacienteId, Hoje, new TimeOnly(9, 0), "PA 120x80.", Tecnica);

        var correcao = await _servico.RetificarAsync(
            original.Id, Hoje, new TimeOnly(9, 30), "PA 130x85.", Tecnica,
            motivoRetificacao: "hora e valor digitados errados");

        correcao.RetificaEvolucaoId.Should().Be(original.Id);
        correcao.EhRetificacao.Should().BeTrue();

        var todas = await _servico.DoPacienteAsync(pacienteId);
        todas.Should().HaveCount(2,
            "a anterior continua na base — imprimir só o valor final faria a via em papel "
            + "esconder o que a trilha guarda");
    }

    [Fact]
    public async Task Retificar_sem_motivo_e_recusado()
    {
        var pacienteId = await PacienteAsync();
        var original = await _servico.RegistrarAsync(
            pacienteId, Hoje, new TimeOnly(9, 0), "PA 120x80.", Tecnica);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _servico.RetificarAsync(
                original.Id, Hoje, new TimeOnly(9, 0), "PA 130x85.", Tecnica, "  "));

        erro.Message.Should().Contain("por que o registro anterior estava errado");
    }

    [Fact]
    public async Task Cancelar_exige_motivo_e_a_linha_FICA()
    {
        var pacienteId = await PacienteAsync();
        var e = await _servico.RegistrarAsync(
            pacienteId, Hoje, new TimeOnly(9, 0), "Lançado no paciente errado.", Tecnica);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _servico.CancelarAsync(e.Id, "   ", "joana"));
        erro.Message.Should().Contain("por que este registro está sendo cancelado");

        var cancelada = await _servico.CancelarAsync(e.Id, "paciente errado", "joana");

        cancelada.Cancelada.Should().BeTrue();
        cancelada.MotivoCancelamento.Should().Be("paciente errado");
        (await _servico.DoPacienteAsync(pacienteId)).Should().ContainSingle(
            "registro clínico não se apaga: ele fica MARCADO");
    }

    [Fact]
    public async Task Registro_cancelado_nao_se_retifica()
    {
        var pacienteId = await PacienteAsync();
        var e = await _servico.RegistrarAsync(
            pacienteId, Hoje, new TimeOnly(9, 0), "Texto.", Tecnica);
        await _servico.CancelarAsync(e.Id, "paciente errado", "joana");

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _servico.RetificarAsync(
                e.Id, Hoje, new TimeOnly(9, 0), "Outro texto.", Tecnica, "corrigindo"));

        erro.Message.Should().Contain("não se retifica");
    }

    // ==================== A trilha e o circuito de volta ====================

    [Fact]
    public async Task Grava_a_auditoria_no_MESMO_save()
    {
        var pacienteId = await PacienteAsync();

        await _servico.RegistrarAsync(
            pacienteId, Hoje, new TimeOnly(9, 0), "Admissão.", Tecnica,
            intercorrencia: true);

        (await _repo.EventosAuditoriaAsync()).Should().Contain(ev =>
            ev.Acao == "EvolucaoEnfermagemIntercorrencia"
            && ev.PacienteId == pacienteId
            && ev.Operador == "Joana Técnica");
    }

    /// <summary>
    /// ⚠️ O circuito de volta que a checagem NÃO fecha: lá a oferta de registrar alergia só
    /// existe no ramo do NÃO REALIZADO, então o paciente que teve náusea e mesmo assim
    /// completou a infusão nunca virava alerta na próxima prescrição.
    /// </summary>
    [Fact]
    public async Task Reacao_observada_vira_ALERGIA_na_lista_de_problemas()
    {
        var pacienteId = await PacienteAsync();

        await _servico.RegistrarAsync(
            pacienteId, Hoje, new TimeOnly(10, 0),
            "Refere prurido após a dipirona. Infusão concluída.", Tecnica,
            intercorrencia: true, alergiaObservada: "Dipirona");

        var problemas = await _repo.ProblemasDoPacienteAsync(pacienteId);
        problemas.Should().Contain(p =>
            p.Natureza == NaturezaProblema.Alergia && p.Descricao == "Dipirona");
    }

    /// EXPERIMENTO — despeja o Registro de execução com o bloco de enfermagem, para
    /// conferir o desenho. Só roda com CLINICA_DUMP_PDF apontando uma pasta.
    [Fact]
    public async Task Despeja_o_registro_com_o_bloco_de_enfermagem()
    {
        var pasta = Environment.GetEnvironmentVariable("CLINICA_DUMP_PDF");
        if (string.IsNullOrWhiteSpace(pasta)) return;

        var pacienteId = await PacienteAsync();
        var medica = new Profissional { Nome = "Dra. Ana Souza", RegistroConselho = "CRM-SP 123456" };
        _db.Profissionais.Add(medica);
        await _db.SaveChangesAsync();

        var conferencia = new PrescricaoService(_repo);
        var prescricoes = new PrescricaoInternaService(_repo, conferencia);
        var checagens = new ChecagemPrescricaoService(_repo, () => MeioDia);

        var pr = await prescricoes.CriarAsync(pacienteId, medica.Id);
        await prescricoes.SalvarRascunhoAsync(pr.Id, "Crise álgica", null,
        [
            new ItemPrescricaoInterna { Descricao = "Dipirona sódica", Dose = "1 g", Diluente = "SF 0,9%", Volume = "100 mL" },
            new ItemPrescricaoInterna { Descricao = "Ondansetrona", Dose = "8 mg", Diluente = "SF 0,9%", Volume = "50 mL" }
        ]);

        var carregada = (await _repo.ObterPrescricaoInternaAsync(pr.Id))!;
        carregada.Situacao = SituacaoPrescricao.Assinada;
        carregada.Assinaturas.Add(new AssinaturaDocumento
        {
            Papel = PapelAssinatura.Prescritor, NomeAssinante = "Dra. Ana Souza",
            AssinadoEm = DateTime.Now, HashConteudo = "AB12CD34"
        });
        await _repo.SalvarAsync();

        var itens = (await _repo.ObterPrescricaoInternaAsync(pr.Id))!.Itens.OrderBy(i => i.Ordem).ToList();
        await checagens.ChecarAsync(itens[0].Id, SituacaoChecagem.Realizado, new TimeOnly(9, 30), Tecnica);
        await checagens.ChecarAsync(itens[1].Id, SituacaoChecagem.NaoRealizado, new TimeOnly(10, 5), Tecnica,
            justificativa: "Paciente já sem náusea.");

        await _servico.RegistrarAsync(pacienteId, Hoje, new TimeOnly(9, 0),
            "Paciente admitida na sala, acesso venoso em MSD, sem queixas.", Tecnica,
            prescricaoInternaId: pr.Id,
            sinais: new SinaisVitais(120, 80, 78, 16, 36.4m, 97, 2));

        await _servico.RegistrarAsync(pacienteId, Hoje, new TimeOnly(9, 40),
            "Refere náusea leve durante a infusão. Gotejamento reduzido e comunicado à médica.",
            Tecnica, prescricaoInternaId: pr.Id, intercorrencia: true,
            sinais: new SinaisVitais(90, 60, 96, 20, 36.2m, 95, 5));

        var tardia = await _servico.RegistrarAsync(pacienteId, Hoje, new TimeOnly(10, 20),
            "Sem queixas. Saiu deambulando, acompanhada.", Tecnica,
            prescricaoInternaId: pr.Id, sinais: new SinaisVitais(110, 70, 80, Dor: 1));

        await _servico.RetificarAsync(tardia.Id, Hoje, new TimeOnly(10, 30),
            "Sem queixas. Saiu deambulando, acompanhada pela filha.", Tecnica,
            motivoRetificacao: "hora e acompanhante corrigidos",
            sinais: new SinaisVitais(110, 70, 80, Dor: 1));

        var pdfs = new PrescricaoInternaPdfService(_repo, conferencia);
        File.WriteAllBytes(Path.Combine(pasta, "enf-registro.pdf"),
            await pdfs.GerarRegistroExecucaoAsync(pr.Id));
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
