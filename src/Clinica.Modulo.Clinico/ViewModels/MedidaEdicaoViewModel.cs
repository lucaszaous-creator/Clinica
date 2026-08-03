using Clinica.Application.Servicos;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using Clinica.Domain.Medidas;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>
/// A colheita de UMA medida (parcela 37, rodada de leiaute).
///
/// Saiu de dentro da tela de Medidas, onde era um painel aberto o tempo todo com cinco
/// campos esticados de ponta a ponta. O que se olha naquela tela é a SÉRIE; colher é o ato
/// pontual, uma vez por consulta — e ato pontual não merece um terço da tela em permanência.
///
/// É construído à mão pela tela, como todo formulário da suíte: recebe o paciente e o tipo
/// que já estava escolhido, para quem clicou em "Registrar" não ter de escolher de novo o
/// que estava acompanhando.
/// </summary>
public sealed partial class MedidaEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int _pacienteId;

    /// <summary>Gravou — é por isso que a janela fecha.</summary>
    public bool Registrada { get; private set; }

    /// <summary>O tipo colhido, para a tela voltar acompanhando o que acabou de ser medido.</summary>
    public string? TipoRegistrado { get; private set; }

    public IReadOnlyList<TipoMedida> TiposRegistraveis { get; } = CatalogoMedidas.Registraveis;

    [ObservableProperty] private string _paciente = string.Empty;

    [ObservableProperty] private TipoMedida? _tipoNovo;
    [ObservableProperty] private DateTime _dataNova = DateTime.Today;
    [ObservableProperty] private string? _valorNovo;
    [ObservableProperty] private string? _valorSecundarioNovo;
    [ObservableProperty] private string? _observacoesNovas;

    [ObservableProperty] private bool _salvando;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>O tipo escolhido pede dois números (a pressão).</summary>
    public bool TemSegundoValor => TipoNovo?.TemSegundoValor ?? false;

    public string RotuloSegundoValor => TipoNovo?.RotuloSegundoValor ?? string.Empty;

    /// <summary>Unidade e nota do tipo, ao lado do campo — não depois do erro.</summary>
    public string AjudaDoTipo => TipoNovo is null
        ? string.Empty
        : $"Em {TipoNovo.Unidade}. {TipoNovo.Nota}".Trim();

    public MedidaEdicaoViewModel(
        IServiceScopeFactory escopos, int pacienteId, string paciente, string? tipoInicial)
    {
        _escopos = escopos;
        _pacienteId = pacienteId;
        Paciente = paciente;

        // Abre no tipo que a tela estava acompanhando — e cai no peso quando o acompanhado
        // é derivado (o IMC não se colhe) ou não existe mais no catálogo.
        TipoNovo = TiposRegistraveis.FirstOrDefault(t => t.Codigo == tipoInicial)
                   ?? TiposRegistraveis.FirstOrDefault(t => t.Codigo == CatalogoMedidas.Peso);
    }

    partial void OnTipoNovoChanged(TipoMedida? value)
    {
        // Trocar de tipo esquece o segundo campo: a diastólica digitada para a pressão não
        // é número nenhum quando o tipo passa a ser peso, e o serviço a recusaria.
        ValorSecundarioNovo = null;
        OnPropertyChanged(nameof(TemSegundoValor));
        OnPropertyChanged(nameof(RotuloSegundoValor));
        OnPropertyChanged(nameof(AjudaDoTipo));
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

            if (TipoNovo is not { } tipo)
                throw new InvalidOperationException("Escolha o que está sendo medido.");

            var valor = Numero(ValorNovo)
                ?? throw new InvalidOperationException($"Digite o valor de {tipo.Nome}.");

            decimal? segundo = null;
            if (tipo.TemSegundoValor)
                segundo = Numero(ValorSecundarioNovo)
                    ?? throw new InvalidOperationException(
                        $"{tipo.Nome} precisa dos dois valores: {tipo.RotuloSegundoValor} está em branco.");

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<MedidaClinicaService>();

            await servico.RegistrarAsync(new MedidaClinica
            {
                PacienteId = _pacienteId,
                ProfissionalId = SessaoUsuario.Atual.ProfissionalId,
                Data = DateOnly.FromDateTime(DataNova),
                TipoCodigo = tipo.Codigo,
                Valor = valor,
                ValorSecundario = segundo,
                Observacoes = ObservacoesNovas
            }, SessaoUsuario.Atual.Operador);

            TipoRegistrado = tipo.Codigo;
            Registrada = true;
        }
        catch (Exception ex)
        {
            Registrada = false;
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — medida não pôde ser registrada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            // `Salvando` volta a false por ÚLTIMO: é a mudança que a janela observa para
            // fechar, e ela precisa encontrar `Registrada` já definida.
            Salvando = false;
        }
    }

    /// <summary>
    /// Lê o número digitado. Aceita vírgula e ponto (a mesma pessoa escreve dos dois
    /// jeitos), e a conversão é INVARIANTE: a cultura da máquina faria "2,5" virar 25 num
    /// posto e 2,5 no outro.
    /// </summary>
    private static decimal? Numero(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;

        var limpo = texto.Trim().Replace(',', '.');
        return decimal.TryParse(
            limpo, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var valor)
            ? valor
            : null;
    }
}
