using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure;

/// <summary>Uma mensagem ao paciente na fila do CRM: o texto e, na oferta, os botões (id + rótulo).</summary>
public sealed record MensagemAoPaciente(string Id, string Telefone, string Texto, IReadOnlyList<(string Id, string Titulo)> Opcoes);

/// <summary>
/// A remarcação pedida pelo PACIENTE (@SemDor, out/2026 — docs/semdor-assistente.md). O paciente toca
/// "Remarcar" no lembrete; a ponte registra o pedido (<see cref="RegistrarPedidoAsync"/>) e o cartão
/// aparece no # Recepção. A recepção oferece até 3 horários livres; o CRM busca a mensagem na fila
/// (<see cref="MensagensPendentesAsync"/>), manda os botões e conta o resultado; a escolha do paciente
/// volta (<see cref="EscolherAsync"/>); e um clique da recepção remarca pela agenda, com os conflitos
/// conferidos DE NOVO, e põe a confirmação na fila.
///
/// Nada aqui remarca sozinho: a agenda só muda no clique de quem trata pedidos, e o paciente só ouve
/// "remarcado" depois que mudou.
/// </summary>
public sealed class RemarcacaoPeloPacienteService : IRemarcacaoPeloPacienteService
{
    private readonly ClinicaDbContext db;
    private readonly AgendaService agenda;
    private readonly BuscaDeVagasService vagas;
    private readonly Func<DateTime> agora;

    public RemarcacaoPeloPacienteService(ClinicaDbContext db, AgendaService agenda, BuscaDeVagasService vagas)
        : this(db, agenda, vagas, () => DateTime.Now) { }

    public RemarcacaoPeloPacienteService(ClinicaDbContext db, AgendaService agenda, BuscaDeVagasService vagas, Func<DateTime> agora)
    {
        this.db = db;
        this.agenda = agenda;
        this.vagas = vagas;
        this.agora = agora;
    }

    /// <summary>A oferta fica na fila por 6 horas: depois disso os horários já envelheceram.</summary>
    private static readonly TimeSpan ValidadeDaOferta = TimeSpan.FromHours(6);

    /// <summary>A confirmação de remarcado fica na fila por um dia.</summary>
    private static readonly TimeSpan ValidadeDaResposta = TimeSpan.FromDays(1);

    // ================================================================ a ponte: o pedido nasce

    /// <summary>
    /// O paciente tocou "Remarcar" no lembrete: abre o pedido de origem paciente e o cartão no
    /// # Recepção. Roda DENTRO da transação de quem chama (a decisão da ponte) e grava com ela; um
    /// pedido do paciente ainda aberto para o mesmo horário não ganha outro.
    /// </summary>
    public static async Task RegistrarPedidoAsync(ClinicaDbContext db, Agendamento ap, string telefone, DateTime momento, CancellationToken ct)
    {
        var jaAberto = await db.PedidosRecepcao.AnyAsync(p => p.AgendamentoId == ap.Id && p.Origem == RemarcacaoPeloPacienteRegras.OrigemPaciente
            && (p.Situacao == PedidosRecepcaoRegras.Aberto || p.Situacao == PedidosRecepcaoRegras.EmAndamento), ct);
        if (jaAberto) return;
        var tecnico = await UsuarioTecnicoAsync(db, ct);
        var nome = ap.Paciente?.Nome ?? await db.Pacientes.Where(p => p.Id == ap.PacienteId).Select(p => p.Nome).SingleAsync(ct);
        var pedido = new PedidoRecepcao
        {
            Idempotencia = Guid.NewGuid(), Tipo = "remarcar", PacienteId = ap.PacienteId, PacienteNome = nome,
            AgendamentoId = ap.Id, HorarioEm = ap.DataHora, CriadoPorId = tecnico.Id, CriadoPorNome = tecnico.Nome, CriadoEm = momento,
            Situacao = PedidosRecepcaoRegras.Aberto, Origem = RemarcacaoPeloPacienteRegras.OrigemPaciente, TelefonePaciente = telefone,
        };
        pedido.Eventos.Add(new EventoPedidoRecepcao { UsuarioId = tecnico.Id, Autor = tecnico.Nome, Em = momento, Acao = "criou",
            Observacao = "O paciente tocou Remarcar no lembrete do WhatsApp." });
        db.PedidosRecepcao.Add(pedido);
        await db.SaveChangesAsync(ct);
        var cartao = GravacaoDoChat.Mensagem(ChatEquipeRegras.CanalRecepcao, tecnico.Id, tecnico.Nome, PedidosRecepcaoRegras.Texto(pedido),
            PedidosRecepcaoRegras.Cartao(pedido), true, momento, pedido.PacienteId, nome);
        await GravacaoDoChat.AdicionarAsync(db, cartao, ct);
        await db.SaveChangesAsync(ct);
        pedido.MensagemRecepcaoId = cartao.Id;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// O usuário técnico do SemDor. A migration o cria no Postgres; no banco dos testes (criado pelo
    /// modelo) ele nasce aqui, na primeira vez.
    /// </summary>
    public static async Task<UsuarioSistema> UsuarioTecnicoAsync(ClinicaDbContext db, CancellationToken ct)
    {
        var u = await db.Usuarios.SingleOrDefaultAsync(x => x.Login == UsuarioTecnico.Login, ct);
        if (u is not null) return u;
        u = new UsuarioSistema
        {
            Nome = UsuarioTecnico.Nome, Login = UsuarioTecnico.Login, Ativo = false, Perfil = PerfilAcesso.Recepcao,
            SenhaHash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=", SenhaSalt = "AAAAAAAAAAAAAAAAAAAAAA==",
            Observacoes = "Usuário técnico do @SemDor (pedidos do paciente pelo WhatsApp). Não entra no sistema.",
        };
        db.Usuarios.Add(u);
        await db.SaveChangesAsync(ct);
        return u;
    }

    // ================================================================ a tela: oferecer e remarcar

    private async Task<UsuarioSistema> QuemTrataAsync(int usuarioId, CancellationToken ct)
    {
        var eu = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Id == usuarioId && u.Ativo, ct)
                 ?? throw new InvalidOperationException("Entre com o seu usuário para tratar os pedidos.");
        if (!PedidosRecepcaoRegras.PodeTratar(eu))
            throw new UnauthorizedAccessException("Quem trata os pedidos é quem marca e remarca na agenda.");
        return eu;
    }

