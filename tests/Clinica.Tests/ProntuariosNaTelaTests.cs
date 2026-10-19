using Clinica.Application.Modelos;
using Xunit;

namespace Clinica.Tests;

/// <summary>O que a tela de Prontuários AFIRMA (migrada do ProntuariosViewModel WPF em out/2026).</summary>
public class ProntuariosNaTelaTests
{
    private static LinhaProntuario Linha(string paciente, SituacaoLinhaProntuario situacao,
        NaturezaLinhaProntuario natureza = NaturezaLinhaProntuario.Anamnese) =>
        new(natureza, 1, paciente, new DateOnly(2026, 10, 1), "", "Anamnese", situacao);

    private static readonly IReadOnlyList<LinhaProntuario> Todas =
    [
        Linha("Maria Souza", SituacaoLinhaProntuario.AAssinar),
        Linha("João Lima", SituacaoLinhaProntuario.Assinada),
        Linha("maria clara", SituacaoLinhaProntuario.AAssinar),
    ];

    [Fact]
    public void Filtra_por_paciente_sem_diferenciar_maiusculas()
        => Assert.Equal(2, ProntuariosNaTela.Filtrar(Todas, " MARIA ", false).Count);

    [Fact]
    public void So_assinaturas_pendentes_recorta_a_lista()
        => Assert.All(ProntuariosNaTela.Filtrar(Todas, null, true), l => Assert.Equal(SituacaoLinhaProntuario.AAssinar, l.Situacao));

    [Fact]
    public void O_rotulo_do_chip_conta_a_lista_inteira()
        => Assert.Equal("Assinar pendentes (2)", ProntuariosNaTela.RotuloPendentes(Todas));

    [Fact]
    public void O_resumo_diz_que_esta_filtrado()
    {
        var desde = new DateOnly(2026, 9, 1);
        Assert.StartsWith("1 de 3 registro(s)", ProntuariosNaTela.Resumo(3, 1, true, desde));
        Assert.StartsWith("3 registro(s)", ProntuariosNaTela.Resumo(3, 3, false, desde));
        Assert.EndsWith("desde 01/09/2026", ProntuariosNaTela.Resumo(3, 3, false, desde));
    }

    [Fact]
    public void O_tom_segue_o_que_a_linha_pede()
    {
        Assert.Equal("aviso", ProntuariosNaTela.Tom(Linha("a", SituacaoLinhaProntuario.AAssinar)));
        Assert.Equal("erro", ProntuariosNaTela.Tom(Linha("a", SituacaoLinhaProntuario.Cancelada)));
    }
}
