using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Prontuario;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Application.Modelos;

/// <summary>Uma linha da escolha "a qual sessão este termo pertence?". <c>AgendamentoId</c> nulo é o termo avulso.</summary>
public sealed record OpcaoSessaoTermo(int? AgendamentoId, string Rotulo, string Detalhe, bool Hoje)
{
    public static OpcaoSessaoTermo Avulso { get; } = new(
        null,
        "Nenhuma — termo avulso",
        "O paciente assina agora e o termo não fica ligado a uma sessão.",
        false);
}

/// <summary>O que a coleta resolveu sobre a sessão: amarrar (com a frase), ficar avulso, ou PERGUNTAR.</summary>
public sealed record DecisaoDaSessaoDoTermo(bool Perguntar, int? Sessao, string? Rotulo)
{
    public static readonly DecisaoDaSessaoDoTermo SemSessao = new(false, null, null);
}

/// <summary>
/// A COLETA DO TERMO — as regras que eram do <c>ColetaDeTermo</c> e dos ViewModels das janelas
/// de escolha (shell WPF), movidas sem mudança na conversão para Blazor (out/2026). O termo
/// assinado pelo PACIENTE é assinatura eletrônica SIMPLES (fora do congelamento do SafeID), e o
/// que ele colhe é a prova de que o paciente consentiu — por isso as regras moram onde o
/// <c>dotnet test</c> alcança, e as quatro portas passam pelo MESMO ponto.
/// </summary>
public static class ColetaDeTermoNaTela
{
    /// <summary>
    /// A qual SESSÃO este termo pertence — três degraus (set/2026): quem chamou já sabe (nada é
    /// perguntado); UMA sessão hoje e nenhuma outra à frente (amarra sozinho, e a janela DIZ a que
    /// amarrou); duas, ou sessões à frente (pergunta). Documento JÁ EMITIDO não pergunta nada.
    /// </summary>
    public static DecisaoDaSessaoDoTermo Decidir(int? documentoId, int? agendamentoId, IReadOnlyList<SessaoParaTermo>? sessoes)
    {
        if (documentoId is not null) return DecisaoDaSessaoDoTermo.SemSessao;
        if (agendamentoId is { } jaSabe) return new(false, jaSabe, null);
        if (sessoes is null || sessoes.Count == 0) return DecisaoDaSessaoDoTermo.SemSessao;
        if (sessoes.Count == 1 && sessoes[0].Hoje) return new(false, sessoes[0].AgendamentoId, Descrever(sessoes[0]));
        return new(true, null, null);
    }

    public static string Descrever(SessaoParaTermo sessao) => $"{sessao.Rotulo}  ·  {sessao.Detalhe}";

    /// <summary>As opções da janela de escolha, com "Nenhuma — termo avulso" de primeira classe no fim.</summary>
    public static IReadOnlyList<OpcaoSessaoTermo> Opcoes(IReadOnlyList<SessaoParaTermo> sessoes)
    {
        var opcoes = sessoes.Select(s => new OpcaoSessaoTermo(s.AgendamentoId, s.Rotulo, s.Detalhe, s.Hoje)).ToList();
        opcoes.Add(OpcaoSessaoTermo.Avulso);
        return opcoes;
    }

    /// <summary>
    /// A sessão de HOJE já vem marcada quando é uma só; com duas, nada vem marcado — marcar a
    /// primeira seria decidir pela pessoa justamente no caso em que a pergunta existe.
    /// </summary>
    public static OpcaoSessaoTermo? PreSelecionada(IReadOnlyList<OpcaoSessaoTermo> opcoes)
    {
        var deHoje = opcoes.Where(o => o.Hoje).ToList();
        return deHoje.Count == 1 ? deHoje[0] : null;
    }

    /// <summary>O rótulo da sessão escolhida na janela (para a linha "Ligado à sessão").</summary>
    public static string? RotuloDaEscolha(IReadOnlyList<SessaoParaTermo> sessoes, int? escolhida)
        => escolhida is { } id && sessoes.FirstOrDefault(s => s.AgendamentoId == id) is { } achada
            ? Descrever(achada)
            : null;

