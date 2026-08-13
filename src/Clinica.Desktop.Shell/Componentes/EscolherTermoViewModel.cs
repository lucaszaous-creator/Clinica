using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// "Qual termo o paciente vai assinar?" — a pergunta da coleta AVULSA (parcela 66, 3ª
/// rodada).
///
/// Nas portas do DIA o modelo já vem decidido pela exigência do procedimento marcado. Aqui
/// não há procedimento nenhum: o paciente apareceu para tirar dúvidas, ou passou no balcão,
/// e a clínica quer aproveitar. Alguém precisa dizer qual papel é.
///
/// A tela é deliberadamente magra — lista, um clique, pronto. Ela fica entre a pessoa e o
/// ato que ela já decidiu fazer, e cada campo a mais aqui é um a menos de atenção no termo,
/// que é onde a atenção importa.
/// </summary>
public sealed partial class EscolherTermoViewModel : ObservableObject
{
    private readonly TermoProcedimentoService _termos;

    public EscolherTermoViewModel(TermoProcedimentoService termos, string pacienteNome)
    {
        _termos = termos;
        PacienteNome = pacienteNome;
        _ = CarregarAsync();
    }

    public string PacienteNome { get; }

    public ObservableCollection<ModeloDocumento> Modelos { get; } = [];

    [ObservableProperty] private ModeloDocumento? _selecionado;

    [ObservableProperty] private bool _carregando;

    [ObservableProperty] private string _mensagem = string.Empty;

    /// <summary>O modelo escolhido. Nulo enquanto ninguém escolheu.</summary>
    public ModeloDocumento? Escolhido { get; private set; }

    /// <summary>Dispara quando a pessoa escolhe — a janela fecha.</summary>
    public event Action? Escolheu;

    public bool PodeEscolher => Selecionado is not null && !Carregando;

    partial void OnSelecionadoChanged(ModeloDocumento? value)
        => OnPropertyChanged(nameof(PodeEscolher));

    partial void OnCarregandoChanged(bool value)
        => OnPropertyChanged(nameof(PodeEscolher));

    private async Task CarregarAsync()
    {
        Carregando = true;

        try
        {
            var modelos = await _termos.ModelosDisponiveisAsync();

            // Monta e só então publica: entre o Clear() e o último Add não pode haver await.
            Modelos.Clear();
            foreach (var m in modelos) Modelos.Add(m);

            Selecionado = Modelos.FirstOrDefault();

            // Estado vazio que DIZ o que fazer. A clínica que nunca escreveu um termo veria
            // uma lista em branco e concluiria que a tela quebrou — e o caminho ("o Gerente
            // escreve em Configurações") não é adivinhável de dentro do balcão.
            if (Modelos.Count == 0)
                Mensagem = "Nenhum termo foi escrito ainda. A direção escreve o texto em "
                           + "Configurações → \"Escrever termos…\", no aplicativo do Gerente.";
        }
        catch (Exception ex)
        {
            Mensagem = "Não foi possível carregar os termos: " + ex.Message;
            Application.Diagnostico.Registrar("Escolha do termo do procedimento", ex);
        }
        finally
        {
            Carregando = false;
        }
    }

    [RelayCommand]
    private void Escolher()
    {
        // Guarda que FALA: o botão já está apagado sem seleção, mas o atalho de teclado
        // passa direto — e voltar calada é botão que não faz nada (parcela 41).
        if (Selecionado is null)
        {
            Mensagem = "Escolha qual termo o paciente vai assinar.";
            return;
        }

        Escolhido = Selecionado;
        Escolheu?.Invoke();
    }
}
