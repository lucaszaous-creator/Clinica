namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// O paciente aberto na tela do paciente, como as SEÇÕES o recebem (out/2026, migração da
/// tela do paciente para Blazor). Imutável: trocar de paciente é um contexto NOVO, e a seção
/// que guardou o anterior compara pela <see cref="Versao"/> antes de publicar uma resposta —
/// a resposta de banco que chega fora de ordem não pode pintar o prontuário de outra pessoa
/// (a lição da parcela 60).
/// </summary>
public sealed record ContextoDoPaciente(
    int PacienteId,
    string Nome,
    int? AgendamentoId,
    int? AtendimentoId,
    DateOnly? DataDoHorario,
    int Versao)
{
    /// <summary>O dia a que o registro fica ligado: o do horário, ou hoje.</summary>
    public DateOnly DiaDaSessao => DataDoHorario ?? DateOnly.FromDateTime(DateTime.Today);

    public PacienteNoPosto ParaPosto() => new(PacienteId, Nome, AgendamentoId, AtendimentoId, DataDoHorario);
}

/// <summary>
/// O que a tela do paciente oferece às seções: reler o cabeçalho e a sessão depois de um ato
/// que os muda (finalizar, registrar alergia), e trocar de seção (o "Abrir a anamnese" da
/// ficha leva à seção certa sem a seção conhecer o rail).
/// </summary>
public interface ITelaDoPaciente
{
    Task RelerCabecalhoAsync();
    void IrPara(string secao, int subAba = 0);

    /// <summary>
    /// O estado de uma seção que precisa SOBREVIVER à troca de seção no rail (a folha em
    /// edição): a seção é desmontada ao trocar, e o rascunho de quem digita não pode ir junto.
    /// Um por paciente — trocar de paciente (contexto novo) descarta todos. Quem o guarda
    /// pode implementar <see cref="IFolhaAberta"/> / <see cref="IPassagemAberta"/>: é por eles
    /// que o "Salvar e finalizar" do cabeçalho grava o que está na folha.
    /// </summary>
    T Guardado<T>(string chave, Func<T> criar) where T : class;

    /// <summary>A seção avisa que mudou algo que o cabeçalho mostra (a sessão gravada, o atendimento que nasceu).</summary>
    void AvisarMudanca();
}

/// <summary>
/// A folha do médico aberta na tela (out/2026): o que o "Salvar e finalizar" do cabeçalho pede
/// a ela — gravar o que está escrito ANTES de encerrar (a ordem é amarra: grava → encerra →
/// conclui; falhando a gravação, o carimbo não acontece).
/// </summary>
public interface IFolhaAberta
{
    /// <summary>Há algo a gravar (texto, EVA, CID ou mapa) — NUNCA "a sessão não está em branco".</summary>
    bool TemAlgoParaGravar { get; }

    /// <summary>Nada do que se ESCREVERIA foi escrito — decide se a tela PERGUNTA antes de encerrar.</summary>
    bool SessaoEmBranco { get; }

    /// <summary>A evolução já gravada desta sessão (0 = nenhuma).</summary>
    int EvolucaoId { get; }

    /// <summary>A folha terminou de ler o horário: antes disso, gravar seria gravar o vazio.</summary>
    bool Carregada { get; }

    /// <summary>Grava e DIZ se conseguiu; a mensagem do erro fica na própria folha.</summary>
    Task<bool> TentarSalvarAsync();

    /// <summary>O atendimento nasceu (a conclusão): a próxima gravação sai com o número.</summary>
    void AtendimentoNasceu(int atendimentoId);
}

/// <summary>A passagem de enfermagem em edição na tela: o Finalizar pergunta antes de encerrar sem ela.</summary>
public interface IPassagemAberta
{
    /// <summary>Nada foi escrito na passagem ainda não registrada.</summary>
    bool PassagemEmBranco { get; }
}