    /// <summary>O estado vazio da escolha do termo — DIZ o que fazer.</summary>
    public const string NenhumTermoEscrito = "Nenhum termo foi escrito ainda. A direção escreve o texto em "
                                             + "Configurações → \"Escrever termos…\", no aplicativo do Gerente.";

    /// <summary>
    /// Alergia formatada: alergia alerta MESMO dada por resolvida (parcela 37) — só o descarte a cala.
    /// </summary>
    public static string FormatarAlergia(ProblemaPaciente p)
        => p.EstaAtivo
            ? p.Descricao
            : $"{p.Descricao} (dada por resolvida — o alerta continua)";

    /// <summary>
    /// O botão de confirmar acende quando há traço, documento conferido e permissão. Cada uma das
    /// três tem a recusa escrita no comando.
    /// </summary>
    public static bool PodeConfirmar(bool podeColher, bool temTraco, string? documentoConferido, bool carregando)
        => podeColher && temTraco && !string.IsNullOrWhiteSpace(documentoConferido) && !carregando;

    /// <summary>
    /// O endereço do WhatsApp com a mensagem pronta — a MESMA regra do <c>Whatsapp.Abrir</c> do
    /// shell (telefone de 10 a 13 dígitos; com 10 ou 11, o DDI 55 na frente, que o wa.me exige).
    /// Nulo = telefone ausente ou inválido no cadastro.
    /// </summary>
    public static string? EnderecoDoWhatsapp(string? telefone, string mensagem)
    {
        var fone = Telefone.Normalizar(telefone);
        if (fone.Length is < 10 or > 13) return null;
        if (fone.Length is 10 or 11) fone = "55" + fone;
        return $"https://wa.me/{fone}?text={Uri.EscapeDataString(mensagem)}";
    }
}

/// <summary>
/// Uma declaração do termo com a resposta do paciente. Nasce SEM resposta de propósito: pré-marcar
/// "Sim" seria fabricar a resposta mais conveniente para a clínica.
/// </summary>
public sealed class DeclaracaoItem(int ordem, string descricao, string? detalhe, string? codigo = null)
{
    public int Ordem { get; } = ordem;
    public string Descricao { get; } = descricao;
    public string? Detalhe { get; } = detalhe;
    public string? Resposta { get; set; }

    /// <summary>
    /// A autorização de mensagens do BSV (fase 3 Recepção×CRM): opcional, aparece em
    /// destaque e fica FORA do "Marcar todas como Sim" — marcá-la pelo paciente seria
    /// fabricar o consentimento que o recall exige.
    /// </summary>
    public bool AutorizacaoDeMensagens { get; } = codigo == RespostaDeclaracao.CodigoAutorizacaoMensagens;

    public bool EhSim => RespostaDeclaracao.EhPositiva(Resposta);
    public bool EhNao => RespostaDeclaracao.EhNegativa(Resposta);

    public void MarcarSim() => Resposta = RespostaDeclaracao.Sim;
    public void MarcarNao() => Resposta = RespostaDeclaracao.Nao;
}

