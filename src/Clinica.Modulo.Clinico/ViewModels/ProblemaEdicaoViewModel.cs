using Clinica.Application.Servicos;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>Uma natureza do problema, com o texto que a tela mostra.</summary>
public sealed record OpcaoNatureza(NaturezaProblema Valor, string Rotulo, string Explicacao);

/// <summary>
/// Formulário de uma linha da LISTA DE PROBLEMAS (parcela 37).
///
/// É construído à mão pela tela, como todo formulário da suíte: recebe o paciente e, na
/// edição, o problema. O CID fica ao lado da descrição e é OPCIONAL — exigi-lo faria o
/// fisioterapeuta e o acupunturista pararem de usar a lista, e lista pela metade é pior que
/// lista nenhuma, porque seria lida como completa.
/// </summary>
public sealed partial class ProblemaEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int _pacienteId;
    private readonly int _problemaId;

    /// <summary>Preenchido na gravação bem-sucedida — é por ele que a janela fecha.</summary>
    public ProblemaPaciente? Salvo { get; private set; }

    public IReadOnlyList<OpcaoNatureza> Naturezas { get; } =
    [
        new(NaturezaProblema.Diagnostico, "Diagnóstico",
            "O que o paciente tem e está em acompanhamento."),
        new(NaturezaProblema.Alergia, "Alergia ou intolerância",
            "Vira alerta na tela de atendimento — inclusive depois de dada por resolvida."),
        new(NaturezaProblema.Antecedente, "Antecedente",
            "Cirurgia, internação, fratura: o que já aconteceu e importa saber."),
        new(NaturezaProblema.MedicacaoContinua, "Medicação de uso contínuo",
            "Também vira alerta: interação é o segundo motivo de dano evitável no consultório.")
    ];

    [ObservableProperty] private string _titulo = "Novo problema";

    [ObservableProperty] private OpcaoNatureza? _natureza;

    [ObservableProperty] private string? _descricao;
    [ObservableProperty] private string? _cid;
    [ObservableProperty] private DateTime? _inicio;
    [ObservableProperty] private string? _observacoes;

    [ObservableProperty] private bool _salvando;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    public ProblemaEdicaoViewModel(
        IServiceScopeFactory escopos, int pacienteId, ProblemaPaciente? existente = null)
    {
        _escopos = escopos;
        _pacienteId = pacienteId;
        _problemaId = existente?.Id ?? 0;

        Natureza = Naturezas.FirstOrDefault(n => n.Valor == (existente?.Natureza
                                                             ?? NaturezaProblema.Diagnostico));

        if (existente is null) return;

        Titulo = "Editar problema";
        Descricao = existente.Descricao;
        Cid = existente.Cid;
        Inicio = existente.Inicio?.ToDateTime(TimeOnly.MinValue);
        Observacoes = existente.Observacoes;
    }

    /// <summary>
    /// A explicação da natureza escolhida, na tela. O usuário precisa saber, ANTES de
    /// gravar, que marcar "alergia" muda o que aparece na próxima consulta — descobrir o
    /// efeito depois é o que faz a pessoa desconfiar da tela.
    /// </summary>
    public string ExplicacaoNatureza => Natureza?.Explicacao ?? string.Empty;

    partial void OnNaturezaChanged(OpcaoNatureza? value)
        => OnPropertyChanged(nameof(ExplicacaoNatureza));

    [RelayCommand]
    private async Task SalvarAsync()
    {
        try
        {
            Salvando = true;
            Mensagem = null;
            MensagemEhErro = false;

            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<ProblemaPacienteService>();

            Salvo = await servico.SalvarAsync(new ProblemaPaciente
            {
                Id = _problemaId,
                PacienteId = _pacienteId,
                ProfissionalId = SessaoUsuario.Atual.ProfissionalId,
                Natureza = Natureza?.Valor ?? NaturezaProblema.Diagnostico,
                Descricao = Descricao ?? string.Empty,
                Cid = Cid,
                Inicio = Inicio is { } d ? DateOnly.FromDateTime(d) : null,
                Observacoes = Observacoes
            }, SessaoUsuario.Atual.Operador);
        }
        catch (Exception ex)
        {
            Salvo = null;
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — problema não pôde ser salvo", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            // `Salvando` volta a false por ÚLTIMO: é a mudança que a janela observa para
            // fechar, e ela precisa encontrar `Salvo` já preenchido.
            Salvando = false;
        }
    }
}
