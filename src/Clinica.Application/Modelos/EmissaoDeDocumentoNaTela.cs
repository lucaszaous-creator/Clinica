using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// O que a EMISSÃO DE DOCUMENTO ESCRITO (receita, atestado, laudo, declaração de comparecimento
/// e pedido de exame) AFIRMA e DECIDE — out/2026, quando a janela WPF (<c>DocumentoWindow</c> +
/// <c>DocumentoEdicaoViewModel</c>) virou o formulário Blazor <c>DocumentoForm</c>.
///
/// Mora aqui, e não no Razor, pela regra da casa: o que a tela afirma precisa morar onde o
/// <c>dotnet test</c> alcança. E o que estava no ViewModel não era leiaute — era a conferência de
/// alergia na receita, a regra do CID sob autorização, a herança da sessão, a nova versão do
/// laudo e a montagem campo a campo do que vai ao serviço. Tudo isso foi PORTADO, não reescrito:
/// as frases e a ordem das recusas são as da janela WPF.
///
/// Os tipos que se escrevem à mão. Os montados do prontuário (relatório de evolução, termo de
/// consentimento, anamnese) não passam por aqui: o sistema os monta, e pedir para alguém
/// digitar o que o banco já sabe é o caminho mais curto para o documento sair errado.
/// </summary>
public static class EmissaoDeDocumentoNaTela
{
    /// <summary>Os tipos que se escrevem à mão — a régua de abas do topo da folha.</summary>
    public static IReadOnlyList<TipoDocumentoClinico> Tipos { get; } =
    [
        TipoDocumentoClinico.Receita,
        TipoDocumentoClinico.Atestado,
        TipoDocumentoClinico.LaudoMedico,
        TipoDocumentoClinico.Comparecimento,
        TipoDocumentoClinico.PedidoExame
    ];

    /// <summary>
    /// Por que este formulário NÃO abre — nulo quando abre.
    ///
    /// ⚠️ Ele não sabe montar o termo que o paciente assina (parcela 66): não copia o modelo, não
    /// traz as declarações e não grava <c>ModeloOrigemId</c>. Emitir por aqui produziria um papel
    /// numerado do mesmo ato, com a linha de assinatura em branco e a pendência do dia acesa —
    /// falha exibida como sucesso. A frase NOMEIA a porta certa de cada tipo (a lição da parcela
    /// 88: "no dia do procedimento" é verdade para o termo do BSV e MENTIRA para o termo LGPD).
    ///
    /// A nova versão é do MESMO papel e do MESMO paciente — a recusa mora no serviço, e aqui ela
    /// é dita antes de o formulário abrir, em vez de depois de a pessoa reescrever o laudo.
    /// </summary>
    public static string? RecusaDeAbertura(int pacienteId, TipoDocumentoClinico tipo, DocumentoClinico? versaoDe)
    {
        if (TipoDocumentoInfo.AssinadoPeloPaciente(tipo))
            return $"{TipoDocumentoInfo.Rotular(tipo)} é assinado pelo PACIENTE e não se "
                   + "emite por esta janela. "
                   + (tipo is TipoDocumentoClinico.Consentimento
                       ? "Colha-o na ficha do paciente, aba LGPD — lá dá para assinar no balcão "
                         + "ou enviar o link pelo WhatsApp."
                       : "Colha-o na ficha do paciente, aba Documentos, no dia do procedimento.");

        if (versaoDe is null) return null;
        if (!TipoDocumentoInfo.PermiteNovaVersao(versaoDe.Tipo))
            return $"{TipoDocumentoInfo.Rotular(versaoDe.Tipo)} não se altera por nova versão.";
        if (versaoDe.PacienteId != pacienteId)
            return $"O documento {versaoDe.Numero} é de outro paciente.";
        return null;
    }

    /// <summary>
    /// "Alterar (emitir nova versão)" de um documento da lista: as recusas que vinham ANTES de a
    /// janela abrir (<c>AcoesDoDocumento.NovaVersaoAsync</c>), na mesma ordem. Nulo = pode abrir.
    /// A permissão (o bit do TIPO) é conferida por quem chama, como sempre.
    /// </summary>
    public static string? RecusaDaNovaVersao(TipoDocumentoClinico tipo, string rotulo, string numero,
        bool cancelado, int? pacienteId)
    {
        if (!TipoDocumentoInfo.PermiteNovaVersao(tipo))
            return $"{rotulo} não se altera por nova versão: cancele com motivo e emita outro.";
        if (cancelado)
            return $"{numero} está cancelado — não há o que substituir. A 2ª via dele sai como foi emitido.";
        if (pacienteId is null)
            return $"{numero} não é de um paciente.";
        return null;
    }

    /// <summary>A confirmação da nova versão — o anterior foi cancelado no mesmo gravar.</summary>
    public static string NovaVersaoEmitida(string numeroAnterior)
        => $"Nova versão emitida. {numeroAnterior} foi cancelado e aponta para ela.";

