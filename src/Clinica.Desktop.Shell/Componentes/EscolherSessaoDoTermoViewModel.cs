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
///
/// ⚠️ As sessões chegam PRONTAS, e a janela não vai ao banco. Quem já as leu é a porta —
/// é a leitura dela que decide se esta janela precisa existir —, e ler de novo aqui seria
/// pagar duas idas a um banco remoto por um clique, com o risco de as duas listas
/// DISCORDAREM se a outra máquina mexer na agenda entre elas.
/// </summary>
public sealed partial class EscolherSessaoDoTermoViewModel : ObservableObject
{
    public EscolherSessaoDoTermoViewModel(
        IReadOnlyList<SessaoParaTermo> sessoes, string pacienteNome, string nomeDoTermo)
    {
        ArgumentNullException.ThrowIfNull(sessoes);

        PacienteNome = pacienteNome;
        NomeDoTermo = nomeDoTermo;

        foreach (var sessao in sessoes)
            Opcoes.Add(new OpcaoSessaoTermo(
                sessao.AgendamentoId, sessao.Rotulo, sessao.Detalhe, sessao.Hoje));

        Opcoes.Add(OpcaoSessaoTermo.Avulso);

        // A sessão de HOJE já vem marcada quando é uma só: é o caso esperado, e deixar a
        // janela sem escolha obrigaria um clique para confirmar o óbvio. Com duas, nada vem
        // marcado — marcar a primeira seria decidir pela pessoa justamente no caso em que a
        // pergunta existe.
        var deHoje = Opcoes.Where(o => o.Hoje).ToList();
        if (deHoje.Count == 1) Selecionada = deHoje[0];
    }

    public string PacienteNome { get; }

    public string NomeDoTermo { get; }

    public ObservableCollection<OpcaoSessaoTermo> Opcoes { get; } = [];

    [ObservableProperty] private OpcaoSessaoTermo? _selecionada;

    /// <summary>A sessão escolhida. Nulo = termo avulso, sem sessão.</summary>
    public int? Escolhida => Selecionada?.AgendamentoId;

    /// <summary>Dispara quando a pessoa confirma — a janela fecha.</summary>
    public event Action? Escolheu;

    public bool PodeConfirmar => Selecionada is not null;

    partial void OnSelecionadaChanged(OpcaoSessaoTermo? value)
        => OnPropertyChanged(nameof(PodeConfirmar));

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
