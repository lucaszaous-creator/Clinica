using System.Text.Json;
using Clinica.Application.Modelos;
using Clinica.Clinico.ViewModels;
using Clinica.Desktop.Shell.WebClinica;
using Clinica.Domain.Entities;

namespace Clinica.Clinico.Views;

internal static class AgendaMedicoWeb
{
    public static object Dia(MeuDiaViewModel vm) => new
    {
        contexto = vm.Dia.ToString("yyyy-MM-dd"), carregando = vm.Carregando, naoVerificado = vm.NaoVerificado,
        linhas = vm.Sessoes.Select(l => new LinhaAgendaWeb(l.AgendamentoId.ToString(), l.Data.ToString("dd/MM/yyyy"),
            l.Hora, l.Paciente, l.Modalidade, "", l.Local, l.Status, SituacaoVisualAgenda.Grupo(l.Estado, l.Etapa),
            l.StatusDetalhe, l.Prontuario, l.Observacoes, l.PodeAtender ? l.RotuloRegistro : "", vm.PodeVerProntuario)).ToArray()
    };
    public static async Task ExecutarDia(MeuDiaViewModel vm, JsonElement m)
    {
        if (vm.Carregando || vm.NaoVerificado || m.GetProperty("contexto").GetString() != vm.Dia.ToString("yyyy-MM-dd") || m.GetProperty("acao").GetString() != "abrir") return;
        var linha = vm.Sessoes.FirstOrDefault(l => l.AgendamentoId.ToString() == m.GetProperty("id").GetString());
        if (linha is not null && linha.PodeAtender && vm.PodeVerProntuario && vm.AtenderCommand.CanExecute(linha)) await vm.AtenderCommand.ExecuteAsync(linha);
    }
    private static IEnumerable<SessaoDoDia> Sessoes(MinhaSemanaViewModel vm) => vm.Faixas.SelectMany(f => f.Celulas).SelectMany(c => c.Sessoes).DistinctBy(s => s.AgendamentoId);
    public static object Semana(MinhaSemanaViewModel vm) => new
    {
        contexto = vm.Referencia.ToString("yyyy-MM-dd"), carregando = vm.Carregando, naoVerificado = vm.NaoVerificado,
        linhas = Sessoes(vm).OrderBy(s => s.DataHora).Select(s => new LinhaAgendaWeb(s.AgendamentoId.ToString(), s.DataHora.ToString("dd/MM/yyyy"),
            $"{s.DataHora:HH:mm}–{s.FimPrevisto:HH:mm}", s.PacienteNome, s.Modalidade, "", s.Sala ?? "",
            StatusDaFila.Palavra(s.Status, s.Etapa), SituacaoVisualAgenda.Grupo(s.Status, s.Etapa), "", "", s.Observacoes ?? "", "Abrir paciente",
            SessaoUsuario.Atual.Pode(Permissao.VerProntuario))).ToArray()
    };
    public static Task ExecutarSemana(MinhaSemanaViewModel vm, JsonElement m)
    {
        if (vm.Carregando || vm.NaoVerificado || m.GetProperty("contexto").GetString() != vm.Referencia.ToString("yyyy-MM-dd") || m.GetProperty("acao").GetString() != "abrir") return Task.CompletedTask;
        var linha = Sessoes(vm).FirstOrDefault(l => l.AgendamentoId.ToString() == m.GetProperty("id").GetString());
        if (linha is not null && vm.AbrirCommand.CanExecute(linha)) vm.AbrirCommand.Execute(linha);
        return Task.CompletedTask;
    }
}