    /// <summary>O título do papel, como a folha o escreve no topo.</summary>
    public static string TituloDocumento(TipoDocumentoClinico tipo) => tipo switch
    {
        TipoDocumentoClinico.Receita => "Receita médica",
        TipoDocumentoClinico.Atestado => "Atestado médico",
        TipoDocumentoClinico.Comparecimento => "Declaração de comparecimento",
        TipoDocumentoClinico.PedidoExame => "Pedido de exames",
        TipoDocumentoClinico.LaudoMedico => "Laudo médico",
        _ => TipoDocumentoInfo.Rotular(tipo)
    };

    /// <summary>O rótulo do campo de texto, dentro da folha.</summary>
    public static string RotuloDoTexto(TipoDocumentoClinico tipo) => tipo switch
    {
        TipoDocumentoClinico.Receita => "Prescrição e modo de usar",
        TipoDocumentoClinico.LaudoMedico => "Texto do laudo",
        _ => "Texto complementar · opcional"
    };

    /// <summary>A ajuda do campo vazio — no laudo, o roteiro que a perícia espera ler.</summary>
    public static string AjudaDoTexto(TipoDocumentoClinico tipo) => tipo switch
    {
        TipoDocumentoClinico.LaudoMedico => "Descreva o quadro clínico, achados e conclusão…",
        TipoDocumentoClinico.Receita => "Medicamento, dose e modo de usar…",
        _ => "Texto complementar (opcional)…"
    };

    /// <summary>Sem número inventado: ele é dado na emissão, por ano, e só lá.</summary>
    public static string NumeroDaFolha(string? numeroAnterior)
        => numeroAnterior is { Length: > 0 }
            ? $"Nº gerado ao emitir · substitui o {numeroAnterior}"
            : "Nº gerado ao emitir";

    /// <summary>
    /// "Paciente · da sessão de hoje, 14h00 · nova versão do 2026/0003 — ele será cancelado ao
    /// emitir": de quem é o papel, de onde ele vem e — na nova versão — o que acontece com o
    /// anterior. Quem altera precisa ler, ANTES de emitir, que o anterior vai ser cancelado.
    /// A procedência é medida contra a DATA DA EMISSÃO, como a janela WPF fazia.
    /// </summary>
    public static string Contexto(string? paciente, HerancaDaSessao? heranca, DateOnly data, string? numeroAnterior)
        => string.Join("   ·   ", new[]
        {
            paciente,
            heranca?.Procedencia(data) is { Length: > 0 } origem ? origem : $"Emissão de {data:dd/MM/yyyy}",
            numeroAnterior is { Length: > 0 } n ? $"nova versão do {n} — ele será cancelado ao emitir" : null
        }.Where(p => !string.IsNullOrWhiteSpace(p)));

    /// <summary>Documento e idade, pulando o que a ficha não tem (nunca "· anos ·").</summary>
    public static string Identificacao(Paciente? paciente, DateOnly hoje)
        => paciente is null ? string.Empty : string.Join("   ·   ", new[]
        {
            paciente.DocumentoFormatado is { Length: > 0 } documento ? $"CPF/doc. {documento}" : null,
            IdadeDoPaciente.Texto(paciente.DataNascimento, hoje),
            paciente.ConvenioNome
        }.Where(p => !string.IsNullOrWhiteSpace(p)));

    /// <summary>A linha do endereço na receita — o art. 35 exige, e a emissão pergunta quando falta.</summary>
    public static string EnderecoDaReceita(string? endereco)
        => string.IsNullOrWhiteSpace(endereco)
            ? "Endereço: será solicitado ao emitir."
            : $"Endereço: {endereco}";

    public const string TituloPerguntaEndereco = "Endereço para a receita";

    /// <summary>A pergunta do endereço que falta — antes de qualquer número ser gasto.</summary>
    public static string PerguntaEndereco(string paciente)
        => $"Qual é o endereço residencial de {paciente}? "
           + "Informe rua, número, complemento, bairro e cidade. "
           + "O endereço será guardado no cadastro para as próximas receitas.";

    public const string EmissaoAdiada =
        "Emissão adiada. A prescrição continua nesta janela para você completar o endereço.";

    public const string PreviaAberta =
        "Prévia aberta. Ela não tem número nem valor de documento — nada foi emitido.";

    public const string RecusaAlergia =
        "Esta prescrição bate com uma alergia registrada do paciente. "
        + "Confira o alerta e marque que você o viu antes de emitir.";

    public const string ConfirmacaoAlergia = "Eu vi o alerta de alergia e assumo esta prescrição";

    public const string AlergiasNaoConferidas =
        "Não foi possível conferir as alergias deste paciente agora — confira na "
        + "lista de problemas antes de prescrever.";

    public const string ExigenciasNaoConferidas =
        "Não foi possível conferir as exigências legais deste documento agora.";

    public const string CidOmitido = "O CID será omitido do documento enquanto não houver autorização.";

    public const string CidForaDaLista = "Fora da lista da clínica: confira o código antes de emitir.";

    public const string HorasDaSessao = "Horários preenchidos a partir do atendimento. Confira antes de emitir.";

    public const string IndicacaoDaSessao =
        "A indicação clínica do primeiro exame veio da sua hipótese desta sessão. Trocar aqui não muda o prontuário.";

    public const string ModeloSemNome = "Dê um nome ao modelo antes de guardá-lo.";

