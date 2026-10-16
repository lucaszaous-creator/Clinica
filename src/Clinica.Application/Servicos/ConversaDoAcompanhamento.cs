using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Um degrau do trilho de progresso, já com o que se escreve debaixo dele.</summary>
public sealed record DegrauDoTrilho(string Rotulo, string? Detalhe, bool Feito, bool Agora);

/// <summary>Uma linha da "visão da equipe": o que cada responsável tem nas mãos.</summary>
public sealed record CargaDoResponsavel(string Responsavel, int Pendentes, int Atrasados, int Agendados, int Voltaram);

/// <summary>Os encerramentos agrupados pelo motivo (a outra metade da visão da gestão).</summary>
public sealed record EncerramentosPorMotivo(string Motivo, int Quantidade);

/// <summary>
/// Os filtros raros de "Mais filtros" que o <see cref="FiltroAcompanhamento"/> não carrega:
/// vieram da tela WPF (out/2026) e não podiam se perder na troca — tentativas mínimas, o
/// período do último contato, os dias sem vir e a etapa. Moram à parte para o recorte
/// principal continuar sendo o do funil.
/// </summary>
public sealed record FiltroExtraAcompanhamento(
    int TentativasMinimas = 0,
    DateOnly? ContatoDesde = null,
    DateOnly? ContatoAte = null,
    int? DiasMinimos = null,
    int? DiasMaximos = null,
    EtapaAcompanhamento? Etapa = null)
{
    public static readonly FiltroExtraAcompanhamento Nenhum = new();

    /// <summary>Quantos estão ligados — somado ao <see cref="FiltroAcompanhamento.Extras"/> no botão "Mais filtros (N)".</summary>
    public int Contagem => (TentativasMinimas > 0 ? 1 : 0) + (ContatoDesde is null && ContatoAte is null ? 0 : 1)
        + (DiasMinimos is null && DiasMaximos is null ? 0 : 1) + (Etapa is null ? 0 : 1);

    /// <summary>A frase do impedimento (nulo = os valores fazem sentido). Intervalo invertido não filtra nada: diz.</summary>
    public string? Impedimento()
    {
        if (TentativasMinimas < 0) return "As tentativas não podem ser negativas.";
        if (DiasMinimos < 0 || DiasMaximos < 0) return "Os dias sem vir não podem ser negativos.";
        if (DiasMinimos is { } a && DiasMaximos is { } b && b < a) return "O máximo de dias sem vir é menor que o mínimo.";
        if (ContatoDesde is { } d && ContatoAte is { } f && f < d) return "O fim do período do último contato é antes do começo.";
        return null;
    }

    public bool Aceita(LinhaAcompanhamento l)
        => l.Tentativas >= TentativasMinimas
           && (ContatoDesde is not { } desde || l.UltimoContato is { } u1 && DateOnly.FromDateTime(u1) >= desde)
           && (ContatoAte is not { } ate || l.UltimoContato is { } u2 && DateOnly.FromDateTime(u2) <= ate)
           && (DiasMinimos is not { } min || l.Dias >= min)
           && (DiasMaximos is not { } max || l.Dias <= max)
           && (Etapa is not { } e || l.Etapa == e);
}

