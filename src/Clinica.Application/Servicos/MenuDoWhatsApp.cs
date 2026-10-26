using Clinica.Application.Modelos;
using Clinica.Crm.Contracts;

namespace Clinica.Application.Servicos;

/// <summary>
/// Um item do menu de contexto (o botão direito, out/2026): a ação, o rótulo, o ícone da casa e, quando ela não cabe
/// agora, o MOTIVO — o item aparece desabilitado com a dica, em vez de sumir (a pessoa aprende onde a ação mora).
/// <see cref="GrupoNovo"/> desenha a linha divisória antes do item; <see cref="Perigo"/>, o vermelho.
/// </summary>
public sealed record ItemDeMenu<TAcao>(TAcao Acao, string Rotulo, string Icone, string? Motivo = null, bool Perigo = false,
    bool GrupoNovo = false)
{
    public bool Habilitado => Motivo is null;
}

/// <summary>O que o botão direito faz numa conversa da lista.</summary>
public enum AcaoNaConversa
{
    Abrir, Assumir, Devolver, MarcarNaoLida, MarcarLida, Transferir, Encerrar, Reabrir, NotaInterna,
    CopiarTelefone, AbrirFicha, ConferirFicha, Resumir
}

/// <summary>O que o botão direito faz numa mensagem da conversa.</summary>
public enum AcaoNaMensagem
{
    Responder, CopiarSelecao, CopiarTexto, CopiarComHora, Reenviar, UsarComoRascunho, SalvarArquivo, GuardarNaFicha,
    NotaSobre, DetalhesDaEntrega
}

/// <summary>Quem está vendo e o que o acesso dela alcança — o mesmo que os botões da tela já conferem.</summary>
/// <param name="PodeEscrever">O bit da aba (escrever no WhatsApp).</param>
/// <param name="PodeVerFicha">VerFichaPaciente: abrir a ficha.</param>
/// <param name="PodeGuardarNaFicha">EditarProntuario: "Guardar na ficha" (a regra do <c>GuardarNaFichaService</c>).</param>
/// <param name="SemDor">O @SemDor ligado (o Resumo).</param>
public sealed record AcessoNoMenu(bool PodeEscrever, bool PodeVerFicha, bool PodeGuardarNaFicha, bool SemDor);

/// <summary>
/// O menu do botão direito da aba WhatsApp (out/2026, pedido da direção). A regra de QUAL item aparece e QUANDO ele
/// cabe mora aqui (com teste), e a tela só desenha e liga cada ação ao que já existe — o mesmo handler do botão
/// do topo, do aviso ou do balão: o menu é ATALHO, nunca uma segunda regra (regra 1, uma porta por pergunta, vale
/// para a decisão; o atalho leva à mesma porta).
/// </summary>
public static class MenuDoWhatsApp
{
    public const string SoComVoce = "Só quem está com a conversa: assuma antes";

