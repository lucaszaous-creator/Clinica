using System.Data;
using Clinica.Application.Abstracoes;
using Clinica.Application.Assinatura;
using Clinica.Application.Assinatura.SafeID;
using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Infrastructure.Tablet;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Assinaturas.Api;

public sealed class SafeIdTabletService(IConfiguration configuration, AtendimentoTabletService acesso,
    AutorizacoesSafeIdTablet autorizacoes, IClinicaRepositorio repo, ClinicaDbContext db,
    PrescricaoInternaService prescricoes, AssinaturaDeDocumentoClinicoService assinadorDocumento,
    AssinaturaDePrescricaoService assinadorInfusao, ILogger<SafeIdTabletService> logger,
    RegistroAssinaturaTablet registro, IHttpClientFactory clientes)
{
    public bool Habilitado => configuration.GetValue<bool>("Portal:SafeId:Habilitado")
        && !configuration.GetValue<bool>("Portal:Demo");
    public bool ConclusaoAutomatica => Habilitado && configuration.GetValue<bool>("Portal:SafeId:ConclusaoAutomatica");
    private (OpcoesSafeID Opcoes, Uri Retorno) Configuracao()
    {
        if(!Habilitado) throw ErroFormularioTablet.Criar("A assinatura SafeID no tablet ainda não foi habilitada pela clínica.");
        var id=configuration["Portal:SafeId:ClientId"]; var segredo=configuration["Portal:SafeId:ClientSecret"];
        if(string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(segredo)
            || !Uri.TryCreate(configuration["Portal:SafeId:Retorno"],UriKind.Absolute,out var retorno)
            || retorno.Scheme!="https" || (retorno.Host!="portal.clinicasemdormacae.com.br" && retorno.Host!="homologacao.clinicasemdormacae.com.br")
            || retorno.AbsolutePath!="/safeid/retorno" || retorno.UserInfo!="" || retorno.Query!="" || retorno.Fragment!="" || !retorno.IsDefaultPort)
            throw ErroFormularioTablet.Criar("A clínica precisa configurar o retorno HTTPS do SafeID para o tablet.");
        return (new(id,segredo,[retorno],configuration.GetValue<bool>("Portal:SafeId:Homologacao") ? OpcoesSafeID.BaseHomologacao : OpcoesSafeID.BasePadrao),retorno);
    }
    private async Task<string> Conferir(string tipo,int id,bool confirmou,CancellationToken ct)
    {
        var prestador=await new ParametrosService(repo).ObterPrestadorAsync(ct);
        if(tipo=="execucao")
        {
            var p=await repo.ObterPrescricaoInternaAsync(id,ct)??throw new RecursoClinicoIndisponivel();
            if(!p.AguardaAssinaturaDaExecucao || !p.ExecucaoCompleta || !p.OrigemEnfermagem && p.AssinaturaDoPrescritor?.ArquivoId is null)
                throw ErroFormularioTablet.Criar("Encerre a execução e confira a via assinada pelo prescritor antes de assinar.");
            return ContratoTablet.Hash(ContratoTablet.Serializar(new {Versao=PostoTabletService.Versao(p),Cadastro=Cadastro(p.Paciente,p.Profissional),Prestador=prestador,ArquivoId=p.AssinaturaDoPrescritor?.ArquivoId,p.OrigemEnfermagem,p.OrientacaoExterna}));
        }
        if(tipo=="infusao")
        {
            var p=await repo.ObterPrescricaoInternaAsync(id,ct) ?? throw new RecursoClinicoIndisponivel();
            if((!p.PodeEditar && !(p.OrigemEnfermagem && p.AguardaValidacaoMedica && p.AssinaturaDaExecucao?.ArquivoId!=null)) || p.Itens.Count==0)
                throw ErroFormularioTablet.Criar("Confira o rascunho ou a execução assinada pela enfermagem antes de validar.");
            var alerta=await prescricoes.ConferirParaAssinaturaAsync(id,ct);
            if(alerta.ExigeConfirmacao && !confirmou) throw ErroFormularioTablet.Criar("Confira as alergias e confirme a revisão antes de assinar.");
            // Fotografia sem ids de navegação: qualquer mudança relevante invalida a autorização.
            return ContratoTablet.Hash(ContratoTablet.Serializar(new {p.PacienteId,p.ProfissionalId,p.AgendamentoId,p.Data,p.Hora,
                Cadastro=Cadastro(p.Paciente,p.Profissional),Prestador=prestador,
                p.Numero,p.Indicacao,p.Observacoes,p.ExigeAssinaturaEletronicaDaExecucao,p.OrigemEnfermagem,p.OrientacaoExterna,ArquivoExecucao=p.AssinaturaDaExecucao?.ArquivoId,
                Itens=p.Itens.OrderBy(i=>i.Ordem).Select(i=>new {i.Ordem,i.Descricao,i.Dose,i.Diluente,i.Volume,i.Via,i.TempoInfusao,i.HoraPrevista,i.SeNecessario,i.Observacoes}),
                Alergias=alerta.Alergias.Select(a=>new {a.Id,a.Descricao,a.AtualizadoEm})}));
        }
        var d=await repo.ObterDocumentoAsync(id,ct) ?? throw new RecursoClinicoIndisponivel();
        if(d.AssinadoEletronicamente || d.Cancelado) throw ErroFormularioTablet.Criar("O documento já foi assinado ou cancelado.");
        var faltas=ConformidadeDocumentoClinico.Conferir(d,assinaturaEletronica:true).Where(f=>f.Impeditiva).ToList();
        if(faltas.Count>0) throw ErroFormularioTablet.Criar(string.Join(" ",faltas.Select(f=>f.Descricao))
            + " Corrija as informações indicadas, confira o PDF atualizado e tente autorizar novamente.");
        return ContratoTablet.Hash(ContratoTablet.Serializar(new {d.PacienteId,d.ProfissionalId,d.AgendamentoId,d.Data,d.Numero,
            Cadastro=Cadastro(d.Paciente,d.Profissional),Prestador=prestador,
            d.Tipo,d.Titulo,d.Corpo,d.Observacoes,d.DiasAfastamento,d.Cid,d.CidAutorizado,
            Itens=d.Itens.OrderBy(i=>i.Ordem).Select(i=>new {i.Descricao,i.Detalhe,i.Quantidade})}));
    }
    private static object Cadastro(Paciente? p,Profissional? profissional)=>new {
        Paciente=p is null ? null : new {p.Id,p.Nome,p.Documento,p.DataNascimento,p.Endereco,p.Telefone,p.Email,p.Carteirinha,p.ConvenioCodigo},
        Profissional=profissional is null ? null : new {profissional.Id,profissional.Nome,profissional.Cpf,profissional.RegistroConselho,profissional.EspecialidadeCodigo,profissional.Ativo}
    };
    public async Task<object> IniciarAsync(SessaoTablet s,int agendamento,string tipo,int id,bool confirmou,CancellationToken ct,bool concluirAutomaticamente = false)
    {
        if(concluirAutomaticamente && (!ConclusaoAutomatica || !confirmou || tipo is not ("infusao" or "execucao")))
            throw ErroFormularioTablet.Criar("Confira a infusão antes de autorizar a conclusão automática.");
        var (u,_)=await acesso.ExigirDocumentoAsync(s,agendamento,tipo,id,true,ct);
        var (opcoes,retorno)=Configuracao();
        if(string.IsNullOrWhiteSpace(u.Profissional!.Cpf)) throw ErroFormularioTablet.Criar("Cadastre o CPF do profissional no sistema antes de assinar.");
        var hash=ContratoTablet.Hash(await Conferir(tipo,id,confirmou,ct)+ContratoTablet.Serializar(Cadastro(null,u.Profissional)));
        using var http=clientes.CreateClient("SafeIdTablet"); http.Timeout=TimeSpan.FromSeconds(30);
        var cliente=new ClienteSafeID(http,opcoes);
        try {await cliente.TokenDaAplicacaoAsync(ct);}
        catch {throw ErroFormularioTablet.Criar("Não foi possível autorizar a aplicação no SafeID. Confira a configuração da clínica.");}
        var a=autorizacoes.Criar(s.Id,agendamento,id,tipo,hash,confirmou,concluirAutomaticamente);
        if(concluirAutomaticamente)
        {
            try { await registro.CriarAsync(a,ct); }
            catch { autorizacoes.Concluir(a,false); throw; }
        }
        var escopo=EscopoSafeID.ParaAto(tipo=="execucao"?2:1);
        return new {a.Id,Url=cliente.UrlDeAutorizacao(a.Pkce,retorno,escopo.Escopo,
            cpf:u.Profissional.Cpf,estado:a.Estado,duracaoSegundos:escopo.DuracaoSegundos).AbsoluteUri};
    }
    public async Task<Guid> ReceberRetornoAsync(string estado,string? codigo,string? erro)
    {
        // A intenção foi confirmada no POST autenticado com CSRF antes do SafeID.
        // Só o state imprevisível, vigente e de uso único pode acionar essa intenção.
        var a=autorizacoes.ReceberRetorno(estado,codigo,erro);
        if(!a.ConclusaoAutomatica)return a.Id; // Autorizações de telas antigas conservam o contrato.
        using var prazo=new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            if(a.Situacao=="autorizado")
                await ConcluirAsync(new SessaoTablet {Id=a.Sessao},a.Id,prazo.Token);
            else await registro.FalharAsync(a.Id,false,prazo.Token);
        }
        catch
        {
            // O POST já classifica a falha. Uma rejeição anterior ao consumo também
            // precisa deixar resultado consultável, sem expor parâmetros do provedor.
            try { await registro.FalharAsync(a.Id,a.Situacao is "assinando" or "falha",CancellationToken.None); }
            catch { logger.LogWarning("SafeID: resultado deve ser conferido pela referência {Referencia}",a.Id); }
            if(a.Situacao is "autorizado" or "assinando")autorizacoes.Concluir(a,false);
        }
        return a.Id;
    }
    public async Task<object> ConcluirAsync(SessaoTablet s,Guid id,CancellationToken ct)
    {
        var a=autorizacoes.Obter(id,s.Id);
        var (u,paciente)=await acesso.ExigirDocumentoAsync(s,a.Agendamento,a.Tipo,a.Documento,true,ct);
        if(a.Situacao=="concluido") return new {estado="concluido",a.Documento,a.Tipo};
        var (opcoes,retorno)=Configuracao();
        a=autorizacoes.Obter(id,s.Id,consumir:true);
        // Depois da confirmação explícita, perder a conexão do tablet não deve
        // descartar uma assinatura que o provedor já consumiu. O servidor conclui
        // o arquivamento, com prazo limitado, e o próximo acesso consulta o PDF.
        using var prazo=new CancellationTokenSource(TimeSpan.FromMinutes(2));
        ct=prazo.Token;
        var etapa = "transacao";
        var atoRemotoIniciado = false;
        try
        {
            if(a.ConclusaoAutomatica)await registro.IniciarAsync(a.Id,ct);
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            if(db.Database.IsNpgsql())
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(20260917, {paciente})",ct);
                if(a.Agendamento>0)await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(20260916, {a.Agendamento})",ct);
            }
            // Descartar leituras anteriores à transação para não assinar uma versão
            // obsoleta do documento, vínculo ou cadastro do profissional.
            db.ChangeTracker.Clear();
            (u,_)=await acesso.ExigirDocumentoAsync(s,a.Agendamento,a.Tipo,a.Documento,true,ct);
            etapa = "conferencia";
            if(ContratoTablet.Hash(await Conferir(a.Tipo,a.Documento,a.ConfirmouAlergia,ct)+ContratoTablet.Serializar(Cadastro(null,u.Profissional)))!=a.ConteudoHash)
                throw new ConflitoClinicoTablet("O documento ou as alergias mudaram. Confira o conteúdo e autorize novamente.");
            using var http=clientes.CreateClient("SafeIdTablet"); http.Timeout=TimeSpan.FromSeconds(45);
            var cliente=new ClienteSafeID(http,opcoes);
            etapa = "token";
            var token=await cliente.TokenPorCodigoAsync(a.Codigo!,a.Pkce,retorno,ct);
            if(!token.Vigente) throw ErroFormularioTablet.Criar("A autorização SafeID expirou.");
            etapa = "certificados";
            var certificados=await cliente.CertificadosAsync(token.AccessToken,somenteDaAutorizacao:true,ct);
            var validos=certificados.Where(c=>c.Certificado.Vigente && c.Certificado.Cpf==Cpf.Normalizar(u.Profissional!.Cpf)).ToArray();
            if(validos.Length!=1) throw ErroFormularioTablet.Criar("O SafeID precisa autorizar um único certificado válido do profissional conectado.");
            var escolhido=validos[0];
            etapa = "titular";
            TitularDoCertificado.Exigir(escolhido.Certificado,u.Profissional!.Cpf,u.Profissional.Nome);
            var certificado=escolhido.Certificado with {AssinadorRemoto=new AssinadorSafeID(cliente,token.AccessToken,escolhido,"Documento clínico")};
            etapa = "assinatura-arquivo";
            atoRemotoIniciado = true;
            if(a.Tipo=="infusao") await assinadorInfusao.AssinarPrescricaoAsync(a.Documento,certificado,a.ConfirmouAlergia,u.Id,u.Login,ct);
            else if(a.Tipo=="execucao")await assinadorInfusao.AssinarExecucaoAsync(a.Documento,certificado,u.Id,u.Login,ct);
            else await assinadorDocumento.AssinarAsync(a.Documento,certificado,u.Id,u.Login,ct);
            etapa = "confirmacao-banco";
            if(a.ConclusaoAutomatica)await registro.ConfirmarAsync(a.Id,ct);
            await tx.CommitAsync(ct);
            autorizacoes.Concluir(a,true);
            return new {estado="concluido",a.Documento,a.Tipo};
        }
        catch(Exception ex)
        {
            if(a.ConclusaoAutomatica)
            {
                // Se COMMIT aconteceu mas sua resposta se perdeu, a linha concluída
                // não é sobrescrita; uma falha incerta conserva o bloqueio de repetição.
                try { await registro.FalharAsync(a.Id,atoRemotoIniciado,CancellationToken.None); }
                catch { logger.LogWarning("SafeID: persistência do resultado indisponível, referência {Referencia}",a.Id); }
            }
            autorizacoes.Concluir(a,false);
            if(ex is ConflitoClinicoTablet)throw;
            var falha = FalhaSafeIdTablet.Classificar(ex);
            // Nunca passar a exceção ao logger: mensagens do PSC podem conter tokens,
            // CPF ou conteúdo da resposta. Só códigos e nomes de métodos controlados.
            logger.LogWarning("SafeID tablet: referencia={Referencia} etapa={Etapa} codigo={Codigo} origem={Origem}",
                a.Id, etapa, falha.Codigo, falha.Origem);
            throw ErroFormularioTablet.Criar("A assinatura não foi confirmada. Confira o documento no histórico antes de iniciar outra autorização. "
                + $"Referência: {a.Id:N}. Código: {falha.Codigo}.");
        }
    }
}
