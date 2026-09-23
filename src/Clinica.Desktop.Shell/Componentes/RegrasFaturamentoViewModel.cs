using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
namespace Clinica.Desktop.Shell.Componentes;
public sealed partial class RegrasFaturamentoViewModel(IServiceScopeFactory escopos, ISnackbarService snackbar) : ObservableObject
{
    public bool PodeEditar => SessaoUsuario.Atual.PodeAlgum(Permissao.GerenciarUsuarios | Permissao.ConfigurarFaturamento);
    [ObservableProperty] private string _janelaAlertaConsulta = "";
    [ObservableProperty] private string _prazoRecursoGlosa = "";
    [ObservableProperty] private string _intervaloRodadaPendencias = "";
    [ObservableProperty] private bool _rodadaAplicaConsultas;
    [ObservableProperty] private bool _rodadaAplicaCarteirinhas;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _carregando;
    private bool _carregado;
    public async Task CarregarAsync()
    {
        if (_carregado || Carregando) return;
        try {
            SessaoUsuario.Atual.ExigirAlgum(Permissao.GerenciarUsuarios | Permissao.ConfigurarFaturamento,"consultar regras do faturamento");
            Carregando=true;
            using var scope = escopos.CreateScope(); var p=scope.ServiceProvider.GetRequiredService<ParametrosService>();
            JanelaAlertaConsulta=(await p.ObterJanelaAlertaConsultaAsync()).ToString();
            PrazoRecursoGlosa=(await p.ObterPrazoRecursoGlosaAsync()).ToString();
            IntervaloRodadaPendencias=(await p.ObterIntervaloRodadaPendenciasAsync()).ToString();
            RodadaAplicaConsultas=await p.ObterRodadaAplicaConsultasAsync();
            RodadaAplicaCarteirinhas=await p.ObterRodadaAplicaCarteirinhasAsync();
            _carregado=true;
        } catch(Exception ex) { Mensagem="Não foi possível carregar as regras. "+ex.Message; }
        finally { Carregando=false; }
    }
    [RelayCommand] private async Task SalvarFaturamentoAsync()
    {
        SessaoUsuario.Atual.ExigirAlgum(Permissao.GerenciarUsuarios | Permissao.ConfigurarFaturamento,"salvar regras do faturamento");
        if (!_carregado || Carregando) { Mensagem="Aguarde o carregamento das regras antes de salvar."; return; }
        try {
            if (!int.TryParse(JanelaAlertaConsulta,out var janela) || janela is <0 or >365 ||
                !int.TryParse(PrazoRecursoGlosa,out var glosa) || glosa is <1 or >365 ||
                !int.TryParse(IntervaloRodadaPendencias,out var rodada) || rodada is <1 or >365)
                throw new InvalidOperationException("Informe a antecedência entre 0 e 365 dias e os demais prazos entre 1 e 365 dias.");
            Carregando=true;
            using var scope=escopos.CreateScope();var p=scope.ServiceProvider.GetRequiredService<ParametrosService>();
            await p.SalvarJanelaAlertaConsultaAsync(janela);await p.SalvarPrazoRecursoGlosaAsync(glosa);
            await p.SalvarIntervaloRodadaPendenciasAsync(rodada);await p.SalvarRodadaAplicaAsync(RodadaAplicaConsultas,RodadaAplicaCarteirinhas);
            Mensagem="Regras de faturamento salvas.";snackbar.Sucesso(Mensagem);
        } catch(Exception ex) { Mensagem=ex.Message;snackbar.Erro(Mensagem); }
        finally { Carregando=false; }
    }
}