    /// <summary>O pedido do paciente ainda aberto, com o horário dele ainda marcado e no futuro.</summary>
    private async Task<(PedidoRecepcao Pedido, Agendamento Horario)> PedidoAbertoAsync(int pedidoId, bool rastrear, CancellationToken ct)
    {
        var q = rastrear ? db.PedidosRecepcao : db.PedidosRecepcao.AsNoTracking();
        var pedido = await q.SingleOrDefaultAsync(p => p.Id == pedidoId, ct)
                     ?? throw new InvalidOperationException("Pedido não encontrado — atualize a conversa.");
        if (!PedidosRecepcaoRegras.DoPaciente(pedido) || pedido.AgendamentoId is null)
            throw new InvalidOperationException("Este pedido não veio do paciente pelo WhatsApp.");
        if (PedidosRecepcaoRegras.Encerrado(pedido.Situacao)) throw new InvalidOperationException("Este pedido já foi encerrado.");
        var qa = rastrear ? db.Agendamentos : db.Agendamentos.AsNoTracking();
        var ap = await qa.Include(a => a.Paciente).SingleOrDefaultAsync(a => a.Id == pedido.AgendamentoId, ct)
                 ?? throw new InvalidOperationException("O horário do paciente não foi encontrado.");
        if (ap.Status != StatusAgendamento.Agendado || ap.DataHora <= agora() || ap.ChegadaEm is not null || ap.ProfissionalId is null)
            throw new InvalidOperationException("O horário do paciente mudou na agenda — confira por lá e encerre o pedido.");
        return (pedido, ap);
    }

    private async Task<IReadOnlyList<DateTime>> LivresAsync(Agendamento ap, CancellationToken ct)
    {
        var momento = agora();
        var r = await vagas.ProximasComRecursosAsync(ap.ProfissionalId!.Value, RemarcacaoPeloPacienteRegras.InicioDaBusca(momento),
            ap.DuracaoEfetiva, ap.SalaId, ap.PacienteId, ct);
        return r.Vagas.Select(v => v.Inicio).Where(h => h <= momento.AddDays(RemarcacaoPeloPacienteRegras.DiasMaximos)).ToList();
    }

    public async Task<IReadOnlyList<VagaParaOferta>> VagasAsync(int usuarioId, int pedidoId, CancellationToken ct = default)
    {
        await QuemTrataAsync(usuarioId, ct);
        var (_, ap) = await PedidoAbertoAsync(pedidoId, false, ct);
        return (await LivresAsync(ap, ct)).Take(RemarcacaoPeloPacienteRegras.VagasSugeridas)
            .Select(h => new VagaParaOferta(h, PedidosRecepcaoRegras.Horario(h))).ToList();
    }

