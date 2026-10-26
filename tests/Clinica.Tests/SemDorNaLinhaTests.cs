using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A etiqueta ✨ do @SemDor na linha só quando acrescenta (25/10/2026, item 9 da revisão) e a falha dele sem a
/// frase do transporte (item 3).
/// </summary>
public sealed class SemDorNaLinhaTests
{
    private static readonly EtiquetaDoSemDor Assunto = new("Remarcar", false, SemDorNaTela.DicaDoAssunto);
    private static readonly EtiquetaDoSemDor Atencao = new("Reação ao tratamento", true, SemDorNaTela.DicaDaAtencao);

    [Fact]
    public void A_etiqueta_de_assunto_so_aparece_quando_o_selo_da_clinica_e_o_neutro()
    {
        // "✨ Remarcar" ao lado de "Não vem — remarcar", "✨ Confirmou" ao lado de "2 fichas": só repetiam.
        Assert.Null(SemDorNaTela.EtiquetaNaLinha(Assunto, new SeloDoWhatsApp("Não vem — remarcar", "perigo")));
        Assert.Null(SemDorNaTela.EtiquetaNaLinha(Assunto, new SeloDoWhatsApp("2 fichas", "info")));
        Assert.Same(Assunto, SemDorNaTela.EtiquetaNaLinha(Assunto, new SeloDoWhatsApp("Nova", "neutro")));
        Assert.Null(SemDorNaTela.EtiquetaNaLinha(null, new SeloDoWhatsApp("Nova", "neutro")));
    }

    [Fact]
    public void A_etiqueta_de_atencao_aparece_sempre()
    {
        Assert.Same(Atencao, SemDorNaTela.EtiquetaNaLinha(Atencao, new SeloDoWhatsApp("Não vem — remarcar", "perigo")));
        Assert.Same(Atencao, SemDorNaTela.EtiquetaNaLinha(Atencao, new SeloDoWhatsApp("Sem ficha", "neutro")));
    }

    [Fact]
    public void Sem_ligacao_a_falha_do_SemDor_e_a_frase_da_clinica()
    {
        Assert.Equal(SemDorNaTela.SemResposta, SemDorNaTela.Falha(new CrmIndisponivel("O CRM não respondeu. Tente de novo em instantes.")));
        // A que o CRM escreveu sobre o @SemDor passa.
        Assert.Equal("O @SemDor não conseguiu resumir agora.", SemDorNaTela.Falha(new CrmIndisponivel("O @SemDor não conseguiu resumir agora.")));
    }
}
