using Clinica.Domain.Entities;
using Clinica.Recepcao.ViewModels;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests.Ui;

/// <summary>
/// A curva da dor (EVA) no ViewModel do prontuário.
///
/// `EvolucaoDaDorAsync` calcula a série ponto a ponto desde a parcela 2 e nenhuma tela
/// lia os `Pontos`: a evolução de um tratamento inteiro chegava ao balcão como quatro
/// números. O serviço já tinha teste; o que não tinha era a ponte — e é justamente ali
/// que "dado calculado sem leitor" acontece.
///
/// São DUAS séries porque uma só esconderia o caso que mais interessa: o paciente que
/// sai bem de toda sessão e volta na semana seguinte na mesma dor.
/// </summary>
public class ProntuarioEvaTests : IDisposable
{
    private readonly AmbienteDeTela _amb = new();

    private async Task<ProntuarioViewModel> AbrirComPacienteAsync(Paciente paciente)
    {
        var vm = new ProntuarioViewModel(_amb.Escopos, _amb.Snackbar, _amb.Dialogo);
        vm.Seletor.Selecionado = paciente;
        await AmbienteDeTela.AssentarAsync(() => vm.Carregando);
        return vm;
    }

    private async Task SessaoAsync(int pacienteId, DateOnly data, int? antes, int? depois)
    {
        _amb.Db.Evolucoes.Add(new Evolucao
        {
            PacienteId = pacienteId,
            Data = data,
            QueixaPrincipal = "Dor lombar",
            EvaAntes = antes,
            EvaDepois = depois
        });
        await _amb.Db.SaveChangesAsync();
    }

    private static DateOnly Dia(int diasAtras)
        => DateOnly.FromDateTime(DateTime.Today.AddDays(-diasAtras));

    // ==================== A curva ====================

    [Fact]
    public async Task Duas_sessoes_medidas_em_par_desenham_as_duas_linhas()
    {
        var p = await _amb.PacienteAsync();
        await SessaoAsync(p.Id, Dia(14), antes: 8, depois: 5);
        await SessaoAsync(p.Id, Dia(7), antes: 6, depois: 3);

        var vm = await AbrirComPacienteAsync(p);

        vm.TemCurvaDaDor.Should().BeTrue();
        vm.CurvaAntes.Select(x => x.Valor).Should().ContainInOrder(8d, 6d);
        vm.CurvaDepois.Select(x => x.Valor).Should().ContainInOrder(5d, 3d);
        vm.CurvaVazia.Should().BeEmpty();
    }

    [Fact]
    public async Task A_curva_sai_em_ordem_CRONOLOGICA_e_nao_na_ordem_do_prontuario()
    {
        var p = await _amb.PacienteAsync();
        // O prontuário lista do mais novo para o mais velho; o gráfico precisa do contrário.
        await SessaoAsync(p.Id, Dia(1), antes: 4, depois: 2);
        await SessaoAsync(p.Id, Dia(30), antes: 9, depois: 7);

        var vm = await AbrirComPacienteAsync(p);

        vm.CurvaAntes.First().Valor.Should().Be(9d, "a sessão mais antiga abre a curva");
        vm.CurvaAntes.Last().Valor.Should().Be(4d);
        vm.PrimeiraSessaoEva.Should().Be(Dia(30).ToString("dd/MM/yyyy"));
        vm.UltimaSessaoEva.Should().Be(Dia(1).ToString("dd/MM/yyyy"));
    }

    [Fact]
    public async Task Uma_sessao_so_nao_vira_curva_e_a_tela_explica_por_que()
    {
        var p = await _amb.PacienteAsync();
        await SessaoAsync(p.Id, Dia(7), antes: 8, depois: 5);

        var vm = await AbrirComPacienteAsync(p);

        // Com um ponto só não há tendência, e um gráfico daria ao paciente uma conclusão
        // que o dado não sustenta.
        vm.TemCurvaDaDor.Should().BeFalse();
        vm.CurvaVazia.Should().Contain("a partir da segunda");
        // Os KPIs continuam valendo: uma sessão medida já diz de quanto ela aliviou.
        vm.DorInicial.Should().Be("8/10");
        vm.DorAtual.Should().Be("5/10");
    }

