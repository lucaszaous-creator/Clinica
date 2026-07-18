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
}
