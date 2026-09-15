using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Clinica.Domain.Prontuario;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure.Tablet;

public sealed record OpcoesTablet(int[] Modelos);
public sealed class PortalTabletService(ClinicaDbContext db, IClinicaRepositorio repo,
    DocumentoClinicoService documentos, AssinaturaDoPacienteService assinaturas,
    DocumentosClinicosPdfService pdf, ParametrosService parametros, OpcoesTablet opcoes, TimeProvider tempo)
{
    public long Agora => tempo.GetUtcNow().ToUnixTimeMilliseconds();
    public DateOnly Hoje => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(tempo.GetUtcNow(),
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);
    public static string Token() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static string Operador(UsuarioSistema u) => $"{u.Nome} ({u.Login})";

    public async Task<(string Token, SessaoTablet Sessao)> EntrarAsync(UsuarioSistema u, string dispositivo, string? anterior, CancellationToken ct)
    {
        if (!PodeColher(u)) throw new UnauthorizedAccessException();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (anterior is not null && await db.SessoesTablet.FindAsync([ContratoTablet.Hash(anterior)], ct) is { } velha)
        {
            await EncerrarPreparadasAsync(velha.Id, "encerrado", "Equipe retomou o tablet", ct);
            velha.Modo = "revogada"; velha.ExpiraEm = Agora;
        }
        var token = Token();
        var sessao = new SessaoTablet { Id=ContratoTablet.Hash(token), UsuarioId=u.Id,
            CredencialVersao=ContratoTablet.Hash(u.SenhaHash), Dispositivo=dispositivo, ExpiraEm=Agora+7_200_000 };
        db.SessoesTablet.Add(sessao);
        await Auditar("TabletEntrada", null, Operador(u), "Acesso ao portal de coleta", ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (token,sessao);
    }

    public static bool PodeColher(UsuarioSistema u) => u.Ativo && !u.DeveTrocarSenha
        && u.Pode(Permissao.ColherAssinaturaPaciente) && u.Pode(Permissao.VerAgenda);

    public async Task<SessaoTablet> AutorizarAsync(string? token, string dispositivo, bool equipe, CancellationToken ct)
    {
        if (token?.Length != 64) throw new UnauthorizedAccessException();
        var id = ContratoTablet.Hash(token);
        var s = await db.SessoesTablet.Include(x=>x.Usuario).SingleOrDefaultAsync(x=>x.Id==id,ct);
        if (s?.Usuario is not { } u || s.ExpiraEm<=Agora || s.Modo=="revogada" || s.Dispositivo!=dispositivo
            || !PodeColher(u) || u.Travado(DateTime.Now) || s.CredencialVersao!=ContratoTablet.Hash(u.SenhaHash))
            throw new UnauthorizedAccessException();
        if (equipe && s.Modo!="equipe") throw new AcessoTabletBloqueado();
        return s;
    }

    public async Task<object> DiaAsync(CancellationToken ct)
    {
        var de=Hoje.ToDateTime(TimeOnly.MinValue); var ate=de.AddDays(1);
        var agenda=await db.Agendamentos.AsNoTracking().Where(a=>a.DataHora>=de && a.DataHora<ate
            && a.Status!=StatusAgendamento.Cancelado && a.Status!=StatusAgendamento.Faltou)
            .OrderBy(a=>a.DataHora).Take(300).Select(a=>new {
                a.Id,a.PacienteId,Nome=a.Paciente!.Nome,Nascimento=a.Paciente.DataNascimento,
                Horario=a.DataHora,Procedimento=a.ModalidadePrevista.ToString() }).ToListAsync(ct);
        return new { Data=Hoje,Pacientes=agenda };
    }

    public async Task<object> BuscarAsync(string? busca, CancellationToken ct)
    {
        var q=busca?.Trim();
        if (q?.Length is not (>=3 and <=80)) return Array.Empty<object>();
        return await db.Pacientes.AsNoTracking().Where(p=>p.Nome.Contains(q))
            .OrderBy(p=>p.Nome).Take(20).Select(p=>new {p.Id,p.Nome,Nascimento=p.DataNascimento}).ToListAsync(ct);
    }

    private Task<List<ModeloDocumento>> ModelosAsync(CancellationToken ct) => db.ModelosDocumento.AsNoTracking()
        .Include(m=>m.Itens).Where(m=>opcoes.Modelos.Contains(m.Id) && m.Ativo
            && m.Tipo==TipoDocumentoClinico.TermoProcedimento).OrderBy(m=>m.Ordem).ThenBy(m=>m.Id).ToListAsync(ct);

    private async Task<bool> DiarioAsync(int modelo, CancellationToken ct) => await db.ExigenciasTermo.AsNoTracking()
        .AnyAsync(e=>e.ModeloDocumentoId==modelo && e.Ativa && e.SoValeNoDiaDoProcedimento,ct);
    private Task<bool> CobertoAsync(int paciente,int modelo,bool diario,CancellationToken ct) => db.DocumentosClinicos
        .AnyAsync(d=>d.PacienteId==paciente && d.ModeloOrigemId==modelo && d.PacienteAssinadoEm!=null
            && d.CanceladoEm==null && (!diario || d.Data==Hoje),ct);

    public async Task<object> PacienteAsync(int id, string operadora, CancellationToken ct)
    {
        var p=await db.Pacientes.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)
            ?? throw new InvalidOperationException("Paciente não encontrado.");
        var modelos=new List<object>();
        foreach (var m in await ModelosAsync(ct))
        {
            var diario=await DiarioAsync(m.Id,ct);
            modelos.Add(new {m.Id,m.Nome,m.Titulo,Diario=diario,Coberto=await CobertoAsync(id,m.Id,diario,ct)});
        }
        var historico=await db.DocumentosClinicos.AsNoTracking().Where(d=>d.PacienteId==id
            && d.Tipo==TipoDocumentoClinico.TermoProcedimento).OrderByDescending(d=>d.Id).Take(40)
            .Select(d=>new {d.Id,d.Numero,d.Titulo,d.Data,d.PacienteAssinadoEm,d.CanceladoEm,
                d.PacienteRecusouEm,d.MotivoRecusaPaciente,
                Coleta=db.ColetasTablet.Where(c=>c.DocumentoId==d.Id).Select(c=>new {c.Id,c.Estado}).FirstOrDefault(),
                Arquivado=db.ViasAssinadasPaciente.Any(v=>v.DocumentoId==d.Id),
                Alerta=d.Itens.Any(i=>i.Codigo==RespostaDeclaracao.CodigoAlergiasTablet
                    ? i.Quantidade=="Sim" : i.Quantidade=="Não")}).ToListAsync(ct);
        await Auditar("TabletConsultaTermos",id,operadora,"Consulta de termos do paciente",ct);
        await db.SaveChangesAsync(ct);
        return new { p.Id,p.Nome,Nascimento=p.DataNascimento,Modelos=modelos,Documentos=historico };
    }

    public async Task PrepararAsync(SessaoTablet s, PrepararTablet pedido, CancellationToken ct)
    {
        if(s.Modo!="equipe") throw new AcessoTabletBloqueado();
        if(pedido.Modelos is null || pedido.Modelos.Length is <1 or >2 || pedido.Modelos.Distinct().Count()!=pedido.Modelos.Length)
            throw new InvalidOperationException("Selecione um ou dois termos pendentes.");
        var p=await db.Pacientes.SingleOrDefaultAsync(x=>x.Id==pedido.PacienteId,ct)
            ?? throw new InvalidOperationException("Paciente não encontrado.");
        if (p.DataNascimento is null || p.DataNascimento!=pedido.Nascimento)
            throw new InvalidOperationException("A data de nascimento não confere. Confira a identidade e o cadastro antes da coleta.");
        if (string.IsNullOrWhiteSpace(pedido.IdentidadeConferida) || pedido.IdentidadeConferida.Length>150)
            throw new InvalidOperationException("Registre qual documento foi conferido presencialmente.");
        var modelos=await ModelosAsync(ct);
        if(pedido.Modelos.Any(id=>modelos.All(m=>m.Id!=id))) throw new InvalidOperationException("Modelo indisponível para este portal.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        foreach(var m in modelos.Where(m=>pedido.Modelos.Contains(m.Id)))
        {
            var diario=await DiarioAsync(m.Id,ct);
            if(await CobertoAsync(p.Id,m.Id,diario,ct)) throw new InvalidOperationException("Este termo já está assinado e vigente no prontuário.");
            var chave=$"{p.Id}:{m.Id}:{(diario ? Hoje.ToString("yyyy-MM-dd") : "continuo")}";
            if(await db.ColetasTablet.AnyAsync(c=>c.ChaveAtiva==chave,ct))
                throw new InvalidOperationException("Já existe coleta deste termo em andamento. Conclua ou encerre a coleta anterior.");
            var itens=m.Itens.OrderBy(i=>i.Ordem).Select(i=>new ItemDocumento
                {Descricao=i.Descricao,Detalhe=i.Detalhe}).ToList();
            itens.Add(new ItemDocumento { Codigo=RespostaDeclaracao.CodigoAlergiasTablet,
                Descricao="Você tem alguma alergia conhecida?",
                Detalhe="Responda mesmo que não tenha alergias. Se tiver, informe à equipe antes do procedimento." });
            var doc=await documentos.EmitirAsync(new DocumentoClinico {PacienteId=p.Id,
                Tipo=TipoDocumentoClinico.TermoProcedimento,ModeloOrigemId=m.Id,Data=Hoje,
                Titulo=m.Titulo,Corpo=m.Corpo,Itens=itens},Operador(s.Usuario!),ct);
            doc.Paciente=p;
            var conteudo=ContratoTablet.Serializar(ContratoTablet.Fotografar(doc));
            db.ColetasTablet.Add(new ColetaTablet {SessaoId=s.Id,DocumentoId=doc.Id,PacienteId=p.Id,
                Operadora=Operador(s.Usuario!),IdentidadeConferida=pedido.IdentidadeConferida.Trim(),
                ConteudoJson=conteudo,ConteudoHash=ContratoTablet.Hash(conteudo),ChaveAtiva=chave,
                PreparadoEm=Agora,ExpiraEm=Agora+3_600_000 });
        }
        s.Modo="paciente"; s.ExpiraEm=Agora+3_600_000;
        await Auditar("TabletEntregue",p.Id,Operador(s.Usuario!),"Acesso restrito aos documentos preparados; identidade conferida",ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<object> ColetasAsync(SessaoTablet s,CancellationToken ct)
    {
        if(s.Modo!="paciente") throw new AcessoTabletBloqueado();
        var coletas=await db.ColetasTablet.AsNoTracking().Where(c=>c.SessaoId==s.Id)
            .OrderBy(c=>c.PreparadoEm).ThenBy(c=>c.DocumentoId)
            .Select(c=>new {c.Id,c.DocumentoId,c.Estado,c.ConteudoHash,c.ExpiraEm,c.ConteudoJson,c.Falha}).ToListAsync(ct);
        // Não expor documento de identidade completo no modo paciente.
        return coletas.Select(c=>new { c.Id,c.DocumentoId,c.Estado,c.ConteudoHash,c.ExpiraEm,
            Documento=c.Estado=="preparado" ? JsonSerializer.Deserialize<DocumentoTablet>(c.ConteudoJson,ContratoTablet.Json)! with {Identificacao=null} : null,
            PrecisaEquipe=c.Estado=="falha" });
    }

    public async Task ReceberAsync(SessaoTablet s,Guid coletaId,EnviarRubrica envio,CancellationToken ct)
    {
        if(s.Modo!="paciente") throw new AcessoTabletBloqueado();
        var c=await db.ColetasTablet.SingleOrDefaultAsync(c=>c.Id==coletaId && c.SessaoId==s.Id,ct)
            ?? throw new AcessoTabletBloqueado();
        if(envio.ConteudoHash!=c.ConteudoHash) throw new InvalidOperationException("O documento apresentado mudou. Chame a enfermeira.");
        var d=JsonSerializer.Deserialize<DocumentoTablet>(c.ConteudoJson,ContratoTablet.Json)!;
        var respostas=ContratoTablet.ValidarRespostas(d,envio);
        byte[] png;
        try { png=Convert.FromBase64String(envio.TracoPng ?? ""); }
        catch(FormatException) { throw new InvalidOperationException("Rubrica inválida. Desenhe novamente."); }
        TracoTablet.Validar(png);
        var json=ContratoTablet.Serializar(respostas);
        var hash=ContratoTablet.Hash(c.ConteudoHash+"\n"+json+"\n"+ContratoTablet.Hash(png));
        if(c.Idempotencia==envio.Idempotencia && c.SubmissaoHash==hash) return;
        if(c.Estado!="preparado" || c.ExpiraEm<=Agora)
            throw new InvalidOperationException("Esta coleta já foi enviada ou encerrou. Chame a enfermeira.");
        var atual=await repo.ObterDocumentoAsync(c.DocumentoId,ct) ?? throw new InvalidOperationException("Documento indisponível.");
        if(atual.CanceladoEm!=null || atual.PacienteAssinadoEm!=null
            || ContratoTablet.Hash(ContratoTablet.Serializar(ContratoTablet.Fotografar(atual)))!=c.ConteudoHash)
            throw new InvalidOperationException("O documento foi alterado. Chame a enfermeira para preparar uma nova coleta.");
        c.SubmissaoJson=json; c.SubmissaoHash=hash; c.TracoPng=png; c.Idempotencia=envio.Idempotencia;
        c.RecebidoEm=Agora; c.Estado="recebido";
        // Concorre também com a reentrada da equipe: a sessão lida antes da entrega
        // ou revogação não pode autorizar uma escrita depois dela.
        db.Entry(s).Property(x=>x.Versao).IsModified=true;
        await Auditar("TabletRubricaRecebida",c.PacienteId,c.Operadora,$"Coleta {c.Id}; documento {c.DocumentoId}; aguardando arquivamento",ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task EncerrarAsync(SessaoTablet s,bool recusa,string? motivo,CancellationToken ct)
    {
        if(s.Modo!="paciente") throw new AcessoTabletBloqueado();
        if(recusa && (string.IsNullOrWhiteSpace(motivo) || motivo.Length>500))
            throw new InvalidOperationException("Informe o motivo da recusa em até 500 caracteres.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        foreach(var c in await db.ColetasTablet.Where(c=>c.SessaoId==s.Id && c.Estado=="preparado").ToListAsync(ct))
        {
            c.Estado=recusa ? "recusado" : "encerrado"; c.ChaveAtiva=null;
            // Não registra a enfermeira como signatária do termo.
            var d=await repo.ObterDocumentoAsync(c.DocumentoId,ct);
            if(d is not null && d.PacienteAssinadoEm==null && d.CanceladoEm==null)
            {
                if(recusa) await assinaturas.RecusarAsync(d.Id,motivo!,c.Operadora,ct);
                else { d.CanceladoEm=DateTime.Now; d.MotivoCancelamento="Coleta encerrada sem assinatura"; }
            }
            await Auditar(recusa ? "TabletRecusa" : "TabletEncerrado",c.PacienteId,c.Operadora,
                $"Documento {c.DocumentoId}: "+(recusa ? motivo : "Coleta encerrada"),ct);
        }
        // Submissões recebidas continuam finalizando; nunca apagar após perder a rede.
        s.Modo="encerrada";
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    private async Task EncerrarPreparadasAsync(string sessaoId,string estado,string motivo,CancellationToken ct)
    {
        foreach(var c in await db.ColetasTablet.Where(c=>c.SessaoId==sessaoId && c.Estado=="preparado").ToListAsync(ct))
        {
            c.Estado=estado; c.ChaveAtiva=null;
            var d=await repo.ObterDocumentoAsync(c.DocumentoId,ct);
            if(d is not null && d.PacienteAssinadoEm==null && d.CanceladoEm==null && !d.PacienteRecusou)
            { d.CanceladoEm=DateTime.Now; d.MotivoCancelamento=motivo; }
            await Auditar("TabletColetaEncerrada",c.PacienteId,c.Operadora,$"Documento {c.DocumentoId}; {motivo}",ct);
        }
    }

    public async Task ExpirarAsync(CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var ids=await db.ColetasTablet.Where(c=>c.Estado=="preparado" && c.ExpiraEm<=Agora)
            .Select(c=>c.SessaoId).Distinct().Take(20).ToListAsync(ct);
        foreach(var id in ids) await EncerrarPreparadasAsync(id,"expirado","Prazo de leitura encerrado sem assinatura",ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task RetomarAsync(Guid id,string operadora,CancellationToken ct)
    {
        var c=await db.ColetasTablet.SingleOrDefaultAsync(c=>c.Id==id,ct)
            ?? throw new InvalidOperationException("Coleta não encontrada.");
        if(c.Estado!="falha" || c.SubmissaoJson is null || c.TracoPng is null)
            throw new InvalidOperationException("Somente um arquivamento pendente pode ser retomado.");
        c.Estado="recebido"; c.Tentativas=0; c.Falha=null;
        await Auditar("TabletArquivamentoRetomado",c.PacienteId,operadora,$"Coleta {id}; mesma rubrica recebida",ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task FinalizarAsync(Guid id,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var c=await db.ColetasTablet.SingleOrDefaultAsync(x=>x.Id==id,ct);
        if(c is null || c.Estado!="recebido") return;
        c.Estado="finalizando";
        await db.SaveChangesAsync(ct); // adquire a linha; rollback mantém "recebido"
        var documento=await repo.ObterDocumentoAsync(c.DocumentoId,ct) ?? throw new InvalidOperationException("DOCUMENTO_AUSENTE");
        if(ContratoTablet.Hash(ContratoTablet.Serializar(ContratoTablet.Fotografar(documento)))!=c.ConteudoHash)
            throw new InvalidOperationException("CONTEUDO_ALTERADO");
        var resposta=JsonSerializer.Deserialize<RespostasTablet>(c.SubmissaoJson!,ContratoTablet.Json)!;
        var tamanho=TracoTablet.Validar(c.TracoPng!);
        var alergia=documento.Itens.Single(i=>i.Codigo==RespostaDeclaracao.CodigoAlergiasTablet);
        if(resposta.AlergiasDetalhes is { } relato) alergia.Detalhe="Relato do paciente: "+relato;
        var assinado=await assinaturas.ColherAsync(c.DocumentoId,c.TracoPng!,tamanho.Largura,tamanho.Altura,
            resposta.Respostas,c.IdentidadeConferida,c.Operadora,ct:ct);
        // A hora da assinatura é a recepção durável, não a hora de uma eventual retomada.
        assinado.PacienteAssinadoEm=TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(c.RecebidoEm!.Value),
            TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime;
        var bytes=pdf.Gerar(assinado,await parametros.ObterPrestadorAsync(ct),tracoPaciente:c.TracoPng,somentePaciente:true);
        if(bytes.Length is <100 or >10_000_000) throw new InvalidOperationException("PDF_FORA_DO_LIMITE");
        var hash=ContratoTablet.Hash(bytes);
        var evidencia=ContratoTablet.Serializar(new { Versao=1,c.Id,c.DocumentoId,c.PacienteId,
            c.Operadora,c.IdentidadeConferida,c.ConteudoJson,c.ConteudoHash,c.SubmissaoJson,c.SubmissaoHash,
            c.Idempotencia,c.PreparadoEm,c.RecebidoEm,TracoSha256=ContratoTablet.Hash(c.TracoPng!),
            PdfSha256=hash,Gerador=typeof(DocumentosClinicosPdfService).Assembly.GetName().Version?.ToString() });
        db.ViasAssinadasPaciente.Add(new ViaAssinadaPaciente {DocumentoId=c.DocumentoId,ColetaId=c.Id,
            Conteudo=bytes,Sha256=hash,EvidenciaJson=evidencia,EvidenciaSha256=ContratoTablet.Hash(evidencia),ArquivadoEm=Agora});
        c.Estado="arquivado"; c.FinalizadoEm=Agora; c.Falha=null; c.ChaveAtiva=null;
        await Auditar("TabletViaArquivada",c.PacienteId,c.Operadora,$"Documento {c.DocumentoId}; SHA-256 {hash}",ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task<byte[]> AbrirViaAsync(int documentoId,string operadora,CancellationToken ct)
    {
        var via=await db.ViasAssinadasPaciente.AsNoTracking().SingleOrDefaultAsync(v=>v.DocumentoId==documentoId,ct)
            ?? throw new InvalidOperationException("A via ainda não foi arquivada pelo portal.");
        if(ContratoTablet.Hash(via.Conteudo)!=via.Sha256 || ContratoTablet.Hash(via.EvidenciaJson)!=via.EvidenciaSha256)
            throw new InvalidOperationException("A integridade da via precisa de conferência. Acione o suporte.");
        var paciente=await db.DocumentosClinicos.Where(d=>d.Id==documentoId).Select(d=>d.PacienteId).SingleAsync(ct);
        await Auditar("TabletViaConsultada",paciente,operadora,$"Documento {documentoId}",ct);
        await db.SaveChangesAsync(ct);
        return via.Conteudo;
    }

    private Task Auditar(string acao,int? paciente,string operadora,string detalhe,CancellationToken ct)
        => repo.RegistrarAuditoriaAsync(new EventoAuditoria {Acao=acao,PacienteId=paciente,Operador=operadora,Detalhe=detalhe},ct);
}

public sealed class AcessoTabletBloqueado : Exception;
