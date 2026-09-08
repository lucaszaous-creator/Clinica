using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// "A qual sessão este termo pertence?" — a pergunta do BALCÃO (set/2026).
///
/// O termo passou a guardar a sessão a que se refere (<c>DocumentoClinico.AgendamentoId</c>),
/// e quem colhe nem sempre sabe qual é: no consultório o horário está aberto na tela e
/// ninguém pergunta nada, mas na recepção o paciente pode ter duas sessões no dia — ou
/// nenhuma, e estar assinando com semanas de antecedência.
///
/// ⚠️ Esta janela só ABRE quando há dúvida. Com um horário só, a porta amarra sozinha e
/// DIZ a que amarrou; com nenhum, o termo nasce avulso sem perguntar. Pergunta que tem uma
/// resposta possível não é pergunta — é um clique a mais, e é assim que se ensina alguém a
/// confirmar sem ler (a lição do incidente dos três encaixes em 71 segundos).
///
/// ⚠️ "Nenhuma — termo avulso" é opção de primeira classe, e não uma saída de emergência:
/// é o caso do paciente que assina o consentimento na consulta em que veio tirar dúvidas.
/// Sem ela, a janela obrigaria a inventar uma procedência para poder fechar.
/// </summary>
public sealed partial class EscolherSessaoDoTermoViewModel : ObservableObject
{
    private readonly TermoProcedimentoService _termos;
    private readonly int _pacienteId;

    public EscolherSessaoDoTermoViewModel(
        TermoProcedimentoService termos, int pacienteId, string pacienteNome, string nomeDoTermo)
    {
        _termos = termos;
        _pacienteId = pacienteId;
        PacienteNome = pacienteNome;
        NomeDoTermo = nomeDoTermo;
        _ = CarregarAsync();
    }

    public string PacienteNome { get; }

    public string NomeDoTermo { get; }

    public ObservableCollection<OpcaoSessaoTermo> Opcoes { get; } = [];

    [ObservableProperty] private OpcaoSessaoTermo? _selecionada;

    [ObservableProperty] private bool _carregando;

    [ObservableProperty] private string _mensagem = string.Empty;

    /// <summary>A sessão escolhida. Nulo = termo avulso, sem sessão.</summary>
    public int? Escolhida => Selecionada?.AgendamentoId;

    /// <summary>Dispara quando a pessoa confirma — a janela fecha.</summary>
    public event Action? Escolheu;

    public bool PodeConfirmar => Selecionada is not null && !Carregando;

    partial void OnSelecionadaChanged(OpcaoSessaoTermo? value)
        => OnPropertyChanged(nameof(PodeConfirmar));

    partial void OnCarregandoChanged(bool value)
        => OnPropertyChanged(nameof(PodeConfirmar));

    private async Task CarregarAsync()
    {
        Carregando = true;

        try
        {
            var sessoes = await _termos.SessoesParaTermoAsync(
                _pacienteId, DateOnly.FromDateTime(DateTime.Today));

            // Entre o Clear e o último Add não pode haver await (a regra da parcela 62):
            // a lista é montada FORA e publicada numa passada só.
            var opcoes = sessoes
                .Select(s => new OpcaoSessaoTermo(s.AgendamentoId, s.Rotulo, s.Detalhe, s.Hoje))
                .ToList();

            opcoes.Add(OpcaoSessaoTermo.Avulso);

            Opcoes.Clear();
            foreach (var opcao in opcoes) Opcoes.Add(opcao);

            // A sessão de HOJE já vem marcada quando é uma só: é o caso esperado, e deixar
            // a janela sem escolha obrigaria um clique para confirmar o óbvio. Com duas,
            // nada vem marcado — marcar a primeira seria decidir pela pessoa justamente no
            // caso em que a pergunta existe.
            var deHoje = opcoes.Where(o => o.Hoje).ToList();
            if (deHoje.Count == 1) Selecionada = deHoje[0];
        }
        catch (Exception ex)
        {
            Application.Diagnostico.Registrar("Escolha da sessão do termo", ex);
            Mensagem = "Não foi possível ler a agenda deste paciente. "
                       + "Você ainda pode colher o termo sem ligá-lo a uma sessão.";

            // Terceiro estado: a leitura falhou e o avulso continua possível. Lista vazia
            // aqui seria lida como "este paciente não tem sessão nenhuma", que é uma
            // afirmação que ninguém conferiu.
            Opcoes.Clear();
            Opcoes.Add(OpcaoSessaoTermo.Avulso);
        }
        finally
        {
            Carregando = false;
        }
    }

    [RelayCommand]
    private void Confirmar()
    {
        if (Selecionada is null) return;
        Escolheu?.Invoke();
    }
}

/// <summary>Uma linha da escolha. <c>AgendamentoId</c> nulo é o termo avulso.</summary>
public sealed record OpcaoSessaoTermo(int? AgendamentoId, string Rotulo, string Detalhe, bool Hoje)
{
    public static OpcaoSessaoTermo Avulso { get; } = new(
        null,
        "Nenhuma — termo avulso",
        "O paciente assina agora e o termo não fica ligado a uma sessão.",
        false);
}
