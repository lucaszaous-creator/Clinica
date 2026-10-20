namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// As janelas do SHELL que as abas administrativas da ficha (Blazor, out/2026) pedem: editar o
/// cadastro. Ela mora no shell e serve a várias portas — a tela Blazor não a reescreve, e quem
/// grava continua sendo um só (o <c>PacienteService</c>). A cobrança no balcão saiu daqui em
/// out/2026: é o <c>CobrancaDoPacienteForm</c> Blazor.
///
/// A implementação WPF (<c>JanelasDaFichaWpf</c>, em <c>Clinica.Desktop.Blazor</c>) existe em
/// todo executável que hospeda tela Blazor; onde ela não existe (a prévia, os testes) vale
/// <see cref="JanelasDaFichaIndisponiveis"/>, e a tela DIZ que a ação é do aplicativo.
///
/// A primeira barreira de permissão é da tela; a janela confere de novo ao gravar.
/// </summary>
public interface IJanelasDaFicha
{
    bool Disponivel { get; }

    /// <summary>Abre a edição do cadastro. Verdadeiro = gravou (a tela relê).</summary>
    Task<bool> EditarCadastroAsync(int pacienteId);
}

/// <summary>Onde as janelas não existem: nada abre, e a tela diz.</summary>
public sealed class JanelasDaFichaIndisponiveis : IJanelasDaFicha
{
    public static readonly JanelasDaFichaIndisponiveis Instancia = new();

    public const string Frase = "Esta ação abre no aplicativo instalado.";

    public bool Disponivel => false;
    public Task<bool> EditarCadastroAsync(int pacienteId) => Task.FromResult(false);
}
