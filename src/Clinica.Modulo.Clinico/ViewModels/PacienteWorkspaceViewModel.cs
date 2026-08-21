using Clinica.Clinico.Modulo;
using Clinica.Desktop.Shell.Modulos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>
/// A TELA DO PACIENTE — uma pessoa, cinco abas (parcela 37, 4ª rodada).
///
/// O vício que ela corrige
/// -----------------------
/// As cinco telas clínicas tinham, cada uma, uma coluna de 300 px com a lista de pacientes
/// grudada à esquerda. Era mestre-detalhe espremido numa tela só, repetido seis vezes — e o
/// resultado é o que o cliente viu: metade da largura útil gasta com a MESMA lista, em toda
/// tela, inclusive quando o paciente já estava escolhido havia vinte minutos.
///
/// O desenho certo é o de qualquer prontuário eletrônico sério, e são dois passos:
/// <list type="number">
///   <item><b>Uma lista</b> — "Meu dia" (quem eu vejo hoje) ou "Meus pacientes" (quem eu
///   acompanho). São as duas portas de entrada, e são telas de verdade, com a largura
///   inteira.</item>
///   <item><b>A tela do paciente</b> — clicou, entrou. A identidade fica no topo, as cinco
///   seções viram ABAS, e a largura inteira é do conteúdo clínico.</item>
/// </list>
///
/// Por que abas, e não cinco itens de menu
/// ---------------------------------------
/// Porque as cinco só existem COM paciente. Item de menu que só funciona depois de você ter
/// passado por outro lugar é item que ensina o usuário a errar — e era exatamente o que a
/// versão anterior fazia, abrindo "Medidas" numa tela em branco pedindo que se buscasse
/// alguém. Aba, ao contrário, diz sozinha que pertence à pessoa cujo nome está logo acima.
///
/// As cinco chaves antigas continuam válidas e caem AQUI, cada uma na sua aba: o painel da
/// direção e a fila do dia navegam por elas, e renomear contrato de navegação para arrumar
/// leiaute quebraria o que funciona em outro módulo.
/// </summary>
public sealed partial class PacienteWorkspaceViewModel : ObservableObject
{
    private readonly PacienteEmFoco _foco;

    public AtendimentoViewModel Atendimento { get; }
    public ProntuarioClinicoViewModel Prontuario { get; }
    public EvolucaoDorViewModel Dor { get; }
    public MedidasViewModel Medidas { get; }
    public AvaliacoesViewModel Avaliacoes { get; }

    /// <summary>Aba aberta. É por ela que a chave de navegação escolhe onde cair.</summary>
    [ObservableProperty] private int _abaAtual;

    [ObservableProperty] private string _paciente = string.Empty;

    /// <summary>De onde a pessoa veio: chamada da agenda, ou escolhida na carteira.</summary>
    [ObservableProperty] private string _contexto = string.Empty;

    /// <summary>
    /// Ninguém em foco. Acontece quando se chega aqui por navegação direta (o painel da
    /// direção manda para o Consultório sem ter escolhido paciente): a tela diz o que
    /// fazer em vez de mostrar cinco abas vazias.
    /// </summary>
    [ObservableProperty] private bool _semPaciente = true;

    public PacienteWorkspaceViewModel(IServiceProvider servicos, PacienteEmFoco foco, int aba = 0)
    {
        _foco = foco;
        AbaAtual = aba;

        // As cinco seções são construídas juntas de propósito. Elas falam da MESMA pessoa,
        // e trocar de aba é folhear o prontuário dela — se cada aba fosse carregada no
        // primeiro clique, folhear custaria uma ida ao banco por página, justamente no
        // momento em que se está com o paciente na frente.
        Atendimento = servicos.GetRequiredService<AtendimentoViewModel>();
        Prontuario = servicos.GetRequiredService<ProntuarioClinicoViewModel>();
        Dor = servicos.GetRequiredService<EvolucaoDorViewModel>();
        Medidas = servicos.GetRequiredService<MedidasViewModel>();
        Avaliacoes = servicos.GetRequiredService<AvaliacoesViewModel>();

        SemPaciente = !_foco.Definido;
        Paciente = _foco.Nome;
        // ⚠️ A MESMA função do AtendimentoViewModel (parcela 72). Este cabeçalho escrevia
        // "Chamado da agenda de HOJE" para qualquer horário, enquanto a tela de dentro já
        // dizia a DATA desde a parcela 69 — duas frases para a mesma pergunta, e a de cima
        // era a errada. É a lição das parcelas 64 e 68 pela sétima vez: quando duas telas
        // respondem à mesma coisa, a que ninguém releu é a que mente.
        Contexto = AtendimentoViewModel.DescreverOrigem(
            _foco.AgendamentoId, _foco.DataDoHorario);
    }

    /// <summary>Volta para a lista de onde se veio. Sem sair, não há como trocar de pessoa.</summary>
    [RelayCommand]
    private void Voltar()
        => NavegacaoSuite.Ir(_foco.AgendamentoId is null
            ? ModuloClinico.ChaveMeusPacientes
            : ModuloClinico.ChaveMeuDia);

    /// <summary>Abre a carteira para escolher outra pessoa.</summary>
    [RelayCommand]
    private void TrocarPaciente() => NavegacaoSuite.Ir(ModuloClinico.ChaveMeusPacientes);
}
