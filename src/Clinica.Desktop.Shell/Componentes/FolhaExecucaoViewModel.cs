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

    /// <summary>Já tem checagem: o caminho é retificar, não checar de novo.</summary>
    public bool Checado => Realizado || NaoRealizado;

    public static LinhaExecucaoItem De(ItemPrescricaoInterna item)
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
            SeNecessario = item.SeNecessario
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
    [ObservableProperty] private bool _emExecucao;
    [ObservableProperty] private bool _execucaoCompleta;

    /// <summary>Já houve checagem — o registro de execução tem o que mostrar.</summary>
    [ObservableProperty] private bool _temRegistroExecucao;

    /// <summary>Hora sugerida para a próxima checagem. Sugestão — o campo é de quem executou.</summary>
    [ObservableProperty] private string _hora = DateTime.Now.ToString("HH\\:mm");

    /// <summary>Metade visível da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeChecar => SessaoUsuario.Atual.Pode(Permissao.ChecarPrescricao);

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
            Paciente = prescricao.Paciente?.Nome ?? "—";
            Cabecalho = $"{RotulosEnum.De(prescricao.Situacao)} · "
                      + $"{prescricao.Data:dd/MM/yyyy} às {prescricao.Hora:HH\\:mm} · "
                      + $"prescrita por {prescricao.Profissional?.Nome ?? "—"}";
            Resumo = $"{prescricao.Realizados} realizados · {prescricao.NaoRealizados} não "
                   + $"realizados · {prescricao.Pendentes} aguardando";

            EmExecucao = prescricao.PodeChecar;
            ExecucaoCompleta = prescricao.ExecucaoCompleta;
            TemRegistroExecucao = prescricao.Realizados + prescricao.NaoRealizados > 0;

            Itens.Clear();
            foreach (var item in prescricao.Itens.OrderBy(i => i.Ordem).ThenBy(i => i.Id))
                Itens.Add(LinhaExecucaoItem.De(item));

            var contexto = await servico.ContextoAsync(prescricao.PacienteId);
            Alertas.Clear();
            foreach (var alergia in contexto.Alergias)
                Alertas.Add($"ALERGIA: {alergia.Rotulo}");
            TemAlertas = Alertas.Count > 0;
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

        await ExecutarAsync(async (servico, executante, hora) =>
            await servico.RetificarAsync(
                linha.ItemId, novaSituacao, hora, executante, motivo, justificativa));
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
                    + "pode mais ser checada.\n\nLembre de assinar a via impressa — é ela "
                    + "que responde pela execução."))
                return;

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<ChecagemPrescricaoService>();

            await servico.EncerrarAsync(_prescricaoId, Executante());

            await CarregarAsync();
            Mensagem = "Execução encerrada. Confira e assine a via impressa.";
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
                linha.ItemId, situacao, hora, executante, justificativa, alergia));
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
        Conselho: null);
}
