using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// O que a conferência clínica achou quando a folha foi para a assinatura.
///
/// Vem junto do resultado em vez de ser consultado à parte porque a assinatura é o último
/// momento em que alguém ainda pode desistir — depois dela a folha vai para a sala.
/// </summary>
public sealed record ResultadoAssinaturaPrescricao(
    PrescricaoInterna Prescricao,
    ConferenciaPrescricao Conferencia);

/// <summary>
/// A PRESCRIÇÃO DE EXECUÇÃO INTERNA — escrever, assinar e corrigir (parcela 42).
///
/// A folha de infusão da clínica: vários itens, destinada ao próprio consultório, e
/// executada e checada pela enfermagem na sala. A execução mora no
/// <see cref="ChecagemPrescricaoService"/>; aqui fica o lado de quem prescreve.
///
/// As regras, e a razão de cada uma
/// --------------------------------
/// - <b>Só rascunho se edita.</b> Depois de assinada a folha é um documento — e um
///   documento que muda depois de assinado não é documento. Corrigir uma prescrição em
///   execução se faz SUSPENDENDO o item e prescrevendo outro, que é como se corrige no
///   papel e deixa rastro dos dois.
/// - <b>Assinar exige itens.</b> Folha assinada em branco é assinatura em cheque em
///   branco: alguém acrescenta a linha depois.
/// - <b>Assinar exige o profissional.</b> Terceira exceção do projeto à regra de "avisa,
///   mas não impede" — as outras duas são receita e atestado, pelo mesmo motivo: sem
///   quem assina, o papel não vale nada e imprimi-lo só ensina a equipe a usar um
///   documento inválido.
/// - <b>A conferência de alergia da parcela 40 roda AQUI</b>, na assinatura. Não é
///   detalhe: o exemplo que a própria clínica deu ao pedir a feature foi o paciente que
///   "apresentou reação alérgica e não quis fazer a dipirona" — e o sistema já sabia da
///   alergia, em outra tabela, sem ninguém consultar. Conferir na assinatura é conferir no
///   único instante em que a informação ainda muda a conduta.
/// - <b>Cancelar não apaga.</b> A folha pode ter sido impressa e estar na sala; a linha
///   fica, com motivo, como a NC do faturamento.
/// </summary>
public sealed class PrescricaoInternaService
{
    private readonly IClinicaRepositorio _repo;
    private readonly PrescricaoService _conferencia;

    /// <summary>Alfabeto do código de conferência, sem o que se confunde à mão (I, O, 0, 1).</summary>
    private const string AlfabetoCodigo = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public PrescricaoInternaService(IClinicaRepositorio repo, PrescricaoService conferencia)
    {
        _repo = repo;
        _conferencia = conferencia;
    }

    // ---- Leitura ----

    public Task<PrescricaoInterna?> ObterAsync(int prescricaoId, CancellationToken ct = default)
        => _repo.ObterPrescricaoInternaAsync(prescricaoId, ct);

    public Task<PrescricaoInterna?> PorCodigoAsync(string codigo, CancellationToken ct = default)
        => _repo.ObterPrescricaoInternaPorCodigoAsync(codigo, ct);

    public Task<IReadOnlyList<PrescricaoInterna>> DoPacienteAsync(
        int pacienteId, int limite = 50, CancellationToken ct = default)
        => _repo.PrescricoesInternasDoPacienteAsync(pacienteId, limite, ct);

    /// <summary>A fila da sala de infusão: as folhas assinadas do dia.</summary>
    public Task<IReadOnlyList<PrescricaoInterna>> DoDiaAsync(
        DateOnly data, int? profissionalId = null, bool incluirEncerradas = false,
        CancellationToken ct = default)
        => _repo.PrescricoesInternasDoDiaAsync(data, profissionalId, incluirEncerradas, ct);

    /// <summary>
    /// O contexto clínico que a tela de prescrição abre mostrando: alergias e medicação de
    /// uso contínuo. Vale com a folha ainda em branco — é o que se olha ANTES de escrever.
    /// </summary>
    public Task<ConferenciaPrescricao> ContextoAsync(int pacienteId, CancellationToken ct = default)
        => _conferencia.ContextoAsync(pacienteId, ct);

