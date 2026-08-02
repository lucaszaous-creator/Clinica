using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>
/// Tela de PRONTUÁRIO: escolhe o paciente à esquerda, trabalha as sessões dele à direita.
///
/// Ela existe porque o prontuário é um item de primeiro nível da proposta comercial e no
/// sistema só se chegava nele por dentro da ficha do paciente — não havia entrada de menu
/// em lugar nenhum. Quem abria o app procurando "Prontuário" não achava, e concluía que
/// não tinha sido entregue.
///
/// Não substitui a ficha: lá o prontuário é uma seção entre outras (cadastro,
/// elegibilidade, LGPD, guias), para quem está com o paciente na frente. Aqui é a tela
/// inteira, para quem vai escrever a evolução do dia. As duas listam a MESMA
/// <see cref="LinhaEvolucao"/>, pela mesma fábrica, para nunca descreverem a mesma sessão
/// de dois jeitos.
/// </summary>
public sealed partial class ProntuarioViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public SeletorPacienteViewModel Seletor { get; }

    public ObservableCollection<LinhaEvolucao> Sessoes { get; } = [];

    /// <summary>
    /// Busca por texto dentro do prontuário (parcela 28).
    ///
    /// O prontuário de um paciente de tratamento longo passa de cinquenta sessões, e a
    /// pergunta que o profissional faz antes de atender é sempre a mesma: "o que eu fiz
    /// da última vez que ele veio com dor no ombro?". Sem busca, a resposta era rolar a
    /// lista inteira — e na prática ninguém rolava.
    /// </summary>
    [ObservableProperty] private string _termoSessao = string.Empty;

    /// <summary>O que a lista está mostrando. Filtro invisível é filtro que engana.</summary>
    [ObservableProperty] private string _resumoSessoes = string.Empty;

    [ObservableProperty] private bool _carregando;

    /// <summary>
    /// A leitura FALHOU — o terceiro estado. Sem ele, lista vazia por erro fica idêntica
    /// a lista vazia por não haver nada.
    /// </summary>
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Nenhum paciente escolhido — a direita mostra o convite, não uma lista vazia.</summary>
    [ObservableProperty] private bool _semPaciente = true;

    [ObservableProperty] private string _paciente = string.Empty;

    // ===== Evolução da dor (EVA) =====
    [ObservableProperty] private string _dorInicial = "—";
    [ObservableProperty] private string _dorAtual = "—";
    [ObservableProperty] private string _ganhoAcumulado = "—";
    [ObservableProperty] private string _alivioMedio = "—";
    [ObservableProperty] private string _resumoEva = string.Empty;

    /// <summary>
    /// Últimos escores das escalas aplicadas pelo Consultório (parcela 36).
    ///
    /// Elas são gravadas noutro módulo, e sem esta linha só seriam lidas lá — enquanto a
    /// recepção, que atende o telefone do paciente e monta o relatório para o convênio,
    /// veria um prontuário que não menciona a metade medida do tratamento. Vazio quando
    /// nenhuma escala foi aplicada: a linha some em vez de dizer "nenhuma", que ocuparia
    /// espaço para informar ausência.
    /// </summary>
    [ObservableProperty] private string _resumoEscalas = string.Empty;

    /// <summary>
    /// Metade VISÍVEL da permissão: quem não pode escrever vê os botões apagados com o
    /// motivo. A que impede é o <c>Exigir</c> dentro do comando.
    /// </summary>
    public bool PodeEditarProntuario => SessaoUsuario.Atual.Pode(Permissao.EditarProntuario);

    public ProntuarioViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;

        // O seletor da suíte, com o corte no SQL e o descarte de resposta fora de ordem
        // já resolvidos. Tela nova que escolhe paciente usa ele — não se reescreve busca.
        Seletor = new SeletorPacienteViewModel(escopos);
        Seletor.SelecaoMudou += AoTrocarPaciente;
    }

    private void AoTrocarPaciente(Paciente? paciente)
    {
        // Trocar de paciente limpa a busca: "ombro" digitado para um não é pergunta sobre
        // o próximo, e a lista voltaria filtrada sem ninguém entender por quê.
        TermoSessao = string.Empty;
        _ = CarregarAsync();
    }

    partial void OnTermoSessaoChanged(string value) => _ = CarregarAsync();

    /// <summary>
    /// A sessão contém o termo em algum dos campos escritos. Sem acento e sem caixa: quem
    /// digita no balcão escreve "lombalgia" e o prontuário diz "Lombalgia", e uma busca
    /// que erra por isso é uma busca que ninguém usa duas vezes.
    /// </summary>
    private static bool Casa(Evolucao e, string termo)
    {
        var alvo = Normalizar(string.Join(" ",
            e.QueixaPrincipal, e.Conduta, e.TextoEvolucao, e.Orientacoes));
        return alvo.Contains(Normalizar(termo), StringComparison.Ordinal);
    }

    private static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        var decomposto = texto.Normalize(System.Text.NormalizationForm.FormD);
        var limpo = new System.Text.StringBuilder(decomposto.Length);

        foreach (var c in decomposto)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
                limpo.Append(char.ToLowerInvariant(c));

        return limpo.ToString();
    }

    private int PacienteId => Seletor.Selecionado?.Id ?? 0;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        SemPaciente = PacienteId == 0;
        Sessoes.Clear();

        if (SemPaciente)
        {
            Paciente = string.Empty;
            return;
        }

        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = null;
            MensagemEhErro = false;
            Paciente = Seletor.Selecionado?.Nome ?? string.Empty;

            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();

            var todas = await prontuario.DoPacienteAsync(PacienteId);
            var termo = TermoSessao.Trim();

            foreach (var e in todas)
            {
                if (termo.Length > 0 && !Casa(e, termo)) continue;
                var anexos = await prontuario.AnexosAsync(e.Id);
                Sessoes.Add(LinhaEvolucao.De(e, anexos.Count));
            }

            ResumoSessoes = termo.Length == 0
                ? $"{todas.Count} sessão(ões) no prontuário."
                // A lista filtrada DIZ que está filtrada, e diz sobre quantas: "3 sessões"
                // sozinho faria o profissional concluir que o paciente veio três vezes.
                : $"{Sessoes.Count} de {todas.Count} sessão(ões) contêm “{termo}”.";

            // A evolução da dor é sempre do prontuário INTEIRO, nunca do filtro: ela mede
            // o tratamento, e medir só as sessões que citam uma palavra daria um gráfico
            // que não corresponde a tratamento nenhum.
            AplicarEvolucaoDaDor(await prontuario.EvolucaoDaDorAsync(PacienteId));

            var avaliacoes = scope.ServiceProvider.GetRequiredService<AvaliacaoClinicaService>();
            AplicarEscalas(await avaliacoes.DoPacienteAsync(PacienteId));
        }
        catch (Exception ex)
        {
            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar("Recepção — prontuário não pôde ser carregado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    /// <summary>
    /// A EVA vale em PAR: medir só antes (ou só depois) não diz se o tratamento
    /// funcionou. Sem par, a tela mostra "—" e diz quantas sessões têm a medida — 0 e
    /// "não medido" são coisas diferentes.
    /// </summary>
    private void AplicarEvolucaoDaDor(EvolucaoDaDor dor)
    {
        if (dor.SessoesComMedida == 0)
        {
            DorInicial = DorAtual = GanhoAcumulado = AlivioMedio = "—";
            ResumoEva = dor.SessoesRegistradas == 0
                ? "Nenhuma sessão registrada no prontuário ainda."
                : $"{dor.SessoesRegistradas} sessão(ões) registradas, nenhuma com o par EVA "
                  + "(antes e depois). Sem o par não dá para dizer se a dor melhorou.";
            return;
        }

        DorInicial = $"{dor.DorInicial}/10";
        DorAtual = $"{dor.DorAtual}/10";
        GanhoAcumulado = dor.GanhoAcumulado is { } ganho
            ? (ganho >= 0 ? $"−{ganho} pontos" : $"+{-ganho} pontos")
            : "—";
        AlivioMedio = $"{dor.AlivioMedioPorSessao:0.#} por sessão";
        ResumoEva = $"{dor.SessoesComMedida} de {dor.SessoesRegistradas} sessão(ões) com EVA medida.";
    }

    /// <summary>
    /// O ÚLTIMO escore de cada instrumento — não o histórico inteiro. A curva mora no
    /// Consultório, que é de quem aplica; aqui a pergunta é "o que já foi medido nesta
    /// pessoa?", e ela se responde com uma linha.
    /// </summary>
    private void AplicarEscalas(IReadOnlyList<AvaliacaoClinica> avaliacoes)
    {
        if (avaliacoes.Count == 0)
        {
            ResumoEscalas = string.Empty;
            return;
        }

        // A lista vem da mais recente para a mais antiga, então a primeira de cada
        // instrumento já é a última aplicação dele.
        var ultimas = avaliacoes
            .GroupBy(a => a.InstrumentoCodigo)
            .Select(g => g.First())
            .OrderByDescending(a => a.Data)
            .ToList();

        ResumoEscalas = "Escalas aplicadas: " + string.Join(" · ", ultimas.Select(a =>
            $"{a.InstrumentoNome} — {a.PontuacaoFormatada} em {a.Data:dd/MM/yyyy}"
            + (string.IsNullOrWhiteSpace(a.FaixaNome) ? string.Empty : $" ({a.FaixaNome})")));
    }

    [RelayCommand]
    private Task NovaSessaoAsync() => AbrirAsync(null);

    [RelayCommand]
    private Task EditarSessaoAsync(LinhaEvolucao? linha)
        => linha is null ? Task.CompletedTask : AbrirAsync(linha.EvolucaoId);

    private async Task AbrirAsync(int? evolucaoId)
    {
        if (PacienteId == 0) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            var vm = new EvolucaoEdicaoViewModel(_escopos, PacienteId, evolucaoId);
            var janela = new Janelas.EvolucaoWindow(vm)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };

            if (janela.ShowDialog() != true) return;

            _snackbar.Sucesso("Prontuário atualizado.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — sessão do prontuário não pôde ser aberta", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Prontuário é documento clínico: apagar deixa rastro na auditoria e leva os anexos
    /// junto. Por isso a confirmação diz as duas coisas antes.
    /// </summary>
    [RelayCommand]
    private async Task ExcluirSessaoAsync(LinhaEvolucao? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            if (!_dialogo.ConfirmarPerigo("Excluir do prontuário",
                    $"Apagar a sessão de {linha.Data}? Os anexos dela vão junto, e o prontuário "
                    + "é documento clínico — a exclusão fica registrada na auditoria.")) return;

            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();
            await prontuario.ExcluirAsync(linha.EvolucaoId, SessaoUsuario.Atual.Operador);

            _snackbar.Info("Sessão excluída do prontuário.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — evolução não pôde ser excluída", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }
}
