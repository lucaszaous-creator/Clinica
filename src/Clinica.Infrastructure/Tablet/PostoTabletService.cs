using System.Data;
using System.Text.Json;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure.Tablet;

/// <summary>Ficha e atos fora da agenda; acesso clínico explícito e auditado, sem direitos administrativos.</summary>
public sealed partial class PostoTabletService(ClinicaDbContext db, IClinicaRepositorio repo, AtendimentoTabletService acesso,
    AgendaService agenda, PrescricaoService conferencia, ChecagemPrescricaoService checagem)
{
    private const Permissao Leitura = Permissao.VerFichaPaciente | Permissao.VerProntuario;
    public async Task<SessoesEnfermagemPagina> SessoesEnfermagemAsync(SessaoTablet s, FiltroSessoesEnfermagem filtro, CancellationToken ct)
    {
        var u = await Autorizar(s, ct, Permissao.RegistrarEvolucaoEnfermagem | Permissao.VerAgenda);
        return await new SessoesEnfermagemService(db).ListarAsync(u.Id, filtro, acesso.Hoje, ct);
    }
    private async Task<UsuarioSistema> Autorizar(SessaoTablet s, CancellationToken ct, Permissao adicional=Permissao.Nenhuma)
        => await acesso.AutorizarAsync(s,ct,Leitura|adicional);
    private Task Auditar(UsuarioSistema u,int paciente,string acao,CancellationToken ct)
        => repo.RegistrarAuditoriaAsync(new EventoAuditoria {Operador=u.Login,PacienteId=paciente,Acao=acao,Detalhe="Acesso pelo posto clínico no tablet"},ct);
    private async Task<Paciente> Paciente(int id,CancellationToken ct)
        => await db.Pacientes.AsNoTracking().SingleOrDefaultAsync(p=>p.Id==id,ct) ?? throw new RecursoClinicoIndisponivel();

    public async Task<object> AgendaAsync(SessaoTablet s,DateOnly? dia,CancellationToken ct)
    {
        var u=await Autorizar(s,ct,Permissao.VerAgenda);
        if(PoliticaAtendimentoTablet.PodeAtender(u))return await acesso.DiaAsync(s,dia,ct);
        var data=dia??acesso.Hoje;
        if(Math.Abs(data.DayNumber-acesso.Hoje.DayNumber)>366)throw new InvalidOperationException("Escolha uma data no intervalo de um ano.");
        var inicio=data.ToDateTime(TimeOnly.MinValue);var fim=inicio.AddDays(1);
        var horarios=await db.Agendamentos.AsNoTracking().Where(a=>a.DataHora>=inicio&&a.DataHora<fim&&(a.Status==StatusAgendamento.Agendado||a.Status==StatusAgendamento.Realizado))
            .OrderBy(a=>a.DataHora).Take(300).Select(a=>new {a.Id,a.PacienteId,Nome=a.Paciente!.Nome,Nascimento=a.Paciente.DataNascimento,a.DataHora,Modalidade=a.ModalidadePrevista.ToString(),
                a.ChegadaEm,a.InicioAtendimentoEm,a.FimAtendimentoEm,Finalizado=a.FimAtendimentoEm!=null}).ToListAsync(ct);
        return new {Data=data,Profissional=u.Profissional!.Nome,Horarios=horarios};
    }
    public async Task<object> MapaAsync(SessaoTablet s,int paciente,int evolucao,CancellationToken ct)
    {
        var u=await Autorizar(s,ct);await Paciente(paciente,ct);
        if(!await db.Evolucoes.AnyAsync(e=>e.Id==evolucao&&e.PacienteId==paciente&&e.CanceladaEm==null,ct))throw new RecursoClinicoIndisponivel();
        var m=await db.MapasCorporais.AsNoTracking().Include(m=>m.Pontos).SingleOrDefaultAsync(m=>m.EvolucaoId==evolucao,ct)??throw new RecursoClinicoIndisponivel();
        await Auditar(u,paciente,"TabletFichaMapaConsultado",ct);await db.SaveChangesAsync(ct);
        return new {m.Observacoes,Pontos=m.Pontos.OrderBy(p=>p.Ordem).Select(p=>new {p.Face,p.X,p.Y,p.Nome,TecnicaNome=p.Tecnica.ToString(),p.Observacao}),
            Formas=SilhuetaCorporal.Formas.Select(f=>f switch {ElipseSilhueta el=>new {Tipo="elipse",X=el.Cx,Y=el.Cy,Largura=el.Rx,Altura=el.Ry,Raio=0d},RetanguloSilhueta r=>new {Tipo="retangulo",r.X,r.Y,r.Largura,r.Altura,r.Raio},_=>throw new InvalidOperationException("Forma indisponível.")})};
    }
    public async Task<byte[]> AnexoPdfAsync(SessaoTablet s,int paciente,int id,CancellationToken ct)
    {
        var u=await Autorizar(s,ct);
        var a=await db.AnexosPaciente.AsNoTracking().Include(a=>a.Arquivo).SingleOrDefaultAsync(a=>a.Id==id&&a.PacienteId==paciente&&a.CanceladoEm==null,ct)??throw new RecursoClinicoIndisponivel();
        var bytes=a.Arquivo?.Conteudo;
        if(a.TipoConteudo!="application/pdf"||bytes is null||bytes.Length<5||!bytes.AsSpan(0,5).SequenceEqual("%PDF-"u8))throw new InvalidOperationException("O PDF não está disponível no banco. Consulte o anexo no desktop.");
        await Auditar(u,paciente,"TabletFichaAnexoConsultado",ct);await db.SaveChangesAsync(ct);return bytes;
    }

    public async Task<object> BuscarAsync(SessaoTablet s,string? busca,CancellationToken ct)
    {
        await Autorizar(s,ct);var q=busca?.Trim();
        if(q?.Length is not (>=3 and <=80)) return Array.Empty<object>();
        return await db.Pacientes.AsNoTracking().Where(p=>p.Nome.ToLower().Contains(q.ToLower()) || p.Documento==q)
            .OrderBy(p=>p.Nome).Take(30).Select(p=>new {p.Id,p.Nome,Nascimento=p.DataNascimento}).ToListAsync(ct);
    }
    public async Task<object> FichaAsync(SessaoTablet s,int id,int pagina,CancellationToken ct)
    {
        var u=await Autorizar(s,ct);var p=await Paciente(id,ct);
        if(pagina is <0 or >10000) throw new InvalidOperationException("Página inválida.");
        var skip=pagina*25;
        var sessoes=await db.Agendamentos.AsNoTracking().Where(a=>a.PacienteId==id).OrderByDescending(a=>a.DataHora).ThenByDescending(a=>a.Id).Skip(skip).Take(26)
            .Select(a=>new {a.Id,a.DataHora,Modalidade=a.ModalidadePrevista.ToString(),Situacao=a.Status.ToString(),Profissional=a.Profissional==null?null:a.Profissional.Nome,
                Proprio=a.ProfissionalId==u.ProfissionalId,a.ChegadaEm,a.InicioAtendimentoEm,a.FimAtendimentoEm,a.AtendimentoId,
                EvolucaoSalva=db.Evolucoes.Any(e=>e.AgendamentoId==a.Id&&e.CanceladaEm==null)}).ToListAsync(ct);
        var evolucoes=await db.Evolucoes.AsNoTracking().Where(e=>e.PacienteId==id&&e.CanceladaEm==null).OrderByDescending(e=>e.Data).ThenByDescending(e=>e.Id).Skip(skip).Take(26)
            .Select(e=>new {e.Id,e.Data,e.QueixaPrincipal,e.HistoriaDoencaAtual,e.ExameFisico,e.HipoteseDiagnostica,e.CidSessao,e.Conduta,e.TextoEvolucao,e.Orientacoes,e.PlanoTerapeutico,e.EvaAntes,e.EvaDepois,e.RetornoSugeridoEm,e.RetornoSugeridoNota,e.Encaminhamento,
                Profissional=e.Profissional==null?e.CriadoPor:e.Profissional.Nome,TemMapa=db.MapasCorporais.Any(m=>m.EvolucaoId==e.Id)}).ToListAsync(ct);
        var documentos=await db.DocumentosClinicos.AsNoTracking().Where(d=>d.PacienteId==id&&d.CanceladoEm==null).OrderByDescending(d=>d.Id).Skip(skip).Take(26)
            .Select(d=>new {d.Id,d.Numero,Tipo=d.Tipo.ToString(),d.Titulo,d.Corpo,d.Observacoes,d.DiasAfastamento,d.Data,d.AssinadoEm,d.PacienteAssinadoEm,d.AgendamentoId,Proprio=d.ProfissionalId==u.ProfissionalId,
                Itens=d.Itens.OrderBy(i=>i.Ordem).Select(i=>new {i.Descricao,i.Detalhe,i.Quantidade})}).ToListAsync(ct);
        var infusoes=await db.PrescricoesInternas.AsNoTracking().Where(x=>x.PacienteId==id&&x.CanceladaEm==null).OrderByDescending(x=>x.Id).Skip(skip).Take(26)
            .Select(x=>new {x.Id,x.Numero,x.Data,Situacao=x.OrigemEnfermagem&&x.AssinadaEm==null&&x.Situacao==SituacaoPrescricao.Encerrada?"AguardaMedico":x.Situacao.ToString(),x.AssinadaEm,x.EncerradaEm,x.OrigemEnfermagem,x.OrientacaoExterna,x.Indicacao,x.Observacoes,x.AgendamentoId,Proprio=x.ProfissionalId==u.ProfissionalId,Assinaturas=x.Assinaturas.Select(a=>new {Papel=a.Papel.ToString(),a.NomeAssinante,a.RegistroConselho,a.AssinadoEm,PrescricaoArquivada=a.ArquivoId!=null,RegistroArquivado=a.ArquivoRegistroId!=null}),
                Itens=x.Itens.OrderBy(i=>i.Ordem).Select(i=>new {i.Descricao,i.Dose,Via=i.Via.ToString(),i.Diluente,i.Volume,i.TempoInfusao,i.HoraPrevista,i.SeNecessario,i.Observacoes,i.SuspensoEm,i.MotivoSuspensao})}).ToListAsync(ct);
        var medidas=await db.MedidasClinicas.AsNoTracking().Where(m=>m.PacienteId==id&&m.CanceladaEm==null).OrderByDescending(m=>m.Data).ThenByDescending(m=>m.Id).Skip(skip).Take(26)
            .Select(m=>new {m.Id,m.Data,m.TipoNome,m.Valor,m.ValorSecundario,m.Unidade,m.Observacoes,Versao=VersaoMedida(m)}).ToListAsync(ct);
        var anexos=await db.AnexosPaciente.AsNoTracking().Where(a=>a.PacienteId==id&&a.CanceladoEm==null).OrderByDescending(a=>a.Id).Skip(skip).Take(26)
            .Select(a=>new {a.Id,a.Data,a.Titulo,a.NomeArquivo,a.TipoConteudo,a.Tamanho,a.Observacoes,Disponivel=a.Arquivo!=null}).ToListAsync(ct);
        var problemas=await db.ProblemasPaciente.AsNoTracking().Where(x=>x.PacienteId==id).OrderByDescending(x=>x.Id).Take(100)
            .ToListAsync(ct);
        var enfermagem=await db.EvolucoesEnfermagem.AsNoTracking().Include(e=>e.Diagnosticos).Include(e=>e.Cuidados).ThenInclude(c=>c.Checagens).AsSplitQuery().Where(e=>e.PacienteId==id).OrderByDescending(e=>e.Id).Skip(skip).Take(26).ToListAsync(ct);
        var anamnese=await db.Anamneses.AsNoTracking().Where(a=>a.PacienteId==id).SingleOrDefaultAsync(ct);
        var exames=await db.ResultadosExame.AsNoTracking().Where(x=>x.PacienteId==id&&x.CanceladoEm==null).OrderByDescending(x=>x.Data).ThenByDescending(x=>x.Id).Skip(skip).Take(26)
            .Select(x=>new {x.Id,x.Data,x.Nome,x.Valor,x.Unidade,x.Referencia,x.Laboratorio,x.Observacoes,x.ArquivoNome,x.ArquivoTipoConteudo}).ToListAsync(ct);
        var avaliacoes=await db.AvaliacoesClinicas.AsNoTracking().Where(x=>x.PacienteId==id&&x.CanceladaEm==null).OrderByDescending(x=>x.Data).ThenByDescending(x=>x.Id).Skip(skip).Take(26)
            .Select(x=>new {x.Id,x.Data,x.InstrumentoNome,x.Pontuacao,x.PontuacaoMaxima,x.Unidade,x.AlertaItem,x.Observacoes,Respostas=x.Respostas.OrderBy(r=>r.Ordem).Select(r=>new {r.Enunciado,r.OpcaoRotulo,r.Valor})}).ToListAsync(ct);
        var evolucaoIds=evolucoes.Take(25).Select(e=>e.Id).ToArray();
        var campos=await db.ValoresCampoPersonalizado.AsNoTracking().Where(x=>evolucaoIds.Contains(x.EvolucaoId)).Select(x=>new {x.EvolucaoId,x.Rotulo,x.Valor}).ToListAsync(ct);
        await Auditar(u,id,"TabletFichaConsultada",ct);await db.SaveChangesAsync(ct);
        return new {Paciente=new {p.Id,p.Nome,p.Documento,p.DataNascimento,p.Telefone,p.Email,p.Endereco,p.Carteirinha,p.ValidadeCarteirinha,p.ConvenioNome,Sexo=p.Sexo.ToString(),p.Observacoes},
            PodeAtender=PoliticaAtendimentoTablet.PodeAtender(u),PodePrescrever=u.Pode(Permissao.Prescrever),PodeExecutar=u.Pode(Permissao.ChecarPrescricao),Pagina=pagina,
            Mais=sessoes.Count>25||evolucoes.Count>25||exames.Count>25||avaliacoes.Count>25||documentos.Count>25||infusoes.Count>25||medidas.Count>25||anexos.Count>25||enfermagem.Count>25,
            Anamnese=DadosAnamnese(anamnese),VersaoAnamnese=Hash(DadosAnamnese(anamnese)),PodeEditarFicha=u.Pode(Permissao.EditarProntuario),PodeAnexar=PodeAnexar(u),PodeRegistrarEnfermagem=u.Pode(Permissao.RegistrarEvolucaoEnfermagem),TiposMedida=MedidaClinicaService.Registraveis.Select(t=>new {t.Codigo,t.Nome,t.Unidade,t.Minimo,t.Maximo,t.RotuloSegundoValor}),Exames=exames.Take(25),Avaliacoes=avaliacoes.Take(25),Campos=campos,Sessoes=sessoes.Take(25),Evolucoes=evolucoes.Take(25),Documentos=documentos.Take(25),Infusoes=infusoes.Take(25),Medidas=medidas.Take(25),Anexos=anexos.Take(25),Problemas=problemas.Select(x=>new {x.Id,Tipo=x.Natureza.ToString(),x.Descricao,Situacao=x.Situacao.ToString(),x.Cid,x.Observacoes,x.Inicio,x.Fim,Versao=Hash(DadosProblema(x))}),
            Enfermagem=enfermagem.Take(25).Select(e=>new {e.Id,e.AgendamentoId,PodeVincular=e.AutorUsuarioId==u.Id||u.Perfil==PerfilAcesso.Gerente,e.RegistradoEm,e.Data,e.Texto,e.AutorNome,e.AutorConselho,e.Hora,e.Historico,e.ExameFisico,e.Avaliacao,e.Intercorrencia,e.PressaoSistolica,e.PressaoDiastolica,e.FrequenciaCardiaca,e.FrequenciaRespiratoria,e.Temperatura,e.SaturacaoOxigenio,e.Dor,e.RetificaEvolucaoId,e.MotivoRetificacao,e.CanceladaEm,e.MotivoCancelamento,e.AcessoLocal,e.AcessoCalibre,e.AcessoPuncionadoEm,Diagnosticos=e.Diagnosticos.OrderBy(d=>d.Ordem).Select(d=>new {d.Codigo,d.Titulo,d.RelacionadoA,d.EvidenciadoPor,d.ResultadoEsperado}),Cuidados=e.Cuidados.OrderBy(c=>c.Ordem).Select(c=>new {c.Codigo,c.Descricao,c.Frequencia,c.SeNecessario,Checagens=c.Checagens.OrderBy(x=>x.Id).Select(x=>new {x.Id,x.Data,x.HoraRealizacao,Situacao=x.Situacao.ToString(),x.Justificativa,x.Observacao,x.ExecutanteNome,x.ExecutanteConselho,x.RetificaChecagemId,x.MotivoRetificacao})})})};
    }

    // O recibo avulso não depende de criar uma sessão de atendimento fictícia.
    private async Task<T> Escrever<T>(SessaoTablet s,int paciente,Guid chave,object pedido,string acao,Permissao permissao,Func<UsuarioSistema,Task<T>> executar,CancellationToken ct)
    {
        if(chave==Guid.Empty) throw new InvalidOperationException("Atualize antes de enviar.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        if(db.Database.IsNpgsql())await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(20260917, {paciente})",ct);
        var u=await Autorizar(s,ct,permissao);await Paciente(paciente,ct);
        var hash=ContratoTablet.Hash(ContratoTablet.Serializar(new {acao,paciente,pedido}));
        var recibo=await db.Set<OperacaoClinicaTablet>().SingleOrDefaultAsync(o=>o.Id==chave,ct);
        if(recibo!=null){if(recibo.UsuarioId!=u.Id||recibo.PacienteId!=paciente||recibo.PedidoHash!=hash)throw new ConflitoClinicoTablet("Este envio já foi utilizado. Atualize antes de continuar.");await tx.CommitAsync(ct);return JsonSerializer.Deserialize<T>(recibo.ResultadoJson,ContratoTablet.Json)!;}
        var resultado=await executar(u);
        db.Set<OperacaoClinicaTablet>().Add(new() {Id=chave,UsuarioId=u.Id,PacienteId=paciente,PedidoHash=hash,ResultadoJson=ContratoTablet.Serializar(resultado),CriadaEm=acesso.Agora});
        await Auditar(u,paciente,acao,ct);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return resultado;
    }
    public Task<ResultadoAvulsoTablet> IniciarAsync(SessaoTablet s,int paciente,IniciarAvulsoTablet p,CancellationToken ct)
        => Escrever(s,paciente,p.Idempotencia,p,"TabletAtendimentoAvulso",Permissao.EditarProntuario|Permissao.LancarAtendimento,async u=>
        {
            if(!PoliticaAtendimentoTablet.PodeAtender(u))throw new UnauthorizedAccessException("Seu perfil registra enfermagem, sem criar atendimento médico.");
            if(!Enum.IsDefined(p.Modalidade)||p.Motivo?.Length>1000)throw new InvalidOperationException("Confira modalidade e observações.");
            var inicio=acesso.Hoje.ToDateTime(TimeOnly.MinValue);var fim=inicio.AddDays(1);
            var existentes=await db.Agendamentos.Where(a=>a.PacienteId==paciente&&a.ProfissionalId==u.ProfissionalId&&a.DataHora>=inicio&&a.DataHora<fim&&a.Status==StatusAgendamento.Agendado).Take(2).ToListAsync(ct);
            if(existentes.Count>1)throw new ConflitoClinicoTablet("Há mais de uma sessão aberta hoje. Escolha a sessão na agenda para evitar duplicidade.");
            if(existentes.Count==1)return new ResultadoAvulsoTablet(existentes[0].Id,true);
            var agora=TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(acesso.Agora),TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime;
            var a=await agenda.AgendarAsync(paciente,agora,p.Modalidade,p.Motivo,ct:ct,profissionalId:u.ProfissionalId,encaixe:true,operador:u.Login);
            await agenda.IniciarAtendimentoAsync(a.Id,u.Login,quando:agora,ct:ct);
            return new ResultadoAvulsoTablet(a.Id,false);
        },ct);
    public Task<ResultadoDocumentoTablet> EmitirAsync(SessaoTablet s,int paciente,EmitirDocumentoTablet pedido,CancellationToken ct)
        => Escrever(s,paciente,pedido.Idempotencia,pedido,"TabletDocumentoAvulso",Permissao.Prescrever,
            u=>acesso.EmitirConteudoAsync(u,paciente,null,null,pedido,ct),ct);

    public async Task<object> FilaAsync(SessaoTablet s,int pagina,CancellationToken ct)
    {
        await Autorizar(s,ct,Permissao.ChecarPrescricao);
        if(pagina is <0 or >10000)throw new InvalidOperationException("Página inválida.");
        var folhas=await db.PrescricoesInternas.AsNoTracking().Include(p=>p.Paciente).Include(p=>p.Itens).ThenInclude(i=>i.Checagens).Include(p=>p.Assinaturas)
            .Where(p=>p.CanceladaEm==null&&((p.OrigemEnfermagem && p.AssinadaEm == null && p.Situacao == SituacaoPrescricao.Encerrada)||p.Situacao==SituacaoPrescricao.Assinada||p.Situacao==SituacaoPrescricao.Encerrada&&p.ExigeAssinaturaEletronicaDaExecucao&&!p.Assinaturas.Any(a=>a.Papel==PapelAssinatura.Executante&&a.ArquivoRegistroId!=null)))
            .OrderBy(p=>p.Data).ThenBy(p=>p.Id).Skip(pagina*50).Take(51).ToListAsync(ct);
        return new {Pagina=pagina,Mais=folhas.Count>50,Itens=folhas.Take(50).Select(p=>new {p.Id,p.Numero,p.Data,p.PacienteId,Paciente=p.Paciente!.Nome,Nascimento=p.Paciente.DataNascimento,Situacao=p.OrigemEnfermagem&&p.AssinadaEm==null&&p.Situacao==SituacaoPrescricao.Encerrada?"AguardaMedico":p.Situacao.ToString(),p.Pendentes,p.ExigeAssinaturaEletronicaDaExecucao,RegistroSemAssinatura=p.AssinaturaDaExecucao is {} a&&a.ArquivoRegistroId is null})};
    }
    public static string Versao(PrescricaoInterna p)=>ContratoTablet.Hash(ContratoTablet.Serializar(new {p.Id,p.Situacao,p.AtualizadoEm,p.EncerradaEm,p.OrigemEnfermagem,p.OrientacaoExterna,p.RegistradaPorUsuarioId,
        Itens=p.Itens.OrderBy(i=>i.Id).Select(i=>new {i.Id,i.Descricao,i.Dose,i.Via,i.Diluente,i.Volume,i.TempoInfusao,i.HoraPrevista,i.SeNecessario,i.Observacoes,i.SuspensoEm,i.MotivoSuspensao,Checagens=i.Checagens.OrderBy(c=>c.Id).Select(c=>new {c.Id,c.Situacao,Hora=c.HoraRealizacao,c.Justificativa,c.RetificaChecagemId})})}));
    public async Task<object> InfusaoAsync(SessaoTablet s,int id,CancellationToken ct)
    {
        var u=await Autorizar(s,ct,Permissao.ChecarPrescricao);
        var p=await repo.ObterPrescricaoInternaAsync(id,ct)??throw new RecursoClinicoIndisponivel();
        if(p.CanceladaEm!=null||p.Situacao==SituacaoPrescricao.Rascunho)throw new RecursoClinicoIndisponivel();
        var alertas=await conferencia.ContextoAsync(p.PacienteId,ct);await Auditar(u,p.PacienteId,"TabletInfusaoConsultada",ct);await db.SaveChangesAsync(ct);
        return new {p.Id,p.Numero,p.Data,p.PacienteId,Paciente=p.Paciente!.Nome,Nascimento=p.Paciente.DataNascimento,p.Indicacao,p.Observacoes,Situacao=p.OrigemEnfermagem&&p.AssinadaEm==null&&p.Situacao==SituacaoPrescricao.Encerrada?"AguardaMedico":p.Situacao.ToString(),Versao=Versao(p),p.ExecucaoCompleta,p.OrigemEnfermagem,p.OrientacaoExterna,PodeAssinarExecucao=!p.OrigemEnfermagem||p.RegistradaPorUsuarioId==u.Id,p.ExigeAssinaturaEletronicaDaExecucao,ExecucaoAssinadaEletronicamente=p.AssinaturaDaExecucao!=null,Assinaturas=p.Assinaturas.Select(a=>new {Papel=a.Papel.ToString(),a.NomeAssinante,a.RegistroConselho,a.AssinadoEm,PrescricaoArquivada=a.ArquivoId!=null,RegistroArquivado=a.ArquivoRegistroId!=null}),
            Alergias=alertas.Alergias.Select(a=>a.Descricao),Itens=p.Itens.OrderBy(i=>i.Ordem).Select(i=>new {i.Id,i.Descricao,i.Dose,Via=i.Via.ToString(),i.Diluente,i.Volume,i.TempoInfusao,i.HoraPrevista,i.SeNecessario,i.Observacoes,i.SuspensoEm,i.MotivoSuspensao,
                Situacao=i.Situacao.ToString(),Checagem=i.ChecagemVigente is {} c?new {c.Id,Situacao=c.Situacao.ToString(),Hora=c.HoraRealizacao,c.Justificativa,c.ExecutanteNome,c.ExecutanteConselho}:null})};
    }
    public async Task<ResultadoEnfermagemTablet> ChecarAsync(SessaoTablet s,int id,ChecarInfusaoTablet pedido,CancellationToken ct)
    {
        await Autorizar(s,ct,Permissao.ChecarPrescricao);
        var paciente=await db.PrescricoesInternas.Where(p=>p.Id==id).Select(p=>(int?)p.PacienteId).SingleOrDefaultAsync(ct)??throw new RecursoClinicoIndisponivel();
        return await Escrever(s,paciente,pedido.Idempotencia,new {id,pedido},"TabletChecagem",Permissao.ChecarPrescricao,async u=>
        {
            var p=await repo.ObterPrescricaoInternaAsync(id,ct)??throw new RecursoClinicoIndisponivel();
            if(Versao(p)!=pedido.Versao)throw new ConflitoClinicoTablet("A infusão mudou. Atualize e confira a checagem antes de continuar.");
            if(!p.Itens.Any(i=>i.Id==pedido.ItemId))throw new RecursoClinicoIndisponivel();
            if(!Enum.IsDefined(pedido.Situacao)||pedido.Justificativa?.Length>1000||pedido.AlergiaObservada?.Length>500||pedido.MotivoRetificacao?.Length>1000)throw new InvalidOperationException("Confira os dados da checagem.");
            var autor=new IdentificacaoExecutante(u.Id,u.Nome,u.Profissional!.RegistroConselho);autor.Exigir("registrar a execução");
            if(string.IsNullOrWhiteSpace(pedido.MotivoRetificacao))await checagem.ChecarAsync(pedido.ItemId,pedido.Situacao,pedido.Hora,autor,pedido.Justificativa,pedido.AlergiaObservada,pedido.ConfirmouAlergia,ct);
            else await checagem.RetificarAsync(pedido.ItemId,pedido.Situacao,pedido.Hora,autor,pedido.MotivoRetificacao,pedido.Justificativa,pedido.ConfirmouAlergia,ct);
            return new ResultadoEnfermagemTablet(id,p.Situacao.ToString());
        },ct);
    }
    public async Task<ResultadoEnfermagemTablet> EncerrarAsync(SessaoTablet s,int id,EncerrarInfusaoTablet pedido,CancellationToken ct)
    {
        await Autorizar(s,ct,Permissao.ChecarPrescricao);
        var paciente=await db.PrescricoesInternas.Where(p=>p.Id==id).Select(p=>(int?)p.PacienteId).SingleOrDefaultAsync(ct)??throw new RecursoClinicoIndisponivel();
        return await Escrever(s,paciente,pedido.Idempotencia,new {id,pedido},"TabletEncerrarInfusao",Permissao.ChecarPrescricao,async u=>
        {
            var p=await repo.ObterPrescricaoInternaAsync(id,ct)??throw new RecursoClinicoIndisponivel();
            if(Versao(p)!=pedido.Versao)throw new ConflitoClinicoTablet("A infusão mudou. Atualize antes de encerrar.");
            var autor=new IdentificacaoExecutante(u.Id,u.Nome,u.Profissional!.RegistroConselho);autor.Exigir("encerrar a execução");
            p=await checagem.EncerrarAsync(id,autor,ct);return new ResultadoEnfermagemTablet(id,p.Situacao.ToString());
        },ct);
    }
}
