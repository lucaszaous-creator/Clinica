using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Ui.Blazor.Hospedagem;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// "Enviar ao paciente" em TODAS as portas onde um documento é emitido ou recuperado (out/2026,
/// pedido da direção), com os dois canais e o e-mail primeiro — e por UMA definição só.
///
/// Desde out/2026 é UM desenho só, e ele lê as opções de <see cref="DestinosDoEnvio"/>: toda
/// porta (a central de Documentos, o "já saiu hoje" do atendimento e o fim da emissão no
/// <c>DocumentoForm</c>) abre o <c>EnvioAoPacienteForm</c> Blazor. O que se
/// prova aqui é o CONTRATO de cada porta; as opções (e-mail primeiro e marcado, WhatsApp com o
/// custo) são provadas em <c>EnvioAoPacienteTests</c>. Porta nova que não passar pelo ponto único fica de fora da
/// lista abaixo e não ganha os dois canais: é por isso que a lista é escrita aqui, à vista.
/// </summary>
public sealed class PortasDoEnvioAoPacienteTests
{
    static string Raiz()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Clinica.sln"))) d = d.Parent;
        return d!.FullName;
    }

    static string Ler(string caminho) => File.ReadAllText(Path.Combine(Raiz(), caminho));

    [Theory]
    // A central de Documentos (Recepção, Gerente e Consultório; as Prescrições abrem a mesma).
    [InlineData("src/Clinica.Ui.Blazor/Telas/Recepcao/DocumentosDaRecepcao.razor", "<EnvioAoPacienteForm DocumentoId=")]
    // O "já saiu hoje" do atendimento.
    [InlineData("src/Clinica.Ui.Blazor/Componentes/SecoesDoPaciente/SecaoAtendimento.razor", "<EnvioAoPacienteForm DocumentoId=")]
    // O fim da emissão: "Enviar agora ao paciente?" — a folha abre o MESMO formulário.
    [InlineData("src/Clinica.Ui.Blazor/Formularios/Documentos/DocumentoForm.razor", "<EnvioAoPacienteForm DocumentoId=")]
    public void Cada_porta_passa_pelo_ponto_unico_do_envio(string arquivo, string chamada)
        => Ler(arquivo).Should().Contain(chamada, $"{arquivo} precisa oferecer o envio pela janela única (e-mail e WhatsApp)");

    // A folha de infusão é folha INTERNA de execução da enfermagem: não vai ao paciente
    // (decisão da direção, out/2026). Nenhuma das telas dela oferece o envio.
    [Theory]
    // O editor e a folha de execução viraram formulários Blazor (out/2026) — as janelas WPF saíram.
    [InlineData("src/Clinica.Ui.Blazor/Formularios/Infusao/PrescricaoDeInfusaoForm.razor")]
    [InlineData("src/Clinica.Ui.Blazor/Formularios/Infusao/FolhaDeExecucaoForm.razor")]
    [InlineData("src/Clinica.Ui.Blazor/Telas/Consultorio/PrescricaoDeInfusao.razor")]
    public void A_folha_de_infusao_nao_e_enviada_ao_paciente(string arquivo)
        => Ler(arquivo).Should().NotContain("Enviar", $"{arquivo} é a folha interna da infusão, que não vai ao paciente");

    [Fact]
    public void A_central_intercepta_o_Enviar_e_a_ponte_nunca_cai_no_cancelar()
    {
        // A central abre o formulário em vez de pedir o ato à ponte...
        Ler("src/Clinica.Ui.Blazor/Telas/Recepcao/DocumentosDaRecepcao.razor")
            .Should().Contain("ato == AtoDoDocumento.Enviar");
        // ...e a ponte tem braço PRÓPRIO para o Enviar: cair no `_` da troca cancelaria o documento.
        Ler("src/Clinica.Desktop.Blazor/DocumentosNoAplicativoWpf.cs")
            .Should().Contain("AtoDoDocumento.Enviar =>");
    }

    [Fact]
    public void O_formulario_Blazor_desenha_os_dois_canais_a_partir_das_opcoes_da_Application()
    {
        var form = Ler("src/Clinica.Ui.Blazor/Formularios/EnvioAoPacienteForm.razor");
        form.Should().Contain("d.Opcoes").And.Contain("destinos.Padrao")
            .And.Contain("case CanalDeEnvio.WhatsAppPeloCrm:").And.NotContain("CanalDeEnvio.Email")
            .And.Contain("EnvioAoPacienteService.PodeEnviar(").And.Contain("CentralDocumentosService.AcessoParaEmitir(");
    }

    [Fact]
    public void O_menu_da_central_tem_o_enviar_quando_o_documento_pode_sair()
    {
        new AtosDoDocumento(false, false, true, false, false, false).NoMenu().Should().Contain(AtoDoDocumento.Enviar);
        AtosDoDocumento.Rotulo(AtoDoDocumento.Enviar).Should().Be("Enviar ao paciente…");
    }

    [Fact]
    public void As_opcoes_trazem_os_dois_canais_do_WhatsApp_com_o_oficial_primeiro()
    {
        var d = new DestinosDoEnvio("Maria", "Receita 2026/0001", false, "(11) 99999-0001", null, null);
        d.Opcoes.Select(o => o.Canal).Should().Equal(CanalDeEnvio.WhatsAppPeloCrm, CanalDeEnvio.WhatsAppDesteComputador);
        d.Padrao.Should().Be(CanalDeEnvio.WhatsAppPeloCrm);
        d.Opcoes.Single(o => o.Canal == CanalDeEnvio.WhatsAppPeloCrm).Nota.Should().Contain(DestinosDoEnvio.CustoDoWhatsApp);
    }
}