    /// <summary>O documento JÁ está emitido: a falha daqui é de impressão — senão alguém emite de novo.</summary>
    public static string FalhaDeImpressao(string erro, string numero)
        => $"{erro} O documento {numero} foi emitido e está na ficha do paciente.";

    /// <summary>
    /// O papel já saiu: o formulário não oferece emitir de novo (out/2026). Na janela WPF o botão
    /// continuava aceso depois de uma falha de impressão — o segundo clique gastaria outro número
    /// do mesmo ato, que é a família do incidente dos três encaixes (parcela 65).
    /// </summary>
    public static string JaEmitido(string numero)
        => $"O documento {numero} já foi emitido e está na ficha do paciente. Feche esta janela — a 2ª via sai pela lista.";

    /// <summary>O nome do arquivo do documento emitido, sem os caracteres que o Windows recusa.</summary>
    public static string NomeDoArquivo(TipoDocumentoClinico tipo, string numero)
        => NomeSeguro($"{TipoDocumentoInfo.Rotular(tipo)}-{numero.Replace('/', '-')}.pdf");

    /// <summary>O nome da prévia — sem número, porque ela não tem.</summary>
    public static string NomeDaPrevia(TipoDocumentoClinico tipo) => NomeSeguro($"Previa-{TipoDocumentoInfo.Rotular(tipo)}.pdf");

    private static string NomeSeguro(string bruto)
    {
        var limpo = bruto.Trim();
        foreach (var proibido in Path.GetInvalidFileNameChars()) limpo = limpo.Replace(proibido, '-');
        return limpo;
    }

    /// <summary>
    /// O fim da emissão pergunta "Enviar agora ao paciente?" — só para o papel que pode sair como
    /// arquivo E para quem tem o bit do TIPO (a metade visível do <c>OferecerEnviar</c> das
    /// outras portas). O documento JÁ está emitido: nada no envio desfaz isso.
    /// </summary>
    public static bool OferecerEnvio(TipoDocumentoClinico tipo, bool assinado, bool cancelado, bool podeMexerNoTipo)
        => EnvioAoPacienteService.PodeEnviar(tipo, assinado, cancelado) && podeMexerNoTipo;

    /// <summary>
    /// As exigências legais que a FOLHA mostra (parcela 43). A conferência é pura e roda
    /// enquanto se escreve: o endereço que falta é resolvível em trinta segundos ANTES de
    /// emitir, e inútil depois. Duas ficam de fora:
    /// - o art. 13 (assinatura qualificada): este formulário emite a via para impressão, e o
    ///   aviso da entrega eletrônica pertence ao fluxo de certificado;
    /// - na receita, o endereço e "escreva a prescrição": essas respostas têm lugar na própria
    ///   receita (o campo de texto e a pergunta de endereço antes da emissão).
    /// </summary>
    public static IReadOnlyList<string> ExigenciasDaFolha(IEnumerable<ExigenciaLegal> faltas, bool receita)
        => faltas
            .Where(f => f.Fundamento != ConformidadeDocumentoClinico.Art13)
            .Where(f => !(receita && (f.Frase.Contains("endereço residencial") || f.Frase.StartsWith("Escreva a prescrição"))))
            .Select(f => f.Frase)
            .ToList();

    /// <summary>
    /// A leitura da conferência de alergia (parcela 40): o contexto permanente (alergias e
    /// medicação contínua) e as colisões entre o que foi escrito e a alergia registrada.
    /// </summary>
    public static (IReadOnlyList<string> Alertas, IReadOnlyList<string> Colisoes) LerConferencia(ConferenciaPrescricao conferencia)
    {
        var alertas = new List<string>();
        if (ProblemaPacienteService.ResumirAlergias(conferencia.Alergias) is { } alergias) alertas.Add(alergias);
        foreach (var medicacao in conferencia.MedicacoesEmUso) alertas.Add($"Em uso contínuo: {medicacao.Descricao}");
        var colisoes = conferencia.Alertas
            .Where(a => a.Gravidade == GravidadePrescricao.Alergia)
            .Select(a => $"“{a.Item}” — {a.Motivo}")
            .ToList();
        return (alertas, colisoes);
    }

    /// <summary>A prévia do modelo escolhido na aba Modelos.</summary>
    public static string PreviaDoModelo(ModeloDocumento? modelo)
        => modelo is { } m ? TextoReceituario.DoModelo(m) : "Escolha um modelo para conferir o conteúdo.";

    /// <summary>A busca na lista de modelos: por nome, sem caixa e sem acento (o controle WPF fazia assim).</summary>
    public static IReadOnlyList<ModeloDocumento> FiltrarModelos(IEnumerable<ModeloDocumento> modelos, string? termo)
    {
        var t = termo?.Trim() ?? string.Empty;
        var comparar = System.Globalization.CultureInfo.GetCultureInfo("pt-BR").CompareInfo;
        return modelos
            .Where(m => t.Length == 0 || comparar.IndexOf(m.Nome, t,
                System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0)
            .ToList();
    }
}

/// <summary>Uma linha do documento em edição (o exame + a quantidade + a indicação clínica).</summary>
public sealed class LinhaDoDocumento
{
    private string _descricao = string.Empty;
    private string? _detalhe;

