using System.Globalization;
using System.Text.RegularExpressions;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Crm.Contracts;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// O que a tela sabe da conversa para preencher um modelo: o nome da ficha (ou da conversa),
/// a próxima sessão (modalidade e hora), o caso do Acompanhamento e o documento, quando há.
/// </summary>
public sealed record ContextoDoModelo(string? NomeDoPaciente, DateOnly Hoje, ModalidadeAtendimento? Atendimento = null,
    DateTime? Sessao = null, LinhaAcompanhamento? Caso = null, string? Documento = null);

/// <summary>Uma variável <c>{{n}}</c> do modelo: o número, o rótulo do campo e o valor sugerido (vazio = a pessoa preenche).</summary>
public sealed record VariavelDoModelo(int Numero, string Rotulo, string Valor);

/// <summary>Um trecho da prévia; <see cref="Variavel"/> marca o que veio de uma variável (a tela o destaca).</summary>
public sealed record TrechoDaPrevia(string Texto, bool Variavel);

/// <summary>
/// O FORMULÁRIO DO MODELO DO WHATSAPP (out/2026, PR 2 do WhatsApp nativo): os valores iniciais
/// das variáveis a partir do contexto, a prévia e o que impede o envio. O CRM só diz QUANTAS
/// variáveis o modelo tem (<see cref="AppTemplate.Parameters"/>), não o que cada uma é — então o
/// significado de cada <c>{{n}}</c> vem de uma tabela POR NOME de modelo conhecido. Modelo
/// desconhecido, ou conhecido com outra quantidade de variáveis (a Meta aprovou uma versão
/// nova e a tabela ficou velha), sai com tudo vazio e rótulos "Variável n": preencher errado
/// seria pior que pedir para a pessoa digitar. Nada é inventado: sem o dado no contexto, o
/// campo fica vazio e o envio espera.
/// </summary>
public static class ModeloDoWhatsApp
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public const string LembreteSessao = "lembrete_sessao";
    public const string RetornoTratamento = "retorno_tratamento";
    public const string DocumentoPronto = "documento_pronto";
    public const string PrimeiroContato = "primeiro_contato";

    private const string PrimeiroNome = "Primeiro nome", Atendimento = "Atendimento", DiaEHora = "Dia e hora",
        Tratamento = "Tratamento", Dias = "Dias", Documento = "Documento", Assunto = "Assunto";

    // Os rótulos dos modelos APROVADOS na Meta (os mesmos do template-picker.js do CRM). Os de
    // horário usam a sessão do contexto; os que só a recepção sabe ficam vazios para ela preencher.
    private const string NovoDiaEHora = "Novo dia e horário", DiaPerdido = "Dia e horário que perdeu",
        DiaLivre = "Dia e horário livre", OQueFalta = "O que falta", Orcamento = "Orçamento";

    /// <summary>Os modelos aprovados na Meta que a retenção usa (out/2026, CRM em produção — docs/producao-out2026.md do CRM).</summary>
    public const string RecallReal = "semdor_recall", IniciarAtendimentoReal = "semdor_iniciar_atendimento";

    /// <summary>
    /// O que cada variável dos modelos conhecidos é, na ordem do <c>{{n}}</c>. Os quatro do
    /// mockup aprovado, mais os dois cujo texto está no próprio CRM (<c>RichMessaging.cs</c>):
    /// <c>recall_semdor</c> ("Olá, {{1}}!…") e <c>semdor_confirmacao_24h</c> ("Olá, {{1}}!… agendada para {{2}}").
    /// </summary>
    private static readonly Dictionary<string, string[]> Conhecidos = new(StringComparer.OrdinalIgnoreCase)
    {
        [LembreteSessao] = [PrimeiroNome, Atendimento, DiaEHora],
        [RetornoTratamento] = [PrimeiroNome, Tratamento, Dias],
        [DocumentoPronto] = [PrimeiroNome, Documento],
        [PrimeiroContato] = [PrimeiroNome, Assunto],
        ["recall_semdor"] = [PrimeiroNome],
        ["semdor_confirmacao_24h"] = [PrimeiroNome, DiaEHora],
        // Os aprovados na Meta em produção (out/2026). Tabela do CRM: wwwroot/template-picker.js (KNOWN).
        [IniciarAtendimentoReal] = [PrimeiroNome, Assunto],
        ["semdor_retorno_contato"] = [PrimeiroNome],
        ["semdor_retomar_conversa"] = [PrimeiroNome, Assunto],
        ["semdor_horario_remarcado"] = [PrimeiroNome, NovoDiaEHora],
        ["semdor_falta_reagendar"] = [PrimeiroNome, DiaPerdido],
        ["semdor_vaga_disponivel"] = [PrimeiroNome, DiaLivre],
        ["semdor_agendamento_confirmado"] = [PrimeiroNome, DiaEHora],
        ["semdor_informacao_pendente"] = [PrimeiroNome, OQueFalta],
        ["semdor_orcamento"] = [PrimeiroNome, Orcamento],
        ["semdor_documento"] = [PrimeiroNome],
        ["semdor_lembrete_sessao"] = [PrimeiroNome, DiaEHora],
        [RecallReal] = [PrimeiroNome],
    };

    private static readonly Regex Variavel = new(@"\{\{(\d+)\}\}", RegexOptions.CultureInvariant);

    /// <summary>Os rótulos do modelo conhecido com esta quantidade de variáveis, ou nulo.</summary>
    public static IReadOnlyList<string>? Rotulos(AppTemplate modelo)
        => Conhecidos.TryGetValue(modelo.Name ?? "", out var r) && r.Length == modelo.Parameters ? r : null;

    /// <summary>
    /// Os valores iniciais de cada variável. Primeiro nome da ficha; atendimento e tratamento
    /// em minúscula com a sigla preservada ("sessão de BSV", "acupuntura", "consulta"); dia e
    /// hora por extenso ("hoje às 14:00", "quinta-feira, 06/11 às 14:00"); os dias só do
    /// RECALL (o modelo diz "dias desde a última sessão" — o do novo no BSV conta a indicação);
    /// o assunto do novo no BSV é o agendamento da primeira sessão.
    /// </summary>
    public static IReadOnlyList<VariavelDoModelo> Sugestoes(AppTemplate modelo, ContextoDoModelo contexto)
    {
        var quantas = Math.Max(0, modelo.Parameters);
        var rotulos = Rotulos(modelo);
        return Enumerable.Range(1, quantas).Select(n => rotulos is null
            ? new VariavelDoModelo(n, $"Variável {n}", "")
            : new VariavelDoModelo(n, rotulos[n - 1], Valor(rotulos[n - 1], contexto))).ToList();
    }

    private static string Valor(string rotulo, ContextoDoModelo c) => rotulo switch
    {
        PrimeiroNome => ConversasNaTela.PrimeiroNomeOu(c.NomeDoPaciente, ""),
        Atendimento => c.Atendimento is { } m ? NaFrase(m) : "",
        DiaEHora or NovoDiaEHora or DiaPerdido => c.Sessao is { } s ? MensagensDeContato.QuandoPorExtenso(s, c.Hoje) : "",
        Tratamento => c.Caso is { } caso ? NaFrase(caso.Tipo == TipoAcompanhamento.NovoBsv && !EhBsv(caso.Modalidade) ? ModalidadeAtendimento.BsvApenas : caso.Modalidade)
            : c.Atendimento is { } a ? NaFrase(a) : "",
        Dias => c.Caso is { Tipo: TipoAcompanhamento.Recall } r ? r.Dias.ToString(CultureInfo.InvariantCulture) : "",
        Documento => c.Documento?.Trim() ?? "",
        Assunto => c.Caso is { Tipo: TipoAcompanhamento.NovoBsv } ? "o agendamento da sua primeira sessão de BSV" : "",
        _ => ""
    };

    /// <summary>"sessão de BSV", "sessão de BSV e acupuntura", "acupuntura", "consulta" — a modalidade dentro da frase ao paciente.</summary>
    public static string NaFrase(ModalidadeAtendimento m) => m switch
    {
        ModalidadeAtendimento.BsvApenas => "sessão de BSV",
        ModalidadeAtendimento.BsvComAcupuntura => "sessão de BSV e acupuntura",
        ModalidadeAtendimento.AcupunturaSimples or ModalidadeAtendimento.AcupunturaComEletro => "acupuntura",
        _ => "consulta"
    };

    private static bool EhBsv(ModalidadeAtendimento m) => m is ModalidadeAtendimento.BsvApenas or ModalidadeAtendimento.BsvComAcupuntura;

    /// <summary>
    /// O texto que vai ao paciente, com os valores aplicados; variável sem valor fica como
    /// <c>{{n}}</c> (a prévia mostra o que falta, nunca um buraco).
    /// </summary>
    public static string Previa(AppTemplate modelo, IReadOnlyList<string> valores)
        => string.Concat(Trechos(modelo, valores).Select(t => t.Texto));

    /// <summary>A prévia em trechos, para a tela destacar o que veio de variável.</summary>
    public static IReadOnlyList<TrechoDaPrevia> Trechos(AppTemplate modelo, IReadOnlyList<string> valores)
    {
        var texto = modelo.Text ?? "";
        var trechos = new List<TrechoDaPrevia>();
        var desde = 0;
        foreach (Match m in Variavel.Matches(texto))
        {
            if (m.Index > desde) trechos.Add(new(texto[desde..m.Index], false));
            var n = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            var valor = n >= 1 && n <= valores.Count ? valores[n - 1]?.Trim() : null;
            trechos.Add(new(string.IsNullOrEmpty(valor) ? m.Value : valor, true));
            desde = m.Index + m.Length;
        }
        if (desde < texto.Length) trechos.Add(new(texto[desde..], false));
        return trechos;
    }

    /// <summary>
    /// O que impede o envio, na frase da tela, ou nulo: modelo que o CRM não aceita (o
    /// <see cref="AppTemplate.Reason"/> dele) e variável em branco (nomeada pelo rótulo).
    /// </summary>
    public static string? Impedimento(AppTemplate modelo, IReadOnlyList<VariavelDoModelo> variaveis, IReadOnlyList<string> valores)
    {
        if (!modelo.Supported)
            return string.IsNullOrWhiteSpace(modelo.Reason) ? "Este modelo não pode ser enviado por aqui." : modelo.Reason.Trim();
        foreach (var v in variaveis)
            if (v.Numero > valores.Count || string.IsNullOrWhiteSpace(valores[v.Numero - 1]))
                return $"Preencha o campo \"{v.Rotulo}\".";
        return null;
    }

    // ---------------------------------------------------------------- a lista dos modelos (25/10/2026)

    /// <summary>
    /// O nome que a recepção lê, por modelo aprovado — o técnico (<c>semdor_confirmacao_24h</c>) fica só na
    /// dica do botão, para quem configura. Os de produção (prefixo <c>semdor_</c>) e os do mockup.
    /// </summary>
    private static readonly Dictionary<string, string> NomesNaTela = new(StringComparer.OrdinalIgnoreCase)
    {
        [LembreteSessao] = "Lembrete da sessão (com o atendimento)",
        ["semdor_lembrete_sessao"] = "Lembrete da sessão",
        ["semdor_confirmacao_24h"] = "Confirmação da consulta",
        ["semdor_agendamento_confirmado"] = "Horário marcado",
        ["semdor_horario_remarcado"] = "Horário remarcado",
        ["semdor_falta_reagendar"] = "Faltou · remarcar",
        ["semdor_vaga_disponivel"] = "Vaga disponível",
        [RecallReal] = "Convite para voltar",
        [RetornoTratamento] = "Convite para voltar (com os dias)",
        ["recall_semdor"] = "Convite para novo agendamento",
        [PrimeiroContato] = "Primeiro contato",
        [IniciarAtendimentoReal] = "Começar o atendimento",
        [DocumentoPronto] = "Documento pronto",
        ["semdor_documento"] = "Documento enviado",
        ["semdor_retorno_contato"] = "Retorno do seu contato",
        ["semdor_retomar_conversa"] = "Retomar a conversa",
        ["semdor_informacao_pendente"] = "Informação pendente",
        ["semdor_orcamento"] = "Orçamento",
    };

    /// <summary>O que cada variável vira no texto da lista: "Olá, [nome]! … {{2}}" → "… [data]".</summary>
    private static readonly Dictionary<string, string> MarcasNaLista = new(StringComparer.Ordinal)
    {
        [PrimeiroNome] = "nome", [Atendimento] = "atendimento", [DiaEHora] = "data", [Tratamento] = "tratamento",
        [Dias] = "dias", [Documento] = "documento", [Assunto] = "assunto", [NovoDiaEHora] = "nova data",
        [DiaPerdido] = "data perdida", [DiaLivre] = "data livre", [OQueFalta] = "o que falta", [Orcamento] = "orçamento",
    };

    /// <summary>
    /// O nome do modelo na tela: o da tabela; desconhecido, o técnico sem o prefixo e sem os sublinhados
    /// ("semdor_aviso_feriado" → "Aviso feriado") — nunca o identificador cru.
    /// </summary>
    public static string NomeNaTela(string? nome)
    {
        var n = (nome ?? "").Trim();
        if (NomesNaTela.TryGetValue(n, out var rotulo)) return rotulo;
        if (n.StartsWith("semdor_", StringComparison.OrdinalIgnoreCase)) n = n["semdor_".Length..];
        n = n.Replace('_', ' ').Trim();
        return n.Length == 0 ? "Modelo aprovado" : char.ToUpper(n[0], PtBr) + n[1..];
    }

    /// <summary>
    /// O texto do modelo na lista: cada <c>{{n}}</c> vira o nome da variável entre colchetes ("[nome]",
    /// "[data]"), pelos mesmos rótulos do formulário (<see cref="Rotulos"/>); modelo desconhecido, "[variável n]".
    /// </summary>
    public static string TextoNaLista(AppTemplate modelo)
    {
        var rotulos = Rotulos(modelo);
        return Variavel.Replace(modelo.Text ?? "", m =>
        {
            var n = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            var rotulo = rotulos is not null && n >= 1 && n <= rotulos.Count ? rotulos[n - 1] : null;
            return rotulo is not null && MarcasNaLista.TryGetValue(rotulo, out var marca) ? $"[{marca}]" : $"[variável {n}]";
        });
    }

    /// <summary>Os modelos que confirmam a presença: os primeiros quando a pessoa tem horário HOJE.</summary>
    private static readonly HashSet<string> DeConfirmacao = new(StringComparer.OrdinalIgnoreCase)
        { LembreteSessao, "semdor_lembrete_sessao", "semdor_confirmacao_24h" };

    /// <summary>
    /// A ordem da lista, por pertinência: os que a clínica aceita enviar primeiro; depois, na conversa
    /// de um caso aberto da retenção, os convites (<see cref="WhatsAppDaRecepcaoService.ModelosDeConvite"/>);
    /// com horário HOJE, os de confirmação; o resto pelo nome que a recepção lê.
    /// </summary>
    public static IReadOnlyList<AppTemplate> Ordenar(IEnumerable<AppTemplate> modelos, ContextoDoModelo contexto)
    {
        var retencao = contexto.Caso is { Pendente: true };
        var horarioHoje = contexto.Sessao is { } s && DateOnly.FromDateTime(s) == contexto.Hoje;
        int Pertinencia(AppTemplate m)
            => retencao && WhatsAppDaRecepcaoService.ModelosDeConvite.Contains(m.Name ?? "") ? 0
                : horarioHoje && DeConfirmacao.Contains(m.Name ?? "") ? 0 : 1;
        return modelos.OrderByDescending(m => m.Supported).ThenBy(Pertinencia)
            .ThenBy(m => NomeNaTela(m.Name), StringComparer.Create(PtBr, ignoreCase: true)).ToList();
    }

    /// <summary>
    /// Os botões do modelo aprovado (<see cref="AppTemplate.Buttons"/>), para a tela desenhá-los sob o balão
    /// da mensagem que saiu por ele — como o WhatsApp mostra ao paciente. Sem o modelo na lista do CRM, nenhum.
    /// </summary>
    public static IReadOnlyList<string> Botoes(string? nome, IEnumerable<AppTemplate>? modelos)
        => string.IsNullOrWhiteSpace(nome) || modelos is null ? []
            : modelos.FirstOrDefault(m => string.Equals(m.Name, nome, StringComparison.OrdinalIgnoreCase))?.Buttons?
                .Where(b => !string.IsNullOrWhiteSpace(b)).Select(b => b.Trim()).ToList() ?? [];

    /// <summary>A escolha que vai no <c>AppReply.Template</c>: nome, idioma e os valores, sem espaço sobrando.</summary>
    public static AppTemplateSelection Selecao(AppTemplate modelo, IReadOnlyList<string> valores)
        => new(modelo.Name, modelo.Language, valores.Take(Math.Max(0, modelo.Parameters)).Select(v => v?.Trim() ?? "").ToArray());
}
