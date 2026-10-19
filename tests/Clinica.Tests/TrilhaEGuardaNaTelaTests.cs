using System.Text;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Domain.Prontuario;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que as telas Auditoria e Guarda do prontuário do Gerente AFIRMAM (Blazor desde out/2026,
/// etapa 6) — antes no <c>AuditoriaViewModel</c> e no <c>GuardaProntuarioViewModel</c> do WPF.
/// As frases, o filtro e o CSV são os do WPF, byte a byte onde a clínica os lê.
/// </summary>
public class TrilhaEGuardaNaTelaTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 18);

    private static EventoAuditoria Evento(string acao = "BaixaGuia", string operador = "ana", string? detalhe = "Guia 123",
        int? codigo = null, int? paciente = null, int? lote = null)
        => new()
        {
            DataHora = new DateTime(2026, 10, 18, 14, 5, 0), Acao = acao, Operador = operador, Detalhe = detalhe,
            CodigoId = codigo, PacienteId = paciente, LoteTissId = lote
        };

    // ==================== Auditoria ====================

    [Fact]
    public void O_padrao_e_os_ultimos_30_dias_com_limite_300_e_sem_paciente()
    {
        var p = TrilhaDeAuditoriaNaTela.Padrao(Hoje);

        p.Inicio.Should().Be(new DateOnly(2026, 9, 18));
        p.Fim.Should().Be(Hoje);
        p.Limite.Should().Be(300);
        p.PacienteId.Should().BeNull();
        p.SomenteAcessos.Should().BeFalse();
        TrilhaDeAuditoriaNaTela.Limites.Should().Equal(100, 300, 1000);
    }

    [Fact]
    public void So_quem_abriu_vence_o_combo_de_acao()
    {
        var f = TrilhaDeAuditoriaNaTela.Filtro(new FiltrosDaTrilha(7, true, "Conta", "ana", "123", Hoje, Hoje, 100));

        f.Acao.Should().Be(AcessoProntuarioService.PrefixoAcao);
        f.PacienteId.Should().Be(7);
        f.Operador.Should().Be("ana");
        f.Termo.Should().Be("123");
        f.Limite.Should().Be(100);
    }

    [Fact]
    public void Sem_o_atalho_a_acao_digitada_vai_como_prefixo()
        => TrilhaDeAuditoriaNaTela.Filtro(new FiltrosDaTrilha(null, false, "Conta", null, null, null, null, 300))
            .Acao.Should().Be("Conta");

    [Fact]
    public void A_linha_tem_hora_e_referencias_e_usa_o_nome_do_paciente_quando_ha()
    {
        var e = Evento(codigo: 5, paciente: 9, lote: 2);

        var sem = TrilhaDeAuditoriaNaTela.Linha(e);
        var com = TrilhaDeAuditoriaNaTela.Linha(e, "Maria Silva");

        sem.Quando.Should().Be("18/10/2026 14:05");
        sem.Referencias.Should().Be("guia #5 · paciente #9 · lote #2");
        com.Referencias.Should().Be("guia #5 · Maria Silva · lote #2");
    }

    [Fact]
    public void Operador_vazio_e_detalhe_nulo_nao_saem_em_branco()
    {
        var l = TrilhaDeAuditoriaNaTela.Linha(Evento(operador: " ", detalhe: null));

        l.Operador.Should().Be("(não informado)");
        l.Detalhe.Should().Be("—");
        l.Referencias.Should().BeEmpty();
    }

    [Fact]
    public void O_resumo_diz_quando_o_limite_cortou_o_resultado()
    {
        var r = new ResumoAuditoria(300, [("BaixaGuia", 300)], [("ana", 300)]);

        var cortado = TrilhaDeAuditoriaNaTela.Resumo(r, eventosLidos: 300, limite: 300);
        var inteiro = TrilhaDeAuditoriaNaTela.Resumo(r with { Eventos = 12 }, eventosLidos: 12, limite: 300);
        var vazio = TrilhaDeAuditoriaNaTela.Resumo(new ResumoAuditoria(0, [], []), 0, 300);

        cortado.NoLimite.Should().BeTrue();
        cortado.Texto.Should().Contain("LIMITE ATINGIDO");
        inteiro.NoLimite.Should().BeFalse();
        inteiro.Texto.Should().Be("12 evento(s) no filtro atual.");
        vazio.Texto.Should().Be("Nenhum evento no filtro atual.");
    }

    [Fact]
    public void As_contagens_param_em_doze_e_a_fracao_e_sobre_o_resultado()
    {
        var grupos = Enumerable.Range(1, 15).Select(i => ($"Acao{i}", i)).ToList();

        var c = TrilhaDeAuditoriaNaTela.Contagens(grupos, total: 120);

        c.Should().HaveCount(12);
        c[0].Vezes.Should().Be("1x");
        c[1].Fracao.Should().BeApproximately(2.0 / 120, 1e-9);
        TrilhaDeAuditoriaNaTela.Contagens(grupos, 0).Should().OnlyContain(x => x.Fracao == 0);
    }

    [Fact]
    public void O_csv_tem_as_colunas_do_wpf_na_ordem_da_lista()
    {
        var linhas = new[] { TrilhaDeAuditoriaNaTela.Linha(Evento(codigo: 1)) };

        var (cab, corpo) = TrilhaDeAuditoriaNaTela.Csv(linhas);

        cab.Should().Equal("Quando", "Acao", "Operador", "Detalhe", "Referencias");
        corpo.Single().Should().Equal("18/10/2026 14:05", "BaixaGuia", "ana", "Guia 123", "guia #1");
        TrilhaDeAuditoriaNaTela.NomeDoCsv(Hoje).Should().Be("auditoria-2026-10-18.csv");
    }

    // ==================== Guarda do prontuário ====================

    [Fact]
    public void Zero_elegivel_e_o_normal_e_a_frase_diz_isso()
    {
        var r = GuardaDoProntuarioNaTela.Retrato(new ResumoGuarda(1234, 900, 0, null, 45678));

        r.Pacientes.Should().Be("1.234");
        r.Registros.Should().Be("45.678");
        r.Elegiveis.Should().Be("0");
        r.MaisAntigo.Should().Be("—");
        r.Resumo.Should().Contain("TODOS continuam sob guarda obrigatória");
        GuardaDoProntuarioNaTela.Prazo.Should().Be("20 anos");
    }

    [Fact]
    public void Com_elegiveis_a_frase_lembra_que_o_sistema_nao_elimina()
    {
        var r = GuardaDoProntuarioNaTela.Retrato(new ResumoGuarda(10, 8, 3, new DateOnly(2000, 1, 5), 50));

        r.MaisAntigo.Should().Be("05/01/2000");
        r.Resumo.Should().StartWith("3 prontuário(s)").And.Contain("O sistema não elimina nada").And.Contain("art. 7º");
    }

    [Fact]
    public void A_guarda_do_paciente_conta_pelo_catalogo_e_diz_a_origem_da_data()
    {
        var s = new SituacaoGuarda(1, "Maria", new DateOnly(2026, 3, 2), "sessão",
            new Dictionary<NaturezaRegistroClinico, int> { [NaturezaRegistroClinico.SessaoMedica] = 3 }, 0);

        var g = GuardaDoProntuarioNaTela.Paciente(s, Hoje);

        g.Nome.Should().Be("Maria");
        g.Situacao.Should().Be(s.Descrever(Hoje));
        g.Detalhe.Should().StartWith(s.DescreverContagens()).And.EndWith(" — último registro em 02/03/2026 (sessão)");
    }

    [Fact]
    public void Paciente_sem_registro_diz_que_nao_ha_prazo()
    {
        var s = new SituacaoGuarda(1, "João", null, null, new Dictionary<NaturezaRegistroClinico, int>(), 0);

        var g = GuardaDoProntuarioNaTela.Paciente(s, Hoje);

        g.Detalhe.Should().Be("nada guardado — sem registro no prontuário");
        g.Situacao.Should().Contain("não há prazo de guarda");
    }

    [Fact]
    public void A_linha_de_acesso_mostra_a_porta_humana_nunca_o_identificador()
    {
        var com = GuardaDoProntuarioNaTela.Acesso(Evento("ProntuarioAcessado:Documento", "bia", "Documento clínico aberto"));
        var sem = GuardaDoProntuarioNaTela.Acesso(Evento("ProntuarioAcessado:Documento", "bia", null));

        com.Porta.Should().Be("Documento clínico aberto");
        sem.Porta.Should().Be("Prontuário acessado");
        com.Quando.Should().Be("18/10/2026 14:05");
    }

    [Fact]
    public void O_resumo_dos_acessos_diz_o_vazio_e_o_corte()
    {
        GuardaDoProntuarioNaTela.ResumoDosAcessos(0).Should().Contain("parcela 52");
        GuardaDoProntuarioNaTela.ResumoDosAcessos(3).Should().Be("3 acesso(s) registrados.");
        GuardaDoProntuarioNaTela.ResumoDosAcessos(100).Should().Contain("mostrando os 100 mais recentes");
    }

    [Fact]
    public void A_exportacao_vai_para_uma_pasta_datada_em_utf8_com_bom()
    {
        GuardaDoProntuarioNaTela.PastaDaExportacao(new DateTime(2026, 10, 18, 9, 7, 0)).Should().Be("prontuario-2026-10-18-0907");

        var bytes = GuardaDoProntuarioNaTela.Bytes("Ocupação");

        bytes.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
        Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3).Should().Be("Ocupação");
    }
}
