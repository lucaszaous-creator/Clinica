using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using Clinica.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Um item da folha, do ponto de vista de quem executa.
///
/// O <see cref="Marca"/> é o "chequezinho" ou a "rodela" do papel — a linguagem visual da
/// enfermagem, que quem lê esta tela lê em centenas de folhas de hospital. Trocá-la por um
/// rótulo obrigaria a equipe a aprender a convenção deste sistema em cima da que ela já tem.
/// </summary>
public sealed class LinhaExecucaoItem
{
    public required int ItemId { get; init; }
    public required int Ordem { get; init; }
    public required string Descricao { get; init; }
    public required string Detalhe { get; init; }
    public required string Situacao { get; init; }
    public required string Marca { get; init; }
    public required string? Justificativa { get; init; }
    public required string? Executante { get; init; }
    public required bool Pendente { get; init; }
    public required bool Realizado { get; init; }
    public required bool NaoRealizado { get; init; }
    public required bool Suspenso { get; init; }
    public required bool SeNecessario { get; init; }

    /// <summary>
    /// A coincidência com ALERGIA registrada deste paciente, escrita na LINHA — não no
    /// topo da folha (parcela 72).
    ///
    /// ⚠️ O lugar é a decisão. O alerta no cabeçalho diz que o paciente é alérgico a
    /// dipirona; ele não diz <b>qual das seis linhas</b> é a dipirona, e é uma linha que
    /// se vai administrar. A conferência casa item a item pelo
    /// <c>TextoCompleto</c> — que inclui dose e diluente, porque o alérgeno pode estar no
    /// diluente —, então a resposta existe e só faltava chegar onde o dedo clica.
    /// </summary>
    public required string? AlertaAlergia { get; init; }

    public bool TemAlertaAlergia => !string.IsNullOrWhiteSpace(AlertaAlergia);

    /// <summary>Já tem checagem: o caminho é retificar, não checar de novo.</summary>
    public bool Checado => Realizado || NaoRealizado;

    public static LinhaExecucaoItem De(ItemPrescricaoInterna item, string? alertaAlergia = null)
    {
        var checagem = item.ChecagemVigente;
        var situacao = item.Situacao;

        var detalhe = string.Join("  ·  ", new[]
        {
            RotulosEnum.De(item.Via),
            item.TempoInfusao,
            item.HoraPrevista is { } h ? $"previsto {h:HH\\:mm}" : null,
            item.SeNecessario ? "se necessário (SOS)" : null,
            item.Observacoes
        }.Where(p => !string.IsNullOrWhiteSpace(p)));

        var marca = situacao switch
        {
            SituacaoItemPrescricao.Realizado => $"✓ {checagem!.HoraRealizacao:HH\\:mm}",
            SituacaoItemPrescricao.NaoRealizado => $"○ {checagem!.HoraRealizacao:HH\\:mm}",
            SituacaoItemPrescricao.Suspenso => "suspenso",
            _ => "—"
        };

        return new LinhaExecucaoItem
        {
            ItemId = item.Id,
            Ordem = item.Ordem,
            Descricao = item.TextoCompleto,
            Detalhe = detalhe,
            Situacao = RotulosEnum.De(situacao),
            Marca = marca,
            Justificativa = item.Suspenso
                ? $"Suspenso pelo prescritor: {item.MotivoSuspensao}"
                : checagem?.Justificativa,
            Executante = checagem is null
                ? null
                : string.Join(" · ", new[] { checagem.ExecutanteNome, checagem.ExecutanteConselho }
                    .Where(p => !string.IsNullOrWhiteSpace(p))),
            Pendente = situacao == SituacaoItemPrescricao.Pendente,
            Realizado = situacao == SituacaoItemPrescricao.Realizado,
            NaoRealizado = situacao == SituacaoItemPrescricao.NaoRealizado,
            Suspenso = item.Suspenso,
            SeNecessario = item.SeNecessario,
            AlertaAlergia = alertaAlergia
        };
    }
}

