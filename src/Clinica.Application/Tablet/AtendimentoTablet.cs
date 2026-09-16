using Clinica.Domain.Entities;

namespace Clinica.Application.Tablet;

public sealed record SalvarModeloMapaTablet(Guid Idempotencia, string Nome, MapaClinicoTablet Mapa);
public sealed record ResultadoModeloMapaTablet(int Id);

public static class PoliticaAtendimentoTablet
{
    public static bool PodeAtender(UsuarioSistema u) => u.Ativo && !u.DeveTrocarSenha
        && u.ProfissionalId is > 0 && u.Pode(Permissao.VerAgenda | Permissao.VerProntuario | Permissao.EditarProntuario);
}

public sealed class RecursoClinicoIndisponivel : Exception;
public sealed class ConflitoClinicoTablet(string mensagem) : Exception(mensagem);

public sealed record PontoClinicoTablet(FaceCorpo Face, double X, double Y, string? Nome,
    TecnicaPonto Tecnica, string? Observacao = null);
public sealed record MapaClinicoTablet(PontoClinicoTablet[] Pontos, string? Observacoes);
public sealed record EvolucaoClinicaTablet(int Id, string Versao, string? QueixaPrincipal,
    string? HistoriaDoencaAtual, string? ExameFisico, string? HipoteseDiagnostica, string? CidSessao,
    string? Conduta, string? TextoEvolucao, string? Orientacoes, string? PlanoTerapeutico,
    int? EvaAntes, int? EvaDepois, MapaClinicoTablet? Mapa);
public sealed record SalvarAtendimentoTablet(Guid Idempotencia, EvolucaoClinicaTablet Evolucao, bool Finalizar = false);
public sealed record EmitirDocumentoTablet(Guid Idempotencia, string Tipo, string Texto,
    string? Observacoes = null, int? DiasAfastamento = null, string? Diluente = "SF 0,9%",
    string? Volume = null, string? TempoInfusao = "1h", bool AssinaturaEnfermagem = true,
    ViaAdministracao Via = ViaAdministracao.Endovenosa);
public sealed record ResultadoGravacaoTablet(int EvolucaoId, string Versao, bool Finalizado, int? AtendimentoId, int Guias, string[] Avisos);
public sealed record ResultadoDocumentoTablet(int Id, string Tipo, string Numero);
