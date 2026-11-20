using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>
/// Repasse por GUIA RECEBIDA (nov/2026). Ver <see cref="RepasseFechamento"/> para o porquê.
///
/// ⚠️ CONTRATO FIXADO — a tela é escrita contra estas assinaturas. Mudar uma delas é
/// combinar com quem escreve a tela.
///
/// As decisões que o código não diz sozinho:
///
/// 1. <b>Recebido = entrada REALIZADA e viva.</b> Prevista é "a receber" (conciliada sem
///    confirmar o pagamento) e cancelada não existe. Data do recebimento =
///    <c>DataPagamento</c> (ou <c>Data</c>, quando ela falta).
/// 2. <b>Quem atendeu vem do HORÁRIO</b> (<see cref="Agendamento.AtendimentoId"/>; o mais
///    novo com profissional). Sem nenhum, cai no horário SUBSTITUÍDO
///    (<see cref="Agendamento.AtendimentoSubstitutoId"/>): a sessão foi lançada por fora de
///    um horário que já dizia quem ia atender, e o encaixe da recepção muitas vezes nasce sem
///    profissional. Os dois nunca somam — é um item por guia, não por horário.
/// 3. <b>Particular</b> é a definição da aba Particulares da Conciliação: todos os códigos
///    <see cref="StatusCodigo.NaoAplicavel"/>, atendimento não estornado. A sessão de pacote
///    não gera item (o dinheiro entrou na venda, sem atendimento).
/// 4. <b>Modelo antigo</b>: o <see cref="RepasseApurado"/> pagava TODA sessão do período
///    (convênio incluso), então qualquer item cujo atendimento caia num período apurado vivo
///    do mesmo profissional é bloqueado — pagá-lo de novo seria pagar duas vezes.
/// 5. <b>A regra é a da DATA DO ATENDIMENTO</b>, não a do pagamento: o acordo que valia
///    quando o médico atendeu é o que ele tem a receber.
/// </summary>
public sealed class FechamentoRepasseService
{
    /// <summary>A frase da corrida entre dois postos (também usada pela tradução do índice).</summary>
    public const string MensagemConcorrencia =
        "Outro posto fechou estes itens de repasse enquanto você conferia — atualize a tela e confira de novo.";

    /// <summary>
    /// Configuração (aaaa-MM-dd): recebimentos ANTERIORES a esta data não entram no repasse por
    /// guia. É o marco da virada: o que foi pago à mão antes do sistema não pode reaparecer
    /// pronto para fechar na primeira competência. Sem a configuração, não há piso.
    /// </summary>
    public const string ChaveRecebidosDesde = "Repasse.GuiaRecebida.RecebidosDesde";

    /// <summary>Até quantos meses para trás a prévia procura guias "aguardando o convênio".</summary>
    public const int MesesAguardando = 12;

    /// <summary>Código e nome do pagador no item particular.</summary>
    public const string CodigoParticular = "PARTICULAR";
    public const string NomeParticular = "Particular";

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly IClinicaRepositorio _repo;

    public FechamentoRepasseService(IClinicaRepositorio repo) => _repo = repo;

    // ==================== Prévia ====================

