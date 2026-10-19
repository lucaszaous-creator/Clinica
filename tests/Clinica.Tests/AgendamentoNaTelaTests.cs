using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que o formulário do horário da agenda (Blazor, out/2026) AFIRMA e DECIDE — o mesmo que o
/// <c>AgendamentoEdicaoViewModel</c> do WPF decidia, agora onde o <c>dotnet test</c> alcança.
/// </summary>
public sealed class AgendamentoNaTelaTests
{
    private static readonly DateOnly Dia = new(2026, 10, 20);

    [Fact]
    public void O_titulo_vem_da_porta()
    {
        Assert.Equal("Novo horário", AgendamentoNaTela.Titulo(false, null, null));
        Assert.Equal("Chamar Ana da lista de espera", AgendamentoNaTela.Titulo(false, "Chamar Ana da lista de espera", "ignorado"));
        Assert.Equal("Remarcar horário", AgendamentoNaTela.Titulo(true, "ignorado", null));
        Assert.Equal("Editar o horário", AgendamentoNaTela.Titulo(true, null, "Editar o horário"));
    }

    [Theory]
    [InlineData("14:30", true)]
    [InlineData(" 09:05 ", true)]
    [InlineData("25:00", false)]
    [InlineData("abc", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void A_hora_e_lida_pela_mesma_regra_do_formulario_WPF(string? hora, bool valida)
        => Assert.Equal(valida, AgendamentoNaTela.TentarMontarDataHora(Dia, hora, out _));

    [Fact]
    public void Dia_e_hora_viram_um_instante()
    {
        Assert.True(AgendamentoNaTela.TentarMontarDataHora(Dia, "14:30", out var quando));
        Assert.Equal(new DateTime(2026, 10, 20, 14, 30, 0), quando);
    }

    [Theory]
    [InlineData("50", 50)]
    [InlineData(" 30 ", 30)]
    [InlineData("", null)]
    [InlineData("0", null)]
    [InlineData("-5", null)]
    [InlineData("xx", null)]
    public void Duracao_vazia_ou_invalida_e_o_padrao_do_profissional(string duracao, int? esperado)
        => Assert.Equal(esperado, AgendamentoNaTela.DuracaoInformada(duracao));

    private static string? Recusa(bool paciente = true, bool modalidade = true, bool consulta = false,
        bool especialidade = false, string? hora = "10:00", string? duracao = "", bool edicao = false,
        bool encaixe = false, bool profissional = true)
        => AgendamentoNaTela.RecusaAoSalvar(paciente, modalidade, consulta, especialidade, Dia, hora, duracao,
            edicao, encaixe, profissional);

    [Fact]
    public void Tudo_preenchido_grava()
        => Assert.Null(Recusa());

    [Fact]
    public void As_recusas_seguem_a_ordem_do_formulario()
    {
        Assert.Equal(AgendamentoNaTela.RecusaSemPaciente, Recusa(paciente: false, modalidade: false, hora: "x"));
        Assert.Equal(AgendamentoNaTela.RecusaSemModalidade, Recusa(modalidade: false, hora: "x"));
        Assert.Equal(AgendamentoNaTela.RecusaSemEspecialidade, Recusa(consulta: true, hora: "x"));
        Assert.Equal(AgendamentoNaTela.RecusaHora, Recusa(hora: "x", duracao: "abc"));
        Assert.Equal(AgendamentoNaTela.RecusaDuracao, Recusa(duracao: "0", profissional: false));
        Assert.Equal(AgendamentoNaTela.RecusaDuracao, Recusa(duracao: "abc"));
    }

    [Fact]
    public void Consulta_com_especialidade_passa()
        => Assert.Null(Recusa(consulta: true, especialidade: true));

    [Fact]
    public void Marcar_sem_profissional_e_recusado_mas_o_encaixe_e_a_edicao_passam()
    {
        Assert.Equal(AgendamentoNaTela.RecusaSemProfissional, Recusa(profissional: false));
        Assert.Null(Recusa(profissional: false, encaixe: true));
        Assert.Null(Recusa(profissional: false, edicao: true));
    }

    [Fact]
    public void O_aviso_de_horario_sem_dono_so_existe_na_edicao_depois_de_ler_o_horario()
    {
        Assert.True(AgendamentoNaTela.AvisarSemProfissional(edicao: true, horarioCarregado: true, temProfissional: false));
        Assert.False(AgendamentoNaTela.AvisarSemProfissional(edicao: true, horarioCarregado: false, temProfissional: false));
        Assert.False(AgendamentoNaTela.AvisarSemProfissional(edicao: false, horarioCarregado: true, temProfissional: false));
        Assert.False(AgendamentoNaTela.AvisarSemProfissional(edicao: true, horarioCarregado: true, temProfissional: true));
    }

    [Fact]
    public void Com_a_chave_ligada_criar_exige_os_dois_bits_e_editar_so_a_agenda()
    {
        Assert.Equal(Permissao.EditarAgenda, AgendamentoNaTela.PermissaoParaSalvar(edicao: false, chaveGuiaNoAgendamento: false));
        Assert.Equal(Permissao.EditarAgenda | Permissao.LancarAtendimento,
            AgendamentoNaTela.PermissaoParaSalvar(edicao: false, chaveGuiaNoAgendamento: true));
        Assert.Equal(Permissao.EditarAgenda, AgendamentoNaTela.PermissaoParaSalvar(edicao: true, chaveGuiaNoAgendamento: true));
    }

    [Fact]
    public void A_capa_do_dia_diz_o_verbo_marcar_e_some_sem_atendimento()
    {
        Assert.Null(AgendamentoNaTela.AvisoJaLancado([], Dia));
        var aviso = AgendamentoNaTela.AvisoJaLancado(
            [new CapaDoDia(7, "2026-000007", "Acupuntura", "lançado por ana às 09:10", 2, 1)], Dia);
        Assert.NotNull(aviso);
        Assert.StartsWith("Já lançado em 20/10: nº 2026-000007 (Acupuntura,", aviso);
        Assert.EndsWith("Marcar de novo cria OUTRO atendimento e outro jogo de guias.", aviso);
    }

    [Fact]
    public void Profissional_e_sala_inativos_dizem_que_estao_inativos()
    {
        Assert.Equal("Dr. Caio", AgendamentoNaTela.RotuloProfissional(new Profissional { Nome = "Dr. Caio Rezende", NomeCurto = "Dr. Caio" }));
        Assert.Equal("Rafael (inativo)", AgendamentoNaTela.RotuloProfissional(new Profissional { Nome = "Rafael", Ativo = false }));
        Assert.Equal("Damasco (inativa)", AgendamentoNaTela.RotuloSala(new Sala { Nome = "Damasco", Ativa = false }));
    }

    [Fact]
    public void O_desfecho_carrega_os_avisos_das_guias()
    {
        Assert.Equal("Agenda atualizada.", AgendamentoNaTela.Desfecho([]));
        Assert.Equal("Agenda atualizada — 2 guias regeradas.", AgendamentoNaTela.Desfecho(["2 guias regeradas."]));
    }

    [Fact]
    public void A_recusa_sem_convenio_nomeia_o_paciente()
        => Assert.StartsWith("Ana está sem convênio", AgendamentoNaTela.RecusaSemConvenio("Ana"));

    [Fact]
    public void Os_turnos_da_lista_de_espera_tem_rotulo_e_nao_o_enum_cru()
    {
        Assert.Equal(["Qualquer turno", "Manhã", "Tarde"], ListaEsperaNaTela.Periodos.Select(p => p.Nome));
        Assert.Equal([PeriodoPreferido.Qualquer, PeriodoPreferido.Manha, PeriodoPreferido.Tarde],
            ListaEsperaNaTela.Periodos.Select(p => p.Valor));
        Assert.Equal("Manhã", RotulosEnum.De(PeriodoPreferido.Manha));
    }
}
