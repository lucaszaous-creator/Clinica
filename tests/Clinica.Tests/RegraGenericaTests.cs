using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using FluentAssertions;

namespace Clinica.Tests;

public class RegraGenericaTests
{
    private static Atendimento Atend(ModalidadeAtendimento m) => new() { Data = new DateOnly(2026, 7, 10), Modalidade = m };
    private static Paciente Pac(bool app = false) => new() { Nome = "P", Convenio = Convenio.Personalizado, PossuiApp = app, Sexo = Sexo.Feminino };

    private static ResultadoFaturamento Gerar(ConfiguracaoRegraGenerica cfg, ModalidadeAtendimento m, bool app = false)
        => new RegraGenerica().Gerar(Pac(app), Atend(m), new ContextoFaturamento { DiasSegundoCodigo = cfg.DiasSegundoCodigo, Generica = cfg });

    [Fact]
    public void SemEletroESemSegundoCodigo_FaturaSoAcupuntura()
    {
        var cfg = new ConfiguracaoRegraGenerica { FazEletro = false, TemSegundoCodigo = false };
        var r = Gerar(cfg, ModalidadeAtendimento.AcupunturaComEletro);

        r.Codigos.Should().ContainSingle();
        r.Codigos[0].Tipo.Should().Be(TipoCodigo.Acupuntura);
        r.Codigos[0].Ordem.Should().Be(OrdemCodigo.Primeiro);
    }

    [Fact]
    public void ComEletroESegundoCodigo_GeraOSegundoEmMaisDias()
    {
        var cfg = new ConfiguracaoRegraGenerica
        {
            FazEletro = true, TemSegundoCodigo = true, FormaSegundoCodigo = FormaObtencao.Sistema, DiasSegundoCodigo = 2
        };
        var r = Gerar(cfg, ModalidadeAtendimento.AcupunturaComEletro);

        r.Codigos.Should().HaveCount(2);
        var segundo = r.Codigos.Single(c => c.Ordem == OrdemCodigo.Segundo);
        segundo.Tipo.Should().Be(TipoCodigo.Eletroacupuntura);
        segundo.DataPrevistaFaturamento.Should().Be(new DateOnly(2026, 7, 12));
        segundo.FormaObtencao.Should().Be(FormaObtencao.Sistema);
    }

    [Fact]
    public void SegundoCodigoDependeApp_SemApp_NaoGeraSegundo()
    {
        var cfg = new ConfiguracaoRegraGenerica
        {
            FazEletro = true, TemSegundoCodigo = true, SegundoCodigoDependeApp = true
        };
        var r = Gerar(cfg, ModalidadeAtendimento.AcupunturaComEletro, app: false);

        r.Codigos.Should().ContainSingle();
        r.Avisos.Should().NotBeEmpty();
    }

    [Fact]
    public void SegundoCodigoDependeApp_ComApp_GeraSegundo()
    {
        var cfg = new ConfiguracaoRegraGenerica
        {
            FazEletro = true, TemSegundoCodigo = true, SegundoCodigoDependeApp = true
        };
        var r = Gerar(cfg, ModalidadeAtendimento.AcupunturaComEletro, app: true);

        r.Codigos.Should().HaveCount(2);
    }

    [Fact]
    public void NaoFaturaBsv_ModalidadeBsvApenas_NadaGerado()
    {
        var cfg = new ConfiguracaoRegraGenerica { FaturaBsv = false };
        var r = Gerar(cfg, ModalidadeAtendimento.BsvApenas);

        r.Codigos.Should().BeEmpty();
        r.Avisos.Should().NotBeEmpty();
    }

    [Fact]
    public void FaturaBsv_ModalidadeBsvApenas_GeraBsv()
    {
        var cfg = new ConfiguracaoRegraGenerica { FaturaBsv = true };
        var r = Gerar(cfg, ModalidadeAtendimento.BsvApenas);

        r.Codigos.Should().ContainSingle(c => c.Tipo == TipoCodigo.Bsv);
    }

    [Fact]
    public void InverteDatasBsv_GeraInstrucaoNoPrimeiroCodigo()
    {
        var cfg = new ConfiguracaoRegraGenerica { FaturaBsv = true, InverteDatasBsv = true };
        var r = Gerar(cfg, ModalidadeAtendimento.BsvComAcupuntura);

        r.Codigos.First(c => c.Tipo == TipoCodigo.Bsv).Descricao.Should().Contain("inverter");
    }