    /// <summary>Editar o texto descarta a formatação antiga (o contrato do texto formatado).</summary>
    public string Descricao
    {
        get => _descricao;
        set
        {
            var novo = value ?? string.Empty;
            if (novo == _descricao) return;
            _descricao = novo;
            DescricaoFormatada = null;
        }
    }

    public string? DescricaoFormatada { get; set; }
    public string? Quantidade { get; set; }

    public string? Detalhe
    {
        get => _detalhe;
        set
        {
            if (value == _detalhe) return;
            _detalhe = value;
            DetalheFormatado = null;
        }
    }

    public string? DetalheFormatado { get; set; }

    internal ItemDocumento ParaDocumento() => new()
    {
        Descricao = Descricao, DescricaoFormatada = DescricaoFormatada,
        Quantidade = Quantidade,
        Detalhe = Detalhe, DetalheFormatado = DetalheFormatado
    };
}

/// <summary>
/// O PAPEL EM EDIÇÃO — o estado e as regras que eram do <c>DocumentoEdicaoViewModel</c> WPF, sem
/// leitura de banco (quem lê é o formulário, por escopo de operação). Cada setter que tinha um
/// <c>On…Changed</c> no ViewModel carrega a MESMA consequência aqui:
/// - trocar o TEXTO desfaz a confirmação de alergia (ela não descreve o texto novo) e reconfere
///   as exigências legais;
/// - a colisão que SOME leva junto a confirmação — senão a próxima receita passaria com um
///   "eu vi" que ninguém deu;
/// - trocar o CID desliga a marca "veio da sessão" quando o código deixou de ser o da hipótese;
/// - trocar o TIPO reaplica a herança da sessão (só em campo vazio).
/// </summary>
public sealed class RascunhoDeDocumento
{
    private TipoDocumentoClinico _tipo;
    private string? _corpo;
    private string? _cid;
    private bool _colideComAlergia;
    private Profissional? _profissional;
    private bool _conferenciaLegalLigada;

    /// <summary>Abre o papel. Lança <see cref="InvalidOperationException"/> com a frase de <see cref="EmissaoDeDocumentoNaTela.RecusaDeAbertura"/>.</summary>
    public RascunhoDeDocumento(int pacienteId, TipoDocumentoClinico tipoInicial = TipoDocumentoClinico.Receita,
        HerancaDaSessao? heranca = null, DocumentoClinico? versaoDe = null, DateOnly? hoje = null)
    {
        if (EmissaoDeDocumentoNaTela.RecusaDeAbertura(pacienteId, tipoInicial, versaoDe) is { } recusa)
            throw new InvalidOperationException(recusa);

        PacienteId = pacienteId;
        Heranca = heranca;
        VersaoDe = versaoDe;
        _tipo = versaoDe?.Tipo ?? tipoInicial;
        Data = hoje ?? DateOnly.FromDateTime(DateTime.Today);
        Itens.Add(new LinhaDoDocumento());

        // Alterar começa do que o laudo DIZ, não de uma folha em branco: quem corrige uma linha
        // não pode ter de reescrever as outras vinte.
        if (versaoDe is not null)
        {
            Titulo = versaoDe.Titulo;
            _corpo = versaoDe.Corpo;
            CorpoFormatado = versaoDe.CorpoFormatado;
            Observacoes = versaoDe.Observacoes;
            ObservacoesFormatadas = versaoDe.ObservacoesFormatadas;
            _cid = versaoDe.Cid;
            CidAutorizado = versaoDe.CidAutorizado;
        }

        AplicarHeranca();
    }

    public int PacienteId { get; }

    /// <summary>
    /// O que a SESSÃO ABERTA já sabe. Nulo fora de um atendimento (a central, a ficha) — e aí o
    /// papel abre em branco. ⚠️ Só se herda o que está GRAVADO num campo próprio (o CID e a
    /// hipótese, os carimbos do horário): a receita abre em branco de propósito.
    /// </summary>
    public HerancaDaSessao? Heranca { get; }

    /// <summary>O laudo que esta emissão SUBSTITUI (a nova versão). Nulo na emissão comum.</summary>
    public DocumentoClinico? VersaoDe { get; }

    /// <summary>O tipo só se troca na emissão comum: a nova versão é do mesmo papel.</summary>
    public bool PodeTrocarTipo => VersaoDe is null;

    /// <summary>O paciente inteiro — a conferência legal lê o endereço dele. Nulo até a carga chegar.</summary>
    public Paciente? Paciente { get; set; }

    public TipoDocumentoClinico Tipo => _tipo;

    /// <summary>
    /// Escolhe OUTRO papel. Devolve falso quando nada mudou (o tipo é o mesmo, ou a nova versão,
    /// que não troca). O que a sessão tinha a dar para ele é outra coisa, e as marcas do papel
    /// anterior mentiriam sobre este — por isso a herança é reaplicada.
    /// </summary>
    public bool TrocarTipo(TipoDocumentoClinico tipo)
    {
        if (!PodeTrocarTipo || tipo == _tipo) return false;
        _tipo = tipo;
        AplicarHeranca();
        ConferirLegalmente();
        return true;
    }

