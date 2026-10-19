using System.Text;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using Xunit;

namespace Clinica.Tests;

public class MedicamentosNaTelaTests
{
    private static MedicamentoCadastro M(string nome, string codigo = "anvisa:1", string? principio = null, string? fabricante = null)
        => new() { Codigo = codigo, Nome = nome, PrincipioAtivo = principio, Fabricante = fabricante };

    [Fact]
    public void Busca_ignora_acento_e_caixa_e_olha_principio_e_fabricante()
    {
        var todos = new[] { M("Dipirona", principio: "metamizol"), M("Paracetamol", fabricante: "Medley"), M("Ácido acetilsalicílico") };

        Assert.Single(MedicamentosNaTela.Filtrar(todos, "METAMIZOL"));
        Assert.Single(MedicamentosNaTela.Filtrar(todos, "medley"));
        Assert.Single(MedicamentosNaTela.Filtrar(todos, "acido"));
        Assert.Equal(3, MedicamentosNaTela.Filtrar(todos, "  ").Count);
    }

    [Fact]
    public void Cadastro_da_clinica_vem_antes_da_base_oficial()
    {
        var lista = MedicamentosNaTela.Filtrar([M("Amoxicilina"), M("Zinco", codigo: "clinica:abc")], null);
        Assert.Equal("Zinco", lista[0].Nome);
    }

    [Fact]
    public void Resumo_diz_quando_a_lista_esta_cortada_e_quando_esta_filtrada()
    {
        Assert.Equal("12 cadastro(s).", MedicamentosNaTela.Resumo(12, 12, false));
        Assert.Equal("3 de 12 cadastro(s) batem com a busca.", MedicamentosNaTela.Resumo(3, 12, true));
        Assert.Contains("exibindo os primeiros 250", MedicamentosNaTela.Resumo(400, 400, false));
    }

    [Fact]
    public void Modelo_e_lido_de_volta_pela_importacao()
    {
        var modelo = MedicamentosNaTela.Modelo();
        var planilha = modelo.Concat(Encoding.UTF8.GetBytes("Dipirona;metamizol;\"comprimido 500 mg; caixa\";EMS\r\n\r\n")).ToArray();

        var lista = MedicamentosNaTela.LerPlanilha(planilha);

        var m = Assert.Single(lista);
        Assert.Equal("Dipirona", m.Nome);
        Assert.Equal("comprimido 500 mg; caixa", m.Apresentacao);
        Assert.Equal("EMS", m.Fabricante);
    }

    [Fact]
    public void Planilha_fora_do_modelo_e_recusada_dizendo_o_que_corrigir()
    {
        var semCabecalho = Encoding.UTF8.GetBytes("Nome;Principio\r\nDipirona;x\r\n");
        Assert.Contains("Modelo de planilha", Assert.Throws<InvalidOperationException>(() => MedicamentosNaTela.LerPlanilha(semCabecalho)).Message);

        var colunaFaltando = Encoding.UTF8.GetBytes("Nome;PrincipioAtivo;Apresentacao;Fabricante\r\nDipirona;x;y\r\n");
        Assert.Contains("Linha 2", Assert.Throws<InvalidOperationException>(() => MedicamentosNaTela.LerPlanilha(colunaFaltando)).Message);

        var vazia = MedicamentosNaTela.Modelo();
        Assert.Throws<InvalidOperationException>(() => MedicamentosNaTela.LerPlanilha(vazia));
    }
}
