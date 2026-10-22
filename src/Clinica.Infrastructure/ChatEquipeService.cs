using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure;

/// <summary>
/// O chat da equipe (out/2026). A conversa é uma chave (<see cref="ChatEquipeRegras"/>); o que
/// a tela afirma sai das regras puras, e aqui fica só a leitura e a gravação. Nada se apaga:
/// editar guarda a versão anterior, e não existe excluir.
///
/// ⚠️ DESEMPENHO — o banco fica a ~250 ms do posto (docs/desempenho-leituras-2026-10-15.md), então
/// o que custa é o NÚMERO de idas, não o tamanho da consulta. As regras daqui, as mesmas que fazem
/// o WhatsApp abrir na hora:
/// - nenhuma consulta varre o histórico: tudo vai por índice — a chave da mensagem, (Conversa, Id),
///   a lista de conversas (<see cref="ConversaEquipe"/>) e o índice de texto da busca;
/// - a tela guarda o que já leu e pergunta só "o que há depois de X" (<see cref="PulsoAsync"/>,
///   <see cref="AtualizarAsync"/>);
/// - o que é da mesma pergunta vem na mesma ida: <see cref="ConsultaParticipantes"/> traz, de uma
///   vez, quem pode ver a conversa, a presença, quem está escrevendo e até onde cada um leu;
/// - o texto longo só vem inteiro quando pedido (<see cref="TextoAsync"/>).
/// O orçamento de idas de cada operação é conferido em ChatEquipeIdasAoBancoTests.
/// </summary>
public sealed class ChatEquipeService : IChatEquipeService
{
    private readonly ClinicaDbContext db;
    private readonly Func<DateTime> agora;

    public ChatEquipeService(ClinicaDbContext db) : this(db, () => DateTime.Now) { }

    public ChatEquipeService(ClinicaDbContext db, Func<DateTime> agora)
    {
        this.db = db;
        this.agora = agora;
    }

