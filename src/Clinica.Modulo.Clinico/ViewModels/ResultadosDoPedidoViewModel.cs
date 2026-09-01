using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>Um resultado no modal — os campos do laudo como foram registrados.</summary>
public sealed record ResultadoResumido(
    string Nome, string Valor, string? Referencia, string? Laboratorio,
    string DataTexto, string? Observacoes);

/// <summary>
/// O modal "Resultado — exame" da tela de Exames (o quick-view do mockup): os resultados
/// REGISTRADOS que respondem a este pedido, sem sair da lista. O "Baixar PDF" do mockup
/// não existe aqui de propósito — o resultado estruturado não tem arquivo; o laudo em
/// arquivo é ANEXO e mora na seção Exames e anexos, atrás do botão de abrir o paciente.
/// </summary>
public sealed partial class ResultadosDoPedidoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly PacienteEmFoco _foco;
    private readonly PedidoDeExameLinha _pedido;

    /// <summary>A janela observa para fechar quando a navegação foi disparada.</summary>
    public event Action? NavegouParaOPaciente;

    public string Paciente => _pedido.Paciente;
    public string ExameRotulo => _pedido.ExameRotulo;
    public string PedidoTexto => $"Pedido {_pedido.Numero} · {_pedido.Data:dd/MM/yyyy}";

    public ObservableCollection<ResultadoResumido> Resultados { get; } = [];

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    public ResultadosDoPedidoViewModel(
        IServiceScopeFactory escopos, PacienteEmFoco foco, PedidoDeExameLinha pedido)
    {
        _escopos = escopos;
        _foco = foco;
        _pedido = pedido;

        _ = CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        Carregando = true;
        try
        {
            IReadOnlyList<ResultadoExame> doPaciente;
            using (var scope = _escopos.CreateScope())
            {
                // Modal de dado de saúde deixa rastro (parcela 60).
                await scope.ServiceProvider.GetRequiredService<AcessoProntuarioService>()
                    .RegistrarAsync(_pedido.PacienteId, SessaoUsuario.Atual.Operador,
                        OrigemAcessoProntuario.ProntuarioClinico);

                var servico = scope.ServiceProvider.GetRequiredService<ResultadoExameService>();
                doPaciente = await servico.DoPacienteAsync(_pedido.PacienteId);
            }

            // O recorte é DESTE pedido — os resultados do paciente são poucos, e o
            // vínculo já veio na linha; uma consulta própria seria peça a mais.
            Resultados.Clear();
            foreach (var r in doPaciente.Where(r => r.PedidoDocumentoId == _pedido.DocumentoId))
                Resultados.Add(new ResultadoResumido(
                    r.Nome, r.ValorComUnidade, r.Referencia, r.Laboratorio,
                    $"{r.Data:dd/MM/yyyy}", r.Observacoes));

            NaoVerificado = false;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — os resultados do pedido não puderam ser carregados", ex);
            NaoVerificado = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    /// <summary>Abre o paciente na seção Exames e anexos — laudos em arquivo, registro de
    /// novos resultados e o histórico inteiro moram lá.</summary>
    [RelayCommand]
    private void AbrirNoPaciente()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.VerProntuario, "abrir os exames do paciente");
            _foco.Definir(_pedido.PacienteId, _pedido.Paciente);
            if (NavegacaoSuite.Ir(ModuloClinico.ChaveExamesDoPaciente))
            {
                NavegouParaOPaciente?.Invoke();
                return;
            }
            Mensagem = "Não deu para abrir a seção de exames do paciente.";
            MensagemEhErro = true;
        }
        catch (Exception ex)
        {
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }
}
