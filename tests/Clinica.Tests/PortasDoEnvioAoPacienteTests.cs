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
/// As portas são telas WPF e o hospedeiro WPF da tela Blazor; o <c>dotnet test</c> não abre
/// WPF. O que se prova aqui é o CONTRATO: cada porta chama o ponto único
/// (<c>AcoesDoDocumento.EnviarAsync</c> ou <c>EnviarPrescricaoAsync</c>), que abre a
/// <c>EnvioAoPacienteWindow</c>, que desenha as opções de <see cref="DestinosDoEnvio"/> — e
/// estas (e-mail primeiro e marcado, WhatsApp com o custo) são provadas em
/// <c>EnvioAoPacienteTests</c>. Porta nova que não passar pelo ponto único fica de fora da
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
    // A central de Documentos (Recepção, Gerente e Consultório), pelo hospedeiro WPF.
    [InlineData("src/Clinica.Desktop.Blazor/DocumentosNoAplicativoWpf.cs", "AtoDoDocumento.Enviar => await AcoesDoDocumento.EnviarAsync(")]
    // A aba Documentos da ficha.
    [InlineData("src/Clinica.Modulo.Recepcao/ViewModels/FichaPacienteViewModel.cs", "AcoesDoDocumento.EnviarAsync(")]
    // As Prescrições do Consultório abrem a MESMA central de Documentos (Blazor) — coberta acima.
    // O "já saiu hoje" do atendimento.
    [InlineData("src/Clinica.Ui.Blazor/Componentes/SecoesDoPaciente/SecaoAtendimento.razor", "Janelas.EnviarAoPacienteAsync(")]
    [InlineData("src/Clinica.Modulo.Clinico/Modulo/JanelasDoAtendimentoClinicoWpf.cs", "AcoesDoDocumento.EnviarAsync(")]
    // O fim da emissão: "Enviar agora ao paciente?" — a folha Blazor pergunta pela ponte, e a
    // ponte WPF abre a MESMA janela, com a pergunta de quem acabou de emitir.
    [InlineData("src/Clinica.Ui.Blazor/Formularios/Documentos/DocumentoForm.razor", "Envio.PerguntarAposEmitirAsync(")]
    [InlineData("src/Clinica.Desktop.Blazor/EnvioDoDocumentoAoPacienteWpf.cs", "AcoesDoDocumento.EnviarAsync(doc, escopos, aposEmitir: true)")]
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