    private async Task<UsuarioSistema> UsuarioAsync(int usuarioId, CancellationToken ct)
        => await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Id == usuarioId && u.Ativo, ct)
           ?? throw new InvalidOperationException("Entre com o seu usuário para usar o chat da equipe.");

    private static void ExigirAcesso(UsuarioSistema u, string conversa)
    {
        if (!ChatEquipeRegras.PodeVer(u, conversa))
            throw new UnauthorizedAccessException("Esta conversa não é sua.");
    }

    private static string Primeiro(string nome) => nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? nome;

    // ================================================================ quem está na conversa

    /// <summary>
    /// Uma pessoa da conversa, com a presença e a leitura dela. É a resposta de UMA ida: quem pode
    /// ver (o canal pelo perfil, a direta pelos dois ids), se está online ou escrevendo, e até onde
    /// leu — o "Lida por", o "está escrevendo" e a barreira de acesso saem daqui.
    /// </summary>
    public sealed record Participante(int Id, string Nome, PerfilAcesso Perfil, bool Ativo, int? ProfissionalId,
        Permissao PermissoesExtras, Permissao PermissoesNegadas, DateTime? VistoEm, string? EscrevendoEm,
        DateTime? EscrevendoAte, bool PresencaGravada, int? LeituraId, int? UltimaLidaId)
    {
        public UsuarioSistema Usuario => new()
        {
            Id = Id, Nome = Nome, Perfil = Perfil, Ativo = Ativo, ProfissionalId = ProfissionalId,
            PermissoesExtras = PermissoesExtras, PermissoesNegadas = PermissoesNegadas,
        };

        public PresencaEquipe Presenca => new() { UsuarioId = Id, VistoEm = VistoEm ?? DateTime.MinValue, EscrevendoEm = EscrevendoEm, EscrevendoAte = EscrevendoAte };
    }

    public static IQueryable<Participante> ConsultaParticipantes(ClinicaDbContext db, string conversa)
    {
        var usuarios = db.Usuarios.AsNoTracking();
        if (ChatEquipeRegras.EhCanal(conversa))
        {
            var perfis = ChatEquipeRegras.PerfisDoCanal(conversa);
            usuarios = usuarios.Where(u => u.Ativo && perfis.Contains(u.Perfil));
        }
        else if (ChatEquipeRegras.ParticipantesDaDireta(conversa) is { } par)
        {
            var (a, b) = par;
            usuarios = usuarios.Where(u => u.Id == a || u.Id == b);
        }
        else usuarios = usuarios.Where(u => false);

        return from u in usuarios
               join p in db.PresencasEquipe on u.Id equals p.UsuarioId into ps
               from p in ps.DefaultIfEmpty()
               select new Participante(u.Id, u.Nome, u.Perfil, u.Ativo, u.ProfissionalId, u.PermissoesExtras, u.PermissoesNegadas,
                   p == null ? null : (DateTime?)p.VistoEm, p == null ? null : p.EscrevendoEm,
                   p == null ? null : p.EscrevendoAte, p != null,
                   db.LeiturasEquipe.Where(l => l.UsuarioId == u.Id && l.Conversa == conversa).Select(l => (int?)l.Id).FirstOrDefault(),
                   db.LeiturasEquipe.Where(l => l.UsuarioId == u.Id && l.Conversa == conversa).Select(l => (int?)l.UltimaLidaId).FirstOrDefault());
    }

    /// <summary>As pessoas da conversa e EU entre elas — ou a recusa, quando a conversa não é minha.</summary>
    private async Task<(List<Participante> Todos, Participante Eu)> ParticipantesAsync(int usuarioId, string conversa, CancellationToken ct)
    {
        var todos = await ConsultaParticipantes(db, conversa).ToListAsync(ct);
        var eu = todos.FirstOrDefault(p => p.Id == usuarioId && p.Ativo)
                 ?? throw new UnauthorizedAccessException("Esta conversa não é sua.");
        return (todos, eu);
    }

    private static List<(int UsuarioId, int UltimaLidaId)> Leituras(IEnumerable<Participante> todos)
        => todos.Where(p => p.UltimaLidaId is not null).Select(p => (p.Id, p.UltimaLidaId!.Value)).ToList();

    private static List<string> Escrevendo(IEnumerable<Participante> todos, int eu, string conversa, DateTime agoraLocal)
        => todos.Where(p => p.Id != eu && ChatEquipeRegras.Escrevendo(p.Presenca, conversa, agoraLocal)).Select(p => p.Nome).ToList();

    /// <summary>O subtítulo da conversa: o canal com quantas pessoas; a direta com a presença da outra.</summary>
    private async Task<string> SubtituloAsync(string conversa, List<Participante> todos, int eu, DateTime agoraLocal, CancellationToken ct)
    {
        if (ChatEquipeRegras.EhCanal(conversa))
            return $"{ChatEquipeRegras.DescricaoDoCanal(conversa)} · {todos.Count} pessoa{(todos.Count == 1 ? "" : "s")}";
        var outro = todos.FirstOrDefault(p => p.Id != eu);
        if (outro is null) return "fora";
        if (!outro.Ativo) return "saiu da equipe · só leitura";
        DateTime? ate = null;
        if (outro.ProfissionalId is { } pid && (await EmAtendimentoAsync(ct, pid)).TryGetValue(pid, out var a)) ate = a;
        return ChatEquipeRegras.RotuloPresenca(ChatEquipeRegras.Presenca(outro.VistoEm, ate is not null, agoraLocal), ate);
    }

    /// <summary>
    /// Quem está com paciente na sala agora (por profissional) e até quando, pela agenda de hoje.
    /// Com <paramref name="soDe"/>, só aquele profissional — a direta aberta não lê a agenda inteira.
    /// </summary>
    private async Task<Dictionary<int, DateTime>> EmAtendimentoAsync(CancellationToken ct, int? soDe = null)
    {
        var hoje = agora().Date;
        var amanha = hoje.AddDays(1);
        var consulta = db.Agendamentos.AsNoTracking()
            .Where(a => a.DataHora >= hoje && a.DataHora < amanha && a.ProfissionalId != null
                        && a.Status == StatusAgendamento.Agendado && a.InicioAtendimentoEm != null && a.FimAtendimentoEm == null);
        if (soDe is { } pid) consulta = consulta.Where(a => a.ProfissionalId == pid);
        var linhas = await consulta
            .Select(a => new { ProfissionalId = a.ProfissionalId!.Value, a.DataHora, a.DuracaoMinutos }).ToListAsync(ct);
        return linhas.GroupBy(a => a.ProfissionalId).ToDictionary(g => g.Key,
            g => g.Max(a => a.DataHora.AddMinutes(a.DuracaoMinutos ?? Agendamento.DuracaoPadraoMinutos)));
    }

    // ================================================================ a bolinha e a lista

    /// <summary>A linha resumida (texto cortado no banco: o laudo inteiro não atravessa a rede para virar prévia).</summary>
    public sealed record ResumoMensagem(int Id, string Conversa, int AutorId, string AutorNome, DateTime EnviadaEm,
        string? Mencoes, string? ArquivoNome, string? PacienteNome, string? DocumentoRotulo, string Texto);

    /// <summary>
    /// As mensagens depois de <paramref name="desdeId"/>, das mais novas, dizendo quais são PARA MIM —
    /// e só destas o texto vem. Vai pela chave primária (Id &gt; desdeId), então custa o mesmo com
    /// um mês ou com dez anos de conversa.
    /// </summary>
    public static IQueryable<LinhaDoPulso> ConsultaPulso(ClinicaDbContext db, int eu, int desdeId, int limite)
    {
        var geral = ChatEquipeRegras.CanalGeral;
        var recepcao = ChatEquipeRegras.CanalRecepcao;
        var enfermagem = ChatEquipeRegras.CanalEnfermagem;
        var perfisRecepcao = ChatEquipeRegras.PerfisDoCanal(recepcao);
        var perfisEnfermagem = ChatEquipeRegras.PerfisDoCanal(enfermagem);
        return db.MensagensEquipe.AsNoTracking()
            .Where(m => m.Id > desdeId)
            .OrderByDescending(m => m.Id).Take(limite)
            .Select(m => new
            {
                M = m,
                ParaMim = m.AutorId != eu && db.Usuarios.Any(u => u.Id == eu && u.Ativo && (
                    m.Conversa == geral
                    || (m.Conversa == recepcao && perfisRecepcao.Contains(u.Perfil))
                    || (m.Conversa == enfermagem && perfisEnfermagem.Contains(u.Perfil))
                    || db.ConversasEquipe.Any(c => c.Conversa == m.Conversa && (c.ParticipanteA == eu || c.ParticipanteB == eu)))),
            })
            .Select(x => new LinhaDoPulso(x.M.Id, x.ParaMim,
                x.ParaMim ? x.M.Conversa : null, x.ParaMim ? x.M.AutorId : 0, x.ParaMim ? x.M.AutorNome : null,
                x.M.EnviadaEm, x.ParaMim ? x.M.Mencoes : null, x.ParaMim ? x.M.ArquivoNome : null,
                x.ParaMim ? x.M.PacienteNome : null, x.ParaMim ? x.M.DocumentoRotulo : null,
                x.ParaMim ? (x.M.Texto.Length > 300 ? x.M.Texto.Substring(0, 300) : x.M.Texto) : null));
    }

    public sealed record LinhaDoPulso(int Id, bool ParaMim, string? Conversa, int AutorId, string? AutorNome, DateTime EnviadaEm,
        string? Mencoes, string? ArquivoNome, string? PacienteNome, string? DocumentoRotulo, string? Texto);

    public async Task<PulsoChat> PulsoAsync(int usuarioId, int desdeId, PreferenciasSomChat prefs, CancellationToken ct = default)
    {
        var agoraLocal = agora();
        // A primeira batida (desdeId 0) só aprende onde a conversa está: o que já existia não é
        // novidade, e o número da bolinha vem do painel.
        var linhas = await ConsultaPulso(db, usuarioId, desdeId, desdeId == 0 ? 1 : 30).ToListAsync(ct);
        var ultimo = linhas.Count == 0 ? desdeId : Math.Max(desdeId, linhas.Max(l => l.Id));
        if (desdeId == 0) return new PulsoChat(ultimo, []);
        var novidades = linhas.Where(l => l.ParaMim).OrderBy(l => l.Id).TakeLast(10).Select(m => new NovidadeChat(m.Id, m.Conversa!,
                ChatEquipeRegras.EhCanal(m.Conversa!) ? "# " + ChatEquipeRegras.NomeDoCanal(m.Conversa!) : "mensagem direta",
                m.AutorNome ?? "", ChatEquipeRegras.Previa(m.Texto ?? "", ChatEquipeRegras.AnexoNaPrevia(m.DocumentoRotulo, m.PacienteNome, m.ArquivoNome)),
                m.EnviadaEm, ChatEquipeRegras.Som(m.Conversa!, m.AutorId, m.Mencoes, usuarioId, prefs, agoraLocal)))
            .ToList();
        return new PulsoChat(ultimo, novidades);
    }

    /// <summary>
    /// As minhas conversas, cada uma com a última mensagem e as NÃO LIDAS contadas no banco — pelo
    /// índice (Conversa, Id), a partir da minha leitura: contar dez mil não lidas não traz dez mil
    /// linhas, e não há teto que deixe o número errado.
    /// </summary>
    public static IQueryable<LinhaDaLista> ConsultaLista(ClinicaDbContext db, int eu, DateTime desdeCadastro, IReadOnlyCollection<string> canais)
        => db.ConversasEquipe.AsNoTracking()
            .Where(c => canais.Contains(c.Conversa) || c.ParticipanteA == eu || c.ParticipanteB == eu)
            .Select(c => new LinhaDaLista(c.Conversa, c.ParticipanteA, c.ParticipanteB, c.UltimaEm, c.UltimoAutorId,
                c.UltimoAutorNome, c.UltimaPrevia,
                db.MensagensEquipe.Count(m => m.Conversa == c.Conversa && m.AutorId != eu && m.EnviadaEm >= desdeCadastro
                    && m.Id > (db.LeiturasEquipe.Where(l => l.UsuarioId == eu && l.Conversa == c.Conversa)
                        .Select(l => (int?)l.UltimaLidaId).FirstOrDefault() ?? 0))));

    public sealed record LinhaDaLista(string Conversa, int? ParticipanteA, int? ParticipanteB, DateTime UltimaEm,
        int UltimoAutorId, string UltimoAutorNome, string UltimaPrevia, int NaoLidas);

    public async Task<PainelChat> PainelAsync(int usuarioId, bool completo = true, CancellationToken ct = default)
    {
        var eu = await UsuarioAsync(usuarioId, ct);
        var agoraLocal = agora();
        var canais = ChatEquipeRegras.CanaisDe(eu);
        // Conversa nunca aberta conta a partir do dia em que a pessoa entrou na clínica — quem
        // chega hoje não herda o histórico inteiro como "não lido".
        var linhas = await ConsultaLista(db, eu.Id, eu.CriadoEm, canais).ToListAsync(ct);
        var porConversa = linhas.ToDictionary(l => l.Conversa);

        string? Previa(LinhaDaLista? l)
            => l is null ? null : $"{(l.UltimoAutorId == eu.Id ? "Você" : Primeiro(l.UltimoAutorNome))}: {ChatEquipeRegras.Previa(l.UltimaPrevia, null, 70)}";

        var listaCanais = canais.Select(c =>
        {
            porConversa.TryGetValue(c, out var l);
            return new ConversaNaLista(c, ChatEquipeRegras.NomeDoCanal(c), ChatEquipeRegras.DescricaoDoCanal(c),
                l?.NaoLidas ?? 0, Previa(l), l?.UltimaEm, true);
        }).ToList();

        var diretasComConversa = linhas.Where(l => !ChatEquipeRegras.EhCanal(l.Conversa))
            .Select(l => (Linha: l, Outro: ChatEquipeRegras.OutroDaDireta(l.Conversa, eu.Id)))
            .Where(x => x.Outro is not null).ToDictionary(x => x.Outro!.Value, x => x.Linha);

        if (!completo)
        {
            // A bolinha fechada só precisa do número: sem nomes, sem presença, sem agenda.
            var soNumeros = diretasComConversa.Select(d => new ConversaNaLista(d.Value.Conversa, "", "", d.Value.NaoLidas,
                null, d.Value.UltimaEm, false, d.Key)).ToList();
            return new PainelChat(listaCanais, soNumeros, listaCanais.Sum(c => c.NaoLidas) + soNumeros.Sum(d => d.NaoLidas), 0, false);
        }

        // A equipe: quem está ativo e quem já não está mas tem conversa comigo (a direta fica, só leitura).
        var comConversa = diretasComConversa.Keys.ToList();
        var pessoas = await (from u in db.Usuarios.AsNoTracking()
                             where u.Id != eu.Id && (u.Ativo || comConversa.Contains(u.Id))
                             join p in db.PresencasEquipe on u.Id equals p.UsuarioId into ps
                             from p in ps.DefaultIfEmpty()
                             select new { u.Id, u.Nome, u.Perfil, u.ProfissionalId, u.Ativo, Visto = p == null ? null : (DateTime?)p.VistoEm })
            .ToListAsync(ct);
        var emAtendimento = await EmAtendimentoAsync(ct);

        int online = 0;
        var listaDiretas = pessoas.Select(p =>
            {
                diretasComConversa.TryGetValue(p.Id, out var l);
                var atend = p.ProfissionalId is { } pid && emAtendimento.ContainsKey(pid);
                var pres = p.Ativo ? ChatEquipeRegras.Presenca(p.Visto, atend, agoraLocal) : PresencaChat.Fora;
                if (pres != PresencaChat.Fora) online++;
                var rotulo = p.Ativo ? ChatEquipeRegras.RotuloDoPerfil(p.Perfil) : "saiu da equipe";
                return new ConversaNaLista(ChatEquipeRegras.Direta(eu.Id, p.Id), p.Nome, rotulo, l?.NaoLidas ?? 0, Previa(l),
                    l?.UltimaEm, false, p.Id, ChatEquipeRegras.Iniciais(p.Nome), pres, p.Ativo);
            })
            // Quem tem o que ler sobe; depois a conversa mais recente; depois quem está online; depois o nome.
            // Quem saiu da equipe vai para o fim.
            .OrderByDescending(d => d.PessoaAtiva).ThenByDescending(d => d.NaoLidas > 0)
            .ThenByDescending(d => d.UltimaEm ?? DateTime.MinValue)
            .ThenByDescending(d => d.Presenca != PresencaChat.Fora).ThenBy(d => d.Titulo, StringComparer.CurrentCultureIgnoreCase).ToList();

        return new PainelChat(listaCanais, listaDiretas,
            listaCanais.Sum(c => c.NaoLidas) + listaDiretas.Sum(d => d.NaoLidas), online);
    }

    // ================================================================ a conversa

    /// <summary>A linha da mensagem como a conversa a mostra — o texto só até <see cref="ChatEquipeRegras.TextoNaConversa"/>.</summary>
    public sealed record LinhaDaConversa(int Id, int AutorId, string AutorNome, string Texto, int TamanhoTexto, DateTime EnviadaEm,
        DateTime? EditadaEm, string? Mencoes, int? PacienteId, string? PacienteNome, int? DocumentoClinicoId,
        string? DocumentoRotulo, TipoDocumentoClinico? DocumentoTipo, string? ArquivoNome, long? ArquivoTamanho);

    public static IQueryable<LinhaDaConversa> ConsultaLinhas(IQueryable<MensagemEquipe> origem)
        => origem.Select(m => new LinhaDaConversa(m.Id, m.AutorId, m.AutorNome,
            m.Texto.Length > ChatEquipeRegras.TextoNaConversa ? m.Texto.Substring(0, ChatEquipeRegras.TextoNaConversa) : m.Texto,
            m.Texto.Length, m.EnviadaEm, m.EditadaEm,
            m.Mencoes, m.PacienteId, m.PacienteNome, m.DocumentoClinicoId, m.DocumentoRotulo,
            m.DocumentoClinicoId == null ? null : (TipoDocumentoClinico?)m.DocumentoClinico!.Tipo,
            m.ArquivoNome, m.ArquivoTamanho));

    /// <summary>
    /// A linha vira mensagem da tela. Documento que o meu acesso não abre chega SEM o id e sem o
    /// número — o cartão diz que há um documento, e quem pode abri-lo é quem o mandou.
    /// </summary>
    private static MensagemNaConversa Mensagem(LinhaDaConversa m, UsuarioSistema eu, string conversa,
        IReadOnlyList<(int UsuarioId, int UltimaLidaId)> leituras)
    {
        var veDocumento = m.DocumentoTipo is not { } tipo || CentralDocumentosService.PodeVer(eu.Efetivas, tipo);
        return new MensagemNaConversa(m.Id, m.AutorId, m.AutorNome,
            ChatEquipeRegras.Iniciais(m.AutorNome), m.Texto, m.EnviadaEm, m.EditadaEm, m.AutorId == eu.Id,
            m.AutorId != eu.Id && ChatEquipeRegras.Mencionou(m.Mencoes, eu.Id),
            m.PacienteId, m.PacienteNome,
            veDocumento ? m.DocumentoClinicoId : null,
            m.DocumentoRotulo is null ? null : veDocumento ? m.DocumentoRotulo : ChatEquipeRegras.DocumentoSemAcesso,
            m.ArquivoNome, m.ArquivoTamanho,
            m.AutorId == eu.Id ? ChatEquipeRegras.LidaPor(m.Id, conversa, eu.Id, leituras) : null,
            m.TamanhoTexto > m.Texto.Length ? m.TamanhoTexto : null);
    }

    /// <summary>
    /// "Li até aqui", numa ida: a leitura que já existe é atualizada sem ser lida de novo (o id dela
    /// veio com os participantes), e a primeira é criada.
    /// </summary>
    private async Task MarcarAteAsync(Participante eu, string conversa, int ultimaId, CancellationToken ct)
    {
        if (ultimaId <= 0 || eu.UltimaLidaId >= ultimaId) return;
        var momento = agora();
        if (eu.LeituraId is { } id)
            await db.LeiturasEquipe.Where(l => l.Id == id && l.UltimaLidaId < ultimaId)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.UltimaLidaId, ultimaId).SetProperty(l => l.LidaEm, momento), ct);
        else
        {
            var nova = db.LeiturasEquipe.Add(new LeituraEquipe { UsuarioId = eu.Id, Conversa = conversa, UltimaLidaId = ultimaId, LidaEm = momento });
            try { await db.SaveChangesAsync(ct); nova.State = EntityState.Detached; }
            catch (DbUpdateException)
            {
                // A mesma pessoa com dois apps abertos marcou ao mesmo tempo: a outra janela já criou.
                nova.State = EntityState.Detached;
                await db.LeiturasEquipe.Where(l => l.UsuarioId == eu.Id && l.Conversa == conversa && l.UltimaLidaId < ultimaId)
                    .ExecuteUpdateAsync(s => s.SetProperty(l => l.UltimaLidaId, ultimaId).SetProperty(l => l.LidaEm, momento), ct);
            }
        }
    }

    private static IReadOnlyList<PessoaDaConversa> PessoasParaCitar(IEnumerable<Participante> todos, int eu)
        => todos.Where(p => p.Id != eu && p.Ativo).OrderBy(p => p.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => new PessoaDaConversa(p.Id, p.Nome, ChatEquipeRegras.Iniciais(p.Nome))).ToList();

    public async Task<ConversaAberta> AbrirAsync(int usuarioId, string conversa, int? antesDe = null, int limite = 40, CancellationToken ct = default)
    {
        var agoraLocal = agora();
        var (todos, eu) = await ParticipantesAsync(usuarioId, conversa, ct);                       // ida 1
        var usuario = eu.Usuario;

        var consulta = db.MensagensEquipe.AsNoTracking().Where(m => m.Conversa == conversa);
        if (antesDe is { } antes) consulta = consulta.Where(m => m.Id < antes);
        var linhas = await ConsultaLinhas(consulta.OrderByDescending(m => m.Id).Take(limite + 1)).ToListAsync(ct); // ida 2
        var temAnteriores = linhas.Count > limite;
        linhas = linhas.Take(limite).OrderBy(m => m.Id).ToList();

        var leituras = Leituras(todos);
        var mensagens = linhas.Select(m => Mensagem(m, usuario, conversa, leituras)).ToList();

        string titulo;
        int? pessoaId = null;
        var soLeitura = false;
        if (ChatEquipeRegras.EhCanal(conversa)) titulo = "# " + ChatEquipeRegras.NomeDoCanal(conversa);
        else
        {
            pessoaId = ChatEquipeRegras.OutroDaDireta(conversa, eu.Id)!.Value;
            var outro = todos.FirstOrDefault(p => p.Id == pessoaId);
            titulo = outro?.Nome ?? "Pessoa removida";
            soLeitura = outro is null || !outro.Ativo;
        }
        var subtitulo = await SubtituloAsync(conversa, todos, eu.Id, agoraLocal, ct);                // (ida 3: só a direta com quem atende)

        // Abrir é ler: a marcação vai junto (ida 4), e a página de mensagens antigas não marca nada.
        if (antesDe is null && mensagens.Count > 0) await MarcarAteAsync(eu, conversa, mensagens[^1].Id, ct);

        return new ConversaAberta(conversa, titulo, subtitulo, pessoaId, mensagens, eu.UltimaLidaId ?? 0,
            Escrevendo(todos, eu.Id, conversa, agoraLocal), temAnteriores, agoraLocal, PessoasParaCitar(todos, eu.Id), soLeitura);
    }

    public async Task<AtualizacaoDaConversa> AtualizarAsync(int usuarioId, string conversa, int depoisDe, DateTime editadasDesde,
        int limite = 60, CancellationToken ct = default)
    {
        var agoraLocal = agora();
        var (todos, eu) = await ParticipantesAsync(usuarioId, conversa, ct);                       // ida 1
        var usuario = eu.Usuario;

        // As novas e as corrigidas numa ida só (ida 2): as duas metades vão por índice —
        // (Conversa, Id) e (Conversa, EditadaEm) —, nenhuma lê a conversa inteira.
        var corte = editadasDesde > DateTime.MinValue + ChatEquipeRegras.MargemDoRelogio
            ? editadasDesde - ChatEquipeRegras.MargemDoRelogio : DateTime.MinValue;
        var linhas = await ConsultaLinhas(db.MensagensEquipe.AsNoTracking()
                .Where(m => m.Conversa == conversa && (m.Id > depoisDe || (m.EditadaEm != null && m.EditadaEm > corte)))
                .OrderBy(m => m.Id).Take(limite + 50))
            .ToListAsync(ct);
        var novas = linhas.Where(m => m.Id > depoisDe).ToList();
        var editadas = linhas.Where(m => m.Id <= depoisDe).ToList();

        var leituras = Leituras(todos);
        string? subtitulo = ChatEquipeRegras.EhCanal(conversa) ? null : await SubtituloAsync(conversa, todos, eu.Id, agoraLocal, ct);

        // Linhas no teto: pode haver novas além delas, e a tela pede a próxima página.
        var haMais = novas.Count > limite || linhas.Count == limite + 50;
        var mostradas = novas.Take(limite).ToList();
        if (mostradas.Count > 0) await MarcarAteAsync(eu, conversa, mostradas[^1].Id, ct);

        return new AtualizacaoDaConversa(
            mostradas.Select(m => Mensagem(m, usuario, conversa, leituras)).ToList(), haMais,
            editadas.Select(m => Mensagem(m, usuario, conversa, leituras)).ToList(),
            leituras.Select(p => new LeituraNaConversa(p.UsuarioId, p.UltimaLidaId)).ToList(),
            Escrevendo(todos, eu.Id, conversa, agoraLocal), subtitulo, agoraLocal);
    }

    public async Task<string> TextoAsync(int usuarioId, int mensagemId, CancellationToken ct = default)
    {
        var eu = await UsuarioAsync(usuarioId, ct);
        var m = await db.MensagensEquipe.AsNoTracking().Where(x => x.Id == mensagemId)
                    .Select(x => new { x.Conversa, x.Texto }).SingleOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("Mensagem não encontrada — atualize a conversa.");
        ExigirAcesso(eu, m.Conversa);
        return m.Texto;
    }

    // ================================================================ escrever

    public async Task<int> EnviarAsync(int usuarioId, string conversa, NovaMensagemEquipe dados, CancellationToken ct = default)
    {
        var (todos, participante) = await ParticipantesAsync(usuarioId, conversa, ct);            // ida 1
        var eu = participante.Usuario;
        if (!ChatEquipeRegras.EhCanal(conversa) && todos.Any(p => p.Id != eu.Id && !p.Ativo))
            throw new InvalidOperationException("Esta pessoa saiu da equipe: a conversa fica para leitura, sem mensagens novas.");
        var texto = (dados.Texto ?? "").Replace("\r\n", "\n").Trim();
        var temArquivo = dados.ArquivoConteudo is { Length: > 0 };
        if (texto.Length == 0 && dados.PacienteId is null && dados.DocumentoClinicoId is null && !temArquivo)
            throw new InvalidOperationException("Escreva a mensagem ou anexe algo antes de enviar.");
        if (temArquivo && dados.ArquivoConteudo!.LongLength > ChatEquipeRegras.TamanhoMaximoArquivo)
            throw new InvalidOperationException($"O arquivo passa de {ChatEquipeRegras.Tamanho(ChatEquipeRegras.TamanhoMaximoArquivo)}. Envie um menor.");
        if (temArquivo && ChatEquipeRegras.NomeDoArquivo(dados.ArquivoNome ?? "").Length == 0)
            throw new InvalidOperationException("O arquivo anexado precisa de um nome.");
        if (temArquivo && !ChatEquipeRegras.ArquivoPermitido(dados.ArquivoNome!))
            throw new InvalidOperationException(ChatEquipeRegras.TipoDeArquivoRecusado);

        // As barreiras dos anexos moram AQUI, e não só na tela que os oferece: quem chama o
        // serviço com um id qualquer não pode fazer o chat abrir para os outros o que ele
        // mesmo não pode ver.
        string? pacienteNome = null;
        if (dados.PacienteId is { } pacId)
        {
            if (!eu.Pode(Permissao.VerFichaPaciente))
                throw new UnauthorizedAccessException("Seu acesso não abre a ficha do paciente, então não dá para anexá-lo.");
            pacienteNome = await db.Pacientes.AsNoTracking().Where(p => p.Id == pacId).Select(p => p.Nome).SingleOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("O paciente anexado não foi encontrado — escolha de novo.");
        }
        string? docRotulo = null;
        if (dados.DocumentoClinicoId is { } docId)
        {
            var doc = await db.DocumentosClinicos.AsNoTracking().Where(d => d.Id == docId)
                .Select(d => new { d.Tipo, d.Numero, d.PacienteId, d.CanceladoEm }).SingleOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("O documento anexado não foi encontrado — escolha de novo.");
            if (doc.PacienteId != dados.PacienteId)
                throw new InvalidOperationException("O documento anexado não é do paciente anexado — escolha de novo.");
            if (doc.CanceladoEm is not null)
                throw new InvalidOperationException("O documento anexado foi cancelado e não vai pelo chat.");
            if (!CentralDocumentosService.PodeVer(eu.Efetivas, doc.Tipo))
                throw new UnauthorizedAccessException("Seu acesso não abre este documento, então não dá para anexá-lo.");
            docRotulo = $"{TipoDocumentoInfo.Rotular(doc.Tipo)} {doc.Numero}";
        }

        var momento = agora();
        var mensagem = new MensagemEquipe
        {
            Conversa = conversa, AutorId = eu.Id, AutorNome = eu.Nome, Texto = texto, TextoBusca = ChatEquipeRegras.TextoDeBusca(texto),
            EnviadaEm = momento,
            // Cita só quem pode ler a conversa — os participantes já vieram na ida 1.
            Mencoes = ChatEquipeRegras.GravarMencoes(Mencionados(texto, todos, eu.Id)),
            PacienteId = dados.PacienteId, PacienteNome = pacienteNome,
            DocumentoClinicoId = dados.DocumentoClinicoId, DocumentoRotulo = docRotulo,
        };
        if (temArquivo)
        {
            mensagem.ArquivoNome = ChatEquipeRegras.NomeDoArquivo(dados.ArquivoNome!);
            mensagem.ArquivoTamanho = dados.ArquivoConteudo!.LongLength;
            mensagem.Arquivo = new ArquivoMensagemEquipe { Conteudo = dados.ArquivoConteudo };
        }
        db.MensagensEquipe.Add(mensagem);

        // A lista de conversas e a minha presença vão no MESMO SaveChanges da mensagem (ida 2): o
        // resumo aponta para a mensagem pela chave, e quem escreveu parou de escrever.
        var previa = ChatEquipeRegras.Previa(texto, ChatEquipeRegras.AnexoNaPrevia(docRotulo, pacienteNome, mensagem.ArquivoNome), 120);
        var direta = ChatEquipeRegras.ParticipantesDaDireta(conversa);
        var resumo = new ConversaEquipe
        {
            Conversa = conversa, ParticipanteA = direta?.A, ParticipanteB = direta?.B, UltimaMensagem = mensagem,
            UltimaEm = momento, UltimoAutorId = eu.Id, UltimoAutorNome = eu.Nome, UltimaPrevia = previa,
        };
        var presenca = new PresencaEquipe { UsuarioId = eu.Id, VistoEm = momento };
        await GravarComResumoAsync(resumo, presenca, participante.PresencaGravada, ct);
        return mensagem.Id;
    }

    /// <summary>
    /// Grava a mensagem já adicionada, o resumo da conversa e a presença numa ida. O resumo é
    /// tentado como ATUALIZAÇÃO (a conversa quase sempre já existe); quando é a primeira mensagem,
    /// a atualização não acha linha e o EF avisa — então ele entra como inclusão. Duas pessoas
    /// abrindo a mesma direta ao mesmo tempo caem no caminho inverso.
    /// </summary>
    private async Task GravarComResumoAsync(ConversaEquipe resumo, PresencaEquipe presenca, bool presencaExiste, CancellationToken ct)
    {
        // Uma operação anterior DESTE contexto (um serviço vive o escopo inteiro) pode ter deixado a
        // mesma presença ou conversa rastreada: a linha nova a substitui.
        Soltar<PresencaEquipe>(p => p.UsuarioId == presenca.UsuarioId);
        Soltar<ConversaEquipe>(c => c.Conversa == resumo.Conversa);
        var entradaPresenca = presencaExiste ? db.PresencasEquipe.Attach(presenca) : db.PresencasEquipe.Add(presenca);
        if (presencaExiste)
        {
            entradaPresenca.Property(p => p.VistoEm).IsModified = true;
            entradaPresenca.Property(p => p.EscrevendoEm).IsModified = true;
            entradaPresenca.Property(p => p.EscrevendoAte).IsModified = true;
        }
        var entradaResumo = db.ConversasEquipe.Attach(resumo);
        entradaResumo.State = EntityState.Modified;
        entradaResumo.Property(r => r.ParticipanteA).IsModified = false;
        entradaResumo.Property(r => r.ParticipanteB).IsModified = false;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A atualização não achou a linha: é a primeira mensagem da conversa.
            entradaResumo.State = EntityState.Added;
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) when (entradaResumo.State == EntityState.Added)
            {
                // Alguém criou a conversa no mesmo instante: agora ela existe, e é atualização.
                entradaResumo.State = EntityState.Modified;
                entradaResumo.Property(r => r.ParticipanteA).IsModified = false;
                entradaResumo.Property(r => r.ParticipanteB).IsModified = false;
                await db.SaveChangesAsync(ct);
            }
        }
        catch (DbUpdateException) when (!presencaExiste && entradaPresenca.State == EntityState.Added)
        {
            // A presença foi criada por outro app da mesma pessoa no meio do caminho.
            entradaPresenca.State = EntityState.Modified;
            await db.SaveChangesAsync(ct);
        }
        // Gravadas, as duas linhas saem do rastreio: são um retrato do envio, não o estado do banco.
        entradaPresenca.State = EntityState.Detached;
        entradaResumo.State = EntityState.Detached;
    }

    private void Soltar<T>(Func<T, bool> mesma) where T : class
    {
        foreach (var e in db.ChangeTracker.Entries<T>().Where(e => mesma(e.Entity)).ToList()) e.State = EntityState.Detached;
    }

    private static IReadOnlyList<int> Mencionados(string texto, IEnumerable<Participante> todos, int eu)
        => !texto.Contains('@') ? [] : ChatEquipeRegras.Mencionados(texto, todos.Where(p => p.Id != eu && p.Ativo).Select(p => (p.Id, p.Nome)));

    public async Task EditarAsync(int usuarioId, int mensagemId, string texto, CancellationToken ct = default)
    {
        var m = await db.MensagensEquipe.SingleOrDefaultAsync(x => x.Id == mensagemId, ct)
                ?? throw new InvalidOperationException("Mensagem não encontrada — atualize a conversa.");
        var (todos, eu) = await ParticipantesAsync(usuarioId, m.Conversa, ct);
        if (m.AutorId != eu.Id) throw new UnauthorizedAccessException("Só quem escreveu corrige a mensagem.");
        var novo = (texto ?? "").Replace("\r\n", "\n").Trim();
        if (novo == m.Texto) return;
        if (novo.Length == 0 && m.PacienteId is null && m.DocumentoClinicoId is null && m.ArquivoNome is null)
            throw new InvalidOperationException("A mensagem não pode ficar vazia — não existe apagar no chat.");
        var momento = agora();
        db.VersoesMensagemEquipe.Add(new VersaoMensagemEquipe { MensagemId = m.Id, Texto = m.Texto, SubstituidaEm = momento });
        m.Texto = novo;
        m.TextoBusca = ChatEquipeRegras.TextoDeBusca(novo);
        m.EditadaEm = momento;
        m.Mencoes = ChatEquipeRegras.GravarMencoes(Mencionados(novo, todos, eu.Id));
        await db.SaveChangesAsync(ct);
        // Corrigiu a ÚLTIMA mensagem? A prévia da lista acompanha.
        var previa = ChatEquipeRegras.Previa(novo, ChatEquipeRegras.AnexoNaPrevia(m.DocumentoRotulo, m.PacienteNome, m.ArquivoNome), 120);
        await db.ConversasEquipe.Where(c => c.Conversa == m.Conversa && c.UltimaMensagemId == m.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.UltimaPrevia, previa), ct);
    }

    public async Task MarcarLidaAsync(int usuarioId, string conversa, int ultimaId, CancellationToken ct = default)
    {
        if (ultimaId <= 0) return;
        var (_, eu) = await ParticipantesAsync(usuarioId, conversa, ct);
        await MarcarAteAsync(eu, conversa, ultimaId, ct);
    }

    /// <summary>
    /// Uma ida: a presença que já existe é atualizada direto no banco, sem ser lida antes; só a
    /// primeira vez da pessoa cria a linha. "Escrevendo" numa conversa que não é dela só apareceria
    /// para quem vê aquela conversa — e nem isso: quem lê confere a conversa da linha.
    /// </summary>
    public async Task SinalizarAsync(int usuarioId, string? escrevendoEm, CancellationToken ct = default)
    {
        var momento = agora();
        DateTime? ate = escrevendoEm is null ? null : momento + ChatEquipeRegras.ValidadeEscrevendo;
        var mudou = await db.PresencasEquipe.Where(p => p.UsuarioId == usuarioId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.VistoEm, momento)
                .SetProperty(p => p.EscrevendoEm, escrevendoEm).SetProperty(p => p.EscrevendoAte, ate), ct);
        if (mudou > 0) return;
        var eu = await UsuarioAsync(usuarioId, ct);
        if (escrevendoEm is not null && !ChatEquipeRegras.PodeVer(eu, escrevendoEm)) { escrevendoEm = null; ate = null; }
        Soltar<PresencaEquipe>(p => p.UsuarioId == eu.Id);
        var nova = db.PresencasEquipe.Add(new PresencaEquipe { UsuarioId = eu.Id, VistoEm = momento, EscrevendoEm = escrevendoEm, EscrevendoAte = ate });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { /* o outro app da mesma pessoa criou no mesmo instante */ }
        nova.State = EntityState.Detached;
    }

    // ================================================================ busca

    /// <summary>
    /// As mensagens com TODAS as palavras, nas conversas que a pessoa vê. No Postgres vai ao índice
    /// GIN de <c>to_tsvector('simple', "TextoBusca")</c> (a MESMA expressão do índice da migration);
    /// no SQLite dos testes, LIKE sobre a mesma coluna.
    /// </summary>
    public static IQueryable<MensagemEquipe> ConsultaBusca(ClinicaDbContext db, IReadOnlyList<string> palavras, int eu,
        IReadOnlyCollection<string> canais, string? conversa)
    {
        var q = db.MensagensEquipe.AsNoTracking();
        if (db.Database.IsNpgsql())
        {
            var consulta = ChatEquipeRegras.ConsultaDeTexto(palavras);
            q = q.Where(m => EF.Functions.ToTsVector("simple", m.TextoBusca).Matches(EF.Functions.ToTsQuery("simple", consulta)));
        }
        else
            foreach (var p in palavras)
            {
                var padrao = "%" + p + "%";
                q = q.Where(m => EF.Functions.Like(m.TextoBusca, padrao));
            }
        q = conversa is not null
            ? q.Where(m => m.Conversa == conversa)
            : q.Where(m => canais.Contains(m.Conversa)
                           || db.ConversasEquipe.Any(c => c.Conversa == m.Conversa && (c.ParticipanteA == eu || c.ParticipanteB == eu)));
        return q;
    }

    public async Task<IReadOnlyList<ResultadoDaBusca>> BuscarAsync(int usuarioId, string termo, string? conversa = null, int limite = 30,
        CancellationToken ct = default)
    {
        var palavras = ChatEquipeRegras.PalavrasDaBusca(termo);
        if (palavras.Count == 0) return [];
        var eu = await UsuarioAsync(usuarioId, ct);
        if (conversa is not null) ExigirAcesso(eu, conversa);
        var achadas = await ConsultaBusca(db, palavras, eu.Id, ChatEquipeRegras.CanaisDe(eu), conversa)
            .OrderByDescending(m => m.Id).Take(limite)
            .Select(m => new { m.Id, m.Conversa, m.AutorNome, m.EnviadaEm, Texto = m.Texto.Length > 2000 ? m.Texto.Substring(0, 2000) : m.Texto })
            .ToListAsync(ct);
        // O título da direta é o nome da outra pessoa — os nomes vêm numa ida, só dos ids achados.
        var outros = achadas.Select(a => ChatEquipeRegras.OutroDaDireta(a.Conversa, eu.Id)).OfType<int>().Distinct().ToList();
        var nomes = outros.Count == 0 ? new Dictionary<int, string>()
            : await db.Usuarios.AsNoTracking().Where(u => outros.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Nome, ct);
        return achadas.Select(a => new ResultadoDaBusca(a.Id, a.Conversa,
                ChatEquipeRegras.EhCanal(a.Conversa) ? "# " + ChatEquipeRegras.NomeDoCanal(a.Conversa)
                : nomes.GetValueOrDefault(ChatEquipeRegras.OutroDaDireta(a.Conversa, eu.Id) ?? 0, "Pessoa removida"),
                a.AutorNome, a.EnviadaEm, ChatEquipeRegras.Trecho(a.Texto, palavras)))
            .ToList();
    }

    // ================================================================ arquivo

    public async Task<(string Nome, byte[] Conteudo)?> ArquivoAsync(int usuarioId, int mensagemId, CancellationToken ct = default)
    {
        var eu = await UsuarioAsync(usuarioId, ct);
        var m = await db.MensagensEquipe.AsNoTracking().Where(x => x.Id == mensagemId)
            .Select(x => new { x.Conversa, x.ArquivoNome }).SingleOrDefaultAsync(ct);
        if (m?.ArquivoNome is null) return null;
        ExigirAcesso(eu, m.Conversa);
        var bytes = await db.ArquivosMensagemEquipe.AsNoTracking().Where(a => a.MensagemId == mensagemId)
            .Select(a => a.Conteudo).SingleOrDefaultAsync(ct);
        return bytes is null ? null : (m.ArquivoNome, bytes);
    }
}
