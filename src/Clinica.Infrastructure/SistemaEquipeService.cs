using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure;

/// <summary>
/// O Sistema SemDor responde (out/2026): lê a pergunta com <see cref="SistemaEquipeRegras"/>, busca
/// os dados da clínica pelas MESMAS tabelas da agenda do balcão (agendamentos e a resposta ao
/// lembrete em <see cref="ContatoCampanha"/>) e devolve o texto e o cartão. Quem decide o que a
/// resposta AFIRMA são as regras (Application, com teste); aqui só se lê.
///
/// Barreiras: a agenda da própria pessoa (o profissional ligado ao login) é sempre dela; a de outro
/// profissional e a da clínica inteira pedem <see cref="Permissao.VerAgenda"/>; procurar paciente
/// pede <see cref="Permissao.VerFichaPaciente"/>. Custa de 2 a 4 idas ao banco por pergunta.
/// </summary>
public sealed class SistemaEquipeService
{
    private readonly ClinicaDbContext db;
    private readonly Func<DateTime> agora;

    public SistemaEquipeService(ClinicaDbContext db, Func<DateTime> agora)
    {
        this.db = db;
        this.agora = agora;
    }

    public async Task<(string Texto, CartaoChat? Cartao)> ResponderAsync(UsuarioSistema eu, string pergunta, CancellationToken ct = default)
    {
        var momento = agora();
        var hoje = DateOnly.FromDateTime(momento);
        var profissionais = await db.Profissionais.AsNoTracking().Where(p => p.Ativo)
            .Select(p => new { p.Id, p.Nome }).ToListAsync(ct);
        var p = SistemaEquipeRegras.Interpretar(pergunta, hoje, profissionais.Select(x => (x.Id, x.Nome)).ToList());

        switch (p.Intencao)
        {
            case IntencaoDoSistema.Ajuda:
            case IntencaoDoSistema.NaoEntendi:
                return SistemaEquipeRegras.Ajuda(p.Intencao == IntencaoDoSistema.NaoEntendi, PedidosRecepcaoRegras.PodeTratar(eu));
            case IntencaoDoSistema.Saudacao:
                return (SistemaEquipeRegras.Saudacao(eu.Nome, momento), new CartaoChat("ajuda", "Comece por aqui",
                    Sugestoes: SistemaEquipeRegras.SugestoesIniciais));
            case IntencaoDoSistema.NovoPedido:
                return ("Para pedir à recepção, toque em \"Pedir à recepção\" abaixo do campo de escrever, ou no botão da linha do horário na sua agenda. O pedido chega no # Recepção e a resposta volta para você aqui.",
                    new CartaoChat("ajuda", "Pedir à recepção", "Remarcar, cancelar, encaixar ou avisar o paciente",
                        Sugestoes: ["Minha agenda de hoje", "Agenda de amanhã", "Meus pedidos"]));
            case IntencaoDoSistema.Pedidos:
                return await PedidosAsync(eu, momento, ct);
            case IntencaoDoSistema.Paciente:
                return await PacienteAsync(eu, p.Termo!, momento, ct);
        }

        // Agenda e suas variações: de quem?
        int? profissionalId;
        string deQuem;
        if (p.ProfissionalId is { } citado && citado != eu.ProfissionalId)
        {
            if (!eu.Pode(Permissao.VerAgenda))
                return ("Seu acesso não inclui a agenda de outros profissionais. Pergunte pela sua: \"minha agenda de hoje\".", null);
            profissionalId = citado;
            deQuem = "Agenda de " + profissionais.First(x => x.Id == citado).Nome;
        }
        else if (eu.ProfissionalId is { } meu)
        {
            profissionalId = meu;
            deQuem = "Sua agenda";
        }
        else if (eu.Pode(Permissao.VerAgenda))
        {
            profissionalId = null;
            deQuem = "Agenda da clínica";
        }
        else
            return ("Seu usuário não está ligado a um profissional, e o seu acesso não inclui a agenda da clínica.", null);

        var dia = p.Intencao is IntencaoDoSistema.Proximo or IntencaoDoSistema.Chegaram ? hoje : p.Dia;
        var horarios = await HorariosAsync(dia, profissionalId, ct);
        if (p.Intencao == IntencaoDoSistema.Proximo) return SistemaEquipeRegras.Proximo(deQuem, horarios, momento);
        return SistemaEquipeRegras.Agenda(deQuem, dia, hoje, horarios, momento, profissionalId is null, p.Intencao);
    }