    [Fact]
    public void Categoria_AcompanhaAppConformeConfig()
    {
        var cfg = new ConfiguracaoRegraGenerica { CategoriaComApp = Categoria.Verde, CategoriaSemApp = Categoria.Vermelha };

        Gerar(cfg, ModalidadeAtendimento.AcupunturaSimples, app: true).Categoria.Should().Be(Categoria.Verde);
        Gerar(cfg, ModalidadeAtendimento.AcupunturaSimples, app: false).Categoria.Should().Be(Categoria.Vermelha);
    }

    // ===== Sul América (nov/2026): acu + eletro no mesmo dia; acupuntura a cada 1 dia, eletro a cada 7 =====

    private static ConfiguracaoRegraGenerica SulAmerica() => new()
    {
        FazEletro = true, EletroNoMesmoDia = true, IntervaloAcupunturaDias = 1, IntervaloEletroDias = 7,
        FormaSegundoCodigo = FormaObtencao.Sistema
    };

    private static CodigoFaturamento Anterior(TipoCodigo tipo, DateOnly data, StatusCodigo status = StatusCodigo.Baixado)
        => new() { Tipo = tipo, DataPrevistaFaturamento = data, Status = status };

    private static ResultadoFaturamento GerarCom(ConfiguracaoRegraGenerica cfg, ModalidadeAtendimento m, params CodigoFaturamento[] recentes)
        => new RegraGenerica().Gerar(Pac(), Atend(m),
            new ContextoFaturamento { DiasSegundoCodigo = cfg.DiasSegundoCodigo, Generica = cfg, CodigosRecentes = recentes });

    [Fact]
    public void SulAmerica_acupuntura_e_eletro_saem_no_mesmo_dia_como_primeiro_codigo()
    {
        var r = GerarCom(SulAmerica(), ModalidadeAtendimento.AcupunturaComEletro);

        r.Codigos.Should().HaveCount(2);
        r.Codigos.Should().OnlyContain(c => c.Ordem == OrdemCodigo.Primeiro
                                            && c.DataPrevistaFaturamento == new DateOnly(2026, 7, 10));
        r.Codigos.Select(c => c.Tipo).Should().BeEquivalentTo([TipoCodigo.Acupuntura, TipoCodigo.Eletroacupuntura]);
        r.Avisos.Should().BeEmpty();
    }

    [Fact]
    public void SulAmerica_eletro_no_mesmo_dia_nao_depende_do_app_nem_do_segundo_codigo()
    {
        var cfg = SulAmerica();
        cfg.TemSegundoCodigo = true;
        cfg.SegundoCodigoDependeApp = true;

        var r = GerarCom(cfg, ModalidadeAtendimento.AcupunturaComEletro);

        r.Codigos.Should().Contain(c => c.Tipo == TipoCodigo.Eletroacupuntura && c.Ordem == OrdemCodigo.Primeiro);
    }

    [Fact]
    public void SulAmerica_eletro_antes_de_7_dias_fica_para_quando_o_convenio_libera()
    {
        // Última eletro em 07/07; a de 10/07 só autoriza em 14/07.
        var r = GerarCom(SulAmerica(), ModalidadeAtendimento.AcupunturaComEletro,
            Anterior(TipoCodigo.Eletroacupuntura, new DateOnly(2026, 7, 7)),
            Anterior(TipoCodigo.Acupuntura, new DateOnly(2026, 7, 7)));

        var acu = r.Codigos.Single(c => c.Tipo == TipoCodigo.Acupuntura);
        acu.DataPrevistaFaturamento.Should().Be(new DateOnly(2026, 7, 10), "a acupuntura libera com 1 dia");

        var eletro = r.Codigos.Single(c => c.Tipo == TipoCodigo.Eletroacupuntura);
        eletro.DataPrevistaFaturamento.Should().Be(new DateOnly(2026, 7, 14));
        eletro.Ordem.Should().Be(OrdemCodigo.Segundo, "é obtida depois do atendimento");
        eletro.FormaObtencao.Should().Be(FormaObtencao.Sistema);
        eletro.Descricao.Should().Contain("14/07/2026").And.Contain("7 dias");
        r.Avisos.Should().ContainSingle(a => a.Contains("eletroacupuntura") && a.Contains("14/07/2026") && a.Contains("07/07/2026"));
    }

    [Fact]
    public void SulAmerica_eletro_com_7_dias_ou_mais_sai_no_dia()
    {
        var r = GerarCom(SulAmerica(), ModalidadeAtendimento.AcupunturaComEletro,
            Anterior(TipoCodigo.Eletroacupuntura, new DateOnly(2026, 7, 3)));

        r.Codigos.Single(c => c.Tipo == TipoCodigo.Eletroacupuntura).DataPrevistaFaturamento
            .Should().Be(new DateOnly(2026, 7, 10));
        r.Avisos.Should().BeEmpty();
    }

