using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>Uma sessão particular que a recuperação gravaria (ou não, quando falta preço). Só leitura.</summary>
public sealed record SessaoParticularSemEntrada(
    int AtendimentoId, int? AgendamentoId, int PacienteId, string Paciente, DateOnly Data,
    string Modalidade, decimal? Valor, string? Procedencia)
{
    /// <summary>Sem histórico e sem tabela: a recepção completa o valor (a recuperação não inventa preço).</summary>
    public bool SemPreco => Valor is null;
}

/// <summary>
/// O PARTICULAR ENTRA NO CAIXA QUANDO É LANÇADO (nov/2026, decisão do dono: "no momento do lançamento já deve ser
/// entendido que entrou dinheiro").
///
/// Por que existe: quem lança a sessão particular é quase sempre o MÉDICO (Consultório, portal do posto, conclusão
/// automática), e a pergunta "Pago agora / Fica a receber" só existia no fechamento da Recepção — que, depois disso,
/// nunca roda. Em produção a tabela de lançamentos estava VAZIA: nenhuma sessão particular tinha virado dinheiro.
///
/// A regra é uma só e mora aqui; a porta é o <c>AgendaService.ConfirmarNucleoAsync</c>, por onde passam TODAS as
/// formas de lançar (presença da Fila, Concluir do Consultório, posto/tablet, conclusão automática, recuperação de
/// guias, fechamento da Recepção). A entrada nasce no MESMO SaveChanges do atendimento:
/// <list type="bullet">
/// <item>RECEBIDA (<see cref="StatusLancamento.Realizado"/>, pagamento = dia da sessão), SEM forma de pagamento
/// (o dono decidiu não perguntar), presa ao atendimento E ao horário;</item>
/// <item>valor = o da última entrada do paciente (o combinado); senão a tabela do particular (como o fechamento);</item>
/// <item>sem preço → não grava (não inventa valor): a sessão aparece na aba Particulares da Conciliação, "sem dinheiro
/// registrado", para a recepção completar;</item>
/// <item>com PACOTE de saldo → nenhuma receita nova (a sessão já foi comprada);</item>
/// <item>idempotente: entrada já presa ao horário (pagamento na marcação, fechamento) ou ao atendimento → nada;</item>
/// <item>estorno do atendimento cancela as entradas presas a ele (<c>EstornoAtendimentoService</c>, como já fazia).</item>
/// </list>
/// </summary>
public sealed class ReceitaDoParticularService
{
    private readonly IClinicaRepositorio _repo;

    public ReceitaDoParticularService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>
    /// Acrescenta ao contexto (sem salvar) a entrada recebida do particular deste horário, quando cabe. Devolve a
    /// entrada acrescentada, ou nulo quando não cabe (não é particular, já tem entrada, tem pacote, falta preço).
    /// Falha de leitura não derruba o lançamento do atendimento: vira log e a sessão segue para a Conciliação.
    /// </summary>
    public async Task<LancamentoFinanceiro?> AcrescentarAsync(
        Agendamento ag, Atendimento atendimento, CancellationToken ct = default)
    {
        try
        {
            var dia = atendimento.Data;
            var paciente = ag.Paciente ?? atendimento.Paciente ?? await _repo.ObterPacienteAsync(atendimento.PacienteId, ct);
            if (!EhParticular(paciente, ag.Particular || atendimento.Particular)) return null;
            if ((await _repo.EntradasDoHorarioAsync(ag.Id, ct)).Count > 0) return null;
            if (atendimento.Id != 0 && await TemEntradaAsync(atendimento.Id, ct)) return null;
            if (await new PacoteService(_repo).ADebitarAsync(atendimento.PacienteId, dia, ct) is not null) return null;

            var (valor, _) = await ValorAsync(atendimento.PacienteId, atendimento.ModalidadeCodigo, atendimento.Modalidade,
                atendimento.EspecialidadeConsultaCodigo ?? atendimento.EspecialidadeConsulta?.ToString(), dia, ag.ProfissionalId, ct);
            if (valor is not { } v || v <= 0) return null;

            var lancamento = Montar(atendimento.PacienteId, paciente?.Nome,
                dia, atendimento.ModalidadeCodigo, atendimento.Modalidade, v);
            lancamento.AgendamentoId = ag.Id == 0 ? null : ag.Id;
            if (ag.Id == 0) lancamento.Agendamento = ag;
            if (atendimento.Id != 0) lancamento.AtendimentoId = atendimento.Id;
            else lancamento.Atendimento = atendimento; // a navegação: o Id nasce no mesmo SaveChanges
            await _repo.AdicionarLancamentoAsync(lancamento, ct);
            return lancamento;
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Particular no lançamento — a entrada não pôde ser preparada", ex);
            return null;
        }
    }

