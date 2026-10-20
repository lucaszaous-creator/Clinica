namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// O POSTO DO CONSULTÓRIO visto das telas Blazor (out/2026 — Meu dia, Minha semana, Sessões sem
/// evolução e Meus números viraram Blazor; a tela do paciente continua WPF).
///
/// "Atender", "Escrever evolução" e o clique num horário da semana levam ao PRONTUÁRIO, que é a
/// <c>PacienteWorkspaceView</c> do módulo Clínico: o caminho é fixar o paciente no
/// <c>PacienteEmFoco</c> (o contexto do posto — paciente, horário, atendimento e o DIA do
/// horário, porque o casamento sessão × evolução cai para paciente + data) e navegar pela
/// <c>NavegacaoSuite</c>. Nada disso é alcançável daqui (este projeto é net8.0 puro), então a
/// tela PEDE a quem a hospeda — como pede o disco ao <see cref="IHostDaTela"/> e as janelas da
/// agenda ao <see cref="IJanelasDaAgenda"/>. O módulo Clínico registra a implementação WPF
/// (<c>PostoDoConsultorioWpf</c>), que faz EXATAMENTE o que os ViewModels WPF faziam; onde ela
/// não existe (a prévia, os testes) vale <see cref="PostoDoConsultorioIndisponivel"/>, e a tela
/// DIZ que a ação é do aplicativo do Consultório — botão que não faz nada em silêncio é o defeito
/// da parcela 41.
///
/// As barreiras de permissão (<c>VerProntuario</c>) e o carimbo de entrada na sala são de QUEM
/// CHAMA (a tela), como toda segunda barreira; a ponte só fixa o foco e navega.
/// </summary>
public interface IPostoDoConsultorio
{
    /// <summary>Falso onde não há posto WPF: a tela avisa em vez de navegar para nada.</summary>
    bool Disponivel { get; }

    /// <summary>
    /// Fixa o paciente no posto e abre o destino. Falso = o destino não existe neste executável
    /// (ou a ponte está indisponível) — quem chama DIZ isso.
    /// </summary>
    Task<bool> AbrirNoPostoAsync(PacienteNoPosto paciente, DestinoNoPosto destino);

    /// <summary>
    /// Avisa quando o BANCO diz que a agenda mudou noutra máquina (a releitura em menos de um
    /// segundo, em vez de esperar o relógio — que continua como rede para aviso perdido). Nulo =
    /// não há aviso (a tela fica só com o relógio). Descartar a assinatura desliga o aviso.
    /// </summary>
    IDisposable? EscutarAgenda(Func<Task> reler);

    /// <summary>
    /// O paciente em foco no posto (quem acabou de ser atendido) — a tela de Documentos abre
    /// já nele, como a de Prescrições WPF abria. Nulo = ninguém em foco (ou sem posto).
    /// </summary>
    PacienteNoPosto? PacienteEmFoco => null;
}

/// <summary>Para onde o posto leva o paciente.</summary>
public enum DestinoNoPosto
{
    /// <summary>
    /// O "Atender": a seção de escrita de QUEM clicou — o S-O-A-P de quem consulta, a passagem
    /// de enfermagem de quem executa (a bifurcação do <c>PostoClinico.ChaveDoAtendimento</c>).
    /// </summary>
    Atendimento,

    /// <summary>A sessão do MÉDICO, sem bifurcação — a dívida de prontuário é de evolução.</summary>
    EvolucaoMedica,

    /// <summary>A tela do paciente na ficha (o clique num horário da semana).</summary>
    Paciente,

    /// <summary>A tela do paciente no Histórico (o Prontuário › Por paciente da Recepção).</summary>
    Historico
}

/// <summary>O que o posto precisa saber do paciente escolhido.</summary>
public sealed record PacienteNoPosto(
    int PacienteId,
    string Paciente,
    int? AgendamentoId,
    int? AtendimentoId,
    DateOnly? DataDoHorario);

/// <summary>Onde não há posto WPF (a prévia, os testes): nada abre, e a tela diz.</summary>
public sealed class PostoDoConsultorioIndisponivel : IPostoDoConsultorio
{
    public static readonly PostoDoConsultorioIndisponivel Instancia = new();

    /// <summary>A frase que a tela mostra quando a ponte não existe.</summary>
    public const string Frase = "O prontuário do paciente abre no aplicativo do Consultório.";

    public bool Disponivel => false;
    public Task<bool> AbrirNoPostoAsync(PacienteNoPosto paciente, DestinoNoPosto destino) => Task.FromResult(false);
    public IDisposable? EscutarAgenda(Func<Task> reler) => null;
}