/// <summary>
/// A FOLHA DE EXECUÇÃO — onde a técnica de enfermagem checa (parcela 42).
///
/// O que esta janela faz que nenhuma outra fazia
/// ---------------------------------------------
/// Registra a afirmação <i>"foi prescrito assim e foi realizado assim"</i>, com hora e
/// assinatura. Checar não é preencher um campo: é responder por um ato feito num paciente.
///
/// As três coisas que a tela cobra, e por quê
/// ------------------------------------------
/// - <b>A hora é digitada</b>, e vem preenchida com a de agora só como sugestão. A técnica
///   administra às 14h e registra às 14h20; o campo é dela, não do relógio.
/// - <b>Não realizado abre o pedido de justificativa</b> antes de gravar. O serviço recusa
///   sem ela, e a tela pergunta em vez de deixar o erro acontecer.
/// - <b>A reação alérgica vira ALERGIA no prontuário</b>, se quem checou confirmar. É o
///   circuito que a clínica pediu quando descreveu o caso: sem isso, "teve reação à
///   dipirona" morre no campo de texto e a próxima receita sai com dipirona de novo.
///
/// O que esta tela NÃO faz: assinar
/// --------------------------------
/// Quem confere e assina a execução é a enfermeira, <b>na via impressa</b>. O que se grava
/// aqui é o registro — prontuário, conferência do fim do dia e o circuito da alergia. Pedir
/// um segundo certificado ICP-Brasil obrigaria a clínica a comprar um e-CPF para a técnica
/// e produziria, com muita cerimônia, a mesma garantia que a caneta dela já dá.
/// </summary>
public sealed partial class FolhaExecucaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly IDialogoService _dialogo;
    private readonly int _prescricaoId;

    /// <summary>Último paciente cujo acesso já entrou na trilha — a folha recarrega a cada checagem.</summary>
    private int _acessoRegistradoDe;

    /// <summary>O paciente da folha, para abrir a evolução de enfermagem.</summary>
    private int _pacienteId;

    public ObservableCollection<LinhaExecucaoItem> Itens { get; } = [];
    public ObservableCollection<string> Alertas { get; } = [];

    [ObservableProperty] private string _numero = string.Empty;
    [ObservableProperty] private string _paciente = string.Empty;
    [ObservableProperty] private string _cabecalho = string.Empty;
    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private bool _temAlertas;

    /// <summary>Medicação de uso contínuo do paciente, já montada em uma linha.</summary>
    [ObservableProperty] private string? _medicacaoEmUso;
    [ObservableProperty] private bool _emExecucao;
    [ObservableProperty] private bool _execucaoCompleta;

    /// <summary>Já houve checagem — o registro de execução tem o que mostrar.</summary>
    [ObservableProperty] private bool _temRegistroExecucao;

    /// <summary>A folha nasceu pedindo a 2ª assinatura (a eletrônica da enfermagem).</summary>
    [ObservableProperty] private bool _exigeAssinaturaEletronica;

    /// <summary>Encerrada, pedindo a 2ª assinatura, e ela ainda não foi colhida.</summary>
    [ObservableProperty] private bool _aguardaAssinaturaExecucao;

    /// <summary>Estado da 2ª assinatura, por extenso — vazio esconde a linha.</summary>
    [ObservableProperty] private string? _situacaoAssinaturaExecucao;

    /// <summary>Hora sugerida para a próxima checagem. Sugestão — o campo é de quem executou.</summary>
    [ObservableProperty] private string _hora = DateTime.Now.ToString("HH\\:mm");

    /// <summary>Metade visível da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeChecar => SessaoUsuario.Atual.Pode(Permissao.ChecarPrescricao)
                              && TemConselho;

    /// <summary>
    /// ⚠️ A METADE VISÍVEL da recusa do COREN (parcela 72). O serviço RECUSA checar sem o
    /// registro no conselho — e é regra certa, porque o número é COPIADO no ato e corrigir
    /// depois exigiria retificar registro a registro. Mas a recusa chegava no CLIQUE, com
    /// os botões acesos: a técnica cuja ficha de profissional não está vinculada
    /// administrava o soro, vinha marcar e levava um erro que ela não pode consertar
    /// sozinha (o conserto é em Equipe e em Acessos, bits que o perfil dela não tem).
    ///
    /// Botão apagado com a frase ao lado é a metade que EXPLICA; a recusa do serviço
    /// continua sendo a que impede. As duas barreiras, aplicadas a uma regra clínica.
    /// </summary>
    public bool TemConselho =>
        !string.IsNullOrWhiteSpace(SessaoUsuario.Atual.RegistroConselho);

    /// <summary>A frase que aparece na ABERTURA da folha, não no clique.</summary>
    public string? AvisoDoConselho => TemConselho
        ? null
        : "O seu login não tem o registro no conselho (COREN/CRM), e a checagem fica "
          + "bloqueada: o número é copiado para o prontuário no momento em que você checa. "
          + "Peça à direção para preencher o registro na ficha do profissional (Equipe) e "
          + "vincular essa ficha ao seu login (Acessos).";

    /// <summary>
    /// Bit próprio da evolução de enfermagem (parcela 71). ⚠️ Sem <c>EmExecucao</c>: a
    /// folha encerrada continua aceitando registro — encerrar bloqueia a checagem de ITEM,
    /// não a observação clínica.
    /// </summary>
    public bool PodeRegistrarEnfermagem =>
        SessaoUsuario.Atual.Pode(Permissao.RegistrarEvolucaoEnfermagem) && TemConselho;

    /// <summary>Só folha em execução se checa — a encerrada já foi fechada e assinada no papel.</summary>
    public bool PodeMexer => PodeChecar && EmExecucao;

    /// <summary>Encerrar exige tudo checado, e o botão diz isso antes do clique.</summary>
    public bool PodeEncerrar => PodeMexer && ExecucaoCompleta;

    /// <summary>
    /// Suspender é ato de QUEM PRESCREVE, não de quem executa — por isso o bit é
    /// <see cref="Permissao.Prescrever"/>, e não o da checagem. O botão fica apagado
    /// para a enfermagem com a dica dizendo por quê.
    /// </summary>
    public bool PodeSuspender => SessaoUsuario.Atual.Pode(Permissao.Prescrever) && EmExecucao;

    /// <summary>
    /// Assinar a execução é ato de quem EXECUTA — o mesmo bit da checagem, porque é o
    /// mesmo trabalho selado: quem checou responde pelo que checou. A metade que impede é
    /// o <c>Exigir</c> no comando; a titularidade (CPF do certificado × enfermeira logada)
    /// é do serviço.
    /// </summary>
    public bool PodeAssinarExecucao => PodeChecar && AguardaAssinaturaExecucao;

    partial void OnAguardaAssinaturaExecucaoChanged(bool value)
        => OnPropertyChanged(nameof(PodeAssinarExecucao));

    public FolhaExecucaoViewModel(
        IServiceScopeFactory escopos, IDialogoService dialogo, int prescricaoId)
    {
        _escopos = escopos;
        _dialogo = dialogo;
        _prescricaoId = prescricaoId;
        _ = CarregarAsync();
    }

    partial void OnEmExecucaoChanged(bool value)
    {
        OnPropertyChanged(nameof(PodeMexer));
        OnPropertyChanged(nameof(PodeEncerrar));
        OnPropertyChanged(nameof(PodeSuspender));
    }

    partial void OnExecucaoCompletaChanged(bool value)
        => OnPropertyChanged(nameof(PodeEncerrar));

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Carregando = true;
            NaoVerificado = false;

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<PrescricaoInternaService>();

            var prescricao = await servico.ObterAsync(_prescricaoId)
                ?? throw new InvalidOperationException("Prescrição não encontrada.");

            // Trilha de LEITURA (parcela 52): esta janela abre a folha de QUALQUER
            // paciente da fila do dia — itens prescritos e alergias — a partir da Sala de
            // Infusão, sem paciente escolhido antes. Registrada na troca de prescrição
            // (a janela recarrega a cada checagem, e o paciente é o mesmo).
            if (_acessoRegistradoDe != prescricao.PacienteId)
            {
                _acessoRegistradoDe = prescricao.PacienteId;
                await scope.ServiceProvider.GetRequiredService<AcessoProntuarioService>()
                    .RegistrarAsync(prescricao.PacienteId, SessaoUsuario.Atual.Operador,
                        OrigemAcessoProntuario.Documento);
            }

            Numero = prescricao.Numero;
            _pacienteId = prescricao.PacienteId;
            Paciente = prescricao.Paciente?.Nome ?? "—";
            Cabecalho = $"{RotulosEnum.De(prescricao.Situacao)} · "
                      + $"{prescricao.Data:dd/MM/yyyy} às {prescricao.Hora:HH\\:mm} · "
                      + $"prescrita por {prescricao.Profissional?.Nome ?? "—"}";
            Resumo = $"{prescricao.Realizados} realizados · {prescricao.NaoRealizados} não "
                   + $"realizados · {prescricao.Pendentes} aguardando";

            EmExecucao = prescricao.PodeChecar;
            ExecucaoCompleta = prescricao.ExecucaoCompleta;
            TemRegistroExecucao = prescricao.Realizados + prescricao.NaoRealizados > 0;

            ExigeAssinaturaEletronica = prescricao.ExigeAssinaturaEletronicaDaExecucao;
            AguardaAssinaturaExecucao = prescricao.AguardaAssinaturaDaExecucao;
            SituacaoAssinaturaExecucao = prescricao.AssinaturaDaExecucao is { } daExecucao
                ? $"Execução assinada eletronicamente por {daExecucao.NomeAssinante} "
                  + $"em {daExecucao.AssinadoEm:dd/MM/yyyy HH\\:mm}."
                : prescricao.AguardaAssinaturaDaExecucao
                    ? "Esta folha pede a assinatura eletrônica da enfermagem — falta colhê-la."
                    : null;

            // ⚠️ CONFERIR, não só o CONTEXTO (parcela 72). Até aqui esta tela chamava
            // `ContextoAsync`, que devolve as alergias do paciente e a medicação de uso
            // contínuo — e o código percorria SÓ as alergias: `MedicacoesEmUso` morria na
            // variável com a consulta já paga. Pior, o alerta ficava no TOPO da folha, sem
            // dizer QUAL das seis linhas é a dipirona.
            //
            // `ConferirAsync` casa item a item pelo TextoCompleto (dose e diluente
            // incluídos, porque o alérgeno pode estar no diluente) e custa exatamente a
            // MESMA consulta: os dois caminham para `PrescricaoService.ConferirAsync`.
            var conferencia = await servico.ConferirAsync(prescricao);

            var alertaPorItem = conferencia.Alertas
                .Where(a => a.Gravidade == GravidadePrescricao.Alergia)
                .GroupBy(a => a.Item)
                .ToDictionary(g => g.Key, g => string.Join(" · ", g.Select(a => a.Motivo)));

            Itens.Clear();
            foreach (var item in prescricao.Itens.OrderBy(i => i.Ordem).ThenBy(i => i.Id))
                Itens.Add(LinhaExecucaoItem.De(
                    item,
                    alertaPorItem.GetValueOrDefault(item.TextoCompleto)));

            Alertas.Clear();
            foreach (var alergia in conferencia.Alergias)
                Alertas.Add($"ALERGIA: {alergia.Rotulo}");
            TemAlertas = Alertas.Count > 0;

            // A medicação de uso contínuo é CONTEXTO, não alerta: ela não casa com item
            // nenhum de propósito (o caso normal da renovação de receita é justamente
            // prescrever o que o paciente já toma). Fica como linha, fora da caixa
            // vermelha — quem administra é hoje a única pessoa da clínica que não sabe o
            // que o paciente já usa.
            MedicacaoEmUso = conferencia.MedicacoesEmUso.Count == 0
                ? null
                : "Em uso contínuo: "
                  + string.Join(" · ", conferencia.MedicacoesEmUso.Select(m => m.Rotulo));
        }
        catch (Exception ex)
        {
            NaoVerificado = true;
            Application.Diagnostico.Registrar(
                "Consultório — folha de execução não pôde ser carregada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    /// <summary>
    /// A evolução de enfermagem — a segunda porta, para quem já está na folha. Abre a MESMA
    /// janela da fila da sala: quatro montagens da mesma tela divergiriam na primeira
    /// correção.
    /// </summary>
    [RelayCommand]
    private async Task AnotarAsync()
    {
        SessaoUsuario.Atual.Exigir(
            Permissao.RegistrarEvolucaoEnfermagem, "registrar evolução de enfermagem");

        EvolucaoEnfermagemWindow.Abrir(
            _escopos, _dialogo, _pacienteId, Paciente, _prescricaoId, Numero);

        await CarregarAsync();
    }

    /// <summary>O "chequezinho": foi realizado, nesta hora.</summary>
    [RelayCommand]
    private Task RealizadoAsync(LinhaExecucaoItem? linha)
        => ChecarAsync(linha, SituacaoChecagem.Realizado);

    /// <summary>A "rodela": foi prescrito e NÃO foi feito — com justificativa.</summary>
    [RelayCommand]
    private Task NaoRealizadoAsync(LinhaExecucaoItem? linha)
        => ChecarAsync(linha, SituacaoChecagem.NaoRealizado);

    /// <summary>
    /// Corrige uma checagem SEM apagá-la: grava outra apontando a anterior, com motivo.
    /// A antiga fica na folha impressa, no bloco "checagens retificadas".
    /// </summary>
    [RelayCommand]
    private async Task RetificarAsync(LinhaExecucaoItem? linha)
    {
        if (linha is null || !linha.Checado) return;

        // A segunda barreira, ANTES de pedir o motivo: retificar é o mesmo ato de checar
        // (o mesmo bit), e era o único comando de escrita desta folha sem o Exigir — o
        // IsEnabled explica, este impede (atalho de teclado passa pelo primeiro).
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.ChecarPrescricao, "retificar checagem");
        }
        catch (Exception ex)
        {
            Mensagem = ex.Message;
            MensagemEhErro = true;
            return;
        }

        var motivo = _dialogo.PerguntarTexto(
            "Retificar checagem",
            $"Por que a checagem do item {linha.Ordem} estava errada? A anterior NÃO é "
            + "apagada — ela continua na folha, com este motivo ao lado.");
        if (string.IsNullOrWhiteSpace(motivo)) return;

        var novaSituacao = linha.Realizado
            ? SituacaoChecagem.NaoRealizado
            : SituacaoChecagem.Realizado;

        string? justificativa = null;
        if (novaSituacao == SituacaoChecagem.NaoRealizado)
        {
            justificativa = _dialogo.PerguntarTexto(
                "Por que não foi realizado?",
                $"Item {linha.Ordem} — {linha.Descricao}");
            if (string.IsNullOrWhiteSpace(justificativa)) return;
        }

        // Retificar de "não realizado" para "realizado" É administrar: a conferência de
        // alergia vale igual, e o serviço recusa sem confirmação.
        var confirmouAlergia = false;
        if (novaSituacao == SituacaoChecagem.Realizado && linha.TemAlertaAlergia)
        {
            if (!_dialogo.Confirmar(
                    "ALERGIA registrada — administrar mesmo assim?",
                    $"Item {linha.Ordem} — {linha.Descricao}\n\n{linha.AlertaAlergia}\n\n"
                    + "O sistema não impede — quem está com o paciente é quem decide —, "
                    + "mas a confirmação fica registrada."))
                return;

            confirmouAlergia = true;
        }

        await ExecutarAsync(async (servico, executante, hora) =>
            await servico.RetificarAsync(
                linha.ItemId, novaSituacao, hora, executante, motivo, justificativa,
                confirmouAlergia));
    }

    /// <summary>
    /// Encerra a execução: a folha sai da sala e não se checa mais.
    ///
    /// NÃO pede certificado. Quem confere e assina a execução é a enfermeira, na via
    /// impressa — o que esta tela grava é o registro do que foi feito, e a autoria dele
    /// está na caneta dela sobre a folha.
    ///
    /// Não há reabertura: se a folha foi encerrada cedo demais, a correção é retificar a
    /// checagem errada ou prescrever outra folha.
    /// </summary>
    [RelayCommand]
    private async Task EncerrarAsync()
    {
        if (!ExecucaoCompleta)
        {
            Mensagem = "Ainda há item sem checagem. Uma folha encerrada com item em aberto "
                     + "não diz se a medicação entrou no paciente.";
            MensagemEhErro = true;
            return;
        }

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.ChecarPrescricao, "checar prescrição");

            if (!_dialogo.Confirmar(
                    "Encerrar execução",
                    $"Encerrar a execução da prescrição {Numero}? Depois disso a folha não "
                    + "pode mais ser checada.\n\n"
                    + (ExigeAssinaturaEletronica
                        ? "Esta folha pede a assinatura ELETRÔNICA da enfermagem — ela é "
                          + "colhida logo depois de encerrar, e sela DOIS documentos: a "
                          + "prescrição (que fica com as duas assinaturas) e o registro de "
                          + "execução (a folha que mostra o que foi feito)."
                        : "Lembre de assinar a via impressa — é ela que responde pela "
                          + "execução.")))
                return;

            using (var scope = _escopos.CreateScope())
            {
                var servico = scope.ServiceProvider.GetRequiredService<ChecagemPrescricaoService>();
                await servico.EncerrarAsync(_prescricaoId, Executante());
            }

            await CarregarAsync();

            // A folha que pede a 2ª assinatura oferece a colheita NA HORA: a enfermeira
            // está aqui, com a folha na frente — mandá-la procurar o botão depois é como
            // a pendência vira esquecimento. Recusar agora não perde nada: o botão
            // "Assinar execução" continua na folha encerrada.
            if (AguardaAssinaturaExecucao
                && _dialogo.Confirmar(
                    "Assinar a execução",
                    "Esta folha pede a assinatura eletrônica da enfermagem. Assinar agora, "
                    + "com o seu certificado?"))
            {
                await AssinarExecucaoAsync();
                return;
            }

            Mensagem = ExigeAssinaturaEletronica
                ? "Execução encerrada. Falta a assinatura eletrônica da enfermagem — o "
                  + "botão \"Assinar execução\" fica nesta folha."
                : "Execução encerrada. Confira e assine a via impressa.";
            MensagemEhErro = false;
        }
        catch (Exception ex)
        {
            Application.Diagnostico.Registrar(
                "Consultório — execução não pôde ser encerrada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// A 2ª ASSINATURA (decisão da direção, 14/08/2026): a enfermagem sela o registro de
    /// execução com o certificado DELA — o e-CPF ou o SafeID de quem está logado, nunca o
    /// do prescritor. Quem confere a titularidade é o serviço; quem valida o estado da
    /// folha (encerrada, campo marcado, ainda sem assinatura) é o domínio. Aqui só se
    /// escolhe o certificado e se diz o que aconteceu.
    /// </summary>
    [RelayCommand]
    private async Task AssinarExecucaoAsync()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.ChecarPrescricao, "checar prescrição");

            if (!SessaoUsuario.Atual.Autenticado)
            {
                // Sem login não há CPF contra o qual conferir o certificado — e a regra
                // inteira da 2ª assinatura é provar QUEM assinou.
                Mensagem = "Entre com o seu usuário para assinar a execução: a assinatura "
                         + "é conferida contra o CPF de quem está logado.";
                MensagemEhErro = true;
                return;
            }

            // DOIS documentos, um certificado: a prescrição (revisão incremental) e o
            // registro de execução. Em nuvem isso muda o escopo da autorização — com o
            // padrão do PSC a segunda selagem seria recusada sempre.
            var certificado = EscolherCertificadoWindow.Perguntar(
                $"Prescrição {Numero} — execução · {Paciente}", JanelaAtiva(), _escopos,
                assinaturasDoAto: 2);

            if (certificado is null) return;   // diálogo cancelado: sair calado é o certo

            var registroSelado = false;

            using (var scope = _escopos.CreateScope())
            {
                var assinaturas = scope.ServiceProvider
                    .GetRequiredService<AssinaturaDePrescricaoService>();

                var assinada = await assinaturas.AssinarExecucaoAsync(
                    _prescricaoId, certificado,
                    SessaoUsuario.Atual.UsuarioId, SessaoUsuario.Atual.Operador);

                registroSelado = assinada.AssinaturaDaExecucao?.ArquivoRegistroId is not null;
            }

            await CarregarAsync();

            // ⚠️ São DOIS documentos de UM ato, e a tela tem de dizer qual dos dois saiu.
            // Falhar ao selar o registro não desfaz a assinatura da prescrição — mas ficar
            // calado faria a enfermeira imprimir uma folha de execução sem carimbo achando
            // que ela está selada.
            Mensagem = registroSelado
                ? "Execução assinada. A PRESCRIÇÃO passa a sair com as duas assinaturas, e "
                  + "o REGISTRO DE EXECUÇÃO — a folha que mostra o que foi feito — saiu "
                  + "selado com o seu certificado."
                : "Execução assinada na PRESCRIÇÃO, que agora sai com as duas assinaturas. "
                  + "O registro de execução NÃO pôde ser selado: ele sai como espelho, "
                  + "apontando esta folha. Avise o suporte.";
            MensagemEhErro = !registroSelado;
        }
        catch (Exception ex)
        {
            Application.Diagnostico.Registrar(
                "Sala de infusão — execução não pôde ser assinada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>A janela ATIVA é a dona do modal — a lição da lista de espera (parcela 58).</summary>
    private static System.Windows.Window? JanelaAtiva()
        => System.Windows.Application.Current?.Windows.OfType<System.Windows.Window>()
               .FirstOrDefault(w => w.IsActive)
           ?? System.Windows.Application.Current?.MainWindow;

    /// <summary>
    /// Imprime a folha de PRESCRIÇÃO — a via que a enfermagem confere e assina à caneta.
    ///
    /// Era a metade que faltava da sala: todo o desenho da checagem assume a via impressa,
    /// e a única porta de impressão morava na tela de quem prescreve, num app que a máquina
    /// da enfermagem não instala. Quando há assinatura eletrônica, saem os BYTES GUARDADOS —
    /// a assinatura cobre bytes, e um PDF "igual" regerado agora abriria como inválido.
    /// </summary>
    [RelayCommand]
    private Task ImprimirAsync() => ImprimirFolhaAsync(FolhaPrescricao.Prescricao);

    /// <summary>O espelho eletrônico do que foi checado — prontuário e conferência do fim do dia.</summary>
    [RelayCommand]
    private Task ImprimirRegistroAsync() => ImprimirFolhaAsync(FolhaPrescricao.RegistroExecucao);

    private async Task ImprimirFolhaAsync(FolhaPrescricao folhaPedida)
    {
        try
        {
            FolhaAssinada folha;
            using (var scope = _escopos.CreateScope())
            {
                var assinaturas = scope.ServiceProvider
                    .GetRequiredService<AssinaturaDePrescricaoService>();
                folha = await assinaturas.FolhaAsync(_prescricaoId, folhaPedida);
            }

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                folha.Pdf, ImpressaoPdf.NomeSeguro(folha.NomeArquivo));

            // A conferência da assinatura é DITA, nas três respostas possíveis: íntegra,
            // alterada, ou não foi possível conferir — abrir em silêncio faria a terceira
            // passar por sucesso.
            Mensagem = erro ?? folha.Conferencia?.Frase;
            MensagemEhErro = erro is not null || folha.Conferencia is { Integra: false };
        }
        catch (Exception ex)
        {
            Application.Diagnostico.Registrar(
                "Sala de infusão — folha não pôde ser impressa", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Tira um item da folha ASSINADA — o caminho de correção de quem prescreve, diferente
    /// da "rodela" (que registra o que aconteceu na sala). O serviço recusa item já
    /// checado; aqui a guarda DIZ isso em vez de deixar a exceção falar.
    /// </summary>
    [RelayCommand]
    private async Task SuspenderAsync(LinhaExecucaoItem? linha)
    {
        if (linha is null) return;

        if (linha.Checado || linha.Suspenso)
        {
            Mensagem = linha.Suspenso
                ? "Este item já está suspenso."
                : "Este item já foi checado pela enfermagem e não se suspende — se a "
                  + "checagem está errada, quem executou deve retificá-la.";
            MensagemEhErro = true;
            return;
        }

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.Prescrever, "suspender item de prescrição");

            var motivo = _dialogo.PerguntarTexto(
                "Suspender item",
                $"Por que o item {linha.Ordem} ({linha.Descricao}) não deve mais ser feito? "
                + "O item continua na folha, marcado como suspenso, com este motivo ao lado.");
            if (string.IsNullOrWhiteSpace(motivo)) return;

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<PrescricaoInternaService>();
            await servico.SuspenderItemAsync(
                linha.ItemId, motivo, SessaoUsuario.Atual.Operador);

            Mensagem = null;
            MensagemEhErro = false;
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Application.Diagnostico.Registrar(
                "Sala de infusão — item não pôde ser suspenso", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    // ---- Apoio ----

    private async Task ChecarAsync(LinhaExecucaoItem? linha, SituacaoChecagem situacao)
    {
        if (linha is null) return;

        if (linha.Suspenso)
        {
            Mensagem = "Este item foi suspenso pelo prescritor e não deve ser administrado.";
            MensagemEhErro = true;
            return;
        }

        if (linha.Checado)
        {
            Mensagem = "Este item já foi checado. Use Retificar — a checagem anterior fica "
                     + "na folha, com o motivo da correção.";
            MensagemEhErro = true;
            return;
        }

        string? justificativa = null;
        string? alergia = null;
        var confirmouAlergia = false;

        // ⚠️ A alergia é conferida ANTES do clique valer (parcela 72). O serviço RECUSA
        // sem confirmação explícita; a tela pergunta em vez de deixar a recusa chegar
        // como erro na cara de quem está com o paciente — as duas barreiras de sempre,
        // aplicadas a uma regra clínica em vez de a uma permissão.
        if (situacao == SituacaoChecagem.Realizado && linha.TemAlertaAlergia)
        {
            if (!_dialogo.Confirmar(
                    "ALERGIA registrada — administrar mesmo assim?",
                    $"Item {linha.Ordem} — {linha.Descricao}\n\n{linha.AlertaAlergia}\n\n"
                    + "O sistema não impede: o registro pode estar errado, pode haver "
                    + "dessensibilização, e quem está com o paciente é quem decide. Mas a "
                    + "confirmação fica registrada."))
                return;

            confirmouAlergia = true;
        }

        if (situacao == SituacaoChecagem.NaoRealizado)
        {
            justificativa = _dialogo.PerguntarTexto(
                "Por que não foi realizado?",
                $"Item {linha.Ordem} — {linha.Descricao}\n\n"
                + "Ex.: paciente recusou, apresentou reação, acesso perdido, medicação em falta.");

            // Sem justificativa não se grava — e o serviço recusaria de qualquer forma.
            // Perguntar aqui é o que evita transformar a regra num erro na cara da técnica.
            if (string.IsNullOrWhiteSpace(justificativa)) return;

            alergia = PerguntarAlergia(justificativa);
        }

        await ExecutarAsync(async (servico, executante, hora) =>
            await servico.ChecarAsync(
                linha.ItemId, situacao, hora, executante, justificativa, alergia,
                confirmouAlergia));
    }

    /// <summary>
    /// Quando a justificativa fala em reação/alergia, oferece registrar a alergia no
    /// prontuário — e é o que faz o caso de hoje acender o alerta da próxima prescrição.
    ///
    /// A oferta é DISPARADA pela palavra, mas quem decide é quem checou: registrar sozinho
    /// encheria a lista de problemas de alergias que a técnica não afirmou.
    /// </summary>
    private string? PerguntarAlergia(string justificativa)
    {
        var texto = justificativa.ToLowerInvariant();
        var falaEmReacao = texto.Contains("alerg") || texto.Contains("reaç")
                        || texto.Contains("reac") || texto.Contains("anafil");

        if (!falaEmReacao) return null;

        if (!_dialogo.Confirmar(
                "Registrar alergia no prontuário?",
                "A justificativa fala em reação. Quer registrar isso como ALERGIA na lista "
                + "de problemas do paciente? A partir daí toda prescrição com esse termo "
                + "passa a acender um alerta."))
            return null;

        var descricao = _dialogo.PerguntarTexto(
            "Alergia a quê?",
            "Escreva só o agente (ex.: \"Dipirona\"). É esta palavra que a conferência "
            + "procura nas próximas prescrições — uma frase inteira casaria com quase nada.");

        return string.IsNullOrWhiteSpace(descricao) ? null : descricao;
    }

    /// <summary>Roda a checagem com as guardas comuns e recarrega a folha.</summary>
    private async Task ExecutarAsync(
        Func<ChecagemPrescricaoService, IdentificacaoExecutante, TimeOnly, Task> acao)
    {
        if (!TimeOnly.TryParse(Hora, out var hora))
        {
            Mensagem = $"Hora inválida (\"{Hora}\"). Escreva no formato 14:30 — é o horário "
                     + "em que o item foi administrado, não o de agora.";
            MensagemEhErro = true;
            return;
        }

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.ChecarPrescricao, "checar prescrição");

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<ChecagemPrescricaoService>();

            await acao(servico, Executante(), hora);

            Mensagem = null;
            MensagemEhErro = false;
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Application.Diagnostico.Registrar("Consultório — checagem não pôde ser gravada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Quem está executando sai do LOGIN, nunca de um campo digitado: é o vínculo com a
    /// pessoa que dá valor à checagem.
    /// </summary>
    private static IdentificacaoExecutante Executante() => new(
        UsuarioId: SessaoUsuario.Atual.Autenticado ? SessaoUsuario.Atual.UsuarioId : null,
        Nome: SessaoUsuario.Atual.Autenticado
            ? SessaoUsuario.Atual.Nome
            : SessaoUsuario.Atual.Operador,
        // ⚠️ Era `null` LITERAL desde a parcela 42: a coluna existia, o PDF tinha o ramo
        // que a imprime e a exportação tinha a coluna — e nenhuma checagem de produção
        // saía identificada. O conselho vem do Profissional vinculado ao login.
        Conselho: SessaoUsuario.Atual.RegistroConselho);
}
