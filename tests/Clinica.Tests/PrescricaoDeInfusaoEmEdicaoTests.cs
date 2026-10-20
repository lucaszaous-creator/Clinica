using Clinica.Application.Abstracoes;
using Clinica.Application.Assinatura;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O editor da prescrição de infusão depois de sair do WPF (out/2026): o estado e a gravação do
/// antigo <c>PrescricaoInternaEdicaoViewModel</c> moram em <see cref="PrescricaoDeInfusaoEmEdicao"/>
/// (Application), e estes testes são o PORTE das conferências que a ferramenta
/// <c>tools/VerificarInfusao</c> fazia sobre o ViewModel — a ferramenta saiu junto com a janela.
/// O que ela renderizava em PNG não tem porte: o leiaute é Razor, e quem o abre e clica é o
/// <c>TelasBlazorAbremTests</c>.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class PrescricaoDeInfusaoEmEdicaoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly DbContextOptions<ClinicaDbContext> _opcoes;
    private readonly ServiceProvider _provedor;
    private readonly IServiceScopeFactory _escopos;
    private static Task<bool> Sim(string _, string __) => Task.FromResult(true);

    public PrescricaoDeInfusaoEmEdicaoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _opcoes = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        using (var db = new ClinicaDbContext(_opcoes)) db.Database.EnsureCreated();
        var s = new ServiceCollection();
        s.AddClinica("Host=teste;Database=teste");
        s.AddScoped(_ => new ClinicaDbContext(_opcoes));
        _provedor = s.BuildServiceProvider();
        _escopos = _provedor.GetRequiredService<IServiceScopeFactory>();
    }

    public void Dispose()
    {
        new SessaoUsuario().Entrar(new UsuarioSistema());
        _provedor.Dispose();
        _conn.Dispose();
    }

    [Fact]
    public async Task Abre_com_uma_infusao_e_a_lidocaina_orienta_quantidade_sem_preencher_a_dose()
    {
        var c = await CenarioAsync();
        var editor = new PrescricaoDeInfusaoEmEdicao(_escopos, c.PacienteId, "Paciente fictício", c.ProfissionalId);
        await editor.InicializarAsync();

        editor.MensagemEhErro.Should().BeFalse(editor.Mensagem);
        editor.Infusoes.Should().HaveCount(1, "só a Infusão 1 ao abrir");

        var lidocaina = BuscaMedicamentos.Buscar(editor.CatalogoMedicamentos, "li").First();
        editor.Infusoes[0].Itens[0].Descricao = lidocaina.Texto;
        editor.Infusoes[0].Itens[0].Dose.Should().BeNull("escolher o medicamento não define dose");
        editor.Infusoes[0].Itens[0].DicaDose.Should().Be("Quantidade em mL");
    }

    [Fact]
    public async Task Modelo_guarda_a_composicao_sem_notas_do_paciente_e_nao_reaproveita_horario()
    {
        var c = await CenarioAsync();
        var editor = new PrescricaoDeInfusaoEmEdicao(_escopos, c.PacienteId, "Paciente fictício", c.ProfissionalId);
        await editor.InicializarAsync();
        var grupo = editor.Infusoes[0];
        grupo.Itens[0].Descricao = "Lidocaína 2%";
        grupo.Volume = "250 mL"; grupo.Itens[0].Dose = "7 mL"; grupo.Horario = "09:00";
        grupo.Itens[0].Observacoes = "Nota do paciente fictício";

        (await editor.SalvarModeloAsync(grupo, "Composição demonstrativa")).Should().BeTrue(editor.Mensagem);
        var modelo = editor.Modelos.Single(m => m.Nome == "Composição demonstrativa");
        var conteudo = ModeloInfusao.Ler(modelo.ConfiguracaoInfusao);
        conteudo.Indicacao.Should().BeNull();
        conteudo.Observacoes.Should().BeNull();
        conteudo.Itens.Should().OnlyContain(i => i.Observacoes == null, "o modelo é da clínica, a nota é do paciente");

        editor.CriarInfusao();
        editor.Infusoes.Should().HaveCount(2);
        await editor.AplicarModeloAsync(editor.Infusoes[1], modelo, Sim);
        editor.Infusoes[1].Horario.Should().BeNull("o modelo não reaproveita horário");
    }

    [Fact]
    public async Task Rascunho_conserva_as_duas_infusoes_e_reabrir_devolve_dose_e_preparo()
    {
        var c = await CenarioAsync();
        var editor = new PrescricaoDeInfusaoEmEdicao(_escopos, c.PacienteId, "Paciente fictício", c.ProfissionalId);
        await editor.InicializarAsync();
        editor.Infusoes[0].Itens[0].Descricao = "Lidocaína 2%";
        editor.Infusoes[0].Itens[0].Dose = "7 mL";
        editor.Infusoes[0].Volume = "250 mL";
        editor.CriarInfusao();
        editor.Infusoes[1].Itens[0].Descricao = "Dexametasona 4 mg";
        editor.Infusoes[1].Diluente = "Diluente demonstrativo";
        editor.Infusoes[1].Volume = "100 mL";

        await editor.SalvarRascunhoAsync();
        editor.MensagemEhErro.Should().BeFalse(editor.Mensagem);

        await using var db = new ClinicaDbContext(_opcoes);
        var p = await db.PrescricoesInternas.Include(x => x.Itens).SingleAsync();
        p.Itens.OrderBy(i => i.Ordem).Select(i => i.GrupoInfusao).Should().Equal(1, 2);

        var reaberta = new PrescricaoDeInfusaoEmEdicao(_escopos, c.PacienteId, "Paciente fictício", c.ProfissionalId, prescricaoId: p.Id);
        await reaberta.InicializarAsync();
        reaberta.Infusoes.Should().HaveCount(2);
        reaberta.Infusoes[0].Itens[0].Dose.Should().Be("7 mL");
        reaberta.Infusoes[1].Volume.Should().Be("100 mL");
    }

    [Fact]
    public void Itens_antigos_independentes_nao_viram_uma_mistura()
    {
        var legado = GrupoInfusaoEdicao.Carregar([new() { Descricao = "A" }, new() { Descricao = "B" }], false, null, null);
        legado.Should().HaveCount(2);
    }

    [Fact]
    public async Task Com_continuidade_libera_sem_registrar_assinatura_inexistente_e_sem_pedir_certificado()
    {
        var c = await CenarioAsync(continuidade: true);
        var editor = new PrescricaoDeInfusaoEmEdicao(_escopos, c.PacienteId, "Paciente fictício", c.ProfissionalId);
        await editor.InicializarAsync();
        editor.ContinuidadeAtiva.Should().BeTrue();
        editor.Infusoes[0].Itens[0].Descricao = "Lidocaína 2%";
        editor.Infusoes[0].Itens[0].Dose = "7 mL";
        var pediuCertificado = false;

        await editor.AssinarAsync(Sim, _ => { pediuCertificado = true; return Task.FromResult<CertificadoAssinatura?>(null); });

        editor.MensagemEhErro.Should().BeFalse(editor.Mensagem);
        editor.Liberou.Should().BeTrue();
        editor.Assinou.Should().BeFalse();
        pediuCertificado.Should().BeFalse();
        await using var db = new ClinicaDbContext(_opcoes);
        var p = await db.PrescricoesInternas.SingleAsync();
        p.Situacao.Should().Be(SituacaoPrescricao.Liberada);
        p.AssinadaEm.Should().BeNull();
    }

    [Fact]
    public async Task Sem_continuidade_desistir_no_seletor_grava_o_rascunho_e_nao_libera()
    {
        var c = await CenarioAsync();
        var editor = new PrescricaoDeInfusaoEmEdicao(_escopos, c.PacienteId, "Paciente fictício", c.ProfissionalId);
        await editor.InicializarAsync();
        editor.Infusoes[0].Itens[0].Descricao = "Lidocaína 2%";

        await editor.AssinarAsync(Sim, _ => Task.FromResult<CertificadoAssinatura?>(null));

        editor.Liberou.Should().BeFalse();
        await using var db = new ClinicaDbContext(_opcoes);
        (await db.PrescricoesInternas.SingleAsync()).Situacao.Should().Be(SituacaoPrescricao.Rascunho);
    }

    // ==================== Apoio ====================

    private sealed record Cenario(int PacienteId, int ProfissionalId);

    private async Task<Cenario> CenarioAsync(bool continuidade = false)
    {
        await using var db = new ClinicaDbContext(_opcoes);
        var paciente = new Paciente { Nome = "Paciente fictício — demonstração" };
        var profissional = new Profissional { Nome = "Médico de demonstração", RegistroConselho = "CRM-SP 123456", Cpf = "11111111111", Ativo = true };
        db.Pacientes.Add(paciente); db.Profissionais.Add(profissional);
        await db.SaveChangesAsync();
        var repo = new ClinicaRepositorio(db);
        var usuario = await new AcessoService(repo).CriarAsync(profissional.Nome, "demo.infusao", "Teste#Infusao2026", PerfilAcesso.Profissional);
        usuario.ProfissionalId = profissional.Id; usuario.Profissional = profissional;
        await db.SaveChangesAsync();
        if (continuidade)
        {
            await repo.SalvarConfiguracaoAsync(ContinuidadeSemAssinatura.Configuracao, "true");
            await repo.SalvarAsync();
        }
        new SessaoUsuario().Entrar(usuario);
        return new Cenario(paciente.Id, profissional.Id);
    }
}
