using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Cada teste valida que o motor de regras reproduz fielmente o fluxograma operacional correspondente.
/// </summary>
public class RegrasFaturamentoTests
{
    private static readonly DateOnly Hoje = new(2026, 7, 15);

    private static (Paciente, Atendimento) Cenario(Convenio convenio, ModalidadeAtendimento modalidade,
        bool possuiApp = false, Sexo sexo = Sexo.Masculino)
    {
        var paciente = new Paciente { Nome = "Teste", Convenio = convenio, PossuiApp = possuiApp, Sexo = sexo };
        var atendimento = new Atendimento { Data = Hoje, Modalidade = modalidade };
        return (paciente, atendimento);
    }

    // ---------- Unimed Costa do Sol (Padrão) ----------

    [Fact]
    public void UnimedPadrao_ComApp_AcuEEletro_GeraSegundoCodigoEm24h_Verde()
    {
        var (p, a) = Cenario(Convenio.UnimedPadrao, ModalidadeAtendimento.AcupunturaComEletro, possuiApp: true);

        var r = new RegraUnimedPadrao().Gerar(p, a, ContextoFaturamento.Vazio);

        r.Categoria.Should().Be(Categoria.Verde);
        r.Codigos.Should().HaveCount(2);
        var eletro = r.Codigos.Single(c => c.Tipo == TipoCodigo.Eletroacupuntura);
        eletro.Ordem.Should().Be(OrdemCodigo.Segundo);
        eletro.DataPrevistaFaturamento.Should().Be(Hoje.AddDays(1)); // 24h após
        eletro.FormaObtencao.Should().Be(FormaObtencao.Ligacao);
    }

    [Fact]
    public void UnimedPadrao_SemApp_AcuEEletro_SoAcupuntura_SemSegundoCodigo_Amarela()
    {
        var (p, a) = Cenario(Convenio.UnimedPadrao, ModalidadeAtendimento.AcupunturaComEletro, possuiApp: false);

        var r = new RegraUnimedPadrao().Gerar(p, a, ContextoFaturamento.Vazio);

        r.Categoria.Should().Be(Categoria.Amarela);
        r.Codigos.Should().ContainSingle().Which.Tipo.Should().Be(TipoCodigo.Acupuntura);
        r.Codigos.Should().NotContain(c => c.Ordem == OrdemCodigo.Segundo);
    }

    [Fact]
    public void UnimedPadrao_ComApp_BsvComAcupuntura_InverteDatas_SegundoCodigoEm24h()
    {
        var (p, a) = Cenario(Convenio.UnimedPadrao, ModalidadeAtendimento.BsvComAcupuntura, possuiApp: true);

        var r = new RegraUnimedPadrao().Gerar(p, a, ContextoFaturamento.Vazio);

        r.Codigos.Should().HaveCount(2);
        r.Codigos.Single(c => c.Ordem == OrdemCodigo.Primeiro).DataPrevistaFaturamento.Should().Be(Hoje);
        r.Codigos.Single(c => c.Ordem == OrdemCodigo.Segundo).DataPrevistaFaturamento.Should().Be(Hoje.AddDays(1));
        r.Categoria.Should().Be(Categoria.Verde);
    }

    // ---------- Unimed Intercâmbio ----------

    [Fact]
    public void UnimedIntercambio_AcuEEletro_SegundoCodigoPeloSistema_Verde()
    {
        var (p, a) = Cenario(Convenio.UnimedIntercambio, ModalidadeAtendimento.AcupunturaComEletro);

        var r = new RegraUnimedIntercambio().Gerar(p, a, ContextoFaturamento.Vazio);

        r.Categoria.Should().Be(Categoria.Verde);
        var eletro = r.Codigos.Single(c => c.Tipo == TipoCodigo.Eletroacupuntura);
        eletro.DataPrevistaFaturamento.Should().Be(Hoje.AddDays(1));
        eletro.FormaObtencao.Should().Be(FormaObtencao.Sistema); // não precisa ligar
    }

    // ---------- Amil ----------

    [Fact]
    public void Amil_NuncaGeraEletroNemSegundoCodigo_SempreAmarela()
    {
        var (p, a) = Cenario(Convenio.Amil, ModalidadeAtendimento.AcupunturaComEletro);

        var r = new RegraAmil().Gerar(p, a, ContextoFaturamento.Vazio);

        r.Categoria.Should().Be(Categoria.Amarela);
        r.Codigos.Should().OnlyContain(c => c.Tipo != TipoCodigo.Eletroacupuntura);
        r.Codigos.Should().NotContain(c => c.Ordem == OrdemCodigo.Segundo);
    }

    // ---------- Petrobras ----------

    [Fact]
    public void Petrobras_Mulher_RotacionaTresEspecialidadesNoMes()
    {
        var (p, _) = Cenario(Convenio.Petrobras, ModalidadeAtendimento.AcupunturaSimples, sexo: Sexo.Feminino);
        var regra = new RegraPetrobras();
        var acumulados = new List<CodigoFaturamento>();
        var usadas = new List<Especialidade>();

        for (int semana = 0; semana < 3; semana++)
        {
            var atendimento = new Atendimento { Data = Hoje.AddDays(semana * 7), Modalidade = ModalidadeAtendimento.AcupunturaSimples };
            var ctx = new ContextoFaturamento { CodigosNoMes = acumulados.ToList() };
            var r = regra.Gerar(p, atendimento, ctx);

            var codigo = r.Codigos.Single();
            codigo.Status.Should().Be(StatusCodigo.Aberto);
            codigo.Especialidade.Should().NotBeNull();
            usadas.Add(codigo.Especialidade!.Value);
            acumulados.Add(codigo);
        }

        usadas.Should().BeEquivalentTo(new[] { Especialidade.Psiquiatria, Especialidade.Geriatria, Especialidade.Ginecologia });
    }

