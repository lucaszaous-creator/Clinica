using Clinica.Application.Assinatura;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Documento ao paciente (out/2026): a fila que o CRM executa pelo WhatsApp oficial (sem
/// e-mail — decisão da cliente, out/2026). Duas metades:
///
/// 1. o DESKTOP põe o pedido (<see cref="EnvioAoPacienteService"/>) — com a trilha no mesmo
///    gravar, sem duplicar no segundo clique, com os bytes da 2ª via;
/// 2. a PONTE entrega o pedido ao CRM (<see cref="FilaEnviosCrmService"/>) — o protocolo que
///    o <c>ClinicalDispatchWorker</c> espera: listar sem conteúdo, reservar com conteúdo,
///    autorizar UMA vez, relatar sem regredir.
/// </summary>
public sealed class EnvioAoPacienteTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ParametrosService _parametros;
    private readonly DocumentoClinicoService _documentos;
    private DateTime _agora = new(2026, 10, 19, 10, 0, 0);

    public EnvioAoPacienteTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _parametros = new ParametrosService(_repo, new ProtecaoSegredoGlobal(Convert.ToBase64String(new byte[32])));
        _documentos = new DocumentoClinicoService(_repo, new ProntuarioService(_repo), new ConsentimentoService(_repo));
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    private EnvioAoPacienteService Envio()
        => new(_repo, new DocumentosClinicosPdfService(_repo), _parametros, () => _agora);

    private FilaEnviosCrmService Fila(ClinicaRepositorio? repo = null) => new(repo ?? _repo, () => _agora);

    private async Task<DocumentoClinico> ReceitaAsync(string? telefone = "(11) 99999-0001", string? email = "maria@exemplo.com.br",
        TipoDocumentoClinico tipo = TipoDocumentoClinico.Receita)
    {
        var p = new Paciente { Nome = "Maria Souza", Telefone = telefone, Email = email, Convenio = Convenio.UnimedIntercambio };
        var m = new Profissional { Nome = "Dra. Ana", RegistroConselho = "CRM-SP 123456" };
        _db.Pacientes.Add(p); _db.Profissionais.Add(m);
        await _db.SaveChangesAsync();
        var d = new DocumentoClinico { Tipo = tipo, PacienteId = p.Id, ProfissionalId = m.Id, Data = new DateOnly(2026, 10, 19) };
        if (tipo == TipoDocumentoClinico.Receita) d.Itens = [new ItemDocumento { Descricao = "Fitoterápico fictício", Quantidade = "1 frasco" }];
        if (tipo == TipoDocumentoClinico.PedidoExame) d.Itens = [new ItemDocumento { Descricao = "Hemograma fictício" }];
        if (tipo == TipoDocumentoClinico.Atestado) d.DiasAfastamento = 1;
        return await _documentos.EmitirAsync(d, operador: "ana");
    }

    private async Task<EnvioPacienteCrm> NaFilaAsync(DocumentoClinico? doc = null)
    {
        doc ??= await ReceitaAsync();
        (await Envio().EnfileirarWhatsAppAsync(doc.Id, "ana")).Ok.Should().BeTrue();
        _db.ChangeTracker.Clear();
        return await _db.EnviosPacienteCrm.AsNoTracking().SingleAsync();
    }

    // ===================== O desktop põe o pedido =====================

    [Fact]
    public async Task Enfileirar_grava_o_pedido_com_o_PDF_a_validade_e_a_trilha_no_mesmo_gravar()
    {
        var doc = await ReceitaAsync();

        var r = await Envio().EnfileirarWhatsAppAsync(doc.Id, "ana");

        r.Ok.Should().BeTrue(r.Frase);
        r.Frase.Should().Contain("fila do CRM");
        var e = await _db.EnviosPacienteCrm.AsNoTracking().SingleAsync();
        e.Telefone.Should().Be("5511999990001", "o CRM acha a conversa pelo número com DDI");
        e.Estado.Should().Be(FilaEnviosCrm.Fila);
        e.Canal.Should().Be("whatsapp");
        e.Origem.Should().Be("DocumentoClinico");
        e.Operador.Should().Be("ana");
        e.ExpiraEm.Should().Be(_agora.AddHours(24));
        e.DocumentoClinicoId.Should().Be(doc.Id);
        e.Arquivo.Should().NotBeNull();
        System.Text.Encoding.ASCII.GetString(e.Arquivo!, 0, 4).Should().Be("%PDF");
        e.NomeArquivo.Should().Be($"Receita-{doc.Numero.Replace('/', '-')}.pdf");
        e.Texto.Should().Contain("Olá, Maria!").And.Contain("sua receita").And.NotContain("assinad",
            "o papel sem certificado não promete assinatura digital");
        var trilha = await _db.Auditoria.AsNoTracking().Where(a => a.Acao == EnvioAoPacienteService.AcaoNaFilaDoCrm).SingleAsync();
        trilha.PacienteId.Should().Be(doc.PacienteId);
        trilha.Operador.Should().Be("ana");
        trilha.Detalhe.Should().Contain(e.Id);
    }

    [Fact]
    public async Task Documento_assinado_vai_com_os_bytes_GUARDADOS_nunca_regenerados()
    {
        var doc = await ReceitaAsync(tipo: TipoDocumentoClinico.Atestado);
        var guardado = new ArquivoAssinado { Conteudo = System.Text.Encoding.ASCII.GetBytes("%PDF-1.7 assinado fictício %%EOF"), NomeArquivo = "x.pdf" };
        _db.ArquivosAssinados.Add(guardado);
        await _db.SaveChangesAsync();
        var linha = await _db.DocumentosClinicos.SingleAsync(d => d.Id == doc.Id);
        linha.ArquivoAssinadoId = guardado.Id;
        linha.AssinaturaTipo = TipoAssinatura.IcpBrasil;
        await _db.SaveChangesAsync();

        var e = await NaFilaAsync(linha);

        e.Arquivo.Should().Equal(guardado.Conteudo);
        e.NomeArquivo.Should().EndWith("-assinado.pdf");
        e.Texto.Should().Contain("assinado(a) digitalmente");
    }

    [Fact]
    public async Task O_segundo_clique_nao_poe_o_mesmo_papel_duas_vezes_na_fila()
    {
        var doc = await ReceitaAsync();
        await Envio().EnfileirarWhatsAppAsync(doc.Id, "ana");

        var r = await Envio().EnfileirarWhatsAppAsync(doc.Id, "bia");

        r.Ok.Should().BeTrue();
        r.Frase.Should().Contain("já está na fila");
        (await _db.EnviosPacienteCrm.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Pedido_vencido_nao_impede_pedir_de_novo()
    {
        var doc = await ReceitaAsync();
        await Envio().EnfileirarWhatsAppAsync(doc.Id, "ana");
        _agora = _agora.AddHours(25);

        (await Envio().EnfileirarWhatsAppAsync(doc.Id, "ana")).Frase.Should().NotContain("já está na fila");

        (await _db.EnviosPacienteCrm.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Sem_celular_valido_nada_vai_para_a_fila_e_a_frase_diz_o_que_corrigir()
    {
        var doc = await ReceitaAsync(telefone: "123");

        var r = await Envio().EnfileirarWhatsAppAsync(doc.Id, "ana");

        r.Ok.Should().BeFalse();
        r.Frase.Should().Contain("celular válido");
        (await _db.EnviosPacienteCrm.AnyAsync()).Should().BeFalse();
        (await _db.Auditoria.AnyAsync(a => a.Acao == EnvioAoPacienteService.AcaoNaFilaDoCrm)).Should().BeFalse();
    }

    [Fact]
    public async Task Documento_cancelado_nao_sai_por_canal_nenhum()
    {
        var doc = await ReceitaAsync();
        await _documentos.CancelarAsync(doc.Id, "Emitido por engano", "ana");

        var act = () => Envio().EnfileirarWhatsAppAsync(doc.Id, "ana");
        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*cancelado*");
    }

    [Theory]
    [InlineData(TipoDocumentoClinico.Receita, false, false, true)]
    [InlineData(TipoDocumentoClinico.Atestado, false, false, true)]
    [InlineData(TipoDocumentoClinico.PedidoExame, false, false, true)]
    [InlineData(TipoDocumentoClinico.Comparecimento, false, false, true)]
    [InlineData(TipoDocumentoClinico.Anamnese, false, false, false)]
    [InlineData(TipoDocumentoClinico.Anamnese, true, false, true)]
    [InlineData(TipoDocumentoClinico.Receita, true, true, false)]
    public void O_que_pode_sair_como_arquivo(TipoDocumentoClinico tipo, bool assinado, bool cancelado, bool pode)
        => EnvioAoPacienteService.PodeEnviar(tipo, assinado, cancelado).Should().Be(pode);

    [Fact]
    public async Task Destinos_mostram_o_numero_antes_de_enviar_e_o_que_ja_esta_na_fila()
    {
        var doc = await ReceitaAsync();
        var d = await Envio().DestinosAsync(doc.Id);
        d.Celular.Should().Be("(11) 99999-0001");
        d.JaNaFila.Should().BeNull();

        await Envio().EnfileirarWhatsAppAsync(doc.Id, "ana");
        (await Envio().DestinosAsync(doc.Id)).JaNaFila.Should().Contain("Já está na fila");
    }

    // ===================== Os canais: só WhatsApp (out/2026) =====================

    [Theory]
    [InlineData(TipoDocumentoClinico.Receita)]
    [InlineData(TipoDocumentoClinico.Atestado)]
    [InlineData(TipoDocumentoClinico.PedidoExame)]
    [InlineData(TipoDocumentoClinico.Comparecimento)]
    public async Task Todo_papel_enviavel_oferece_so_WhatsApp_o_oficial_marcado_e_com_o_custo(TipoDocumentoClinico tipo)
    {
        var doc = await ReceitaAsync(tipo: tipo);

        var d = await Envio().DestinosAsync(doc.Id);

        d.Opcoes.Select(o => o.Canal).Should().Equal(CanalDeEnvio.WhatsAppPeloCrm, CanalDeEnvio.WhatsAppDesteComputador);
        d.Opcoes.Should().OnlyContain(o => o.Disponivel);
        d.Padrao.Should().Be(CanalDeEnvio.WhatsAppPeloCrm);
        d.Opcoes[0].Nota.Should().Contain("cobrado pela Meta").And.NotContain("e-mail");
    }

    [Fact]
    public async Task Sem_celular_os_dois_canais_aparecem_apagados_com_o_motivo()
    {
        var doc = await ReceitaAsync(telefone: null);
        var d = await Envio().DestinosAsync(doc.Id);
        d.Opcoes.Should().OnlyContain(o => !o.Disponivel && o.Motivo!.Contains("celular válido"));
        d.Padrao.Should().BeNull();
    }

    [Fact]
    public async Task A_trilha_diz_quem_por_qual_canal_e_para_onde()
    {
        var doc = await ReceitaAsync();
        await Envio().EnfileirarWhatsAppAsync(doc.Id, "bia");

        var trilha = await _db.Auditoria.AsNoTracking().Where(a => a.Acao.StartsWith("Documento")).ToListAsync();
        trilha.Should().Contain(a => a.Operador == "bia" && a.Detalhe!.Contains("Canal: WhatsApp (pelo CRM)") && a.Detalhe.Contains("Para: (11) 99999-0001"));
    }

    // ===================== A ponte entrega o pedido ao CRM =====================

    [Fact]
    public async Task Listar_nao_traz_texto_nem_PDF()
    {
        await NaFilaAsync();
        var lista = await Fila().ListarAsync("crm-1");
        var p = lista.Should().ContainSingle().Subject;
        p.Texto.Should().BeNull();
        p.Arquivo.Should().BeNull();
        p.Estado.Should().Be("queued");
    }

    [Fact]
    public async Task Reservar_devolve_a_linha_inteira_e_repete_para_o_mesmo_CRM()
    {
        var e = await NaFilaAsync();
        var r1 = await Fila().ReservarAsync(e.Id, "crm-1", 2);
        r1.Estado.Should().Be("processing");
        r1.Arquivo.Should().Equal(e.Arquivo);
        r1.Texto.Should().Be(e.Texto);

        var r2 = await Fila().ReservarAsync(e.Id, "crm-1", 2);
        r2.Estado.Should().Be("processing");
        r2.Arquivo.Should().NotBeNull();
        (await Fila().ListarAsync("crm-1")).Should().ContainSingle("a reserva sem autorização continua visível ao mesmo CRM");
    }

    [Fact]
    public async Task Protocolo_antigo_nao_leva_o_conteudo_e_nada_muda()
    {
        var e = await NaFilaAsync();
        var r = await Fila().ReservarAsync(e.Id, "crm-1", 0);
        r.Estado.Should().Be("blocked");
        r.Arquivo.Should().BeNull();
        r.Texto.Should().BeNull();
        (await _db.EnviosPacienteCrm.AsNoTracking().SingleAsync()).Estado.Should().Be("queued");
        (await Fila().AutorizarAsync(e.Id, "crm-1", 1)).Concedida.Should().BeFalse();
    }

    [Fact]
    public async Task Reserva_viva_de_outro_CRM_nao_entrega_conteudo_e_vencida_pode_ser_retomada()
    {
        var e = await NaFilaAsync();
        await Fila().ReservarAsync(e.Id, "crm-1", 2);

        var outro = await Fila().ReservarAsync(e.Id, "crm-2", 2);
        outro.Estado.Should().Be("queued");
        outro.Arquivo.Should().BeNull();
        (await Fila().ListarAsync("crm-2")).Should().BeEmpty();

        _agora = _agora.AddMinutes(11);
        (await Fila().ListarAsync("crm-2")).Should().ContainSingle();
        (await Fila().ReservarAsync(e.Id, "crm-2", 2)).Estado.Should().Be("processing");
    }

    [Fact]
    public async Task Autorizacao_e_de_uso_unico_e_tira_o_PDF_da_linha()
    {
        var e = await NaFilaAsync();
        await Fila().ReservarAsync(e.Id, "crm-1", 2);

        var a1 = await Fila().AutorizarAsync(e.Id, "crm-1", 2);
        a1.Concedida.Should().BeTrue();
        a1.Estado.Should().Be("sending");

        var a2 = await Fila().AutorizarAsync(e.Id, "crm-1", 2);
        a2.Concedida.Should().BeFalse();
        a2.Estado.Should().Be("uncertain", "a primeira pode ter sido usada com a resposta perdida");

        _db.ChangeTracker.Clear();
        var linha = await _db.EnviosPacienteCrm.AsNoTracking().SingleAsync();
        linha.Arquivo.Should().BeNull();
        linha.SubmetidoEm.Should().Be(_agora);
        (await Fila().ListarAsync("crm-1")).Should().BeEmpty("autorizado não volta para a fila");
        (await Fila().ReservarAsync(e.Id, "crm-1", 2)).Arquivo.Should().BeNull();
    }

    [Fact]
    public async Task Autorizacao_sem_reserva_deste_CRM_e_recusada()
    {
        var e = await NaFilaAsync();
        (await Fila().AutorizarAsync(e.Id, "crm-1", 2)).Should().Be(
            new AutorizacaoDeEnvio(false, "blocked", "Este envio não está reservado para este CRM."));
        await Fila().ReservarAsync(e.Id, "crm-1", 2);
        (await Fila().AutorizarAsync(e.Id, "crm-2", 2)).Concedida.Should().BeFalse();
    }

    [Fact]
    public async Task Duas_autorizacoes_simultaneas_so_uma_e_concedida()
    {
        var e = await NaFilaAsync();
        await Fila().ReservarAsync(e.Id, "crm-1", 2);

        // Dois contextos leem a MESMA versão da linha antes de qualquer um gravar.
        await using var db2 = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        var repo2 = new ClinicaRepositorio(db2);
        _db.ChangeTracker.Clear();
        var linha1 = await _repo.ObterEnvioPacienteCrmAsync(e.Id);
        var linha2 = await repo2.ObterEnvioPacienteCrmAsync(e.Id);
        linha1!.Versao.Should().Be(linha2!.Versao);

        var a1 = await Fila().AutorizarAsync(e.Id, "crm-1", 2);
        var a2 = await Fila(repo2).AutorizarAsync(e.Id, "crm-1", 2);

        new[] { a1.Concedida, a2.Concedida }.Count(x => x).Should().Be(1);
        a2.Estado.Should().Be("uncertain");
    }

    [Fact]
    public async Task Pedido_vencido_e_bloqueado_na_reserva_e_perde_o_PDF()
    {
        var e = await NaFilaAsync();
        _agora = _agora.AddHours(24);

        var r = await Fila().ReservarAsync(e.Id, "crm-1", 2);

        r.Estado.Should().Be("blocked");
        r.Detalhe.Should().Contain("vencido");
        r.Arquivo.Should().BeNull();
        _db.ChangeTracker.Clear();
        (await _db.EnviosPacienteCrm.AsNoTracking().SingleAsync()).Arquivo.Should().BeNull();
    }

    [Fact]
    public async Task Documento_cancelado_depois_do_pedido_bloqueia_ate_na_autorizacao_final()
    {
        var doc = await ReceitaAsync();
        var e = await NaFilaAsync(doc);
        await Fila().ReservarAsync(e.Id, "crm-1", 2);
        await _documentos.CancelarAsync(doc.Id, "Paciente errado", "ana");

        var a = await Fila().AutorizarAsync(e.Id, "crm-1", 2);

        a.Concedida.Should().BeFalse();
        a.Estado.Should().Be("blocked");
        a.Detalhe.Should().Contain("cancelado");
    }

    [Fact]
    public async Task Relato_nao_regride_e_devolve_o_estado_que_vale()
    {
        var e = await NaFilaAsync();
        await Fila().ReservarAsync(e.Id, "crm-1", 2);
        await Fila().AutorizarAsync(e.Id, "crm-1", 2);

        (await Fila().RegistrarAsync(e.Id, "crm-1", "accepted", "")).Estado.Should().Be("accepted");
        (await Fila().RegistrarAsync(e.Id, "crm-1", "delivered", "Entrega confirmada pelo provedor")).Estado.Should().Be("delivered");
        var atrasado = await Fila().RegistrarAsync(e.Id, "crm-1", "accepted", "recibo atrasado");
        atrasado.Estado.Should().Be("delivered");
        (await Fila().RegistrarAsync(e.Id, "crm-1", "uncertain", "x")).Estado.Should().Be("delivered");
        (await Fila().RegistrarAsync(e.Id, "crm-1", "seen", "Leitura confirmada")).Estado.Should().Be("seen");
        (await Fila().RegistrarAsync(e.Id, "crm-1", "failed", "x")).Estado.Should().Be("seen");
    }

    [Fact]
    public async Task Relato_de_outro_CRM_ou_estado_desconhecido_e_recusado()
    {
        var e = await NaFilaAsync();
        await Fila().ReservarAsync(e.Id, "crm-1", 2);
        await Fila().Invoking(f => f.RegistrarAsync(e.Id, "crm-2", "accepted", "")).Should().ThrowAsync<InvalidOperationException>();
        await Fila().Invoking(f => f.RegistrarAsync(e.Id, "crm-1", "processing", "")).Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData("queued", "blocked", true)]
    [InlineData("sending", "accepted", true)]
    [InlineData("sending", "uncertain", true)]
    [InlineData("uncertain", "accepted", true)]
    [InlineData("accepted", "uncertain", false)]
    [InlineData("accepted", "failed", true)]
    [InlineData("delivered", "accepted", false)]
    [InlineData("delivered", "seen", true)]
    [InlineData("seen", "delivered", false)]
    [InlineData("failed", "delivered", false)]
    [InlineData("blocked", "accepted", false)]
    public void Matriz_de_transicoes(string de, string para, bool pode) => FilaEnviosCrm.PodeIr(de, para).Should().Be(pode);

    [Theory]
    [InlineData("(11) 99999-0001", "5511999990001")]
    [InlineData("1133334444", "551133334444")]
    [InlineData("+55 11 99999-0001", "5511999990001")]
    [InlineData("123", "")]
    [InlineData(null, "")]
    public void Telefone_do_envio(string? entrada, string esperado) => FilaEnviosCrm.Telefone(entrada).Should().Be(esperado);
}
