using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// De onde o prontuário foi aberto. Vai para a trilha porque "abriu a ficha no balcão" e
/// "abriu o prontuário clínico no consultório" são acessos de natureza diferente ao mesmo
/// dado, e quem investiga um acesso indevido precisa dos dois separados.
/// </summary>
public enum OrigemAcessoProntuario
{
    /// <summary>Prontuário clínico: evolução, avaliações, medidas.</summary>
    ProntuarioClinico,

    /// <summary>Ficha do paciente na Recepção (cadastro, convênio, contato).</summary>
    FichaPaciente,

    /// <summary>Tela de atendimento do Consultório.</summary>
    Atendimento,

    /// <summary>Exportação dos dados do titular (LGPD art. 18, II).</summary>
    ExportacaoTitular,

    /// <summary>Documento emitido ou reimpresso a partir do prontuário.</summary>
    Documento,

    /// <summary>
    /// Dado clínico exportado para ARQUIVO (o CSV da curva de dor, das medidas). Separado
    /// de <see cref="ExportacaoTitular"/> porque são fatos diferentes: lá é o paciente
    /// exercendo o art. 18; aqui é o dado de saúde SAINDO do sistema para um arquivo — o
    /// caminho mais fácil de ele sair da clínica, e o que uma investigação procura.
    /// </summary>
    ExportacaoClinica
}

/// <summary>
/// O REGISTRO DE QUEM LEU O PRONTUÁRIO (parcela 52).
///
/// A metade que faltava da trilha
/// ------------------------------
/// A auditoria da parcela 21 gravava 54 tipos de ação e <b>todas eram ESCRITA</b>. A
/// cliente, em auditoria de fornecedor, pediu três coisas: <i>"saber quem acessou
/// determinado prontuário, quando acessou e, idealmente, o que realizou"</i>. O sistema
/// respondia só a terceira — abrir o prontuário de alguém e ler tudo não deixava rastro
/// nenhum.
///
/// Isso importa porque o acesso indevido clássico em clínica é <b>leitura</b>: a
/// funcionária que abre o prontuário da vizinha, do ex-marido, de alguém conhecido. É o
/// caso que a LGPD (arts. 46 e 48) e o dever de prestação de contas querem alcançar, e é
/// exatamente o que a trilha não via.
///
/// A permissão da parcela 49 já limita QUEM pode abrir; ela não responde QUEM abriu — e
/// numa clínica pequena todo mundo tem permissão legítima sobre quase todo mundo. Controle
/// de acesso e trilha de acesso resolvem problemas diferentes, e é preciso ter os dois.
///
/// Por que existe janela de silêncio
/// ---------------------------------
/// Sem ela, um atendimento de vinte minutos entre quatro abas do mesmo paciente geraria
/// quinze linhas idênticas, e a trilha viraria ruído — <b>trilha que ninguém consegue ler
/// é trilha que ninguém lê</b>, que é a mesma lição do alerta que dispara para todo mundo.
/// A janela é de <see cref="JanelaDeSilencio"/>: dentro dela, o mesmo operador no mesmo
/// paciente pela mesma origem não gera linha nova.
///
/// A janela é curta de propósito. Ela agrupa UM atendimento, não um turno: quem abre o
/// mesmo prontuário de manhã e à tarde fez dois acessos, e fundi-los esconderia justamente
/// o padrão que se procura numa investigação.
///
/// Falhar aqui NÃO derruba a tela
/// ------------------------------
/// Registrar o acesso é um efeito colateral da abertura, e banco lento não pode impedir o
/// profissional de ler o prontuário do paciente que está na frente dele. A falha vira
/// rastro no log (<see cref="Diagnostico"/>), como toda degradação silenciosa do projeto.
/// É a mesma decisão da conferência de alergia da parcela 40, e pela mesma razão.
/// </summary>
public sealed class AcessoProntuarioService
{
    private readonly IClinicaRepositorio _repo;
    private readonly Func<DateTime> _agora;

    /// <summary>
    /// Quanto tempo o mesmo acesso fica "já registrado". Ver o resumo da classe.
    /// </summary>
    public static readonly TimeSpan JanelaDeSilencio = TimeSpan.FromMinutes(30);

    /// <summary>Prefixo das ações de leitura na trilha — é o que a tela filtra.</summary>
    public const string PrefixoAcao = "ProntuarioAcessado";

    public AcessoProntuarioService(IClinicaRepositorio repo, Func<DateTime>? agora = null)
    {
        _repo = repo;
        _agora = agora ?? (() => DateTime.Now);
    }