    public async Task OferecerAsync(int usuarioId, int pedidoId, IReadOnlyList<DateTime> horarios, CancellationToken ct = default)
    {
        var eu = await QuemTrataAsync(usuarioId, ct);
        await AtomicoAsync(async () =>
        {
            var (pedido, ap) = await PedidoAbertoAsync(pedidoId, true, ct);
            var escolhidos = horarios.OrderBy(h => h).ToList();
            if (RemarcacaoPeloPacienteRegras.RecusaDaOferta(escolhidos, (await LivresAsync(ap, ct)).ToHashSet(), agora()) is { } recusa)
                throw new InvalidOperationException(recusa);
            if (pedido.TelefonePaciente.Length == 0) throw new InvalidOperationException("O pedido não tem o WhatsApp do paciente.");
            var momento = agora();
            pedido.HorariosOferecidos = RemarcacaoPeloPacienteRegras.Gravar(escolhidos);
            pedido.OfertaToken = RemarcacaoPeloPacienteRegras.NovaChave();
            pedido.OferecidoEm = momento;
            pedido.OfertaTratadaEm = null;
            pedido.OfertaFalha = "";
            pedido.HorarioEscolhido = null;
            pedido.EscolhidoEm = null;
            pedido.Situacao = PedidosRecepcaoRegras.EmAndamento;
            if (pedido.ResponsavelId is null) pedido.AssumidoEm = momento;
            pedido.ResponsavelId = eu.Id;
            pedido.ResponsavelNome = eu.Nome;
            pedido.Versao = Guid.NewGuid();
            db.EventosPedidoRecepcao.Add(new EventoPedidoRecepcao { PedidoRecepcaoId = pedido.Id, UsuarioId = eu.Id, Autor = eu.Nome, Em = momento,
                Acao = "ofereceu", Observacao = string.Join(" · ", escolhidos.Select(PedidosRecepcaoRegras.Horario)) });
            await GravarComCartaoAsync(pedido, momento, ct);
            return true;
        }, ct);
    }

    public async Task<DateTime> RemarcarAsync(int usuarioId, int pedidoId, string operador, CancellationToken ct = default)
    {
        var eu = await QuemTrataAsync(usuarioId, ct);
        return await AtomicoAsync(async () =>
        {
            var (pedido, ap) = await PedidoAbertoAsync(pedidoId, true, ct);
            var novo = pedido.HorarioEscolhido ?? throw new InvalidOperationException("O paciente ainda não escolheu um horário.");
            // O horário pode ter sido ocupado entre a oferta e agora: a mesma conferência da remarcação
            // pela ponte — qualquer conflito recusa, e o horário precisa estar entre as vagas de agora.
            var conflitos = await agenda.ConflitosAsync(novo, ap.DuracaoEfetiva, ap.ProfissionalId, ap.SalaId, ap.PacienteId, ap.Id, ct);
            if (conflitos.Count > 0 || !(await LivresAsync(ap, ct)).Contains(novo))
                throw new InvalidOperationException("O horário que o paciente escolheu foi ocupado. Ofereça outros horários.");

            var momento = agora();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await agenda.RemarcarAsync(ap.Id, novo, ap.Observacoes, operador: operador, ct: ct);
            // A confirmação do horário recomeça: o lembrete do horário novo é outro (como na ponte).
            var contato = await db.Contatos.SingleOrDefaultAsync(c => c.Tipo == TipoContato.ConfirmacaoSessao && c.AgendamentoId == ap.Id, ct);
            if (contato is not null)
            {
                contato.Referencia = DateOnly.FromDateTime(novo);
                contato.Status = StatusContato.Pendente;
                contato.EnviadoEm = null; contato.EnviadoPor = null; contato.RespondidoEm = null;
            }
            pedido.Situacao = PedidosRecepcaoRegras.Concluido;
            pedido.Desfecho = $"Remarcado para {PedidosRecepcaoRegras.Horario(novo)}, o horário que o paciente escolheu. A confirmação vai a ele pelo WhatsApp.";
            pedido.EncerradoEm = momento;
            pedido.ResponsavelId = eu.Id;
            pedido.ResponsavelNome = eu.Nome;
            pedido.RespostaAoPaciente = RemarcacaoPeloPacienteRegras.TextoRemarcado(pedido.PacienteNome, novo);
            pedido.RespostaTratadaEm = null;
            pedido.Versao = Guid.NewGuid();
            db.EventosPedidoRecepcao.Add(new EventoPedidoRecepcao { PedidoRecepcaoId = pedido.Id, UsuarioId = eu.Id, Autor = eu.Nome, Em = momento,
                Acao = "concluiu", Observacao = pedido.Desfecho });
            await GravarComCartaoAsync(pedido, momento, ct);
            await tx.CommitAsync(ct);
            return novo;
        }, ct);
    }

