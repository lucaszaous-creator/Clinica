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
/// Desde out/2026 são DOIS desenhos da mesma pergunta, e os dois leem as MESMAS opções de
/// <see cref="DestinosDoEnvio"/>: as portas Blazor (a central de Documentos e o "já saiu hoje"
/// do atendimento) abrem o <c>EnvioAoPacienteForm</c>; as portas que ainda são WPF (a aba
/// Documentos da ficha do balcão e o fim da emissão na janela de documento) passam pelo ponto
/// único <c>AcoesDoDocumento.EnviarAsync</c>, que abre a <c>EnvioAoPacienteWindow</c>. O que se
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
    // A aba Documentos da ficha do balcão (WPF).
    [InlineData("src/Clinica.Modulo.Recepcao/ViewModels/FichaPacienteViewModel.cs", "AcoesDoDocumento.EnviarAsync(")]
    // O fim da emissão: "Enviar agora ao paciente?" (WPF).
    [InlineData("src/Clinica.Desktop.Shell/Componentes/DocumentoEdicaoViewModel.cs", "AcoesDoDocumento.EnviarAsync(doc, _escopos, aposEmitir: true)")]
    public void Cada_porta_passa_pelo_ponto_unico_do_envio(string arquivo, string chamada)
        => Ler(arquivo).Should().Contain(chamada, $"{arquivo} precisa oferecer o envio pela janela única (e-mail e WhatsApp)");

    // A folha de infusão é folha INTERNA de execução da enfermagem: não vai ao paciente
    // (decisão da direção, out/2026). Nenhuma das telas dela oferece o envio.
    [Theory]
    [InlineData("src/Clinica.Modulo.Clinico/Modulo/JanelasDaPrescricaoDeInfusaoWpf.cs")]
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
            .And.Contain("case CanalDeEnvio.Email:").And.Contain("case CanalDeEnvio.WhatsAppPeloCrm:")
            .And.Contain("EnvioAoPacienteService.PodeEnviar(").And.Contain("CentralDocumentosService.AcessoParaEmitir(");
    }

    [Fact]
    public void O_ponto_unico_abre_a_janela_com_os_dois_canais_a_partir_das_opcoes_da_Application()
    {
        var acoes = Ler("src/Clinica.Desktop.Shell/Componentes/AcoesDoDocumento.cs");
        acoes.Should().Contain("EnvioAoPacienteWindow.Perguntar(destinos");
        acoes.Should().Contain("case CanalDeEnvio.Email:").And.Contain("case CanalDeEnvio.WhatsAppPeloCrm:");

        var janela = Ler("src/Clinica.Desktop.Shell/Componentes/EnvioAoPacienteWindow.xaml.cs");
        janela.Should().Contain("destinos.Opcoes").And.Contain("destinos.Padrao")
            .And.Contain("CanalDeEnvio.Email").And.Contain("CanalDeEnvio.WhatsAppPeloCrm");
        // E-mail é a PRIMEIRA opção desenhada na janela.
        var xaml = Ler("src/Clinica.Desktop.Shell/Componentes/EnvioAoPacienteWindow.xaml");
        xaml.IndexOf("x:Name=\"OpEmail\"", StringComparison.Ordinal).Should()
            .BeLessThan(xaml.IndexOf("x:Name=\"OpCrm\"", StringComparison.Ordinal));
    }

    [Fact]
    public void O_menu_da_central_tem_o_enviar_quando_o_documento_pode_sair()
    {
        new AtosDoDocumento(false, false, true, false, false, false).NoMenu().Should().Contain(AtoDoDocumento.Enviar);
        AtosDoDocumento.Rotulo(AtoDoDocumento.Enviar).Should().Be("Enviar ao paciente…");
    }

    [Fact]
    public void As_opcoes_trazem_os_dois_canais_com_email_primeiro_mesmo_sem_nada_na_ficha()
    {
        var d = new DestinosDoEnvio("Maria", "Receita 2026/0001", false, "(11) 99999-0001", null, null, null, null);
        d.Opcoes.Select(o => o.Canal).Should().StartWith(new[] { CanalDeEnvio.Email, CanalDeEnvio.WhatsAppPeloCrm });
        d.Padrao.Should().Be(CanalDeEnvio.Email);
        d.Opcoes.Single(o => o.Canal == CanalDeEnvio.WhatsAppPeloCrm).Nota.Should().Contain(DestinosDoEnvio.CustoDoWhatsApp);
    }
}
