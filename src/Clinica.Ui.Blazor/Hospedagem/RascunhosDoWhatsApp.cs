namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// Os rascunhos das conversas do WhatsApp, por JANELA (Scoped: um por navegador), e não por tela
/// (out/2026). A mesma conversa aparece na aba WhatsApp e na conversa ao lado das outras telas
/// (<see cref="ConversaAoLado"/>): com o rascunho dentro da tela, fechar a gaveta ou trocar de paciente
/// nela apagava sem perguntar o que a secretária estava escrevendo. Aqui ele sobrevive à gaveta, e a
/// aba e a gaveta mostram o MESMO texto. Nada vai ao banco nem ao CRM (é rascunho); a troca de usuário
/// reabre o app e leva junto.
/// Chave: o id da conversa, ou "caso-{id}" antes de a conversa do convite existir.
/// </summary>
public sealed class RascunhosDoWhatsApp : Dictionary<string, RascunhoDaConversa>
{
    public RascunhosDoWhatsApp() : base(StringComparer.Ordinal) { }
}

/// <summary>O rascunho de UMA conversa: nada vai ao banco, e o Guid "D" do envio só troca depois que ele sai.</summary>
public sealed class RascunhoDaConversa
{
    public string Texto { get; set; } = "";
    public bool Nota { get; set; }
    public Clinica.Crm.Contracts.AppAttachment? Anexo { get; set; }
    public string IdDoEnvio { get; set; } = Guid.NewGuid().ToString("D");
    /// <summary>A mensagem que a resposta cita ("Responder citando", o botão direito). Nota interna não cita.</summary>
    public Clinica.Application.Servicos.CitacaoNaConversa? Citada { get; set; }
    public bool TemConteudo => !string.IsNullOrWhiteSpace(Texto) || !Nota && Anexo is not null;
    public void Saiu() { Texto = ""; Anexo = null; Nota = false; Citada = null; IdDoEnvio = Guid.NewGuid().ToString("D"); }
}