    // ================================================================ a ponte: a fila do CRM e a escolha

    /// <summary>As mensagens ao paciente que o CRM ainda não tratou: ofertas (com botões) e confirmações.</summary>
    public async Task<IReadOnlyList<MensagemAoPaciente>> MensagensPendentesAsync(CancellationToken ct = default)
    {
        var momento = agora();
        var desdeOferta = momento - ValidadeDaOferta;
        var desdeResposta = momento - ValidadeDaResposta;
        var pedidos = await db.PedidosRecepcao.AsNoTracking()
            .Where(p => p.Origem == RemarcacaoPeloPacienteRegras.OrigemPaciente && p.TelefonePaciente != "" && (
                (p.OfertaToken != null && p.OferecidoEm >= desdeOferta && p.OfertaTratadaEm == null
                 && (p.Situacao == PedidosRecepcaoRegras.Aberto || p.Situacao == PedidosRecepcaoRegras.EmAndamento))
                || (p.RespostaAoPaciente != "" && p.RespostaTratadaEm == null && p.EncerradoEm >= desdeResposta)))
            .OrderBy(p => p.Id).Take(50).ToListAsync(ct);
        var lista = new List<MensagemAoPaciente>();
        foreach (var p in pedidos)
        {
            if (p.RespostaAoPaciente.Length > 0 && p.RespostaTratadaEm is null)
            {
                lista.Add(new($"resposta:{p.Id}", p.TelefonePaciente, p.RespostaAoPaciente, []));
                continue;
            }
            var horarios = RemarcacaoPeloPacienteRegras.Ler(p.HorariosOferecidos);
            if (horarios.Count == 0 || p.OfertaToken is null) continue;
            lista.Add(new($"oferta:{p.Id}:{p.OfertaToken}", p.TelefonePaciente, RemarcacaoPeloPacienteRegras.TextoDaOferta(p.PacienteNome, horarios),
                horarios.Select((h, i) => (RemarcacaoPeloPacienteRegras.IdDaOpcao(p.OfertaToken, i), RemarcacaoPeloPacienteRegras.RotuloDoBotao(h))).ToList()));
        }
        return lista;
    }

    /// <summary>O CRM conta o que fez com uma mensagem da fila: entregue ou não, e por quê.</summary>
    public async Task ConfirmarMensagemAsync(string id, bool entregue, string? motivo, CancellationToken ct = default)
    {
        var partes = (id ?? "").Split(':');
        if (partes.Length < 2 || !int.TryParse(partes[1], out var pedidoId)) throw new InvalidOperationException("Mensagem desconhecida.");
        var falha = entregue ? "" : Cortar(string.IsNullOrWhiteSpace(motivo) ? "não chegou ao paciente" : motivo.Trim(), 200);
        await AtomicoAsync(async () =>
        {
            var pedido = await db.PedidosRecepcao.SingleOrDefaultAsync(p => p.Id == pedidoId, ct)
                         ?? throw new InvalidOperationException("Mensagem desconhecida.");
            var tecnico = await UsuarioTecnicoAsync(db, ct);
            var momento = agora();
            if (partes[0] == "oferta" && partes.Length == 3)
            {
                // A resposta de uma oferta que já foi trocada por outra não mexe na nova.
                if (pedido.OfertaToken != partes[2] || pedido.OfertaTratadaEm is not null) return true;
                pedido.OfertaTratadaEm = momento;
                pedido.OfertaFalha = falha;
                db.EventosPedidoRecepcao.Add(new EventoPedidoRecepcao { PedidoRecepcaoId = pedido.Id, UsuarioId = tecnico.Id, Autor = tecnico.Nome,
                    Em = momento, Acao = "oferta-whatsapp", Observacao = entregue ? "Os horários chegaram ao paciente." : falha });
                await GravarComCartaoAsync(pedido, momento, ct);
            }
            else if (partes[0] == "resposta")
            {
                if (pedido.RespostaTratadaEm is not null) return true;
                pedido.RespostaTratadaEm = momento;
                db.EventosPedidoRecepcao.Add(new EventoPedidoRecepcao { PedidoRecepcaoId = pedido.Id, UsuarioId = tecnico.Id, Autor = tecnico.Nome,
                    Em = momento, Acao = "resposta-whatsapp", Observacao = entregue ? "A confirmação chegou ao paciente." : falha });
                await db.SaveChangesAsync(ct);
            }
            else throw new InvalidOperationException("Mensagem desconhecida.");
            return true;
        }, ct);
    }

