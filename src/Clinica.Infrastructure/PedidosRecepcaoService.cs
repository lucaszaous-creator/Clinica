using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure;

/// <summary>
/// Os pedidos à recepção (out/2026). O pedido mora na sua tabela; o chat mostra dois CARTÕES dele —
/// no # Recepção, para quem trata, e na conversa de quem pediu com o Sistema SemDor — e os dois
/// mudam juntos quando alguém assume ou encerra. O desfecho chega a quem pediu como mensagem nova
/// (com o toque de uma direta). Nada aqui mexe na agenda: remarcar é a recepção, pela agenda.
/// </summary>
public sealed class PedidosRecepcaoService : IPedidosRecepcaoService
{
    private readonly ClinicaDbContext db;
    private readonly Func<DateTime> agora;

    public PedidosRecepcaoService(ClinicaDbContext db) : this(db, () => DateTime.Now) { }

    public PedidosRecepcaoService(ClinicaDbContext db, Func<DateTime> agora)
    {
        this.db = db;
        this.agora = agora;
    }

    private async Task<UsuarioSistema> UsuarioAsync(int usuarioId, CancellationToken ct)
        => await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Id == usuarioId && u.Ativo, ct)
           ?? throw new InvalidOperationException("Entre com o seu usuário para usar os pedidos à recepção.");

    private static PedidoNaLista Linha(PedidoRecepcao p, UsuarioSistema eu) => new(p.Id, p.Tipo,
        PedidosRecepcaoRegras.RotuloDoTipo(p.Tipo), p.PacienteId, p.PacienteNome, p.AgendamentoId, p.HorarioEm, p.Detalhe,
        p.CriadoPorId, p.CriadoPorNome, p.CriadoEm, p.Situacao, PedidosRecepcaoRegras.RotuloDaSituacao(p.Situacao),
        p.ResponsavelNome, p.AssumidoEm, p.Desfecho, p.EncerradoEm, PedidosRecepcaoRegras.PodeTratar(eu), p.CriadoPorId == eu.Id);

    public async Task<PedidoNaLista> CriarAsync(int usuarioId, NovoPedidoRecepcao dados, CancellationToken ct = default)
    {
        var eu = await UsuarioAsync(usuarioId, ct);
        if (!PedidosRecepcaoRegras.PodePedir(eu)) throw new UnauthorizedAccessException("Seu usuário não pode fazer pedidos à recepção.");
        if (dados.Idempotencia == Guid.Empty) throw new InvalidOperationException("Pedido sem identificador — tente de novo.");

        // O mesmo comando de novo (duplo clique, resposta perdida): devolve o pedido que ele criou.
        var repetido = await db.PedidosRecepcao.AsNoTracking().SingleOrDefaultAsync(p => p.Idempotencia == dados.Idempotencia, ct);
        if (repetido is not null)
        {
            if (repetido.CriadoPorId != eu.Id) throw new InvalidOperationException("Pedido já utilizado.");
            return Linha(repetido, eu);
        }

        var tipo = (dados.Tipo ?? "").Trim().ToLowerInvariant();
        if (!PedidosRecepcaoRegras.TipoValido(tipo)) throw new InvalidOperationException("Escolha o que pedir à recepção.");
        var detalhe = (dados.Detalhe ?? "").Replace("\r\n", "\n").Trim();
        if (detalhe.Length > PedidosRecepcaoRegras.DetalheMaximo)
            throw new InvalidOperationException($"O pedido passa de {PedidosRecepcaoRegras.DetalheMaximo} caracteres. Resuma.");

        string pacienteNome = "";
        DateTime? horario = null;
        int? pacienteId = dados.PacienteId;
        if (dados.AgendamentoId is { } agId)
        {
            var ag = await db.Agendamentos.AsNoTracking().Where(a => a.Id == agId)
                         .Select(a => new { a.PacienteId, a.DataHora, a.Status, a.ProfissionalId, Nome = a.Paciente!.Nome })
                         .SingleOrDefaultAsync(ct)
                     ?? throw new InvalidOperationException("O horário escolhido não foi encontrado — atualize e escolha de novo.");
            if (pacienteId is { } pid && pid != ag.PacienteId)
                throw new InvalidOperationException("O horário escolhido não é deste paciente.");
            // Quem vê o próprio horário pode pedir sobre ele; os outros precisam ver a agenda ou a ficha.
            var dele = eu.ProfissionalId is { } meu && ag.ProfissionalId == meu;
            if (!dele && !eu.Pode(Permissao.VerAgenda) && !eu.Pode(Permissao.VerFichaPaciente))
                throw new UnauthorizedAccessException("Seu acesso não alcança este horário.");
            pacienteId = ag.PacienteId;
            pacienteNome = ag.Nome;
            horario = ag.DataHora;
        }
        else if (pacienteId is { } pid)
        {
            if (!eu.Pode(Permissao.VerFichaPaciente) && !eu.Pode(Permissao.VerAgenda))
                throw new UnauthorizedAccessException("Seu acesso não abre a ficha do paciente.");
            pacienteNome = await db.Pacientes.AsNoTracking().Where(p => p.Id == pid).Select(p => p.Nome).SingleOrDefaultAsync(ct)
                           ?? throw new InvalidOperationException("O paciente escolhido não foi encontrado — escolha de novo.");
        }
        if (PedidosRecepcaoRegras.PedeHorario(tipo) && dados.AgendamentoId is null)
            throw new InvalidOperationException($"Para {PedidosRecepcaoRegras.RotuloDoTipo(tipo).ToLowerInvariant()}, escolha o horário do paciente.");
        if (pacienteId is null && detalhe.Length == 0)
            throw new InvalidOperationException("Diga o que a recepção deve fazer.");

        return await AtomicoAsync(async () =>
        {
            var momento = agora();
            var pedido = new PedidoRecepcao
            {
                Idempotencia = dados.Idempotencia, Tipo = tipo, PacienteId = pacienteId, PacienteNome = pacienteNome,
                AgendamentoId = dados.AgendamentoId, HorarioEm = horario, Detalhe = detalhe,
                CriadoPorId = eu.Id, CriadoPorNome = eu.Nome, CriadoEm = momento, Situacao = PedidosRecepcaoRegras.Aberto,
            };
            pedido.Eventos.Add(new EventoPedidoRecepcao { UsuarioId = eu.Id, Autor = eu.Nome, Em = momento, Acao = "criou", Observacao = detalhe });

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.PedidosRecepcao.Add(pedido);
            await db.SaveChangesAsync(ct);

            // Os dois cartões já com o número do pedido: o do # Recepção (de quem pediu, como recado) e
            // o da conversa de quem pediu (do sistema, confirmando que foi entregue).
            var cartao = PedidosRecepcaoRegras.Cartao(pedido);
            var texto = PedidosRecepcaoRegras.Texto(pedido);
            var naRecepcao = GravacaoDoChat.Mensagem(ChatEquipeRegras.CanalRecepcao, eu.Id, eu.Nome, texto, cartao, false, momento);
            var paraMim = GravacaoDoChat.Mensagem(ChatEquipeRegras.Sistema(eu.Id), eu.Id, eu.Nome, texto, cartao, true, momento);
            await GravacaoDoChat.AdicionarAsync(db, naRecepcao, ct);
            await GravacaoDoChat.AdicionarAsync(db, paraMim, ct);
            await db.SaveChangesAsync(ct);
            pedido.MensagemRecepcaoId = naRecepcao.Id;
            pedido.MensagemAutorId = paraMim.Id;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            Soltar();
            return Linha(pedido, eu);
        }, ct);
    }

    public async Task<IReadOnlyList<PedidoNaLista>> ListarAsync(int usuarioId, bool comEncerrados = false, CancellationToken ct = default)
    {
        var eu = await UsuarioAsync(usuarioId, ct);
        var momento = agora();
        var q = ConsultaLista(db, eu.Id, PedidosRecepcaoRegras.PodeTratar(eu), comEncerrados, momento);
        var lista = await q.ToListAsync(ct);
        // Abertos primeiro (os mais antigos no topo: quem espera há mais tempo), depois os encerrados.
        return lista.OrderBy(p => PedidosRecepcaoRegras.Encerrado(p.Situacao))
            .ThenBy(p => PedidosRecepcaoRegras.Encerrado(p.Situacao) ? DateTime.MaxValue - (p.EncerradoEm ?? p.CriadoEm) : p.CriadoEm - DateTime.MinValue)
            .Select(p => Linha(p, eu)).ToList();
    }

    /// <summary>
    /// A lista: para quem trata, o que está aberto (e, com <paramref name="comEncerrados"/>, o que
    /// fechou nas últimas 24 h); para quem pede, os seus dos últimos 14 dias e os ainda abertos.
    /// </summary>
    public static IQueryable<PedidoRecepcao> ConsultaLista(ClinicaDbContext db, int eu, bool trata, bool comEncerrados, DateTime momento)
    {
        IQueryable<PedidoRecepcao> q = db.PedidosRecepcao.AsNoTracking();
        if (trata)
        {
            var desde = momento.AddHours(-24);
            q = comEncerrados
                ? q.Where(p => p.Situacao == PedidosRecepcaoRegras.Aberto || p.Situacao == PedidosRecepcaoRegras.EmAndamento
                               || (p.EncerradoEm != null && p.EncerradoEm >= desde) || p.CriadoPorId == eu)
                : q.Where(p => p.Situacao == PedidosRecepcaoRegras.Aberto || p.Situacao == PedidosRecepcaoRegras.EmAndamento);
        }
        else
        {
            var desde = momento.AddDays(-14);
            q = q.Where(p => p.CriadoPorId == eu && (p.CriadoEm >= desde
                || p.Situacao == PedidosRecepcaoRegras.Aberto || p.Situacao == PedidosRecepcaoRegras.EmAndamento));
        }
        return q.OrderByDescending(p => p.CriadoEm).Take(100);
    }

    public Task<PedidoNaLista> AssumirAsync(int usuarioId, int pedidoId, CancellationToken ct = default)
        => MudarAsync(usuarioId, pedidoId, "assumir", null, ct);

    public Task<PedidoNaLista> EncerrarAsync(int usuarioId, int pedidoId, bool feito, string desfecho, CancellationToken ct = default)
        => MudarAsync(usuarioId, pedidoId, feito ? "concluir" : "nao-feito", desfecho, ct);

    private async Task<PedidoNaLista> MudarAsync(int usuarioId, int pedidoId, string acao, string? desfecho, CancellationToken ct)
    {
        var eu = await UsuarioAsync(usuarioId, ct);
        if (!PedidosRecepcaoRegras.PodeTratar(eu))
            throw new UnauthorizedAccessException("Quem trata os pedidos é quem marca e remarca na agenda.");
        return await AtomicoAsync(async () =>
        {
            var pedido = await db.PedidosRecepcao.SingleOrDefaultAsync(p => p.Id == pedidoId, ct)
                         ?? throw new InvalidOperationException("Pedido não encontrado — atualize a lista.");
            if (PedidosRecepcaoRegras.RecusaDaTransicao(pedido.Situacao, acao) is { } recusa)
                throw new InvalidOperationException(recusa);
            var texto = (desfecho ?? "").Replace("\r\n", "\n").Trim();
            if (texto.Length > PedidosRecepcaoRegras.DetalheMaximo)
                throw new InvalidOperationException($"O desfecho passa de {PedidosRecepcaoRegras.DetalheMaximo} caracteres. Resuma.");
            if (acao == "nao-feito" && texto.Length == 0)
                throw new InvalidOperationException("Diga por que não foi possível — a resposta vai para quem pediu.");

            var momento = agora();
            if (acao == "assumir")
            {
                // Assumir de novo o que já é meu não muda nada.
                if (pedido.ResponsavelId == eu.Id && pedido.Situacao == PedidosRecepcaoRegras.EmAndamento) { Soltar(); return Linha(pedido, eu); }
                pedido.Situacao = PedidosRecepcaoRegras.EmAndamento;
                pedido.AssumidoEm = momento;
            }
            else
            {
                pedido.Situacao = acao == "concluir" ? PedidosRecepcaoRegras.Concluido : PedidosRecepcaoRegras.NaoFeito;
                pedido.Desfecho = texto;
                pedido.EncerradoEm = momento;
            }
            pedido.ResponsavelId = eu.Id;
            pedido.ResponsavelNome = eu.Nome;
            pedido.Versao = Guid.NewGuid();
            db.EventosPedidoRecepcao.Add(new EventoPedidoRecepcao
            {
                PedidoRecepcaoId = pedido.Id, UsuarioId = eu.Id, Autor = eu.Nome, Em = momento,
                Acao = acao == "assumir" ? "assumiu" : acao == "concluir" ? "concluiu" : "nao-feito", Observacao = texto,
            });

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // O pedido do PACIENTE não tem quem receber o desfecho no chat: o paciente recebe a
            // confirmação pelo WhatsApp (RemarcacaoPeloPacienteService), e só quando a agenda mudou.
            if (PedidosRecepcaoRegras.Encerrado(pedido.Situacao) && !PedidosRecepcaoRegras.DoPaciente(pedido))
            {
                // O desfecho chega a quem pediu sem ele perguntar: uma mensagem do sistema, na conversa dele.
                var aviso = GravacaoDoChat.Mensagem(ChatEquipeRegras.Sistema(pedido.CriadoPorId), pedido.CriadoPorId, pedido.CriadoPorNome,
                    PedidosRecepcaoRegras.Desfecho(pedido), null, true, momento, pedido.PacienteId, pedido.PacienteId is null ? null : pedido.PacienteNome);
                await GravacaoDoChat.AdicionarAsync(db, aviso, ct);
            }
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                Soltar();
                throw new InvalidOperationException("Outra pessoa mexeu neste pedido agora — atualize a lista.");
            }
            var cartao = PedidosRecepcaoRegras.Cartao(pedido);
            var resumo = PedidosRecepcaoRegras.Texto(pedido);
            await GravacaoDoChat.AtualizarCartaoAsync(db, pedido.MensagemRecepcaoId, resumo, cartao, momento, ct);
            await GravacaoDoChat.AtualizarCartaoAsync(db, pedido.MensagemAutorId, resumo, cartao, momento, ct);
            await tx.CommitAsync(ct);
            Soltar();
            return Linha(pedido, eu);
        }, ct);
    }

    public async Task<IReadOnlyList<HorarioParaPedido>> HorariosAsync(int usuarioId, int pacienteId, CancellationToken ct = default)
    {
        var eu = await UsuarioAsync(usuarioId, ct);
        var momento = agora();
        var ate = momento.AddDays(60);
        var q = db.Agendamentos.AsNoTracking()
            .Where(a => a.PacienteId == pacienteId && a.Status == StatusAgendamento.Agendado && a.DataHora >= momento.Date && a.DataHora < ate);
        // Sem a agenda nem a ficha, quem atende vê só os horários que são dele.
        if (!eu.Pode(Permissao.VerAgenda) && !eu.Pode(Permissao.VerFichaPaciente))
        {
            var meu = eu.ProfissionalId ?? -1;
            q = q.Where(a => a.ProfissionalId == meu);
        }
        var linhas = await q.OrderBy(a => a.DataHora).Take(20)
            .Select(a => new { a.Id, a.DataHora, a.ModalidadePrevista, Profissional = a.Profissional == null ? null : a.Profissional.Nome })
            .ToListAsync(ct);
        return linhas.Select(a => new HorarioParaPedido(a.Id, a.DataHora,
            PedidosRecepcaoRegras.Horario(a.DataHora) + " · " + Clinica.Domain.ModalidadeInfo.NomeExibicao(a.ModalidadePrevista)
            + (a.Profissional is null ? "" : " · " + a.Profissional))).ToList();
    }

    /// <summary>
    /// Transação sob a estratégia de execução: o <c>AddClinica</c> liga a repetição do Npgsql, que
    /// RECUSA transação aberta à mão fora dela (o mesmo achado de <c>TransacaoDaPonte</c>). Repetir
    /// é seguro: criar é idempotente pela chave do comando, e mudar relê o pedido e confere a
    /// transição de novo. O que a tentativa que falhou deixou rastreado sai antes de repetir.
    /// </summary>
    private Task<T> AtomicoAsync<T>(Func<Task<T>> operacao, CancellationToken ct)
    {
        var tentativa = 0;
        return db.Database.CreateExecutionStrategy().ExecuteAsync(async _ =>
        {
            if (tentativa++ > 0) Soltar();
            return await operacao();
        }, ct);
    }

    /// <summary>O contexto vive o escopo inteiro: o que esta operação gravou não fica rastreado para a próxima.</summary>
    private void Soltar()
    {
        foreach (var e in db.ChangeTracker.Entries().Where(e => e.Entity is PedidoRecepcao or EventoPedidoRecepcao or MensagemEquipe or ConversaEquipe).ToList())
            e.State = EntityState.Detached;
    }
}
