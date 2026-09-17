using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure.Tablet;

public sealed partial class AtendimentoTabletService
{
    private async Task<object> ResumoFaturamentoAsync(Agendamento a, CancellationToken ct)
    {
        var paciente = a.Paciente!;
        var data = DateOnly.FromDateTime(a.DataHora);
        var avisos = new List<string>();
        if (paciente.ConvenioADefinir)
            avisos.Add("A recepção precisa definir o convênio no cadastro antes da conclusão. A evolução pode ser salva.");
        if (!paciente.ConvenioADefinir && CatalogoConvenios.GeraGuia(paciente.ConvenioCodigo ?? paciente.Convenio.ToString()))
        {
            if (string.IsNullOrWhiteSpace(paciente.Carteirinha)) avisos.Add("Carteirinha não informada. Solicite conferência à recepção.");
            if (paciente.ValidadeCarteirinha is {} validade && validade < data) avisos.Add("Carteirinha vencida na data da sessão. Solicite conferência à recepção.");
        }
        var atendimento = a.AtendimentoId is {} id ? await db.Atendimentos.AsNoTracking()
            .Include(x => x.Codigos).SingleOrDefaultAsync(x => x.Id == id && x.PacienteId == a.PacienteId, ct) : null;
        IReadOnlyList<CodigoFaturamento> codigos = atendimento?.Codigos ?? [];
        if (atendimento is null && !paciente.ConvenioADefinir)
        {
            var previa = await new AtendimentoService(repo, parametros: new(repo)).PreverAsync(a.PacienteId, data,
                a.ModalidadePrevista, ct, a.PrimeiroCodigo, a.EspecialidadeConsulta, a.ModalidadeCodigo, a.EspecialidadeConsultaCodigo);
            codigos = previa.Codigos;
            avisos.AddRange(previa.Avisos);
        }
        var pacote = atendimento is not null && await db.ConsumosPacote.AsNoTracking()
            .AnyAsync(c=>c.AtendimentoId==atendimento.Id&&c.CanceladoEm==null,ct);
        // Somente a situação operacional, sem valores, forma de pagamento ou dados do caixa.
        var recebimentos = atendimento is null ? [] : await db.Lancamentos.AsNoTracking()
            .Where(l=>l.Tipo==TipoLancamento.Entrada&&l.Status!=StatusLancamento.Cancelado
                &&(l.AtendimentoId==atendimento.Id||l.CodigoFaturamento!=null&&l.CodigoFaturamento.AtendimentoId==atendimento.Id))
            .Select(l=>l.Status).Distinct().ToListAsync(ct);
        return new {
            paciente.ConvenioNome, paciente.Carteirinha, paciente.ValidadeCarteirinha,
            PodeConcluir = !paciente.ConvenioADefinir, Previa = atendimento is null,
            AtendimentoId = atendimento?.Id, Numero = atendimento?.Numero, atendimento?.RealizadoEm,
            ConclusaoClinica = a.FimAtendimentoEm, Estornado = atendimento?.EstornadoEm is not null,
            Avisos = avisos,
            RecepcaoDetalhe = new {SessaoDebitadaDoPacote=pacote,
                RecebimentoRegistrado=recebimentos.Contains(StatusLancamento.Realizado),
                ContaAReceberRegistrada=recebimentos.Contains(StatusLancamento.Previsto),
                Particular=!paciente.ConvenioADefinir&&!CatalogoConvenios.GeraGuia(paciente.ConvenioCodigo??paciente.Convenio.ToString())},
            Guias = codigos.OrderBy(c => c.Ordem).Select(c => new {c.Id, Tipo = c.Tipo.ToString(),
                Ordem = c.Ordem.ToString(), Situacao = c.Status.ToString(), c.DataPrevistaFaturamento,
                c.DataBaixa, c.NumeroGuiaReal, c.Descricao,
                DisponivelParaFaturar = atendimento is not null && atendimento.EstornadoEm is null && c.DataBaixa is null
                    && c.Status != StatusCodigo.NaoAplicavel && c.Status != StatusCodigo.NaoConformidade
                    && c.DataPrevistaFaturamento <= Hoje}),
            Recepcao = "A recepção confere guias, pacote, materiais e cobrança no sistema Clínica. Conclusão clínica e baixa da guia não confirmam recebimento."
        };
    }
}
