namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// "Outra máquina gravou nesta tabela" — o aviso instantâneo do banco (o <c>pg_notify</c> que o
/// <c>OuvinteDeMudancas</c> da Infrastructure escuta, out/2026), para as telas Blazor que o WPF
/// religava pela <c>EscutaDoBanco</c> (a Grade da agenda: um horário marcado no outro balcão
/// aparece em menos de um segundo, em vez de esperar o relógio de um minuto).
///
/// ⚠️ É COMPLEMENTO, nunca substituto: o relógio da tela continua. Aviso perdido (rede que
/// caiu) não pode deixar a grade parada. O aviso não carrega dado: quem quer saber o que mudou
/// relê pelo caminho de sempre, com as permissões de sempre.
///
/// Esta biblioteca é net8.0 puro e não enxerga a Infrastructure: o hospedeiro WPF registra a
/// implementação; onde ela não existe (a prévia, os testes) vale <see cref="AvisosDoBancoAusentes"/>,
/// que nunca dispara — e a tela segue só com o relógio, como sempre foi.
/// </summary>
public interface IAvisosDoBanco
{
    /// <summary>O nome da TABELA que mudou. Disparado fora da thread da tela — use InvokeAsync.</summary>
    event Action<string>? Mudou;
}

/// <summary>Onde o banco não avisa (a prévia, os testes): nunca dispara.</summary>
public sealed class AvisosDoBancoAusentes : IAvisosDoBanco
{
    public static readonly AvisosDoBancoAusentes Instancia = new();

    public event Action<string>? Mudou { add { } remove { } }
}
