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

    /// <summary>
    /// Metade visível da permissão; a que impede é o <c>Exigir</c> no comando.
    ///
    /// ⚠️ É <see cref="Permissao.Prescrever"/> desde a parcela 59, e não
    /// <c>EditarPaciente</c>. Esta tela emite RECEITA, ATESTADO e PEDIDO DE EXAME pela
    /// janela genérica — três papéis que mandam alguém tomar ou fazer alguma coisa. Usar o
    /// bit do CADASTRO para autorizá-los é o bit sobrecarregado que a parcela 49 corrigiu
    /// no domínio e que sobrevivia aqui: quem digita o telefone de um paciente passava a
    /// poder assinar uma receita para ele.
    /// </summary>
    public bool PodeEditarProntuario => SessaoUsuario.Atual.Pode(Permissao.Prescrever);

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

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): a troca de paciente dispara uma
    /// carga por seleção, e a resposta atrasada do paciente anterior chegando por último
    /// poria os documentos dele sob o nome do paciente novo no cabeçalho.
    /// </summary>
    private int _geracaoCarga;

    /// <summary>Último paciente cujo acesso já entrou na trilha — a tela recarrega mais do que troca.</summary>
    private int _acessoRegistradoDe;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;
        var pacienteId = PacienteId;

        SemPaciente = pacienteId == 0;
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

            // Trilha de LEITURA (parcela 52), na troca de paciente: receita e atestado
            // são dado de saúde, e esta porta ficava fora da trilha.
            if (_acessoRegistradoDe != pacienteId)
            {
                _acessoRegistradoDe = pacienteId;
                await scope.ServiceProvider.GetRequiredService<AcessoProntuarioService>()
                    .RegistrarAsync(pacienteId, SessaoUsuario.Atual.Operador,
                        OrigemAcessoProntuario.Documento);
            }

            var doPaciente = await servico.DoPacienteAsync(pacienteId);

            // Chegou tarde: outra seleção já pediu uma carga mais nova.
            if (geracao != _geracaoCarga) return;

            // Mesmo filtro da ficha: o catálogo decide o que cada acesso alcança, e as
            // duas telas listam o MESMO documento (parcela 59).
            foreach (var d in doPaciente)
            {
                var linha = LinhaDocumento.De(d);
                if (SessaoUsuario.Atual.Pode(linha.AcessoParaVer)) Documentos.Add(linha);
            }
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;

            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar("Recepção — documentos não puderam ser carregados", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
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
            SessaoUsuario.Atual.Exigir(Permissao.Prescrever, "emitir documento clínico");

            var vm = new DocumentoEdicaoViewModel(_escopos, PacienteId);
            var janela = new Clinica.Desktop.Shell.Componentes.DocumentoWindow(vm)
            {
                Owner = JanelaDona.Atual()
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
            // ⚠️ O bit do TIPO que está na linha, não o da porta (a regra da parcela 60).
            // Com `Prescrever` fixo, as duas barreiras DISCORDAVAM sobre que ato é aquele:
            // o botão acende por `AcessoParaMexer` — que é `EditarPaciente` na declaração
            // de comparecimento, `VerProntuario` no relatório de evolução e
            // `ColherAssinaturaPaciente` no termo —, e o comando exigia `Prescrever`. Em
            // quatro dos oito tipos isso é o corredor sem saída da parcela 69: a pessoa
            // atravessa a porta, faz o trabalho e leva a recusa no fim. E havia a fuga
            // oposta: quem tem `Prescrever` e não tem o bit do tipo passava direto — a
            // segunda barreira mais FROUXA que a primeira.
            SessaoUsuario.Atual.Exigir(linha.AcessoParaMexer, "assinar documento clínico");

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

        // A barreira que faltava (a mesma que a parcela 68 pôs no gêmeo do Consultório).
        // Emitir, assinar e cancelar já a tinham; enviar não — e enviar é DADO DE SAÚDE
        // SAINDO, que é o que a parcela 60 passou a cobrar no export. Três comandos
        // vizinhos guardados e um não: o errado é o um.
        SessaoUsuario.Atual.Exigir(linha.AcessoParaMexer, "enviar documento clínico");

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
            // ⚠️ A barreira que NÃO EXISTIA (parcela 72). Este comando não tinha `Exigir`
            // nenhum, e a metade visível (`PodeEnviar`) era estado puro — `Assinado &&
            // !Cancelado`, sem permissão. Enviar é DADO DE SAÚDE SAINDO para o WhatsApp do
            // paciente, que é o que uma investigação procura (a lição da parcela 60); e o
            // `Cancelar` ao lado já guardava com o mesmo bit. Três comandos vizinhos
            // guardados e um não: o errado é o um.
            SessaoUsuario.Atual.Exigir(linha.AcessoParaMexer, "enviar documento clínico");

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
            SessaoUsuario.Atual.Exigir(Permissao.Prescrever, "emitir documento clínico");

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