    /// <summary>A sessão é particular de verdade: marcada assim, ou ficha particular (NÃO a ficha "a definir").</summary>
    public static bool EhParticular(Paciente? paciente, bool marcadaComoParticular)
        => marcadaComoParticular || (paciente is not null && !paciente.ConvenioADefinir && SessaoParticular.PelaFicha(paciente));

    /// <summary>O combinado com esta pessoa (última entrada recebida); senão o preço de lista do particular.</summary>
    public async Task<(decimal? Valor, string? Procedencia)> ValorAsync(
        int pacienteId, string? modalidadeCodigo, ModalidadeAtendimento familia, string? especialidade,
        DateOnly dia, int? profissionalId, CancellationToken ct = default)
    {
        var ultimo = await _repo.UltimoRecebimentoDoPacienteAsync(pacienteId, ct);
        if (ultimo is not null && ultimo.Valor > 0) return (ultimo.Valor, $"igual à sessão de {ultimo.Data:dd/MM/yyyy}");
        var proposto = await new PrecoParticularService(_repo).ProporAsync(modalidadeCodigo, familia, especialidade, dia, ct, profissionalId);
        return proposto.Houve ? (proposto.Valor, proposto.Procedencia) : (null, null);
    }

    /// <summary>
    /// RECUPERAÇÃO desde <paramref name="desde"/> (nov/2026: o dono mandou registrar os anteriores desde 10/10/2026).
    /// SIMULAÇÃO por padrão: lista cada sessão particular realizada, não estornada, sem entrada viva e sem pacote
    /// debitado (a MESMA definição da aba Particulares da Conciliação), com o valor que seria gravado — e as sem preço.
    /// Paciente com pacote de saldo naquele dia fica de fora (a sessão é do pacote, que se debita pela Recepção).
    /// Com <paramref name="aplicar"/>, grava as que têm preço (recebidas, sem forma, presas ao atendimento e ao
    /// horário) num SaveChanges só; as sem preço continuam na Conciliação.
    /// </summary>
    public async Task<IReadOnlyList<SessaoParticularSemEntrada>> RecuperarAsync(
        DateOnly desde, DateOnly ate, bool aplicar = false, CancellationToken ct = default)
    {
        var lista = new List<SessaoParticularSemEntrada>();
        var pacotes = new PacoteService(_repo);
        // ⚠️ SEQUENCIAL: o mesmo DbContext.
        foreach (var s in await _repo.SessoesParticularesSemReceitaAsync(desde, ate, ct))
        {
            if (await pacotes.ADebitarAsync(s.PacienteId, s.Data, ct) is not null) continue;
            var horario = await _repo.AgendamentoDoAtendimentoAsync(s.AtendimentoId, ct);
            if (horario is not null && (await _repo.EntradasDoHorarioAsync(horario.Id, ct)).Count > 0) continue;
            var (valor, procedencia) = await ValorAsync(s.PacienteId, s.ModalidadeCodigo, s.Modalidade,
                s.CodigoDaEspecialidade, s.Data, s.ProfissionalId ?? horario?.ProfissionalId, ct);
            lista.Add(new(s.AtendimentoId, horario?.Id, s.PacienteId, s.Paciente, s.Data, s.ModalidadeNome, valor, procedencia));
            if (aplicar && valor is { } v && v > 0)
            {
                var l = Montar(s.PacienteId, s.Paciente, s.Data, s.ModalidadeCodigo, s.Modalidade, v);
                l.AtendimentoId = s.AtendimentoId; l.AgendamentoId = horario?.Id;
                await _repo.AdicionarLancamentoAsync(l, ct);
            }
        }
        if (aplicar) await _repo.SalvarAsync(ct);
        return lista;
    }

    static LancamentoFinanceiro Montar(int pacienteId, string? nome, DateOnly dia, string? modalidadeCodigo,
        ModalidadeAtendimento modalidade, decimal valor) => new()
    {
        Data = dia,
        Tipo = TipoLancamento.Entrada,
        Descricao = FinanceiroService.DescricaoDaSessao(dia, CatalogoModalidades.Nome(modalidadeCodigo, modalidade), nome ?? "(paciente)"),
        Valor = valor,
        Status = StatusLancamento.Realizado,
        DataPagamento = dia,
        FormaPagamento = null, // decisão do dono: não perguntar como pagou
        PacienteId = pacienteId,
    };

    async Task<bool> TemEntradaAsync(int atendimentoId, CancellationToken ct)
        => (await _repo.LancamentosDosAtendimentosAsync([atendimentoId], ct))
            .Any(l => l.Tipo == TipoLancamento.Entrada && l.Status != StatusLancamento.Cancelado);
}
