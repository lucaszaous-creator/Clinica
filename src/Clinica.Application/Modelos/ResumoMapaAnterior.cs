namespace Clinica.Application.Modelos;

/// <summary>Escolha de uma sessão com mapa, sem carregar o texto do prontuário.</summary>
public sealed record ResumoMapaAnterior(int EvolucaoId, DateOnly Data, int Pontos, string? Profissional)
{
    public string Rotulo => $"{Data:dd/MM/yyyy} · {Pontos} pontos"
        + (string.IsNullOrWhiteSpace(Profissional) ? string.Empty : $" · {Profissional}");
}
