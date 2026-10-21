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

    /// <summary>As diretas que já têm mensagem e me envolvem — por prefixo e sufixo da chave.</summary>
    public static IQueryable<string> ConsultaMinhasDiretas(ClinicaDbContext db, int eu)
    {
        var inicio = $"direta:{eu}:";
        var fim = $":{eu}";
        return db.MensagensEquipe.AsNoTracking()
            .Where(m => m.Conversa.StartsWith(inicio) || (m.Conversa.StartsWith("direta:") && m.Conversa.EndsWith(fim)))
            .Select(m => m.Conversa).Distinct();
    }

    private Task<List<string>> MinhasDiretasAsync(int eu, CancellationToken ct) => ConsultaMinhasDiretas(db, eu).ToListAsync(ct);

    /// <summary>A última mensagem de cada conversa — o id, para a prévia da lista.</summary>
    public static IQueryable<int> ConsultaUltimasPorConversa(ClinicaDbContext db, IReadOnlyCollection<string> chaves)
        => db.MensagensEquipe.AsNoTracking().Where(m => chaves.Contains(m.Conversa))
            .GroupBy(m => m.Conversa).Select(g => g.Max(m => m.Id));

    /// <summary>A linha resumida (texto cortado no banco: o laudo inteiro não atravessa a rede para virar prévia).</summary>
    public static IQueryable<ResumoMensagem> ConsultaResumos(IQueryable<MensagemEquipe> origem)
        => origem.Select(m => new ResumoMensagem(m.Id, m.Conversa, m.AutorId, m.AutorNome, m.EnviadaEm, m.Mencoes,
            m.ArquivoNome, m.PacienteNome, m.DocumentoRotulo, m.Texto.Length > 300 ? m.Texto.Substring(0, 300) : m.Texto));

    public sealed record ResumoMensagem(int Id, string Conversa, int AutorId, string AutorNome, DateTime EnviadaEm,
        string? Mencoes, string? ArquivoNome, string? PacienteNome, string? DocumentoRotulo, string Texto);

    public async Task<PainelChat> PainelAsync(int usuarioId, int desdeId, PreferenciasSomChat prefs, CancellationToken ct = default)
    {
        var eu = await UsuarioAsync(usuarioId, ct);
        var agoraLocal = agora();
        var canais = ChatEquipeRegras.CanaisDe(eu);
        var diretas = (await MinhasDiretasAsync(eu.Id, ct)).Where(d => ChatEquipeRegras.OutroDaDireta(d, eu.Id) is not null).ToList();
        var chaves = canais.Concat(diretas).ToList();

        var leituras = await db.LeiturasEquipe.AsNoTracking().Where(l => l.UsuarioId == eu.Id)
            .ToDictionaryAsync(l => l.Conversa, l => l.UltimaLidaId, ct);
        // Conversa nunca aberta conta a partir do dia em que a pessoa entrou na clínica — quem
        // chega hoje não herda o histórico inteiro como "não lido".
        var desdeCadastro = eu.CriadoEm;
        var piso = chaves.Count == 0 ? 0 : chaves.Min(c => leituras.GetValueOrDefault(c));
        var naoLidasBrutas = await db.MensagensEquipe.AsNoTracking()
            .Where(m => chaves.Contains(m.Conversa) && m.AutorId != eu.Id && m.Id > piso && m.EnviadaEm >= desdeCadastro)
            .OrderByDescending(m => m.Id).Take(3000)
            .Select(m => new { m.Conversa, m.Id }).ToListAsync(ct);
        var naoLidas = naoLidasBrutas.Where(m => m.Id > leituras.GetValueOrDefault(m.Conversa))
            .GroupBy(m => m.Conversa).ToDictionary(g => g.Key, g => g.Count());

        var ultimasIds = await ConsultaUltimasPorConversa(db, chaves).ToListAsync(ct);
        var ultimas = await ConsultaResumos(db.MensagensEquipe.AsNoTracking().Where(m => ultimasIds.Contains(m.Id))).ToListAsync(ct);
        var ultimaPor = ultimas.ToDictionary(m => m.Conversa);

        var pessoas = await db.Usuarios.AsNoTracking().Where(u => u.Ativo && u.Id != eu.Id)
            .Select(u => new { u.Id, u.Nome, u.Perfil, u.ProfissionalId }).ToListAsync(ct);
        var presencas = await db.PresencasEquipe.AsNoTracking().ToDictionaryAsync(p => p.UsuarioId, ct);
        var emAtendimento = await EmAtendimentoAsync(ct);

        string? Previa(string conversa)
        {
            if (!ultimaPor.TryGetValue(conversa, out var m)) return null;
            var anexo = ChatEquipeRegras.AnexoNaPrevia(m.DocumentoRotulo, m.PacienteNome, m.ArquivoNome);
            var quem = m.AutorId == eu.Id ? "Você" : Primeiro(m.AutorNome);
            return $"{quem}: {ChatEquipeRegras.Previa(m.Texto, anexo, 70)}";
        }

        var listaCanais = canais.Select(c => new ConversaNaLista(c, ChatEquipeRegras.NomeDoCanal(c),
            ChatEquipeRegras.DescricaoDoCanal(c), naoLidas.GetValueOrDefault(c), Previa(c),
            ultimaPor.TryGetValue(c, out var u) ? u.EnviadaEm : null, true)).ToList();

        int online = 0;
        var listaDiretas = pessoas.Select(p =>
        {
            var chave = ChatEquipeRegras.Direta(eu.Id, p.Id);
            var atend = p.ProfissionalId is { } pid && emAtendimento.TryGetValue(pid, out var ate) ? ate : (DateTime?)null;
            var pres = ChatEquipeRegras.Presenca(presencas.TryGetValue(p.Id, out var pr) ? pr.VistoEm : null, atend is not null, agoraLocal);
            if (pres != PresencaChat.Fora) online++;
            var rotulo = ChatEquipeRegras.RotuloDoPerfil(p.Perfil);
            return new ConversaNaLista(chave, p.Nome, rotulo, naoLidas.GetValueOrDefault(chave), Previa(chave),
                ultimaPor.TryGetValue(chave, out var um) ? um.EnviadaEm : null, false, p.Id, ChatEquipeRegras.Iniciais(p.Nome), pres);
        })
        // Quem tem o que ler sobe; depois a conversa mais recente; depois quem está online; depois o nome.
        .OrderByDescending(d => d.NaoLidas > 0).ThenByDescending(d => d.UltimaEm ?? DateTime.MinValue)
        .ThenByDescending(d => d.Presenca != PresencaChat.Fora).ThenBy(d => d.Titulo, StringComparer.CurrentCultureIgnoreCase).ToList();

        var ultimoId = ultimas.Count == 0 ? 0 : ultimas.Max(m => m.Id);
        var novidades = new List<NovidadeChat>();
        if (desdeId > 0 && ultimoId > desdeId)
        {
            var novas = await ConsultaResumos(db.MensagensEquipe.AsNoTracking()
                .Where(m => chaves.Contains(m.Conversa) && m.Id > desdeId && m.AutorId != eu.Id)
                .OrderByDescending(m => m.Id).Take(10)).ToListAsync(ct);
            novidades = novas.OrderBy(m => m.Id).Select(m => new NovidadeChat(m.Id, m.Conversa,
                ChatEquipeRegras.EhCanal(m.Conversa) ? "# " + ChatEquipeRegras.NomeDoCanal(m.Conversa) : "mensagem direta",
                m.AutorNome, ChatEquipeRegras.Previa(m.Texto, ChatEquipeRegras.AnexoNaPrevia(m.DocumentoRotulo, m.PacienteNome, m.ArquivoNome)),
                m.EnviadaEm, ChatEquipeRegras.Som(m.Conversa, m.AutorId, m.Mencoes, eu.Id, prefs, agoraLocal))).ToList();
        }

        return new PainelChat(listaCanais, listaDiretas,
            listaCanais.Sum(c => c.NaoLidas) + listaDiretas.Sum(d => d.NaoLidas), online, ultimoId, novidades);
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

    private static string Primeiro(string nome) => nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? nome;

    /// <summary>A linha da mensagem como a conversa a mostra — o tipo do documento vem junto para decidir quem o abre.</summary>
    public sealed record LinhaDaConversa(int Id, int AutorId, string AutorNome, string Texto, DateTime EnviadaEm,
        DateTime? EditadaEm, string? Mencoes, int? PacienteId, string? PacienteNome, int? DocumentoClinicoId,
        string? DocumentoRotulo, TipoDocumentoClinico? DocumentoTipo, string? ArquivoNome, long? ArquivoTamanho);

    public static IQueryable<LinhaDaConversa> ConsultaLinhas(IQueryable<MensagemEquipe> origem)
        => origem.Select(m => new LinhaDaConversa(m.Id, m.AutorId, m.AutorNome, m.Texto, m.EnviadaEm, m.EditadaEm,
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
            m.AutorId == eu.Id ? ChatEquipeRegras.LidaPor(m.Id, conversa, eu.Id, leituras) : null);
    }

    private async Task<List<(int UsuarioId, int UltimaLidaId)>> LeiturasAsync(string conversa, CancellationToken ct)
        => (await db.LeiturasEquipe.AsNoTracking().Where(l => l.Conversa == conversa)
                .Select(l => new { l.UsuarioId, l.UltimaLidaId }).ToListAsync(ct))
            .Select(l => (l.UsuarioId, l.UltimaLidaId)).ToList();

    private async Task<List<string>> EscrevendoAsync(string conversa, int eu, DateTime agoraLocal, CancellationToken ct)
    {
        var ids = (await db.PresencasEquipe.AsNoTracking()
                .Where(p => p.EscrevendoEm == conversa && p.UsuarioId != eu).ToListAsync(ct))
            .Where(p => ChatEquipeRegras.Escrevendo(p, conversa, agoraLocal)).Select(p => p.UsuarioId).ToList();
        return ids.Count == 0 ? [] : await db.Usuarios.AsNoTracking()
            .Where(u => ids.Contains(u.Id)).Select(u => u.Nome).ToListAsync(ct);
    }

    /// <summary>A presença da outra pessoa da direta: "online", "em atendimento até 10:30" ou "fora".</summary>
    private async Task<string> PresencaNaDiretaAsync(int pessoaId, int? profissionalId, DateTime agoraLocal, CancellationToken ct)
    {
        var visto = await db.PresencasEquipe.AsNoTracking().Where(p => p.UsuarioId == pessoaId)
            .Select(p => (DateTime?)p.VistoEm).SingleOrDefaultAsync(ct);
        DateTime? ate = null;
        if (profissionalId is { } pid && (await EmAtendimentoAsync(ct, pid)).TryGetValue(pid, out var a)) ate = a;
        return ChatEquipeRegras.RotuloPresenca(ChatEquipeRegras.Presenca(visto, ate is not null, agoraLocal), ate);
    }

    public async Task<ConversaAberta> AbrirAsync(int usuarioId, string conversa, int? antesDe = null, int limite = 60, CancellationToken ct = default)
    {
        var eu = await UsuarioAsync(usuarioId, ct);
        ExigirAcesso(eu, conversa);
        var agoraLocal = agora();

        var consulta = db.MensagensEquipe.AsNoTracking().Where(m => m.Conversa == conversa);
        if (antesDe is { } antes) consulta = consulta.Where(m => m.Id < antes);
        var linhas = await ConsultaLinhas(consulta.OrderByDescending(m => m.Id).Take(limite + 1)).ToListAsync(ct);
        var temAnteriores = linhas.Count > limite;
        linhas = linhas.Take(limite).OrderBy(m => m.Id).ToList();

        var pares = await LeiturasAsync(conversa, ct);
        var minhaLeitura = pares.FirstOrDefault(l => l.UsuarioId == eu.Id).UltimaLidaId;
        var mensagens = linhas.Select(m => Mensagem(m, eu, conversa, pares)).ToList();
        var escrevendo = await EscrevendoAsync(conversa, eu.Id, agoraLocal, ct);

        string titulo, subtitulo;
        int? pessoaId = null;
        if (ChatEquipeRegras.EhCanal(conversa))
        {
            titulo = "# " + ChatEquipeRegras.NomeDoCanal(conversa);
            var perfis = await db.Usuarios.AsNoTracking().Where(u => u.Ativo).Select(u => u.Perfil).ToListAsync(ct);
            var pessoas = perfis.Count(p => ChatEquipeRegras.ParticipaDoCanal(p, conversa));
            subtitulo = $"{ChatEquipeRegras.DescricaoDoCanal(conversa)} · {pessoas} pessoa{(pessoas == 1 ? "" : "s")}";
        }
        else
        {
            pessoaId = ChatEquipeRegras.OutroDaDireta(conversa, eu.Id)!.Value;
            var outro = await db.Usuarios.AsNoTracking().Where(u => u.Id == pessoaId)
                .Select(u => new { u.Nome, u.ProfissionalId }).SingleOrDefaultAsync(ct);
            titulo = outro?.Nome ?? "Pessoa removida";
            subtitulo = await PresencaNaDiretaAsync(pessoaId.Value, outro?.ProfissionalId, agoraLocal, ct);
        }

        return new ConversaAberta(conversa, titulo, subtitulo, pessoaId, mensagens, minhaLeitura, escrevendo, temAnteriores, agoraLocal);
    }

    public async Task<AtualizacaoDaConversa> AtualizarAsync(int usuarioId, string conversa, int depoisDe, DateTime editadasDesde,
        int limite = 60, CancellationToken ct = default)
    {
        var eu = await UsuarioAsync(usuarioId, ct);
        ExigirAcesso(eu, conversa);
        var agoraLocal = agora();

        var daConversa = db.MensagensEquipe.AsNoTracking().Where(m => m.Conversa == conversa);
        var novas = await ConsultaLinhas(daConversa.Where(m => m.Id > depoisDe).OrderBy(m => m.Id).Take(limite + 1)).ToListAsync(ct);
        var corte = editadasDesde > DateTime.MinValue + ChatEquipeRegras.MargemDoRelogio
            ? editadasDesde - ChatEquipeRegras.MargemDoRelogio : DateTime.MinValue;
        var editadas = await ConsultaLinhas(daConversa
            .Where(m => m.Id <= depoisDe && m.EditadaEm != null && m.EditadaEm > corte)).ToListAsync(ct);

        var pares = await LeiturasAsync(conversa, ct);
        var escrevendo = await EscrevendoAsync(conversa, eu.Id, agoraLocal, ct);
        string? subtitulo = null;
        if (ChatEquipeRegras.OutroDaDireta(conversa, eu.Id) is { } pessoaId)
        {
            var profissionalId = await db.Usuarios.AsNoTracking().Where(u => u.Id == pessoaId)
                .Select(u => u.ProfissionalId).SingleOrDefaultAsync(ct);
            subtitulo = await PresencaNaDiretaAsync(pessoaId, profissionalId, agoraLocal, ct);
        }

        return new AtualizacaoDaConversa(
            novas.Take(limite).Select(m => Mensagem(m, eu, conversa, pares)).ToList(), novas.Count > limite,
            editadas.Select(m => Mensagem(m, eu, conversa, pares)).ToList(),
            pares.Select(p => new LeituraNaConversa(p.UsuarioId, p.UltimaLidaId)).ToList(),
            escrevendo, subtitulo, agoraLocal);
    }

    public async Task<int> EnviarAsync(int usuarioId, string conversa, NovaMensagemEquipe dados, CancellationToken ct = default)
    {
        var eu = await UsuarioAsync(usuarioId, ct);
        ExigirAcesso(eu, conversa);
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
            Conversa = conversa, AutorId = eu.Id, AutorNome = eu.Nome, Texto = texto, EnviadaEm = momento,
            Mencoes = ChatEquipeRegras.GravarMencoes(await MencionadosAsync(texto, conversa, eu.Id, ct)),
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

        // Quem escreveu parou de escrever, e a própria mensagem conta como lida.
        var presenca = await db.PresencasEquipe.SingleOrDefaultAsync(p => p.UsuarioId == eu.Id, ct);
        if (presenca is null) db.PresencasEquipe.Add(presenca = new PresencaEquipe { UsuarioId = eu.Id });
        presenca.VistoEm = momento;
        presenca.EscrevendoEm = null;
        presenca.EscrevendoAte = null;
        await db.SaveChangesAsync(ct);

        await MarcarLidaAsync(eu.Id, conversa, mensagem.Id, ct);
        return mensagem.Id;
    }

    /// <summary>Cita só quem pode ler a conversa — citar alguém de fora não toca para ele.</summary>
    private async Task<IReadOnlyList<int>> MencionadosAsync(string texto, string conversa, int eu, CancellationToken ct)
    {
        if (!texto.Contains('@')) return [];
        var pessoas = await db.Usuarios.AsNoTracking().Where(u => u.Ativo && u.Id != eu).ToListAsync(ct);
        return ChatEquipeRegras.Mencionados(texto,
            pessoas.Where(p => ChatEquipeRegras.PodeVer(p, conversa)).Select(p => (p.Id, p.Nome)));
    }

    public async Task EditarAsync(int usuarioId, int mensagemId, string texto, CancellationToken ct = default)
    {
        var eu = await UsuarioAsync(usuarioId, ct);
        var m = await db.MensagensEquipe.SingleOrDefaultAsync(x => x.Id == mensagemId, ct)
                ?? throw new InvalidOperationException("Mensagem não encontrada — atualize a conversa.");
        if (m.AutorId != eu.Id) throw new UnauthorizedAccessException("Só quem escreveu corrige a mensagem.");
        ExigirAcesso(eu, m.Conversa);
        var novo = (texto ?? "").Replace("\r\n", "\n").Trim();
        if (novo == m.Texto) return;
        if (novo.Length == 0 && m.PacienteId is null && m.DocumentoClinicoId is null && m.ArquivoNome is null)
            throw new InvalidOperationException("A mensagem não pode ficar vazia — não existe apagar no chat.");
        var momento = agora();
        db.VersoesMensagemEquipe.Add(new VersaoMensagemEquipe { MensagemId = m.Id, Texto = m.Texto, SubstituidaEm = momento });
        m.Texto = novo;
        m.EditadaEm = momento;
        m.Mencoes = ChatEquipeRegras.GravarMencoes(await MencionadosAsync(novo, m.Conversa, eu.Id, ct));
        await db.SaveChangesAsync(ct);
    }

    public async Task MarcarLidaAsync(int usuarioId, string conversa, int ultimaId, CancellationToken ct = default)
    {
        if (ultimaId <= 0) return;
        var eu = await UsuarioAsync(usuarioId, ct);
        ExigirAcesso(eu, conversa);
        var l = await db.LeiturasEquipe.SingleOrDefaultAsync(x => x.UsuarioId == eu.Id && x.Conversa == conversa, ct);
        if (l is null) db.LeiturasEquipe.Add(l = new LeituraEquipe { UsuarioId = eu.Id, Conversa = conversa });
        else if (l.UltimaLidaId >= ultimaId) return;
        l.UltimaLidaId = ultimaId;
        l.LidaEm = agora();
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // A mesma pessoa com dois apps abertos marcou ao mesmo tempo: a outra janela já gravou.
            db.ChangeTracker.Clear();
        }
    }

    public async Task SinalizarAsync(int usuarioId, string? escrevendoEm, CancellationToken ct = default)
    {
        var eu = await UsuarioAsync(usuarioId, ct);
        if (escrevendoEm is not null && !ChatEquipeRegras.PodeVer(eu, escrevendoEm)) escrevendoEm = null;
        var momento = agora();
        var p = await db.PresencasEquipe.SingleOrDefaultAsync(x => x.UsuarioId == eu.Id, ct);
        if (p is null) db.PresencasEquipe.Add(p = new PresencaEquipe { UsuarioId = eu.Id });
        p.VistoEm = momento;
        p.EscrevendoEm = escrevendoEm;
        p.EscrevendoAte = escrevendoEm is null ? null : momento + ChatEquipeRegras.ValidadeEscrevendo;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); }
    }

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