    [Fact]
    public void Petrobras_Mulher_QuartaSessaoNoMes_NaoEhPossivel()
    {
        var (p, _) = Cenario(Convenio.Petrobras, ModalidadeAtendimento.AcupunturaSimples, sexo: Sexo.Feminino);
        var jaUsadas = new List<CodigoFaturamento>
        {
            new() { Tipo = TipoCodigo.ConsultaEspecialidade, Especialidade = Especialidade.Psiquiatria, Status = StatusCodigo.Aberto },
            new() { Tipo = TipoCodigo.ConsultaEspecialidade, Especialidade = Especialidade.Geriatria, Status = StatusCodigo.Aberto },
            new() { Tipo = TipoCodigo.ConsultaEspecialidade, Especialidade = Especialidade.Ginecologia, Status = StatusCodigo.Aberto },
        };
        var atendimento = new Atendimento { Data = Hoje, Modalidade = ModalidadeAtendimento.AcupunturaSimples };

        var r = new RegraPetrobras().Gerar(p, atendimento, new ContextoFaturamento { CodigosNoMes = jaUsadas });

        r.Codigos.Single().Status.Should().Be(StatusCodigo.NaoAplicavel);
    }

    [Fact]
    public void Petrobras_Homem_LimiteDeDuasSessoesSemGinecologia()
    {
        var (p, _) = Cenario(Convenio.Petrobras, ModalidadeAtendimento.AcupunturaSimples, sexo: Sexo.Masculino);
        var jaUsadas = new List<CodigoFaturamento>
        {
            new() { Tipo = TipoCodigo.ConsultaEspecialidade, Especialidade = Especialidade.Psiquiatria, Status = StatusCodigo.Aberto },
            new() { Tipo = TipoCodigo.ConsultaEspecialidade, Especialidade = Especialidade.Geriatria, Status = StatusCodigo.Aberto },
        };
        var atendimento = new Atendimento { Data = Hoje, Modalidade = ModalidadeAtendimento.AcupunturaSimples };

        var r = new RegraPetrobras().Gerar(p, atendimento, new ContextoFaturamento { CodigosNoMes = jaUsadas });

        // homem não usa Ginecologia → 3ª sessão não é possível
        r.Codigos.Single().Status.Should().Be(StatusCodigo.NaoAplicavel);
    }

    [Fact]
    public void Petrobras_SempreVermelhaENuncaEletro()
    {
        var (p, a) = Cenario(Convenio.Petrobras, ModalidadeAtendimento.AcupunturaComEletro, sexo: Sexo.Feminino);

        var r = new RegraPetrobras().Gerar(p, a, ContextoFaturamento.Vazio);

        r.Categoria.Should().Be(Categoria.Vermelha);
        r.Codigos.Should().NotContain(c => c.Tipo == TipoCodigo.Eletroacupuntura);
    }
}

/// <summary>Valida o comportamento de pendência/baixa da entidade central.</summary>
public class CodigoFaturamentoTests
{
    private static readonly DateOnly Hoje = new(2026, 7, 15);

    [Fact]
    public void SegundoCodigo_SemBaixa_ComDataVencida_EhPendencia()
    {
        var codigo = new CodigoFaturamento
        {
            Ordem = OrdemCodigo.Segundo,
            DataPrevistaFaturamento = Hoje.AddDays(-1),
            Status = StatusCodigo.Aberto
        };

        codigo.EstaPendente(Hoje).Should().BeTrue();
    }

    [Fact]
    public void Codigo_ComDataFutura_NaoEhPendenciaAinda()
    {
        var codigo = new CodigoFaturamento
        {
            DataPrevistaFaturamento = Hoje.AddDays(1),
            Status = StatusCodigo.Aberto
        };

        codigo.EstaPendente(Hoje).Should().BeFalse();
    }

    [Fact]
    public void AposDarBaixa_DeixaDeSerPendencia_ERegistraDados()
    {
        var codigo = new CodigoFaturamento
        {
            DataPrevistaFaturamento = Hoje.AddDays(-1),
            Status = StatusCodigo.Aberto
        };

        codigo.DarBaixa(Hoje, "GUIA-12345", "secretaria", "obtido pelo sistema");

        codigo.EstaPendente(Hoje).Should().BeFalse();
        codigo.Baixado.Should().BeTrue();
        codigo.NumeroGuiaReal.Should().Be("GUIA-12345");
        codigo.Status.Should().Be(StatusCodigo.Baixado);
    }

    [Fact]
    public void NaoAplicavel_NuncaEhPendencia()
    {
        var codigo = new CodigoFaturamento
        {
            DataPrevistaFaturamento = Hoje.AddDays(-5),
            Status = StatusCodigo.NaoAplicavel
        };

        codigo.EstaPendente(Hoje).Should().BeFalse();
    }
}
