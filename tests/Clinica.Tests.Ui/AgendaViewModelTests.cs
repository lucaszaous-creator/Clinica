using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Recepcao.ViewModels;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests.Ui;

/// <summary>
/// A grade da agenda como ViewModel.
///
/// O que esta suíte protege é a parcela 36: até ela, férias e feriado impediam a marcação
/// mas a grade do dia não mostrava NADA — a terça fechada tinha a mesma cara da terça
/// livre. O serviço devolver o bloqueio (provado em `BloqueioAgendaTests`) não bastava:
/// era o ViewModel que precisava distribuí-lo pela coluna certa, recortado ao dia, e
/// parar de dizer "agenda livre neste dia" em cima dele.
/// </summary>
public class AgendaViewModelTests : IDisposable
{
    private readonly AmbienteDeTela _amb = new();

    private static DateTime As(int hora) => DateTime.Today.AddHours(hora);

    private async Task<AgendaViewModel> AbrirAsync()
    {
        var vm = new AgendaViewModel(_amb.Escopos, _amb.Snackbar, _amb.Dialogo);
        await AmbienteDeTela.AssentarAsync(() => vm.Carregando);
        return vm;
    }

    private async Task<BloqueioAgenda> BloquearAsync(
        DateTime inicio, DateTime fim, string motivo, int? profissionalId = null, int? salaId = null)
    {
        var b = new BloqueioAgenda
        {
            Inicio = inicio,
            Fim = fim,
            Motivo = motivo,
            ProfissionalId = profissionalId,
            SalaId = salaId
        };
        _amb.Db.BloqueiosAgenda.Add(b);
        await _amb.Db.SaveChangesAsync();
        return b;
    }

    // ==================== Colunas ====================

    [Fact]
    public async Task Uma_coluna_por_profissional_ativo()
    {
        await _amb.ProfissionalAsync("Dra. Ana");
        await _amb.ProfissionalAsync("Dr. Bruno");

        var vm = await AbrirAsync();

        vm.Colunas.Should().HaveCount(2);
        vm.SemProfissionais.Should().BeFalse();
    }

    [Fact]
    public async Task Sem_equipe_cadastrada_a_tela_diz_por_que_a_grade_esta_vazia()
    {
        var vm = await AbrirAsync();

        vm.SemProfissionais.Should().BeTrue();
        vm.Colunas.Should().BeEmpty();
    }

    // ==================== Bloqueio na grade ====================

    [Fact]
    public async Task Ferias_do_profissional_aparecem_no_topo_da_coluna_dele()
    {
        var ana = await _amb.ProfissionalAsync("Dra. Ana");
        await _amb.ProfissionalAsync("Dr. Bruno");
        await BloquearAsync(As(8), As(12), "Congresso", profissionalId: ana.Id);

        var vm = await AbrirAsync();

        var colunaAna = vm.Colunas.Single(c => c.Nome.Contains("Ana"));
        var colunaBruno = vm.Colunas.Single(c => c.Nome.Contains("Bruno"));

        colunaAna.Bloqueios.Should().ContainSingle().Which.Motivo.Should().Be("Congresso");
        colunaBruno.Bloqueios.Should().BeEmpty("a folga de um não fecha a agenda do outro");
    }

    [Fact]
    public async Task Feriado_da_clinica_alcanca_todas_as_colunas()
    {
        await _amb.ProfissionalAsync("Dra. Ana");
        await _amb.ProfissionalAsync("Dr. Bruno");
        await BloquearAsync(DateTime.Today, DateTime.Today.AddDays(1), "Feriado nacional");

        var vm = await AbrirAsync();

        vm.Colunas.Should().OnlyContain(c => c.Bloqueios.Count == 1);
    }

    [Fact]
    public async Task Bloqueio_que_cobre_o_dia_inteiro_e_escrito_assim_em_vez_da_faixa_de_horas()
    {
        var ana = await _amb.ProfissionalAsync("Dra. Ana");
        // Férias de duas semanas: dentro da coluna de HOJE, o que importa é que hoje
        // está fechado — "de 01/07 a 15/07" não é resposta para a coluna de um dia.
        await BloquearAsync(
            DateTime.Today.AddDays(-3), DateTime.Today.AddDays(10), "Férias",
            profissionalId: ana.Id);

        var vm = await AbrirAsync();

        vm.Colunas.Single().Bloqueios.Single().Faixa.Should().Be("dia inteiro");
    }

    [Fact]
    public async Task Bloqueio_de_meio_periodo_sai_com_a_faixa_de_horas()
    {
        var ana = await _amb.ProfissionalAsync("Dra. Ana");
        await BloquearAsync(As(8), As(12), "Reunião", profissionalId: ana.Id);

        var vm = await AbrirAsync();

        vm.Colunas.Single().Bloqueios.Single().Faixa.Should().Be("08:00–12:00");
    }