    public DateOnly Data { get; set; }
    public string? Titulo { get; set; }

    public string? Corpo
    {
        get => _corpo;
        set
        {
            if (value == _corpo) return;
            _corpo = value;
            // A confirmação anterior não descreve o novo texto. A emissão reconfere antes de
            // numerar, inclusive quando a alteração veio de um modelo aplicado.
            AlergiaConferida = false;
            ColideComAlergia = false;
            ColisoesAlergia.Clear();
            ConferirLegalmente();
        }
    }

    public string? CorpoFormatado { get; set; }
    public string? Observacoes { get; set; }
    public string? ObservacoesFormatadas { get; set; }

    public string? DiasAfastamentoTexto { get; set; }

    public string? Cid
    {
        get => _cid;
        set
        {
            if (value == _cid) return;
            _cid = value;
            // Digitou outro código: a marca deixa de descrever o que está no campo, e "veio da
            // sua hipótese" sobre um CID escrito à mão é pior do que frase nenhuma.
            if (CidVeioDaSessao && !string.Equals(value?.Trim(), Heranca?.Cid?.Trim(), StringComparison.OrdinalIgnoreCase))
                CidVeioDaSessao = false;
        }
    }

    public bool CidAutorizado { get; set; }

    public DateOnly? PeriodoInicio { get; set; }
    public string? HoraChegadaTexto { get; set; }
    public string? HoraSaidaTexto { get; set; }

    public List<LinhaDoDocumento> Itens { get; } = [];

    // ===================== quem assina =====================

    /// <summary>Quem assina. Por padrão, QUEM ESTÁ LOGADO — e só ele (<see cref="AssinanteDoDocumento"/>).</summary>
    public Profissional? Profissional
    {
        get => _profissional;
        set
        {
            if (ReferenceEquals(value, _profissional)) return;
            _profissional = value;
            ConferirLegalmente();
        }
    }

    /// <summary>A frase da abertura quando o assinante ficou em branco (login sem profissional, ou inativo).</summary>
    public string? AvisoAssinante { get; private set; }

    /// <summary>Resolve o assinante proposto: quem está logado, nunca "o primeiro da lista".</summary>
    public void DefinirAssinante(IEnumerable<Profissional> ativos, int? profissionalDoLogin)
    {
        var escolha = AssinanteDoDocumento.Resolver(ativos, profissionalDoLogin);
        AvisoAssinante = escolha.Aviso;
        Profissional = escolha.Profissional;
    }

    /// <summary>Por que o "Emitir" está apagado por falta de quem assina — a metade VISÍVEL da barreira.</summary>
    public string? MotivoSemAssinante => AssinanteDoDocumento.MotivoSemAssinante(Tipo, Profissional, AvisoAssinante);

    public bool TemAssinante => Profissional is not null;
    public string NomeDoAssinante => Profissional?.Nome ?? string.Empty;
    public string RegistroDoAssinante => Profissional?.RegistroConselho is { Length: > 0 } r ? r : "Registro no conselho não cadastrado";
    public string FaltaAssinante => AvisoAssinante ?? "Escolha quem assina este documento.";

    // ===================== a conferência clínica (parcela 40) =====================
    //
    // A conferência mora no ponto por onde TODA receita passa — nas portas da Recepção e do
    // Consultório. Checagem de segurança que só existe em uma delas é o defeito de novo, com a
    // agravante de dar a impressão de estar coberto.

    /// <summary>Alergias e medicação contínua do paciente — contexto permanente da folha.</summary>
    public List<string> AlertasClinicos { get; } = [];

    /// <summary>O que a conferência achou ao comparar os itens escritos com as alergias.</summary>
    public List<string> ColisoesAlergia { get; } = [];

    /// <summary>
    /// Um item escrito bate com alergia registrada. Não impede — exige que alguém diga que viu.
    /// A colisão que some leva junto a confirmação.
    /// </summary>
    public bool ColideComAlergia
    {
        get => _colideComAlergia;
        set
        {
            if (value == _colideComAlergia) return;
            _colideComAlergia = value;
            if (!value) AlergiaConferida = false;
        }
    }

    /// <summary>O profissional marcou que viu o alerta e assume a prescrição.</summary>
    public bool AlergiaConferida { get; set; }

    /// <summary>O que vai à conferência de alergia: as linhas escritas e, na receita, o texto livre.</summary>
    public IReadOnlyList<string> Escritos()
    {
        var escritos = Itens
            .Where(i => MostraItens && !string.IsNullOrWhiteSpace(i.Descricao))
            .Select(i => $"{i.Descricao} {i.Detalhe}".Trim())
            .ToList();
        if (ReceitaLivre && !string.IsNullOrWhiteSpace(Corpo)) escritos.Add(Corpo);
        return escritos;
    }

    /// <summary>Aplica o que a conferência devolveu (<c>PrescricaoService.ConferirAsync</c>).</summary>
    public void AplicarConferencia(ConferenciaPrescricao conferencia)
    {
        var (alertas, colisoes) = EmissaoDeDocumentoNaTela.LerConferencia(conferencia);
        AlertasClinicos.Clear();
        AlertasClinicos.AddRange(alertas);
        ColisoesAlergia.Clear();
        ColisoesAlergia.AddRange(colisoes);
        ColideComAlergia = conferencia.ExigeConfirmacao;
    }

