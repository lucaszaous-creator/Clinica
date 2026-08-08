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

    /// <summary>
    /// Emitir exige paciente escolhido, além da permissão. Sem isto o botão ficava aceso
    /// numa tela sem ninguém selecionado e o clique não fazia nada — o comando voltava
    /// calado no `if (PacienteId == 0)`.
    /// </summary>
    public bool PodeEmitirDocumento => !SemPaciente && PodeEditarProntuario;

    partial void OnSemPacienteChanged(bool value) => OnPropertyChanged(nameof(PodeEmitirDocumento));

    public PrescricoesViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;

        Seletor = new SeletorPacienteViewModel(escopos);
        Seletor.SelecaoMudou += AoTrocarPaciente;

        // A busca INICIAL — ver o comentário em `ProntuarioViewModel`. As duas telas
        // nasceram com a lista de pacientes em branco esperando alguém digitar.
        _ = Seletor.BuscarAsync(imediato: true);
    }

    /// <summary>
    /// LISTA → TELA DO ITEM (parcela 48). Era uma coluna de 340 px com a lista grudada à
    /// esquerda para sempre — o padrão que o `README.md` proíbe. Mesmo desenho de
    /// `PacientesViewModel` e `ProntuarioViewModel`.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrandoLista))]
    private bool _mostrandoDocumentos;

    /// <summary>Estamos na lista? O par existe porque XAML não nega booleano sem conversor.</summary>
    public bool MostrandoLista => !MostrandoDocumentos;

    private void AoTrocarPaciente(Paciente? paciente)
    {
        if (paciente is null) return;

        MostrandoDocumentos = true;
        _ = CarregarAsync();
    }

    /// <summary>
    /// Volta para a lista. LIMPA a seleção: sem isso, clicar de novo no mesmo paciente não
    /// dispararia <c>SelecaoMudou</c> e a tela não reabriria.
    /// </summary>
    [RelayCommand]
    private void Voltar()
    {
        MostrandoDocumentos = false;
        Seletor.Limpar();
    }

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
        if (PacienteId == 0)
        {
            Mensagem = "Escolha um paciente antes de emitir o documento.";
            MensagemEhErro = true;
            return;
        }

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
    /// Assina um documento já emitido com certificado ICP-Brasil (parcela 43).
    ///
    /// A mesma porta existe no Consultório, e as duas chamam o mesmo serviço: capacidade
    /// que só existe num dos módulos é o defeito recorrente do projeto — aqui com o
    /// agravante de a assinatura ser justamente o que dá valor jurídico ao arquivo.
    /// </summary>
    [RelayCommand]
    private async Task AssinarDocumentoAsync(LinhaDocumento? linha)
    {
        if (linha is null) return;

        // Guarda de ESTADO: diz por que não dá, em vez de voltar calada.
        if (!linha.PodeAssinar)
        {
            Mensagem = linha.Cancelado
                ? $"O documento {linha.Numero} está cancelado e não pode ser assinado."
                : $"O documento {linha.Numero} já foi assinado digitalmente.";
            MensagemEhErro = true;
            return;
        }

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "assinar documento clínico");

            var certificado = EscolherCertificadoWindow.Perguntar(
                $"Assinar {linha.Tipo.ToLowerInvariant()} {linha.Numero}",
                System.Windows.Application.Current?.MainWindow, _escopos);

            if (certificado is null) return;   // diálogo cancelado: sair calado é o certo

            DocumentoAssinado assinado;
            using (var scope = _escopos.CreateScope())
            {
                var assinaturas = scope.ServiceProvider
                    .GetRequiredService<AssinaturaDeDocumentoClinicoService>();

                assinado = await assinaturas.AssinarAsync(
                    linha.DocumentoId, certificado,
                    SessaoUsuario.Atual.Autenticado ? SessaoUsuario.Atual.UsuarioId : null,
                    SessaoUsuario.Atual.Operador);
            }

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                assinado.Pdf, ImpressaoPdf.NomeSeguro(assinado.NomeArquivo));

            if (erro is not null)
            {
                Mensagem = $"{erro} O documento foi assinado e está guardado no sistema.";
                MensagemEhErro = true;
            }
            else
            {
                _snackbar.Sucesso("Documento assinado. Entregue o ARQUIVO ao paciente.");
                Mensagem = null;
                MensagemEhErro = false;
            }

            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — documento não pôde ser assinado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Entrega o ARQUIVO assinado ao paciente pelo WhatsApp (parcela 43, 2ª rodada).
    ///
    /// A assinatura vive nos bytes: quem sai só com o papel leva um documento sem a
    /// garantia que o sistema produziu, e a farmácia recusa — com razão. Ver
    /// <see cref="EntregaAoPaciente"/>, inclusive por que o anexo não é automático.
    /// </summary>
    [RelayCommand]
    private async Task EnviarDocumentoAsync(LinhaDocumento? linha)
    {
        if (linha is null) return;

        if (!linha.PodeEnviar)
        {
            Mensagem = linha.Cancelado
                ? $"O documento {linha.Numero} está cancelado."
                : $"O documento {linha.Numero} ainda não foi assinado digitalmente. "
                  + "Sem assinatura, o que vale é a via impressa e assinada à caneta — "
                  + "assine antes de enviar o arquivo.";
            MensagemEhErro = true;
            return;
        }

        try
        {
            byte[] pdf;
            Paciente? paciente;
            string? nomeClinica;

            using (var scope = _escopos.CreateScope())
            {
                var pdfs = scope.ServiceProvider.GetRequiredService<DocumentosClinicosPdfService>();
                var pacientes = scope.ServiceProvider.GetRequiredService<PacienteService>();
                var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();

                // Bytes GUARDADOS: o documento está assinado, e regerar produziria um
                // arquivo que abre como inválido no leitor de quem confere.
                pdf = await pdfs.GerarAsync(linha.DocumentoId);
                paciente = await pacientes.ObterComHistoricoAsync(linha.PacienteId);

                var prestador = await parametros.ObterPrestadorAsync();
                nomeClinica = prestador.NomeFantasia ?? prestador.RazaoSocial;
            }

            var entrega = EntregaAoPaciente.Entregar(
                pdf, linha.NomeArquivoAssinado, paciente?.Telefone,
                paciente?.Nome ?? "paciente", linha.Tipo, nomeClinica);

            Mensagem = entrega.Frase;
            MensagemEhErro = entrega.EhErro;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — documento não pôde ser entregue ao paciente", ex);
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
