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
/// MODELOS DE EVOLUÇÃO (parcela 63) — o roteiro da sessão que se repete.
///
/// <c>ModeloDocumento</c> existe desde a parcela 3 e serve só aos papéis IMPRESSOS. A
/// evolução, que é o texto mais escrito do sistema, não tinha nada: a sessão de
/// acupuntura tem sempre a mesma forma e era redigitada por inteiro, várias vezes por dia.
///
/// A regra que carrega tudo é a mesma do protocolo do mapa corporal e da venda do pacote:
/// <b>aplicar COPIA, nunca aponta</b>. Aqui ela não é só desenho — é a Lei 13.787/2018:
/// referência viva faria corrigir uma palavra do modelo hoje reescrever o prontuário da
/// sessão da semana passada.
/// </summary>
public class ModeloEvolucaoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ProntuarioService _prontuario;

    public ModeloEvolucaoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _prontuario = new ProntuarioService(_repo);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    // ==================== A regra que carrega tudo ====================

    /// <summary>
    /// ⚠️ <b>Corrigir o modelo NÃO reescreve a sessão já salva.</b>
    ///
    /// É o teste central desta parcela. Se o modelo fosse referenciado em vez de copiado,
    /// ajustar uma palavra do roteiro hoje mudaria o que o prontuário diz sobre uma sessão
    /// que aconteceu no mês passado — e o prontuário deixaria de provar o que foi feito,
    /// que é a única coisa que ele existe para fazer.
    /// </summary>
    [Fact]
    public async Task Corrigir_o_modelo_nao_reescreve_a_sessao_ja_salva()
    {
        var pacienteId = await CriarPacienteAsync();

        var modelo = await _prontuario.SalvarModeloAsync(new ModeloEvolucao
        {
            Nome = "Sessão de acupuntura — lombar",
            Conduta = "Agulhamento de B23, B25, VG4. 20 minutos."
        }, "ana");

        // A sessão é salva com o texto COPIADO do modelo — é o que a tela faz ao aplicar.
        var evolucao = await _prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = pacienteId,
            Data = new DateOnly(2026, 7, 10),
            Conduta = modelo.Conduta
        }, "ana");

        // Meses depois, o roteiro da clínica muda.
        modelo.Conduta = "Agulhamento de B23, B25, VG4 e IG4. 30 minutos.";
        await _prontuario.SalvarModeloAsync(modelo, "ana");

        var salva = await _prontuario.ObterAsync(evolucao.Id);
        salva!.Conduta.Should().Be("Agulhamento de B23, B25, VG4. 20 minutos.",
            "o prontuário registra o que foi feito NAQUELE dia, não o que o roteiro diz hoje");
    }

    /// <summary>
    /// E apagar o modelo também não mexe em sessão nenhuma — que é justamente o que
    /// permite apagá-lo. O modelo é a única coisa "de prontuário" deste sistema que se
    /// apaga mesmo, e é por não registrar o que aconteceu com ninguém.
    /// </summary>
    [Fact]
    public async Task Apagar_o_modelo_nao_toca_nas_sessoes_escritas_com_ele()
    {
        var pacienteId = await CriarPacienteAsync();

        var modelo = await _prontuario.SalvarModeloAsync(new ModeloEvolucao
        {
            Nome = "Sessão padrão",
            Conduta = "Agulhamento padrão."
        }, "ana");

        var evolucao = await _prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = pacienteId,
            Data = new DateOnly(2026, 7, 10),
            Conduta = modelo.Conduta
        }, "ana");

        await _prontuario.RemoverModeloAsync(modelo.Id);

        (await _prontuario.ModelosAsync()).Should().BeEmpty();

        var salva = await _prontuario.ObterAsync(evolucao.Id);
        salva!.Conduta.Should().Be("Agulhamento padrão.");
    }

    // ==================== De quem é o modelo ====================

    /// <summary>
    /// O profissional enxerga os DELE e os da clínica — nunca os atalhos pessoais de um
    /// colega. Cada um escreve de um jeito, e o modelo pessoal de outra pessoa na lista é
    /// ruído que ninguém vai limpar.
    /// </summary>
    [Fact]
    public async Task Cada_um_ve_os_seus_e_os_da_clinica()
    {
        var ana = await CriarProfissionalAsync("Dra. Ana");
        var bruno = await CriarProfissionalAsync("Dr. Bruno");

        await _prontuario.SalvarModeloAsync(
            new ModeloEvolucao { Nome = "Padrão da casa", Conduta = "x" }, "gerente");
        await _prontuario.SalvarModeloAsync(
            new ModeloEvolucao { Nome = "Meu jeito", ProfissionalId = ana, Conduta = "y" }, "ana");
        await _prontuario.SalvarModeloAsync(
            new ModeloEvolucao { Nome = "Meu jeito", ProfissionalId = bruno, Conduta = "z" }, "bruno");

        var daAna = await _prontuario.ModelosAsync(ana);

        daAna.Select(m => m.Nome).Should().BeEquivalentTo(["Padrão da casa", "Meu jeito"]);
        daAna.Single(m => m.Nome == "Meu jeito").Conduta.Should().Be("y");

        // O da CLÍNICA vem primeiro: é o padrão combinado, e quem abre a lista pela
        // primeira vez deve encontrá-lo antes dos atalhos pessoais.
        daAna[0].Nome.Should().Be("Padrão da casa");
    }

    /// <summary>
    /// Dois donos podem usar o MESMO nome — o índice é por dono. Um índice global faria o
    /// "Sessão padrão" do Bruno sobrescrever o da Ana em silêncio, que é a pior forma de
    /// perder o texto: sem erro e sem aviso.
    /// </summary>
    [Fact]
    public async Task Mesmo_nome_para_donos_diferentes_sao_modelos_diferentes()
    {
        var ana = await CriarProfissionalAsync("Dra. Ana");

        await _prontuario.SalvarModeloAsync(
            new ModeloEvolucao { Nome = "Sessão padrão", Conduta = "da clínica" }, "gerente");
        await _prontuario.SalvarModeloAsync(
            new ModeloEvolucao { Nome = "Sessão padrão", ProfissionalId = ana, Conduta = "da Ana" }, "ana");

        var daAna = await _prontuario.ModelosAsync(ana);

        daAna.Should().HaveCount(2);
        daAna.Should().Contain(m => m.DaClinica && m.Conduta == "da clínica");
        daAna.Should().Contain(m => !m.DaClinica && m.Conduta == "da Ana");
    }

    /// <summary>
    /// Nome repetido para o MESMO dono sobrescreve em vez de duplicar — é o que quem
    /// clica "guardar como modelo" pela segunda vez espera, e é a mesma regra do modelo
    /// de documento (parcela 3).
    /// </summary>
    [Fact]
    public async Task Nome_repetido_do_mesmo_dono_sobrescreve()
    {
        await _prontuario.SalvarModeloAsync(
            new ModeloEvolucao { Nome = "Sessão padrão", Conduta = "primeira versão" }, "ana");
        await _prontuario.SalvarModeloAsync(
            new ModeloEvolucao { Nome = "Sessão padrão", Conduta = "segunda versão" }, "ana");

        var modelos = await _prontuario.ModelosAsync();

        modelos.Should().ContainSingle().Which.Conduta.Should().Be("segunda versão");
    }

    // ==================== As recusas ====================

    /// <summary>
    /// Modelo VAZIO é recusado. Aplicado, ele apagaria o que já estava escrito na sessão —
    /// e o profissional não teria como saber que foi o modelo. Recusar na gravação é o
    /// lugar barato de impedir.
    /// </summary>
    [Fact]
    public async Task Modelo_sem_uma_linha_preenchida_e_recusado()
    {
        var salvar = () => _prontuario.SalvarModeloAsync(
            new ModeloEvolucao { Nome = "Vazio" }, "ana");

        await salvar.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nenhuma linha preenchida*");
    }

    [Fact]
    public async Task Modelo_sem_nome_e_recusado()
    {
        var salvar = () => _prontuario.SalvarModeloAsync(
            new ModeloEvolucao { Nome = "  ", Conduta = "x" }, "ana");

        await salvar.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nome*");
    }

    // ==================== Cenário ====================

    private async Task<int> CriarPacienteAsync()
    {
        var p = new Paciente { Nome = "Maria", Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<int> CriarProfissionalAsync(string nome)
    {
        var p = new Profissional { Nome = nome };
        _db.Profissionais.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }
}
