using Clinica.Domain.Entities;

namespace Clinica.Domain;

/// <summary>O preparo é compartilhado apenas pelos medicamentos do mesmo grupo.</summary>
public static class GruposInfusao
{
    public static void Validar(IReadOnlyList<ItemPrescricaoInterna> itens, bool diluicaoUnica)
    {
        if (!itens.Any(i => i.GrupoInfusao.HasValue)) return;
        if (diluicaoUnica || itens.Any(i => i.GrupoInfusao is null or < 1 or > 50))
            throw new InvalidOperationException("Confira a identificação das infusões e seus diluentes.");
        var visitados = new HashSet<int>();
        int? anterior=null;
        foreach(var i in itens)
        {
            if(i.GrupoInfusao!=anterior && !visitados.Add(i.GrupoInfusao!.Value))
                throw new InvalidOperationException("Mantenha os medicamentos de cada infusão juntos na ordem da prescrição.");
            anterior=i.GrupoInfusao;
        }
        foreach (var grupo in itens.GroupBy(i => i.GrupoInfusao))
        {
            if (grupo.Select(i => (Limpar(i.Diluente), Limpar(i.Volume), i.Via, Limpar(i.TempoInfusao), i.HoraPrevista)).Distinct().Count() != 1)
                throw new InvalidOperationException($"Os medicamentos da infusão {grupo.Key} devem compartilhar diluente, volume, via, tempo e horário.");
        }
    }

    public static string Rotulo(ItemPrescricaoInterna i) =>
        $"Infusão {i.GrupoInfusao} · Diluente: {i.Diluente ?? "Não informado"} · Volume total: {i.Volume ?? "Não informado"}";

    private static string? Limpar(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