    /// <summary>
    /// O menu de uma conversa da lista. <paramref name="meuAgente"/> é o id da pessoa no CRM; <paramref name="fichaConferida"/>,
    /// se a conversa já tem a ficha ligada.
    /// </summary>
    public static IReadOnlyList<ItemDeMenu<AcaoNaConversa>> DaConversa(AppConversationSummary c, string? meuAgente, AcessoNoMenu acesso)
    {
        var minha = !string.IsNullOrWhiteSpace(meuAgente) && c.AgentId == meuAgente;
        var semEscrever = acesso.PodeEscrever ? null : "Seu acesso não alcança escrever no WhatsApp";
        var itens = new List<ItemDeMenu<AcaoNaConversa>> { new(AcaoNaConversa.Abrir, "Abrir", "chat") };

        if (c.Closed)
            itens.Add(new(AcaoNaConversa.Reabrir, "Reabrir", "atualizar", semEscrever, GrupoNovo: true));
        else if (minha)
            itens.Add(new(AcaoNaConversa.Devolver, "Devolver à fila", "devolver", semEscrever, GrupoNovo: true));
        else
            itens.Add(new(AcaoNaConversa.Assumir, "Assumir", "pessoa",
                semEscrever ?? (c.AgentId is not null ? "Está com outra pessoa: peça a transferência" : null), GrupoNovo: true));

        if (!c.Closed)
        {
            var pendente = c.PendingTransfer is not null ? "Já há um pedido de transferência aguardando o aceite" : null;
            itens.Add(new(AcaoNaConversa.Transferir, "Transferir…", "transferir", semEscrever ?? (minha ? pendente : SoComVoce)));
            itens.Add(new(AcaoNaConversa.NotaInterna, "Nota interna", "nota", semEscrever ?? (minha ? null : SoComVoce)));
        }

        // A marca de leitura é de quem vê, não da conversa: não pede o bit de escrever.
        itens.Add(c.Unread > 0
            ? new(AcaoNaConversa.MarcarLida, "Marcar como lida", "ticks", GrupoNovo: true)
            : new(AcaoNaConversa.MarcarNaoLida, "Marcar como não lida", "mensagem",
                c.LastIncoming == default ? "Ainda não há mensagem recebida" : null, GrupoNovo: true));

        var telefone = ConversasNaTela.TelefoneNaTela(c.Contact);
        itens.Add(new(AcaoNaConversa.CopiarTelefone, "Copiar telefone", "copiar", telefone.Length == 0 ? "Sem telefone" : null, GrupoNovo: true));
        if (c.PatientId is not null)
            itens.Add(new(AcaoNaConversa.AbrirFicha, "Abrir a ficha", "ficha", acesso.PodeVerFicha ? null : "Seu acesso não alcança a ficha do paciente"));
        else
            itens.Add(new(AcaoNaConversa.ConferirFicha, "Ligar a ficha…", "link", semEscrever));

        if (acesso.SemDor)
            itens.Add(new(AcaoNaConversa.Resumir, "Resumo do @SemDor", "raio", semEscrever));

        if (!c.Closed)
            itens.Add(new(AcaoNaConversa.Encerrar, "Encerrar…", "check", semEscrever ?? (minha ? null : SoComVoce), Perigo: true, GrupoNovo: true));
        return itens;
    }

    /// <summary>
    /// O menu de uma mensagem. <paramref name="selecao"/> é o texto selecionado DENTRO do balão no clique (vazio sem seleção);
    /// <paramref name="janelaLivre"/>, se a janela de 24 h aceita texto livre; <paramref name="sms"/>, o canal;
    /// <paramref name="minha"/>, se a conversa está com quem vê.
    /// </summary>
    public static IReadOnlyList<ItemDeMenu<AcaoNaMensagem>> DaMensagem(ItemDaConversa m, string? selecao, bool janelaLivre, bool sms,
        bool minha, bool fichaConferida, AcessoNoMenu acesso)
    {
        var enviada = m.Tipo == TipoNaLinhaDoTempo.Enviada;
        var semEscrever = acesso.PodeEscrever ? null : "Seu acesso não alcança escrever no WhatsApp";
        var itens = new List<ItemDeMenu<AcaoNaMensagem>>();

        itens.Add(new(AcaoNaMensagem.Responder, "Responder citando", "mensagem",
            semEscrever ?? (sms ? "O SMS não cita mensagens"
                : !janelaLivre ? "Janela de 24 h fechada: só modelo, que não cita"
                : string.IsNullOrEmpty(m.Id) ? "Esta mensagem ainda não tem o número do WhatsApp" : null)));

        if (!string.IsNullOrWhiteSpace(selecao))
            itens.Add(new(AcaoNaMensagem.CopiarSelecao, "Copiar a seleção", "copiar", GrupoNovo: true));
        var temTexto = !string.IsNullOrWhiteSpace(m.Texto);
        itens.Add(new(AcaoNaMensagem.CopiarTexto, "Copiar o texto", "copiar", temTexto ? null : "A mensagem não tem texto",
            GrupoNovo: string.IsNullOrWhiteSpace(selecao)));
        itens.Add(new(AcaoNaMensagem.CopiarComHora, "Copiar com data e hora", "relogio", temTexto ? null : "A mensagem não tem texto"));

        if (enviada)
        {
            if (m.Tick?.Estado is EstadoDaEntrega.Falhou or EstadoDaEntrega.NaoConfirmada)
                itens.Add(new(AcaoNaMensagem.Reenviar, "Reenviar…", "atualizar",
                    semEscrever ?? MotivoParaNaoReenviar(m, janelaLivre, minha), GrupoNovo: true));
            itens.Add(new(AcaoNaMensagem.UsarComoRascunho, "Usar como rascunho", "editar",
                semEscrever ?? (!temTexto || m.Modelo is not null ? "Só texto escrito vira rascunho (modelo se escolhe em Modelos)" : null),
                GrupoNovo: m.Tick?.Estado is not (EstadoDaEntrega.Falhou or EstadoDaEntrega.NaoConfirmada)));
        }

        if (m.Midia is not null)
        {
            itens.Add(new(AcaoNaMensagem.SalvarArquivo, "Salvar o arquivo…", "baixar", GrupoNovo: true));
            // A MESMA regra do botão do balão (GuardarNaFichaService.Cabe: foto que não é figurinha, ou documento).
            if (!enviada && GuardarNaFichaService.Cabe(m.Midia))
                itens.Add(new(AcaoNaMensagem.GuardarNaFicha, "Guardar na ficha", "ficha",
                    !acesso.PodeGuardarNaFicha ? GuardarNaFichaService.SemPermissao
                    : !fichaConferida ? GuardarNaFichaService.SemFicha : null));
        }

        itens.Add(new(AcaoNaMensagem.NotaSobre, "Nota interna sobre esta mensagem", "nota", semEscrever ?? (minha ? null : SoComVoce), GrupoNovo: true));
        if (enviada && m.Tick is not null)
            itens.Add(new(AcaoNaMensagem.DetalhesDaEntrega, "Detalhes da entrega", "info"));
        return itens;
    }

