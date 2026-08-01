using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Recepcao.ViewModels;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests.Ui;

/// <summary>
/// O quadro do balcão, testado como ViewModel — a camada que até aqui só tinha a
/// garantia de COMPILAR.
///
/// A regra dos três relógios está provada no domínio (`FilaDoBalcaoTests`), mas quem
/// decide o que aparece em cada cartão é o ViewModel: em que coluna ele entra, em que
/// ORDEM, com que texto e sob que filtro. Um `switch` trocado ali põe o paciente atrasado
/// na coluna errada com o domínio inteiro verde.
/// </summary>
public class FilaViewModelTests : IDisposable
{
    private readonly AmbienteDeTela _amb = new();

    private static readonly DateTime Hoje = DateTime.Today;
    private static DateTime As(int hora, int minuto = 0) => Hoje.AddHours(hora).AddMinutes(minuto);

    private FilaViewModel Abrir() => new(
        _amb.Servico<AgendaService>(),
        _amb.Servico<PainelRecepcaoService>(),
        _amb.Escopos,
        _amb.Snackbar,
        _amb.Dialogo);

    private async Task<FilaViewModel> AbrirCarregadaAsync()
    {
        var vm = Abrir();
        await AmbienteDeTela.AssentarAsync(() => vm.Carregando);
        return vm;
    }

    // ==================== As colunas ====================

    [Fact]
    public async Task Cada_carimbo_de_hora_manda_o_cartao_para_a_sua_coluna()
    {
        var p = await _amb.PacienteAsync();

        await _amb.AgendamentoAsync(p.Id, As(9));                                  // aguardando
        await _amb.AgendamentoAsync(p.Id, As(10), chegada: As(9, 55));              // na recepção
        await _amb.AgendamentoAsync(p.Id, As(11), chegada: As(10, 58), inicio: As(11)); // em atendimento
        await _amb.AgendamentoAsync(p.Id, As(8), status: StatusAgendamento.Realizado);   // finalizado
        await _amb.AgendamentoAsync(p.Id, As(12), status: StatusAgendamento.Faltou);     // fora da fila

        var vm = await AbrirCarregadaAsync();

        vm.Aguardando.Should().ContainSingle();
        vm.NaRecepcao.Should().ContainSingle();
        vm.EmAtendimento.Should().ContainSingle();
        vm.Finalizados.Should().ContainSingle();
        vm.ForaDaFila.Should().ContainSingle();
        vm.QuadroVazio.Should().BeFalse();
    }

    [Fact]
    public async Task Quadro_com_so_faltas_nao_conta_como_dia_com_movimento()
    {
        var p = await _amb.PacienteAsync();
        await _amb.AgendamentoAsync(p.Id, As(9), status: StatusAgendamento.Cancelado);

        var vm = await AbrirCarregadaAsync();

        // O cancelado aparece no rodapé (é onde se desfaz o clique errado), mas as quatro
        // colunas do fluxo estão vazias — e é isso que o estado vazio da tela diz.
        vm.QuadroVazio.Should().BeTrue();
        vm.ForaDaFila.Should().ContainSingle();
        vm.TituloVazio.Should().Be("Ninguém marcado para hoje");
    }

    // ==================== A ordem de chamada ====================

    [Fact]
    public async Task Na_recepcao_quem_espera_ha_mais_tempo_vem_primeiro()
    {
        var pontual = await _amb.PacienteAsync("Marcado 9h30, chegou 9h30");
        var atrasado = await _amb.PacienteAsync("Marcado 9h, chegou 9h45");

        // Pela HORA MARCADA, o das 9h viria primeiro. Mas o relógio da clínica só começa
        // a correr para ele às 9h45, quando ele apareceu — enquanto o das 9h30 está sendo
        // feito de esperar desde as 9h30. É o das 9h30 que espera há mais tempo.
        await _amb.AgendamentoAsync(atrasado.Id, As(9), chegada: As(9, 45));
        await _amb.AgendamentoAsync(pontual.Id, As(9, 30), chegada: As(9, 30));

        var vm = await AbrirCarregadaAsync();

        vm.NaRecepcao.Should().HaveCount(2);
        vm.NaRecepcao[0].Paciente.Should().Be("Marcado 9h30, chegou 9h30");
        vm.NaRecepcao[1].Paciente.Should().Be("Marcado 9h, chegou 9h45");
    }

