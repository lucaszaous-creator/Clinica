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
/// Os CAMPOS PERSONALIZADOS do prontuário (set/2026): o que ESTA clínica anota e o sistema
/// não tem.
///
/// O que estes testes prendem é o que NÃO quebra nada quando é esquecido: a cópia do
/// rótulo (renomear o campo não pode reescrever a sessão do mês passado), o rastro da
/// correção (art. 3º da Lei 13.787/2018) e o "quem não edita PRESERVA" — a janela do
/// balcão não mostra estes campos, e regravar sobre eles apagaria o que o médico escreveu,
/// sem erro e sem aviso.
/// </summary>
public class CamposPersonalizadosTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ProntuarioService _prontuario;
    private readonly CampoPersonalizadoService _campos;
    private readonly int _pacienteId;

    private static readonly DateOnly Dia = new(2026, 9, 1);

    public CamposPersonalizadosTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(
            new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();

        var p = new Paciente { Nome = "Maria", Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        _db.SaveChanges();
        _pacienteId = p.Id;

        _repo = new ClinicaRepositorio(_db);
        _prontuario = new ProntuarioService(_repo);
        _campos = new CampoPersonalizadoService(_repo);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    private Task<CampoPersonalizadoProntuario> CampoAsync(
        string rotulo = "Nº de agulhas",
        TipoCampoPersonalizado tipo = TipoCampoPersonalizado.Numero,
        string? opcoes = null, string? modalidade = null, bool ativo = true)
        => _campos.SalvarAsync(new CampoPersonalizadoProntuario
        {
            Rotulo = rotulo, Tipo = tipo, Opcoes = opcoes,
            ModalidadeCodigo = modalidade, Ativo = ativo
        }, "gerente");

    private Evolucao Sessao(IReadOnlyList<ValorCampoPersonalizado>? valores = null, int id = 0)
        => new()
        {
            Id = id,
            PacienteId = _pacienteId,
            Data = Dia,
            QueixaPrincipal = "Lombalgia",
            CamposPersonalizados = valores?.ToList() ?? []
        };

    // ==================== A cópia ====================

    [Fact]
    public async Task O_valor_COPIA_o_rotulo_e_o_tipo_do_campo()
    {
        var campo = await CampoAsync();
        var valores = CampoPersonalizadoService.Montar([campo], new Dictionary<int, string?> { [campo.Id] = "12" });

        var e = await _prontuario.SalvarAsync(Sessao(valores), "medico");

        var gravado = _db.ValoresCampoPersonalizado.AsNoTracking().Single(v => v.EvolucaoId == e.Id);
        gravado.Rotulo.Should().Be("Nº de agulhas");
        gravado.Tipo.Should().Be(TipoCampoPersonalizado.Numero);
        gravado.CampoId.Should().Be(campo.Id);
    }

    [Fact]
    public async Task Renomear_o_campo_NAO_reescreve_a_sessao_ja_gravada()
    {
        // A regra do protocolo do mapa corporal, das escalas e das medidas: aplicar COPIA.
        // Referência viva faria corrigir uma palavra hoje reescrever o prontuário da semana
        // passada — que é exatamente o que a Lei 13.787/2018 proíbe.
        var campo = await CampoAsync();
        var e = await _prontuario.SalvarAsync(
            Sessao(CampoPersonalizadoService.Montar(
                [campo], new Dictionary<int, string?> { [campo.Id] = "12" })), "medico");

        campo.Rotulo = "Agulhas usadas";
        await _campos.SalvarAsync(campo, "gerente");

        var gravado = _db.ValoresCampoPersonalizado.AsNoTracking().Single(v => v.EvolucaoId == e.Id);
        gravado.Rotulo.Should().Be("Nº de agulhas");
    }

    // ==================== O rastro da correção ====================

    [Fact]
    public async Task Corrigir_a_sessao_GUARDA_o_que_os_campos_diziam_antes()
    {
        var campo = await CampoAsync();
        var e = await _prontuario.SalvarAsync(
            Sessao(CampoPersonalizadoService.Montar(
                [campo], new Dictionary<int, string?> { [campo.Id] = "12" })), "medico");

        // A correção: outro valor no mesmo campo.
        await _prontuario.SalvarAsync(
            Sessao(CampoPersonalizadoService.Montar(
                [campo], new Dictionary<int, string?> { [campo.Id] = "8" }), e.Id),
            "medico", "corrigindo a contagem");

        var versao = (await _prontuario.VersoesAsync(e.Id)).Should().ContainSingle().Subject;
        versao.CamposPersonalizados.Should().Be("Nº de agulhas: 12");

        var agora = _db.ValoresCampoPersonalizado.AsNoTracking().Single(v => v.EvolucaoId == e.Id);
        agora.Valor.Should().Be("8");
    }

    [Fact]
    public async Task Quem_NAO_edita_os_campos_PRESERVA_o_que_estava_gravado()
    {
        // A janela do BALCÃO não mostra os campos personalizados: `null` quer dizer "esta
        // tela não os edita". Sem esta regra, salvar uma correção de texto pelo balcão
        // apagaria o que o médico anotou — sem erro e sem aviso (a armadilha da parcela 74).
        var campo = await CampoAsync();
        var e = await _prontuario.SalvarAsync(
            Sessao(CampoPersonalizadoService.Montar(
                [campo], new Dictionary<int, string?> { [campo.Id] = "12" })), "medico");

        var pelaRecepcao = Sessao(id: e.Id);
        pelaRecepcao.CamposPersonalizados = null!;
        await _prontuario.SalvarAsync(pelaRecepcao, "recepcao");

        _db.ValoresCampoPersonalizado.AsNoTracking()
            .Count(v => v.EvolucaoId == e.Id).Should().Be(1);
    }

    [Fact]
    public async Task Lista_VAZIA_apaga_de_propósito()
    {
        // Vazio não é nulo: é a tela que MOSTRA os campos e teve todos apagados. Aí apagar
        // é o certo — e o que estava fica na versão anterior.
        var campo = await CampoAsync();
        var e = await _prontuario.SalvarAsync(
            Sessao(CampoPersonalizadoService.Montar(
                [campo], new Dictionary<int, string?> { [campo.Id] = "12" })), "medico");

        await _prontuario.SalvarAsync(Sessao([], e.Id), "medico");

        _db.ValoresCampoPersonalizado.AsNoTracking()
            .Count(v => v.EvolucaoId == e.Id).Should().Be(0);
        (await _prontuario.VersoesAsync(e.Id)).Single()
            .CamposPersonalizados.Should().Be("Nº de agulhas: 12");
    }

    // ==================== A montagem ====================

    [Fact]
    public async Task Campo_em_branco_NAO_vira_linha()
    {
        // "Não respondido" já é o que a ausência significa: gravar o vazio encheria o
        // prontuário de linhas dizendo que ninguém respondeu.
        var campo = await CampoAsync();

        var valores = CampoPersonalizadoService.Montar(
            [campo], new Dictionary<int, string?> { [campo.Id] = "   " });

        valores.Should().BeEmpty();
    }

    [Fact]
    public async Task Numero_e_gravado_em_cultura_INVARIANTE_e_lido_em_pt_BR()
    {
        // Dois postos com culturas diferentes escreveriam "2,5" e "2.5" na mesma coluna, e
        // a comparação entre sessões deixaria de existir sem nada falhar.
        var campo = await CampoAsync("Carga", TipoCampoPersonalizado.Numero);

        var valor = CampoPersonalizadoService.Montar(
            [campo], new Dictionary<int, string?> { [campo.Id] = "2,5" }).Single();

        valor.Valor.Should().Be("2.5");
        CampoPersonalizadoService.Exibir(valor).Should().Be("2,5");
    }

    [Fact]
    public async Task Numero_invalido_e_RECUSADO_nunca_descartado()
    {
        // Descartar deixaria o campo em branco depois de a pessoa o ter preenchido — o
        // registro afirmaria que ninguém respondeu.
        var campo = await CampoAsync("Carga", TipoCampoPersonalizado.Numero);

        var acao = () => CampoPersonalizadoService.Montar(
            [campo], new Dictionary<int, string?> { [campo.Id] = "doze" });

        acao.Should().Throw<InvalidOperationException>().WithMessage("*espera um número*");
    }

    [Fact]
    public async Task Opcao_fora_da_LISTA_e_recusada()
    {
        var campo = await CampoAsync("Aparelho", TipoCampoPersonalizado.Lista, opcoes: "TENS\nUltrassom");

        var acao = () => CampoPersonalizadoService.Montar(
            [campo], new Dictionary<int, string?> { [campo.Id] = "Laser" });

        acao.Should().Throw<InvalidOperationException>().WithMessage("*não é uma das opções*");
    }

    [Fact]
    public async Task Data_e_gravada_em_ISO_e_lida_em_pt_BR()
    {
        var campo = await CampoAsync("Início dos sintomas", TipoCampoPersonalizado.Data);

        var valor = CampoPersonalizadoService.Montar(
            [campo], new Dictionary<int, string?> { [campo.Id] = "05/03/2026" }).Single();

        valor.Valor.Should().Be("2026-03-05");
        CampoPersonalizadoService.Exibir(valor).Should().Be("05/03/2026");
    }

    // ==================== O catálogo ====================

    [Fact]
    public async Task Campo_de_OUTRA_modalidade_nao_aparece_na_sessao()
    {
        // Campo que aparece onde não serve é o campo que ninguém preenche — e que faz
        // parar de preencher os outros.
        await CampoAsync("Nº de agulhas", modalidade: "Acupuntura");
        await CampoAsync("Escala do humor", TipoCampoPersonalizado.Texto, modalidade: "Consulta");
        await CampoAsync("Observação da sala", TipoCampoPersonalizado.Texto);

        var daSessao = await _campos.DaSessaoAsync("Acupuntura");

        daSessao.Select(c => c.Rotulo).Should()
            .BeEquivalentTo(["Nº de agulhas", "Observação da sala"]);
    }

    [Fact]
    public async Task Campo_DESATIVADO_sai_da_tela_de_escrita_e_o_que_foi_gravado_fica()
    {
        var campo = await CampoAsync();
        var e = await _prontuario.SalvarAsync(
            Sessao(CampoPersonalizadoService.Montar(
                [campo], new Dictionary<int, string?> { [campo.Id] = "12" })), "medico");

        campo.Ativo = false;
        await _campos.SalvarAsync(campo, "gerente");

        (await _campos.DaSessaoAsync(null)).Should().BeEmpty();
        _db.ValoresCampoPersonalizado.AsNoTracking()
            .Count(v => v.EvolucaoId == e.Id).Should().Be(1);
    }

    [Fact]
    public async Task Lista_sem_opcoes_e_recusada_no_cadastro()
    {
        var acao = () => CampoAsync("Aparelho", TipoCampoPersonalizado.Lista);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*escreva as opções*");
    }

    [Fact]
    public async Task O_teto_de_campos_ativos_e_cobrado_e_desativar_sempre_passa()
    {
        for (var i = 0; i < CampoPersonalizadoService.MaximoAtivos; i++)
            await CampoAsync($"Campo {i}", TipoCampoPersonalizado.Texto);

        var acao = () => CampoAsync("Mais um", TipoCampoPersonalizado.Texto);
        await acao.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Desative um*");

        // Desativar um dos existentes NÃO pode ser recusado pelo teto: seria travar
        // justamente a correção que resolve o excesso.
        var existente = (await _campos.TodosAsync()).First();
        existente.Ativo = false;
        var desativar = () => _campos.SalvarAsync(existente, "gerente");
        await desativar.Should().NotThrowAsync();
    }

    // ==================== A leitura de fora ====================

    [Fact]
    public async Task O_resumo_e_UMA_definicao_para_a_versao_a_exportacao_e_o_papel()
    {
        var agulhas = await CampoAsync();
        var aparelho = await CampoAsync("Aparelho", TipoCampoPersonalizado.Lista, opcoes: "TENS\nUltrassom");

        var valores = CampoPersonalizadoService.Montar(
            [agulhas, aparelho],
            new Dictionary<int, string?> { [agulhas.Id] = "12", [aparelho.Id] = "TENS" });

        CampoPersonalizadoService.Resumir(valores).Should().Be("Nº de agulhas: 12 · Aparelho: TENS");
    }

    [Fact]
    public void Resumo_sem_nenhum_campo_e_NULO_nunca_vazio()
        // String vazia gravada seria uma versão afirmando que a sessão TINHA campos e que
        // todos estavam em branco.
        => CampoPersonalizadoService.Resumir([]).Should().BeNull();

    [Fact]
    public async Task A_sessao_lida_de_volta_traz_os_campos()
    {
        // ⚠️ O `Include` que falta não quebra nada no teste (o fixup do EF preenche a
        // navegação), mas em produção a folha sairia sem o que a clínica anotou — a lição
        // da parcela 68. Este teste usa um DbContext NOVO para provar o Include de verdade.
        var campo = await CampoAsync();
        await _prontuario.SalvarAsync(
            Sessao(CampoPersonalizadoService.Montar(
                [campo], new Dictionary<int, string?> { [campo.Id] = "12" })), "medico");

        using var outro = new ClinicaDbContext(
            new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        var repoNovo = new ClinicaRepositorio(outro);

        var sessoes = await repoNovo.EvolucoesDoPacienteAsync(_pacienteId);

        sessoes.Single().CamposPersonalizados.Should().ContainSingle()
            .Which.Rotulo.Should().Be("Nº de agulhas");
    }
}
