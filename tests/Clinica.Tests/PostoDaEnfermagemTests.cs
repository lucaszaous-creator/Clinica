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
/// O POSTO DA ENFERMAGEM (parcela 88) — de quem é a lista que as telas do Consultório
/// mostram.
///
/// O defeito que estes testes existem para acusar
/// ----------------------------------------------
/// A clínica escreveu: <i>"os enfermeiros podem ver TODOS os pacientes e clicar em
/// ATENDER, em vez de ver só os pacientes dele"</i>. Havia um mecanismo por trás disso, e
/// ele é do tipo mais caro que este projeto conhece — <b>nada falha</b>:
///
/// <list type="number">
///   <item>As cinco telas de lista do Consultório filtram por <c>ProfissionalId</c>.</item>
///   <item>A enfermeira PRECISA de um <c>Profissional</c> vinculado, porque é dele que sai
///   o COREN copiado em cada registro (parcela 72).</item>
///   <item>Os horários pertencem a quem CONSULTA — a enfermagem não tem agenda própria.</item>
/// </list>
///
/// Somados: cadastrá-la CERTO fazia o dia, a semana e a carteira dela abrirem VAZIOS. E
/// tela vazia se lê como sistema quebrado, não como "esta lista não é sua".
///
/// ⚠️ O teste que carrega o arquivo é <see cref="A_carteira_da_enfermeira_e_a_da_clinica_e_a_do_medico_nao_e"/>:
/// ele compara OS DOIS LADOS contra o serviço de verdade, e não só o predicado. É a lição
/// da parcela 64 — quando o mesmo ato existe em dois lugares, o teste que falta é o que
/// compara os dois, e não o que prova que o lado recém-arrumado funciona.
/// </summary>
public class PostoDaEnfermagemTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ConsultorioService _consultorio;

    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    public PostoDaEnfermagemTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _consultorio = new ConsultorioService(_repo);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    // =====================================================================
    // O predicado — quem escreve pelo lado Y e não pelo X
    // =====================================================================

    [Fact]
    public void So_a_enfermagem_escreve_pelo_lado_Y_sem_escrever_pelo_do_medico()
    {
        PerfisAcesso.EscreveComoEnfermagem(PerfisAcesso.Padrao(PerfilAcesso.Enfermagem))
            .Should().BeTrue("é ela que executa e registra sem prescrever nem evoluir");

        foreach (var perfil in new[]
                 {
                     PerfilAcesso.Profissional, PerfilAcesso.Recepcao,
                     PerfilAcesso.Financeiro, PerfilAcesso.Faturista, PerfilAcesso.Gerente
                 })
            PerfisAcesso.EscreveComoEnfermagem(PerfisAcesso.Padrao(perfil))
                .Should().BeFalse($"{perfil} não é o posto da enfermagem");
    }

    /// <summary>
    /// ⚠️ Quem tem OS DOIS lados responde <c>false</c>, e é decisão. O Gerente Geral recebe
    /// <c>Todas</c> — inclusive o bit da enfermagem —, e ele TEM agenda própria como
    /// qualquer profissional: devolver-lhe a clínica inteira na carteira esconderia
    /// justamente os pacientes dele. A regra é "escreve SÓ por Y", não "escreve por Y".
    /// </summary>
    [Fact]
    public void Quem_escreve_pelos_DOIS_lados_nao_e_o_posto_da_enfermagem()
    {
        PerfisAcesso.EscreveComoEnfermagem(PerfisAcesso.Todas).Should().BeFalse();

        // E o caso concreto que produziria o engano: a enfermeira a quem a direção
        // concedeu, em Acessos, o bit de escrever prontuário.
        var enfermagemComProntuario =
            PerfisAcesso.Padrao(PerfilAcesso.Enfermagem) | Permissao.EditarProntuario;

        PerfisAcesso.EscreveComoEnfermagem(enfermagemComProntuario).Should().BeFalse();
    }

    /// <summary>
    /// Sem sessão autenticada, <c>SessaoUsuario.Efetivas</c> devolve <c>Todas</c> — e o
    /// posto NÃO pode virar o da enfermagem por causa disso. É a regra de sempre ("sem
    /// login, libera") vista pelo avesso: aqui ela não pode ALARGAR a lista de ninguém.
    /// </summary>
    [Fact]
    public void Sem_login_o_posto_nao_vira_o_da_enfermagem()
    {
        var sessao = new SessaoUsuario();

        PerfisAcesso.EscreveComoEnfermagem(sessao.Efetivas).Should().BeFalse();
    }

    // =====================================================================
    // A lista: quem é filtrado e quem não é
    // =====================================================================

    [Fact]
    public void A_enfermeira_com_vinculo_ve_a_clinica_e_o_medico_ve_a_agenda_dele()
    {
        const int idDaEnfermeira = 7;
        const int idDoMedico = 3;

        // ⚠️ ESTE é o caso que o defeito produzia: a enfermeira TEM vínculo (precisa dele
        // pelo COREN) e mesmo assim a lista é a da clínica.
        PerfisAcesso.ProfissionalDaListaDoPosto(
                PerfisAcesso.Padrao(PerfilAcesso.Enfermagem), idDaEnfermeira)
            .Should().BeNull("a enfermagem não tem agenda própria — ela passa por todos");

        PerfisAcesso.ProfissionalDaListaDoPosto(
                PerfisAcesso.Padrao(PerfilAcesso.Profissional), idDoMedico)
            .Should().Be(idDoMedico, "os horários são dele");

        // O caso que já existia e continua valendo: sem cadastro vinculado, a clínica.
        PerfisAcesso.ProfissionalDaListaDoPosto(
                PerfisAcesso.Padrao(PerfilAcesso.Profissional), null)
            .Should().BeNull();
    }

    /// <summary>
    /// ⚠️ A frase importa tanto quanto o filtro. Mandar a enfermeira pedir à direção que
    /// conserte um vínculo que JÁ EXISTE faria o suporte procurar um defeito que não há —
    /// é a irmã de "falha exibida como sucesso": instrução errada com cara de instrução
    /// certa.
    /// </summary>
    [Fact]
    public void O_motivo_da_lista_ampla_diz_a_verdade_de_cada_caso()
    {
        var daEnfermeira = PerfisAcesso.MotivoDaListaDoPosto(
            PerfisAcesso.Padrao(PerfilAcesso.Enfermagem), profissionalId: 7);

        daEnfermeira.Should().NotBeNull();
        daEnfermeira!.Should().Contain("não tem agenda própria");
        daEnfermeira.Should().NotContain("não está vinculado",
            "ela ESTÁ vinculada — é o COREN dela que assina cada registro");

        var semVinculo = PerfisAcesso.MotivoDaListaDoPosto(
            PerfisAcesso.Padrao(PerfilAcesso.Profissional), profissionalId: null);

        semVinculo.Should().NotBeNull();
        semVinculo!.Should().Contain("não está vinculado");

        // Com agenda própria e vínculo, não há o que explicar: a lista é dele.
        PerfisAcesso.MotivoDaListaDoPosto(
                PerfisAcesso.Padrao(PerfilAcesso.Profissional), profissionalId: 3)
            .Should().BeNull();
    }

    // =====================================================================
    // O circuito: os dois lados, contra o serviço de verdade
    // =====================================================================

    /// <summary>
    /// O teste que carrega o arquivo. Ele monta a clínica real — um paciente atendido pelo
    /// MÉDICO — e pergunta a carteira das duas maneiras.
    ///
    /// ⚠️ Provar só o predicado deixaria passar o defeito: ele estava no CASAMENTO entre a
    /// regra ("a enfermagem passa por todos") e o filtro do serviço ("os pacientes deste
    /// profissional"). Elo partido aqui não vira erro — vira LISTA VAZIA, indistinguível
    /// de uma clínica sem movimento.
    /// </summary>
    [Fact]
    public async Task A_carteira_da_enfermeira_e_a_da_clinica_e_a_do_medico_nao_e()
    {
        var (pacienteId, medicoId, enfermeiraId) = await ClinicaComUmAtendimentoAsync();

        // O médico: a carteira é a dele, e o paciente está nela.
        var doMedico = await _consultorio.MeusPacientesAsync(
            PerfisAcesso.ProfissionalDaListaDoPosto(
                PerfisAcesso.Padrao(PerfilAcesso.Profissional), medicoId));

        doMedico.Should().ContainSingle(p => p.PacienteId == pacienteId);

        // ⚠️ O DEFEITO: filtrar pela enfermeira devolve VAZIO, porque o horário é do médico.
        // Esta linha é o "antes" — ela documenta por que a tela dela abria em branco.
        (await _consultorio.MeusPacientesAsync(enfermeiraId))
            .Should().BeEmpty("o horário é do médico — a enfermeira não é dona de nenhum");

        // A CORREÇÃO: o posto dela é a clínica, e o paciente aparece.
        var daEnfermeira = await _consultorio.MeusPacientesAsync(
            PerfisAcesso.ProfissionalDaListaDoPosto(
                PerfisAcesso.Padrao(PerfilAcesso.Enfermagem), enfermeiraId));

        daEnfermeira.Should().ContainSingle(p => p.PacienteId == pacienteId,
            "todo paciente passa pela enfermagem");
    }

    /// <summary>
    /// O mesmo, no DIA — a tela que a enfermeira abre para saber quem está na clínica.
    /// </summary>
    [Fact]
    public async Task O_dia_da_enfermeira_e_o_da_clinica_inteira()
    {
        var (_, medicoId, enfermeiraId) = await ClinicaComUmAtendimentoAsync();

        (await _consultorio.DoDiaAsync(Hoje, enfermeiraId)).Sessoes
            .Should().BeEmpty("filtrado por ela, o dia é vazio — o horário é do médico");

        var dia = await _consultorio.DoDiaAsync(
            Hoje,
            PerfisAcesso.ProfissionalDaListaDoPosto(
                PerfisAcesso.Padrao(PerfilAcesso.Enfermagem), enfermeiraId));

        dia.Sessoes.Should().ContainSingle("é o horário que ela vai receber");

        // E o médico continua vendo o dele, sem mudança nenhuma.
        (await _consultorio.DoDiaAsync(Hoje, medicoId)).Sessoes.Should().ContainSingle();
    }

    // =====================================================================
    // Montagem
    // =====================================================================

    /// <summary>
    /// Uma clínica com um paciente atendido pelo MÉDICO hoje, e uma enfermeira cadastrada
    /// como profissional (é o cadastro que o COREN exige) sem horário nenhum.
    /// </summary>
    private async Task<(int PacienteId, int MedicoId, int EnfermeiraId)>
        ClinicaComUmAtendimentoAsync()
    {
        var paciente = new Paciente { Nome = "Maria de Teste", Convenio = Convenio.UnimedIntercambio };
        var medico = new Profissional { Nome = "Dra. Ana Souza", RegistroConselho = "CRM-SP 123456" };
        var enfermeira = new Profissional { Nome = "Joana Técnica", RegistroConselho = "COREN-SP 999999" };
        _db.Pacientes.Add(paciente);
        _db.Profissionais.AddRange(medico, enfermeira);
        await _db.SaveChangesAsync();

        var atendimento = new Atendimento
        {
            PacienteId = paciente.Id,
            Data = Hoje,
            Modalidade = ModalidadeAtendimento.AcupunturaSimples,
            RealizadoEm = DateTime.Today.AddHours(9)
        };
        _db.Atendimentos.Add(atendimento);
        await _db.SaveChangesAsync();

        _db.Agendamentos.Add(new Agendamento
        {
            PacienteId = paciente.Id,
            ProfissionalId = medico.Id,
            DataHora = DateTime.Today.AddHours(9),
            DuracaoMinutos = 60,
            Status = StatusAgendamento.Realizado,
            AtendimentoId = atendimento.Id,
            ModalidadePrevista = ModalidadeAtendimento.AcupunturaSimples
        });
        await _db.SaveChangesAsync();

        return (paciente.Id, medico.Id, enfermeira.Id);
    }
}
