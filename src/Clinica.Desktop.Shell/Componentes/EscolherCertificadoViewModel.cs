using System.Collections.ObjectModel;
using System.Net.Http;
using Clinica.Application.Assinatura;
using Clinica.Application.Assinatura.SafeID;
using Clinica.Desktop.Shell.Configuracao;
using Clinica.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
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

    /// <summary>"SafeID — nuvem" ou "nesta máquina". A linha DIZ de onde o certificado veio.</summary>
    public required string Procedencia { get; init; }

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
        EhECpf = c.EhECpf,
        Procedencia = c.Procedencia
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

    private readonly IServiceScopeFactory? _escopos;
    private readonly string? _cpfDeQuemAssina;

    /// <summary>Está buscando na nuvem — a janela desabilita os botões e diz o que fazer.</summary>
    [ObservableProperty] private bool _autorizando;

    /// <summary>
    /// A clínica cadastrou o SafeID. Metade visível da regra: sem isto o botão nem aparece,
    /// em vez de aparecer e explicar depois do clique.
    /// </summary>
    [ObservableProperty] private bool _nuvemDisponivel;

    /// <param name="escopos">
    /// Null desliga a busca em nuvem. É o caso de quem chama sem DI à mão — e desligar é
    /// melhor que oferecer um botão que não teria como funcionar.
    /// </param>
    public EscolherCertificadoViewModel(
        string assunto, IServiceScopeFactory? escopos = null, string? cpfDeQuemAssina = null)
    {
        Assunto = assunto;
        _escopos = escopos;
        _cpfDeQuemAssina = cpfDeQuemAssina;
        Carregar();
        _ = VerificarNuvemAsync();
    }

    private async Task VerificarNuvemAsync()
    {
        if (_escopos is null) return;

        try
        {
            using var escopo = _escopos.CreateScope();
            NuvemDisponivel = await escopo.ServiceProvider
                .GetRequiredService<ProvedorOpcoesSafeID>().ObterAsync() is not null;
        }
        catch (Exception ex)
        {
            // Sem SafeID a janela segue servindo para os certificados da máquina. Sumir sem
            // rastro faria "o botão não aparece" virar mistério na hora do suporte.
            LogSuite.Registrar("EscolherCertificado.VerificarNuvem", ex);
        }
    }

    /// <summary>
    /// Busca os certificados em nuvem: manda a médica ao QR Code e traz o que ela autorizar.
    ///
    /// Os certificados encontrados entram na MESMA lista dos da máquina, com a procedência
    /// escrita na linha. Substituir a lista esconderia o token de quem ainda usa os dois, e
    /// escolher sozinho acertaria na maioria das vezes e erraria em silêncio nas outras.
    /// </summary>
    [RelayCommand]
    private async Task BuscarNaNuvemAsync()
    {
        if (_escopos is null || !NuvemDisponivel)
        {
            Mensagem = "O SafeID não está configurado. A direção cadastra o client_id e o "
                     + "client_secret em Configurações → Operação.";
            MensagemEhErro = true;
            return;
        }

        if (Autorizando) return;   // reentrância: "já estou fazendo", não "não dá"

        Autorizando = true;
        Mensagem = "Abrimos a página do SafeID no navegador. Leia o QR Code com o aplicativo "
                 + "e confirme no celular — esta janela espera.";
        MensagemEhErro = false;

        try
        {
            OpcoesSafeID? opcoes;
            using (var escopo = _escopos.CreateScope())
                opcoes = await escopo.ServiceProvider
                    .GetRequiredService<ProvedorOpcoesSafeID>().ObterAsync();

            if (opcoes is null)
                throw new InvalidOperationException(
                    "O SafeID não está configurado nesta clínica.");

            // O HttpClient vem da fábrica (singleton) e sobrevive ao escopo de propósito: o
            // assinador guardado na linha vai usá-lo DEPOIS que esta janela fechar.
            IHttpClientFactory fabrica;
            using (var escopo = _escopos.CreateScope())
                fabrica = escopo.ServiceProvider.GetRequiredService<IHttpClientFactory>();

            var cliente = new ClienteSafeID(
                fabrica.CreateClient(DependencyInjection.NomeHttpSafeID), opcoes);

            var autorizacao = new AutorizacaoSafeIDService(
                _ => Task.FromResult<OpcoesSafeID?>(opcoes),
                _ => cliente,
                AbrirNoNavegador);

            var sessao = await autorizacao.AutorizarAsync(cpf: _cpfDeQuemAssina);

            foreach (var daNuvem in sessao.Certificados)
            {
                var assinador = new AssinadorSafeID(
                    cliente, sessao.Token.AccessToken, daNuvem, Assunto);

                Certificados.Insert(0, LinhaCertificado.De(
                    daNuvem.Certificado with { AssinadorRemoto = assinador }));
            }

            Vazio = Certificados.Count == 0;
            Selecionado = Certificados.FirstOrDefault(c => c.PodeEscolher);

            Mensagem = sessao.Certificados.Count == 0
                ? "O SafeID autorizou, mas não devolveu nenhum certificado."
                : null;
            MensagemEhErro = sessao.Certificados.Count == 0;
        }
        catch (Exception ex)
        {
            LogSuite.Registrar("EscolherCertificado.BuscarNaNuvem", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Autorizando = false;
        }
    }

    /// <summary>
    /// Abre a página do PSC no navegador padrão. <c>UseShellExecute</c> é obrigatório: sem
    /// ele o .NET tenta executar a URL como programa e falha.
    /// </summary>
    private static void AbrirNoNavegador(Uri url)
        => System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(url.ToString()) { UseShellExecute = true });

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

    /// <summary>
    /// Falso enquanto a autorização em nuvem está em curso. Não é "não dá" — é "já estou
    /// fazendo": clicar de novo abriria uma segunda página de QR e a médica confirmaria a
    /// que não está sendo esperada.
    /// </summary>
    public bool PodeInteragir => !Autorizando;

    partial void OnAutorizandoChanged(bool value) => OnPropertyChanged(nameof(PodeInteragir));

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
