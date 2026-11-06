using System.Data;
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

/// <summary>
/// SafeID (certificado em nuvem da Safeweb) no portal, de volta em out/2026 ao lado do A1.
///
/// Mesmo contrato HTTP de antes da retirada de 01/10/2026 (rotas e corpos), com duas
/// diferenças de alcance pedidas pela clínica: só DOCUMENTOS clínicos (receita, atestado,
/// pedido de exame, comparecimento, relatório, anamnese) e a conferência do conteúdo é a
/// MESMA do A1 (<see cref="ConferenciaAssinaturaTablet"/>), para os dois caminhos recusarem
/// as mesmas mudanças posteriores à revisão. Infusão, execução e enfermagem: só A1.
/// </summary>
public sealed class SafeIdTabletService(IConfiguration configuration, AtendimentoTabletService acesso,
    AutorizacoesSafeIdTablet autorizacoes, ClinicaDbContext db, ConferenciaAssinaturaTablet conferencia,
    AssinaturaDeDocumentoClinicoService assinadorDocumento, IHttpClientFactory http, ILogger<SafeIdTabletService> logger)
{
    /// <summary>Nome do HttpClient: sem redirecionamento automático, pelo HTTPS_PROXY da unit.</summary>
    public const string NomeHttp = "SafeIdTablet";
    public const string SomenteA1 =
        "A prescrição de infusão e a folha de execução são assinadas somente com o certificado A1. Use \"Assinar com A1\".";

    public bool Habilitado => configuration.GetValue<bool>("Portal:SafeId:Habilitado")
        && !configuration.GetValue<bool>("Portal:Demo");

    /// <summary>O que o portal pode oferecer com SafeID; o resto do portal só oferece A1.</summary>
    public static IReadOnlyList<string> Tipos { get; } = ["documento"];

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

    /// <summary>Recusa clara para infusão/execução, antes de qualquer conferência ou chamada ao PSC.</summary>
    private static void ExigirDocumentoClinico(string tipo)
    {
        if(tipo is "infusao" or "execucao") throw ErroFormularioTablet.Criar(SomenteA1);
    }

    private ClienteSafeID Cliente(OpcoesSafeID opcoes,int segundos)
    {
        var cliente=http.CreateClient(NomeHttp); cliente.Timeout=TimeSpan.FromSeconds(segundos);
        return new ClienteSafeID(cliente,opcoes);
    }

    private async Task<string> Fotografia(int documento,bool confirmou,UsuarioSistema u,CancellationToken ct)
        => ContratoTablet.Hash(await conferencia.Conferir("documento",documento,confirmou,ct)
            + ContratoTablet.Serializar(ConferenciaAssinaturaTablet.Cadastro(null,u.Profissional)));

    public async Task<object> IniciarAsync(SessaoTablet s,int agendamento,string tipo,int id,bool confirmou,CancellationToken ct)
    {
        ExigirDocumentoClinico(tipo);
        var (u,_)=await acesso.ExigirDocumentoAsync(s,agendamento,tipo,id,true,ct);
        if(tipo!="documento") throw new RecursoClinicoIndisponivel();
        var (opcoes,retorno)=Configuracao();
        if(string.IsNullOrWhiteSpace(u.Profissional!.Cpf)) throw ErroFormularioTablet.Criar("Cadastre o CPF do profissional no sistema antes de assinar.");
        // Conferir antes do PSC: documento incompleto não gasta autorização nem chamada externa.
        var hash=await Fotografia(id,confirmou,u,ct);
        var cliente=Cliente(opcoes,30);
        try {await cliente.TokenDaAplicacaoAsync(ct);}
        catch {throw ErroFormularioTablet.Criar("Não foi possível autorizar a aplicação no SafeID. Confira a configuração da clínica.");}
        var a=autorizacoes.Criar(s.Id,agendamento,id,tipo,hash,confirmou);
        // Um documento, uma selagem: o token morre no uso (EscopoSafeID.ParaAto).
        var escopo=EscopoSafeID.ParaAto(1);
        return new {a.Id,Url=cliente.UrlDeAutorizacao(a.Pkce,retorno,escopo.Escopo,
            cpf:u.Profissional.Cpf,estado:a.Estado,duracaoSegundos:escopo.DuracaoSegundos).AbsoluteUri};
    }

    public async Task<object> ConcluirAsync(SessaoTablet s,Guid id,CancellationToken ct)
    {
        var a=autorizacoes.Obter(id,s.Id);
        ExigirDocumentoClinico(a.Tipo);
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
        try
        {
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
            if(await Fotografia(a.Documento,a.ConfirmouAlergia,u,ct)!=a.ConteudoHash)
                throw new ConflitoClinicoTablet("O documento ou o cadastro mudaram. Confira o conteúdo e autorize novamente.");
            var numero=await db.DocumentosClinicos.Where(d=>d.Id==a.Documento).Select(d=>d.Numero).SingleAsync(ct);
            var cliente=Cliente(opcoes,45);
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
            var certificado=escolhido.Certificado with {AssinadorRemoto=new AssinadorSafeID(cliente,token.AccessToken,escolhido,"Documento clínico nº "+numero)};
            etapa = "assinatura-arquivo";
            await assinadorDocumento.AssinarAsync(a.Documento,certificado,u.Id,u.Login,ct);
            etapa = "confirmacao-banco";
            await tx.CommitAsync(ct);
            autorizacoes.Concluir(a,true);
            return new {estado="concluido",a.Documento,a.Tipo};
        }
        catch(ConflitoClinicoTablet) {autorizacoes.Concluir(a,false);throw;}
        catch(Exception ex)
        {
            autorizacoes.Concluir(a,false);
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
