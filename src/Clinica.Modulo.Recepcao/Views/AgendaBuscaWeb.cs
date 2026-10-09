using System.Text.Json;
using Clinica.Application.Modelos;
using Clinica.Desktop.Shell.WebClinica;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Views;

internal sealed class AgendaBuscaWeb(AgendaViewModel vm)
{
    private readonly FiltroAgenda _filtro = new();
    private void Filtrar()
    {
        foreach (var coluna in vm.ColunasPlanejamento)
            _filtro.Aplicar(coluna.Blocos, item => item is BlocoAgendaVisual b && (b.Cartao is not { } l || _filtro.Aceitar(l.Paciente, l.Modalidade, SituacaoVisualAgenda.Grupo(l.Situacao, l.Etapa))));
    }
    private string Contexto => $"{vm.Dia:yyyy-MM-dd}:{vm.ModoSemana}:{vm.FiltroProfissional?.Id}:{vm.FiltroSala?.Id}";
    private IEnumerable<CartaoAgenda> Linhas => vm.Colunas.SelectMany(c => c.Horarios).DistinctBy(l => l.AgendamentoId);
    public object Estado()
    {
        Filtrar();
        return new
    {
        contexto = Contexto, carregando = vm.Carregando, naoVerificado = vm.NaoVerificado,
        linhas = Linhas.OrderBy(l => l.DataHora).Select(l => new LinhaAgendaWeb(l.AgendamentoId.ToString(),
            l.DataHora.ToString("dd/MM/yyyy"), l.Faixa, l.Paciente, l.Modalidade, l.Profissional, l.Sala,
            StatusDaFila.Palavra(l.Situacao, l.Etapa), SituacaoVisualAgenda.Grupo(l.Situacao, l.Etapa),
            l.EhEncaixe ? "Encaixe" : "", "", l.Observacoes ?? "", "Ver horário", true)).ToArray()
    };
    }
    public Task Executar(JsonElement m)
    {
        if (_filtro.Receber(m, Contexto)) Filtrar();
        return Task.CompletedTask;
    }
}
