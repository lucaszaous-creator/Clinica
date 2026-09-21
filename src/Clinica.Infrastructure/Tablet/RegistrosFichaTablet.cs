using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure.Tablet;

public sealed partial class PostoTabletService
{
    public Task<ResultadoObservacoesEnfermagemTablet> RegistrarObservacoesEnfermagemAsync(
        SessaoTablet s, int paciente, ObservacoesEnfermagemTablet p, CancellationToken ct)
        => Escrever(s, paciente, p.Idempotencia, p, "TabletObservacoesEnfermagem",
            Permissao.RegistrarEvolucaoEnfermagem, async u => {
                if (p.AgendamentoId <= 0 || p.Observacoes is not { Length: >= 1 and <= 20 })
                    throw new InvalidOperationException("Escolha a sessão e informe de 1 a 20 observações por envio.");
                var autor = new IdentificacaoExecutante(u.Id, u.Nome, u.Profissional!.RegistroConselho);
                autor.Exigir("registrar as observações de enfermagem");
                var servico = new EvolucaoEnfermagemService(repo);
                var ids = new List<int>();
                // Escrever mantém todos os registros e o recibo na mesma transação.
                // Se qualquer observação falhar, nenhuma é persistida.
                foreach (var item in p.Observacoes)
                {
                    if (item is null) throw new InvalidOperationException("Confira as observações preenchidas.");
                    Textos(4000, item.Texto); Textos(300, item.AlergiaObservada);
                    if (item.NegaAlergia && !string.IsNullOrWhiteSpace(item.AlergiaObservada))
                        throw new InvalidOperationException("Escolha Nega ou descreva a alergia; não informe as duas opções na mesma observação.");
                    if (string.IsNullOrWhiteSpace(item.Texto))
                        throw new InvalidOperationException("Preencha a evolução da observação.");
                    // A negativa pertence à observação, nunca à lista de alergias ativas.
                    // Fica visível também no desktop e PDF, sem apagar alertas anteriores.
                    var texto = item.NegaAlergia ? item.Texto.TrimEnd() + "\n\nAlergia: NEGA."
                        : !string.IsNullOrWhiteSpace(item.AlergiaObservada)
                            ? item.Texto.TrimEnd() + "\n\nAlergia observada: " + item.AlergiaObservada.Trim()
                            : item.Texto;
                    Textos(4000, texto);
                    var e = await servico.RegistrarAsync(paciente, p.Data, item.Hora, texto, autor,
                        agendamentoId: p.AgendamentoId, intercorrencia: item.Intercorrencia,
                        sinais: item.Sinais, alergiaObservada: item.AlergiaObservada, ct: ct);
                    ids.Add(e.Id);
                }
                return new ResultadoObservacoesEnfermagemTablet(ids.ToArray());
            }, ct);