    /// <summary>Confere os itens já escritos contra a lista de problemas do paciente.</summary>
    public Task<ConferenciaPrescricao> ConferirAsync(
        PrescricaoInterna prescricao, CancellationToken ct = default)
        => _conferencia.ConferirAsync(
            prescricao.PacienteId,
            prescricao.Itens.Where(i => !i.Suspenso).Select(i => i.TextoCompleto),
            ct);

    // ---- Escrita ----

    /// <summary>
    /// Cria a folha em RASCUNHO. Ela já nasce numerada porque o número é a identidade da
    /// via em papel, e a clínica imprime rascunho para conferir antes de assinar.
    /// </summary>
    public async Task<PrescricaoInterna> CriarAsync(
        int pacienteId, int? profissionalId, int? agendamentoId = null,
        int? evolucaoId = null, string? operador = null, CancellationToken ct = default)
    {
        if (await _repo.ObterPacienteAsync(pacienteId, ct) is null)
            throw new InvalidOperationException("Paciente não encontrado.");

        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var sequencial = await _repo.ProximoNumeroPrescricaoInternaAsync(hoje.Year, ct);

        var prescricao = new PrescricaoInterna
        {
            Numero = $"PRE {hoje.Year}/{sequencial:0000}",
            CodigoVerificacao = GerarCodigo(),
            PacienteId = pacienteId,
            ProfissionalId = profissionalId,
            AgendamentoId = agendamentoId,
            EvolucaoId = evolucaoId,
            Data = hoje,
            Hora = TimeOnly.FromDateTime(DateTime.Now),
            Situacao = SituacaoPrescricao.Rascunho,
            CriadoPor = operador
        };

        await _repo.AdicionarPrescricaoInternaAsync(prescricao, ct);
        await _repo.SalvarAsync(ct);
        return prescricao;
    }

    /// <summary>
    /// Grava o cabeçalho e a lista de itens de um RASCUNHO.
    ///
    /// Recebe a lista inteira e a reconstrói em vez de aceitar alterações item a item: no
    /// rascunho ninguém depende dos identificadores, e reconstruir evita o bug clássico de
    /// formulário — a linha que o usuário removeu na tela continuar no banco porque
    /// ninguém mandou removê-la.
    /// </summary>
    public async Task<PrescricaoInterna> SalvarRascunhoAsync(
        int prescricaoId, string? indicacao, string? observacoes,
        IReadOnlyList<ItemPrescricaoInterna> itens,
        string? operador = null, CancellationToken ct = default)
    {
        var prescricao = await Exigir(prescricaoId, ct);

        if (!prescricao.PodeEditar)
            throw new InvalidOperationException(
                $"A prescrição {prescricao.Numero} já foi assinada e não se edita. Para "
                + "corrigir, suspenda o item e prescreva outro — é o que deixa rastro dos dois.");

        prescricao.Indicacao = Limpar(indicacao);
        prescricao.Observacoes = Limpar(observacoes);
        prescricao.AtualizadoEm = DateTime.Now;
        prescricao.AtualizadoPor = operador;

        prescricao.Itens.Clear();

        var ordem = 1;
        foreach (var entrada in itens)
        {
            if (string.IsNullOrWhiteSpace(entrada.Descricao)) continue;

            prescricao.Itens.Add(new ItemPrescricaoInterna
            {
                Ordem = ordem++,
                Descricao = entrada.Descricao.Trim(),
                Dose = Limpar(entrada.Dose),
                Diluente = Limpar(entrada.Diluente),
                Volume = Limpar(entrada.Volume),
                Via = entrada.Via,
                TempoInfusao = Limpar(entrada.TempoInfusao),
                HoraPrevista = entrada.HoraPrevista,
                SeNecessario = entrada.SeNecessario,
                Observacoes = Limpar(entrada.Observacoes)
            });
        }

        await _repo.SalvarAsync(ct);
        return prescricao;
    }