    [Fact]
    public async Task Chegar_cedo_nao_fura_a_fila_de_quem_tem_hora_antes()
    {
        var cedo = await _amb.PacienteAsync("Marcado 9h30, chegou 9h05");
        var naHora = await _amb.PacienteAsync("Marcado 9h, chegou 9h");

        // O contrário do teste acima, e a mesma regra: quem chegou uma hora antes
        // escolheu esperar, e a espera dele só começa quando chega a hora dele.
        await _amb.AgendamentoAsync(cedo.Id, As(9, 30), chegada: As(9, 5));
        await _amb.AgendamentoAsync(naHora.Id, As(9), chegada: As(9));

        var vm = await AbrirCarregadaAsync();

        vm.NaRecepcao[0].Paciente.Should().Be("Marcado 9h, chegou 9h");
        vm.NaRecepcao[1].Paciente.Should().Be("Marcado 9h30, chegou 9h05");
    }

    [Fact]
    public async Task Aguardando_segue_a_cronologia_do_dia()
    {
        var p = await _amb.PacienteAsync();
        await _amb.AgendamentoAsync(p.Id, As(16));
        await _amb.AgendamentoAsync(p.Id, As(8));
        await _amb.AgendamentoAsync(p.Id, As(12));

        var vm = await AbrirCarregadaAsync();

        vm.Aguardando.Select(c => c.Horario).Should().ContainInOrder("08:00", "12:00", "16:00");
    }

    // ==================== O texto de cada situação ====================

    [Fact]
    public async Task Quem_chega_adiantado_nao_e_marcado_como_espera_longa()
    {
        var p = await _amb.PacienteAsync();
        // Marcado para daqui a duas horas, chegou agora: uma hora e meia "esperando" que
        // não é atraso da clínica — e vermelho aqui seria alarme falso todo dia.
        await _amb.AgendamentoAsync(
            p.Id, DateTime.Now.AddHours(2), chegada: DateTime.Now.AddMinutes(-90));

        var vm = await AbrirCarregadaAsync();

        var cartao = vm.NaRecepcao.Should().ContainSingle().Subject;
        cartao.SituacaoCritica.Should().BeFalse();
        cartao.Situacao.Should().Contain("antes da hora");
    }

    [Fact]
    public async Task Espera_longa_da_clinica_fica_critica()
    {
        var p = await _amb.PacienteAsync();
        // Hora marcada há 50 minutos, chegou na hora: a clínica está devendo 50 minutos.
        var marcado = DateTime.Now.AddMinutes(-50);
        await _amb.AgendamentoAsync(p.Id, marcado, chegada: marcado);

        var vm = await AbrirCarregadaAsync();

        var cartao = vm.NaRecepcao.Should().ContainSingle().Subject;
        cartao.Situacao.Should().Contain("esperando há");
        cartao.SituacaoCritica.Should().BeTrue();
    }

    [Fact]
    public async Task Paciente_que_nao_chegou_e_cuja_hora_passou_aparece_atrasado()
    {
        var p = await _amb.PacienteAsync();
        await _amb.AgendamentoAsync(p.Id, DateTime.Now.AddMinutes(-40));

        var vm = await AbrirCarregadaAsync();

        var cartao = vm.Aguardando.Should().ContainSingle().Subject;
        cartao.Situacao.Should().Contain("atrasado");
        cartao.SituacaoCritica.Should().BeTrue();
    }

    [Fact]
    public async Task Quem_ainda_tem_hora_pela_frente_nao_recebe_rotulo_nenhum()
    {
        var p = await _amb.PacienteAsync();
        await _amb.AgendamentoAsync(p.Id, DateTime.Now.AddHours(3));

        var vm = await AbrirCarregadaAsync();

        // Cartão sem problema não precisa de rótulo: enchê-lo de texto faria o rótulo
        // que importa se perder no meio.
        vm.Aguardando.Should().ContainSingle().Which.Situacao.Should().BeEmpty();
    }

    [Fact]
    public async Task Sessao_que_estoura_a_duracao_prevista_e_acusada()
    {
        var p = await _amb.PacienteAsync();
        await _amb.AgendamentoAsync(
            p.Id, DateTime.Now.AddMinutes(-60),
            chegada: DateTime.Now.AddMinutes(-60),
            inicio: DateTime.Now.AddMinutes(-45),
            duracaoMinutos: 30);

        var vm = await AbrirCarregadaAsync();

        var cartao = vm.EmAtendimento.Should().ContainSingle().Subject;
        cartao.Situacao.Should().Contain("na sala há");
        cartao.Situacao.Should().Contain("passou de 30 min");
        cartao.SituacaoCritica.Should().BeTrue();
    }

