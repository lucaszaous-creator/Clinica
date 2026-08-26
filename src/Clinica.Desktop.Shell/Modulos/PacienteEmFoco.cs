namespace Clinica.Desktop.Shell.Modulos;

/// <summary>
/// Quem está sendo atendido AGORA neste posto.
///
/// Por que existe
/// --------------
/// Nos outros módulos o paciente é um parâmetro de cada tela: a recepção busca o paciente
/// na agenda, busca de novo na ficha, busca de novo no prontuário — e está certo, porque
/// no balcão cada tela atende uma pessoa diferente em sequência.
///
/// No posto de quem ATENDE é o contrário: a pessoa escolhe o paciente UMA vez, quando
/// chama da fila, e passa os vinte minutos seguintes entre várias telas sobre a MESMA
/// pessoa — a evolução, a curva de dor, as escalas, o histórico. Fazer com que ela
/// redigitasse o nome a cada troca de aba é o tipo de atrito que faz o sistema ser
/// preenchido depois, de memória, no fim do turno.
///
/// Então o paciente é CONTEXTO do posto, não argumento de tela. Registrado como singleton
/// e lido por cada ViewModel na abertura — o shell reconstrói a View a cada navegação
/// (<c>ShellViewModel.Navegar</c>), então basta ler no construtor, sem evento nem
/// assinatura para desfazer.
///
/// ⚠️ POR QUE ELE MORA NO SHELL, e não no módulo do Consultório (parcela 88)
/// ------------------------------------------------------------------------
/// Ele nasceu lá, quando o único posto era o do médico. A enfermagem tem um posto
/// também — e a tela dela é do SHELL, publicada por DOIS módulos (Consultório e
/// Recepção, parcela 71). Para o "Atender" dela abrir o paciente na tela clínica, quem
/// ENTREGA a pessoa é uma tela do shell e quem a RECEBE é uma do módulo: o handoff
/// atravessa a fronteira, e um tipo que só existe de um lado não pode ser o contrato.
///
/// A alternativa era um segundo singleton de handoff no shell — e duas respostas para
/// "quem é o paciente deste posto" divergem na primeira correção, com a agravante de o
/// erro não estourar: a tela abriria a pessoa ERRADA. Subir o tipo é o mesmo movimento
/// que já pôs aqui o mapa corporal e o <c>SeletorPacienteViewModel</c> (parcela 36):
/// compartilhar a DECISÃO sem compartilhar a janela.
///
/// Quem o REGISTRA no DI continua sendo o módulo do Consultório: em
/// <c>Clinica.Recepcao.exe</c> não há posto clínico, e resolver o singleton lá seria
/// oferecer um contexto que nenhuma tela daquele executável consome. É por isso que a
/// tela da enfermagem o pede com <c>GetService</c> (nulo = este app não tem posto) e
/// anda junto de <c>NavegacaoSuite.Existe</c>, que responde a mesma pergunta pelo lado
/// do destino.
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
