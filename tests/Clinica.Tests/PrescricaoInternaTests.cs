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
/// A prescrição de execução interna e a CHECAGEM DE ENFERMAGEM (parcela 42).
///
/// O que estes testes travam, e por que cada um importa:
///
/// - <b>Só se checa folha assinada.</b> Administrar sobre rascunho é o buraco clássico.
/// - <b>Não realizado exige justificativa.</b> Um item que não entrou no paciente sem uma
///   linha dizendo por quê é a pior linha possível de um prontuário.
/// - <b>A hora é informada, não é o relógio</b> — e não pode ser no futuro, porque checar
///   antes de administrar deixa registrado como feito o que o paciente pode não receber.
/// - <b>Checagem não se apaga: retifica-se.</b> Apagar e regravar é exatamente o gesto que
///   uma auditoria de enfermagem procura.
/// - <b>Item checado não se suspende nem se edita</b>, senão a folha passa a desdizer uma
///   execução que já tem assinatura.
/// - <b>A alergia registrada bloqueia a assinatura até alguém confirmar</b> — e a reação
///   observada na sala vira alerta da próxima prescrição, que é o circuito que a clínica
///   pediu quando descreveu "o paciente teve reação e não quis fazer a dipirona".
/// </summary>
public class PrescricaoInternaTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly PrescricaoInternaService _prescricoes;
    private readonly ChecagemPrescricaoService _checagens;
    private readonly ProblemaPacienteService _problemas;

    private static DateTime MeioDia => DateTime.Today.AddHours(12);

    private static readonly IdentificacaoExecutante Tecnica =
        new(UsuarioId: null, Nome: "Joana Técnica", Conselho: "COREN-SP 999999");

    public PrescricaoInternaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);

        var conferencia = new PrescricaoService(_repo);
        _prescricoes = new PrescricaoInternaService(_repo, conferencia);
        // Relógio congelado ao MEIO-DIA: a recusa de hora futura precisa de um "agora"
        // conhecido, senão o teste vira loteria — rodando às 23h, "duas horas depois"
        // dá a volta no relógio e cai na madrugada, que está no passado.
        _checagens = new ChecagemPrescricaoService(_repo, () => MeioDia);
        _problemas = new ProblemaPacienteService(_repo);
    }

    // ==================== Escrever e assinar ====================

    [Fact]
    public async Task Prescricao_nasce_em_rascunho_numerada_e_com_codigo()
    {
        var (pacienteId, profissionalId) = await BaseAsync();

        var prescricao = await _prescricoes.CriarAsync(pacienteId, profissionalId);

        prescricao.Situacao.Should().Be(SituacaoPrescricao.Rascunho);
        prescricao.Numero.Should().StartWith($"PRE {DateTime.Today.Year}/");
        prescricao.CodigoVerificacao.Should().HaveLength(8);
    }

    [Fact]
    public async Task Folha_sem_itens_nao_se_assina()
    {
        var prescricao = await RascunhoAsync(comItens: false);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _prescricoes.AssinarAsync(prescricao.Id, Assinatura()));

        erro.Message.Should().Contain("sem itens");
    }

    [Fact]
    public async Task Folha_sem_profissional_nao_se_assina()
    {
        var pacienteId = await PacienteAsync();
        var prescricao = await _prescricoes.CriarAsync(pacienteId, profissionalId: null);
        await ComItensAsync(prescricao.Id);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _prescricoes.AssinarAsync(prescricao.Id, Assinatura()));

        erro.Message.Should().Contain("profissional");
    }

    [Fact]
    public async Task Rascunho_assinado_vira_assinada_e_aparece_na_sala()
    {
        var prescricao = await RascunhoAsync();

        await _prescricoes.AssinarAsync(prescricao.Id, Assinatura());

        var naSala = await _checagens.DoDiaAsync(DateOnly.FromDateTime(DateTime.Today));
        naSala.Should().ContainSingle(p => p.Id == prescricao.Id);
        naSala[0].Situacao.Should().Be(SituacaoPrescricao.Assinada);
    }

    [Fact]
    public async Task Rascunho_nao_aparece_na_sala_de_infusao()
    {
        await RascunhoAsync();

        var naSala = await _checagens.DoDiaAsync(DateOnly.FromDateTime(DateTime.Today));

        // Mostrar rascunho na sala convidaria a técnica a administrar o que ninguém mandou.
        naSala.Should().BeEmpty();
    }

    [Fact]
    public async Task Folha_assinada_nao_se_edita()
    {
        var prescricao = await AssinadaAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _prescricoes.SalvarRascunhoAsync(prescricao.Id, null, null, []));

        erro.Message.Should().Contain("suspenda o item");
    }

    // ==================== A conferência de alergia ====================

    [Fact]
    public async Task Alergia_registrada_impede_assinar_sem_confirmacao_explicita()
    {
        var (pacienteId, profissionalId) = await BaseAsync();
        await RegistrarAlergiaAsync(pacienteId, "Dipirona");

        var prescricao = await _prescricoes.CriarAsync(pacienteId, profissionalId);
        await ComItensAsync(prescricao.Id, "Dipirona sódica", dose: "1 g");

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _prescricoes.AssinarAsync(prescricao.Id, Assinatura()));

        erro.Message.Should().Contain("ALERGIA");
    }

    [Fact]
    public async Task Alergia_confirmada_deixa_assinar_e_fica_na_trilha()
    {
        var (pacienteId, profissionalId) = await BaseAsync();
        await RegistrarAlergiaAsync(pacienteId, "Dipirona");

        var prescricao = await _prescricoes.CriarAsync(pacienteId, profissionalId);
        await ComItensAsync(prescricao.Id, "Dipirona sódica", dose: "1 g");

        // Avisa e exige confirmação — NÃO impede. O registro pode estar errado, pode haver
        // dessensibilização, e quem decide é quem assina.
        var resultado = await _prescricoes.AssinarAsync(
            prescricao.Id, Assinatura(), confirmouAlergia: true, operador: "Dra. Ana");

        resultado.Prescricao.Situacao.Should().Be(SituacaoPrescricao.Assinada);
        resultado.Conferencia.ExigeConfirmacao.Should().BeTrue();

        var trilha = await _repo.EventosAuditoriaAsync();
        trilha.Should().Contain(e =>
            e.Acao == "PrescricaoAssinada" && e.Detalhe!.Contains("alergia confirmado"));
    }

    [Fact]
    public async Task Alergia_no_DILUENTE_tambem_acende()
    {
        var (pacienteId, profissionalId) = await BaseAsync();
        await RegistrarAlergiaAsync(pacienteId, "látex");

        var prescricao = await _prescricoes.CriarAsync(pacienteId, profissionalId);
        // O alérgeno pode estar no diluente, não no fármaco — por isso TextoCompleto
        // concatena descrição, dose, diluente e volume antes de comparar.
        await ComItensAsync(prescricao.Id, "Soro", dose: "100 mL", diluente: "equipo com látex");

        var conferencia = await _prescricoes.ConferirParaAssinaturaAsync(prescricao.Id);

        conferencia.ExigeConfirmacao.Should().BeTrue();
    }

    // ==================== A checagem ====================

    [Fact]
    public async Task Nao_se_checa_rascunho()
    {
        var prescricao = await RascunhoAsync();
        var itemId = (await Carregar(prescricao.Id)).Itens[0].Id;

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _checagens.ChecarAsync(itemId, SituacaoChecagem.Realizado, Agora(), Tecnica));

        erro.Message.Should().Contain("não foi assinada");
    }

    [Fact]
    public async Task Checar_grava_a_hora_INFORMADA_e_o_relogio_ao_lado()
    {
        var itemId = await ItemAssinadoAsync();
        var hora = Agora().AddHours(-2);

        var checagem = await _checagens.ChecarAsync(
            itemId, SituacaoChecagem.Realizado, hora, Tecnica);

        // A hora da administração é a digitada; o relógio fica em RegistradoEm, e a
        // diferença entre os dois é o que uma auditoria de enfermagem procura.
        checagem.HoraRealizacao.Should().Be(hora);
        checagem.RegistradoEm.Should().Be(MeioDia);
        checagem.AtrasoDoRegistro.Should().BeGreaterThan(TimeSpan.FromMinutes(90));
    }

    [Fact]
    public async Task Hora_no_futuro_e_recusada()
    {
        var itemId = await ItemAssinadoAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _checagens.ChecarAsync(
                itemId, SituacaoChecagem.Realizado, Agora().AddHours(2), Tecnica));

        // Pré-checagem é o hábito que faz um item aparecer como feito num paciente que
        // saiu antes de recebê-lo.
        erro.Message.Should().Contain("ainda não chegou");
    }

    [Fact]
    public async Task Nao_realizado_SEM_justificativa_e_recusado()
    {
        var itemId = await ItemAssinadoAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _checagens.ChecarAsync(
                itemId, SituacaoChecagem.NaoRealizado, Agora(), Tecnica));

        erro.Message.Should().Contain("justificativa");
    }

    [Fact]
    public async Task Nao_realizado_COM_justificativa_e_a_rodela_da_folha()
    {
        var itemId = await ItemAssinadoAsync();

        await _checagens.ChecarAsync(
            itemId, SituacaoChecagem.NaoRealizado, Agora(), Tecnica,
            justificativa: "Paciente recusou a medicação.");

        var item = await _repo.ObterItemPrescricaoInternaAsync(itemId);
        item!.Situacao.Should().Be(SituacaoItemPrescricao.NaoRealizado);
        item.ChecagemVigente!.Justificativa.Should().Contain("recusou");
    }

    [Fact]
    public async Task Checagem_sem_executante_e_recusada()
    {
        var itemId = await ItemAssinadoAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _checagens.ChecarAsync(
                itemId, SituacaoChecagem.Realizado, Agora(),
                new IdentificacaoExecutante(null, "  ")));

        erro.Message.Should().Contain("quem executou");
    }

    [Fact]
    public async Task Checar_duas_vezes_o_mesmo_item_e_recusado_pedindo_retificacao()
    {
        var itemId = await ItemAssinadoAsync();
        await _checagens.ChecarAsync(itemId, SituacaoChecagem.Realizado, Agora(), Tecnica);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _checagens.ChecarAsync(itemId, SituacaoChecagem.Realizado, Agora(), Tecnica));

        erro.Message.Should().Contain("RETIFIQUE");
    }

    // ==================== Retificação ====================

    [Fact]
    public async Task Retificar_NAO_apaga_a_checagem_anterior()
    {
        var itemId = await ItemAssinadoAsync();
        var original = await _checagens.ChecarAsync(
            itemId, SituacaoChecagem.Realizado, Agora().AddHours(-1), Tecnica);

        await _checagens.RetificarAsync(
            itemId, SituacaoChecagem.NaoRealizado, Agora().AddHours(-1), Tecnica,
            motivoRetificacao: "Checado no paciente errado.",
            justificativa: "Não foi administrado.");

        var item = await _repo.ObterItemPrescricaoInternaAsync(itemId);

        // Duas linhas na base, e a vigente é a nova. A anterior fica — é a prova de que a
        // conduta da época era razoável.
        item!.Checagens.Should().HaveCount(2);
        item.Checagens.Should().Contain(c => c.Id == original.Id);
        item.ChecagemVigente!.Situacao.Should().Be(SituacaoChecagem.NaoRealizado);
        item.ChecagemVigente.RetificaChecagemId.Should().Be(original.Id);
    }

    [Fact]
    public async Task Retificar_sem_dizer_por_que_e_recusado()
    {
        var itemId = await ItemAssinadoAsync();
        await _checagens.ChecarAsync(itemId, SituacaoChecagem.Realizado, Agora(), Tecnica);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _checagens.RetificarAsync(
                itemId, SituacaoChecagem.Realizado, Agora(), Tecnica, motivoRetificacao: " "));

        erro.Message.Should().Contain("por que");
    }

    [Fact]
    public async Task Retificar_item_nunca_checado_e_recusado()
    {
        var itemId = await ItemAssinadoAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _checagens.RetificarAsync(
                itemId, SituacaoChecagem.Realizado, Agora(), Tecnica, "motivo qualquer"));

        erro.Message.Should().Contain("checagem normal");
    }

    // ==================== Suspensão e cancelamento ====================

    [Fact]
    public async Task Suspender_item_exige_motivo()
    {
        var itemId = await ItemAssinadoAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _prescricoes.SuspenderItemAsync(itemId, motivo: ""));

        erro.Message.Should().Contain("por que");
    }

    [Fact]
    public async Task Item_JA_CHECADO_nao_se_suspende()
    {
        var itemId = await ItemAssinadoAsync();
        await _checagens.ChecarAsync(itemId, SituacaoChecagem.Realizado, Agora(), Tecnica);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _prescricoes.SuspenderItemAsync(itemId, "mudei de ideia"));

        // O que foi feito, foi feito. Suspender depois faria a folha desdizer uma execução
        // que já tem assinatura.
        erro.Message.Should().Contain("retificá-la");
    }

    [Fact]
    public async Task Item_suspenso_nao_se_checa()
    {
        var itemId = await ItemAssinadoAsync();
        await _prescricoes.SuspenderItemAsync(itemId, "contraindicação");

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _checagens.ChecarAsync(itemId, SituacaoChecagem.Realizado, Agora(), Tecnica));

        erro.Message.Should().Contain("suspenso");
    }

    [Fact]
    public async Task Folha_com_item_checado_nao_se_cancela()
    {
        var prescricao = await AssinadaAsync();
        var itemId = (await Carregar(prescricao.Id)).Itens[0].Id;
        await _checagens.ChecarAsync(itemId, SituacaoChecagem.Realizado, Agora(), Tecnica);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _prescricoes.CancelarAsync(prescricao.Id, "engano"));

        erro.Message.Should().Contain("item checado");
    }

    [Fact]
    public async Task Cancelar_nao_apaga_a_folha()
    {
        var prescricao = await AssinadaAsync();

        await _prescricoes.CancelarAsync(prescricao.Id, "paciente não compareceu");

        var recarregada = await Carregar(prescricao.Id);
        recarregada.Situacao.Should().Be(SituacaoPrescricao.Cancelada);
        recarregada.MotivoCancelamento.Should().Contain("não compareceu");
    }

    // ==================== Encerrar ====================

    [Fact]
    public async Task Nao_se_encerra_com_item_pendente()
    {
        var prescricao = await AssinadaAsync(quantidadeItens: 2);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _checagens.EncerrarAsync(prescricao.Id, Assinatura(), Tecnica));

        erro.Message.Should().Contain("sem checagem");
    }

    [Fact]
    public async Task Item_SE_NECESSARIO_nao_impede_encerrar()
    {
        var (pacienteId, profissionalId) = await BaseAsync();
        var prescricao = await _prescricoes.CriarAsync(pacienteId, profissionalId);

        await _prescricoes.SalvarRascunhoAsync(prescricao.Id, null, null,
        [
            new ItemPrescricaoInterna { Descricao = "Soro fisiológico", Dose = "500 mL" },
            new ItemPrescricaoInterna { Descricao = "Ondansetrona", Dose = "8 mg", SeNecessario = true }
        ]);
        await _prescricoes.AssinarAsync(prescricao.Id, Assinatura());

        var carregada = await Carregar(prescricao.Id);
        var fixo = carregada.Itens.First(i => !i.SeNecessario);
        await _checagens.ChecarAsync(fixo.Id, SituacaoChecagem.Realizado, Agora(), Tecnica);

        // Condição que não aconteceu não é trabalho por fazer.
        var encerrada = await _checagens.EncerrarAsync(prescricao.Id, Assinatura(), Tecnica);

        encerrada.Situacao.Should().Be(SituacaoPrescricao.Encerrada);
    }

    [Fact]
    public async Task Encerrada_sai_da_sala_e_nao_se_checa_mais()
    {
        var prescricao = await AssinadaAsync();
        var itemId = (await Carregar(prescricao.Id)).Itens[0].Id;
        await _checagens.ChecarAsync(itemId, SituacaoChecagem.Realizado, Agora(), Tecnica);
        await _checagens.EncerrarAsync(prescricao.Id, Assinatura(), Tecnica);

        var naSala = await _checagens.DoDiaAsync(DateOnly.FromDateTime(DateTime.Today));
        naSala.Should().BeEmpty();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _checagens.RetificarAsync(
                itemId, SituacaoChecagem.NaoRealizado, Agora(), Tecnica, "motivo", "just"));
        erro.Message.Should().Contain("já foi encerrada");
    }

    [Fact]
    public async Task Encerrar_guarda_as_DUAS_assinaturas_uma_por_papel()
    {
        var prescricao = await AssinadaAsync();
        var itemId = (await Carregar(prescricao.Id)).Itens[0].Id;
        await _checagens.ChecarAsync(itemId, SituacaoChecagem.Realizado, Agora(), Tecnica);

        await _checagens.EncerrarAsync(
            prescricao.Id,
            Assinatura(nome: "Joana Técnica", conselho: "COREN-SP 999999"),
            Tecnica);

        var carregada = await Carregar(prescricao.Id);
        carregada.AssinaturaDoPrescritor.Should().NotBeNull();
        carregada.AssinaturaDoExecutante.Should().NotBeNull();
        carregada.AssinaturaDoExecutante!.NomeAssinante.Should().Be("Joana Técnica");
    }

    // ==================== O circuito de volta ====================

    [Fact]
    public async Task Reacao_na_sala_vira_ALERGIA_na_lista_de_problemas()
    {
        var itemId = await ItemAssinadoAsync(descricao: "Dipirona sódica", dose: "1 g");
        var pacienteId = (await _repo.ObterItemPrescricaoInternaAsync(itemId))!.Prescricao!.PacienteId;

        await _checagens.ChecarAsync(
            itemId, SituacaoChecagem.NaoRealizado, Agora(), Tecnica,
            justificativa: "Paciente apresentou reação alérgica.",
            alergiaObservada: "Dipirona");

        // É este o circuito que a clínica pediu: o que aconteceu hoje acende o alerta
        // amanhã, em vez de morrer num campo de texto.
        var alertas = await _problemas.AlertasAsync(pacienteId);
        alertas.Should().ContainSingle(p =>
            p.Natureza == NaturezaProblema.Alergia && p.Descricao == "Dipirona");
    }

    [Fact]
    public async Task Alergia_registrada_na_sala_bloqueia_a_PROXIMA_prescricao()
    {
        var itemId = await ItemAssinadoAsync(descricao: "Dipirona sódica", dose: "1 g");
        var prescricaoAnterior = (await _repo.ObterItemPrescricaoInternaAsync(itemId))!.Prescricao!;
        var pacienteId = prescricaoAnterior.PacienteId;

        await _checagens.ChecarAsync(
            itemId, SituacaoChecagem.NaoRealizado, Agora(), Tecnica,
            justificativa: "Reação alérgica.", alergiaObservada: "Dipirona");

        var nova = await _prescricoes.CriarAsync(pacienteId, prescricaoAnterior.ProfissionalId);
        await ComItensAsync(nova.Id, "Dipirona sódica", dose: "1 g");

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _prescricoes.AssinarAsync(nova.Id, Assinatura()));

        erro.Message.Should().Contain("ALERGIA");
    }

    // ==================== Apoio ====================

    private static TimeOnly Agora() => TimeOnly.FromDateTime(MeioDia);

    private static AssinaturaDocumento Assinatura(
        string nome = "Dra. Ana Souza", string? conselho = "CRM-SP 123456")
        => new()
        {
            Tipo = TipoAssinatura.IcpBrasil,
            HashConteudo = new string('a', 64),
            NomeAssinante = nome,
            RegistroConselho = conselho,
            CpfAssinante = "12345678909",
            CertificadoTitular = nome,
            AssinadoEm = DateTime.Now
        };

    private async Task<int> PacienteAsync()
    {
        var paciente = new Paciente { Nome = "Maria de Teste", Convenio = Convenio.UnimedIntercambio };
        _db.Pacientes.Add(paciente);
        await _db.SaveChangesAsync();
        return paciente.Id;
    }

    private async Task<(int PacienteId, int ProfissionalId)> BaseAsync()
    {
        var pacienteId = await PacienteAsync();
        var profissional = new Profissional { Nome = "Dra. Ana Souza", RegistroConselho = "CRM-SP 123456" };
        _db.Profissionais.Add(profissional);
        await _db.SaveChangesAsync();
        return (pacienteId, profissional.Id);
    }

    private Task RegistrarAlergiaAsync(int pacienteId, string descricao)
        => _problemas.SalvarAsync(new ProblemaPaciente
        {
            PacienteId = pacienteId,
            Natureza = NaturezaProblema.Alergia,
            Descricao = descricao
        });

    private Task ComItensAsync(
        int prescricaoId, string descricao = "Soro fisiológico 0,9%",
        string? dose = "500 mL", string? diluente = null, int quantidade = 1)
        => _prescricoes.SalvarRascunhoAsync(prescricaoId, "Teste", null,
            Enumerable.Range(0, quantidade).Select(i => new ItemPrescricaoInterna
            {
                Descricao = quantidade == 1 ? descricao : $"{descricao} {i + 1}",
                Dose = dose,
                Diluente = diluente,
                Via = ViaAdministracao.Endovenosa,
                TempoInfusao = "30 min"
            }).ToList());

    private async Task<PrescricaoInterna> RascunhoAsync(bool comItens = true)
    {
        var (pacienteId, profissionalId) = await BaseAsync();
        var prescricao = await _prescricoes.CriarAsync(pacienteId, profissionalId);
        if (comItens) await ComItensAsync(prescricao.Id);
        return prescricao;
    }

    private async Task<PrescricaoInterna> AssinadaAsync(int quantidadeItens = 1)
    {
        var (pacienteId, profissionalId) = await BaseAsync();
        var prescricao = await _prescricoes.CriarAsync(pacienteId, profissionalId);
        await ComItensAsync(prescricao.Id, quantidade: quantidadeItens);
        await _prescricoes.AssinarAsync(prescricao.Id, Assinatura());
        return prescricao;
    }

    private async Task<int> ItemAssinadoAsync(
        string descricao = "Soro fisiológico 0,9%", string? dose = "500 mL")
    {
        var (pacienteId, profissionalId) = await BaseAsync();
        var prescricao = await _prescricoes.CriarAsync(pacienteId, profissionalId);
        await ComItensAsync(prescricao.Id, descricao, dose);
        await _prescricoes.AssinarAsync(prescricao.Id, Assinatura());
        return (await Carregar(prescricao.Id)).Itens[0].Id;
    }

    private async Task<PrescricaoInterna> Carregar(int prescricaoId)
    {
        _db.ChangeTracker.Clear();
        return (await _repo.ObterPrescricaoInternaAsync(prescricaoId))!;
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
