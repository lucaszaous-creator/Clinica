using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>Um documento clínico já emitido, como o consultório o lê.</summary>
public sealed class LinhaDocumentoClinico
{
    public required int DocumentoId { get; init; }

    /// <summary>Dono do documento — a entrega precisa do telefone dele.</summary>
    public required int PacienteId { get; init; }
    public required string Numero { get; init; }
    public required string Tipo { get; init; }
    public required string Data { get; init; }
    public required string Profissional { get; init; }
    public required string Codigo { get; init; }
    public required bool Cancelado { get; init; }
    public required bool Assinado { get; init; }
    public required string Situacao { get; init; }

    public string NomeArquivo => $"{Tipo}-{Numero.Replace('/', '-')}.pdf";

    /// <summary>
    /// Nome do arquivo entregue ao paciente. O sufixo é o mesmo que o serviço de
    /// assinatura grava, para a pasta de entregas não ter duas versões do mesmo número
    /// com nomes diferentes.
    /// </summary>
    public string NomeArquivoAssinado => $"{Tipo}-{Numero.Replace('/', '-')}-assinado.pdf";

    /// <summary>Cancelar duas vezes não existe — o botão desliga depois do primeiro.</summary>
    public bool PodeCancelar => !Cancelado;

    /// <summary>
    /// Assinar depois é a porta para o documento emitido hoje de manhã, antes de o token
    /// estar na máquina. Assinado não se reassina (haveria dois arquivos válidos do mesmo
    /// ato) e cancelado não se assina.
    /// </summary>
    public bool PodeAssinar => !Cancelado && !Assinado;

    /// <summary>
    /// Só documento ASSINADO se entrega como arquivo: um PDF sem assinatura é algo que a
    /// farmácia não tem como conferir, e o paciente descobre no balcão. Sem assinatura, o
    /// que vale é a via impressa e assinada à caneta.
    /// </summary>
    public bool PodeEnviar => Assinado && !Cancelado;

    public static LinhaDocumentoClinico De(DocumentoClinico d) => new()
    {
        DocumentoId = d.Id,
        PacienteId = d.PacienteId,
        Numero = d.Numero,
        Tipo = TipoDocumentoInfo.Rotular(d.Tipo),
        Data = d.Data.ToString("dd/MM/yyyy"),
        Profissional = d.Profissional?.Rotulo ?? "—",
        Codigo = d.CodigoVerificacao,
        Cancelado = d.Cancelado,
        Assinado = d.AssinadoEletronicamente,
        Situacao = d.Cancelado
            ? $"Cancelado em {d.CanceladoEm:dd/MM/yyyy}"
            : d.AssinadoEletronicamente
                ? $"Assinado digitalmente em {d.AssinadoEm:dd/MM/yyyy}"
                : "Válido"
    };
}

