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

/// <summary>O que o botão direito faz numa conversa da lista — e o ⋮ do cabeçalho da conversa aberta (Buscar, Dados do contato).</summary>
public enum AcaoNaConversa
{
    Abrir, Assumir, Devolver, MarcarNaoLida, MarcarLida, Transferir, Encerrar, Reabrir, NotaInterna,
    CopiarTelefone, AbrirFicha, ConferirFicha, Resumir, Buscar, DadosDoContato, Fixar, Desafixar, Etiquetas
}

/// <summary>O que o botão direito faz numa mensagem da conversa.</summary>
public enum AcaoNaMensagem
{
    Responder, CopiarSelecao, CopiarTexto, CopiarComHora, Reenviar, UsarComoRascunho, SalvarArquivo, GuardarNaFicha,
    NotaSobre, DetalhesDaEntrega, PerguntarAoProfissional, Reagir, Estrelar
}

/// <summary>O que o clipe 📎 do campo oferece (F1, nov/2026): o arquivo, os modelos e as respostas rápidas, que eram três botões escritos.</summary>
public enum AcaoDoClipe { Arquivo, Modelos, Rapidas, Endereco, Contato, Servicos }

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
    /// <summary>Todos escrevem (nov/2026, decisão da direção): responder, anotar e reenviar valem para toda a equipe;
    /// transferir e encerrar continuam de quem está com a conversa.</summary>
    public const string SoComVoce = "Só quem está com a conversa transfere e encerra";

    /// <summary>
    /// O menu de uma conversa da lista. <paramref name="meuAgente"/> é o id da pessoa no CRM; <paramref name="fichaConferida"/>,
    /// se a conversa já tem a ficha ligada.
    /// </summary>
    public static IReadOnlyList<ItemDeMenu<AcaoNaConversa>> DaConversa(AppConversationSummary c, string? meuAgente, AcessoNoMenu acesso,
        int fixadas = 0, int maximoDeFixadas = 3)
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
            itens.Add(new(AcaoNaConversa.NotaInterna, "Nota para a equipe", "nota", semEscrever));
        }

        // A marca de leitura é de quem vê, não da conversa: não pede o bit de escrever.
        itens.Add(c.Unread > 0
            ? new(AcaoNaConversa.MarcarLida, "Marcar como lida", "ticks", GrupoNovo: true)
            : new(AcaoNaConversa.MarcarNaoLida, "Marcar como não lida", "mensagem",
                c.LastIncoming == default ? "Ainda não há mensagem recebida" : null, GrupoNovo: true));

        // Fixar (F3): por pessoa, até o máximo do CRM; a fixada sempre desafixa. Não pede o bit de escrever (é a lista de quem vê).
        itens.Add(c.Pinned
            ? new(AcaoNaConversa.Desafixar, "Desafixar", "fixado")
            : new(AcaoNaConversa.Fixar, "Fixar no topo", "fixado", ConversasNaTela.MotivoParaNaoFixar(false, fixadas, maximoDeFixadas)));

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
    /// O clipe 📎 do campo (F1, nov/2026, como o do WhatsApp): anexar arquivo e os modelos só no WhatsApp (o SMS não tem
    /// nem um nem outro), o arquivo só com os anexos ligados no CRM, e as respostas rápidas sempre. Os dois últimos também
    /// saem pelo "/" no começo do campo (<see cref="ConversasNaTela.OpcoesDaBarra"/>).
    /// </summary>
    public static IReadOnlyList<ItemDeMenu<AcaoDoClipe>> DoClipe(bool sms, bool anexosLigados, AcessoNoMenu acesso,
        AppShareOptions? compartilhar = null, bool janelaLivre = true)
    {
        var semEscrever = acesso.PodeEscrever ? null : "Seu acesso não alcança escrever no WhatsApp";
        var itens = new List<ItemDeMenu<AcaoDoClipe>>();
        if (!sms)
        {
            itens.Add(new(AcaoDoClipe.Arquivo, "Documento, foto ou áudio", "clipe",
                semEscrever ?? (anexosLigados ? null : "O envio de arquivos está desligado no CRM")));
            itens.Add(new(AcaoDoClipe.Modelos, "Modelos aprovados", "modelo", semEscrever));
        }
        itens.Add(new(AcaoDoClipe.Rapidas, "Respostas rápidas", "raio"));
        // Endereço, contato do médico e serviços (F3, v4): o que a gestão configurou no CRM. Só na janela de 24 h — a Meta não
        // aceita fora dela, e o item diz por quê em poucas palavras.
        if (!sms && compartilhar is { } o)
        {
            var fora = semEscrever ?? (janelaLivre ? null : "Só dentro da janela de 24 h");
            var primeiro = true;
            if (o.Location is not null) { itens.Add(new(AcaoDoClipe.Endereco, "Endereço da clínica", "link", fora, GrupoNovo: primeiro)); primeiro = false; }
            if (o.Contacts.Any(c => !c.Archived)) { itens.Add(new(AcaoDoClipe.Contato, "Contato do médico…", "pessoa", fora, GrupoNovo: primeiro)); primeiro = false; }
            if (o.Services.Any(x => !x.Archived)) itens.Add(new(AcaoDoClipe.Servicos, "Serviços…", "lista", fora, GrupoNovo: primeiro));
        }
        return itens;
    }

    /// <summary>
    /// O ⋮ do cabeçalho da conversa aberta (nov/2026, F1 — como o do WhatsApp): o que antes eram cinco botões escritos
    /// (Assumir/Devolver, Transferir, Resumo, Buscar, Encerrar) e o ícone da ficha, num menu só, com a MESMA regra do botão
    /// direito da lista — quem não pode vê o item apagado com o motivo. <paramref name="conversa"/> nula é o caso da
    /// retenção sem conversa ainda: só os Dados do contato. <paramref name="mensagens"/> é quantas a conversa tem (sem
    /// nenhuma, o @SemDor não tem o que resumir).
    /// </summary>
    public static IReadOnlyList<ItemDeMenu<AcaoNaConversa>> DoCabecalho(AppConversation? conversa, string? meuAgente, int mensagens,
        AcessoNoMenu acesso)
    {
        var dados = new ItemDeMenu<AcaoNaConversa>(AcaoNaConversa.DadosDoContato, "Dados do contato", "ficha");
        if (conversa is null) return [dados];

        var minha = !string.IsNullOrWhiteSpace(meuAgente) && conversa.AgentId == meuAgente;
        var semEscrever = acesso.PodeEscrever ? null : "Seu acesso não alcança escrever no WhatsApp";
        var itens = new List<ItemDeMenu<AcaoNaConversa>>();
        if (conversa.Closed)
            itens.Add(new(AcaoNaConversa.Reabrir, "Reabrir", "atualizar", semEscrever));
        else
        {
            if (minha)
                itens.Add(new(AcaoNaConversa.Devolver, "Devolver à fila", "devolver", semEscrever));
            else
                itens.Add(new(AcaoNaConversa.Assumir, "Assumir", "pessoa",
                    semEscrever ?? (conversa.AgentId is not null ? "Está com outra pessoa: peça a transferência" : null)));
            var pendente = conversa.PendingTransfer is not null ? "Já há um pedido de transferência aguardando o aceite" : null;
            itens.Add(new(AcaoNaConversa.Transferir, "Transferir…", "transferir", semEscrever ?? (minha ? pendente : SoComVoce)));
            itens.Add(new(AcaoNaConversa.NotaInterna, "Nota para a equipe", "nota", semEscrever));
        }
        itens.Add(new(AcaoNaConversa.Buscar, "Buscar na conversa", "busca", GrupoNovo: true));
        itens.Add(dados);
        itens.Add(new(AcaoNaConversa.Etiquetas, "Etiquetas…", "etiqueta", semEscrever));
        if (acesso.SemDor)
            itens.Add(new(AcaoNaConversa.Resumir, "Resumo do @SemDor", "raio",
                semEscrever ?? (mensagens == 0 ? "Ainda não há mensagem para resumir" : null)));
        if (!conversa.Closed)
            itens.Add(new(AcaoNaConversa.Encerrar, "Encerrar…", "check", semEscrever ?? (minha ? null : SoComVoce), Perigo: true, GrupoNovo: true));
        return itens;
    }

    /// <summary>
    /// O menu de uma mensagem. <paramref name="selecao"/> é o texto selecionado DENTRO do balão no clique (vazio sem seleção);
    /// <paramref name="janelaLivre"/>, se a janela de 24 h aceita texto livre; <paramref name="sms"/>, o canal;
    /// <paramref name="minha"/>, se a conversa está com quem vê.
    /// </summary>
    public static IReadOnlyList<ItemDeMenu<AcaoNaMensagem>> DaMensagem(ItemDaConversa m, string? selecao, bool janelaLivre, bool sms,
        bool minha, bool fichaConferida, AcessoNoMenu acesso, DateTimeOffset? agora = null)
    {
        var enviada = m.Tipo == TipoNaLinhaDoTempo.Enviada;
        var semEscrever = acesso.PodeEscrever ? null : "Seu acesso não alcança escrever no WhatsApp";
        var itens = new List<ItemDeMenu<AcaoNaMensagem>>();

        // Os quatro do balão (F1, nov/2026 — como o menu do WhatsApp): Responder, Copiar, Nota para a equipe e Perguntar ao
        // profissional. O resto vem depois da linha, sem sumir nada do que o botão direito já fazia.
        itens.Add(new(AcaoNaMensagem.Responder, "Responder", "mensagem",
            semEscrever ?? (sms ? "O SMS não cita mensagens"
                : !janelaLivre ? "Janela de 24 h fechada: só modelo, que não cita"
                : string.IsNullOrEmpty(m.Id) ? "Esta mensagem ainda não tem o número do WhatsApp" : null)));
        var temTexto = !string.IsNullOrWhiteSpace(m.Texto);
        itens.Add(new(AcaoNaMensagem.CopiarTexto, "Copiar", "copiar", temTexto ? null : "A mensagem não tem texto"));
        itens.Add(new(AcaoNaMensagem.NotaSobre, "Nota para a equipe", "nota", semEscrever));
        // A mensagem do PACIENTE vai a quem trata pelo chat da equipe (R10); a regra do trecho é a do botão que ficava ao lado do balão.
        if (m.Tipo == TipoNaLinhaDoTempo.Recebida)
            itens.Add(new(AcaoNaMensagem.PerguntarAoProfissional, "Perguntar ao profissional", "pessoas",
                semEscrever ?? (temTexto || m.Midia is not null ? null : "A mensagem não tem texto")));

        // Reagir e ★ (F3, v3): a reação vai ao paciente (a Meta manda: janela aberta, até 30 dias); a ★ é só da equipe.
        itens.Add(new(AcaoNaMensagem.Reagir, "Reagir…", "emoji",
            semEscrever ?? ConversasNaTela.MotivoParaNaoReagir(m, janelaLivre, sms, agora ?? DateTimeOffset.Now), GrupoNovo: true));
        itens.Add(new(AcaoNaMensagem.Estrelar, m.Estrela ? "Tirar a estrela" : "Marcar como importante ★", "estrela",
            semEscrever ?? (string.IsNullOrEmpty(m.Id) ? "Esta mensagem ainda não tem o número do WhatsApp" : null)));

        if (!string.IsNullOrWhiteSpace(selecao))
            itens.Add(new(AcaoNaMensagem.CopiarSelecao, "Copiar a seleção", "copiar", GrupoNovo: true));
        itens.Add(new(AcaoNaMensagem.CopiarComHora, "Copiar com data e hora", "relogio", temTexto ? null : "A mensagem não tem texto",
            GrupoNovo: string.IsNullOrWhiteSpace(selecao)));

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
            : null; // todos escrevem (nov/2026): reenviar não pede ser o responsável

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
