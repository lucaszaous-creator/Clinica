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

    // ==================== Os nove campos (parcela 76) ====================

    /// <summary>
    /// ⚠️ O teste que o lugar 3 da auditoria de linha pede: <c>SalvarModeloAsync</c> copia
    /// CAMPO A CAMPO quando o nome já existe, e o que ficar de fora da lista é APAGADO ao
    /// regravar. A criação continua funcionando, que é o que esconde o defeito — foi assim
    /// que o modelo ficou com quatro campos enquanto a evolução passou a ter nove.
    ///
    /// Ele FALHA se alguém acrescentar um campo ao modelo e esquecer a cópia.
    /// </summary>
    [Fact]
    public async Task Regravar_pelo_nome_preserva_os_cinco_campos_da_consulta()
    {
        await _prontuario.SalvarModeloAsync(new ModeloEvolucao
        {
            Nome = "Sessão padrão",
            Conduta = "primeira versão",
            HistoriaDoencaAtual = "lombalgia há 6 meses",
            ExameFisico = "Lasègue negativo",
            HipoteseDiagnostica = "lombalgia mecânica",
            CidSessao = "M54.5",
            PlanoTerapeutico = "10 sessões, reavaliar na 5ª"
        }, "ana");

        await _prontuario.SalvarModeloAsync(new ModeloEvolucao
        {
            Nome = "Sessão padrão",
            Conduta = "segunda versão",
            HistoriaDoencaAtual = "lombalgia há 8 meses",
            ExameFisico = "Lasègue negativo",
            HipoteseDiagnostica = "lombalgia mecânica",
            CidSessao = "M54.5",
            PlanoTerapeutico = "10 sessões, reavaliar na 5ª"
        }, "ana");

        var m = (await _prontuario.ModelosAsync()).Should().ContainSingle().Subject;

        m.Conduta.Should().Be("segunda versão");
        m.HistoriaDoencaAtual.Should().Be("lombalgia há 8 meses",
            "o que não entrar na cópia campo a campo é apagado ao regravar");
        m.ExameFisico.Should().Be("Lasègue negativo");
        m.HipoteseDiagnostica.Should().Be("lombalgia mecânica");
        m.CidSessao.Should().Be("M54.5");
        m.PlanoTerapeutico.Should().Be("10 sessões, reavaliar na 5ª");
    }

    /// <summary>
    /// Modelo só com os campos NOVOS não é modelo vazio. Sem os cinco em
    /// <c>TemConteudo</c>, um roteiro de exame físico e plano seria recusado na gravação
    /// dizendo que não tem nenhuma linha preenchida — com cinco linhas preenchidas.
    /// </summary>
    [Fact]
    public async Task Modelo_so_com_exame_e_plano_e_aceito()
    {
        await _prontuario.SalvarModeloAsync(new ModeloEvolucao
        {
            Nome = "Só o exame",
            ExameFisico = "Inspeção: sem alterações. Palpação: dor em L4-L5.",
            PlanoTerapeutico = "Reavaliar em 4 semanas."
        }, "ana");

        (await _prontuario.ModelosAsync()).Should().ContainSingle()
            .Which.ExameFisico.Should().Contain("L4-L5");
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
