// Dados FICTÍCIOS da prévia — nenhuma operadora ou clínica real.
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// As CONFIGURAÇÕES do faturamento (out/2026): um convênio personalizado com formato de guia e
/// registro ANS próprios, uma modalidade e uma especialidade criadas pela clínica, e o prestador
/// com TUSS e Pix — para a tela de Regras e catálogos TISS não abrir só com os embutidos.
/// </summary>
public sealed class SementeFaturamentoConfiguracoes : ISemente
{
    public int Ordem => 47;

    public void Semear(ClinicaDbContext db)
    {
        var repo = new ClinicaRepositorio(db);
        var catalogo = new ConvenioCatalogoService(repo);
        catalogo.SalvarAsync(
        [
            new ConvenioCadastro
            {
                Codigo = "CVprevia01", Nome = "Saúde Exemplo", Familia = Convenio.Personalizado, Ativo = true,
                FormatoNumeroGuia = FormatoNumeroGuia.Alfanumerico, RegistroAnsOperadora = "000000",
                FazEletro = true, TemSegundoCodigo = true, FormaSegundoCodigo = FormaObtencao.App,
                DiasSegundoCodigo = 2, ValidadeConsultaDias = 30
            }
        ]).GetAwaiter().GetResult();

        new ModalidadeCatalogoService(repo).SalvarAsync(
        [
            new ModalidadeCadastro { Codigo = "MDprevia01", Nome = "Auriculoterapia", Base = ModalidadeAtendimento.AcupunturaSimples, Ativo = true }
        ]).GetAwaiter().GetResult();

        new EspecialidadeCatalogoService(repo).SalvarAsync(
        [
            new EspecialidadeCadastro { Codigo = "ESprevia01", Nome = "Medicina do sono", Ativo = true }
        ]).GetAwaiter().GetResult();

        var parametros = new ParametrosService(repo);
        parametros.SalvarPrestadorAsync(new DadosPrestador
        {
            RazaoSocial = "Clínica Exemplo de Demonstração Ltda", NomeFantasia = "Clínica Exemplo",
            Cnpj = "00.000.000/0001-00", Cidade = "Macaé", ChavePix = "pix@exemplo.invalid",
            CodigosTuss = new() { [TipoCodigo.Acupuntura] = "31601014", [TipoCodigo.Consulta] = "10101012" }
        }).GetAwaiter().GetResult();
    }
}
