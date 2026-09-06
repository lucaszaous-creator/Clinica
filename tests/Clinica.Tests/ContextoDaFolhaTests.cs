using Clinica.Application.Modelos;

namespace Clinica.Tests;

/// <summary>
/// A linha de contexto da folha (set/2026). O que ela afirma sobre a enfermagem muda
/// conduta, então os três estados estão fixados aqui — inclusive o que NÃO aparece.
/// </summary>
public class ContextoDaFolhaTests
{
    [Fact]
    public void Primeira_sessao_sem_enfermagem_e_linha_vazia()
        => Assert.Equal(string.Empty, ContextoDaFolha.Montar(null, null, null, leituraFalhou: false));

    [Fact]
    public void Ultima_sessao_e_enfermagem_na_mesma_linha_com_a_procedencia()
    {
        var linha = ContextoDaFolha.Montar(
            "Última sessão em 27/08 · EVA 8 → 3",
            "PA 120x80 · FC 78", "às 09:12, por Joana (COREN-SP 999999)", leituraFalhou: false);

        Assert.Equal(
            "Última sessão em 27/08 · EVA 8 → 3" + ContextoDaFolha.Separador
            + "Enfermagem hoje: PA 120x80 · FC 78 (às 09:12, por Joana (COREN-SP 999999))",
            linha);
    }

    [Fact]
    public void Sem_afericao_no_dia_a_metade_da_enfermagem_SOME()
    {
        var linha = ContextoDaFolha.Montar("Última sessão em 27/08", null, null, leituraFalhou: false);
        Assert.Equal("Última sessão em 27/08", linha);
        Assert.DoesNotContain("Enfermagem", linha);
    }

    [Fact]
    public void Leitura_que_falhou_e_escrita_nunca_confundida_com_ausencia()
    {
        var linha = ContextoDaFolha.Montar(null, null, null, leituraFalhou: true);
        Assert.Contains("não foi possível conferir", linha);
    }

    [Fact]
    public void Afericao_sem_procedencia_sai_sem_parenteses_vazio()
    {
        var linha = ContextoDaFolha.Montar(null, "PA 120x80", "", leituraFalhou: false);
        Assert.Equal("Enfermagem hoje: PA 120x80", linha);
    }
}
