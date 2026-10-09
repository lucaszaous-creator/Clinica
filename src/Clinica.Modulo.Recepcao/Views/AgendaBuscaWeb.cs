using System.Text.Json;
using Clinica.Application.Modelos;
using Clinica.Desktop.Shell.WebClinica;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Views;

internal sealed class AgendaBuscaWeb(AgendaViewModel vm)
{
    private string Contexto => $"{vm.Dia:yyyy-MM-dd}:{vm.ModoSemana}:{vm.FiltroProfissional?.Id}:{vm.FiltroSala?.Id}";
    private IEnumerable<CartaoAgenda> Linhas => vm.Colunas.SelectMany(c => c.Horarios).DistinctBy(l => l.AgendamentoId);
    public object Estado() => new
    {
        contexto = Contexto, carregando = vm.Carregando, naoVerificado = vm.NaoVerificado,
        linhas = Linhas.OrderBy(l => l.DataHora).Select(l => new LinhaAgendaWeb(l.AgendamentoId.ToString(),
            l.DataHora.ToString("dd/MM/yyyy"), l.Faixa, l.Paciente, l.Modalidade, l.Profissional, l.Sala,
            StatusDaFila.Palavra(l.Situacao, l.Etapa), SituacaoVisualAgenda.Grupo(l.Situacao, l.Etapa),
            l.EhEncaixe ? "Encaixe" : "", "", l.Observacoes ?? "", "Ver horário", true)).ToArray()
    };
    public async Task Executar(JsonElement m)
    {
        if (vm.Carregando || vm.NaoVerificado || m.GetProperty("contexto").GetString() != Contexto || m.GetProperty("acao").GetString() != "abrir") return;
        var linha = Linhas.FirstOrDefault(l => l.AgendamentoId.ToString() == m.GetProperty("id").GetString());
        if (linha is not null && vm.AbrirHorarioCommand.CanExecute(linha)) await vm.AbrirHorarioCommand.ExecuteAsync(linha);
    }
}
