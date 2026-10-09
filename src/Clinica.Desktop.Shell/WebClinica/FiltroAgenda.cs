using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Data;

namespace Clinica.Desktop.Shell.WebClinica;

/// <summary>Filtra apenas a apresentação; não altera disponibilidade nem dados clínicos.</summary>
public sealed class FiltroAgenda
{
    private string[] _termos = [];
    private string _modalidade = "", _situacao = "todos";
    private int _revisao;
    private readonly ConditionalWeakTable<ICollectionView, Revisao> _vistas = new();
    private sealed class Revisao { public int Valor = -1; }

    public bool Receber(JsonElement m, string contexto)
    {
        if (m.GetProperty("acao").GetString() != "filtrar" || m.GetProperty("contexto").GetString() != contexto) return false;
        _termos = (m.GetProperty("busca").GetString() ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        _modalidade = m.GetProperty("modalidade").GetString() ?? "";
        _situacao = m.GetProperty("situacao").GetString() ?? "todos";
        _revisao++;
        return true;
    }

    public bool Aceitar(string paciente, string modalidade, string grupo) =>
        (_modalidade.Length == 0 || modalidade == _modalidade) && (_situacao == "todos" || grupo == _situacao) &&
        _termos.All(t => CultureInfo.GetCultureInfo("pt-BR").CompareInfo.IndexOf(paciente, t, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);

    public void Aplicar(IEnumerable itens, Predicate<object> aceitar)
    {
        var vista = CollectionViewSource.GetDefaultView(itens);
        var revisao = _vistas.GetValue(vista, _ => new Revisao());
        if (revisao.Valor == _revisao) return;
        vista.Filter = aceitar;
        revisao.Valor = _revisao;
    }
}
