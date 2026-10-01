using System.Collections.ObjectModel;
using Clinica.Application.Assinatura;
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

    /// <summary>Certificado importado de arquivo para este ato.</summary>
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

/// <summary>Importa o A1 individual somente para a operação atual.</summary>
public sealed partial class EscolherCertificadoViewModel : ObservableObject
{
    public ObservableCollection<LinhaCertificado> Certificados { get; } = [];
    [ObservableProperty] private LinhaCertificado? _selecionado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _vazio = true;
    [ObservableProperty] private bool _autorizando;
    private readonly IServiceScopeFactory? _escopos;
    public string Assunto { get; }
    public CertificadoAssinatura? Escolhido { get; private set; }
    public bool Confirmou { get; private set; }
    public Action? Fechar { get; set; }
    public bool PodeConfirmar => !Autorizando && Selecionado?.PodeEscolher == true;
    public bool PodeInteragir => !Autorizando;

    public EscolherCertificadoViewModel(string assunto, IServiceScopeFactory? escopos = null)
    { Assunto = assunto; _escopos = escopos; }

    partial void OnAutorizandoChanged(bool value)
    { OnPropertyChanged(nameof(PodeInteragir)); OnPropertyChanged(nameof(PodeConfirmar)); }
    partial void OnSelecionadoChanged(LinhaCertificado? value) => OnPropertyChanged(nameof(PodeConfirmar));

    [RelayCommand]
    private void Confirmar()
    {
        if (Autorizando) return;
        if (Selecionado?.PodeEscolher != true)
        { Mensagem = "Carregue seu certificado A1 e confira o titular antes de assinar."; MensagemEhErro = true; return; }
        Escolhido = Selecionado.Certificado; Confirmou = true; Fechar?.Invoke();
    }

    public async Task CarregarA1Async(byte[] arquivo, string senha)
    {
        if (Autorizando) return;
        Selecionado = null;
        Autorizando = true;
        Mensagem = "Conferindo seu certificado A1…";
        MensagemEhErro = false;
        CertificadoAssinatura? certificado = null;
        try
        {
            if (_escopos is null || !Domain.Entities.SessaoUsuario.Atual.Autenticado)
                throw new InvalidOperationException("Entre com seu usuário profissional para carregar o A1.");
            var usuarioId = Domain.Entities.SessaoUsuario.Atual.UsuarioId;
            using var escopo = _escopos.CreateScope();
            var usuario = await escopo.ServiceProvider.GetRequiredService<Application.Abstracoes.IClinicaRepositorio>()
                .ObterUsuarioAsync(usuarioId);
            if (usuario?.Ativo != true || usuario.Profissional?.Ativo != true)
                throw new InvalidOperationException("Seu usuário precisa estar vinculado a um profissional ativo.");
            certificado = await Task.Run(() =>
            {
                var c = CertificadoA1.Abrir(arquivo, senha, usuario.Profissional.Cpf);
                try { new ConfiancaCertificadoA1().Exigir(c.Certificado); return c; }
                catch { c.Dispose(); throw; }
            });
            if (Domain.Entities.SessaoUsuario.Atual.UsuarioId != usuarioId)
                throw new InvalidOperationException("O acesso mudou. Abra novamente a assinatura.");
            var linha = LinhaCertificado.De(certificado);
            LiberarNaoEscolhidos(); Certificados.Clear();
            Certificados.Add(linha); Selecionado = linha; Vazio = false;
            certificado = null; // a janela transfere somente o escolhido ao chamador
            Mensagem = "A1 carregado para esta operação. Confira o titular e clique em Assinar. O arquivo não foi instalado e a senha não foi salva.";
        }
        catch (Exception e)
        {
            Mensagem = e is InvalidOperationException ? e.Message : "Não foi possível carregar o A1. Confira o arquivo e tente novamente.";
            MensagemEhErro = true;
        }
        finally { certificado?.Dispose(); Autorizando = false; }
    }

    public void LiberarNaoEscolhidos()
    {
        foreach (var linha in Certificados)
            if (!ReferenceEquals(linha.Certificado, Escolhido)) linha.Certificado.Dispose();
    }
}