    [Fact]
    public async Task Sessao_com_meia_medida_nao_entra_na_curva()
    {
        var p = await _amb.PacienteAsync();
        await SessaoAsync(p.Id, Dia(21), antes: 8, depois: 4);
        await SessaoAsync(p.Id, Dia(14), antes: 7, depois: null);   // só o antes
        await SessaoAsync(p.Id, Dia(7), antes: null, depois: 3);    // só o depois
        await SessaoAsync(p.Id, Dia(1), antes: 5, depois: 2);

        var vm = await AbrirComPacienteAsync(p);

        // A EVA vale em PAR: deixar meia medida entrar faria a linha oscilar por falta de
        // dado, não por dor.
        vm.CurvaAntes.Should().HaveCount(2);
        vm.ResumoEva.Should().Contain("2 de 4");
    }

    [Fact]
    public async Task Paciente_sem_nenhuma_medida_nao_recebe_curva_nem_numero_inventado()
    {
        var p = await _amb.PacienteAsync();
        await SessaoAsync(p.Id, Dia(7), antes: null, depois: null);

        var vm = await AbrirComPacienteAsync(p);

        vm.TemCurvaDaDor.Should().BeFalse();
        vm.CurvaAntes.Should().BeEmpty();
        vm.CurvaDepois.Should().BeEmpty();
        // "—" e "0" são coisas diferentes: zero diria que o paciente não sente dor.
        vm.DorInicial.Should().Be("—");
        vm.GanhoAcumulado.Should().Be("—");
        vm.PrimeiraSessaoEva.Should().Be("—");
    }

    [Fact]
    public async Task Ganho_acumulado_mostra_a_direcao_do_tratamento()
    {
        var p = await _amb.PacienteAsync();
        await SessaoAsync(p.Id, Dia(30), antes: 9, depois: 6);
        await SessaoAsync(p.Id, Dia(1), antes: 4, depois: 2);

        var vm = await AbrirComPacienteAsync(p);

        // Começou em 9, está em 2: sete pontos a menos. É a leitura que o paciente olha.
        vm.GanhoAcumulado.Should().Be("−7 pontos");
    }

    [Fact]
    public async Task Quando_a_dor_piora_o_ganho_aparece_positivo_e_nao_escondido()
    {
        var p = await _amb.PacienteAsync();
        await SessaoAsync(p.Id, Dia(30), antes: 3, depois: 2);
        await SessaoAsync(p.Id, Dia(1), antes: 6, depois: 5);

        var vm = await AbrirComPacienteAsync(p);

        // O ganho é dor INICIAL (o "antes" da primeira sessão = 3) menos dor ATUAL
        // (o "depois" da última = 5). Piorou dois pontos — e a tela diz isso em vez de
        // esconder o sinal.
        vm.GanhoAcumulado.Should().Be("+2 pontos");
        vm.DorInicial.Should().Be("3/10");
        vm.DorAtual.Should().Be("5/10");
    }

    [Fact]
    public async Task Os_rotulos_da_curva_sao_as_datas_das_sessoes()
    {
        var p = await _amb.PacienteAsync();
        await SessaoAsync(p.Id, Dia(10), antes: 8, depois: 5);
        await SessaoAsync(p.Id, Dia(3), antes: 6, depois: 3);

        var vm = await AbrirComPacienteAsync(p);

        // A linha mostra a forma; a direção também precisa da data exata.
        vm.CurvaAntes.Select(x => x.Rotulo)
            .Should().ContainInOrder(Dia(10).ToString("dd/MM"), Dia(3).ToString("dd/MM"));
        vm.CurvaAntes.Select(x => x.Rotulo)
            .Should().BeEquivalentTo(vm.CurvaDepois.Select(x => x.Rotulo));
    }

    [Fact]
    public async Task A_curva_e_do_prontuario_INTEIRO_mesmo_com_a_busca_filtrando_as_sessoes()
    {
        var p = await _amb.PacienteAsync();
        await SessaoAsync(p.Id, Dia(30), antes: 9, depois: 6);
        await SessaoAsync(p.Id, Dia(1), antes: 4, depois: 2);

        var vm = await AbrirComPacienteAsync(p);
        vm.CurvaAntes.Should().HaveCount(2);

        vm.TermoSessao = "nada que exista";
        await AmbienteDeTela.AssentarAsync(() => vm.Carregando);

        // A curva mede o TRATAMENTO. Medir só as sessões que citam uma palavra daria um
        // gráfico que não corresponde a tratamento nenhum.
        vm.Sessoes.Should().BeEmpty();
        vm.CurvaAntes.Should().HaveCount(2);
        vm.TemCurvaDaDor.Should().BeTrue();
    }

    public void Dispose()
    {
        _amb.Dispose();
        GC.SuppressFinalize(this);
    }
}
