using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
namespace Clinica.Desktop.Shell.Componentes;
public sealed partial class DadosClinicaViewModel(IServiceScopeFactory escopos, ISnackbarService snackbar) : ObservableObject
{
    public bool PodeEditar => SessaoUsuario.Atual.Pode(Permissao.GerenciarUsuarios) || SessaoUsuario.Atual.Pode(Permissao.ConfigurarFaturamento);
    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private string? _razaoSocial;
    [ObservableProperty] private string? _nomeFantasia;
    [ObservableProperty] private string? _cnpj;
    [ObservableProperty] private string? _cnes;
    [ObservableProperty] private string? _endereco;
    [ObservableProperty] private string? _telefone;
    [ObservableProperty] private string? _email;
    [ObservableProperty] private string? _chavePix;
    [ObservableProperty] private string? _cidade;
    [ObservableProperty] private string? _codigoNaOperadora;
    [ObservableProperty] private string? _registroAnsOperadora;

    private bool _carregado;
    public async Task CarregarAsync()
    {
        if (_carregado || Carregando) return;
        Carregando = true;
        try {
            SessaoUsuario.Atual.ExigirAlgum(Permissao.GerenciarUsuarios | Permissao.ConfigurarFaturamento, "consultar dados da clínica");
            using var scope = escopos.CreateScope();
            var dados = await scope.ServiceProvider.GetRequiredService<ParametrosService>().ObterPrestadorAsync();
            RazaoSocial = dados.RazaoSocial;
            NomeFantasia = dados.NomeFantasia;
            Cnpj = dados.Cnpj;
            Cnes = dados.Cnes;
            Endereco = dados.Endereco;
            Telefone = dados.Telefone;
            Email = dados.Email;
            ChavePix = dados.ChavePix;
            Cidade = dados.Cidade;
            CodigoNaOperadora = dados.CodigoNaOperadora;
            RegistroAnsOperadora = dados.RegistroAnsOperadora;
            _carregado = true;
        }
        catch(Exception ex) { Mensagem = "Não foi possível carregar os dados da clínica. " + ex.Message; }
        finally { Carregando = false; }
    }
    [RelayCommand] private async Task SalvarAsync()
    {
        SessaoUsuario.Atual.ExigirAlgum(Permissao.GerenciarUsuarios | Permissao.ConfigurarFaturamento, "salvar dados da clínica");
        if (!_carregado || Carregando) { Mensagem = "Carregue os dados antes de salvar."; return; }
        Carregando = true;
        try {
            using var scope = escopos.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ParametrosService>();
            var dados = await service.ObterPrestadorAsync();
            dados.RazaoSocial = string.IsNullOrWhiteSpace(RazaoSocial) ? null : RazaoSocial.Trim();
            dados.NomeFantasia = string.IsNullOrWhiteSpace(NomeFantasia) ? null : NomeFantasia.Trim();
            dados.Cnpj = string.IsNullOrWhiteSpace(Cnpj) ? null : Cnpj.Trim();
            dados.Cnes = string.IsNullOrWhiteSpace(Cnes) ? null : Cnes.Trim();
            dados.Endereco = string.IsNullOrWhiteSpace(Endereco) ? null : Endereco.Trim();
            dados.Telefone = string.IsNullOrWhiteSpace(Telefone) ? null : Telefone.Trim();
            dados.Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
            dados.ChavePix = string.IsNullOrWhiteSpace(ChavePix) ? null : ChavePix.Trim();
            dados.Cidade = string.IsNullOrWhiteSpace(Cidade) ? null : Cidade.Trim();
            dados.CodigoNaOperadora = string.IsNullOrWhiteSpace(CodigoNaOperadora) ? null : CodigoNaOperadora.Trim();
            dados.RegistroAnsOperadora = string.IsNullOrWhiteSpace(RegistroAnsOperadora) ? null : RegistroAnsOperadora.Trim();
            await service.SalvarPrestadorAsync(dados);
            Mensagem = "Dados da clínica salvos.";
            snackbar.Sucesso(Mensagem);
        }
        catch(Exception ex) { Mensagem = "Não foi possível salvar. " + ex.Message; snackbar.Erro(Mensagem); }
        finally { Carregando = false; }
    }
}