/// <summary>
/// A CONVERSA DO PACIENTE (out/2026, desenho A do Relacionamento): o formulário que abre por
/// cima da fila. O que ela AFIRMA e o que ela GRAVA mora aqui, puro e testado — o Razor só
/// desenha. As regras do serviço (versão, idempotência, quem pode encerrar sem tentativa)
/// continuam sendo do serviço; aqui fica o que a tela decide ANTES de chamá-lo.
/// </summary>
public static class ConversaDoAcompanhamento
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    /// <summary>Mínimo de letras da justificativa de encerrar e de reabrir — o MESMO do serviço.</summary>
    public const int MinimoJustificativa = 5;

    /// <summary>
    /// O desfecho pede por onde se falou? "Não respondeu", "pediu outra data" e "quer marcar"
    /// são respostas a um CONTATO — sem canal, o serviço grava uma etapa sem tentativa, e o
    /// contador de tentativas (que decide o "Tentar de novo") ficaria mentindo. "Aguardando o
    /// plano" é conversa com a operadora, e "não quer voltar" pode ser decisão da gestão.
    /// </summary>
    public static bool ExigeCanal(DesfechoContato d)
        => d is DesfechoContato.NaoRespondeu or DesfechoContato.PediuOutraData or DesfechoContato.QuerMarcar;

    /// <summary>A observação é obrigatória (encerrar exige justificativa e motivo; o resto é opcional).</summary>
    public static bool ExigeObservacao(DesfechoContato d) => d == DesfechoContato.NaoQuerVoltar;

    /// <summary>
    /// Monta o pedido do "Registrar". Devolve a frase do impedimento quando falta algo — a
    /// tela a mostra INLINE, perto do campo, e não grava nada.
    /// </summary>
    public static (AtualizarAcompanhamento? Pedido, string? Impedimento) Pedido(LinhaAcompanhamento linha,
        DesfechoContato? desfecho, DateOnly hoje, DateOnly? data, CanalContato? canal, string? observacao,
        int? motivoId, int usuarioId, Guid idempotencia)
    {
        if (desfecho is not { } d) return (null, "Escolha como foi o contato.");
        if (linha.MotivoEncerramento is not null) return (null, "Este acompanhamento está encerrado — reabra-o antes de registrar.");
        var obs = observacao?.Trim() ?? string.Empty;
        if (ExigeCanal(d) && canal is null) return (null, "Diga por onde foi o contato (WhatsApp, telefone ou presencial).");
        if (d == DesfechoContato.NaoQuerVoltar)
        {
            if (motivoId is null) return (null, "Escolha o motivo do encerramento.");
            if (obs.Length < MinimoJustificativa) return (null, $"Escreva a justificativa do encerramento (pelo menos {MinimoJustificativa} letras).");
        }
        var (gravacao, impedimento) = AcompanhamentoNaTela.Gravar(d, hoje, data);
        if (gravacao is null) return (null, impedimento);
        return (new AtualizarAcompanhamento(idempotencia, linha.Versao, usuarioId, gravacao.ProximoContato,
            gravacao.Etapa, canal, obs, gravacao.Encerrar, motivoId), null);
    }

    /// <summary>Reabrir um caso encerrado: justificativa e o dia do próximo contato, como o serviço pede.</summary>
    public static (AtualizarAcompanhamento? Pedido, string? Impedimento) Reabertura(LinhaAcompanhamento linha,
        DateOnly hoje, DateOnly? data, string? justificativa, int? motivoId, int usuarioId, Guid idempotencia)
    {
        var obs = justificativa?.Trim() ?? string.Empty;
        if (obs.Length < MinimoJustificativa) return (null, $"Escreva por que o acompanhamento volta (pelo menos {MinimoJustificativa} letras).");
        var proximo = data ?? hoje;
        if (proximo < hoje) return (null, "O próximo contato não pode ser no passado.");
        return (new AtualizarAcompanhamento(idempotencia, linha.Versao, usuarioId, proximo, EtapaAcompanhamento.AContatar,
            null, obs, Reabrir: true, MotivoId: motivoId), null);
    }

    /// <summary>
    /// O trilho de cinco degraus, com o que se escreve debaixo de cada um: quando sumiu (ou
    /// foi indicado), quem assumiu, quantas tentativas, a data marcada e o dia em que voltou.
    /// O degrau "agora" é o próximo a cumprir — num caso que voltou, nenhum.
    /// </summary>
    public static IReadOnlyList<DegrauDoTrilho> Trilho(LinhaAcompanhamento l, int usuarioId)
    {
        var rotulos = AcompanhamentoNaTela.Degraus(l.Tipo);
        var alcancado = AcompanhamentoNaTela.Degrau(l);
        var proximo = l.VoltouEm is not null || !l.Pendente && l.AgendadoPara is null ? -1 : alcancado + 1;
        string?[] detalhes =
        [
            l.ReferenciaEm.ToString("dd/MM", PtBr),
            l.ResponsavelId is null ? null : l.ResponsavelId == usuarioId ? "por você" : "por " + PrimeiroNome(l.Responsavel),
            l.Tentativas == 0 ? null : $"{l.Tentativas}ª tentativa",
            l.AgendadoPara?.ToString("dd/MM 'às' HH:mm", PtBr),
            l.VoltouEm?.ToString("dd/MM", PtBr)
        ];
        return rotulos.Select((r, i) => new DegrauDoTrilho(r, i <= alcancado ? detalhes[i] : null, i <= alcancado, i == proximo)).ToList();
    }

    /// <summary>"Você" para quem está usando; o primeiro nome para os outros; "ninguém" sem dono.</summary>
    public static string Dono(LinhaAcompanhamento l, int usuarioId)
        => l.ResponsavelId is null ? "ninguém" : l.ResponsavelId == usuarioId ? "Você" : PrimeiroNome(l.Responsavel);

    /// <summary>As iniciais do avatar do responsável (duas letras).</summary>
    public static string Iniciais(string nome)
    {
        var partes = nome.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => char.IsLetter(p[0]) && (p.Length > 2 || char.IsUpper(p[0]))).ToArray();
        if (partes.Length == 0) return "?";
        return partes.Length == 1 ? partes[0][..1].ToUpper(PtBr) : (partes[0][..1] + partes[^1][..1]).ToUpper(PtBr);
    }

    /// <summary>"Sem vir" da linha: dias sem retornar (recall) ou há quanto foi indicado (BSV).</summary>
    public static string TempoTexto(LinhaAcompanhamento l) => l.Dias == 1 ? "1 dia" : $"{l.Dias} dias";

    /// <summary>A linha do cabeçalho da conversa: o que, a última sessão e com quem.</summary>
    public static string Cabecalho(LinhaAcompanhamento l)
        => l.Tipo == TipoAcompanhamento.Recall
            ? $"Recall de {l.ModalidadeTexto} · última sessão em {l.ReferenciaEm.ToString("dd/MM/yyyy", PtBr)}"
              + (l.Profissional is { Length: > 0 } p && p != "—" ? $" com {p}" : string.Empty)
            : $"Indicação de BSV em {l.ReferenciaEm.ToString("dd/MM/yyyy", PtBr)}"
              + (l.Profissional is { Length: > 0 } q && q != "—" ? $" por {q}" : string.Empty);

    /// <summary>A situação da autorização de mensagem, como a ficha lateral escreve.</summary>
    public static string Autorizacao(LinhaAcompanhamento l)
        => string.IsNullOrWhiteSpace(l.Telefone) ? "sem telefone no cadastro"
            : l.Consentimento ? "autorizada" : "não autorizada (LGPD)";

    /// <summary>A coluna do CSV: quem pode receber mensagem, com o impedimento junto (sem ela a planilha vira lista de disparo).</summary>
    public static string PodeReceberMensagem(LinhaAcompanhamento l)
        => string.IsNullOrWhiteSpace(l.Telefone) ? "não — sem telefone" : !l.Consentimento ? "não — sem consentimento (LGPD)" : "sim";

    /// <summary>O resumo da lista: quantos à vista, de quantos da fila, e se há filtro escondendo gente.</summary>
    public static string Resumo(int mostrados, int naFila, bool filtrada)
    {
        var plural = mostrados == 1 ? "paciente" : "pacientes";
        return filtrada ? $"{mostrados} de {naFila} na fila · filtrada" : $"{mostrados} {plural} na fila";
    }

    /// <summary>
    /// O vazio que separa "não há ninguém" de "o filtro escondeu todo mundo" — um filtro
    /// esquecido que responde "fila vazia" faz a recepção dar o dia por resolvido.
    /// </summary>
    public static (string Titulo, string Descricao) Vazio(int naFila, bool filtrada, FaixaAcompanhamento? faixa)
        => filtrada && naFila > 0
            ? ("Ninguém bate com o filtro", "Há pacientes na fila, mas nenhum com este recorte. Limpe os filtros para ver todos.")
            : faixa switch
            {
                FaixaAcompanhamento.Voltou => ("Ninguém voltou ainda", "Quem retornar depois do contato aparece aqui."),
                FaixaAcompanhamento.Agendado => ("Nenhum agendado", "Quem marcar horário pela conversa aparece aqui até a sessão."),
                FaixaAcompanhamento.Encerrado => ("Nenhum encerrado", "Os casos encerrados com motivo ficam aqui e podem ser reabertos."),
                _ => ("Fila em dia", "Ninguém esperando contato nesta fila.")
            };

    /// <summary>
    /// A visão da equipe (o expander "Visão da gestão" da tela WPF): o que cada responsável tem
    /// nas mãos e os encerramentos por motivo, sobre a fila do tipo escolhido.
    /// </summary>
    public static (IReadOnlyList<CargaDoResponsavel> Responsaveis, IReadOnlyList<EncerramentosPorMotivo> Encerramentos)
        VisaoDaEquipe(IEnumerable<LinhaAcompanhamento> linhas, TipoAcompanhamento tipo, DateOnly hoje)
    {
        var lista = linhas.Where(l => l.Tipo == tipo).ToList();
        var responsaveis = lista.GroupBy(l => l.Responsavel)
            .Select(g => new CargaDoResponsavel(g.Key, g.Count(x => x.Pendente), g.Count(x => x.Atrasado),
                g.Count(x => AcompanhamentoNaTela.Faixa(x, hoje) == FaixaAcompanhamento.Agendado),
                g.Count(x => x.VoltouEm is not null)))
            .OrderByDescending(c => c.Pendentes).ThenBy(c => c.Responsavel, StringComparer.CurrentCultureIgnoreCase).ToList();
        var encerramentos = lista.Where(l => l.MotivoEncerramento is not null).GroupBy(l => l.Motivo ?? "Sem motivo")
            .Select(g => new EncerramentosPorMotivo(g.Key, g.Count()))
            .OrderByDescending(e => e.Quantidade).ThenBy(e => e.Motivo, StringComparer.CurrentCultureIgnoreCase).ToList();
        return (responsaveis, encerramentos);
    }

    private static string PrimeiroNome(string nome)
        => nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? nome;
}