    /// <summary>
    /// Prévia (não grava nada): por profissional, os recebimentos ainda não fechados com
    /// data de recebimento até o fim da <paramref name="competencia"/> (meses anteriores
    /// INCLUSIVE — o que ficou para trás entra no próximo fechamento), os estornos pendentes,
    /// as guias aguardando o convênio e os fechamentos já feitos nesta competência.
    /// Profissional sem nada a fechar e sem fechamento na competência não aparece.
    /// </summary>
    public async Task<IReadOnlyList<PreviaRepasseProfissional>> PreviaAsync(
        DateOnly competencia, CancellationToken ct = default)
    {
        var (inicio, fim) = Mes(competencia);

        // Leituras em FILA (mesmo repositório = mesmo DbContext).
        var itens = await ItensElegiveisAsync(fim, ct);
        var aguardando = await AguardandoAsync(inicio, fim, ct);
        var fechados = (await _repo.FechamentosRepasseAsync(inicio, null, ct))
            .Where(f => f.CanceladoEm == null)
            .ToList();
        var profissionais = (await _repo.ProfissionaisAsync(ct)).ToDictionary(p => p.Id);

        var ids = itens.Select(i => i.ProfissionalId)
            .Concat(fechados.Select(f => f.ProfissionalId))
            .Distinct();

        return ids
            .Select(id =>
            {
                profissionais.TryGetValue(id, out var p);
                var doProfissional = itens.Where(i => i.ProfissionalId == id)
                    .OrderBy(i => i.Natureza)
                    .ThenBy(i => i.ConvenioNome, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(i => i.Paciente, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(i => i.DataAtendimento)
                    .ToList();
                return new PreviaRepasseProfissional(
                    id,
                    id == 0 ? "Sem profissional no horário" : p?.Nome ?? $"Profissional {id}",
                    p?.RegistroConselho,
                    doProfissional,
                    aguardando.Where(a => a.ProfissionalId == id).Select(a => a.Guia).ToList(),
                    fechados.Where(f => f.ProfissionalId == id).ToList());
            })
            // O "sem profissional" vai para o fim: é pendência de cadastro, não repasse.
            .OrderBy(p => p.ProfissionalId == 0)
            .ThenBy(p => p.Profissional, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    // ==================== Fechar ====================

    /// <summary>
    /// Fecha (confere) o repasse: grava o fechamento com os itens PRONTOS da prévia, cria a
    /// saída PREVISTA no caixa e a auditoria — tudo no mesmo SaveChanges. Recusa (com
    /// <see cref="InvalidOperationException"/> e frase para a tela) quando não há item pronto,
    /// quando o total é zero ou negativo, ou quando outro posto fechou os mesmos itens antes.
    /// </summary>
    public async Task<RepasseFechamento> FecharAsync(
        int profissionalId, DateOnly competencia, string operador,
        string? observacoes = null, int? categoriaId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(operador))
            throw new InvalidOperationException("Entre com o seu usuário para conferir o repasse.");
        if (profissionalId == 0)
            throw new InvalidOperationException(
                "Estes recebimentos não têm profissional no horário. Corrija o horário na agenda antes de fechar.");

        var (inicio, fim) = Mes(competencia);
        var obs = string.IsNullOrWhiteSpace(observacoes) ? null : observacoes.Trim();
        if (obs is { Length: > 1000 })
            throw new InvalidOperationException("A observação passa de 1000 caracteres.");

        return await _repo.ExecutarGestaoAtomicaAsync(async () =>
        {
            var profissional = await _repo.ObterProfissionalAsync(profissionalId, ct)
                ?? throw new InvalidOperationException("Profissional não encontrado.");

            // A prévia é REFEITA aqui, dentro da trava: a tela pode estar velha, e o que se
            // grava é o que o banco diz agora — nunca a lista que veio da tela.
            var prontos = (await ItensElegiveisAsync(fim, ct))
                .Where(i => i.ProfissionalId == profissionalId && i.Pronto)
                .ToList();

            if (prontos.Count == 0)
                throw new InvalidOperationException(
                    $"Não há recebimento pronto para fechar de {profissional.Nome} até {fim:dd/MM/yyyy}.");

            var total = prontos.Sum(i => i.Valor);
            if (total <= 0)
                throw new InvalidOperationException(
                    "O repasse ficou em zero ou negativo (os estornos superam os créditos) — "
                    + "ele entra no próximo fechamento, junto dos novos recebimentos.");

            var quantidade = prontos.Count(i => i.Natureza == NaturezaItemRepasse.Credito);
            var agora = DateTime.Now;

            var lancamento = new LancamentoFinanceiro
            {
                Data = fim,
                Tipo = TipoLancamento.Saida,
                Descricao = Cortar($"Repasse {profissional.Nome} — competência {inicio:MM/yyyy}", 200)!,
                Valor = total,
                Status = StatusLancamento.Previsto,
                CategoriaFinanceiraId = categoriaId,
                Contraparte = Cortar(profissional.Nome, 200),
                Observacoes = $"{quantidade} recebimento(s) conferido(s) por {operador.Trim()}",
                CriadoEm = agora,
                CriadoPor = operador.Trim()
            };
            await _repo.AdicionarLancamentoAsync(lancamento, ct);

            var fechamento = new RepasseFechamento
            {
                ProfissionalId = profissionalId,
                Competencia = inicio,
                RecebidosAte = fim,
                ProfissionalNome = Cortar(profissional.Nome, 200)!,
                ProfissionalRegistro = Cortar(profissional.RegistroConselho, 80),
                Quantidade = quantidade,
                Valor = total,
                ConferidoPor = Cortar(operador.Trim(), 80)!,
                ConferidoEm = agora,
                Observacoes = obs,
                Lancamento = lancamento,
                Itens = prontos.Select(ParaEntidade).ToList()
            };
            await _repo.AdicionarFechamentoRepasseAsync(fechamento, ct);

            await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
            {
                Operador = operador.Trim(),
                Acao = "RepasseFechado",
                Detalhe = $"{profissional.Nome} — competência {inicio:MM/yyyy} — "
                          + $"{quantidade} recebimento(s), {Dinheiro(total)}"
            }, ct);

            try
            {
                await _repo.SalvarAsync(ct);
            }
            catch (InvalidOperationException ex) when (EhTravaDoRepasse(ex))
            {
                throw new InvalidOperationException(MensagemConcorrencia, ex);
            }

            return fechamento;
        }, ct);
    }

    /// <summary>Fechamentos (com itens e lançamento), do mais novo ao mais antigo.</summary>
    public Task<IReadOnlyList<RepasseFechamento>> FechamentosAsync(
        DateOnly? competencia = null, int? profissionalId = null, CancellationToken ct = default)
        => _repo.FechamentosRepasseAsync(competencia is { } c ? Mes(c).Inicio : null, profissionalId, ct);

    public Task<RepasseFechamento?> ObterAsync(int fechamentoId, CancellationToken ct = default)
        => _repo.ObterFechamentoRepasseAsync(fechamentoId, ct);

    // ==================== Pagar e cancelar ====================

    /// <summary>Realiza a saída do caixa do fechamento (o médico foi pago).</summary>
    public Task RegistrarPagamentoAsync(
        int fechamentoId, DateOnly dataPagamento, FormaPagamento forma, string operador,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(operador))
            throw new InvalidOperationException("Entre com o seu usuário para registrar o pagamento.");

        return _repo.ExecutarGestaoAtomicaAsync(async () =>
        {
            var fechamento = await _repo.ObterFechamentoRepasseAsync(fechamentoId, ct)
                ?? throw new InvalidOperationException("Fechamento de repasse não encontrado.");
            if (fechamento.CanceladoEm is not null)
                throw new InvalidOperationException("Este fechamento foi cancelado — não há o que pagar.");
            if (fechamento.LancamentoFinanceiroId is not { } lancamentoId || fechamento.Lancamento is null)
                throw new InvalidOperationException("Este fechamento não tem saída no caixa.");
            if (fechamento.Lancamento.Status == StatusLancamento.Realizado)
                throw new InvalidOperationException(
                    $"Este repasse já foi pago em {fechamento.Lancamento.DataPagamento:dd/MM/yyyy}.");
            if (fechamento.Lancamento.Status == StatusLancamento.Cancelado)
                throw new InvalidOperationException(
                    "A saída deste repasse foi cancelada no Caixa. Cancele o fechamento e feche de novo.");

            // Auditoria do repasse antes: o RealizarAsync grava as duas no MESMO SaveChanges
            // (ele participa desta transação e salva o que já está rastreado).
            await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
            {
                Operador = operador.Trim(),
                Acao = "RepassePago",
                Detalhe = $"{fechamento.ProfissionalNome} — competência {fechamento.Competencia:MM/yyyy} — "
                          + $"{Dinheiro(fechamento.Valor)} em {dataPagamento:dd/MM/yyyy}"
            }, ct);

            await new FinanceiroService(_repo).RealizarAsync(
                lancamentoId, dataPagamento, forma, operador.Trim(), ct);
            return true;
        }, ct);
    }

