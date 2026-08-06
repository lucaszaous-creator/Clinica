using System.Collections.ObjectModel;
using Clinica.Application.Assinatura;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>Um certificado na lista, já com o que decide a escolha.</summary>
public sealed class LinhaCertificado
{
    public required CertificadoAssinatura Certificado { get; init; }
    public required string Titular { get; init; }
    public required string Documento { get; init; }
    public required string Validade { get; init; }
    public required string Emissor { get; init; }
    public required bool Vigente { get; init; }
    public required bool EhECpf { get; init; }

    /// <summary>Vencido não se escolhe: assinar com ele produz documento inválido.</summary>
    public bool PodeEscolher => Vigente && EhECpf;

    /// <summary>Por que não dá — ao lado da linha, em vez de só depois do clique.</summary>
    public string? Impedimento => (Vigente, EhECpf) switch
    {
        (false, _) => "Fora da validade",
        (_, false) => "Não é e-CPF (não traz o CPF do titular)",
        _ => null
    };

    public static LinhaCertificado De(CertificadoAssinatura c) => new()
    {
        Certificado = c,
        Titular = c.Titular,
        Documento = c.Cpf is null ? "sem CPF no certificado" : Domain.Cpf.Formatar(c.Cpf),
        Validade = $"{c.ValidoDe:dd/MM/yyyy} a {c.ValidoAte:dd/MM/yyyy}",
        Emissor = c.Emissor,
        Vigente = c.Vigente,
        EhECpf = c.EhECpf
    };
}

/// <summary>
/// A escolha do certificado ICP-Brasil na hora de assinar (parcela 42; subiu para o shell
/// na 43).
///
/// Mora no SHELL pelo mesmo argumento que já trouxe para cá o mapa corporal, a emissão de
/// documento e a conferência de alergia: quem assina é quem atende, mas quem emite também
/// é o balcão — e nenhum módulo conhece o outro. Deixá-la no Consultório obrigaria a
/// Recepção a ter uma cópia, e duas cópias divergem na primeira correção.
///
/// Por que uma tela, e não "usa o primeiro que achar"
/// --------------------------------------------------
/// Numa máquina de consultório é comum haver mais de um certificado instalado — o e-CPF do
/// profissional, o e-CNPJ da clínica, o do contador que já usou aquele computador —, e
/// escolher sozinho acertaria na maioria das vezes e erraria em silêncio nas outras. A
/// escolha é do signatário, e o resto do sistema já confere se o escolhido é mesmo dele
/// (<c>AssinaturaDePrescricaoService</c>).
///
/// A lista mostra o IMPEDIMENTO ao lado de cada linha em vez de apenas apagar o item:
/// descobrir o requisito errando é o que faz a pessoa desistir da tela — mesma razão do
/// <c>FolhaCatalogo.Exigencia</c> da central de documentos.
/// </summary>
public sealed partial class EscolherCertificadoViewModel : ObservableObject
{
    public ObservableCollection<LinhaCertificado> Certificados { get; } = [];

    [ObservableProperty] private LinhaCertificado? _selecionado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Nenhum certificado utilizável — a janela diz o que fazer.</summary>
    [ObservableProperty] private bool _vazio;

    /// <summary>O que a janela devolve. Null enquanto ninguém confirmou.</summary>
    public CertificadoAssinatura? Escolhido { get; private set; }

    /// <summary>A janela pergunta isto para fechar com <c>DialogResult = true</c>.</summary>
    public bool Confirmou { get; private set; }

    /// <summary>Frase do cabeçalho, dita pelo chamador ("Assinar a prescrição PRE 2026/0001").</summary>
    public string Assunto { get; }

    public EscolherCertificadoViewModel(string assunto)
    {
        Assunto = assunto;
        Carregar();
    }

    private void Carregar()
    {
        Certificados.Clear();

        foreach (var certificado in CertificadoIcpBrasil.DoRepositorioDoUsuario())
            Certificados.Add(LinhaCertificado.De(certificado));

        Vazio = Certificados.Count == 0;

        if (Vazio)
            Mensagem = "Nenhum certificado encontrado nesta máquina. Se for um A3, conecte o "
                     + "token ou o cartão; se for um A1, importe o arquivo .pfx no Windows "
                     + "(Gerenciador de Certificados → Pessoal).";

        Selecionado = Certificados.FirstOrDefault(c => c.PodeEscolher);
    }

    /// <summary>Metade visível da regra; a guarda em <see cref="ConfirmarAsync"/> é a que impede.</summary>
    public bool PodeConfirmar => Selecionado?.PodeEscolher == true;

    partial void OnSelecionadoChanged(LinhaCertificado? value)
        => OnPropertyChanged(nameof(PodeConfirmar));

    [RelayCommand]
    private void Atualizar() => Carregar();

    /// <summary>
    /// Confirma a escolha.
    ///
    /// A guarda DIZ por que não dá, em vez de voltar calada — botão que não faz nada é o
    /// defeito que a parcela 41 corrigiu, e a regra vale para toda pré-condição, não só
    /// para permissão.
    /// </summary>
    [RelayCommand]
    private void Confirmar()
    {
        if (Selecionado is null)
        {
            Mensagem = "Escolha um certificado na lista.";
            MensagemEhErro = true;
            return;
        }

        if (!Selecionado.PodeEscolher)
        {
            Mensagem = $"Este certificado não pode assinar: {Selecionado.Impedimento!.ToLowerInvariant()}.";
            MensagemEhErro = true;
            return;
        }

        Escolhido = Selecionado.Certificado;
        Confirmou = true;
        Fechar?.Invoke();
    }

    /// <summary>A janela liga isto ao próprio fechamento — o VM não conhece WPF.</summary>
    public Action? Fechar { get; set; }
}