    public Task<ResultadoFichaTablet> RegistrarExameAsync(SessaoTablet s,int paciente,ResultadoExameTablet p,CancellationToken ct)
        => Escrever(s,paciente,p.Idempotencia,p,"TabletResultadoExame",Permissao.Nenhuma,async u=> {
            if(!PodeAnexar(u))throw new UnauthorizedAccessException();
            Textos(160,p.Nome,p.Referencia);Textos(120,p.Valor,p.Laboratorio);Textos(30,p.Unidade);Textos(1000,p.Observacoes);
            var r=await new ResultadoExameService(repo).RegistrarAsync(new() {PacienteId=paciente,Data=p.Data,Nome=p.Nome,
                Valor=p.Valor,Unidade=p.Unidade,Referencia=p.Referencia,Laboratorio=p.Laboratorio,Observacoes=p.Observacoes},u.Login,ct);
            return new ResultadoFichaTablet(r.Id);
        },ct);
    public Task<ResultadoFichaTablet> RegistrarEnfermagemAsync(SessaoTablet s,int paciente,RegistroEnfermagemTablet p,CancellationToken ct)
        => Escrever(s,paciente,p.Idempotencia,p,"TabletEvolucaoEnfermagem",Permissao.RegistrarEvolucaoEnfermagem,async u=> {
            Textos(4000,p.Texto);Textos(300,p.AlergiaObservada);Textos(500,p.Motivo);
            var autor=new IdentificacaoExecutante(u.Id,u.Nome,u.Profissional!.RegistroConselho);autor.Exigir("registrar a evolução de enfermagem");
            var servico=new EvolucaoEnfermagemService(repo);
            EvolucaoEnfermagem e;
            if(p.RetificaId is {} id) {
                var anterior=await repo.ObterEvolucaoEnfermagemAsync(id,ct)??throw new RecursoClinicoIndisponivel();
                if(anterior.PacienteId!=paciente)throw new RecursoClinicoIndisponivel();
                if(anterior.Diagnosticos.Count>0||anterior.Cuidados.Count>0)
                    throw new InvalidOperationException("Retifique o processo com diagnósticos e cuidados no módulo clínico, preservando o plano completo.");
                if(await db.EvolucoesEnfermagem.AnyAsync(x=>x.RetificaEvolucaoId==id&&x.CanceladaEm==null,ct))
                    throw new ConflitoClinicoTablet("Este registro já foi retificado. Atualize a ficha.");
                if(!string.IsNullOrWhiteSpace(p.AlergiaObservada))throw new InvalidOperationException("Registre a nova alergia em uma nova observação de enfermagem.");
                e=await servico.RetificarAsync(id,p.Data,p.Hora,p.Texto,autor,p.Motivo!,p.Intercorrencia,p.Sinais,
                    new(anterior.Historico,anterior.ExameFisico,anterior.Avaliacao),
                    new(anterior.AcessoLocal,anterior.AcessoCalibre,anterior.AcessoPuncionadoEm),ct);
            } else e=await servico.RegistrarAsync(paciente,p.Data,p.Hora,p.Texto,autor,intercorrencia:p.Intercorrencia,
                sinais:p.Sinais,alergiaObservada:p.AlergiaObservada,agendamentoId:p.AgendamentoId,ct:ct);
            return new ResultadoFichaTablet(e.Id);
        },ct);

    public Task<ResultadoFichaTablet> VincularEnfermagemAsync(SessaoTablet s, int paciente, int evolucao,
        VinculoEnfermagemTablet p, CancellationToken ct)
        => Escrever(s,paciente,p.Idempotencia,new {evolucao,p},"TabletVinculoEnfermagem",Permissao.RegistrarEvolucaoEnfermagem,async u=> {
            var e=await repo.ObterEvolucaoEnfermagemAsync(evolucao,ct)??throw new RecursoClinicoIndisponivel();
            if(e.PacienteId!=paciente)throw new RecursoClinicoIndisponivel();
            await new EvolucaoEnfermagemService(repo).VincularSessaoAsync(evolucao,p.AgendamentoId,u.Id,p.Motivo,ct);
            return new ResultadoFichaTablet(evolucao);
        },ct);
    public Task<ResultadoFichaTablet> CriarModeloAsync(SessaoTablet s,int paciente,NovoModeloDocumentoTablet p,CancellationToken ct)
        => Escrever(s,paciente,p.Idempotencia,p,"TabletModeloDocumento",Permissao.Prescrever,async u=> {
            Textos(120,p.Nome);Textos(20000,p.Texto);
            if(!TiposEditaveis.Contains(p.Tipo))throw new InvalidOperationException("Escolha um tipo de documento disponível.");
            if(string.IsNullOrWhiteSpace(p.Nome)||await db.ModelosDocumento.AnyAsync(m=>m.Tipo==p.Tipo&&m.Nome.ToLower()==p.Nome.Trim().ToLower(),ct))
                throw new InvalidOperationException("Dê um nome novo ao modelo. Os modelos existentes são preservados.");
            var m=await Documentos.SalvarModeloAsync(new() {Nome=p.Nome,Tipo=p.Tipo,Corpo=p.Texto,Ativo=true},u.Login,ct);
            return new ResultadoFichaTablet(m.Id);
        },ct);
}
