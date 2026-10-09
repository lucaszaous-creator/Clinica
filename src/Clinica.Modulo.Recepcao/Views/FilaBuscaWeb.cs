using System.Text.Json;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Clinica.Application.Modelos;
using Clinica.Desktop.Shell.WebClinica;
using Clinica.Domain.Entities;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Views;

/// <summary>A lista apresenta dados; as ações continuam nos comandos autorizados do balcão.</summary>
internal sealed class FilaBuscaWeb(FilaViewModel vm, Action<CartaoFila> abrirMenu)
{
    private bool PodeAvancar(CartaoFila l) => l.TemProximoPasso &&
        (l.ProximoPassoEhConcluir ? vm.PodeConcluirSessao : l.ProximoPassoEhFechar ? vm.PodeFecharSessao : vm.PodeEditarAgenda);

    public object Estado() => new
    {
        contexto = vm.Dia.ToString("yyyy-MM-dd"), carregando = vm.Carregando, naoVerificado = vm.NaoVerificado,
        linhas = vm.Linhas.Select(l => new
        {
            id = l.AgendamentoId.ToString(), data = l.DataHora.ToString("dd/MM/yyyy"), hora = l.Horario,
            paciente = l.Paciente, modalidade = l.Modalidade, profissional = l.Profissional, sala = l.Sala,
            situacao = l.Status, grupo = SituacaoVisualAgenda.Grupo(l.Situacao, l.Etapa), detalhe = l.StatusDetalhe,
            registro = "", observacoes = string.Join("\n", new[] { l.ContextoDaLista, l.Detalhe }), acao = l.ProximoPasso, comando = "avancar",
            habilitada = PodeAvancar(l) && vm.AvancarCommand.CanExecute(l),
            selos = l.Selos.Select(s => new { texto = s.Texto, tom = s.Tom.ToString().ToLowerInvariant() }).ToArray(),
            acoes = new[]
            {
                new { chave = "editarHorario", rotulo = "Editar", habilitada = l.EmAberto && vm.PodeEditarHorario && vm.EditarHorarioCommand.CanExecute(l) },
                new { chave = "ficha", rotulo = "Ficha", habilitada = vm.PodeVerFicha && vm.AbrirFichaCommand.CanExecute(l) },
                new { chave = "menu", rotulo = "Mais ações", habilitada = true }
            }.Where(a => a.chave != "editarHorario" || l.EmAberto).ToArray()
        }).ToArray()
    };

    public async Task Executar(JsonElement m)
    {
        SessaoUsuario.Atual.Exigir(Permissao.VerAgenda, "consultar a agenda");
        if (vm.Carregando || vm.NaoVerificado || m.GetProperty("contexto").GetString() != vm.Dia.ToString("yyyy-MM-dd")) return;
        var l = vm.Linhas.FirstOrDefault(l => l.AgendamentoId.ToString() == m.GetProperty("id").GetString());
        if (l is null) return;
        ICommand? comando = m.GetProperty("acao").GetString() switch
        {
            "avancar" when PodeAvancar(l) => vm.AvancarCommand,
            "editarHorario" when l.EmAberto && vm.PodeEditarHorario => vm.EditarHorarioCommand,
            "ficha" when vm.PodeVerFicha => vm.AbrirFichaCommand,
            _ => null
        };
        if (m.GetProperty("acao").GetString() == "menu") { abrirMenu(l); return; }
        if (comando?.CanExecute(l) != true) return;
        if (comando is IAsyncRelayCommand assincrono) await assincrono.ExecuteAsync(l);
        else comando.Execute(l);
    }
}