/// <summary>
/// COLHER A ASSINATURA DO PACIENTE num termo (parcela 66) — o estado da coleta (out/2026: era o
/// <c>AssinaturaPacienteViewModel</c> WPF, movido sem mudança de regra; o traço agora vem do
/// canvas do formulário Blazor, no MESMO formato do InkCanvas: PNG de fundo transparente em
/// resolução dobrada, com a largura e a altura da área em unidades de tela).
///
/// O que a coleta NÃO faz: impedir. Responder "não" ao jejum grava o "não" e emite o termo
/// assim mesmo. A única coisa que ela EXIGE é o documento de identidade conferido.
///
/// ⚠️ Os serviços vêm de UM escopo, aberto por quem monta a coleta e vivo enquanto o
/// formulário vive — como a janela WPF, que segurava o escopo do <c>ColetaDeTermo</c>. O
/// porteiro (<see cref="SemaphoreSlim"/>) existe porque a vigia do WhatsApp bate nos MESMOS
/// serviços a cada 5 s e o DbContext não aceita duas operações ao mesmo tempo (parcela 69).
/// </summary>
public sealed class ColetaDoTermoEmAndamento : IDisposable
{
    private readonly IServiceScope _escopo;
    private readonly DocumentoClinicoService _documentos;
    private readonly AssinaturaDoPacienteService _assinaturas;
    private readonly ParametrosService _parametros;
    private readonly AcessoProntuarioService _acessos;
    private readonly ProblemaPacienteService _problemas;
    private readonly ColetaRemotaTermoService _coletaRemota;
    private readonly int _pacienteId;
    private readonly int? _modeloId;
    private readonly int? _profissionalId;
    private readonly int? _agendamentoId;
    private DocumentoClinico? _documento;
    private CancellationTokenSource? _vigia;
    private readonly SemaphoreSlim _portaDoContexto = new(1, 1);

    /// <param name="modeloId">O modelo escrito pela clínica — NULO no termo LGPD (montado das finalidades).</param>
    /// <param name="agendamentoId">A SESSÃO a que o termo se refere; só vale na EMISSÃO.</param>
    public ColetaDoTermoEmAndamento(
        IServiceScopeFactory escopos, int pacienteId, int? modeloId, string pacienteNome,
        int? documentoExistenteId = null, int? profissionalId = null, int? agendamentoId = null,
        string? sessaoDoTermo = null)
    {
        _escopo = escopos.CreateScope();
        var s = _escopo.ServiceProvider;
        _documentos = s.GetRequiredService<DocumentoClinicoService>();
        _assinaturas = s.GetRequiredService<AssinaturaDoPacienteService>();
        _acessos = s.GetRequiredService<AcessoProntuarioService>();
        _parametros = s.GetRequiredService<ParametrosService>();
        _problemas = s.GetRequiredService<ProblemaPacienteService>();
        _coletaRemota = s.GetRequiredService<ColetaRemotaTermoService>();
        _pacienteId = pacienteId;
        _modeloId = modeloId;
        _profissionalId = profissionalId;
        _agendamentoId = agendamentoId;
        DocumentoExistenteId = documentoExistenteId;
        PacienteNome = pacienteNome;
        SessaoDoTermo = sessaoDoTermo;
    }

    public int? DocumentoExistenteId { get; }

    /// <summary>A sessão a que o termo vai ficar ligado, por extenso — vazio esconde a linha.</summary>
    public string? SessaoDoTermo { get; }

    public string PacienteNome { get; }
    public List<DeclaracaoItem> Declaracoes { get; } = [];
    public string Titulo { get; private set; } = "Termo de procedimento";
    public string Corpo { get; private set; } = string.Empty;
    public string Numero { get; private set; } = string.Empty;
    public string DocumentoConferido { get; set; } = string.Empty;
    public string Mensagem { get; private set; } = string.Empty;
    public bool MensagemEhErro { get; private set; }
    public bool Carregando { get; private set; }

    /// <summary>Há traço na área de assinatura — quem escreve é o formulário (o canvas).</summary>
    public bool TemTraco { get; set; }

    /// <summary>Colhido com sucesso (assinou ou recusou) — o formulário fecha e quem abriu recarrega.</summary>
    public bool Concluido { get; private set; }

    /// <summary>Algo mudou por fora de um campo (a vigia, uma resposta): quem desenha redesenha.</summary>
    public event Action? Mudou;
    private void Avisar() => Mudou?.Invoke();

    /// <summary>Quem TESTEMUNHA é quem fez LOGIN, nunca o usuário do Windows.</summary>
    public static string Testemunha => SessaoUsuario.Atual.Operador;

    public static bool PodeColher => SessaoUsuario.Atual.Pode(Permissao.ColherAssinaturaPaciente);

    // ==================== O link pelo WhatsApp (parcela 81) ====================