    /// <summary>
    /// Os horários de um dia (de um profissional, ou da clínica) com a resposta ao lembrete. Duas
    /// idas: os horários (com o nome do paciente e do profissional) e os contatos de confirmação.
    /// </summary>
    public async Task<IReadOnlyList<HorarioDoSistema>> HorariosAsync(DateOnly dia, int? profissionalId, CancellationToken ct)
    {
        var de = dia.ToDateTime(TimeOnly.MinValue);
        var linhas = await ConsultaHorarios(db, de, de.AddDays(1), profissionalId).ToListAsync(ct);
        var ids = linhas.Select(l => (int?)l.Id).ToList();
        var contatos = ids.Count == 0 ? [] : await ConsultaConfirmacoes(db, ids).ToListAsync(ct);
        var porHorario = contatos.GroupBy(c => c.AgendamentoId!.Value).ToDictionary(g => g.Key, g => g.First());

        return linhas.Select(l =>
        {
            // A etapa pela MESMA regra da fila (Agendamento.Etapa), montada com os carimbos lidos.
            var etapa = new Agendamento { Status = l.Status, ChegadaEm = l.ChegadaEm, ChamadoEm = l.ChamadoEm, InicioAtendimentoEm = l.InicioAtendimentoEm }.Etapa;
            SituacaoConfirmacao? conf = porHorario.TryGetValue(l.Id, out var c)
                ? RodadaDeConfirmacao.Classificar(c.Status, c.EnviadoEm, c.Comentario) : null;
            return new HorarioDoSistema(l.Id, l.DataHora, l.PacienteId, l.Paciente, ModalidadeInfo.NomeExibicao(l.ModalidadePrevista),
                l.Profissional, l.Status, etapa, conf);
        }).ToList();
    }

    public sealed record LinhaDoHorario(int Id, DateTime DataHora, int PacienteId, string Paciente, ModalidadeAtendimento ModalidadePrevista,
        string Profissional, StatusAgendamento Status, DateTime? ChegadaEm, DateTime? ChamadoEm, DateTime? InicioAtendimentoEm);

    public sealed record LinhaDaConfirmacao(int? AgendamentoId, StatusContato Status, DateTime? EnviadoEm, string? Comentario);

    /// <summary>Os horários do período (de um profissional, ou da clínica), só com as colunas que a resposta usa.</summary>
    public static IQueryable<LinhaDoHorario> ConsultaHorarios(ClinicaDbContext db, DateTime de, DateTime ate, int? profissionalId)
    {
        var q = db.Agendamentos.AsNoTracking().Where(a => a.DataHora >= de && a.DataHora < ate);
        if (profissionalId is { } pid) q = q.Where(a => a.ProfissionalId == pid);
        return q.OrderBy(a => a.DataHora).Take(400)
            .Select(a => new LinhaDoHorario(a.Id, a.DataHora, a.PacienteId, a.Paciente!.Nome, a.ModalidadePrevista,
                a.Profissional == null ? "" : a.Profissional.Nome, a.Status, a.ChegadaEm, a.ChamadoEm, a.InicioAtendimentoEm));
    }

    /// <summary>A resposta ao lembrete de cada horário — o mesmo contato que a agenda do balcão lê.</summary>
    public static IQueryable<LinhaDaConfirmacao> ConsultaConfirmacoes(ClinicaDbContext db, IReadOnlyCollection<int?> agendamentos)
        => db.Contatos.AsNoTracking()
            .Where(c => c.Tipo == TipoContato.ConfirmacaoSessao && agendamentos.Contains(c.AgendamentoId))
            .Select(c => new LinhaDaConfirmacao(c.AgendamentoId, c.Status, c.EnviadoEm, c.Comentario));

