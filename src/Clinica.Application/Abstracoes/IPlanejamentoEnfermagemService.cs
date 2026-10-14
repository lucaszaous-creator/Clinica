using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Abstracoes;

public sealed record PeriodoEnfermagem(DateTime Inicio, DateTime Fim, int Pagina = 0);
public sealed record SemanaEnfermagem(DateOnly Inicio);
public sealed record DiaPlanejamentoEnfermagem(DateOnly Dia, int Total, int Bsv, int BsvAcupuntura, int Bloqueios);
public sealed record CriarSolicitacaoEnfermagem(Guid Idempotencia, DateTime Inicio, DateTime Fim,
    DateTime Validade, ModalidadeAtendimento Modalidade, int Quantidade, string Observacao, string AgendaReferencia);
public sealed record AcaoSolicitacaoEnfermagem(Guid Idempotencia, Guid Versao, string Acao,
    string Observacao = "");
public sealed record HorarioPlanejamento(int Id, string Paciente, DateTime Inicio, DateTime Fim,
    string Modalidade, string Medico, string Sala, string Situacao);
public sealed record BloqueioPlanejamento(DateTime Inicio, DateTime Fim, string Recurso);
public sealed record ResumoPeriodoEnfermagem(DateTime Inicio, DateTime Fim, int Total, int Bsv, int BsvAcupuntura,
    int OutrasModalidades, string Referencia, DateTime AtualizadoEm, IReadOnlyList<HorarioPlanejamento> Horarios,
    IReadOnlyList<BloqueioPlanejamento> Bloqueios, int Pagina, bool Mais);
public sealed record LinhaSolicitacaoEnfermagem(int Id, DateTime Inicio, DateTime Fim, DateTime Validade,
    string Modalidade, ModalidadeAtendimento ModalidadeCodigo, int Quantidade, string Autora, int AutoraId,
    string Situacao, string Observacao, DateTime? VistaEm, string VistaPor, int TotalReferencia,
    bool Ativa, bool PodeTratar, bool PodeRetirar, Guid Versao);
public sealed record EventoPlanejamento(DateTime Em, string Autor, string Acao, string Observacao);
public sealed record DetalheSolicitacaoEnfermagem(LinhaSolicitacaoEnfermagem Pedido,
    ResumoPeriodoEnfermagem Periodo, bool AgendaAlterada, IReadOnlyList<EventoPlanejamento> Historico);

public static class AcessoPlanejamentoEnfermagem
{
    public static bool Solicitar(UsuarioSistema u) => u.Ativo && u.Perfil == PerfilAcesso.Enfermagem
        && u.ProfissionalId != null && u.Pode(Permissao.VerAgenda | Permissao.RegistrarEvolucaoEnfermagem);
    public static bool Tratar(UsuarioSistema u) => u.Ativo && u.Perfil is PerfilAcesso.Recepcao or PerfilAcesso.Gerente
        && u.Pode(Permissao.VerAgenda | Permissao.EditarAgenda | Permissao.VerFichaPaciente);
    public static bool Consultar(UsuarioSistema u) => Solicitar(u) || Tratar(u);
}

public interface IPlanejamentoEnfermagemService
{
    Task<IReadOnlyList<DiaPlanejamentoEnfermagem>> SemanaAsync(int usuarioId, SemanaEnfermagem semana, CancellationToken ct = default);
    Task<ResumoPeriodoEnfermagem> PeriodoAsync(int usuarioId, PeriodoEnfermagem periodo, CancellationToken ct = default);
    Task<IReadOnlyList<LinhaSolicitacaoEnfermagem>> ListarAsync(int usuarioId, DateOnly? dia = null, CancellationToken ct = default);
    Task<DetalheSolicitacaoEnfermagem> DetalheAsync(int usuarioId, int id, CancellationToken ct = default);
    Task<int> CriarAsync(int usuarioId, CriarSolicitacaoEnfermagem pedido, CancellationToken ct = default);
    Task AtualizarAsync(int usuarioId, int id, AcaoSolicitacaoEnfermagem pedido, CancellationToken ct = default);
}
