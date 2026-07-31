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
/// Direitos do titular (LGPD, arts. 18 e 16).
///
/// A clínica sabia COLHER e REVOGAR consentimento desde a parcela 2. Faltavam os outros
/// dois pedidos que o paciente pode fazer e que ela é obrigada a atender: **me dê tudo o
/// que vocês têm sobre mim** e **apaguem meus dados**.
///
/// O que estes testes protegem:
///
/// 1. **A exportação traz o que a clínica realmente guarda** — cadastro, consentimento,
///    guia, evolução e documento. Um "relatório" que só repete o cadastro atenderia ao
///    pedido no papel e não no fato.
/// 2. **Anonimizar APAGA a identificação e PRESERVA o prontuário.** É a regra que o
///    produto não pode fingir que não existe: a guarda do prontuário é obrigação legal
///    do profissional de saúde (CFM 1.821/2007) e a LGPD a preserva expressamente
///    (art. 16, II). Prometer apagar tudo e manter o histórico seria mentir por escrito.
/// 3. **O consentimento cai junto**: sem contato não há como falar com o titular, e
///    guardar o "sim" de quem pediu para sair é permissão de um contato que não existe.
/// 4. **Motivo é obrigatório e a ação vai para a trilha** — a clínica precisa poder
///    provar que atendeu, e quando. O nome original fica NA AUDITORIA, que é o único
///    lugar onde esse vínculo pode existir sem expor o dado na operação do dia.
/// </summary>
public class TitularDadosTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ConsentimentoService _consentimentos;
    private readonly ProntuarioService _prontuario;
    private readonly DocumentoClinicoService _documentos;
    private readonly AtendimentoService _atendimentos;
    private readonly TitularDadosService _titular;

    private static readonly DateOnly Dia = new(2026, 8, 12);

    public TitularDadosTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _consentimentos = new ConsentimentoService(_repo);
        _prontuario = new ProntuarioService(_repo);
        _documentos = new DocumentoClinicoService(_repo, _prontuario, _consentimentos);
        _atendimentos = new AtendimentoService(_repo);
        _titular = new TitularDadosService(_repo, _consentimentos, _prontuario, _documentos);
    }

    private async Task<int> CriarPacienteAsync()
    {
        var p = new Paciente
        {
            Nome = "Maria Souza",
            Documento = "123.456.789-09",
            Telefone = "11988887777",
            Carteirinha = "UNI-4455",
            DataNascimento = new DateOnly(1980, 5, 20),
            Observacoes = "Prefere atendimento pela manhã.",
            Convenio = Convenio.UnimedIntercambio,
            Sexo = Sexo.Feminino
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    /// <summary>Um paciente com história: guia, evolução, documento e consentimento.</summary>
    private async Task<int> CriarPacienteComHistoriaAsync()
    {
        var pacienteId = await CriarPacienteAsync();

        await _atendimentos.LancarAsync(pacienteId, Dia, ModalidadeAtendimento.AcupunturaComEletro);

        await _prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = pacienteId,
            Data = Dia,
            EvaAntes = 8,
            EvaDepois = 3,
            QueixaPrincipal = "dor lombar há três meses",
            Conduta = "agulhamento seco em pontos lombares"
        }, "secretaria");

        await _consentimentos.RegistrarAsync(
            pacienteId, FinalidadeConsentimento.TratamentoDeDados, concedido: true,
            operador: "secretaria");
        await _consentimentos.RegistrarAsync(
            pacienteId, FinalidadeConsentimento.ComunicacaoEMarketing, concedido: true,
            operador: "secretaria");

        var profissional = new Profissional { Nome = "Dra. Ana", RegistroConselho = "CRM-SP 123456" };
        _db.Profissionais.Add(profissional);
        await _db.SaveChangesAsync();

        await _documentos.EmitirAsync(new DocumentoClinico
        {
            Tipo = TipoDocumentoClinico.Receita,
            PacienteId = pacienteId,
            ProfissionalId = profissional.Id,
            Data = Dia,
            Itens = [new ItemDocumento { Descricao = "Dipirona 500mg", Quantidade = "1 caixa" }]
        }, "secretaria");

        return pacienteId;
    }

    // ==================== Acesso (art. 18, II) ====================

    [Fact]
    public async Task Exportacao_traz_tudo_o_que_a_clinica_guarda()
    {
        var pacienteId = await CriarPacienteComHistoriaAsync();

        var texto = await _titular.ExportarAsync(pacienteId, "secretaria");

        // Cadastro
        texto.Should().Contain("Maria Souza");
        texto.Should().Contain("123.456.789-09");
        texto.Should().Contain("11988887777");

        // O que a clínica sabe além do cadastro — é isto que faz a exportação atender ao
        // pedido de verdade, e não só no papel.
        texto.Should().Contain("CONSENTIMENTOS");
        texto.Should().Contain("ATENDIMENTOS E GUIAS");
        texto.Should().Contain("dor lombar há três meses");
        texto.Should().Contain("DOCUMENTOS EMITIDOS");
    }

    [Fact]
    public async Task Exportacao_de_paciente_sem_historia_diz_que_nao_ha_em_vez_de_omitir()
    {
        // Seção que some deixa o titular sem saber se a clínica não tem o dado ou se o
        // sistema esqueceu de mostrá-lo.
        var pacienteId = await CriarPacienteAsync();

        var texto = await _titular.ExportarAsync(pacienteId);

        texto.Should().Contain("(nenhum registrado)");
        texto.Should().Contain("(nenhuma)");
    }

    [Fact]
    public async Task Exportacao_fica_na_trilha_de_auditoria()
    {
        var pacienteId = await CriarPacienteAsync();

        await _titular.ExportarAsync(pacienteId, "secretaria");

        (await _repo.EventosAuditoriaAsync())
            .Should().Contain(e =>
                e.Acao == "DadosDoTitularExportados" && e.Operador == "secretaria");
    }

    // ==================== Eliminação (art. 18, VI) ====================

    [Fact]
    public async Task Anonimizar_apaga_a_identificacao()
    {
        var pacienteId = await CriarPacienteComHistoriaAsync();

        await _titular.AnonimizarAsync(pacienteId, "Pedido por e-mail em 12/08, recebido por Ana", "gerente");

        var paciente = await _repo.ObterPacienteAsync(pacienteId);
        paciente!.Nome.Should().NotContain("Maria");
        paciente.Documento.Should().BeNull();
        paciente.Telefone.Should().BeNull();
        paciente.Carteirinha.Should().BeNull();
        paciente.DataNascimento.Should().BeNull();
        paciente.Observacoes.Should().BeNull();
    }

    [Fact]
    public async Task Anonimizar_preserva_o_prontuario_sob_guarda_legal()
    {
        var pacienteId = await CriarPacienteComHistoriaAsync();

        var resultado = await _titular.AnonimizarAsync(pacienteId, "pedido do titular", "gerente");

        // A guarda do prontuário é obrigação legal (CFM 1.821/2007) e a LGPD a preserva
        // (art. 16, II). O histórico fica — sem dono identificável.
        resultado.PreservouProntuario.Should().BeTrue();
        resultado.SessoesPreservadas.Should().Be(1);

        (await _prontuario.DoPacienteAsync(pacienteId)).Should().ContainSingle();
        (await _documentos.DoPacienteAsync(pacienteId)).Should().ContainSingle();

        var paciente = await _repo.ObterPacienteComHistoricoAsync(pacienteId);
        paciente!.Atendimentos.Should().ContainSingle();
        paciente.Atendimentos[0].Codigos.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Anonimizar_revoga_os_consentimentos_vigentes()
    {
        var pacienteId = await CriarPacienteComHistoriaAsync();

        var resultado = await _titular.AnonimizarAsync(pacienteId, "pedido do titular", "gerente");

        resultado.ConsentimentosRevogados.Should().Be(2);

        var situacao = await _consentimentos.SituacaoAsync(pacienteId);
        situacao.Values.Should().OnlyContain(c => c.RevogadoEm != null);
    }

    [Fact]
    public async Task Anonimizar_sem_motivo_e_recusado()
    {
        var pacienteId = await CriarPacienteAsync();

        var anonimizar = () => _titular.AnonimizarAsync(pacienteId, "   ", "gerente");

        await anonimizar.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*pedido do titular*");
    }

    [Fact]
    public async Task Anonimizar_guarda_o_nome_original_na_trilha()
    {
        var pacienteId = await CriarPacienteAsync();

        await _titular.AnonimizarAsync(pacienteId, "pedido por telefone em 12/08", "gerente");

        var eventos = await _repo.EventosAuditoriaAsync();
        var evento = eventos.Should().ContainSingle(e => e.Acao == "TitularAnonimizado").Subject;

        // O vínculo entre o pedido e o registro tem de existir em ALGUM lugar, e a
        // auditoria é onde ele pode existir sem aparecer na operação do dia a dia.
        evento.Detalhe.Should().Contain("Maria Souza");
        evento.Detalhe.Should().Contain("pedido por telefone");
        evento.Operador.Should().Be("gerente");
    }

    [Fact]
    public async Task Exportar_depois_de_anonimizar_ja_nao_identifica_ninguem()
    {
        var pacienteId = await CriarPacienteComHistoriaAsync();

        await _titular.AnonimizarAsync(pacienteId, "pedido do titular", "gerente");
        var texto = await _titular.ExportarAsync(pacienteId);

        texto.Should().NotContain("Maria Souza");
        texto.Should().NotContain("123.456.789-09");
        // …e o histórico continua lá, porque é ele que a lei manda guardar.
        texto.Should().Contain("dor lombar há três meses");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
