namespace Clinica.Domain.Entities;

/// <summary>Para que o SMS saiu — e é isto que decide se ele exige consentimento.</summary>
public enum FinalidadeSms
{
    /// <summary>Confirmar a própria sessão: transacional, sem consentimento de marketing (parcela 5).</summary>
    ConfirmacaoSessao,

    /// <summary>Campanha, promoção, recall em massa: SÓ para quem consentiu comunicação.</summary>
    Campanha,

    /// <summary>O teste de Configurações — para um número digitado, nenhum paciente.</summary>
    Teste
}

/// <summary>Em que pé está o envio.</summary>
public enum SituacaoSms
{
    /// <summary>A linha foi gravada ANTES do envio — é o que impede duas máquinas de mandar o mesmo.</summary>
    Enviando,

    /// <summary>O provedor aceitou a mensagem.</summary>
    Enviada,

    /// <summary>O provedor recusou ou não respondeu. Ver <see cref="MensagemSms.Erro"/>.</summary>
    Falhou
}

/// <summary>
/// UM SMS que a clínica mandou (out/2026). Existe por três razões, e nenhuma é enfeite:
///
/// 1. <b>Cada SMS é cobrado.</b> A direção precisa saber quantos saíram no mês e quantos
///    segmentos custaram — sem a linha, a fatura do provedor seria o único registro.
/// 2. <b>A idempotência é de BANCO, não de memória</b> (a regra da <see cref="ContatoCampanha"/>):
///    a Recepção e o Gerente rodam o lembrete ao mesmo tempo, e a linha é GRAVADA antes do
///    envio com <see cref="ChaveIdempotencia"/> única. A segunda máquina esbarra no índice e
///    não manda — sem isto, cada sessão custaria dois SMS e o paciente receberia dois.
/// 3. <b>A trilha responde "mandaram SMS para mim?"</b> — o titular pode perguntar (LGPD,
///    art. 18), e a campanha precisa provar que só saiu para quem consentiu.
///
/// Não se apaga: é registro de comunicação, como o contato da campanha.
/// </summary>
public class MensagemSms
{
    public int Id { get; set; }

    public int? PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>O contato da rodada de confirmação, quando o SMS nasceu dela.</summary>
    public int? ContatoCampanhaId { get; set; }

    public FinalidadeSms Finalidade { get; set; }

    public SituacaoSms Situacao { get; set; } = SituacaoSms.Enviando;

    /// <summary>
    /// O que impede o mesmo SMS de sair duas vezes: <c>CONF:{contato}</c> na confirmação,
    /// <c>CAMP:{lote}:{paciente}</c> na campanha. Único.
    /// </summary>
    public string ChaveIdempotencia { get; set; } = string.Empty;

    /// <summary>O celular em E.164, como foi entregue ao provedor.</summary>
    public string Telefone { get; set; } = string.Empty;

    /// <summary>O texto EXATO que saiu (já no alfabeto GSM).</summary>
    public string Texto { get; set; } = string.Empty;

    /// <summary>Segmentos cobrados (ver <see cref="Clinica.Domain.TextoSms.Segmentos"/>).</summary>
    public int Segmentos { get; set; }

    /// <summary>O identificador que o provedor devolveu — é por ele que se reclama de uma entrega.</summary>
    public string? IdNoProvedor { get; set; }

    public string? Erro { get; set; }

    public int Tentativas { get; set; }

    public DateTime CriadaEm { get; set; } = DateTime.Now;

    public DateTime? EnviadaEm { get; set; }

    /// <summary>Quem disparou: o login, ou o envio automático.</summary>
    public string? EnviadaPor { get; set; }

    /// <summary>Falhou e ainda pode tentar de novo.</summary>
    public bool PodeRetentar => Situacao == SituacaoSms.Falhou && Tentativas < TentativasMaximas;

    /// <summary>Três tentativas: provedor fora do ar se resolve; número recusado não.</summary>
    public const int TentativasMaximas = 3;

    public static string ChaveDeConfirmacao(int contatoId) => $"CONF:{contatoId}";

    public static string ChaveDeCampanha(string lote, int pacienteId) => $"CAMP:{lote}:{pacienteId}";
}
