using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure.Tablet;

public sealed partial class PostoTabletService
{
    public const int LimiteAnexo = 5 * 1024 * 1024;
    private static bool PodeAnexar(UsuarioSistema u) => u.Pode(Permissao.EditarProntuario)||u.Pode(Permissao.RegistrarEvolucaoEnfermagem);
    private static string TipoArquivo(byte[] b)
    {
        if(b.Length>=5&&b.AsSpan(0,5).SequenceEqual("%PDF-"u8))return "application/pdf";
        if(b.Length>=8&&b.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))return "image/png";
        if(b.Length>=3&&b[0]==255&&b[1]==216&&b[2]==255)return "image/jpeg";
        throw new InvalidOperationException("Envie um arquivo PDF, JPEG ou PNG válido.");
    }
    public Task<ResultadoFichaTablet> AnexarAsync(SessaoTablet s,int paciente,AnexoTablet p,CancellationToken ct)
        => Escrever(s,paciente,p.Idempotencia,p,"TabletAnexo",Permissao.Nenhuma,async u=> {
            if(!PodeAnexar(u))throw new UnauthorizedAccessException();
            if(p.Conteudo is null || p.Conteudo.Length is 0 or > LimiteAnexo)throw new InvalidOperationException("Escolha um arquivo de até 5 MB.");
            Textos(160,p.Titulo);Textos(260,p.NomeArquivo);Textos(1000,p.Observacoes);
            if(string.IsNullOrWhiteSpace(p.NomeArquivo)||p.NomeArquivo.IndexOfAny(['/', '\\', '\r', '\n'])>=0)
                throw new InvalidOperationException("O nome do arquivo é inválido.");
            var tipo=TipoArquivo(p.Conteudo);
            var ext=Path.GetExtension(p.NomeArquivo).ToLowerInvariant();
            if(tipo!=p.TipoConteudo || !(tipo=="application/pdf"&&ext==".pdf" || tipo=="image/png"&&ext==".png" || tipo=="image/jpeg"&&(ext==".jpg"||ext==".jpeg")))
                throw new InvalidOperationException("O formato do arquivo não corresponde ao nome informado.");
            var a=await new AnexoPacienteService(repo).AnexarAsync(paciente,p.Data,p.Titulo,p.NomeArquivo,p.Conteudo,tipo,p.Observacoes,u.Login,ct:ct);
            return new ResultadoFichaTablet(a.Id);
        },ct);

    public async Task<(byte[] Conteudo,string Tipo,string Nome)> ConteudoAnexoAsync(SessaoTablet s,int paciente,int id,CancellationToken ct)
    {
        var u=await Autorizar(s,ct);
        var a=await db.AnexosPaciente.AsNoTracking().Include(a=>a.Arquivo)
            .SingleOrDefaultAsync(a=>a.Id==id&&a.PacienteId==paciente&&a.CanceladoEm==null,ct)??throw new RecursoClinicoIndisponivel();
        if(a.Arquivo?.Conteudo is not {} b || b.Length>ProntuarioService.TamanhoMaximoAnexo)
            throw new InvalidOperationException("Consulte o arquivo no sistema Clínica.");
        var tipo=TipoArquivo(b);
        if(tipo!=a.TipoConteudo)throw new InvalidOperationException("Confira o formato deste arquivo no sistema Clínica.");
        await Auditar(u,paciente,"TabletAnexoConsultado",ct);await db.SaveChangesAsync(ct);
        return (b,tipo,a.NomeArquivo);
    }
}
