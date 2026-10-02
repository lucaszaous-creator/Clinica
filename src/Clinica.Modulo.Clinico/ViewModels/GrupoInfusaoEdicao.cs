using System.Collections.ObjectModel;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clinica.Clinico.ViewModels;

public sealed partial class GrupoInfusaoEdicao : ObservableObject
{
    [ObservableProperty] private int _numero;
    [ObservableProperty] private string? _diluente = "SF 0,9%";
    [ObservableProperty] private string? _volume;
    [ObservableProperty] private ViaAdministracao _via = ViaAdministracao.Endovenosa;
    [ObservableProperty] private string? _tempo;
    [ObservableProperty] private string? _horario;
    public string Titulo => $"Infusão {Numero}";
    partial void OnNumeroChanged(int value) => OnPropertyChanged(nameof(Titulo));
    public ObservableCollection<LinhaItemPrescricao> Itens { get; } = [];

    public IReadOnlyList<ItemPrescricaoInterna> Preparar()
    {
        if (!string.IsNullOrWhiteSpace(Horario) && !TimeOnly.TryParseExact(Horario,"HH:mm",out _))
            throw new InvalidOperationException($"Confira o horário da infusão {Numero} (HH:mm).");
        var itens = Itens.Where(i => !string.IsNullOrWhiteSpace(i.Descricao)).Select(i => i.Para()).ToArray();
        if (itens.Length == 0) throw new InvalidOperationException($"Adicione medicamentos à infusão {Numero} ou remova a infusão vazia.");
        foreach (var i in itens)
        {
            i.GrupoInfusao=Numero; i.Diluente=Diluente;
            i.Volume=decimal.TryParse(Volume,System.Globalization.NumberStyles.Number,System.Globalization.CultureInfo.GetCultureInfo("pt-BR"),out _) ? Volume?.Trim()+" mL" : Volume;
            i.Via=Via; i.TempoInfusao=Tempo;
            i.HoraPrevista=TimeOnly.TryParseExact(Horario,"HH:mm",out var h) ? h : null;
        }
        return itens;
    }

    public static IReadOnlyList<GrupoInfusaoEdicao> Carregar(IEnumerable<ItemPrescricaoInterna> itens, bool diluicaoUnica, string? diluente, string? volume)
    {
        var lista = itens.OrderBy(i => i.Ordem).ToArray();
        Clinica.Domain.GruposInfusao.Validar(lista,diluicaoUnica);
        if(diluicaoUnica && lista.Select(i=>(i.Via,i.TempoInfusao,i.HoraPrevista)).Distinct().Count()>1)
            throw new InvalidOperationException("A prescrição antiga combina diluição única com vias, tempos ou horários diferentes. Revise os preparos em uma nova prescrição; a via anterior permanece preservada.");
        // Sem diluição única explícita, cada item antigo conserva seu próprio preparo.
        var grupos = new List<List<ItemPrescricaoInterna>>();
        string? anterior=null;
        foreach(var i in lista)
        {
            var chave=i.GrupoInfusao is {} n ? $"grupo:{n}" : !diluicaoUnica ? $"item:{grupos.Count}" : System.Text.Json.JsonSerializer.Serialize(new { Diluente=diluente, Volume=volume, i.Via,i.TempoInfusao,i.HoraPrevista });
            if(chave!=anterior){grupos.Add([]);anterior=chave;}
            grupos[^1].Add(i);
        }
        return grupos.Select((itensDoGrupo,n) =>
        {
            var primeiro=itensDoGrupo.First();
            var g=new GrupoInfusaoEdicao { Numero=n+1,Diluente=diluicaoUnica?diluente:primeiro.Diluente,Volume=diluicaoUnica?volume:primeiro.Volume,Via=primeiro.Via,Tempo=primeiro.TempoInfusao,Horario=primeiro.HoraPrevista?.ToString("HH:mm") };
            foreach(var i in itensDoGrupo) g.Itens.Add(LinhaItemPrescricao.De(i));
            return g;
        }).ToArray();
    }
}
