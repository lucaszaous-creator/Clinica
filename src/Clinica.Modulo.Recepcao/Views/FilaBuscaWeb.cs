using System.Globalization;
using System.Text.Json;
using System.Windows.Data;
using Clinica.Application.Modelos;
using Clinica.Desktop.Shell.WebClinica;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Views;

internal sealed class FilaBuscaWeb(FilaViewModel vm)
{
    private string _busca = "", _modalidade = "", _situacao = "todos";
    public object Estado() => new
    {
        contexto = vm.Dia.ToString("yyyy-MM-dd"), carregando = vm.Carregando, naoVerificado = vm.NaoVerificado,
        linhas = vm.Linhas.Select(l => new LinhaAgendaWeb(l.AgendamentoId.ToString(), l.DataHora.ToString("dd/MM/yyyy"),
            l.Horario, l.Paciente, l.Modalidade, l.Profissional, l.Sala, l.Status,
            SituacaoVisualAgenda.Grupo(l.Situacao, l.Etapa), l.StatusDetalhe, "", l.Observacoes ?? "", "", false)).ToArray()
    };
    public Task Executar(JsonElement m)
    {
        if (m.GetProperty("acao").GetString() != "filtrar" || m.GetProperty("contexto").GetString() != vm.Dia.ToString("yyyy-MM-dd")) return Task.CompletedTask;
        _busca = m.GetProperty("busca").GetString() ?? "";
        _modalidade = m.GetProperty("modalidade").GetString() ?? "";
        _situacao = m.GetProperty("situacao").GetString() ?? "todos";
        var vista = CollectionViewSource.GetDefaultView(vm.Linhas);
        vista.Filter = item => item is CartaoFila l && Aceitar(l);
        vista.Refresh();
        return Task.CompletedTask;
    }
    private bool Aceitar(CartaoFila l) =>
        (_modalidade.Length == 0 || l.Modalidade == _modalidade) &&
        (_situacao == "todos" || SituacaoVisualAgenda.Grupo(l.Situacao, l.Etapa) == _situacao) &&
        _busca.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(t => CultureInfo.GetCultureInfo("pt-BR").CompareInfo.IndexOf(l.Paciente, t, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);
}