    public bool EnvioRemotoDisponivel { get; private set; }
    public bool AguardandoCelular { get; private set; }

    /// <summary>O traço que chegou do celular — é assim que a técnica VÊ o que vai confirmar.</summary>
    public byte[]? TracoRemotoPng { get; private set; }
    public int TracoRemotoLargura { get; private set; }
    public int TracoRemotoAltura { get; private set; }
    public bool TemTracoRemoto => TracoRemotoPng is not null;

    /// <param name="abrirWhatsapp">Abre a conversa com a mensagem; devolve nulo quando abriu, senão a frase.</param>
    public async Task EnviarPeloCelularAsync(Func<string, string, string, Task<string?>> abrirWhatsapp)
    {
        SessaoUsuario.Atual.Exigir(
            Permissao.ColherAssinaturaPaciente, "enviar o termo para o celular do paciente");

        if (_documento is null) return;

        await _portaDoContexto.WaitAsync();
        try
        {
            var envio = await _coletaRemota.EnviarAsync(_documento.Id, Testemunha);

            var erro = await abrirWhatsapp(envio.Telefone, PacienteNome, envio.Mensagem);
            if (erro is not null)
            {
                // O WhatsApp não abriu: o pedido não pode ficar publicado sem ninguém com o link.
                await _coletaRemota.CancelarAsync(_documento.Id, Testemunha);
                MensagemEhErro = true;
                Mensagem = erro;
                return;
            }

            AguardandoCelular = true;
            MensagemEhErro = false;
            Mensagem = $"Link enviado para {envio.Telefone} — esta janela percebe sozinha "
                     + "quando o paciente assinar. O link vale por 24 horas.";
            IniciarVigia();
        }
        catch (Exception ex)
        {
            MensagemEhErro = true;
            Mensagem = ex.Message;
            Diagnostico.Registrar("Termo pelo WhatsApp — envio", ex);
        }
        finally
        {
            _portaDoContexto.Release();
            Avisar();
        }
    }

    public async Task CancelarEnvioCelularAsync()
    {
        SessaoUsuario.Atual.Exigir(
            Permissao.ColherAssinaturaPaciente, "cancelar o envio do termo");

        if (_documento is null) return;

        PararVigia();
        AguardandoCelular = false;

        await _portaDoContexto.WaitAsync();
        try
        {
            var limpou = await _coletaRemota.CancelarAsync(_documento.Id, Testemunha);
            MensagemEhErro = !limpou;
            Mensagem = limpou
                ? "Envio cancelado — o link saiu do ar. A coleta continua aqui na janela."
                : "Envio cancelado, mas o link pode NÃO ter saído do ar (o armazenamento "
                  + "não respondeu). Veja a pasta de logs e avise o suporte.";
        }
        catch (Exception ex)
        {
            MensagemEhErro = true;
            Mensagem = ex.Message;
            Diagnostico.Registrar("Termo pelo WhatsApp — cancelamento", ex);
        }
        finally
        {
            _portaDoContexto.Release();
            Avisar();
        }
    }

    private void IniciarVigia()
    {
        PararVigia();
        var cts = new CancellationTokenSource();
        _vigia = cts;
        _ = VigiarAsync(cts.Token);
    }

    /// <summary>O formulário chama ao FECHAR — vigia órfã seguiria consultando o balde.</summary>
    public void PararVigia()
    {
        _vigia?.Cancel();
        _vigia = null;
    }