    private async Task<(string, CartaoChat?)> PedidosAsync(UsuarioSistema eu, DateTime momento, CancellationToken ct)
    {
        var trata = PedidosRecepcaoRegras.PodeTratar(eu);
        var lista = await new PedidosRecepcaoService(db, agora).ListarAsync(eu.Id, comEncerrados: !trata, ct);
        var linhas = lista.Take(SistemaEquipeRegras.LinhasNoCartao).Select(x => new LinhaDoCartao(
            x.CriadoEm.Date == momento.Date ? x.CriadoEm.ToString("HH:mm") : x.CriadoEm.ToString("dd/MM"),
            $"{x.TipoRotulo} · {(x.PacienteNome.Length > 0 ? x.PacienteNome : "sem paciente")}",
            trata ? $"de {x.CriadoPorNome}" + (x.ResponsavelNome.Length > 0 && !PedidosRecepcaoRegras.Encerrado(x.Situacao) ? $" · com {x.ResponsavelNome}" : "")
                  : x.Desfecho.Length > 0 ? x.Desfecho : x.Detalhe,
            x.SituacaoRotulo, PedidosRecepcaoRegras.TomDaSituacao(x.Situacao), x.PacienteId)).ToList();
        var abertos = lista.Count(x => !PedidosRecepcaoRegras.Encerrado(x.Situacao));
        var cartao = new CartaoChat("lista", trata ? "Pedidos da equipe em aberto" : "Seus pedidos à recepção",
            trata ? null : "Últimos 14 dias",
            [new("em aberto", abertos, abertos > 0 ? TomDoCartao.Atencao : TomDoCartao.Neutro)],
            linhas, ["Minha agenda de hoje"],
            lista.Count == 0 ? (trata ? "Nenhum pedido em aberto." : "Você não fez pedidos nos últimos 14 dias.") : null);
        return (SistemaEquipeRegras.TextoDoCartao(cartao), cartao);
    }

    private async Task<(string, CartaoChat?)> PacienteAsync(UsuarioSistema eu, string termo, DateTime momento, CancellationToken ct)
    {
        if (!eu.Pode(Permissao.VerFichaPaciente))
            return ("Seu acesso não abre a ficha do paciente, então não posso procurar pelo nome.", null);
        var nome = termo.Trim().ToLower();
        var achados = await db.Pacientes.AsNoTracking().Where(p => p.Nome.ToLower().Contains(nome))
            .OrderBy(p => p.Nome).Take(6).Select(p => new { p.Id, p.Nome }).ToListAsync(ct);
        if (achados.Count == 0)
            return ($"Não achei paciente com \"{termo}\" no nome.", new CartaoChat("ajuda", "Nenhum paciente encontrado",
                $"Procurei \"{termo}\"", Sugestoes: ["Minha agenda de hoje"]));

        var ids = achados.Select(a => a.Id).ToList();
        var ate = momento.AddDays(60);
        var proximos = await db.Agendamentos.AsNoTracking()
            .Where(a => ids.Contains(a.PacienteId) && a.Status == StatusAgendamento.Agendado && a.DataHora >= momento.Date && a.DataHora < ate)
            .OrderBy(a => a.DataHora).Take(30)
            .Select(a => new { a.Id, a.PacienteId, a.DataHora, a.ModalidadePrevista, Profissional = a.Profissional == null ? "" : a.Profissional.Nome })
            .ToListAsync(ct);

        var linhas = new List<LinhaDoCartao>();
        foreach (var pac in achados)
        {
            var dele = proximos.Where(a => a.PacienteId == pac.Id).ToList();
            if (dele.Count == 0) linhas.Add(new("", pac.Nome, "sem horário marcado nos próximos 60 dias", null, TomDoCartao.Neutro, pac.Id));
            foreach (var a in dele.Take(achados.Count == 1 ? 10 : 2))
                linhas.Add(new(a.DataHora.ToString("dd/MM HH:mm"), pac.Nome,
                    ModalidadeInfo.NomeExibicao(a.ModalidadePrevista) + (a.Profissional.Length > 0 ? " · " + a.Profissional : ""),
                    null, TomDoCartao.Neutro, pac.Id, a.Id, a.DataHora > momento));
        }
        var cartao = new CartaoChat("lista", achados.Count == 1 ? achados[0].Nome : $"Pacientes com \"{termo}\"",
            "Próximos horários (60 dias)", Linhas: linhas,
            Rodape: achados.Count == 6 ? "Há mais pacientes com esse nome — escreva o nome completo." : null);
        return (SistemaEquipeRegras.TextoDoCartao(cartao), cartao);
    }
}