/// <summary>
/// PRESCRIÇÕES no consultório (parcela 39) — receita, atestado, comparecimento e pedido
/// de exame, emitidos de onde eles nascem.
///
/// A lacuna que esta tela fecha
/// ----------------------------
/// O fluxo de emissão existia inteiro e a única porta para ele estava no módulo da
/// RECEPÇÃO. Quem prescreve, atesta e pede exame é quem ATENDE — e o app instalado na sala
/// do médico não tinha por onde. É a sétima ocorrência do defeito recorrente do projeto,
/// na variante mais discreta: não é dado sem leitor nem serviço sem chamador, é
/// <b>porta no módulo errado</b>. O CI ficava verde e a receita saía do bloquinho de
/// papel, ou o profissional descia até o balcão para pedir que alguém emitisse por ele.
///
/// Os componentes já estavam prontos e no lugar certo: a parcela 36 subiu a emissão para
/// <see cref="DocumentoWindow"/>, no shell, exatamente por isto. Esta tela não
/// reimplementa emissão nenhuma — ela abre a janela que já existe.
///
/// O paciente já vem escolhido
/// ---------------------------
/// A diferença para a tela da recepção não é estética. No consultório o paciente é
/// CONTEXTO, não parâmetro (ver <see cref="PacienteEmFoco"/>): quem acabou de atender já
/// escolheu a pessoa, e obrigá-lo a digitar o nome de novo para dar o atestado que ele
/// prometeu há trinta segundos é o tipo de atrito que faz a clínica voltar para o papel.
/// A busca continua aqui como atalho, para quem precisa emitir a segunda via de alguém
/// que não está na cadeira.
/// </summary>
public sealed partial class PrescricoesClinicasViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly PacienteEmFoco _foco;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public SeletorPacienteViewModel Seletor { get; }

    public ObservableCollection<LinhaDocumentoClinico> Documentos { get; } = [];

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Nenhum paciente escolhido — a tela pede um em vez de mostrar lista vazia.</summary>
    [ObservableProperty] private bool _semPaciente = true;

    [ObservableProperty] private string _paciente = string.Empty;

    /// <summary>Inverso de <see cref="SemPaciente"/>. Existe porque o projeto não tem
    /// conversor invertido e a alternativa seria um <c>DataTrigger</c> em cada uso.</summary>
    public bool TemPaciente => !SemPaciente;

    /// <summary>
    /// Os quatro botões de emitir só funcionam com paciente escolhido E com permissão de
    /// escrever no prontuário.
    ///
    /// É a metade VISÍVEL da regra, e ela faltava: a tela abre pela sidebar sem paciente
    /// nenhum em foco, os botões ficavam acesos e o clique não fazia NADA — o comando
    /// batia num `if (_pacienteId == 0) return;` e voltava calado. Botão aceso que não faz
    /// nada é pior do que botão apagado: quem clica conclui que o sistema quebrou.
    /// </summary>
    public bool PodeEmitirDocumento => TemPaciente && PodeEditarProntuario;

    partial void OnSemPacienteChanged(bool value)
    {
        OnPropertyChanged(nameof(TemPaciente));
        OnPropertyChanged(nameof(PodeEmitirDocumento));
    }

    private int _pacienteId;

    /// <summary>Metade visível da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeEditarProntuario => SessaoUsuario.Atual.Pode(Permissao.EditarProntuario);

    public PrescricoesClinicasViewModel(
        IServiceScopeFactory escopos, PacienteEmFoco foco,
        ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _foco = foco;
        _snackbar = snackbar;
        _dialogo = dialogo;

        Seletor = new SeletorPacienteViewModel(escopos);
        Seletor.SelecaoMudou += paciente =>
        {
            if (paciente is null) return;

            // Escolher alguém aqui TROCA o foco do posto: quem foi buscar outra pessoa
            // para emitir um documento vai continuar nela nas telas seguintes, e um foco
            // que só a metade das telas respeita é pior do que não ter foco nenhum.
            _foco.Definir(paciente.Id, paciente.Nome);
            _pacienteId = paciente.Id;
            Paciente = paciente.Nome;
            _ = CarregarAsync();
        };

        // Abre já no paciente do posto — este é o ponto da tela.
        if (_foco.PacienteId is { } id)
        {
            _pacienteId = id;
            Paciente = _foco.Nome;
        }

        _ = CarregarAsync();
    }

    [RelayCommand]
    public async Task CarregarAsync()
    {
        SemPaciente = _pacienteId == 0;
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

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>();

            foreach (var d in await servico.DoPacienteAsync(_pacienteId))
                Documentos.Add(LinhaDocumentoClinico.De(d));
        }
        catch (Exception ex)
        {
            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — documentos do paciente não puderam ser carregados", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    /// <summary>
    /// Emite já no tipo pedido.
    ///
    /// São quatro botões, e não um "Novo documento" que abre pedindo o tipo, porque no
    /// consultório a decisão vem ANTES do clique: ninguém pensa "vou emitir um documento",
    /// pensa "vou dar um atestado". O tipo inicial é o único parâmetro que a janela do
    /// shell precisa para isso — ela já aceitava, e ninguém aproveitava.
    /// </summary>
    [RelayCommand]
    private async Task EmitirAsync(string? tipo)
    {
        // Sem paciente, DIZ. O botão já nasce apagado (`PodeEmitirDocumento`), mas um
        // atalho de teclado ou um clique numa corrida de carregamento chegam aqui — e
        // guarda que volta em silêncio é exatamente o defeito que esta linha corrigiu.
        if (_pacienteId == 0)
        {
            Mensagem = "Escolha um paciente antes de emitir: a tela abre no paciente que "
                     + "você está atendendo, ou use a busca ao lado.";
            MensagemEhErro = true;
            return;
        }

        if (!Enum.TryParse<TipoDocumentoClinico>(tipo, out var tipoDocumento))
            tipoDocumento = TipoDocumentoClinico.Receita;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            var vm = new DocumentoEdicaoViewModel(_escopos, _pacienteId, tipoDocumento);
            var janela = new DocumentoWindow(vm)
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
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — documento não pôde ser emitido", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Segunda via: reimprime o que foi EMITIDO, não o que o prontuário diz hoje. É a
    /// regra do documento clínico, e ela mora no serviço — a via que o paciente levou e a
    /// que a clínica reimprime têm de ser a mesma folha.
    /// </summary>
    [RelayCommand]
    private async Task ImprimirAsync(LinhaDocumentoClinico? linha)
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
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — segunda via não pôde ser gerada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Assina um documento já emitido com o certificado ICP-Brasil (parcela 43).
    ///
    /// Existe porque a emissão e a assinatura nem sempre acontecem no mesmo minuto: o
    /// atestado sai às 9h e o token está na bolsa. Sem esta porta, a única saída seria
    /// cancelar e emitir outro — que é gastar um número de documento para resolver um
    /// problema de logística.
    ///
    /// O arquivo assinado é <b>salvo e aberto</b>, não impresso e pronto: é o ARQUIVO que
    /// vale, e imprimi-lo deixa a assinatura para trás.
    /// </summary>
    [RelayCommand]
    private async Task AssinarAsync(LinhaDocumentoClinico? linha)
    {
        if (linha is null) return;

        // Guarda de estado (e não de parâmetro): diz por que não dá, em vez de voltar
        // calada — o botão já está apagado, e quem chega aqui por atalho merece a frase.
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
                System.Windows.Application.Current?.MainWindow);

            // Diálogo cancelado: sair calado é o certo, e é a exceção prevista pela regra
            // do "botão que não faz nada".
            if (certificado is null) return;

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
                Mensagem = string.Empty;
                MensagemEhErro = false;
            }

            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — documento não pôde ser assinado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Entrega o ARQUIVO assinado ao paciente pelo WhatsApp (parcela 43, 2ª rodada).
    ///
    /// É a metade que faltava: a assinatura vive nos bytes, e o paciente que sai só com o
    /// papel leva um documento sem a garantia que o sistema produziu. Ver
    /// <see cref="EntregaAoPaciente"/> — inclusive por que o anexo não é automático.
    /// </summary>
    [RelayCommand]
    private async Task EnviarAsync(LinhaDocumentoClinico? linha)
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

                // Devolve os BYTES GUARDADOS porque o documento está assinado — é a regra
                // que mora dentro do GerarAsync, e é o que faz o arquivo continuar válido.
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
                "Consultório — documento não pôde ser entregue ao paciente", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Cancela com motivo. A linha continua na lista marcada como cancelada: a via em
    /// papel não desaparece por ser apagada do sistema.
    /// </summary>
    [RelayCommand]
    private async Task CancelarAsync(LinhaDocumentoClinico? linha)
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
                "Consultório — documento clínico não pôde ser cancelado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }
}
