using System.Collections.ObjectModel;
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
/// Tela de PRESCRIÇÕES: escolhe o paciente e emite, reimprime ou cancela os documentos
/// clínicos dele (receita, atestado, comparecimento, pedido de exame, relatório de
/// evolução, termo de consentimento, anamnese).
///
/// Mesmo motivo da tela de Prontuário: "Prescrições" é item de primeiro nível da proposta
/// e no sistema só existia por dentro da ficha do paciente, sem entrada de menu.
///
/// As três regras do documento clínico continuam valendo aqui, porque moram no serviço e
/// não na tela: documento emitido é FATO (não se apaga, cancela-se com motivo e emite-se
/// outro); o conteúdo fica gravado na emissão e a segunda via sai idêntica à primeira; e
/// o CID só é impresso com autorização expressa do paciente.
/// </summary>
public sealed partial class PrescricoesViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public SeletorPacienteViewModel Seletor { get; }

    public ObservableCollection<LinhaDocumento> Documentos { get; } = [];

    [ObservableProperty] private bool _carregando;

    /// <summary>
    /// A leitura FALHOU — o terceiro estado. Sem ele, lista vazia por erro fica idêntica
    /// a lista vazia por não haver nada.
    /// </summary>
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Nenhum paciente escolhido — a direita convida a escolher um.</summary>
    [ObservableProperty] private bool _semPaciente = true;

    [ObservableProperty] private string _paciente = string.Empty;

    /// <summary>Metade visível da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeEditarProntuario => SessaoUsuario.Atual.Pode(Permissao.EditarProntuario);

    public PrescricoesViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;

        Seletor = new SeletorPacienteViewModel(escopos);
        Seletor.SelecaoMudou += AoTrocarPaciente;
    }

    private void AoTrocarPaciente(Paciente? paciente) => _ = CarregarAsync();

    private int PacienteId => Seletor.Selecionado?.Id ?? 0;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        SemPaciente = PacienteId == 0;
        Documentos.Clear();

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
            var servico = scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>();

            foreach (var d in await servico.DoPacienteAsync(PacienteId))
                Documentos.Add(LinhaDocumento.De(d));
        }
        catch (Exception ex)
        {
            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar("Recepção — documentos não puderam ser carregados", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    [RelayCommand]
    private async Task NovoDocumentoAsync()
    {
        if (PacienteId == 0) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            var vm = new DocumentoEdicaoViewModel(_escopos, PacienteId);
            var janela = new Clinica.Desktop.Shell.Componentes.DocumentoWindow(vm)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };

            var concluiu = janela.ShowDialog() == true;

            // Recarrega dos dois jeitos: fechar sem concluir não significa que nada
            // aconteceu — o documento pode ter sido emitido e só a impressão ter falhado.
            await CarregarAsync();

            if (concluiu) _snackbar.Sucesso("Documento emitido.");
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — documento não pôde ser emitido", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>Segunda via: reimprime exatamente o que foi emitido, não o que o prontuário diz hoje.</summary>
    [RelayCommand]
    private async Task ImprimirDocumentoAsync(LinhaDocumento? linha)
    {
        if (linha is null) return;

        try
        {
            byte[] pdf;
            using (var scope = _escopos.CreateScope())
            {
                var pdfs = scope.ServiceProvider.GetRequiredService<DocumentosClinicosPdfService>();
                var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();
                pdf = await pdfs.GerarAsync(linha.DocumentoId, await parametros.ObterPrestadorAsync());
            }

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                pdf, ImpressaoPdf.NomeSeguro(linha.NomeArquivo));

            Mensagem = erro;
            MensagemEhErro = erro is not null;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — segunda via não pôde ser gerada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Cancela com motivo. A linha continua na lista marcada como cancelada: a via em
    /// papel não desaparece por ser apagada do sistema.
    /// </summary>
    [RelayCommand]
    private async Task CancelarDocumentoAsync(LinhaDocumento? linha)
    {
        if (linha is null || linha.Cancelado) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            var motivo = _dialogo.PerguntarTexto(
                "Cancelar documento",
                $"Por que o(a) {linha.Tipo.ToLowerInvariant()} {linha.Numero} está sendo cancelado? "
                + "Ele continua na lista, marcado como cancelado — a via impressa não desaparece "
                + "por ser apagada do sistema.");
            if (string.IsNullOrWhiteSpace(motivo)) return;

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>();
            await servico.CancelarAsync(linha.DocumentoId, motivo, SessaoUsuario.Atual.Operador);

            _snackbar.Info("Documento cancelado.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — documento clínico não pôde ser cancelado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }
}
