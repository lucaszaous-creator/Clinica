using Clinica.Application.Servicos;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>
/// O registro de UM resultado de exame (ago/2026) — em diálogo, pelo molde da colheita de
/// medida: registrar é ato pontual, e ato pontual não merece painel permanente.
///
/// O VALOR é texto livre por desenho (a entidade explica: "não reagente", "&lt; 0,01" são
/// resultados reais), então aqui não há conversão numérica nenhuma — quem valida
/// completude e plausibilidade de DATA é o serviço, e a recusa volta inline.
/// </summary>
public sealed partial class ResultadoExameEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int _pacienteId;

    /// <summary>Gravou — é por isso que a janela fecha.</summary>
    public bool Registrado { get; private set; }

    [ObservableProperty] private string _paciente = string.Empty;

    [ObservableProperty] private DateTime _dataNova = DateTime.Today;
    [ObservableProperty] private string? _nomeNovo;
    [ObservableProperty] private string? _valorNovo;
    [ObservableProperty] private string? _unidadeNova;
    [ObservableProperty] private string? _referenciaNova;
    [ObservableProperty] private string? _laboratorioNovo;
    [ObservableProperty] private string? _observacoesNovas;

    [ObservableProperty] private bool _salvando;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    public ResultadoExameEdicaoViewModel(
        IServiceScopeFactory escopos, int pacienteId, string paciente)
    {
        _escopos = escopos;
        _pacienteId = pacienteId;
        Paciente = paciente;
    }

    [RelayCommand]
    private async Task RegistrarAsync()
    {
        try
        {
            Salvando = true;
            Mensagem = null;
            MensagemEhErro = false;

            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<ResultadoExameService>();

            await servico.RegistrarAsync(new ResultadoExame
            {
                PacienteId = _pacienteId,
                Data = DateOnly.FromDateTime(DataNova),
                Nome = NomeNovo ?? string.Empty,
                Valor = ValorNovo ?? string.Empty,
                Unidade = UnidadeNova,
                Referencia = ReferenciaNova,
                Laboratorio = LaboratorioNovo,
                Observacoes = ObservacoesNovas
            }, SessaoUsuario.Atual.Operador);

            Registrado = true;
        }
        catch (Exception ex)
        {
            Registrado = false;
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — resultado de exame não pôde ser registrado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            // `Salvando` volta a false por ÚLTIMO: é a mudança que a janela observa para
            // fechar, e ela precisa encontrar `Registrado` já definido.
            Salvando = false;
        }
    }
}
