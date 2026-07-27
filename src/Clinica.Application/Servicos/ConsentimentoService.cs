using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Consentimento LGPD do paciente, por finalidade.
///
/// O registro é um FATO datado, nunca um interruptor: conceder, negar e revogar criam
/// linhas novas em vez de sobrescrever a anterior. Consentimento sem histórico não prova
/// nada — e provar, com data e responsável, é exatamente o que a lei pede.
/// </summary>
public sealed class ConsentimentoService
{
    private readonly IClinicaRepositorio _repo;

    /// <summary>Finalidades oferecidas no balcão, na ordem em que fazem sentido perguntar.</summary>
    public static readonly IReadOnlyList<FinalidadeConsentimento> Finalidades =
    [
        FinalidadeConsentimento.TratamentoDeDados,
        FinalidadeConsentimento.CompartilhamentoComConvenio,
        FinalidadeConsentimento.UsoDeImagem,
        FinalidadeConsentimento.ComunicacaoEMarketing
    ];

    public ConsentimentoService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>Histórico completo do paciente, do mais recente ao mais antigo.</summary>
    public Task<IReadOnlyList<ConsentimentoLgpd>> HistoricoAsync(
        int pacienteId, CancellationToken ct = default)
        => _repo.ConsentimentosDoPacienteAsync(pacienteId, ct);

    /// <summary>
    /// Situação ATUAL por finalidade: o registro mais recente de cada uma. É o que a
    /// tela mostra — o histórico inteiro é para auditoria, não para decidir.
    /// </summary>
    public async Task<IReadOnlyDictionary<FinalidadeConsentimento, ConsentimentoLgpd>> SituacaoAsync(
        int pacienteId, CancellationToken ct = default)
    {
        var historico = await _repo.ConsentimentosDoPacienteAsync(pacienteId, ct);

        return historico
            .GroupBy(c => c.Finalidade)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(c => c.RegistradoEm).ThenByDescending(c => c.Id).First());
    }

    /// <summary>O paciente tem consentimento vigente para esta finalidade?</summary>
    public async Task<bool> VigenteAsync(
        int pacienteId, FinalidadeConsentimento finalidade, CancellationToken ct = default)
    {
        var situacao = await SituacaoAsync(pacienteId, ct);
        return situacao.TryGetValue(finalidade, out var atual) && atual.Vigente;
    }

    /// <summary>Registra a resposta do paciente (concedeu ou negou) para uma finalidade.</summary>
    public async Task<ConsentimentoLgpd> RegistrarAsync(
        int pacienteId, FinalidadeConsentimento finalidade, bool concedido,
        string? versaoTermo = null, string? operador = null, string? observacoes = null,
        CancellationToken ct = default)
    {
        if (await _repo.ObterPacienteAsync(pacienteId, ct) is null)
            throw new InvalidOperationException("Paciente não encontrado.");

        var registro = new ConsentimentoLgpd
        {
            PacienteId = pacienteId,
            Finalidade = finalidade,
            Concedido = concedido,
            VersaoTermo = versaoTermo,
            RegistradoEm = DateTime.Now,
            RegistradoPor = operador,
            Observacoes = observacoes
        };

        await _repo.AdicionarConsentimentoAsync(registro, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = concedido ? "ConsentimentoConcedido" : "ConsentimentoNegado",
            Detalhe = finalidade.ToString(),
            PacienteId = pacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        return registro;
    }

    /// <summary>
    /// Revoga um consentimento vigente. NÃO apaga o registro: a linha continua provando
    /// que houve consentimento durante o período em que os dados foram tratados.
    /// </summary>
    public async Task RevogarAsync(
        int consentimentoId, string? operador = null, string? motivo = null,
        CancellationToken ct = default)
    {
        var registro = await _repo.ObterConsentimentoAsync(consentimentoId, ct)
            ?? throw new InvalidOperationException("Consentimento não encontrado.");

        if (!registro.Concedido)
            throw new InvalidOperationException(
                "Este registro é uma recusa; não há o que revogar.");

        if (registro.RevogadoEm is not null)
            throw new InvalidOperationException("Este consentimento já foi revogado.");

        registro.RevogadoEm = DateTime.Now;
        if (!string.IsNullOrWhiteSpace(motivo))
            registro.Observacoes = string.IsNullOrWhiteSpace(registro.Observacoes)
                ? $"Revogado: {motivo}"
                : $"{registro.Observacoes} — revogado: {motivo}";

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "ConsentimentoRevogado",
            Detalhe = registro.Finalidade.ToString(),
            PacienteId = registro.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
    }

    /// <summary>Rótulo em português da finalidade (usado nas telas e nos documentos).</summary>
    public static string Rotular(FinalidadeConsentimento finalidade) => finalidade switch
    {
        FinalidadeConsentimento.TratamentoDeDados => "Tratamento de dados pessoais e de saúde",
        FinalidadeConsentimento.UsoDeImagem => "Uso de imagem (foto do cadastro e fotos clínicas)",
        FinalidadeConsentimento.ComunicacaoEMarketing => "Mensagens de confirmação, recall e campanhas",
        FinalidadeConsentimento.CompartilhamentoComConvenio => "Compartilhamento com o convênio para faturamento",
        _ => finalidade.ToString()
    };
}