    /// <summary>
    /// Confere a folha e devolve o que ela tem de alergia — SEM assinar.
    ///
    /// Existe separado porque a tela precisa mostrar o achado e cobrar uma confirmação
    /// escrita antes de chamar <see cref="AssinarAsync"/>. Juntar os dois passos faria a
    /// confirmação acontecer depois do fato, que é o mesmo que não acontecer.
    /// </summary>
    public async Task<ConferenciaPrescricao> ConferirParaAssinaturaAsync(
        int prescricaoId, CancellationToken ct = default)
    {
        var prescricao = await Exigir(prescricaoId, ct);
        return await ConferirAsync(prescricao, ct);
    }

    /// <summary>
    /// Assina a folha: ela sai do rascunho e aparece na sala de infusão.
    ///
    /// Este método grava o FATO da assinatura. O PDF assinado e os dados do certificado
    /// entram pelo <paramref name="assinatura"/>, montado por quem sabe assinar
    /// (<c>AssinaturaDigitalService</c>) — este serviço não conhece criptografia, e
    /// misturar as duas coisas faria o teste de regra de negócio precisar de um
    /// certificado.
    /// </summary>
    /// <param name="confirmouAlergia">
    /// Quem assina viu o alerta de alergia e decidiu prosseguir. Sem isso a assinatura é
    /// RECUSADA quando há coincidência — é o segundo caso do projeto em que a tela cobra
    /// confirmação explícita, e o primeiro em que ela é obrigatória no serviço.
    /// </param>
    public async Task<ResultadoAssinaturaPrescricao> AssinarAsync(
        int prescricaoId, AssinaturaDocumento assinatura, bool confirmouAlergia = false,
        string? operador = null, CancellationToken ct = default)
    {
        var prescricao = await Exigir(prescricaoId, ct);

        if (prescricao.Cancelada)
            throw new InvalidOperationException($"A prescrição {prescricao.Numero} foi cancelada.");

        if (prescricao.EstaAssinada)
            throw new InvalidOperationException($"A prescrição {prescricao.Numero} já está assinada.");

        if (prescricao.Itens.Count == 0)
            throw new InvalidOperationException(
                "Não dá para assinar uma folha sem itens: assinada em branco, ela vira "
                + "espaço para alguém acrescentar uma linha depois.");

        if (prescricao.ProfissionalId is null)
            throw new InvalidOperationException(
                "A prescrição precisa do profissional que assina. Sem ele o papel não vale, "
                + "e emiti-lo assim só ensinaria a equipe a usar um documento inválido.");

        var conferencia = await ConferirAsync(prescricao, ct);

        if (conferencia.ExigeConfirmacao && !confirmouAlergia)
            throw new InvalidOperationException(
                "Há item prescrito que bate com ALERGIA registrada deste paciente. O sistema "
                + "não impede — quem decide é quem assina —, mas exige que a confirmação seja "
                + "explícita. Confira os alertas antes de continuar.");

        assinatura.PrescricaoInternaId = prescricao.Id;
        assinatura.Papel = PapelAssinatura.Prescritor;
        prescricao.Assinaturas.Add(assinatura);

        prescricao.Situacao = SituacaoPrescricao.Assinada;
        prescricao.AssinadaEm = assinatura.AssinadoEm;
        prescricao.AtualizadoEm = DateTime.Now;
        prescricao.AtualizadoPor = operador;

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = operador ?? "?",
            Acao = "PrescricaoAssinada",
            PacienteId = prescricao.PacienteId,
            Detalhe = $"{prescricao.Numero} · {prescricao.Itens.Count} itens · "
                    + $"{assinatura.RotuloDoNivel} · hash {assinatura.HashCurto}"
                    + (conferencia.ExigeConfirmacao ? " · assinada COM alerta de alergia confirmado" : "")
        }, ct);

        await _repo.SalvarAsync(ct);
        return new ResultadoAssinaturaPrescricao(prescricao, conferencia);
    }

    /// <summary>
    /// Tira um item da folha já assinada.
    ///
    /// É o caminho de correção depois da assinatura, e é diferente de "não realizado": um
    /// é decisão de quem prescreve (o item não deve mais ser feito), o outro é o que
    /// aconteceu na sala. Somá-los num estado só apagaria qual dos dois foi — que é
    /// justamente a pergunta de quem lê a folha depois.
    ///
    /// Item que já foi checado NÃO se suspende: o que foi feito, foi feito, e suspender
    /// depois faria a folha desdizer uma execução que já tem assinatura.
    /// </summary>
    public async Task SuspenderItemAsync(
        int itemId, string motivo, string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException(
                "Diga por que o item está sendo suspenso. Sem o motivo, quem ler a folha "
                + "amanhã não sabe se houve contraindicação ou engano de digitação.");

        var item = await _repo.ObterItemPrescricaoInternaAsync(itemId, ct)
            ?? throw new InvalidOperationException("Item não encontrado.");

        if (item.Suspenso)
            throw new InvalidOperationException("O item já está suspenso.");

        if (item.ChecagemVigente is not null)
            throw new InvalidOperationException(
                "Este item já foi checado pela enfermagem e não se suspende — a checagem é "
                + "o registro assinado do que aconteceu com o paciente. Se a checagem está "
                + "errada, quem executou deve retificá-la.");

        item.SuspensoEm = DateTime.Now;
        item.MotivoSuspensao = motivo.Trim();
        item.SuspensoPor = operador;

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = operador ?? "?",
            Acao = "PrescricaoItemSuspenso",
            PacienteId = item.Prescricao?.PacienteId,
            Detalhe = $"{item.Prescricao?.Numero} · item {item.Ordem} ({item.Descricao}) · {motivo.Trim()}"
        }, ct);

        await _repo.SalvarAsync(ct);
    }

    /// <summary>
    /// Cancela a folha inteira. Não apaga: ela pode ter sido impressa e estar na sala.
    ///
    /// Folha com item JÁ CHECADO não se cancela — cancelar apagaria o contexto de uma
    /// administração que aconteceu, e o registro da enfermagem ficaria pendurado numa
    /// prescrição que o sistema diz que nunca valeu.
    /// </summary>
    public async Task CancelarAsync(
        int prescricaoId, string motivo, string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Diga por que a prescrição está sendo cancelada.");

        var prescricao = await Exigir(prescricaoId, ct);

        if (prescricao.Cancelada)
            throw new InvalidOperationException("A prescrição já está cancelada.");

        if (prescricao.Situacao == SituacaoPrescricao.Encerrada)
            throw new InvalidOperationException(
                "A execução desta folha já foi encerrada e assinada pela enfermagem. Ela é "
                + "registro do que aconteceu com o paciente e não se cancela.");

        if (prescricao.Itens.Any(i => i.ChecagemVigente is not null))
            throw new InvalidOperationException(
                "Já há item checado nesta folha. Cancelá-la deixaria o registro da "
                + "enfermagem preso a uma prescrição que o sistema diz que nunca valeu — "
                + "suspenda os itens que ainda não foram executados.");

        prescricao.Situacao = SituacaoPrescricao.Cancelada;
        prescricao.CanceladaEm = DateTime.Now;
        prescricao.MotivoCancelamento = motivo.Trim();
        prescricao.AtualizadoEm = DateTime.Now;
        prescricao.AtualizadoPor = operador;

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = operador ?? "?",
            Acao = "PrescricaoCancelada",
            PacienteId = prescricao.PacienteId,
            Detalhe = $"{prescricao.Numero} · {motivo.Trim()}"
        }, ct);

        await _repo.SalvarAsync(ct);
    }

    // ---- Apoio ----

    private async Task<PrescricaoInterna> Exigir(int prescricaoId, CancellationToken ct)
        => await _repo.ObterPrescricaoInternaAsync(prescricaoId, ct)
           ?? throw new InvalidOperationException("Prescrição não encontrada.");

    private static string? Limpar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string GerarCodigo()
    {
        var sorteio = Random.Shared;
        return new string(Enumerable.Range(0, 8)
            .Select(_ => AlfabetoCodigo[sorteio.Next(AlfabetoCodigo.Length)])
            .ToArray());
    }
}
