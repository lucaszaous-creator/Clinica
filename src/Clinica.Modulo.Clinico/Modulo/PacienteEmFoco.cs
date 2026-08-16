namespace Clinica.Clinico.Modulo;

/// <summary>
/// Quem está sendo atendido AGORA neste posto.
///
/// Por que existe
/// --------------
/// Nos outros módulos o paciente é um parâmetro de cada tela: a recepção busca o paciente
/// na agenda, busca de novo na ficha, busca de novo no prontuário — e está certo, porque
/// no balcão cada tela atende uma pessoa diferente em sequência.
///
/// No consultório é o contrário: o profissional escolhe o paciente UMA vez, quando chama
/// da fila, e passa os vinte minutos seguintes entre quatro telas sobre a MESMA pessoa —
/// a evolução, a curva de dor, as escalas, o histórico. Fazer com que ele redigitasse o
/// nome a cada troca de aba é o tipo de atrito que faz o sistema ser preenchido depois,
/// de memória, no fim do turno.
///
/// Então o paciente é CONTEXTO do módulo, não argumento de tela. Registrado como singleton
/// e lido por cada ViewModel na abertura — o shell reconstrói a View a cada navegação
/// (<c>ShellViewModel.Navegar</c>), então basta ler no construtor, sem evento nem
/// assinatura para desfazer.
///
/// O que ele NÃO é: cache de dados do paciente. Guarda o id e o nome para o cabeçalho; a
/// leitura clínica é sempre do banco, porque a outra ponta do balcão pode ter mudado o
/// cadastro entre uma tela e outra.
/// </summary>
public sealed class PacienteEmFoco
{
    public int? PacienteId { get; private set; }

    public string Nome { get; private set; } = string.Empty;

    /// <summary>Há paciente escolhido neste posto.</summary>
    public bool Definido => PacienteId is not null;

    /// <summary>
    /// Agendamento de origem, quando o paciente foi chamado do dia. É o que permite à
    /// evolução nascer LIGADA ao horário — sem esse vínculo, o consultório continuaria
    /// cobrando o registro de uma sessão que acabou de ser escrita.
    /// </summary>
    public int? AgendamentoId { get; private set; }

    /// <summary>Atendimento/guia correspondente, quando o check-in já o gerou.</summary>
    public int? AtendimentoId { get; private set; }

    /// <summary>
    /// Dia do horário de origem. Anda JUNTO do <see cref="AgendamentoId"/> porque o
    /// caminho de baixo de "esta sessão já foi escrita?" casa por paciente + data — e o
    /// posto abre horário de outros dias (a dívida de prontuário e a Minha semana), então
    /// presumir hoje daria a resposta errada justamente onde a pergunta importa.
    /// </summary>
    public DateOnly? DataDoHorario { get; private set; }

    /// <summary>
    /// Escolhe o paciente do posto.
    ///
    /// Sem <paramref name="agendamentoId"/> o horário de origem é ESQUECIDO, e é por isso
    /// que os últimos parâmetros são opcionais em vez de haver uma sobrecarga curta:
    /// quem escolhe pela busca (e não chamando da agenda) precisa que o vínculo do
    /// paciente anterior caia, senão a evolução de um nasceria amarrada à sessão do outro.
    /// </summary>
    public void Definir(int pacienteId, string nome, int? agendamentoId = null,
                        int? atendimentoId = null, DateOnly? dataDoHorario = null)
    {
        PacienteId = pacienteId;
        Nome = nome;
        AgendamentoId = agendamentoId;
        AtendimentoId = atendimentoId;
        DataDoHorario = dataDoHorario;
    }

    public void Limpar()
    {
        PacienteId = null;
        Nome = string.Empty;
        AgendamentoId = null;
        AtendimentoId = null;
        DataDoHorario = null;
    }
}
