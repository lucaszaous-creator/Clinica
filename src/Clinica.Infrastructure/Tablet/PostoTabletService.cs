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
        if(Math.Abs(data.DayNumber-acesso.Hoje.DayNumber)>366)throw ErroFormularioTablet.Criar("Escolha uma data no intervalo de um ano.");
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
        if(a.TipoConteudo!="application/pdf"||bytes is null||bytes.Length<5||!bytes.AsSpan(0,5).SequenceEqual("%PDF-"u8))throw ErroFormularioTablet.Criar("O PDF não está disponível no banco. Consulte o anexo no desktop.");
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
        if(pagina is <0 or >10000) throw ErroFormularioTablet.Criar("Página inválida.");
        var skip=pagina*25;
        var sessoes=await db.Agendamentos.AsNoTracking().Where(a=>a.PacienteId==id).OrderByDescending(a=>a.DataHora).ThenByDescending(a=>a.Id).Skip(skip).Take(26)
            .Select(a=>new {a.Id,a.DataHora,Modalidade=a.ModalidadePrevista.ToString(),Situacao=a.Status.ToString(),Profissional=a.Profissional==null?null:a.Profissional.Nome,
                Proprio=a.ProfissionalId==u.ProfissionalId,a.ChegadaEm,a.InicioAtendimentoEm,a.FimAtendimentoEm,a.AtendimentoId,
                EvolucaoSalva=db.Evolucoes.Any(e=>e.AgendamentoId==a.Id&&e.CanceladaEm==null)}).ToListAsync(ct);
        var evolucoes=await db.Evolucoes.AsNoTracking().Where(e=>e.PacienteId==id&&e.CanceladaEm==null).OrderByDescending(e=>e.Data).ThenByDescending(e=>e.Id).Skip(skip).Take(26)
            .Select(e=>new {e.Id,e.Data,e.QueixaPrincipal,e.HistoriaDoencaAtual,e.ExameFisico,e.HipoteseDiagnostica,e.CidSessao,e.Conduta,e.TextoEvolucao,e.Orientacoes,e.PlanoTerapeutico,e.EvaAntes,e.EvaDepois,e.RetornoSugeridoEm,e.RetornoSugeridoNota,e.Encaminhamento,
                Profissional=e.Profissional==null?e.CriadoPor:e.Profissional.Nome,TemMapa=db.MapasCorporais.Any(m=>m.EvolucaoId==e.Id)}).ToListAsync(ct);
        var documentos=await db.DocumentosClinicos.AsNoTracking().Where(d=>d.PacienteId==id&&d.CanceladoEm==null).OrderByDescending(d=>d.Id).Skip(skip).Take(26)
            .Select(d=>new {d.Id,d.Numero,Tipo=d.Tipo.ToString(),d.Titulo,d.Corpo,d.CorpoFormatado,d.Observacoes,d.ObservacoesFormatadas,d.DiasAfastamento,d.Data,d.AssinadoEm,d.PacienteAssinadoEm,d.AgendamentoId,Proprio=d.ProfissionalId==u.ProfissionalId,
                Itens=d.Itens.OrderBy(i=>i.Ordem).Select(i=>new {i.Descricao,i.DescricaoFormatada,i.Detalhe,i.DetalheFormatado,i.Quantidade})}).ToListAsync(ct);
        var podePrescreverInfusao=u.Pode(Permissao.Prescrever);var podeChecarInfusao=u.Pode(Permissao.ChecarPrescricao);
        var infusoes=await db.PrescricoesInternas.AsNoTracking().Where(x=>x.PacienteId==id).OrderByDescending(x=>x.Id).Skip(skip).Take(26)
            .Select(x=>new {x.Id,x.Numero,x.Data,x.Hora,Situacao=x.DevolvidaEm!=null?"Devolvida":x.OrigemEnfermagem&&x.AssinadaEm==null&&x.Situacao==SituacaoPrescricao.Encerrada?(x.Assinaturas.Any(a=>a.Papel==PapelAssinatura.Executante&&a.ArquivoId!=null&&a.ArquivoRegistroId!=null)?"AguardaMedico":"AguardaEnfermagem"):x.Situacao.ToString(),x.AssinadaEm,x.EncerradaEm,x.CanceladaEm,x.MotivoCancelamento,x.DevolvidaEm,x.MotivoDevolucao,x.RetificaPrescricaoId,RetificadaPorId=x.Retificacao!=null?x.Retificacao.Id:(int?)null,x.OrigemEnfermagem,x.OrientacaoExterna,x.Indicacao,x.IndicacaoFormatada,x.Observacoes,x.ObservacoesFormatadas,x.AgendamentoId,Proprio=x.ProfissionalId==u.ProfissionalId,PodeCancelar=x.CanceladaEm==null&&x.DevolvidaEm==null&&((podePrescreverInfusao&&x.ProfissionalId==u.ProfissionalId)||(x.OrigemEnfermagem&&podeChecarInfusao&&x.RegistradaPorUsuarioId==u.Id))&&(x.OrigemEnfermagem||x.Situacao==SituacaoPrescricao.Assinada&&!x.Itens.Any(i=>i.Checagens.Any())),Assinaturas=x.Assinaturas.Select(a=>new {Papel=a.Papel.ToString(),a.NomeAssinante,a.RegistroConselho,a.AssinadoEm,PrescricaoArquivada=a.ArquivoId!=null,RegistroArquivado=a.ArquivoRegistroId!=null}),
                Itens=x.Itens.OrderBy(i=>i.Ordem).Select(i=>new {i.Descricao,i.DescricaoFormatada,i.Dose,Via=i.Via.ToString(),i.Diluente,i.Volume,i.TempoInfusao,i.HoraPrevista,i.SeNecessario,i.Observacoes,i.ObservacoesFormatadas,i.SuspensoEm,i.MotivoSuspensao})}).ToListAsync(ct);
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
            MaisPorAba=new {Sessoes=sessoes.Count>25,Historico=evolucoes.Count>25,
                Exames=exames.Count>25||avaliacoes.Count>25,Documentos=documentos.Count>25||infusoes.Count>25,
                Enfermagem=enfermagem.Count>25,Anexos=anexos.Count>25},
            Anamnese=DadosAnamnese(anamnese),VersaoAnamnese=Hash(DadosAnamnese(anamnese)),PodeEditarFicha=u.Pode(Permissao.EditarProntuario),PodeAnexar=PodeAnexar(u),PodeRegistrarEnfermagem=u.Pode(Permissao.RegistrarEvolucaoEnfermagem),TiposMedida=MedidaClinicaService.Registraveis.Select(t=>new {t.Codigo,t.Nome,t.Unidade,t.Minimo,t.Maximo,t.RotuloSegundoValor}),Exames=exames.Take(25),Avaliacoes=avaliacoes.Take(25),Campos=campos,Sessoes=sessoes.Take(25),Evolucoes=evolucoes.Take(25),Documentos=documentos.Take(25),Infusoes=infusoes.Take(25),Medidas=medidas.Take(25),Anexos=anexos.Take(25),Problemas=problemas.Select(x=>new {x.Id,Tipo=x.Natureza.ToString(),x.Descricao,Situacao=x.Situacao.ToString(),x.Cid,x.Observacoes,x.Inicio,x.Fim,Versao=Hash(DadosProblema(x))}),
            Enfermagem=enfermagem.Take(25).Select(e=>new {e.Id,e.AgendamentoId,e.FaseAtendimento,PodeVincular=e.AutorUsuarioId==u.Id||u.Perfil==PerfilAcesso.Gerente,e.RegistradoEm,e.Data,e.Texto,e.AutorNome,e.AutorConselho,e.Hora,e.Historico,e.ExameFisico,e.Avaliacao,e.Intercorrencia,e.PressaoSistolica,e.PressaoDiastolica,e.FrequenciaCardiaca,e.FrequenciaRespiratoria,e.Temperatura,e.SaturacaoOxigenio,e.Dor,e.RetificaEvolucaoId,e.MotivoRetificacao,e.CanceladaEm,e.MotivoCancelamento,e.AcessoLocal,e.AcessoCalibre,e.AcessoPuncionadoEm,Diagnosticos=e.Diagnosticos.OrderBy(d=>d.Ordem).Select(d=>new {d.Codigo,d.Titulo,d.RelacionadoA,d.EvidenciadoPor,d.ResultadoEsperado}),Cuidados=e.Cuidados.OrderBy(c=>c.Ordem).Select(c=>new {c.Codigo,c.Descricao,c.Frequencia,c.SeNecessario,Checagens=c.Checagens.OrderBy(x=>x.Id).Select(x=>new {x.Id,x.Data,x.HoraRealizacao,Situacao=x.Situacao.ToString(),x.Justificativa,x.Observacao,x.ExecutanteNome,x.ExecutanteConselho,x.RetificaChecagemId,x.MotivoRetificacao})})})};
    }

    // O recibo avulso não depende de criar uma sessão de atendimento fictícia.
    private async Task<T> Escrever<T>(SessaoTablet s,int paciente,Guid chave,object pedido,string acao,Permissao permissao,Func<UsuarioSistema,Task<T>> executar,CancellationToken ct)
    {
        if(chave==Guid.Empty) throw ErroFormularioTablet.Criar("Atualize antes de enviar.");
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
            if(!Enum.IsDefined(p.Modalidade)||p.Motivo?.Length>1000)throw ErroFormularioTablet.Criar("Confira modalidade e observações.");
            if(u.Perfil==PerfilAcesso.Psicologia&&p.Modalidade!=ModalidadeAtendimento.Consulta)
                throw ErroFormularioTablet.Criar("O perfil Psicologia inicia somente consultas de Psicologia.");
            var inicio=acesso.Hoje.ToDateTime(TimeOnly.MinValue);var fim=inicio.AddDays(1);
            var existentes=await db.Agendamentos.Where(a=>a.PacienteId==paciente&&a.ProfissionalId==u.ProfissionalId&&a.DataHora>=inicio&&a.DataHora<fim&&a.Status==StatusAgendamento.Agendado).Take(2).ToListAsync(ct);
            if(existentes.Count>1)throw new ConflitoClinicoTablet("Há mais de uma sessão aberta hoje. Escolha a sessão na agenda para evitar duplicidade.");
            if(existentes.Count==1)return new ResultadoAvulsoTablet(existentes[0].Id,true);
            var agora=TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(acesso.Agora),TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime;
            var a=await agenda.AgendarAsync(paciente,agora,p.Modalidade,p.Motivo,ct:ct,profissionalId:u.ProfissionalId,encaixe:true,operador:u.Login,
                especialidadeConsultaCodigo:u.Perfil==PerfilAcesso.Psicologia?"Psicologia":p.EspecialidadeConsultaCodigo);
            await agenda.IniciarAtendimentoAsync(a.Id,u.Login,quando:agora,ct:ct);
            return new ResultadoAvulsoTablet(a.Id,false);
        },ct);
    public Task<ResultadoDocumentoTablet> EmitirAsync(SessaoTablet s,int paciente,EmitirDocumentoTablet pedido,CancellationToken ct)
        => Escrever(s,paciente,pedido.Idempotencia,pedido,"TabletDocumentoAvulso",Permissao.Prescrever,
            u=>acesso.EmitirConteudoAsync(u,paciente,null,null,pedido,ct),ct);

    public async Task<object> FilaAsync(SessaoTablet s,int pagina,CancellationToken ct)
    {
        var u=await Autorizar(s,ct);
        var podeExecutar=u.Pode(Permissao.ChecarPrescricao);
        var podePrescrever=u.Pode(Permissao.Prescrever);
        if(!podeExecutar&&!podePrescrever)throw new UnauthorizedAccessException("Esta fila exige permissão de enfermagem ou prescrição.");
        if(pagina is <0 or >10000)throw ErroFormularioTablet.Criar("Página inválida.");
        var pendencias=db.PrescricoesInternas.AsNoTracking().Where(p=>p.CanceladaEm==null&&(
                podeExecutar&&((p.OrigemEnfermagem&&p.RegistradaPorUsuarioId==u.Id&&p.DevolvidaEm!=null&&p.Retificacao==null)
                    ||(p.OrigemEnfermagem&&p.DevolvidaEm==null&&p.RegistradaPorUsuarioId==u.Id&&p.AssinadaEm==null&&p.Situacao==SituacaoPrescricao.Encerrada
                    &&!p.Assinaturas.Any(a=>a.Papel==PapelAssinatura.Executante&&a.ArquivoId!=null&&a.ArquivoRegistroId!=null))
                    ||p.Situacao==SituacaoPrescricao.Assinada
                    ||p.Situacao==SituacaoPrescricao.Encerrada&&p.ExigeAssinaturaEletronicaDaExecucao
                        &&(!p.OrigemEnfermagem||p.RegistradaPorUsuarioId==u.Id)
                        &&!p.Assinaturas.Any(a=>a.Papel==PapelAssinatura.Executante&&a.ArquivoRegistroId!=null))
                ||podePrescrever&&p.ProfissionalId==u.ProfissionalId&&(p.Situacao==SituacaoPrescricao.Rascunho
                    ||p.OrigemEnfermagem&&p.DevolvidaEm==null&&p.AssinadaEm==null&&p.Situacao==SituacaoPrescricao.Encerrada
                        &&p.Assinaturas.Any(a=>a.Papel==PapelAssinatura.Executante&&a.ArquivoId!=null&&a.ArquivoRegistroId!=null))));

        // A contagem é feita antes da paginação. Cada etapa é uma ação diferente:
        // assinatura já colhida mas ainda não arquivada não deve aparecer como
        // "falta assinar"; isso mandaria o profissional repetir uma assinatura.
        var execucaoEnfermagem=await pendencias.CountAsync(p=>p.Situacao==SituacaoPrescricao.Assinada,ct);
        var assinaturaEnfermagem=await pendencias.CountAsync(p=>p.Situacao==SituacaoPrescricao.Encerrada
            &&(p.OrigemEnfermagem&&p.AssinadaEm==null||p.ExigeAssinaturaEletronicaDaExecucao)
            &&!p.Assinaturas.Any(a=>a.Papel==PapelAssinatura.Executante&&a.ArquivoId!=null),ct);
        var assinaturaMedica=await pendencias.CountAsync(p=>p.OrigemEnfermagem&&p.DevolvidaEm==null&&p.AssinadaEm==null
            &&p.Situacao==SituacaoPrescricao.Encerrada
            &&p.Assinaturas.Any(a=>a.Papel==PapelAssinatura.Executante&&a.ArquivoId!=null&&a.ArquivoRegistroId!=null),ct);
        var devolvidasEnfermagem=await pendencias.CountAsync(p=>p.OrigemEnfermagem&&p.DevolvidaEm!=null
            &&p.Retificacao==null&&p.RegistradaPorUsuarioId==u.Id,ct);
        var registroPendente=await pendencias.CountAsync(p=>p.Situacao==SituacaoPrescricao.Encerrada
            &&(p.ExigeAssinaturaEletronicaDaExecucao||p.OrigemEnfermagem&&p.AssinadaEm==null)
            &&p.Assinaturas.Any(a=>a.Papel==PapelAssinatura.Executante&&a.ArquivoId!=null&&a.ArquivoRegistroId==null),ct);
        var rascunhosMedicos=await pendencias.CountAsync(p=>p.Situacao==SituacaoPrescricao.Rascunho,ct);
        var total=await pendencias.CountAsync(ct);
        var folhas=await pendencias.Include(p=>p.Paciente).Include(p=>p.Itens).ThenInclude(i=>i.Checagens).Include(p=>p.Assinaturas)
            .OrderBy(p=>p.Data).ThenBy(p=>p.Id).Skip(pagina*50).Take(51).ToListAsync(ct);
        return new {Pagina=pagina,Mais=folhas.Count>50,Total=total,
            Resumo=new {ExecucaoEnfermagem=execucaoEnfermagem,AssinaturaEnfermagem=assinaturaEnfermagem,
                AssinaturaMedica=assinaturaMedica,DevolvidasEnfermagem=devolvidasEnfermagem,
                RegistroPendente=registroPendente,RascunhosMedicos=rascunhosMedicos},
            Itens=folhas.Take(50).Select(p=>new {p.Id,p.Numero,p.Data,p.Hora,p.PacienteId,Paciente=p.Paciente!.Nome,Nascimento=p.Paciente.DataNascimento,
                Situacao=p.DevolvidaEm!=null?"Devolvida":p.OrigemEnfermagem&&p.AssinadaEm==null&&p.Situacao==SituacaoPrescricao.Encerrada
                    ?p.Assinaturas.Any(a=>a.Papel==PapelAssinatura.Executante&&a.ArquivoId!=null&&a.ArquivoRegistroId!=null)?"AguardaMedico":"AguardaEnfermagem"
                    :p.Situacao.ToString(),AcaoPendente=AcaoPendenteInfusao(p),p.OrigemEnfermagem,
                p.MotivoDevolucao,p.DevolvidaEm,p.RetificaPrescricaoId,
                p.Pendentes,p.ExigeAssinaturaEletronicaDaExecucao,RegistroSemAssinatura=p.AssinaturaDaExecucao is {} a&&a.ArquivoRegistroId is null})};
    }

    private static string AcaoPendenteInfusao(PrescricaoInterna p)
    {
        if(p.DevolvidaEm!=null)return "revisarDevolucao";
        if(p.Situacao==SituacaoPrescricao.Rascunho)return "revisarRascunho";
        if(p.Situacao==SituacaoPrescricao.Assinada)return "executar";
        var assinatura=p.AssinaturaDaExecucao;
        if(assinatura?.ArquivoId is null)return "assinarEnfermagem";
        if(assinatura.ArquivoRegistroId is null)return "regularizarRegistro";
        return "assinarMedico";
    }
    public static string Versao(PrescricaoInterna p)=>ContratoTablet.Hash(ContratoTablet.Serializar(new {p.Id,p.Situacao,p.Data,p.Hora,p.AtualizadoEm,p.EncerradaEm,p.DevolvidaEm,p.MotivoDevolucao,p.RetificaPrescricaoId,p.OrigemEnfermagem,p.OrientacaoExterna,p.RegistradaPorUsuarioId,
        Itens=p.Itens.OrderBy(i=>i.Id).Select(i=>new {i.Id,i.Descricao,i.DescricaoFormatada,i.Dose,i.Via,i.Diluente,i.Volume,i.TempoInfusao,i.HoraPrevista,i.SeNecessario,i.Observacoes,i.ObservacoesFormatadas,i.SuspensoEm,i.MotivoSuspensao,Checagens=i.Checagens.OrderBy(c=>c.Id).Select(c=>new {c.Id,c.Situacao,c.DataRealizacao,Hora=c.HoraRealizacao,c.Justificativa,c.RetificaChecagemId})})}));
    public async Task<object> InfusaoAsync(SessaoTablet s,int id,CancellationToken ct)
    {
        var u=await Autorizar(s,ct);
        var p=await repo.ObterPrescricaoInternaAsync(id,ct)??throw new RecursoClinicoIndisponivel();
        if(!u.Pode(Permissao.ChecarPrescricao) && !(u.Pode(Permissao.Prescrever)&&p.ProfissionalId==u.ProfissionalId))
            throw new RecursoClinicoIndisponivel();
        if(p.CanceladaEm!=null||p.Situacao==SituacaoPrescricao.Rascunho)throw new RecursoClinicoIndisponivel();
        var alertas=await conferencia.ContextoAsync(p.PacienteId,ct);await Auditar(u,p.PacienteId,"TabletInfusaoConsultada",ct);await db.SaveChangesAsync(ct);
        return new {p.Id,p.Numero,p.Data,p.Hora,p.PacienteId,Paciente=p.Paciente!.Nome,Nascimento=p.Paciente.DataNascimento,p.ProfissionalId,p.AgendamentoId,p.Indicacao,p.IndicacaoFormatada,p.Observacoes,p.ObservacoesFormatadas,Situacao=p.DevolvidaEm!=null?"Devolvida":p.OrigemEnfermagem&&p.AssinadaEm==null&&p.Situacao==SituacaoPrescricao.Encerrada?(p.AssinaturaDaExecucao?.ArquivoId!=null&&p.AssinaturaDaExecucao.ArquivoRegistroId!=null?"AguardaMedico":"AguardaEnfermagem"):p.Situacao.ToString(),Versao=Versao(p),p.ExecucaoCompleta,p.OrigemEnfermagem,p.OrientacaoExterna,p.DevolvidaEm,p.MotivoDevolucao,p.RetificaPrescricaoId,RetificadaPorId=p.Retificacao?.Id,PodeRetificar=p.DevolvidaEm!=null&&p.Retificacao is null&&p.RegistradaPorUsuarioId==u.Id&&u.Pode(Permissao.ChecarPrescricao),PodeAssinarExecucao=p.DevolvidaEm==null&&(!p.OrigemEnfermagem||p.RegistradaPorUsuarioId==u.Id),PodeValidarMedico=p.AguardaValidacaoMedica&&p.ProfissionalId==u.ProfissionalId&&p.AssinaturaDaExecucao?.ArquivoId!=null&&p.AssinaturaDaExecucao.ArquivoRegistroId!=null,PodeDevolver=p.AguardaValidacaoMedica&&p.ProfissionalId==u.ProfissionalId&&p.AssinaturaDaExecucao?.ArquivoRegistroId!=null,PodeCorrigirHorarios=p.OrigemEnfermagem&&p.RegistradaPorUsuarioId==u.Id&&p.AssinadaEm==null&&p.Assinaturas.Count==0,PodeCancelar=p.DevolvidaEm==null&&(u.Pode(Permissao.Prescrever)&&p.ProfissionalId==u.ProfissionalId||p.OrigemEnfermagem&&u.Pode(Permissao.ChecarPrescricao)&&p.RegistradaPorUsuarioId==u.Id)&&(p.OrigemEnfermagem||((p.Situacao is SituacaoPrescricao.Rascunho or SituacaoPrescricao.Assinada)&&!p.Itens.Any(i=>i.Checagens.Count>0))),p.ExigeAssinaturaEletronicaDaExecucao,ExecucaoAssinadaEletronicamente=p.AssinaturaDaExecucao?.ArquivoId!=null,RegistroExecucaoArquivado=p.AssinaturaDaExecucao?.ArquivoRegistroId!=null,Assinaturas=p.Assinaturas.Select(a=>new {Papel=a.Papel.ToString(),a.NomeAssinante,a.RegistroConselho,a.AssinadoEm,PrescricaoArquivada=a.ArquivoId!=null,RegistroArquivado=a.ArquivoRegistroId!=null}),
            Alergias=alertas.Alergias.Select(a=>a.Descricao),Itens=p.Itens.OrderBy(i=>i.Ordem).Select(i=>new {i.Id,i.Descricao,i.DescricaoFormatada,i.Dose,Via=i.Via.ToString(),i.Diluente,i.Volume,i.TempoInfusao,i.HoraPrevista,i.SeNecessario,i.Observacoes,i.ObservacoesFormatadas,i.SuspensoEm,i.MotivoSuspensao,
                Situacao=i.Situacao.ToString(),Checagem=i.ChecagemVigente is {} c?new {c.Id,Situacao=c.Situacao.ToString(),Data=c.DataRealizacao??DateOnly.FromDateTime(c.RegistradoEm),Hora=c.HoraRealizacao,c.Justificativa,c.ExecutanteNome,c.ExecutanteConselho}:null})};
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
            if(!Enum.IsDefined(pedido.Situacao)||pedido.Justificativa?.Length>1000||pedido.AlergiaObservada?.Length>500||pedido.MotivoRetificacao?.Length>1000)throw ErroFormularioTablet.Criar("Confira os dados da checagem.");
            var autor=new IdentificacaoExecutante(u.Id,u.Nome,u.Profissional!.RegistroConselho);autor.Exigir("registrar a execução");
            if(string.IsNullOrWhiteSpace(pedido.MotivoRetificacao))await checagem.ChecarAsync(pedido.ItemId,pedido.Situacao,pedido.Hora,autor,pedido.Justificativa,pedido.AlergiaObservada,pedido.ConfirmouAlergia,ct,pedido.Data);
            else await checagem.RetificarAsync(pedido.ItemId,pedido.Situacao,pedido.Hora,autor,pedido.MotivoRetificacao,pedido.Justificativa,pedido.ConfirmouAlergia,ct,pedido.Data);
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

    public async Task<ResultadoEnfermagemTablet> CancelarInfusaoAsync(SessaoTablet s,int id,CancelarInfusaoTablet pedido,CancellationToken ct)
    {
        await Autorizar(s,ct);
        var paciente=await db.PrescricoesInternas.Where(p=>p.Id==id).Select(p=>(int?)p.PacienteId).SingleOrDefaultAsync(ct)??throw new RecursoClinicoIndisponivel();
        return await Escrever(s,paciente,pedido.Idempotencia,new{id,pedido},"TabletInfusaoCancelada",Permissao.Nenhuma,async u=>
        {
            var p=await repo.ObterPrescricaoInternaAsync(id,ct)??throw new RecursoClinicoIndisponivel();
            if(Versao(p)!=pedido.Versao)throw new ConflitoClinicoTablet("A infusão mudou. Atualize antes de cancelar.");
            if(pedido.Motivo?.Trim().Length is not (>= 5 and <= 500))throw ErroFormularioTablet.Criar("Informe o motivo do cancelamento (5 a 500 caracteres).");
            var responsavel=u.Pode(Permissao.Prescrever)&&p.ProfissionalId==u.ProfissionalId;
            var executante=p.OrigemEnfermagem&&u.Pode(Permissao.ChecarPrescricao)&&p.RegistradaPorUsuarioId==u.Id;
            if(!responsavel&&!executante)throw new RecursoClinicoIndisponivel();
            await new PrescricaoInternaService(repo,conferencia).CancelarAsync(id,pedido.Motivo,u.Login,ct);
            return new ResultadoEnfermagemTablet(id,"Cancelada");
        },ct);
    }

    public async Task<ResultadoEnfermagemTablet> DevolverInfusaoAsync(SessaoTablet s,int id,DevolverInfusaoTablet pedido,CancellationToken ct)
    {
        await Autorizar(s,ct,Permissao.Prescrever);
        var paciente=await db.PrescricoesInternas.Where(p=>p.Id==id).Select(p=>(int?)p.PacienteId).SingleOrDefaultAsync(ct)
            ??throw new RecursoClinicoIndisponivel();
        return await Escrever(s,paciente,pedido.Idempotencia,new{id,pedido},"TabletInfusaoDevolvida",Permissao.Prescrever,async u=>
        {
            var p=await repo.ObterPrescricaoInternaAsync(id,ct)??throw new RecursoClinicoIndisponivel();
            if(Versao(p)!=pedido.Versao)throw new ConflitoClinicoTablet("A infusão mudou. Atualize antes de devolver.");
            await new PrescricaoInternaService(repo,conferencia).DevolverInfusaoExternaAsync(id,u.Id,pedido.Motivo,ct);
            return new ResultadoEnfermagemTablet(id,"Devolvida");
        },ct);
    }

    public async Task<ResultadoEnfermagemTablet> CorrigirHorariosInfusaoAsync(SessaoTablet s,int id,CorrigirHorariosInfusaoTablet pedido,CancellationToken ct)
    {
        await Autorizar(s,ct,Permissao.ChecarPrescricao);
        var paciente=await db.PrescricoesInternas.Where(p=>p.Id==id).Select(p=>(int?)p.PacienteId).SingleOrDefaultAsync(ct)??throw new RecursoClinicoIndisponivel();
        return await Escrever(s,paciente,pedido.Idempotencia,new{id,pedido},"TabletInfusaoHorariosCorrigidos",Permissao.ChecarPrescricao,async u=>
        {
            var p=await repo.ObterPrescricaoInternaAsync(id,ct)??throw new RecursoClinicoIndisponivel();
            if(Versao(p)!=pedido.Versao)throw new ConflitoClinicoTablet("A infusão mudou. Atualize antes de corrigir os horários.");
            await new PrescricaoInternaService(repo,conferencia).CorrigirHorariosInfusaoExternaAsync(id,u.Id,
                pedido.DataPrescricao,pedido.HoraPrescricao,pedido.DataExecucao,pedido.HoraExecucao,pedido.Motivo,ct);
            return new ResultadoEnfermagemTablet(id,p.Situacao.ToString());
        },ct);
    }
}
