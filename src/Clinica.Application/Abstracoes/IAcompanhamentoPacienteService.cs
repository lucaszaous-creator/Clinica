using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Abstracoes;

public sealed record OpcaoAcompanhamento(int Id, string Nome);
public sealed record ConfiguracaoAcompanhamento(int ProfissionalBsvId, int ResponsavelPadraoId,
    IReadOnlyList<OpcaoAcompanhamento> Profissionais, IReadOnlyList<OpcaoAcompanhamento> Responsaveis,
    IReadOnlyList<OpcaoAcompanhamento> Motivos);
public sealed record EstadoIndicacaoBsv(bool PodeIndicar, bool Indicado);
// ResponsavelId permanece no contrato por compatibilidade; a gravação usa o usuário da sessão validada.
public sealed record AtualizarAcompanhamento(Guid Idempotencia, Guid Versao, int ResponsavelId,
    DateOnly? ProximoContato, EtapaAcompanhamento Etapa, CanalContato? Canal, string Observacao,
    bool Encerrar = false, int? MotivoId = null, bool Reabrir = false);
public sealed record LinhaAcompanhamento(int Id, int PacienteId, string Paciente, string? Telefone,
    string Convenio, TipoAcompanhamento Tipo, ModalidadeAtendimento Modalidade, string Profissional,
    int? ProfissionalId, int? ResponsavelId, string Responsavel, DateTime ReferenciaEm, int Dias,
    string Situacao, bool Pendente, bool Atrasado, DateOnly ProximoContato, EtapaAcompanhamento Etapa,
    int Tentativas, DateTime? UltimoContato, string? UltimoResultado, bool Consentimento,
    bool PacoteComSaldo, DateTime? AgendadoPara, bool Cancelou, bool Faltou, Guid Versao,
    string? MotivoEncerramento, string? Motivo)
{
    public string ModalidadeTexto => Tipo == TipoAcompanhamento.NovoBsv ? "BSV / BSV + acupuntura" : RotulosEnum.De(Modalidade);
    public string DiasTexto => Tipo == TipoAcompanhamento.Recall ? $"{Dias} dias sem retornar" : $"Indicado há {Dias} dias";
    public string AcaoTexto => Pendente ? "Registrar contato" : "Ver acompanhamento";
    public string ProximoPasso => !Pendente ? Situacao == "Agendado" ? "Aguardar a sessão agendada" : "Consultar histórico"
        : string.IsNullOrWhiteSpace(Telefone) ? "Atualizar telefone"
        : !Consentimento ? "Conferir autorização de contato"
        : Situacao == "Conferir comparecimento" ? "Conferir a presença na agenda"
        : Etapa == EtapaAcompanhamento.AguardandoPlano ? "Acompanhar autorização do plano"
        : Etapa == EtapaAcompanhamento.ProntoParaAgendar ? "Agendar a sessão"
        : Tentativas == 0 ? "Fazer o primeiro contato" : "Retomar contato com o paciente";
    public bool PodeContatar => !string.IsNullOrWhiteSpace(Telefone) && Consentimento && Pendente;
    public string Marcadores => string.Join(" · ", new[] { Atrasado ? "Contato atrasado" : null,
        PacoteComSaldo ? "Pacote com saldo" : null, string.IsNullOrWhiteSpace(Telefone) ? "Sem telefone" : null,
        !Consentimento ? "Contato não autorizado" : null, Motivo }.Where(x => x != null));
}

public interface IAcompanhamentoPacienteService
{
    Task<EstadoIndicacaoBsv> EstadoBsvAsync(int usuarioId, int agendamentoId, CancellationToken ct = default);
    Task IndicarBsvAsync(int usuarioId, int agendamentoId, CancellationToken ct = default);
    Task<ConfiguracaoAcompanhamento> ConfiguracaoAsync(int usuarioId, CancellationToken ct = default);
    Task ConfigurarAsync(int usuarioId, int profissionalId, int responsavelId, CancellationToken ct = default);
    Task AdicionarMotivoAsync(int usuarioId, string nome, CancellationToken ct = default);
    Task<IReadOnlyList<LinhaAcompanhamento>> ListarAsync(int usuarioId, CancellationToken ct = default);
    Task<int> GerarRecallAsync(int usuarioId, int dias, ModalidadeAtendimento? modalidade = null, CancellationToken ct = default);
    Task<IReadOnlyList<ContatoAcompanhamento>> HistoricoAsync(int usuarioId, int id, CancellationToken ct = default);
    Task AtualizarAsync(int usuarioId, int id, AtualizarAcompanhamento pedido, CancellationToken ct = default);
    Task ValidarContatoAsync(int usuarioId, int id, CancellationToken ct = default);
}