    [Fact]
    public async Task Coluna_com_bloqueio_deixa_de_dizer_que_a_agenda_esta_livre()
    {
        var ana = await _amb.ProfissionalAsync("Dra. Ana");
        await BloquearAsync(As(8), As(18), "Férias", profissionalId: ana.Id);

        var vm = await AbrirAsync();

        // "Agenda livre neste dia" debaixo de um bloqueio é o convite exato para marcar
        // em cima dele.
        vm.Colunas.Single().Vazia.Should().BeFalse();
        vm.Colunas.Single().Resumo.Should().Contain("bloqueio");
    }

    [Fact]
    public async Task Sala_fechada_vai_para_a_faixa_acima_da_grade_e_nao_para_as_colunas()
    {
        var ana = await _amb.ProfissionalAsync("Dra. Ana");
        var sala = new Sala { Nome = "Consultório 2" };
        _amb.Db.Salas.Add(sala);
        await _amb.Db.SaveChangesAsync();

        await BloquearAsync(As(8), As(18), "Manutenção do ar", salaId: sala.Id);

        var vm = await AbrirAsync();

        // Sala fechada não fecha a agenda de ninguém — ela tira um lugar de todo mundo.
        vm.Colunas.Single().Bloqueios.Should().BeEmpty();
        vm.TemAvisoSalas.Should().BeTrue();
        vm.AvisoSalas.Should().Contain("Consultório 2").And.Contain("Manutenção do ar");
        _ = ana;
    }

    [Fact]
    public async Task Dia_sem_bloqueio_nao_inventa_faixa_de_aviso()
    {
        await _amb.ProfissionalAsync("Dra. Ana");

        var vm = await AbrirAsync();

        vm.TemAvisoSalas.Should().BeFalse();
        vm.BloqueiosNaoVerificados.Should().BeFalse();
        vm.Colunas.Single().Vazia.Should().BeTrue();
    }

    // ==================== Semana ====================

    [Fact]
    public async Task Modo_semana_troca_as_colunas_de_profissional_para_dia()
    {
        await _amb.ProfissionalAsync("Dra. Ana");

        var vm = await AbrirAsync();
        vm.Colunas.Should().ContainSingle();

        vm.ModoSemana = true;
        await AmbienteDeTela.AssentarAsync(() => vm.Carregando);

        vm.Colunas.Should().HaveCount(7, "a semana vai de segunda a domingo");
        vm.Resumo.Should().Contain("na semana");
    }

    [Fact]
    public async Task Na_semana_os_botoes_andam_de_sete_em_sete_dias()
    {
        await _amb.ProfissionalAsync("Dra. Ana");

        var vm = await AbrirAsync();
        vm.ModoSemana = true;
        await AmbienteDeTela.AssentarAsync(() => vm.Carregando);

        var antes = vm.Dia.Date;
        vm.ProximoDiaCommand.Execute(null);
        await AmbienteDeTela.AssentarAsync(() => vm.Carregando);

        // Avançar um dia numa grade que mostra a semana inteira não mudaria quase nada.
        vm.Dia.Date.Should().Be(antes.AddDays(7));
    }

    // ==================== Lista de espera ====================

    [Fact]
    public async Task Lista_de_espera_abre_inteira_e_diz_isso_no_titulo()
    {
        await _amb.ProfissionalAsync("Dra. Ana");
        var p = await _amb.PacienteAsync();
        _amb.Db.ListaEspera.Add(new ListaEspera { PacienteId = p.Id, CriadoEm = DateTime.Now });
        await _amb.Db.SaveChangesAsync();

        var vm = await AbrirAsync();

        vm.Espera.Should().ContainSingle();
        vm.TituloEspera.Should().Be("Lista de espera");
        vm.TemSugestao.Should().BeFalse();
    }

    [Fact]
    public async Task Apontar_a_lista_para_um_horario_muda_o_titulo_e_o_texto_do_vazio()
    {
        var ana = await _amb.ProfissionalAsync("Dra. Ana");
        var p = await _amb.PacienteAsync();
        var ag = await _amb.AgendamentoAsync(p.Id, As(14), profissionalId: ana.Id);

        var vm = await AbrirAsync();
        var cartao = vm.Colunas.Single().Horarios.Single();
        cartao.AgendamentoId.Should().Be(ag.Id);

        vm.QuemChamarCommand.Execute(cartao);
        await AmbienteDeTela.AssentarAsync(() => vm.Carregando);

        // Filtro invisível é filtro que engana: "ninguém espera" e "ninguém serve para
        // este horário" são respostas diferentes.
        vm.TemSugestao.Should().BeTrue();
        vm.TituloEspera.Should().Contain("Quem chamar para");
        vm.EsperaVazia.Should().Contain("turno");
    }

    public void Dispose()
    {
        _amb.Dispose();
        GC.SuppressFinalize(this);
    }
}