    /// <summary>
    /// A conferência não rodou. Ela é AJUDA — um banco lento não pode impedir um atestado —, mas
    /// também não fica muda: tela sem aviso se lê como "está tudo certo".
    /// </summary>
    public void ConferenciaClinicaFalhou()
    {
        AlertasClinicos.Clear();
        AlertasClinicos.Add(EmissaoDeDocumentoNaTela.AlergiasNaoConferidas);
        ColideComAlergia = false;
    }

    /// <summary>A recusa da alergia no clique de emitir — nulo quando passa.</summary>
    public string? RecusaDaAlergia() => ColideComAlergia && !AlergiaConferida ? EmissaoDeDocumentoNaTela.RecusaAlergia : null;

    // ===================== a conformidade legal (parcela 43) =====================

    /// <summary>O que falta para o documento cumprir a lei, já na frase que a folha mostra.</summary>
    public List<string> ExigenciasLegais { get; } = [];

    /// <summary>
    /// Liga a conferência legal (o formulário chama ao terminar a carga — a janela WPF não
    /// conferia enquanto carregava) e confere já.
    /// </summary>
    public void LigarConferenciaLegal()
    {
        _conferenciaLegalLigada = true;
        ConferirLegalmente();
    }

    /// <summary>Confere de novo, agora (o clique de emitir chama depois de completar o endereço).</summary>
    public void ConferirLegalmente()
    {
        if (!_conferenciaLegalLigada) return;
        try
        {
            var faltas = ConformidadeDocumentoClinico.Conferir(DocumentoParaConferencia(), false);
            ExigenciasLegais.Clear();
            ExigenciasLegais.AddRange(EmissaoDeDocumentoNaTela.ExigenciasDaFolha(faltas, ReceitaLivre));
        }
        catch (Exception ex)
        {
            // Conferência é ajuda: ela não pode impedir alguém de emitir. Mas também não fica
            // muda — tela sem aviso se lê como "está tudo certo" quando a checagem nem rodou.
            Diagnostico.Registrar("Documento clínico — conferência legal não pôde ser feita", ex);
            ExigenciasLegais.Clear();
            ExigenciasLegais.Add(EmissaoDeDocumentoNaTela.ExigenciasNaoConferidas);
        }
    }

    // ===================== a herança da sessão =====================

    /// <summary>O CID no campo veio da hipótese desta sessão.</summary>
    public bool CidVeioDaSessao { get; private set; }

    /// <summary>Os horários da declaração vieram dos carimbos do horário.</summary>
    public bool HorasVieramDaSessao { get; private set; }

    /// <summary>A indicação clínica da primeira linha veio da hipótese desta sessão.</summary>
    public bool IndicacaoVeioDaSessao { get; private set; }

    /// <summary>A frase que explica o CID herdado, com a origem por extenso.</summary>
    public string ExplicacaoDoCid =>
        Heranca is null || string.IsNullOrWhiteSpace(Heranca.Hipotese)
            ? "Veio da hipótese desta sessão. Trocar aqui não muda o prontuário."
            : $"Veio da sua hipótese: {Heranca.Hipotese}. Trocar aqui não muda o prontuário.";

    /// <summary>
    /// Copia da sessão o que ESTE tipo de papel sabe aproveitar — e só em campo VAZIO: a pessoa
    /// pode ter escrito antes de trocar o tipo, e sobrescrever seria desfazer o trabalho dela.
    /// </summary>
    private void AplicarHeranca()
    {
        CidVeioDaSessao = false;
        HorasVieramDaSessao = false;
        IndicacaoVeioDaSessao = false;
        if (Heranca is null) return;

        switch (Tipo)
        {
            case TipoDocumentoClinico.Atestado or TipoDocumentoClinico.LaudoMedico
                when !string.IsNullOrWhiteSpace(Heranca.Cid) && string.IsNullOrWhiteSpace(Cid):
                Cid = Heranca.Cid;
                CidVeioDaSessao = true;
                break;

            // A saída pode não existir ainda (a sessão não foi encerrada): a declaração sai só
            // com a chegada, que é a verdade disponível.
            case TipoDocumentoClinico.Comparecimento
                when Heranca.Chegada is { } chegada && string.IsNullOrWhiteSpace(HoraChegadaTexto):
                HoraChegadaTexto = chegada.ToString("HH':'mm");
                if (Heranca.Saida is { } saida && string.IsNullOrWhiteSpace(HoraSaidaTexto))
                    HoraSaidaTexto = saida.ToString("HH':'mm");
                HorasVieramDaSessao = true;
                break;

            // A hipótese vira a INDICAÇÃO CLÍNICA da primeira linha — a coluna que o laboratório lê.
            case TipoDocumentoClinico.PedidoExame when !string.IsNullOrWhiteSpace(Heranca.Hipotese):
                var primeira = Itens.FirstOrDefault();
                if (primeira is not null && string.IsNullOrWhiteSpace(primeira.Detalhe))
                {
                    primeira.Detalhe = Heranca.Hipotese;
                    IndicacaoVeioDaSessao = true;
                }
                break;
        }
    }