    /// <summary>
    /// Cancela com motivo obrigatório: o fechamento fica (marcado), os itens voltam a ser
    /// elegíveis e a saída prevista é cancelada junto. Fechamento PAGO é recusado.
    /// </summary>
    public Task CancelarAsync(
        int fechamentoId, string motivo, string operador, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Diga por que o fechamento está sendo cancelado.");
        if (motivo.Trim().Length > 500)
            throw new InvalidOperationException("O motivo passa de 500 caracteres.");
        if (string.IsNullOrWhiteSpace(operador))
            throw new InvalidOperationException("Entre com o seu usuário para cancelar o fechamento.");

        var texto = motivo.Trim();
        return _repo.ExecutarGestaoAtomicaAsync(async () =>
        {
            var fechamento = await _repo.ObterFechamentoRepasseAsync(fechamentoId, ct)
                ?? throw new InvalidOperationException("Fechamento de repasse não encontrado.");
            if (fechamento.CanceladoEm is not null)
                throw new InvalidOperationException("Este fechamento já foi cancelado.");
            if (fechamento.Lancamento?.Status == StatusLancamento.Realizado)
                throw new InvalidOperationException(
                    "Este repasse já foi pago. Estorne o pagamento no Caixa antes de cancelar o fechamento.");

            var agora = DateTime.Now;
            fechamento.CanceladoEm = agora;
            fechamento.CanceladoPor = Cortar(operador.Trim(), 80);
            fechamento.MotivoCancelamento = texto;
            // A cópia no item é o que solta o índice único: sem ela os itens ficariam presos
            // a um fechamento que não existe mais e nunca voltariam à prévia.
            foreach (var item in fechamento.Itens)
                item.CanceladoEm = agora;

            if (fechamento.Lancamento is { } lancamento && lancamento.Status != StatusLancamento.Cancelado)
            {
                lancamento.Status = StatusLancamento.Cancelado;
                var obs = string.IsNullOrWhiteSpace(lancamento.Observacoes)
                    ? $"Cancelado: {texto}"
                    : $"{lancamento.Observacoes} | Cancelado: {texto}";
                if (obs.Length <= 500) lancamento.Observacoes = obs;
            }

            await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
            {
                Operador = operador.Trim(),
                Acao = "RepasseFechamentoCancelado",
                Detalhe = Cortar($"{fechamento.ProfissionalNome} — competência {fechamento.Competencia:MM/yyyy} — "
                                 + $"{Dinheiro(fechamento.Valor)} — {texto}", 1000)
            }, ct);

            await _repo.SalvarAsync(ct);
            return true;
        }, ct);
    }

    // ==================== Piso dos recebimentos ====================

    /// <summary>A data a partir da qual recebimentos entram no repasse por guia (nulo = sem piso).</summary>
    public async Task<DateOnly?> RecebidosDesdeAsync(CancellationToken ct = default)
        => DateOnly.TryParseExact(await _repo.ObterConfiguracaoAsync(ChaveRecebidosDesde, ct),
            "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;

    /// <summary>
    /// Define o marco da virada (ver <see cref="ChaveRecebidosDesde"/>). Nulo tira o piso.
    /// Auditado: mexer aqui decide o que é pago ao médico.
    /// </summary>
    public async Task DefinirRecebidosDesdeAsync(DateOnly? desde, string operador, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(operador))
            throw new InvalidOperationException("Entre com o seu usuário para mudar esta configuração.");
        await _repo.SalvarConfiguracaoAsync(ChaveRecebidosDesde,
            desde?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = operador.Trim(),
            Acao = "RepasseRecebidosDesde",
            Detalhe = desde is { } d ? $"Repasse por guia conta recebimentos a partir de {d:dd/MM/yyyy}" : "Sem data inicial"
        }, ct);
        await _repo.SalvarAsync(ct);
    }

    // ==================== Demonstrativo ====================

    /// <summary>Monta o demonstrativo de um fechamento gravado.</summary>
    public async Task<DemonstrativoRepasse> DemonstrativoAsync(int fechamentoId, CancellationToken ct = default)
    {
        var fechamento = await _repo.ObterFechamentoRepasseAsync(fechamentoId, ct)
            ?? throw new InvalidOperationException("Fechamento de repasse não encontrado.");
        var prestador = await new ParametrosService(_repo).ObterPrestadorAsync(ct);
        var clinica = PrimeiroPreenchido(prestador.NomeFantasia, prestador.RazaoSocial) ?? "Clínica";
        return Montar(fechamento, clinica);
    }

    /// <summary>A conta do demonstrativo, pura (testável sem banco).</summary>
    public static DemonstrativoRepasse Montar(RepasseFechamento f, string clinica)
    {
        var creditos = f.Itens.Where(i => i.Natureza == NaturezaItemRepasse.Credito).ToList();
        var estornos = f.Itens.Where(i => i.Natureza == NaturezaItemRepasse.Estorno)
            .OrderBy(i => i.PacienteNome, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(i => i.DataAtendimento)
            .ToList();

        var resumo = creditos
            .GroupBy(i => i.ConvenioNome)
            .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(g =>
            {
                var valores = g.Select(i => i.Valor).Distinct().ToList();
                return new ResumoConvenioNoDemonstrativo(
                    g.Key, g.Count(), valores.Count == 1 ? valores[0] : null, g.Sum(i => i.Valor));
            })
            .ToList();

        var regras = creditos
            .Select(i => $"{i.ConvenioNome}: {i.RegraDescricao}")
            .Distinct()
            .OrderBy(t => t, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var blocos = creditos
            .GroupBy(i => (i.ConvenioNome, Referencia: i.ReferenciaPagamento ?? string.Empty))
            .OrderBy(g => g.Key.ConvenioNome, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(g => g.Key.Referencia, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new BlocoDoDemonstrativo(
                g.Key.ConvenioNome,
                g.Key.Referencia.Length == 0 ? null : g.Key.Referencia,
                g.OrderBy(i => i.PacienteNome, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(i => i.DataAtendimento).ThenBy(i => i.Id).ToList(),
                g.Sum(i => i.Valor)))
            .ToList();

        var situacao = f.CanceladoEm is not null
            ? "Cancelado"
            : f.Lancamento?.Status == StatusLancamento.Realizado
                ? $"Pago em {(f.Lancamento.DataPagamento ?? f.Lancamento.Data):dd/MM/yyyy}"
                : "Conferido";

        return new DemonstrativoRepasse(
            f.Id, clinica, f.ProfissionalNome, f.ProfissionalRegistro, f.Competencia, f.RecebidosAte,
            f.ConferidoEm, f.ConferidoPor, situacao, resumo, regras, blocos, estornos,
            f.Quantidade, f.Valor, f.Observacoes);
    }

    // ==================== O coração: o que é elegível ====================

    /// <summary>
    /// Todos os itens ainda não fechados com recebimento até <paramref name="ate"/>, de todos
    /// os profissionais (o "sem profissional" com <c>ProfissionalId</c> 0), mais os estornos
    /// pendentes. Prontos e bloqueados juntos: quem filtra é quem chama.
    /// </summary>
    private async Task<List<ItemRepasseElegivel>> ItensElegiveisAsync(DateOnly ate, CancellationToken ct)
    {
        var desde = await RecebidosDesdeAsync(ct);
        var guias = await _repo.ReceitasDeGuiaParaRepasseAsync(desde, ate, ct);
        var particulares = await _repo.ReceitasParticularesParaRepasseAsync(desde, ate, ct);
        var semReceita = await _repo.CreditosRepasseSemReceitaAsync(ct);

        var atendimentos = guias.Select(g => g.AtendimentoId)
            .Concat(particulares.Select(p => p.AtendimentoId))
            .Distinct().ToList();
        var horarios = (await _repo.HorariosDosAtendimentosAsync(atendimentos, ct))
            .ToLookup(h => h.AtendimentoId);
        var regras = await _repo.RegrasRepasseAsync(null, ct);
        var apurados = (await _repo.RepassesApuradosAsync(null, ct))
            .Where(a => a.CanceladoEm == null).ToList();

        var itens = new List<ItemRepasseElegivel>();

        // Convênio: um item por GUIA (a receita pode ter vindo em mais de um lançamento).
        foreach (var g in guias.GroupBy(r => r.CodigoFaturamentoId!.Value))
        {
            var r = g.OrderBy(x => x.LancamentoId).Last();
            var codigo = PrimeiroPreenchido(r.ConvenioCodigoLancamento, r.ConvenioCodigoFicha)
                         ?? r.ConvenioFicha.ToString();
            var referencia = PrimeiroPreenchido(g.OrderByDescending(x => x.LancamentoId)
                                 .Select(x => x.DocumentoReferencia).ToArray())
                             ?? (r.NumeroLote is { } lote ? $"Lote {lote}" : null);
            itens.Add(Elegivel(
                OrigemItemRepasse.Convenio, $"G:{g.Key}", r, g.Sum(x => x.Valor), g.Max(x => x.DataRecebimento),
                codigo, CatalogoConvenios.NomeOuCodigo(codigo), [codigo],
                r.TipoGuia, RotulosEnum.De(r.TipoGuia!.Value), r.NumeroGuia, referencia,
                horarios[r.AtendimentoId], regras, apurados));
        }

        // Particular: um item por ATENDIMENTO, somando o que entrou por ele.
        foreach (var g in particulares.GroupBy(r => r.AtendimentoId))
        {
            var r = g.OrderBy(x => x.LancamentoId).Last();
            var ficha = r.ConvenioCodigoFicha ?? r.ConvenioFicha.ToString();
            // A regra do particular pode ter sido cadastrada como "PARTICULAR" ou com o código
            // do convênio-particular do catálogo (o de quem tem o particular na ficha).
            var codigos = CatalogoConvenios.GeraGuia(ficha) ? new[] { CodigoParticular } : new[] { CodigoParticular, ficha };
            var referencia = PrimeiroPreenchido(g.OrderByDescending(x => x.LancamentoId)
                .Select(x => x.DocumentoReferencia).ToArray());
            itens.Add(Elegivel(
                OrigemItemRepasse.Particular, $"P:{g.Key}", r, g.Sum(x => x.Valor), g.Max(x => x.DataRecebimento),
                CodigoParticular, NomeParticular, codigos,
                null, null, null, referencia,
                horarios[r.AtendimentoId], regras, apurados));
        }

        // Estornos: o crédito fechado cuja receita sumiu volta NEGATIVO, ao mesmo profissional.
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        foreach (var c in semReceita)
        {
            itens.Add(new ItemRepasseElegivel(
                Cortar($"E:{c.Chave}", 40)!, c.Origem, NaturezaItemRepasse.Estorno,
                c.Fechamento?.ProfissionalId ?? 0, c.AtendimentoId, c.CodigoFaturamentoId,
                c.LancamentoReceitaId, c.Id, c.PacienteId, c.PacienteNome, c.DataAtendimento,
                c.ConvenioCodigo, c.ConvenioNome, c.TipoGuia, c.Descricao, c.NumeroGuia,
                c.ReferenciaPagamento, hoje < ate ? hoje : ate, -c.ValorRecebido,
                c.RegraRepasseId, c.RegraDescricao, -c.Valor, BloqueioItemRepasse.Nenhum));
        }

        return itens;
    }

    private static ItemRepasseElegivel Elegivel(
        OrigemItemRepasse origem, string chave, ReceitaParaRepasse r, decimal recebido, DateOnly dataRecebimento,
        string convenioCodigo, string convenioNome, IReadOnlyList<string> codigosDaRegra,
        TipoCodigo? tipo, string? descricao, string? numeroGuia, string? referencia,
        IEnumerable<HorarioDoAtendimento> horarios, IReadOnlyList<RegraRepasse> regras,
        IReadOnlyList<RepasseApurado> apurados)
    {
        var horario = QuemAtendeu(horarios);
        var profissionalId = horario?.ProfissionalId ?? 0;
        var modalidade = PrimeiroPreenchido(horario?.ModalidadeCodigo, r.ModalidadeCodigo) ?? r.Modalidade.ToString();

        var regra = profissionalId == 0
            ? null
            : EscolherRegra(regras, profissionalId, r.DataAtendimento, codigosDaRegra, tipo, modalidade);

        var valor = regra is null ? 0m : ValorDoItem(regra, recebido);

        var bloqueio = profissionalId == 0
            ? BloqueioItemRepasse.SemProfissional
            : apurados.Any(a => a.ProfissionalId == profissionalId
                                && a.Inicio <= r.DataAtendimento && r.DataAtendimento <= a.Fim)
                ? BloqueioItemRepasse.ApuradoNoModeloAntigo
                : regra is null
                    ? BloqueioItemRepasse.SemRegra
                    : BloqueioItemRepasse.Nenhum;

        return new ItemRepasseElegivel(
            chave, origem, NaturezaItemRepasse.Credito, profissionalId, r.AtendimentoId,
            r.CodigoFaturamentoId, r.LancamentoId, null, r.PacienteId, r.Paciente, r.DataAtendimento,
            convenioCodigo, convenioNome, tipo,
            descricao ?? CatalogoModalidades.Nome(r.ModalidadeCodigo, r.Modalidade),
            numeroGuia, referencia, dataRecebimento, recebido,
            regra?.Id, regra is null ? "Sem regra de repasse" : regra.Descricao,
            valor, bloqueio);
    }

    /// <summary>
    /// O horário que diz quem atendeu: o mais novo com profissional entre os que apontam pelo
    /// <c>AtendimentoId</c>; sem nenhum, o mais novo substituído com profissional; sem nenhum
    /// com profissional, o mais novo de todos (para a modalidade).
    /// </summary>
    private static HorarioDoAtendimento? QuemAtendeu(IEnumerable<HorarioDoAtendimento> horarios)
    {
        var lista = horarios.OrderBy(h => h.Substituido).ThenByDescending(h => h.AgendamentoId).ToList();
        return lista.FirstOrDefault(h => h.ProfissionalId is not null) ?? lista.FirstOrDefault();
    }

    /// <summary>
    /// A regra do profissional vigente no dia do ATENDIMENTO que alcança o item. Ganha a mais
    /// específica (mais filtros preenchidos), depois a vigência mais recente, depois o maior Id.
    /// </summary>
    public static RegraRepasse? EscolherRegra(
        IEnumerable<RegraRepasse> regras, int profissionalId, DateOnly diaDoAtendimento,
        IReadOnlyList<string> convenios, TipoCodigo? tipo, string? modalidade)
        => regras
            .Where(r => r.ProfissionalId == profissionalId && r.VigenteEm(diaDoAtendimento)
                        && (string.IsNullOrWhiteSpace(r.ConvenioCodigo)
                            || convenios.Any(c => string.Equals(c, r.ConvenioCodigo.Trim(), StringComparison.OrdinalIgnoreCase)))
                        && (r.TipoGuia is null || r.TipoGuia == tipo)
                        && (string.IsNullOrWhiteSpace(r.ModalidadeCodigo)
                            || string.Equals(r.ModalidadeCodigo.Trim(), modalidade, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(r => (string.IsNullOrWhiteSpace(r.ConvenioCodigo) ? 0 : 1)
                                    + (r.TipoGuia is null ? 0 : 1)
                                    + (string.IsNullOrWhiteSpace(r.ModalidadeCodigo) ? 0 : 1))
            .ThenByDescending(r => r.VigenteDe ?? DateOnly.MinValue)
            .ThenByDescending(r => r.Id)
            .FirstOrDefault();

    /// <summary>Valor fixo por item, ou o percentual do que entrou, arredondado a centavos.</summary>
    public static decimal ValorDoItem(RegraRepasse regra, decimal recebido)
        => regra.Base == BaseRepasse.ValorPorAtendimento
            ? Math.Round(regra.ValorPorAtendimento ?? 0m, 2, MidpointRounding.AwayFromZero)
            : Math.Round(recebido * (regra.Percentual ?? 0m) / 100m, 2, MidpointRounding.AwayFromZero);

    private async Task<List<(int ProfissionalId, GuiaAguardandoConvenio Guia)>> AguardandoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct)
    {
        var guias = await _repo.GuiasBaixadasSemRecebimentoAsync(inicio.AddMonths(-(MesesAguardando - 1)), fim, ct);
        if (guias.Count == 0) return [];
        var horarios = (await _repo.HorariosDosAtendimentosAsync(
            guias.Select(g => g.AtendimentoId).Distinct().ToList(), ct)).ToLookup(h => h.AtendimentoId);

        return guias.Select(g => (
                QuemAtendeu(horarios[g.AtendimentoId])?.ProfissionalId ?? 0,
                new GuiaAguardandoConvenio(
                    g.CodigoFaturamentoId, g.AtendimentoId, g.Paciente, g.DataAtendimento, g.DataBaixa,
                    CatalogoConvenios.NomeOuCodigo(PrimeiroPreenchido(g.ConvenioCodigoFicha) ?? g.ConvenioFicha.ToString()),
                    RotulosEnum.De(g.Tipo), g.NumeroGuia, g.ReceitaPrevista,
                    g.Glosa is StatusGlosa.Glosada or StatusGlosa.Reapresentada)))
            .ToList();
    }

    // ==================== Utilidades ====================

    private static ItemRepasse ParaEntidade(ItemRepasseElegivel i) => new()
    {
        Origem = i.Origem,
        Natureza = i.Natureza,
        Chave = i.Chave,
        AtendimentoId = i.AtendimentoId,
        CodigoFaturamentoId = i.CodigoFaturamentoId,
        LancamentoReceitaId = i.LancamentoReceitaId,
        ItemEstornadoId = i.ItemEstornadoId,
        PacienteId = i.PacienteId,
        PacienteNome = Cortar(i.Paciente, 200)!,
        DataAtendimento = i.DataAtendimento,
        ConvenioCodigo = Cortar(i.ConvenioCodigo, 40)!,
        ConvenioNome = Cortar(i.ConvenioNome, 120)!,
        TipoGuia = i.TipoGuia,
        Descricao = Cortar(i.Descricao, 120)!,
        NumeroGuia = Cortar(i.NumeroGuia, 60),
        ReferenciaPagamento = Cortar(i.ReferenciaPagamento, 120),
        DataRecebimento = i.DataRecebimento,
        ValorRecebido = i.ValorRecebido,
        RegraRepasseId = i.RegraRepasseId,
        RegraDescricao = Cortar(i.RegraDescricao, 200)!,
        Valor = i.Valor
    };

    /// <summary>A recusa veio do índice único de <c>ItensRepasse</c> (Postgres ou SQLite)?</summary>
    private static bool EhTravaDoRepasse(InvalidOperationException ex)
        => ex.Message == MensagemConcorrencia
           || (ex.InnerException?.GetBaseException().Message.Contains("ItensRepasse", StringComparison.Ordinal) ?? false);

    /// <summary>Primeiro e último dia do mês da competência (aceita qualquer dia do mês).</summary>
    public static (DateOnly Inicio, DateOnly Fim) Mes(DateOnly competencia)
    {
        var inicio = new DateOnly(competencia.Year, competencia.Month, 1);
        return (inicio, inicio.AddMonths(1).AddDays(-1));
    }

    private static string Dinheiro(decimal valor) => valor.ToString("C", PtBr);

    private static string? PrimeiroPreenchido(params string?[] valores)
        => valores.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static string? Cortar(string? texto, int maximo)
        => texto is null ? null : texto.Length <= maximo ? texto : texto[..maximo];
}