    [Fact]
    public void SulAmerica_eletro_ja_adiada_empurra_a_proxima_para_depois_dela()
    {
        // A fila já tem uma eletro adiada para 14/07: a de hoje (10/07) vai para 21/07.
        var r = GerarCom(SulAmerica(), ModalidadeAtendimento.AcupunturaComEletro,
            Anterior(TipoCodigo.Eletroacupuntura, new DateOnly(2026, 7, 7)),
            Anterior(TipoCodigo.Eletroacupuntura, new DateOnly(2026, 7, 14), StatusCodigo.Aberto));

        r.Codigos.Single(c => c.Tipo == TipoCodigo.Eletroacupuntura).DataPrevistaFaturamento
            .Should().Be(new DateOnly(2026, 7, 21));
    }

    [Fact]
    public void SulAmerica_lancamento_retroativo_nao_encosta_numa_eletro_futura()
    {
        // Lançado em 10/07, com outra eletro já em 13/07: menos de 7 dias de distância.
        var r = GerarCom(SulAmerica(), ModalidadeAtendimento.AcupunturaComEletro,
            Anterior(TipoCodigo.Eletroacupuntura, new DateOnly(2026, 7, 13), StatusCodigo.Aberto));

        r.Codigos.Single(c => c.Tipo == TipoCodigo.Eletroacupuntura).DataPrevistaFaturamento
            .Should().Be(new DateOnly(2026, 7, 20));
    }

    [Fact]
    public void SulAmerica_guia_nao_aplicavel_nao_conta_no_intervalo()
    {
        var r = GerarCom(SulAmerica(), ModalidadeAtendimento.AcupunturaComEletro,
            Anterior(TipoCodigo.Eletroacupuntura, new DateOnly(2026, 7, 8), StatusCodigo.NaoAplicavel));

        r.Codigos.Single(c => c.Tipo == TipoCodigo.Eletroacupuntura).DataPrevistaFaturamento
            .Should().Be(new DateOnly(2026, 7, 10));
    }

    [Fact]
    public void SulAmerica_segunda_acupuntura_no_mesmo_dia_vai_para_o_dia_seguinte()
    {
        var r = GerarCom(SulAmerica(), ModalidadeAtendimento.AcupunturaSimples,
            Anterior(TipoCodigo.Acupuntura, new DateOnly(2026, 7, 10), StatusCodigo.Aberto));

        var acu = r.Codigos.Single();
        acu.DataPrevistaFaturamento.Should().Be(new DateOnly(2026, 7, 11));
        acu.Descricao.Should().Contain("por dia");
    }

    [Fact]
    public void Sem_intervalo_configurado_o_historico_nao_muda_nada()
    {
        var cfg = new ConfiguracaoRegraGenerica { FazEletro = true, TemSegundoCodigo = true, DiasSegundoCodigo = 1 };
        var r = GerarCom(cfg, ModalidadeAtendimento.AcupunturaComEletro,
            Anterior(TipoCodigo.Eletroacupuntura, new DateOnly(2026, 7, 10), StatusCodigo.Aberto),
            Anterior(TipoCodigo.Acupuntura, new DateOnly(2026, 7, 10), StatusCodigo.Aberto));

        r.Codigos.Single(c => c.Tipo == TipoCodigo.Acupuntura).DataPrevistaFaturamento.Should().Be(new DateOnly(2026, 7, 10));
        r.Codigos.Single(c => c.Tipo == TipoCodigo.Eletroacupuntura).DataPrevistaFaturamento.Should().Be(new DateOnly(2026, 7, 11));
        r.Avisos.Should().BeEmpty();
    }

    [Fact]
    public void Intervalo_da_eletro_vale_tambem_para_o_segundo_codigo_classico()
    {
        // Sem "mesmo dia": a eletro é 2º código em +1d (11/07), mas a anterior foi em 08/07 → 15/07.
        var cfg = new ConfiguracaoRegraGenerica { FazEletro = true, TemSegundoCodigo = true, DiasSegundoCodigo = 1, IntervaloEletroDias = 7 };
        var r = GerarCom(cfg, ModalidadeAtendimento.AcupunturaComEletro,
            Anterior(TipoCodigo.Eletroacupuntura, new DateOnly(2026, 7, 8)));

        var eletro = r.Codigos.Single(c => c.Tipo == TipoCodigo.Eletroacupuntura);
        eletro.DataPrevistaFaturamento.Should().Be(new DateOnly(2026, 7, 15));
        eletro.Descricao.Should().Contain("Adiado para 15/07/2026");
    }
}
