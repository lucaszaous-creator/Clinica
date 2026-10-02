using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure.Tablet;

public sealed partial class PostoTabletService
{
    public async Task<object> PrepararLiberacaoAsync(SessaoTablet s, int id, CancellationToken ct)
    {
        var u = await Autorizar(s, ct, Permissao.Prescrever);
        var p = await repo.ObterPrescricaoInternaAsync(id, ct) ?? throw new RecursoClinicoIndisponivel();
        if (p.ProfissionalId != u.ProfissionalId) throw new RecursoClinicoIndisponivel();
        var conferencia = await Prescricoes.ConferirAsync(p, ct);
        return new { p.Id, p.Numero, Paciente = p.Paciente!.Nome, Versao = Versao(p),
            conferencia.ExigeConfirmacao, Alertas = conferencia.Alertas.Select(a => a.Motivo) };
    }

    public async Task<ResultadoEnfermagemTablet> LiberarSemAssinaturaAsync(SessaoTablet s, int id,
        LiberarInfusaoTablet pedido, CancellationToken ct)
    {
        await Autorizar(s, ct, Permissao.Prescrever);
        var paciente = await db.PrescricoesInternas.Where(p => p.Id == id).Select(p => (int?)p.PacienteId)
            .SingleOrDefaultAsync(ct) ?? throw new RecursoClinicoIndisponivel();
        return await Escrever(s, paciente, pedido.Idempotencia, new { id, pedido }, "TabletLiberacaoSemAssinatura",
            Permissao.Prescrever, async u => {
                var p = await repo.ObterPrescricaoInternaAsync(id, ct) ?? throw new RecursoClinicoIndisponivel();
                if (p.ProfissionalId != u.ProfissionalId) throw new RecursoClinicoIndisponivel();
                ConferirVersao(pedido.Versao, Versao(p));
                p = await Prescricoes.LiberarSemAssinaturaAsync(id, u.Id, pedido.ConfirmouAlergia, ct);
                return new ResultadoEnfermagemTablet(id, p.Situacao.ToString());
            }, ct);
    }

    public async Task<ResultadoEnfermagemTablet> ConcluirSemAssinaturaAsync(SessaoTablet s, int id,
        EncerrarInfusaoTablet pedido, CancellationToken ct)
    {
        await Autorizar(s, ct, Permissao.ChecarPrescricao);
        var paciente = await db.PrescricoesInternas.Where(p => p.Id == id).Select(p => (int?)p.PacienteId)
            .SingleOrDefaultAsync(ct) ?? throw new RecursoClinicoIndisponivel();
        return await Escrever(s, paciente, pedido.Idempotencia, new { id, pedido }, "TabletExecucaoSemAssinatura",
            Permissao.ChecarPrescricao, async u => {
                new IdentificacaoExecutante(u.Id, u.Nome, u.Profissional!.RegistroConselho, u.Profissional.Cpf)
                    .Exigir("concluir a execução");
                if (!await ContinuidadeSemAssinatura.HabilitadaAsync(repo, ct))
                    throw ErroFormularioTablet.Criar("O modo temporário não está habilitado.");
                var p = await repo.ObterPrescricaoInternaAsync(id, ct) ?? throw new RecursoClinicoIndisponivel();
                ConferirVersao(pedido.Versao, Versao(p));
                if (p.Situacao != SituacaoPrescricao.Encerrada || p.DevolvidaEm is not null
                    || p.AssinaturaDaExecucao is not null || !p.ExecucaoCompleta
                    || p.OrigemEnfermagem && p.RegistradaPorUsuarioId != u.Id)
                    throw ErroFormularioTablet.Criar("Esta execução não está disponível para conclusão sem assinatura.");
                p.ModoSemAssinatura = true;
                p.ExigeAssinaturaEletronicaDaExecucao = false;
                p.AtualizadoEm = DateTime.Now;
                p.AtualizadoPor = u.Login;
                return new ResultadoEnfermagemTablet(id, p.Situacao.ToString());
            }, ct);
    }
}