    /// <summary>
    /// O paciente tocou um dos horários. Devolve o que responder a ele: "recebemos a sua escolha"
    /// (a recepção ainda confirma) ou, se a oferta não vale mais, que a recepção vai falar com ele.
    /// </summary>
    public async Task<string> EscolherAsync(string chave, int indice, string telefone, CancellationToken ct = default)
    {
        return await AtomicoAsync(async () =>
        {
            var pedido = await db.PedidosRecepcao.SingleOrDefaultAsync(p => p.OfertaToken == chave, ct);
            // O telefone pela mesma chave da equipe (55 + DDD + 9 dígitos): o WhatsApp de alguns números
            // antigos chega sem o nono dígito, e a ficha o tem.
            if (pedido is null || Clinica.Domain.TelefoneDaEquipe.Chave(pedido.TelefonePaciente) is not { } dele
                || dele != Clinica.Domain.TelefoneDaEquipe.Chave(telefone) || PedidosRecepcaoRegras.Encerrado(pedido.Situacao))
                return RemarcacaoPeloPacienteRegras.TextoOfertaVencida;
            var horarios = RemarcacaoPeloPacienteRegras.Ler(pedido.HorariosOferecidos);
            if (indice < 0 || indice >= horarios.Count) return RemarcacaoPeloPacienteRegras.TextoOfertaVencida;
            var escolhido = horarios[indice];
            // O mesmo toque de novo (o WhatsApp reentrega) não repete o aviso.
            if (pedido.HorarioEscolhido == escolhido) return RemarcacaoPeloPacienteRegras.TextoDaEscolhaRecebida(escolhido);
            var tecnico = await UsuarioTecnicoAsync(db, ct);
            var momento = agora();
            pedido.HorarioEscolhido = escolhido;
            pedido.EscolhidoEm = momento;
            pedido.Versao = Guid.NewGuid();
            db.EventosPedidoRecepcao.Add(new EventoPedidoRecepcao { PedidoRecepcaoId = pedido.Id, UsuarioId = tecnico.Id, Autor = tecnico.Nome,
                Em = momento, Acao = "paciente-escolheu", Observacao = PedidosRecepcaoRegras.Horario(escolhido) });
            // O que pede ação vira aviso (regra das telas): uma mensagem nova no # Recepção, além do cartão.
            var aviso = GravacaoDoChat.Mensagem(ChatEquipeRegras.CanalRecepcao, tecnico.Id, tecnico.Nome,
                $"{pedido.PacienteNome} escolheu {PedidosRecepcaoRegras.Horario(escolhido)} pelo WhatsApp. Confirme no cartão do pedido.",
                null, true, momento, pedido.PacienteId, pedido.PacienteNome);
            await GravacaoDoChat.AdicionarAsync(db, aviso, ct);
            await GravarComCartaoAsync(pedido, momento, ct);
            return RemarcacaoPeloPacienteRegras.TextoDaEscolhaRecebida(escolhido);
        }, ct);
    }

    // ================================================================ gravação

    private async Task GravarComCartaoAsync(PedidoRecepcao pedido, DateTime momento, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            Soltar();
            throw new InvalidOperationException("Outra pessoa mexeu neste pedido agora — atualize a conversa.");
        }
        var cartao = PedidosRecepcaoRegras.Cartao(pedido);
        var texto = PedidosRecepcaoRegras.Texto(pedido);
        await GravacaoDoChat.AtualizarCartaoAsync(db, pedido.MensagemRecepcaoId, texto, cartao, momento, ct);
        await GravacaoDoChat.AtualizarCartaoAsync(db, pedido.MensagemAutorId, texto, cartao, momento, ct);
    }

    /// <summary>Sob a estratégia de execução (a repetição do Npgsql recusa transação aberta fora dela).</summary>
    private Task<T> AtomicoAsync<T>(Func<Task<T>> operacao, CancellationToken ct)
    {
        var tentativa = 0;
        return db.Database.CreateExecutionStrategy().ExecuteAsync(async _ =>
        {
            if (tentativa++ > 0) Soltar();
            try { return await operacao(); }
            finally { Soltar(); }
        }, ct);
    }

    private void Soltar()
    {
        foreach (var e in db.ChangeTracker.Entries().Where(e => e.Entity is PedidoRecepcao or EventoPedidoRecepcao or MensagemEquipe
                     or ConversaEquipe or Agendamento or ContatoCampanha).ToList())
            e.State = EntityState.Detached;
    }

    private static string Cortar(string s, int max) => s.Length <= max ? s : s[..max];
}
