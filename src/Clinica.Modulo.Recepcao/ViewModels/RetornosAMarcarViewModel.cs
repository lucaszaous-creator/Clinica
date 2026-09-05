using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Configuracao;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>
/// A fila "Retornos a marcar" (set/2026): quem saiu do atendimento com pedido de retorno
/// e ainda não tem horário marcado.
///
/// O médico grava a data sugerida na sessão desde a parcela 77, e a única leitura fora do
/// Consultório era a janela de edição da evolução — a recepcionista não tinha como saber
/// que alguém devia voltar. A frase da própria cliente ("se o paciente precisar voltar, a
/// recepcionista marca uma agenda") pressupõe que ela SAIBA; esta tela é o saber.
///
/// O que ela faz: lista, ordena pelos mais atrasados e leva ao Marcar já preenchido (quem,
/// para quando, com quem). O que ela nunca faz: marcar sozinha ou virar agendamento — a
/// regra da parcela 58 (pendência não vira horário de quem atende) continua de pé, e a
/// linha some quando o horário passa a existir.
///
/// A leitura é sob <c>VerAgenda</c>; o MOTIVO do retorno é registro clínico e só aparece
/// para quem tem <c>VerProntuario</c> — a data e quem pediu são o suficiente para marcar.
/// </summary>
public sealed partial class RetornosAMarcarViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private int _geracao;

    public RetornosAMarcarViewModel(IServiceScopeFactory scopeFactory)
        => _scopeFactory = scopeFactory;

    public ObservableCollection<RetornoAMarcar> Linhas { get; } = [];

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemMensagem))]
    private string? _mensagem;

    [ObservableProperty] private bool _mensagemEhErro;

    [ObservableProperty] private string _resumo = string.Empty;

    public bool TemMensagem => !string.IsNullOrWhiteSpace(Mensagem);

    /// <summary>A metade visível da barreira: marcar exige <c>EditarAgenda</c>.</summary>
    public bool PodeMarcar => SessaoUsuario.Atual.Pode(Permissao.EditarAgenda);

    /// <summary>O motivo do retorno é dado de saúde: só quem lê o prontuário o vê.</summary>
    public bool PodeVerNota => SessaoUsuario.Atual.Pode(Permissao.VerProntuario);

    public int JanelaDias => RetornosAMarcar.JanelaDiasParaTras;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracao;
        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = null;

            using var scope = _scopeFactory.CreateScope();
            var retornos = await scope.ServiceProvider
                .GetRequiredService<RetornosAMarcarService>()
                .ListarAsync();
            if (geracao != _geracao) return;

            // O motivo sai da LINHA para quem não pode lê-lo — não só da tela. Escondê-lo
            // no XAML deixaria o texto na memória de uma tela que não tem o direito de tê-lo.
            var linhas = PodeVerNota
                ? retornos
                : retornos.Select(r => r with { Nota = null }).ToList();

            // Monta em lista local e só ENTÃO publica: entre o Clear e o último Add não
            // pode haver await (parcela 62).
            Linhas.Clear();
            foreach (var l in linhas) Linhas.Add(l);

            var atrasados = linhas.Count(l => l.Atrasado);
            Resumo = linhas.Count == 0
                ? "Ninguém com retorno pedido sem horário marcado."
                : $"{linhas.Count} retorno(s) a marcar"
                  + (atrasados > 0 ? $" · {atrasados} com a data sugerida já passada" : string.Empty);
        }
        catch (Exception ex)
        {
            if (geracao != _geracao) return;
            NaoVerificado = true;
            LogSuite.Registrar("Retornos a marcar — a fila não pôde ser lida", ex);
            Avisar($"Não foi possível ler os retornos: {ex.Message}", erro: true);
        }
        finally
        {
            if (geracao == _geracao) Carregando = false;
        }
    }

    /// <summary>
    /// Leva ao Marcar com quem, para quando e com quem já preenchidos. A data é a sugerida
    /// — ou HOJE, quando a sugerida já passou: pré-preencher uma data no passado faria o
    /// formulário abrir recusando ("marcar para trás").
    /// </summary>
    [RelayCommand]
    private void Marcar(RetornoAMarcar? linha)
    {
        if (linha is null) return;
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "marcar o retorno");

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var dia = linha.RetornoEm < hoje ? hoje : linha.RetornoEm;

            using var scope = _scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<PreenchimentoNovoAtendimento>()
                .Definir(new PedidoNovoAtendimento(
                    MarcarParaDepois: true,
                    DataHora: dia.ToDateTime(TimeOnly.MinValue),
                    ProfissionalId: linha.ProfissionalId,
                    SalaId: null,
                    PacienteId: linha.PacienteId));

            if (!NavegacaoSuite.Ir(Modulo.ModuloRecepcao.ChaveMarcarHorario))
                Avisar("A tela de marcar horário não está disponível para este usuário.", erro: true);
        }
        catch (Exception ex)
        {
            Avisar(ex.Message, erro: true);
        }
    }

    /// <summary>
    /// O convite pelo WhatsApp de um clique. É LEITURA da agenda (não grava nada), então a
    /// barreira é a mesma do item — as duas metades concordam (parcela 86).
    /// </summary>
    [RelayCommand]
    private void WhatsApp(RetornoAMarcar? linha)
    {
        if (linha is null) return;
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.VerAgenda, "convidar o paciente pelo WhatsApp");
            var erro = Whatsapp.Abrir(
                linha.Telefone, linha.Paciente,
                Whatsapp.ConviteDeRetorno(linha.Paciente, linha.RetornoEm, linha.Profissional));
            if (erro is not null) Avisar(erro, erro: true);
        }
        catch (Exception ex)
        {
            Avisar(ex.Message, erro: true);
        }
    }

    private void Avisar(string texto, bool erro)
    {
        Mensagem = texto;
        MensagemEhErro = erro;
    }
}