    // ===================== o que a folha mostra =====================

    public bool ReceitaLivre => Tipo == TipoDocumentoClinico.Receita;

    /// <summary>O pedido de exame é lista; os outros, não.</summary>
    public bool MostraItens => TipoDocumentoInfo.ExigeItens(Tipo);

    public bool MostraAtestado => Tipo == TipoDocumentoClinico.Atestado;

    /// <summary>O laudo: o texto é do médico, e o CID sai com a mesma autorização do atestado.</summary>
    public bool MostraLaudo => Tipo == TipoDocumentoClinico.LaudoMedico;

    /// <summary>Os papéis que levam CID — e, com ele, a autorização expressa do paciente.</summary>
    public bool MostraCid => MostraAtestado || MostraLaudo;

    public bool MostraComparecimento => Tipo == TipoDocumentoClinico.Comparecimento;

    public string TituloDocumento => EmissaoDeDocumentoNaTela.TituloDocumento(Tipo);

    public string TituloFolha => (string.IsNullOrWhiteSpace(Titulo) ? TituloDocumento : Titulo.Trim()).ToUpperInvariant();

    public string Contexto => EmissaoDeDocumentoNaTela.Contexto(Paciente?.Nome, Heranca, Data, VersaoDe?.Numero);

    /// <summary>O nome do paciente na folha — "carregando" enquanto a ficha não chegou.</summary>
    public string PacienteDaFolha => Paciente?.Nome ?? "Carregando o paciente…";

    public string IdentificacaoDaFolha(DateOnly hoje) => EmissaoDeDocumentoNaTela.Identificacao(Paciente, hoje);

    public string DataDaFolha => $"{Data:dd/MM/yyyy}";

    public string NumeroDaFolha => EmissaoDeDocumentoNaTela.NumeroDaFolha(VersaoDe?.Numero);

    public string RotuloDoTexto => EmissaoDeDocumentoNaTela.RotuloDoTexto(Tipo);

    public string AjudaDoTexto => EmissaoDeDocumentoNaTela.AjudaDoTexto(Tipo);

    public string EnderecoDaReceita => EmissaoDeDocumentoNaTela.EnderecoDaReceita(Paciente?.Endereco);

    /// <summary>
    /// O que o código digitado quer dizer — vazio fora do atalho da clínica. É a metade que pega
    /// o dígito trocado ("M54.4" no lugar de "M54.5" também existe, e sai IMPRESSO no atestado).
    /// </summary>
    public string DescricaoCid => CatalogoCid.Descrever(Cid) ?? string.Empty;

    /// <summary>"Fora da lista" só com código digitado e sem descrição — com o campo vazio não há o que conferir.</summary>
    public bool CidForaDaLista => !string.IsNullOrWhiteSpace(Cid) && string.IsNullOrEmpty(DescricaoCid);

    /// <summary>O CID preenchido SEM autorização não sai impresso — a folha avisa antes de o papel sair.</summary>
    public bool AvisaCidOmitido => !string.IsNullOrWhiteSpace(Cid) && !CidAutorizado;

    // ===================== as linhas =====================

    public void AdicionarItem() => Itens.Add(new LinhaDoDocumento());

    /// <summary>Remove a linha; a lista nunca fica sem nenhuma (sempre há onde escrever).</summary>
    public void RemoverItem(LinhaDoDocumento? item)
    {
        if (item is null) return;
        Itens.Remove(item);
        if (Itens.Count == 0) Itens.Add(new LinhaDoDocumento());
    }

    // ===================== a emissão =====================

    /// <summary>O texto obrigatório que falta — receita e laudo não saem em branco. Nulo = passa.</summary>
    public string? RecusaDoTexto()
    {
        if (ReceitaLivre && string.IsNullOrWhiteSpace(Corpo)) return "Escreva a prescrição antes de emitir a receita.";
        if (MostraLaudo && string.IsNullOrWhiteSpace(Corpo)) return "Escreva o laudo antes de emiti-lo.";
        return null;
    }

    /// <summary>Pode emitir: nada ocupado, a alergia confirmada quando colide e alguém para assinar.</summary>
    public bool PodeEmitir(bool ocupado) => !ocupado && (!ColideComAlergia || AlergiaConferida) && MotivoSemAssinante is null;

    /// <summary>
    /// Uma cópia do que está na folha, com paciente e profissional LIGADOS, só para a conferência
    /// legal. Separada de <see cref="MontarDocumento"/> de propósito: aquela valida e ESTOURA, e
    /// uma conferência que derruba a tela enquanto a pessoa digita seria pior que não conferir.
    /// </summary>
    public DocumentoClinico DocumentoParaConferencia()
    {
        var documento = new DocumentoClinico
        {
            Tipo = Tipo,
            PacienteId = PacienteId,
            Paciente = Paciente,
            ProfissionalId = Profissional?.Id,
            Profissional = Profissional,
            Corpo = Corpo, CorpoFormatado = CorpoFormatado,
            Data = Data
        };
        if (MostraItens)
            foreach (var i in Itens.Where(i => !string.IsNullOrWhiteSpace(i.Descricao)))
                documento.Itens.Add(i.ParaDocumento());
        return documento;
    }

