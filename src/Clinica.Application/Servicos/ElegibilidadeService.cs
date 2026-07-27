using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Responde "este paciente pode ser atendido hoje?" — no BALCÃO, antes da sessão.
///
/// Hoje carteirinha vencida e cota estourada só aparecem na hora de faturar, quando o
/// serviço já foi prestado e o prejuízo já aconteceu. Este serviço junta num lugar só o
/// que já existia espalhado (<see cref="Paciente.CarteirinhaVencida"/>,
/// <see cref="AutorizacaoService"/>, <see cref="ConsentimentoService"/>) e entrega para a
/// recepção enquanto ainda dá para resolver.
///
/// Ele NUNCA impede o atendimento: quem decide é a clínica. Ele informa.
/// </summary>
public sealed class ElegibilidadeService
{
    private readonly IClinicaRepositorio _repo;
    private readonly AutorizacaoService _autorizacoes;
    private readonly ConsentimentoService _consentimentos;

    /// <summary>Dias de antecedência para começar a avisar da carteirinha.</summary>
    public const int JanelaAvisoCarteirinhaDias = 30;

    public ElegibilidadeService(
        IClinicaRepositorio repo,
        AutorizacaoService autorizacoes,
        ConsentimentoService consentimentos)
    {
        _repo = repo;
        _autorizacoes = autorizacoes;
        _consentimentos = consentimentos;
    }

    public async Task<Elegibilidade> ConferirAsync(
        int pacienteId, DateOnly referencia, CancellationToken ct = default)
    {
        var paciente = await _repo.ObterPacienteAsync(pacienteId, ct)
            ?? throw new InvalidOperationException("Paciente não encontrado.");

        var alertas = new List<AlertaElegibilidade>();

        ConferirCarteirinha(paciente, referencia, alertas);
        await ConferirCotaAsync(pacienteId, referencia, alertas, ct);
        await ConferirConsentimentoAsync(pacienteId, alertas, ct);

        return new Elegibilidade(pacienteId, paciente.Nome, alertas);
    }

    private static void ConferirCarteirinha(
        Paciente paciente, DateOnly referencia, List<AlertaElegibilidade> alertas)
    {
        if (paciente.ValidadeCarteirinha is not { } validade) return;

        if (validade < referencia)
        {
            alertas.Add(new AlertaElegibilidade(
                ImpedimentoElegibilidade.CarteirinhaVencida,
                NivelUrgencia.Vermelho,
                $"Carteirinha vencida em {validade:dd/MM/yyyy} — o convênio recusa a guia."));
            return;
        }

        var dias = validade.DayNumber - referencia.DayNumber;
        if (dias <= JanelaAvisoCarteirinhaDias)
            alertas.Add(new AlertaElegibilidade(
                ImpedimentoElegibilidade.CarteirinhaAVencer,
                NivelUrgencia.Amarelo,
                $"Carteirinha vence em {dias} dia(s) ({validade:dd/MM/yyyy})."));
    }

    private async Task ConferirCotaAsync(
        int pacienteId, DateOnly referencia,
        List<AlertaElegibilidade> alertas, CancellationToken ct)
    {
        var vigente = await _autorizacoes.VigenteAsync(pacienteId, referencia, ct);

        if (vigente is null)
        {
            // Só avisa se ESTE paciente já teve senha registrada alguma vez — aí a
            // ausência de uma vigente é notícia (venceu, acabou). Numa clínica que não
            // controla senha, avisar sempre seria um alerta que dispara para todo mundo
            // — e alerta que sempre aparece é alerta que ninguém lê.
            var historico = await _autorizacoes.DoPacienteAsync(pacienteId, ct);
            if (historico.Count == 0) return;

            alertas.Add(new AlertaElegibilidade(
                ImpedimentoElegibilidade.SemAutorizacaoVigente,
                NivelUrgencia.Amarelo,
                "Nenhuma autorização vigente — a anterior venceu ou foi encerrada."));
            return;
        }

        if (vigente.Esgotada)
        {
            alertas.Add(new AlertaElegibilidade(
                ImpedimentoElegibilidade.CotaEsgotada,
                NivelUrgencia.Vermelho,
                $"Cota esgotada ({vigente.Resumo}) — a próxima sessão vira glosa 2006."));
            return;
        }

        if (vigente.NaUltima)
            alertas.Add(new AlertaElegibilidade(
                ImpedimentoElegibilidade.CotaQuaseNoFim,
                NivelUrgencia.Amarelo,
                $"Última sessão autorizada ({vigente.Resumo}) — peça a renovação da senha."));
    }

    private async Task ConferirConsentimentoAsync(
        int pacienteId, List<AlertaElegibilidade> alertas, CancellationToken ct)
    {
        if (await _consentimentos.VigenteAsync(
                pacienteId, FinalidadeConsentimento.TratamentoDeDados, ct))
            return;

        alertas.Add(new AlertaElegibilidade(
            ImpedimentoElegibilidade.SemConsentimentoLgpd,
            NivelUrgencia.Amarelo,
            "Sem consentimento LGPD de tratamento de dados — colha no balcão."));
    }
}
