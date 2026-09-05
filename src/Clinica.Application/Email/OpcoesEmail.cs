using Clinica.Domain;

namespace Clinica.Application.Email;

/// <summary>
/// O servidor de saída (SMTP) da clínica, para o lembrete automático por e-mail
/// (set/2026). Mora no BANCO, editado em Configurações → Lembretes por e-mail, pela regra
/// da parcela 53: variável de ambiente é ritual de instalação, e o lembrete sai da
/// Recepção E do Gerente — configurar numa máquina e esquecer a outra faria o lembrete
/// sair de um posto e falhar calado no outro.
///
/// <b>Meia configuração é configuração nenhuma</b>, e é <see cref="De"/> — num lugar só —
/// que decide: a tela e o serviço leem a mesma resposta para "está ligado?".
/// </summary>
/// <param name="Host">Servidor de saída (ex.: <c>smtp.gmail.com</c>).</param>
/// <param name="Porta">Porta do servidor; 587 quando não informada.</param>
/// <param name="Usuario">Login no servidor, ou <c>null</c> para servidor sem autenticação.</param>
/// <param name="Senha">Senha (ou senha de aplicativo) do login.</param>
/// <param name="Remetente">Endereço que aparece como "De:" — e que recebe a resposta do paciente.</param>
/// <param name="NomeRemetente">Nome ao lado do endereço ("Clínica SemDor"); em branco sai só o endereço.</param>
/// <param name="UsarTls">Liga a criptografia STARTTLS. Padrão ligado; desligue só para relay interno.</param>
public sealed record OpcoesEmail(
    string Host,
    int Porta,
    string? Usuario,
    string? Senha,
    string Remetente,
    string? NomeRemetente,
    bool UsarTls)
{
    public const int PortaPadrao = 587;

    /// <summary>Tempo-limite de UM envio. Quem abre o app está esperando a tela, não o servidor.</summary>
    public static readonly TimeSpan TempoLimite = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Monta as opções, ou devolve <c>null</c> quando falta o que quer que seja.
    ///
    /// Porta fora da faixa cai no padrão em vez de recusar: "5870" digitado por engano é
    /// erro de tela, e o "Enviar e-mail de teste" mostra na hora que não conectou.
    /// Remetente inválido RECUSA — o servidor rejeitaria cada envio com uma frase que não
    /// diz nada sobre esta tela.
    /// </summary>
    public static OpcoesEmail? De(
        string? host, string? porta, string? usuario, string? senha,
        string? remetente, string? nomeRemetente, bool usarTls)
    {
        host = EnderecoDeEmail.Normalizar(host);
        remetente = EnderecoDeEmail.SeValido(remetente);
        if (host is null || remetente is null) return null;

        return new OpcoesEmail(
            host,
            PortaValida(porta),
            EnderecoDeEmail.Normalizar(usuario),
            string.IsNullOrEmpty(senha) ? null : senha,
            remetente,
            EnderecoDeEmail.Normalizar(nomeRemetente),
            usarTls);
    }

    /// <summary>A porta informada quando serve (1 a 65535); a padrão em qualquer outro caso.</summary>
    public static int PortaValida(string? porta)
        => int.TryParse(porta?.Trim(), out var p) && p is >= 1 and <= 65535 ? p : PortaPadrao;

    /// <summary>"maria@clinica.com.br por smtp.host.com:587" — a frase que a tela de Configurações escreve.</summary>
    public string Descricao => $"{Remetente} por {Host}:{Porta}" + (UsarTls ? " (TLS)" : " (sem TLS)");
}
