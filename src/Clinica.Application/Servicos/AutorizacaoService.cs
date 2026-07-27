using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Cota de sessões liberada pelo convênio. Existe para atacar a glosa
/// <c>2006 — quantidade executada acima da autorizada</c> ANTES do prejuízo: o sistema
/// já sabia registrar essa glosa depois que ela chegava, mas não avisava a recepção
/// quando a cota estava no fim.
///
/// O consumo é derivado: conta os atendimentos do paciente dentro da vigência da
/// autorização, mais um ajuste manual para sessões feitas fora do sistema. Assim nada
/// precisa mudar no lançamento do atendimento — e, se a clínica esquecer de cadastrar a
/// senha, o fluxo continua funcionando exatamente como antes.
/// </summary>
public sealed class AutorizacaoService
{
    private readonly IClinicaRepositorio _repo;

    public AutorizacaoService(IClinicaRepositorio repo) => _repo = repo;

    public Task<IReadOnlyList<AutorizacaoSessoes>> DoPacienteAsync(int pacienteId, CancellationToken ct = default)
        => _repo.AutorizacoesDoPacienteAsync(pacienteId, ct);

    /// <summary>Saldo de todas as autorizações do paciente, da mais recente para a mais antiga.</summary>
    public async Task<IReadOnlyList<SaldoAutorizacao>> SaldosAsync(int pacienteId, DateOnly referencia, CancellationToken ct = default)
    {
        var autorizacoes = await _repo.AutorizacoesDoPacienteAsync(pacienteId, ct);
        var saldos = new List<SaldoAutorizacao>(autorizacoes.Count);
        foreach (var a in autorizacoes)
            saldos.Add(await CalcularAsync(a, referencia, ct));
        return saldos;
    }

    /// <summary>
    /// Autorização que vale para um atendimento nesta data: a vigente com saldo. Não
    /// havendo com saldo, devolve a vigente mesmo esgotada (é o caso que a recepção
    /// precisa ver na tela). Null = paciente sem autorização vigente.
    /// </summary>
    public async Task<SaldoAutorizacao?> VigenteAsync(int pacienteId, DateOnly referencia, CancellationToken ct = default)
    {
        var vigentes = new List<SaldoAutorizacao>();
        foreach (var a in await _repo.AutorizacoesDoPacienteAsync(pacienteId, ct))
        {
            if (!a.Vigente(referencia)) continue;
            vigentes.Add(await CalcularAsync(a, referencia, ct));
        }

        return vigentes.FirstOrDefault(s => s.Utilizavel) ?? vigentes.FirstOrDefault();
    }

    private async Task<SaldoAutorizacao> CalcularAsync(AutorizacaoSessoes a, DateOnly referencia, CancellationToken ct)
    {
        // Atendimentos dentro da vigência + o que já vinha usado de antes do controle.
        var noPeriodo = await _repo.ContarAtendimentosDoPacienteAsync(a.PacienteId, a.DataEmissao, a.DataValidade, ct);
        var utilizadas = noPeriodo + a.QuantidadeUtilizadaManual;
        var restantes = a.QuantidadeAutorizada - utilizadas;
        return new SaldoAutorizacao(a, utilizadas, restantes, a.Vencida(referencia));
    }

    /// <param name="operador">
    /// Quem está gravando. A cota decide se uma guia pode ser emitida, então mudar de 10
    /// para 20 sessões precisa deixar rastro tanto quanto uma baixa.
    /// </param>
    public async Task<AutorizacaoSessoes> SalvarAsync(
        AutorizacaoSessoes autorizacao, string? operador = null, CancellationToken ct = default)
    {
        Validar(autorizacao);

        var novo = autorizacao.Id == 0;
        var anterior = novo ? null : await _repo.ObterAutorizacaoAsync(autorizacao.Id, ct);

        var quantidadeAnterior = anterior?.QuantidadeAutorizada;

        if (novo)
        {
            await _repo.AdicionarAutorizacaoAsync(autorizacao, ct);
        }
        else
        {
            var atual = anterior
                ?? throw new InvalidOperationException("Autorização não encontrada.");

            atual.Numero = autorizacao.Numero;
            atual.Convenio = autorizacao.Convenio;
            atual.ConvenioCodigo = autorizacao.ConvenioCodigo;
            atual.DataEmissao = autorizacao.DataEmissao;
            atual.DataValidade = autorizacao.DataValidade;
            atual.QuantidadeAutorizada = autorizacao.QuantidadeAutorizada;
            atual.QuantidadeUtilizadaManual = autorizacao.QuantidadeUtilizadaManual;
            atual.Observacoes = autorizacao.Observacoes;
            atual.Encerrada = autorizacao.Encerrada;
        }

        var mudouCota = !novo && quantidadeAnterior != autorizacao.QuantidadeAutorizada;
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = novo ? "AutorizacaoCriada" : "AutorizacaoAlterada",
            Detalhe = $"Senha {autorizacao.Numero ?? "(sem número)"}: {autorizacao.QuantidadeAutorizada} sessão(ões)"
                      + (mudouCota ? $" (antes {quantidadeAnterior})" : string.Empty)
                      + $", válida até {autorizacao.DataValidade:dd/MM/yyyy}"
                      + (autorizacao.Encerrada ? " — encerrada" : string.Empty),
            PacienteId = autorizacao.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        return autorizacao;
    }

    public async Task RemoverAsync(int autorizacaoId, string? operador = null, CancellationToken ct = default)
    {
        var autorizacao = await _repo.ObterAutorizacaoAsync(autorizacaoId, ct);
        await _repo.RemoverAutorizacaoAsync(autorizacaoId, ct);

        if (autorizacao is not null)
            await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
            {
                Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
                Acao = "AutorizacaoRemovida",
                Detalhe = $"Senha {autorizacao.Numero ?? "(sem número)"}, {autorizacao.QuantidadeAutorizada} sessão(ões)",
                PacienteId = autorizacao.PacienteId
            }, ct);

        await _repo.SalvarAsync(ct);
    }

    private static void Validar(AutorizacaoSessoes a)
    {
        if (a.QuantidadeAutorizada <= 0)
            throw new ArgumentException("Informe quantas sessões o convênio autorizou.");
        if (a.QuantidadeUtilizadaManual < 0)
            throw new ArgumentException("Sessões já utilizadas não pode ser negativo.");
        if (a.DataValidade < a.DataEmissao)
            throw new ArgumentException("A validade não pode ser anterior à emissão.");
    }
}