    /// <summary>
    /// Por que esta mensagem não pode ser reenviada agora — nulo quando pode. Reenviar é um envio NOVO do mesmo texto
    /// (o CRM nunca repete sozinho um envio incerto): só texto escrito, com a janela aberta e a conversa com quem vê.
    /// </summary>
    public static string? MotivoParaNaoReenviar(ItemDaConversa m, bool janelaLivre, bool minha)
        => m.Modelo is not null ? "Modelo se reenvia escolhendo de novo em Modelos"
            : m.Midia is not null || m.Anexo is not null ? "Arquivo se reenvia anexando de novo"
            : string.IsNullOrWhiteSpace(m.Texto) ? "A mensagem não tem texto"
            : !janelaLivre ? "Janela de 24 h fechada: só modelo"
            : !minha ? SoComVoce
            : null;

    /// <summary>"[21/10 07:34] Alice: Confirmar" — o que "Copiar com data e hora" põe na área de transferência.</summary>
    public static string ComDataEHora(ItemDaConversa m, string nomeDoPaciente, DateTimeOffset agora)
    {
        var quando = m.At is { } at ? at.ToOffset(agora.Offset).ToString("dd/MM HH:mm", System.Globalization.CultureInfo.InvariantCulture) : m.Hora;
        var quem = m.Tipo == TipoNaLinhaDoTempo.Recebida ? MensagensDeContato.PrimeiroNome(nomeDoPaciente) : m.Autor ?? "Clínica";
        return $"[{quando}] {quem}: {m.Texto}";
    }

    /// <summary>O começo da nota "sobre esta mensagem": a hora e o trecho, para a equipe saber do que se fala.</summary>
    public static string ComecoDaNota(ItemDaConversa m)
    {
        var trecho = string.Join(' ', (m.Texto ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (trecho.Length > 60) trecho = trecho[..59].TrimEnd() + "…";
        return trecho.Length == 0 ? $"Sobre a mensagem das {m.Hora}: " : $"Sobre a mensagem das {m.Hora} (\"{trecho}\"): ";
    }

    /// <summary>As linhas de "Detalhes da entrega": o estado, a hora e, quando o CRM disse, o motivo.</summary>
    public static IReadOnlyList<(string Rotulo, string Texto)> DetalhesDaEntrega(ItemDaConversa m)
    {
        var linhas = new List<(string, string)> { ("Situação", m.Tick?.Texto ?? "—"), ("Enviada", m.Hora) };
        if (m.Autor is { } autor) linhas.Add(("Por", autor));
        if (m.Modelo is { } modelo) linhas.Add(("Modelo", modelo));
        if (m.Erro is { } erro) linhas.Add(("O que fazer", erro));
        return linhas;
    }
}