    /// <summary>
    /// Registra que <paramref name="operador"/> abriu o prontuário de
    /// <paramref name="pacienteId"/>. Devolve <c>true</c> quando gravou de fato
    /// (<c>false</c> = estava dentro da janela de silêncio, ou a gravação falhou).
    /// </summary>
    public async Task<bool> RegistrarAsync(
        int pacienteId,
        string? operador,
        OrigemAcessoProntuario origem = OrigemAcessoProntuario.ProntuarioClinico,
        CancellationToken ct = default)
    {
        try
        {
            var quem = string.IsNullOrWhiteSpace(operador) ? "?" : operador.Trim();
            var acao = $"{PrefixoAcao}:{origem}";

            if (await DentroDaJanelaAsync(pacienteId, quem, acao, ct)) return false;

            await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
            {
                DataHora = _agora(),
                Operador = quem,
                Acao = acao,
                PacienteId = pacienteId,
                Detalhe = Rotular(origem)
            }, ct);

            await _repo.SalvarAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            // Degradação COM rastro: a tela abre, e o log diz que a trilha não registrou.
            // Falha silenciosa aqui seria pior do que não ter a feature — a clínica
            // acreditaria estar cobrindo o acesso e não estaria.
            Diagnostico.Registrar(
                $"Auditoria — acesso ao prontuário {pacienteId} não pôde ser registrado", ex);
            return false;
        }
    }

    /// <summary>
    /// Registra a exportação da CLÍNICA INTEIRA: uma linha por paciente exportado, num
    /// só SaveChanges. É o maior acesso possível à base — deixá-lo fora da trilha
    /// enquanto o acesso a UM paciente é registrado seria cobrir o menor caso e perder o
    /// maior. Sem janela de silêncio: exportar tudo é ato deliberado, nunca a mesma tela
    /// recarregando. Devolve quantas linhas gravou (0 = falhou, com rastro no log).
    /// </summary>
    public async Task<int> RegistrarExportacaoDaClinicaAsync(
        IReadOnlyCollection<int> pacienteIds, string? operador, CancellationToken ct = default)
    {
        try
        {
            var quem = string.IsNullOrWhiteSpace(operador) ? "?" : operador.Trim();
            var acao = $"{PrefixoAcao}:{OrigemAcessoProntuario.ExportacaoTitular}";
            var agora = _agora();

            foreach (var pacienteId in pacienteIds)
                await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
                {
                    DataHora = agora,
                    Operador = quem,
                    Acao = acao,
                    PacienteId = pacienteId,
                    Detalhe = "Exportação do prontuário da clínica inteira"
                }, ct);

            await _repo.SalvarAsync(ct);
            return pacienteIds.Count;
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar(
                "Auditoria — exportação da clínica não pôde ser registrada na trilha", ex);
            return 0;
        }
    }

    /// <summary>
    /// Quem abriu o prontuário deste paciente, do mais recente para o mais antigo.
    ///
    /// É a pergunta da cliente ao pé da letra, e por isso tem leitura PRÓPRIA em vez de
    /// deixá-la montar o filtro na tela de auditoria: ninguém investiga um acesso indevido
    /// adivinhando o nome da ação.
    /// </summary>
    public Task<IReadOnlyList<EventoAuditoria>> DoPacienteAsync(
        int pacienteId, int limite = 300, CancellationToken ct = default)
        // O filtro casa a ação por PREFIXO, então "ProntuarioAcessado" traz as cinco
        // origens de uma vez — e o corte acontece no banco.
        => _repo.ConsultarAuditoriaAsync(new Modelos.FiltroAuditoria
        {
            PacienteId = pacienteId,
            Acao = PrefixoAcao,
            Limite = limite
        }, ct);

    private async Task<bool> DentroDaJanelaAsync(
        int pacienteId, string operador, string acao, CancellationToken ct)
    {
        var agora = _agora();

        // Só o dia de hoje e só esta ação: a janela é de 30 min, e varrer a trilha inteira
        // do paciente para responder sobre a última meia hora seria caro num banco remoto.
        var recentes = await _repo.ConsultarAuditoriaAsync(new Modelos.FiltroAuditoria
        {
            PacienteId = pacienteId,
            Acao = acao,
            Operador = operador,
            Inicio = DateOnly.FromDateTime(agora.Date),
            Limite = 20
        }, ct);

        var limite = agora - JanelaDeSilencio;

        // A comparação de operador é reconferida aqui porque o filtro casa por TRECHO:
        // "ana" no filtro traria "mariana", e duas pessoas diferentes compartilhariam a
        // janela de silêncio uma da outra — o que apagaria justamente o acesso da segunda.
        return recentes.Any(e => e.Acao == acao
                                 && string.Equals(e.Operador, operador, StringComparison.Ordinal)
                                 && e.DataHora >= limite);
    }

    private static string Rotular(OrigemAcessoProntuario origem) => origem switch
    {
        OrigemAcessoProntuario.ProntuarioClinico => "Prontuário clínico aberto",
        OrigemAcessoProntuario.FichaPaciente => "Ficha do paciente aberta",
        OrigemAcessoProntuario.Atendimento => "Tela de atendimento aberta",
        OrigemAcessoProntuario.ExportacaoTitular => "Dados do titular exportados (LGPD art. 18, II)",
        OrigemAcessoProntuario.Documento => "Documento do prontuário aberto",
        OrigemAcessoProntuario.ExportacaoClinica => "Dados clínicos exportados para arquivo",
        _ => "Prontuário acessado"
    };
}
