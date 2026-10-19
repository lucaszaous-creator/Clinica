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
}
