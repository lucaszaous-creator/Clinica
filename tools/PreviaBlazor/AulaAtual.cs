namespace PreviaBlazor;

/// <summary>
/// A aula aberta NESTE navegador (out/2026): o hospedeiro da prévia navega por ela, para os
/// atalhos das telas ("Abrir a ficha", "Ir para o Caixa") funcionarem na gravação como no app.
/// Fora da página da aula não há moldura, e navegar responde falso — o que a prévia sempre fez.
/// </summary>
public sealed class AulaAtual
{
    public MolduraDaAula? Moldura { get; set; }

    public bool Navegar(string chave)
    {
        if (Moldura is null || !Moldura.Conhece(chave)) return false;
        Moldura.Navegar(chave);
        return true;
    }
}
