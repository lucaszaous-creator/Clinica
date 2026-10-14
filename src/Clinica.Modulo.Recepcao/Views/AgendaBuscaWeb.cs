using System.Text.Json;
using Clinica.Application.Modelos;
using Clinica.Desktop.Shell.WebClinica;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Views;

internal sealed class AgendaBuscaWeb(AgendaViewModel vm) : IDisposable
{
    private readonly PedidosEnfermagemWeb _pedidos = new(vm);
    public void Iniciar() => _pedidos.Iniciar();
    public void Parar() => _pedidos.Parar();
    public void Dispose() => _pedidos.Dispose();
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
        contexto = Contexto, carregando = vm.Carregando, naoVerificado = vm.NaoVerificado, enfermagem = _pedidos.Resumo,
        linhas = Linhas.OrderBy(l => l.DataHora).Select(l => new LinhaAgendaWeb(l.AgendamentoId.ToString(),
            l.DataHora.ToString("dd/MM/yyyy"), l.Faixa, l.Paciente, l.Modalidade, l.Profissional, l.Sala,
            StatusDaFila.Palavra(l.Situacao, l.Etapa), SituacaoVisualAgenda.Grupo(l.Situacao, l.Etapa),
            l.EhEncaixe ? "Encaixe" : "", "", l.Observacoes ?? "", "Ver horário", true)).ToArray()
    };
    }
    public async Task Executar(JsonElement m)
    {
        if (m.TryGetProperty("acao", out var acao) && acao.GetString() == "pedidos-enfermagem")
        {
            if (m.TryGetProperty("contexto", out var contexto) && contexto.GetString() == Contexto) await _pedidos.Abrir();
            return;
        }
        if (_filtro.Receber(m, Contexto)) Filtrar();
    }
}
