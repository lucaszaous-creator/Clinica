using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clinica.Financeiro.ViewModels;

/// <summary>Uma sessão debitada do pacote, como a lista a mostra.</summary>
public sealed class LinhaConsumo
{
    public required int ConsumoId { get; init; }
    public required string Data { get; init; }
    public required string Origem { get; init; }
    public required bool Cancelado { get; init; }
    public required string Situacao { get; init; }

    /// <summary>
    /// Devolver duas vezes não existe, e sem permissão também não. É a metade visível da
    /// regra; a que impede é o <c>Exigir</c> no comando — e, no fim, o próprio serviço,
    /// que recusa cancelar consumo já cancelado.
    /// </summary>
    public bool PodeDevolver => !Cancelado && SessaoUsuario.Atual.Pode(Permissao.EditarFinanceiro);
}

/// <summary>
/// As sessões que o pacote já consumiu — e a porta para devolver uma delas ao saldo.
///
/// `PacoteService.CancelarConsumoAsync` existia desde a parcela 4, testado e **sem
/// chamador nenhum**: a tela de Pacotes só sabia cancelar o pacote INTEIRO. Na clínica,
/// a sessão debitada por engano (o paciente não veio, a baixa automática pegou o pacote
/// errado) só se resolvia cancelando tudo e revendendo — ou não se resolvia.
///
/// Devolver é CANCELAR COM MOTIVO, nunca apagar: o consumo continua no histórico,
/// marcado, porque "o paciente diz que sobrou uma sessão" é justamente a conversa que
/// este módulo existe para encerrar. Apagar a linha devolveria a discussão para a
/// palavra de um contra a do outro.
/// </summary>
public sealed partial class ConsumosPacoteViewModel : ObservableObject
{
    private readonly PacoteService _pacotes;
    private readonly IDialogoService _dialogo;
    private readonly int _pacoteId;

    public ObservableCollection<LinhaConsumo> Consumos { get; } = [];

    public string Titulo { get; }

    [ObservableProperty] private string _saldo = "—";
    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Metade visível da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeEditarFinanceiro => SessaoUsuario.Atual.Pode(Permissao.EditarFinanceiro);

    /// <summary>Avisa a tela de Pacotes que o saldo mudou e ela precisa recarregar.</summary>
    public bool Mudou { get; private set; }

    public ConsumosPacoteViewModel(
        PacoteService pacotes, IDialogoService dialogo, int pacoteId, string titulo)
    {
        _pacotes = pacotes;
        _dialogo = dialogo;
        _pacoteId = pacoteId;
        Titulo = titulo;
    }

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Mensagem = string.Empty;
            MensagemEhErro = false;

            var pacote = await _pacotes.ObterAsync(_pacoteId);
            if (pacote is null)
            {
                Erro("Pacote não encontrado.");
                return;
            }

            var hoje = DateOnly.FromDateTime(DateTime.Today);

            Consumos.Clear();
            foreach (var c in pacote.Consumos.OrderByDescending(c => c.Data).ThenByDescending(c => c.Id))
                Consumos.Add(new LinhaConsumo
                {
                    ConsumoId = c.Id,
                    Data = c.Data.ToString("dd/MM/yyyy"),
                    Origem = DescreverOrigem(c),
                    Cancelado = c.Cancelado,
                    Situacao = c.Cancelado
                        ? $"devolvida ao saldo — {c.MotivoCancelamento}"
                        : "usada"
                });

            var usadas = pacote.Consumos.Count(c => !c.Cancelado);
            var devolvidas = pacote.Consumos.Count(c => c.Cancelado);

            Saldo = pacote.SessoesContratadas is { } contratadas
                ? $"{Math.Max(contratadas - usadas, 0)} de {contratadas} sessões"
                : pacote.ValidoAte is { } ate
                    ? $"livre até {ate:dd/MM/yyyy}"
                    : "livre, sem prazo";

            Resumo = pacote.Consumos.Count == 0
                ? "Este pacote ainda não debitou nenhuma sessão."
                : $"{usadas} sessão(ões) usada(s)"
                  + (devolvidas > 0 ? $" · {devolvidas} devolvida(s) ao saldo" : string.Empty)
                  + $" · situação hoje: {pacote.Situacao(hoje)}.";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Financeiro — sessões do pacote não puderam ser lidas", ex);
            Erro($"Não foi possível ler as sessões deste pacote: {ex.Message}");
        }
    }

    /// <summary>
    /// A baixa automática grava de onde veio; a manual, quem a digitou. Sem isso a lista
    /// seria uma coluna de datas iguais, e quem confere não teria como achar a errada.
    /// </summary>
    private static string DescreverOrigem(ConsumoPacote c)
    {
        var partes = new List<string>();

        if (c.AtendimentoId is { } atendimento) partes.Add($"atendimento #{atendimento}");
        if (!string.IsNullOrWhiteSpace(c.Observacao)) partes.Add(c.Observacao!);
        if (!string.IsNullOrWhiteSpace(c.CriadoPor)) partes.Add($"por {c.CriadoPor}");

        return partes.Count == 0 ? "—" : string.Join(" · ", partes);
    }

    [RelayCommand]
    private async Task DevolverAsync(LinhaConsumo? linha)
    {
        if (linha is null || linha.Cancelado) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "devolver sessão ao pacote");

            var motivo = _dialogo.PerguntarTexto(
                "Devolver a sessão",
                $"Por que a sessão de {linha.Data} está voltando ao saldo? "
                + "O consumo continua no histórico, marcado com o motivo — não some.");
            if (string.IsNullOrWhiteSpace(motivo)) return;

            await _pacotes.CancelarConsumoAsync(linha.ConsumoId, motivo, SessaoUsuario.Atual.Operador);
            Mudou = true;

            Mensagem = $"Sessão de {linha.Data} devolvida ao saldo.";
            MensagemEhErro = false;
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Financeiro — sessão do pacote não pôde ser devolvida", ex);
            Erro(ex.Message);
        }
    }

    private void Erro(string texto)
    {
        Mensagem = texto;
        MensagemEhErro = true;
    }
}