    // ==================== O filtro ====================

    [Fact]
    public async Task Busca_por_nome_recorta_as_colunas_e_a_tela_diz_que_esta_filtrada()
    {
        var maria = await _amb.PacienteAsync("Maria Silva");
        var joao = await _amb.PacienteAsync("João Souza");
        await _amb.AgendamentoAsync(maria.Id, As(9));
        await _amb.AgendamentoAsync(joao.Id, As(10));

        var vm = await AbrirCarregadaAsync();
        vm.Aguardando.Should().HaveCount(2);

        vm.Busca = "maria";

        vm.Aguardando.Should().ContainSingle().Which.Paciente.Should().Be("Maria Silva");
        vm.Filtrando.Should().BeTrue();
        vm.Resumo.Should().Contain("filtrado");
    }

    [Fact]
    public async Task Filtro_que_nao_acha_ninguem_diz_que_e_o_filtro_e_nao_o_dia()
    {
        var p = await _amb.PacienteAsync("Maria Silva");
        await _amb.AgendamentoAsync(p.Id, As(9));

        var vm = await AbrirCarregadaAsync();
        vm.Busca = "zzzz";

        // "Ninguém marcado para hoje" faria a recepção concluir que o dia está livre.
        vm.QuadroVazio.Should().BeTrue();
        vm.TituloVazio.Should().Be("Nada bate com o filtro");
        vm.DescricaoVazia.Should().Contain("limpe o filtro");
    }

    [Fact]
    public async Task Filtro_por_profissional_mostra_so_a_fila_dele()
    {
        var p = await _amb.PacienteAsync();
        var ana = await _amb.ProfissionalAsync("Dra. Ana");
        var bruno = await _amb.ProfissionalAsync("Dr. Bruno");

        await _amb.AgendamentoAsync(p.Id, As(9), profissionalId: ana.Id);
        await _amb.AgendamentoAsync(p.Id, As(10), profissionalId: bruno.Id);

        var vm = await AbrirCarregadaAsync();
        vm.Profissionais.Should().HaveCount(3, "duas pessoas mais a opção 'todos'");

        vm.ProfissionalFiltro = vm.Profissionais.Single(f => f.Id == ana.Id);

        vm.Aguardando.Should().ContainSingle().Which.Profissional.Should().Contain("Ana");
        vm.Filtrando.Should().BeTrue();
    }

    [Fact]
    public async Task Limpar_filtro_devolve_o_quadro_inteiro()
    {
        var maria = await _amb.PacienteAsync("Maria Silva");
        var joao = await _amb.PacienteAsync("João Souza");
        await _amb.AgendamentoAsync(maria.Id, As(9));
        await _amb.AgendamentoAsync(joao.Id, As(10));

        var vm = await AbrirCarregadaAsync();
        vm.Busca = "maria";
        vm.Aguardando.Should().ContainSingle();

        vm.LimparFiltroCommand.Execute(null);

        vm.Busca.Should().BeEmpty();
        vm.Filtrando.Should().BeFalse();
        vm.Aguardando.Should().HaveCount(2);
    }

    // ==================== O desfazer do balcão ====================

    [Fact]
    public async Task Reabrir_traz_a_falta_de_volta_para_a_coluna_em_que_ela_estava()
    {
        var p = await _amb.PacienteAsync();
        // Tinha feito check-in e foi marcado como falta por engano.
        await _amb.AgendamentoAsync(
            p.Id, As(9), chegada: As(8, 55), status: StatusAgendamento.Faltou);

        var vm = await AbrirCarregadaAsync();
        var cartao = vm.ForaDaFila.Should().ContainSingle().Subject;
        cartao.MotivoForaDaFila.Should().Be("Faltou");
        cartao.PodeReabrir.Should().BeTrue();

        vm.ReabrirCommand.Execute(cartao);
        await AmbienteDeTela.AssentarAsync(() => vm.Carregando);

        vm.ForaDaFila.Should().BeEmpty();
        // Volta para "Na recepção", não para "Aguardando": o carimbo de chegada não foi
        // apagado, porque reabrir é DESFAZER — não é uma segunda alteração silenciosa.
        vm.NaRecepcao.Should().ContainSingle();
        vm.Aguardando.Should().BeEmpty();
    }

