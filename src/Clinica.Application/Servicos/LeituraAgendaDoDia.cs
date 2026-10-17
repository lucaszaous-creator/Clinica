using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// O que a AGENDA DO DIA do balcão lê de uma vez (out/2026 — a tela virou Blazor). É a
/// MESMA leitura que o <c>FilaViewModel</c> WPF fazia, passo a passo e na mesma ordem: o dia,
/// as conclusões pendentes de dias anteriores (só olhando HOJE), as guias pendentes dos
/// pacientes do dia, a sincronização das assinaturas do celular, os termos, as respostas da
/// rodada de confirmação, os pacotes e os fechamentos já feitos.
///
/// ⚠️ SEQUENCIAL, no mesmo repositório (seção 3 do CLAUDE.md): é um <c>DbContext</c> só, e ele
/// não aceita duas operações ao mesmo tempo. E cada aviso que falha (pendência, termo,
/// confirmação, pacote, fechamento) NÃO derruba a lista — vai para o log e vira vazio, como
/// no WPF: é aviso, não o conteúdo da tela. Só o dia em si derruba (o terceiro estado).
///
/// Não é registrada no DI: quem lê a monta no PRÓPRIO escopo da operação
/// (<c>ActivatorUtilities</c>), porque a tela é de vida longa e o contexto precisa ser novo a
/// cada leitura — é o que faz a lista enxergar o que a outra máquina gravou.
/// </summary>
public sealed class LeituraAgendaDoDia(
    AgendaService agenda,
    PainelRecepcaoService painel,
    TermoProcedimentoService termos,
    ColetaRemotaTermoService coletaRemota,
    IClinicaRepositorio repo)
{
    public async Task<DadosDaAgendaDoDia> LerAsync(DateOnly dia, DateOnly hoje, CancellationToken ct = default)
    {
        var doDia = (await agenda.DoDiaAsync(dia, ct)).ToList();
        var horariosDoDia = doDia.ToList(); // só o dia, como o serviço de termos o leria
        var pacientesDoDia = doDia.Where(a => a.OcupaAgenda).Select(a => a.PacienteId).ToHashSet();

        // Pendências atravessam a virada do dia e continuam resolvíveis na mesma lista.
        if (dia == hoje)
        {
            var pendentes = await repo.HorariosComConclusaoPendenteAsync(dia, ct);
            doDia = doDia.Concat(pendentes).DistinctBy(a => a.Id).ToList();
        }

        HashSet<int> comPendencia;
        try
        {
            comPendencia = (await painel.PendenciasDosPacientesAsync(dia, pacientesDoDia, ct))
                .Select(p => p.PacienteId).ToHashSet();
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Recepção — pendências do dia não puderam ser conferidas", ex);
            comPendencia = [];
        }

        // ANTES de perguntar o que falta, PROCURA o que já chegou do celular: é esta lista,
        // relida a cada minuto, que fecha o circuito da assinatura remota sem depender de
        // alguém deixar a janela do termo aberta.
        try
        {
            await coletaRemota.SincronizarRespostasAsync(ct);
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Recepção — assinaturas do celular não puderam ser sincronizadas", ex);
        }

        IReadOnlyDictionary<int, IReadOnlyList<SituacaoTermo>> situacaoTermos;
        try
        {
            situacaoTermos = await termos.DoDiaAsync(dia, horariosDoDia, ct);
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Recepção — termos do dia não puderam ser conferidos", ex);
            situacaoTermos = new Dictionary<int, IReadOnlyList<SituacaoTermo>>();
        }

        IReadOnlyDictionary<int, RespostaDeConfirmacao> confirmacoes;
        try
        {
            confirmacoes = await repo.RespostasDeConfirmacaoAsync(doDia.Select(a => a.Id).ToList(), ct);
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Recepção — confirmações do dia não puderam ser lidas", ex);
            confirmacoes = new Dictionary<int, RespostaDeConfirmacao>();
        }

        Dictionary<int, (int Usadas, int Contratadas)> pacotes;
        try
        {
            var todos = await repo.PacotesDosPacientesAsync(doDia.Select(a => a.PacienteId).Distinct().ToList(), ct);
            pacotes = todos
                .Where(p => p.PodeConsumir(dia) && p.SessoesContratadas is not null)
                .GroupBy(p => p.PacienteId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        // O que vence primeiro é o que a baixa automática debita.
                        var p = g.OrderBy(x => x.ValidoAte ?? DateOnly.MaxValue).First();
                        return (p.SessoesUsadas, p.SessoesContratadas!.Value);
                    });
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Recepção — pacotes do dia não puderam ser lidos", ex);
            pacotes = [];
        }

        // Falhando, a pendência aparece em toda sessão COM PACOTE — o lado seguro: oferecer
        // debitar o que já foi debitado custa um clique (a janela recusa a duplicata).
        HashSet<int> jaFechados;
        try
        {
            var concluidos = doDia
                .Where(a => a.Etapa == EtapaFila.Finalizado && a.AtendimentoId is not null)
                .Select(a => a.AtendimentoId!.Value)
                .ToList();
            jaFechados = concluidos.Count == 0
                ? []
                : (await repo.AtendimentosComFechamentoAsync(concluidos, ct)).ToHashSet();
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Recepção — fechamentos do dia não puderam ser lidos", ex);
            jaFechados = [];
        }

        return new DadosDaAgendaDoDia(dia, doDia, comPendencia, situacaoTermos, confirmacoes, pacotes, jaFechados);
    }
}
