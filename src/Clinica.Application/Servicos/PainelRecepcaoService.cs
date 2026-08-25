using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// O dia visto do balcão. Alimenta o painel próprio da Recepção — que NÃO é o painel do
/// faturamento: lá a pergunta é "que guia está para vencer"; aqui é "como está o dia e
/// quem está esperando".
///
/// As pendências de guia aparecem também aqui, mas só como aviso na hora do check-in
/// (o paciente está na frente da secretária — é a única chance de cobrar o documento).
/// Quem decide sobre a guia continua sendo o faturamento.
/// </summary>
public sealed class PainelRecepcaoService
{
    private readonly IClinicaRepositorio _repo;
    private readonly AgendaService _agenda;
    private readonly PendenciaService _pendencias;

    public PainelRecepcaoService(
        IClinicaRepositorio repo, AgendaService agenda, PendenciaService pendencias)
    {
        _repo = repo;
        _agenda = agenda;
        _pendencias = pendencias;
    }

    /// <summary>Números do dia: fila, ocupação, faltas, encaixes e tempo médio de espera.</summary>
    public async Task<ResumoDiaRecepcao> ResumoAsync(
        DateOnly dia, DateTime? agora = null, CancellationToken ct = default)
    {
        var referencia = agora ?? DateTime.Now;
        var doDia = await _agenda.DoDiaAsync(dia, ct);
        var ocupacao = await _agenda.OcupacaoDoDiaAsync(dia, ct);
        var espera = await _repo.ListaEsperaAsync(somenteAguardando: true, ct);

        // Espera média de quem JÁ chegou: quem não fez check-in não tem espera para medir,
        // e incluí-lo como zero faria a média mentir para baixo.
        //
        // FALTA fica de fora como o cancelado (item 3 da fila da parcela 69): quem fez
        // check-in e depois virou falta nunca foi chamado, então a espera dele corre até
        // `agora` — num dia passado, milhares de minutos, e a média do painel explodia
        // por uma sessão que oficialmente não aconteceu.
        var esperas = doDia
            .Where(a => a.Status is not StatusAgendamento.Cancelado and not StatusAgendamento.Faltou)
            .Select(a => a.EsperaMinutos(referencia))
            .Where(m => m is not null)
            .Select(m => m!.Value)
            .ToList();

        return new ResumoDiaRecepcao(
            Dia: dia,
            Agendados: doDia.Count(a => a.OcupaAgenda),
            Aguardando: doDia.Count(a => a.Etapa == EtapaFila.Aguardando),
            NaRecepcao: doDia.Count(a => a.Etapa == EtapaFila.Chegou),
            EmAtendimento: doDia.Count(a => a.Etapa == EtapaFila.EmAtendimento),
            Atendidos: doDia.Count(a => a.Status == StatusAgendamento.Realizado),
            Faltas: doDia.Count(a => a.Status == StatusAgendamento.Faltou),
            Cancelados: doDia.Count(a => a.Status == StatusAgendamento.Cancelado),
            Encaixes: doDia.Count(a => a.Encaixe && a.OcupaAgenda),
            EsperaMediaMinutos: esperas.Count == 0 ? 0 : (int)Math.Round(esperas.Average()),
            NaListaDeEspera: espera.Count,
            Ocupacao: ocupacao);
    }

    /// <summary>
    /// Guias pendentes dos pacientes que vêm HOJE. É a lista que faz a recepção cobrar o
    /// documento com o paciente ainda no balcão, em vez de o faturamento correr atrás
    /// dele por telefone dias depois.
    /// </summary>
    public async Task<IReadOnlyList<PendenciaCodigo>> PendenciasDoDiaAsync(
        DateOnly dia, CancellationToken ct = default)
    {
        var doDia = await _agenda.DoDiaAsync(dia, ct);
        var pacientesDoDia = doDia
            .Where(a => a.OcupaAgenda)
            .Select(a => a.PacienteId)
            .ToHashSet();

        if (pacientesDoDia.Count == 0) return [];

        var pendentes = await _pendencias.CodigosPendentesAsync(dia, ct);
        return pendentes.Where(p => pacientesDoDia.Contains(p.PacienteId)).ToList();
    }
}
