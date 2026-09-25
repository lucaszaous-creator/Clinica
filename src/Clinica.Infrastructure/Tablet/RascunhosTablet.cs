using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure.Tablet;

public sealed partial class PostoTabletService
{
    private DocumentoClinicoService Documentos => new(repo,new(repo),new(repo));
    private PrescricaoInternaService Prescricoes => new(repo,conferencia);
    private static readonly TipoDocumentoClinico[] TiposEditaveis = [TipoDocumentoClinico.Receita,TipoDocumentoClinico.Atestado,
        TipoDocumentoClinico.PedidoExame,TipoDocumentoClinico.Comparecimento,TipoDocumentoClinico.RelatorioEvolucao,TipoDocumentoClinico.Anamnese];
    private static string VersaoDocumento(DocumentoClinico d) => Hash(new {d.Id,d.Numero,d.Corpo,d.CorpoFormatado,d.Observacoes,d.ObservacoesFormatadas,d.DiasAfastamento,
        d.AssinadoEm,d.PacienteAssinadoEm,d.CanceladoEm,d.Titulo,d.Cid,d.CidAutorizado,d.PeriodoInicio,d.PeriodoFim,d.HoraChegada,d.HoraSaida,
        Itens=d.Itens.OrderBy(i=>i.Ordem).Select(i=>new{i.Id,i.Ordem,i.Descricao,i.DescricaoFormatada,i.Detalhe,i.DetalheFormatado,i.Quantidade,i.Desenho,i.Codigo})});
    private async Task<DocumentoClinico> RascunhoDocumento(UsuarioSistema u,int paciente,int id,CancellationToken ct)
    {
        var d=await repo.ObterDocumentoAsync(id,ct)??throw new RecursoClinicoIndisponivel();
        if(d.PacienteId!=paciente||d.ProfissionalId!=u.ProfissionalId)throw new RecursoClinicoIndisponivel();
        if(d.AssinadoEm!=null||d.PacienteAssinadoEm!=null||d.CanceladoEm!=null||!TiposEditaveis.Contains(d.Tipo))
            throw new ConflitoClinicoTablet("Somente documentos próprios, sem assinatura e não cancelados podem ser corrigidos aqui.");
        return d;
    }
    private async Task<PrescricaoInterna> RascunhoInfusao(UsuarioSistema u,int paciente,int id,CancellationToken ct)
    {
        var p=await repo.ObterPrescricaoInternaAsync(id,ct)??throw new RecursoClinicoIndisponivel();
        if(p.PacienteId!=paciente||p.ProfissionalId!=u.ProfissionalId)throw new RecursoClinicoIndisponivel();
        if(!p.PodeEditar||p.Assinaturas.Count>0||p.Itens.Any(i=>i.Checagens.Count>0))
            throw new ConflitoClinicoTablet("Esta prescrição já foi assinada ou executada. A via original será preservada.");
        return p;
    }
    public async Task<object> RascunhoAsync(SessaoTablet s,int paciente,string tipo,int id,CancellationToken ct)
    {
        var u=await Autorizar(s,ct,Permissao.Prescrever);
        object resultado;
        if(tipo=="infusao") {
            var p=await RascunhoInfusao(u,paciente,id,ct);
            resultado=new {p.Id,p.Numero,Tipo=tipo,Versao=Versao(p),p.Data,p.Hora,p.Indicacao,p.IndicacaoFormatada,p.Observacoes,p.ObservacoesFormatadas,AssinaturaEnfermagem=p.ExigeAssinaturaEletronicaDaExecucao,
                Itens=p.Itens.OrderBy(i=>i.Ordem).Select(i=>new ItemInfusaoTablet(i.Descricao,i.Dose,i.Diluente,i.Volume,i.Via,i.TempoInfusao,i.HoraPrevista,i.SeNecessario,i.Observacoes,i.DescricaoFormatada,i.ObservacoesFormatadas))};
        } else if(tipo=="documento") {
            var d=await RascunhoDocumento(u,paciente,id,ct);
            resultado=new {d.Id,d.Numero,Tipo=tipo,Versao=VersaoDocumento(d),d.Corpo,d.CorpoFormatado,d.Observacoes,d.ObservacoesFormatadas,d.DiasAfastamento,
                TipoDocumento=d.Tipo.ToString(),Itens=d.Itens.OrderBy(i=>i.Ordem).Select(i=>new{i.Descricao,i.DescricaoFormatada,i.Detalhe,i.DetalheFormatado,i.Quantidade})};
        } else throw new RecursoClinicoIndisponivel();
        await Auditar(u,paciente,"TabletRascunhoConsultado",ct);await db.SaveChangesAsync(ct);return resultado;
    }
    public Task<ResultadoDocumentoTablet> CorrigirRascunhoAsync(SessaoTablet s,int paciente,string tipo,int id,RascunhoTablet p,CancellationToken ct)
        => Escrever(s,paciente,p.Idempotencia,new{tipo,id,p},"TabletRascunhoCorrigido",Permissao.Prescrever,async u=> {
            Textos(400,p.Motivo);Textos(20000,p.Corpo);Textos(1000,p.Observacoes);Textos(500,p.Indicacao);
            if(string.IsNullOrWhiteSpace(p.Motivo))throw ErroFormularioTablet.Criar("Informe o motivo da correção.");
            if(tipo=="infusao") {
                var anterior=await RascunhoInfusao(u,paciente,id,ct);ConferirVersao(p.Versao,Versao(anterior));
                if(p.Itens is not {Length:>0 and <=50})throw ErroFormularioTablet.Criar("Informe de 1 a 50 itens.");
                foreach(var i in p.Itens) {
                    Textos(20000,i.Descricao);Textos(1000,i.Observacoes);Textos(120,i.Diluente);Textos(60,i.Dose,i.Volume,i.TempoInfusao);
                    if(string.IsNullOrWhiteSpace(i.Descricao)||!Enum.IsDefined(i.Via))throw ErroFormularioTablet.Criar("Confira descrição e via de cada item.");
                }
                var nova=await Prescricoes.CriarAsync(paciente,u.ProfissionalId,anterior.AgendamentoId,anterior.EvolucaoId,u.Login,ct);
                await Prescricoes.SalvarRascunhoAsync(nova.Id,p.Indicacao,p.Observacoes,p.Itens.Select(i=>new ItemPrescricaoInterna {
                    Descricao=i.Descricao,DescricaoFormatada=i.DescricaoFormatada,Dose=i.Dose,Diluente=i.Diluente,Volume=i.Volume,Via=i.Via,TempoInfusao=i.TempoInfusao,
                    HoraPrevista=i.HoraPrevista,SeNecessario=i.SeNecessario,Observacoes=i.Observacoes,ObservacoesFormatadas=i.ObservacoesFormatadas}).ToArray(),u.Login,p.AssinaturaEnfermagem,ct,p.IndicacaoFormatada,p.ObservacoesFormatadas,
                    p.DataPrescricao??anterior.Data,p.HoraPrescricao??anterior.Hora);
                await Prescricoes.CancelarAsync(id,$"Substituída pela prescrição {nova.Numero}: {p.Motivo}",u.Login,ct);
                return new ResultadoDocumentoTablet(nova.Id,tipo,nova.Numero);
            }
            if(tipo!="documento")throw new RecursoClinicoIndisponivel();
            var d=await RascunhoDocumento(u,paciente,id,ct);ConferirVersao(p.Versao,VersaoDocumento(d));
            // Reemissão preserva os campos estruturados; a via anterior continua no histórico.
            var novo=await Documentos.EmitirAsync(new() {PacienteId=paciente,ProfissionalId=u.ProfissionalId,
                AgendamentoId=d.AgendamentoId,EvolucaoId=d.EvolucaoId,ModeloOrigemId=d.ModeloOrigemId,Tipo=d.Tipo,Data=d.Data,
                Titulo=d.Titulo,Corpo=p.Corpo,CorpoFormatado=p.CorpoFormatado,Observacoes=p.Observacoes,ObservacoesFormatadas=p.ObservacoesFormatadas,DiasAfastamento=d.Tipo==TipoDocumentoClinico.Atestado?p.DiasAfastamento:d.DiasAfastamento,
                Cid=d.Cid,CidAutorizado=d.CidAutorizado,PeriodoInicio=d.PeriodoInicio,PeriodoFim=d.PeriodoFim,HoraChegada=d.HoraChegada,HoraSaida=d.HoraSaida,
                Itens=d.Itens.OrderBy(i=>i.Ordem).Select(i=>new ItemDocumento {Descricao=i.Descricao,DescricaoFormatada=i.DescricaoFormatada,Detalhe=i.Detalhe,DetalheFormatado=i.DetalheFormatado,Quantidade=i.Quantidade,Desenho=i.Desenho,Codigo=i.Codigo}).ToList()},u.Login,ct);
            await Documentos.CancelarAsync(id,$"Substituído pelo documento {novo.Numero}: {p.Motivo}",u.Login,ct);
            return new ResultadoDocumentoTablet(novo.Id,tipo,novo.Numero);
        },ct);
    public Task<ResultadoFichaTablet> CancelarRascunhoAsync(SessaoTablet s,int paciente,string tipo,int id,CancelarRegistroTablet p,CancellationToken ct)
        => Escrever(s,paciente,p.Idempotencia,new{tipo,id,p},"TabletRascunhoCancelado",Permissao.Prescrever,async u=> {
            Textos(500,p.Motivo);
            if(tipo=="infusao") {var r=await RascunhoInfusao(u,paciente,id,ct);ConferirVersao(p.Versao,Versao(r));await Prescricoes.CancelarAsync(id,p.Motivo,u.Login,ct);}
            else if(tipo=="documento") {var r=await RascunhoDocumento(u,paciente,id,ct);ConferirVersao(p.Versao,VersaoDocumento(r));await Documentos.CancelarAsync(id,p.Motivo,u.Login,ct);}
            else throw new RecursoClinicoIndisponivel();
            return new ResultadoFichaTablet(id);
        },ct);
    private static string VersaoModelo(ModeloDocumento m) => Hash(new {m.Id,m.Nome,m.Tipo,m.Corpo,m.CorpoFormatado,m.AtualizadoEm,m.ParaInfusao,m.ConfiguracaoInfusao,Itens=m.Itens.OrderBy(i=>i.Ordem).Select(i=>new{i.Descricao,i.Detalhe,i.Quantidade,i.DescricaoFormatada,i.DetalheFormatado})});
    public async Task<object> ModelosDocumentoAsync(SessaoTablet s,CancellationToken ct)
    {
        await Autorizar(s,ct,Permissao.Prescrever);
        return (await Documentos.ModelosAsync(ct:ct)).Where(m=>m.Ativo&&TiposEditaveis.Contains(m.Tipo))
            .Select(m=>new {m.Id,m.Nome,Tipo=m.Tipo.ToString(),m.Corpo,m.CorpoFormatado,m.Titulo,m.ParaInfusao,m.ConfiguracaoInfusao,Versao=VersaoModelo(m),Itens=m.Itens.OrderBy(i=>i.Ordem).Select(i=>new{i.Descricao,i.DescricaoFormatada,i.Detalhe,i.DetalheFormatado,i.Quantidade})});
    }
}