    private async Task VigiarAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);

                RespostaRemotaTermo? resposta;
                await _portaDoContexto.WaitAsync(ct);
                try
                {
                    resposta = await _coletaRemota.ColherRespostaAsync(_documento!.Id, ct);
                }
                finally
                {
                    _portaDoContexto.Release();
                }

                if (resposta is null) continue;

                AplicarRespostaRemota(resposta);
                Avisar();
                return;
            }
        }
        catch (OperationCanceledException) { /* o formulário fechou ou o envio foi cancelado */ }
        catch (Exception ex)
        {
            AguardandoCelular = false;
            MensagemEhErro = true;
            Mensagem = ex.Message;
            Diagnostico.Registrar("Termo pelo WhatsApp — vigia da resposta", ex);
            Avisar();
        }
    }

    private void AplicarRespostaRemota(RespostaRemotaTermo resposta)
    {
        // As respostas são AS DO PACIENTE — é ele quem responde no celular.
        foreach (var d in Declaracoes)
            if (resposta.Respostas.TryGetValue(d.Ordem, out var valor))
                d.Resposta = valor;

        TracoRemotoLargura = resposta.Largura;
        TracoRemotoAltura = resposta.Altura;
        TracoRemotoPng = resposta.TracoPng;
        TemTraco = true;
        AguardandoCelular = false;

        MensagemEhErro = false;
        Mensagem = $"Assinatura recebida do celular ({resposta.TelefoneDestino}). Confira a "
                 + "identidade do paciente, preencha o documento conferido e clique Confirmar.";
    }

    // ==================== Alergia — o destaque antes da assinatura (parcela 80) ====================

    public List<string> Alergias { get; } = [];

    /// <summary>Dado de saúde: quem não pode ler o prontuário não vê a região.</summary>
    public static bool PodeVerAlergias => SessaoUsuario.Atual.Pode(Permissao.VerProntuario);

    public static bool PodeRegistrarAlergia => SessaoUsuario.Atual.PodeAlgum(
        Permissao.EditarProntuario | Permissao.RegistrarEvolucaoEnfermagem);

    public string NovaAlergia { get; set; } = string.Empty;

    /// <summary>Terceiro estado: "sem alergia" e "não deu para conferir" são respostas diferentes.</summary>
    public bool AlergiasNaoConferidas { get; private set; }
    public bool TemAlergia => Alergias.Count > 0;
    public bool SemAlergiaConferida => !TemAlergia && !AlergiasNaoConferidas;

    private async Task CarregarAlergiasAsync()
    {
        if (!PodeVerAlergias) return;

        try
        {
            var alertas = await _problemas.AlertasAsync(_pacienteId);
            Alergias.Clear();
            foreach (var a in alertas.Where(p => p.Natureza == NaturezaProblema.Alergia)
                         .DistinctBy(p => ProblemaPacienteService.NormalizarAlergia(p.Descricao)))
                Alergias.Add(ColetaDeTermoNaTela.FormatarAlergia(a));
            AlergiasNaoConferidas = false;
        }
        catch (Exception ex)
        {
            AlergiasNaoConferidas = true;
            Diagnostico.Registrar("Assinatura do paciente — alergias não puderam ser conferidas", ex);
        }
    }

    /// <summary>Registra na LISTA DE PROBLEMAS o que o paciente relatou — não depende do desfecho do termo.</summary>
    public async Task RegistrarAlergiaAsync()
    {
        SessaoUsuario.Atual.ExigirAlgum(
            Permissao.EditarProntuario | Permissao.RegistrarEvolucaoEnfermagem,
            "registrar a alergia na lista de problemas");

        if (string.IsNullOrWhiteSpace(NovaAlergia))
        {
            MensagemEhErro = true;
            Mensagem = "Escreva a que o paciente é alérgico (ex.: dipirona) antes de registrar.";
            Avisar();
            return;
        }

        await _portaDoContexto.WaitAsync();
        try
        {
            await _problemas.SalvarAsync(new ProblemaPaciente
            {
                PacienteId = _pacienteId,
                Natureza = NaturezaProblema.Alergia,
                Descricao = NovaAlergia.Trim(),
                ProfissionalId = _profissionalId
            }, SessaoUsuario.Atual.Operador);

            NovaAlergia = string.Empty;
            await CarregarAlergiasAsync();

            MensagemEhErro = false;
            Mensagem = "Alergia registrada na lista de problemas — ela passa a alertar em toda "
                     + "prescrição deste paciente. Coloque a pulseira de alergia antes do procedimento.";
        }
        catch (Exception ex)
        {
            MensagemEhErro = true;
            Mensagem = ex.Message;
            Diagnostico.Registrar("Assinatura do paciente — alergia não pôde ser registrada", ex);
        }
        finally
        {
            _portaDoContexto.Release();
            Avisar();
        }
    }

    public bool PodeConfirmar => ColetaDeTermoNaTela.PodeConfirmar(PodeColher, TemTraco, DocumentoConferido, Carregando);

    /// <summary>Emite o termo (ou recupera o já emitido) e traz o texto e as declarações para a tela.</summary>
    public async Task CarregarAsync()
    {
        Carregando = true;
        Mensagem = string.Empty;
        MensagemEhErro = false;
        Avisar();

        await _portaDoContexto.WaitAsync();
        try
        {
            // A trilha de LEITURA: registrar UMA vez por abertura. Falhar aqui NÃO impede a coleta.
            try
            {
                await _acessos.RegistrarAsync(_pacienteId, Testemunha, OrigemAcessoProntuario.Documento);
            }
            catch (Exception ex)
            {
                Diagnostico.Registrar("Assinatura do paciente — acesso ao prontuário não pôde ser registrado", ex);
            }

            _documento = DocumentoExistenteId is int existente
                ? await _documentos.ObterAsync(existente)
                : _modeloId is int modelo
                    ? await _documentos.EmitirTermoProcedimentoAsync(
                        _pacienteId, modelo, _profissionalId, _agendamentoId, Testemunha)
                    // Sem modelo é o termo LGPD, montado das quatro finalidades.
                    : await _documentos.EmitirTermoConsentimentoAsync(
                        _pacienteId, _profissionalId, Testemunha);

            if (_documento is null)
            {
                MensagemEhErro = true;
                Mensagem = "O termo não foi encontrado. Feche e tente de novo.";
                return;
            }

            Titulo = _documento.TituloImpresso;
            Corpo = _documento.Corpo ?? string.Empty;
            Numero = _documento.Numero;

            Declaracoes.Clear();
            foreach (var item in _documento.Itens.OrderBy(i => i.Ordem))
                Declaracoes.Add(new DeclaracaoItem(item.Ordem, item.Descricao, item.Detalhe, item.Codigo) { Resposta = item.Quantidade });

            await CarregarAlergiasAsync();

            // O envio pelo WhatsApp só se OFERECE quando há para onde apontar o link.
            try
            {
                EnvioRemotoDisponivel =
                    _documento.AguardaAssinaturaDoPaciente
                    && await _parametros.ObterUrlPublicacaoAsync() is not null;
            }
            catch (Exception ex)
            {
                EnvioRemotoDisponivel = false;
                Diagnostico.Registrar("Termo pelo WhatsApp — configuração não pôde ser lida", ex);
            }

            // O paciente pode ter assinado no celular com a janela FECHADA: uma leitura única
            // recupera a resposta que já está guardada.
            if (EnvioRemotoDisponivel)
            {
                try
                {
                    if (await _coletaRemota.ColherRespostaAsync(_documento.Id) is { } prontas)
                        AplicarRespostaRemota(prontas);
                }
                catch (Exception ex)
                {
                    Diagnostico.Registrar("Termo pelo WhatsApp — resposta pendente não pôde ser lida", ex);
                }
            }
        }
        catch (Exception ex)
        {
            MensagemEhErro = true;
            Mensagem = ex.Message;
            Diagnostico.Registrar("Assinatura do paciente — Carregar", ex);
        }
        finally
        {
            _portaDoContexto.Release();
            Carregando = false;
            Avisar();
        }
    }

    /// <summary>
    /// Grava a assinatura. O PNG vem do canvas do formulário (ou do celular): é o único pedaço que
    /// depende do controle visual.
    /// </summary>
    public async Task<bool> ConfirmarAsync(byte[] tracoPng, int largura, int altura,
        Func<string, string, Task> avisar)
    {
        if (!SessaoUsuario.Atual.Pode(Permissao.ColherAssinaturaPaciente))
        {
            MensagemEhErro = true;
            Mensagem = "Você não tem permissão para colher a assinatura do paciente. "
                       + "Peça à direção o acesso \"Colher assinatura do paciente\".";
            Avisar();
            return false;
        }

        if (_documento is null)
        {
            MensagemEhErro = true;
            Mensagem = "O termo ainda não foi carregado. Feche e abra de novo.";
            Avisar();
            return false;
        }

        Carregando = true;
        Mensagem = string.Empty;
        MensagemEhErro = false;
        Avisar();

        // A vigia para AQUI, antes de qualquer serviço — e o porteiro segura a vez.
        PararVigia();
        await _portaDoContexto.WaitAsync();

        try
        {
            var respostas = Declaracoes.ToDictionary(d => d.Ordem, d => d.Resposta);

            await _assinaturas.ColherAsync(
                _documento.Id, tracoPng, largura, altura, respostas,
                DocumentoConferido, Testemunha,
                TemTracoRemoto
                    ? MeioAssinaturaPaciente.LinkRemoto
                    : MeioAssinaturaPaciente.NaClinica);

            // O circuito remoto fecha DEPOIS do selo; falha na remoção não desfaz o selo, mas
            // nunca passa calada.
            if (TemTracoRemoto)
            {
                PararVigia();
                var limpou = await _coletaRemota.ConcluirAsync(_documento.Id, Testemunha);
                if (!limpou)
                    await avisar(
                        "O link não saiu do ar",
                        "O termo foi assinado e gravado, mas o link do WhatsApp não pôde "
                        + "ser removido do armazenamento. Ele vence sozinho em 24 horas — "
                        + "mesmo assim, avise o suporte (a causa está na pasta de logs).");
            }

            Concluido = true;
            return true;
        }
        catch (Exception ex)
        {
            MensagemEhErro = true;
            Mensagem = ex.Message;
            Diagnostico.Registrar("Assinatura do paciente — Confirmar", ex);
            return false;
        }
        finally
        {
            _portaDoContexto.Release();
            Carregando = false;
            Avisar();
        }
    }

    /// <summary>Recusa sem permissão ou sem termo — as frases do WPF; nulo = pode perguntar o motivo.</summary>
    public string? RecusaDeRecusar()
    {
        if (!SessaoUsuario.Atual.Pode(Permissao.ColherAssinaturaPaciente))
            return "Você não tem permissão para registrar a recusa do paciente.";
        if (_documento is null)
            return "O termo ainda não foi carregado. Feche e abra de novo.";
        return null;
    }

    public void Falar(string mensagem, bool erro)
    {
        Mensagem = mensagem;
        MensagemEhErro = erro;
        Avisar();
    }

    /// <summary>Registra que o paciente RECUSOU, com o motivo obrigatório (pedido pelo formulário).</summary>
    public async Task<bool> RecusarAsync(string motivo)
    {
        if (RecusaDeRecusar() is { } recusa) { Falar(recusa, true); return false; }

        Carregando = true;
        Avisar();
        await _portaDoContexto.WaitAsync();
        try
        {
            await _assinaturas.RecusarAsync(_documento!.Id, motivo, Testemunha);
            Concluido = true;
            return true;
        }
        catch (Exception ex)
        {
            MensagemEhErro = true;
            Mensagem = ex.Message;
            Diagnostico.Registrar("Assinatura do paciente — Recusar", ex);
            return false;
        }
        finally
        {
            _portaDoContexto.Release();
            Carregando = false;
            Avisar();
        }
    }

    /// <summary>Marca todas as declarações como "Sim" — o caso normal, num clique (um GESTO de quem lê com o paciente).</summary>
    public void ConfirmarTodas()
    {
        foreach (var d in Declaracoes.Where(d => !d.AutorizacaoDeMensagens)) d.MarcarSim();
        Avisar();
    }

    public void Dispose()
    {
        PararVigia();
        _escopo.Dispose();
    }
}