    [Fact]
    public async Task Cartao_fora_da_fila_nao_oferece_falta_nem_cancelamento()
    {
        var p = await _amb.PacienteAsync();
        await _amb.AgendamentoAsync(p.Id, As(9), status: StatusAgendamento.Cancelado);

        var vm = await AbrirCarregadaAsync();

        var cartao = vm.ForaDaFila.Should().ContainSingle().Subject;
        cartao.EmAberto.Should().BeFalse("o que já saiu da fila não sai duas vezes");
        cartao.PodeChegar.Should().BeFalse();
        cartao.PodeFinalizar.Should().BeFalse();
    }

    // ==================== O check-in ====================

    [Fact]
    public async Task Check_in_sem_nada_a_dizer_confirma_pelo_snackbar_e_nao_abre_dialogo()
    {
        var p = await _amb.PacienteAsync();

        // O consentimento LGPD precisa estar colhido: sem ele, o check-in TEM o que
        // dizer, e com razão. (Foi o CI que mostrou isso — o teste nasceu supondo que
        // paciente recém-cadastrado passa limpo, e ele não passa.)
        await _amb.Servico<ConsentimentoService>().RegistrarAsync(
            p.Id, FinalidadeConsentimento.TratamentoDeDados, concedido: true);

        await _amb.AgendamentoAsync(p.Id, As(9));

        var vm = await AbrirCarregadaAsync();
        var cartao = vm.Aguardando.Should().ContainSingle().Subject;

        vm.RegistrarChegadaCommand.Execute(cartao);
        await AmbienteDeTela.AssentarAsync(() => vm.Carregando);

        vm.NaRecepcao.Should().ContainSingle();

        // Diálogo é interrupção: sem nada a resolver, ele não aparece — o snackbar basta.
        _amb.Dialogo.Avisos.Should().BeEmpty();
        _amb.Snackbar.Sucessos.Should().Contain(m => m.Contains("chegou"));
    }

    [Fact]
    public async Task Paciente_sem_consentimento_LGPD_e_barrado_no_check_in()
    {
        var p = await _amb.PacienteAsync();
        await _amb.AgendamentoAsync(p.Id, As(9));

        var vm = await AbrirCarregadaAsync();
        vm.RegistrarChegadaCommand.Execute(vm.Aguardando[0]);
        await AmbienteDeTela.AssentarAsync(() => vm.Carregando);

        // O balcão é o único lugar onde se colhe consentimento com o paciente presente.
        _amb.Dialogo.Avisos.Should().ContainSingle()
            .Which.Mensagem.Should().Contain("consentimento LGPD");
    }

    [Fact]
    public async Task Aviso_de_elegibilidade_do_check_in_vira_etiqueta_permanente_no_cartao()
    {
        var p = await _amb.PacienteAsync();
        // Carteirinha vencida: o check-in tem o que dizer.
        p.ValidadeCarteirinha = DateOnly.FromDateTime(DateTime.Today.AddDays(-10));
        await _amb.Db.SaveChangesAsync();

        await _amb.AgendamentoAsync(p.Id, As(9));

        var vm = await AbrirCarregadaAsync();
        vm.RegistrarChegadaCommand.Execute(vm.Aguardando[0]);
        await AmbienteDeTela.AssentarAsync(() => vm.Carregando);

        _amb.Dialogo.Avisos.Should().NotBeEmpty("carteirinha vencida tem de parar o balcão");

        // E, depois do OK do diálogo, o aviso continua no cartão: lido e esquecido no OK,
        // ele não sobreviveria ao próximo paciente.
        var cartao = vm.NaRecepcao.Should().ContainSingle().Subject;
        cartao.TemAvisos.Should().BeTrue();
        cartao.Avisos.Should().NotBeEmpty();
    }

    // ==================== Navegação de dia ====================

    [Fact]
    public async Task Trocar_de_dia_recarrega_o_quadro()
    {
        var p = await _amb.PacienteAsync();
        await _amb.AgendamentoAsync(p.Id, As(9));
        await _amb.AgendamentoAsync(p.Id, As(9).AddDays(1));

        var vm = await AbrirCarregadaAsync();
        vm.Aguardando.Should().ContainSingle();

        vm.ProximoDiaCommand.Execute(null);
        await AmbienteDeTela.AssentarAsync(() => vm.Carregando);

        vm.Dia.Date.Should().Be(DateTime.Today.AddDays(1));
        vm.Aguardando.Should().ContainSingle();
    }

    public void Dispose()
    {
        _amb.Dispose();
        GC.SuppressFinalize(this);
    }
}