    /// <summary>
    /// O que vai ao serviço — campo a campo, e o que ficar de fora é descartado em silêncio (o
    /// lugar 3 da auditoria de linha). Lança <see cref="InvalidOperationException"/> quando um
    /// campo não se lê (dias de afastamento que não são número, hora que não é hora).
    /// </summary>
    public DocumentoClinico MontarDocumento()
    {
        var documento = new DocumentoClinico
        {
            Tipo = Tipo,
            PacienteId = PacienteId,
            ProfissionalId = Profissional?.Id,

            // ⚠️ A PROCEDÊNCIA: sem estas duas linhas o documento nasce AVULSO, e a coluna "de
            // onde veio" das listas escreveria "avulso" para toda receita emitida na consulta.
            AgendamentoId = Heranca?.AgendamentoId,
            EvolucaoId = Heranca?.EvolucaoId,

            Data = Data,
            Titulo = Titulo,
            Corpo = Corpo, CorpoFormatado = CorpoFormatado,
            Observacoes = Observacoes, ObservacoesFormatadas = ObservacoesFormatadas
        };

        if (MostraItens)
            foreach (var i in Itens.Where(i => !string.IsNullOrWhiteSpace(i.Descricao)))
                documento.Itens.Add(i.ParaDocumento());

        if (MostraAtestado && !string.IsNullOrWhiteSpace(DiasAfastamentoTexto))
        {
            if (!int.TryParse(DiasAfastamentoTexto, out var dias))
                throw new InvalidOperationException("Os dias de afastamento devem ser um número.");
            documento.DiasAfastamento = dias;
        }

        if (MostraCid)
        {
            documento.Cid = Cid;
            documento.CidAutorizado = CidAutorizado;
        }

        if (MostraAtestado || MostraComparecimento)
            documento.PeriodoInicio = PeriodoInicio;

        if (MostraComparecimento)
        {
            documento.HoraChegada = LerHora(HoraChegadaTexto, "chegada");
            documento.HoraSaida = LerHora(HoraSaidaTexto, "saída");
        }

        return documento;
    }

    private static TimeOnly? LerHora(string? texto, string qual)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        if (TimeOnly.TryParse(texto, out var hora)) return hora;
        throw new InvalidOperationException($"A hora de {qual} não foi entendida — use 14:30.");
    }

    // ===================== os modelos =====================

    /// <summary>
    /// Aplicar um modelo fora da receita SUBSTITUI o que está na folha — e pergunta antes, quando
    /// há o que perder. Na receita ele ACRESCENTA, e não pergunta.
    /// </summary>
    public bool AplicarModeloPedeConfirmacao
        => !ReceitaLivre && (!string.IsNullOrWhiteSpace(Corpo) || Itens.Any(i => !string.IsNullOrWhiteSpace(i.Descricao)));

    /// <summary>Traz o modelo para a folha e devolve a frase que a folha mostra.</summary>
    public string AplicarModelo(ModeloDocumento modelo)
    {
        if (ReceitaLivre)
        {
            var conteudo = TextoReceituario.ConteudoDoModelo(modelo);
            var combinado = TextoFormatado.Juntar([(Corpo, CorpoFormatado), conteudo]);
            Corpo = combinado.Texto;
            CorpoFormatado = combinado.Formato;
            return $"Modelo {modelo.Nome} acrescentado. Revise antes de emitir.";
        }

        Titulo = modelo.Titulo;
        Corpo = modelo.Corpo;
        CorpoFormatado = modelo.CorpoFormatado;

        Itens.Clear();
        foreach (var i in modelo.Itens.OrderBy(i => i.Ordem).ThenBy(i => i.Id))
            Itens.Add(new LinhaDoDocumento
            {
                Descricao = i.Descricao, DescricaoFormatada = i.DescricaoFormatada,
                Quantidade = i.Quantidade,
                Detalhe = i.Detalhe, DetalheFormatado = i.DetalheFormatado
            });
        if (Itens.Count == 0) Itens.Add(new LinhaDoDocumento());
        return $"Modelo \"{modelo.Nome}\" aplicado.";
    }

    /// <summary>O que está na folha como modelo (o rascunho que a clínica inteira usa). <paramref name="id"/> 0 = novo.</summary>
    public ModeloDocumento MontarModelo(string nome, int id)
    {
        var modelo = new ModeloDocumento
        {
            Id = id, Tipo = Tipo,
            Nome = nome,
            Titulo = Titulo,
            Corpo = Corpo, CorpoFormatado = CorpoFormatado
        };
        foreach (var i in Itens.Where(i => MostraItens && !string.IsNullOrWhiteSpace(i.Descricao)))
            modelo.Itens.Add(new ItemModelo
            {
                Descricao = i.Descricao, DescricaoFormatada = i.DescricaoFormatada,
                Quantidade = i.Quantidade,
                Detalhe = i.Detalhe, DetalheFormatado = i.DetalheFormatado
            });
        return modelo;
    }
}
