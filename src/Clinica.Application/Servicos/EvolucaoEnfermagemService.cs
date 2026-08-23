using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// A EVOLUÇÃO DE ENFERMAGEM (parcela 71) — o registro de quem executa.
///
/// A clínica disse que <b>todo paciente passa pela enfermagem</b>, e é isso que decide a
/// forma deste serviço: o dono é o PACIENTE, e a folha de infusão é procedência opcional.
/// Amarrar o registro à folha deixaria sem lugar o curativo, a sala de observação, a
/// triagem — e a reação que aparece meia hora depois de a folha ter sido encerrada, que é
/// justamente a que mais importa.
///
/// As regras vêm inteiras do <see cref="ChecagemPrescricaoService"/>, e não por preguiça:
/// é o mesmo ato de enfermagem, com o mesmo peso, e duas definições da mesma regra divergem
/// na primeira correção.
/// </summary>
public class EvolucaoEnfermagemService
{
    private readonly IClinicaRepositorio _repo;

    /// <summary>
    /// Folga sobre o relógio ao aceitar a hora informada — a máquina da sala pode estar
    /// alguns minutos adiantada. Não é permissão para registrar antes de observar.
    /// </summary>
    private static readonly TimeSpan FolgaDeRelogio = TimeSpan.FromMinutes(5);

    /// <summary>
    /// De onde sai "agora". A costura existe pela mesma razão da checagem: a recusa de hora
    /// futura é regra de SEGURANÇA, e regra de segurança que não dá para testar apodrece
    /// sem ninguém notar.
    /// </summary>
    private readonly Func<DateTime> _agora;

    public EvolucaoEnfermagemService(IClinicaRepositorio repo, Func<DateTime>? agora = null)
    {
        _repo = repo;
        _agora = agora ?? (() => DateTime.Now);
    }

    // ---- Leitura ----

    /// <summary>O que a enfermagem observou durante uma folha de infusão, em ordem de hora.</summary>
    public Task<IReadOnlyList<EvolucaoEnfermagem>> DaPrescricaoAsync(
        int prescricaoId, CancellationToken ct = default)
        => _repo.EvolucoesEnfermagemDaPrescricaoAsync(prescricaoId, ct);

    /// <summary>
    /// A linha do tempo do paciente, da mais recente para a mais antiga. Traz canceladas e
    /// retificadas: elas aparecem MARCADAS, nunca sumindo — imprimir só o valor final faria
    /// o prontuário esconder o que a trilha guarda.
    /// </summary>
    public Task<IReadOnlyList<EvolucaoEnfermagem>> DoPacienteAsync(
        int pacienteId, int limite = 200, CancellationToken ct = default)
        => _repo.EvolucoesEnfermagemDoPacienteAsync(pacienteId, limite, ct);

    // ---- Escrita ----

    /// <summary>
    /// Registra o que foi observado no paciente.
    /// </summary>
    /// <param name="data">O dia do FATO — pode ser anterior a hoje (registro atrasado é legítimo).</param>
    /// <param name="hora">
    /// A hora do FATO, digitada por quem observou. Nunca o relógio: a técnica observa às
    /// 14h20 e senta para digitar às 14h50. O relógio fica em
    /// <see cref="EvolucaoEnfermagem.RegistradoEm"/>, ao lado.
    /// </param>
    /// <param name="alergiaObservada">
    /// Quando preenchido, grava a ALERGIA na lista de problemas do paciente no MESMO
    /// SaveChanges. Aqui ele vale mais que na checagem: lá a oferta só existe no ramo do
    /// não realizado, então o paciente que teve náusea e mesmo assim completou a infusão
    /// nunca virava alerta na próxima prescrição.
    /// </param>
    public async Task<EvolucaoEnfermagem> RegistrarAsync(
        int pacienteId,
        DateOnly data,
        TimeOnly hora,
        string texto,
        IdentificacaoExecutante autor,
        int? prescricaoInternaId = null,
        int? agendamentoId = null,
        bool intercorrencia = false,
        SinaisVitais? sinais = null,
        string? alergiaObservada = null,
        ProcessoDeEnfermagem? processo = null,
        CancellationToken ct = default)
    {
        var evolucao = Montar(
            pacienteId, data, hora, texto, autor,
            prescricaoInternaId, agendamentoId, intercorrencia, sinais);

        AplicarProcesso(evolucao, processo);

        await _repo.AdicionarEvolucaoEnfermagemAsync(evolucao, ct);
        await RegistrarAlergiaSePedido(pacienteId, alergiaObservada, autor, ct);

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = autor.Nome,
            Acao = intercorrencia
                ? "EvolucaoEnfermagemIntercorrencia"
                : "EvolucaoEnfermagemRegistrada",
            PacienteId = pacienteId,
            Detalhe = Descrever(evolucao)
        }, ct);

        await _repo.SalvarAsync(ct);
        return evolucao;
    }

    /// <summary>
    /// Corrige uma evolução SEM apagá-la: grava outra apontando para a anterior, com o
    /// motivo. A anterior continua na base e sai marcada no papel.
    ///
    /// ⚠️ Retificar é para o CONTEÚDO errado (a hora, o valor da pressão, o texto).
    /// <see cref="CancelarAsync"/> é outra coisa: é para a evolução lançada no paciente
    /// errado. Juntar os dois apagaria qual dos dois aconteceu — que é justamente a
    /// pergunta de quem lê o prontuário depois.
    /// </summary>
    public async Task<EvolucaoEnfermagem> RetificarAsync(
        int evolucaoId,
        DateOnly data,
        TimeOnly hora,
        string texto,
        IdentificacaoExecutante autor,
        string motivoRetificacao,
        bool intercorrencia = false,
        SinaisVitais? sinais = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivoRetificacao))
            throw new InvalidOperationException(
                "Diga por que o registro anterior estava errado. É essa frase que separa "
                + "uma correção de uma reescrita.");

        var anterior = await _repo.ObterEvolucaoEnfermagemAsync(evolucaoId, ct)
            ?? throw new InvalidOperationException("Registro de enfermagem não encontrado.");

        if (anterior.Cancelada)
            throw new InvalidOperationException(
                $"Este registro foi cancelado em {anterior.CanceladaEm:dd/MM/yyyy} e não se "
                + "retifica — escreva um registro novo.");

        var evolucao = Montar(
            anterior.PacienteId, data, hora, texto, autor,
            anterior.PrescricaoInternaId, anterior.AgendamentoId, intercorrencia, sinais);

        evolucao.RetificaEvolucaoId = anterior.Id;
        evolucao.MotivoRetificacao = motivoRetificacao.Trim();

        await _repo.AdicionarEvolucaoEnfermagemAsync(evolucao, ct);

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = autor.Nome,
            Acao = "EvolucaoEnfermagemRetificada",
            PacienteId = anterior.PacienteId,
            Detalhe = $"{Descrever(evolucao)} · corrige o registro de "
                    + $"{anterior.Data:dd/MM/yyyy} às {anterior.Hora:HH\\:mm} "
                    + $"(motivo: {evolucao.MotivoRetificacao})"
        }, ct);

        await _repo.SalvarAsync(ct);
        return evolucao;
    }

    /// <summary>
    /// Cancela o registro lançado no paciente ou na sessão ERRADA. A linha FICA, com o
    /// motivo — registro clínico não se apaga (Lei 13.787/2018).
    ///
    /// O motivo é OBRIGATÓRIO, e é a mesma recusa da justificativa do fechamento de caixa e
    /// do descarte de problema: cancelar sem dizer por quê é apagar com uma etapa a mais.
    /// </summary>
    public async Task<EvolucaoEnfermagem> CancelarAsync(
        int evolucaoId, string motivo, string operador, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException(
                "Diga por que este registro está sendo cancelado. Sem o motivo, quem ler o "
                + "prontuário amanhã não sabe se houve engano de paciente ou de digitação.");

        var evolucao = await _repo.ObterEvolucaoEnfermagemAsync(evolucaoId, ct)
            ?? throw new InvalidOperationException("Registro de enfermagem não encontrado.");

        if (evolucao.Cancelada)
            throw new InvalidOperationException(
                $"Este registro já foi cancelado em {evolucao.CanceladaEm:dd/MM/yyyy}.");

        evolucao.CanceladaEm = DateTime.Now;
        evolucao.MotivoCancelamento = motivo.Trim();
        evolucao.CanceladaPor = operador;

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = operador,
            Acao = "EvolucaoEnfermagemCancelada",
            PacienteId = evolucao.PacienteId,
            Detalhe = $"{Descrever(evolucao)} · cancelado por {operador} "
                    + $"(motivo: {evolucao.MotivoCancelamento})"
        }, ct);

        await _repo.SalvarAsync(ct);
        return evolucao;
    }

    // ---- Apoio ----

    /// <summary>
    /// Copia as etapas do Processo de Enfermagem para o registro.
    ///
    /// ⚠️ COPIADAS, e é a regra do protocolo do mapa corporal e do preço por convênio —
    /// aqui com respaldo legal: a Lei 13.787/2018 pede integridade do que foi registrado, e
    /// referência viva ao catálogo faria corrigir uma palavra hoje reescrever o que a
    /// enfermeira registrou no mês passado.
    ///
    /// A ORDEM é a que a tela montou: a folha impressa sai na sequência em que ela pensou o
    /// caso, e reordenar por id daria uma lista que não é a dela.
    /// </summary>
    private static void AplicarProcesso(
        EvolucaoEnfermagem evolucao, ProcessoDeEnfermagem? processo)
    {
        if (processo is null) return;

        evolucao.Historico = Limpar(processo.Historico);
        evolucao.ExameFisico = Limpar(processo.ExameFisico);
        evolucao.Avaliacao = Limpar(processo.Avaliacao);

        var ordem = 0;
        foreach (var d in processo.Diagnosticos ?? [])
        {
            if (string.IsNullOrWhiteSpace(d.Titulo)) continue;

            evolucao.Diagnosticos.Add(new DiagnosticoEnfermagem
            {
                Codigo = Limpar(d.Codigo),
                Titulo = d.Titulo.Trim(),
                RelacionadoA = Limpar(d.RelacionadoA),
                EvidenciadoPor = Limpar(d.EvidenciadoPor),
                ResultadoEsperado = Limpar(d.ResultadoEsperado),
                Ordem = ordem++
            });
        }

        ordem = 0;
        foreach (var c in processo.Cuidados ?? [])
        {
            if (string.IsNullOrWhiteSpace(c.Descricao)) continue;

            evolucao.Cuidados.Add(new CuidadoEnfermagem
            {
                Codigo = Limpar(c.Codigo),
                Descricao = c.Descricao.Trim(),
                Frequencia = Limpar(c.Frequencia),
                Ordem = ordem++
            });
        }
    }

    private static string? Limpar(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private EvolucaoEnfermagem Montar(
        int pacienteId, DateOnly data, TimeOnly hora, string texto,
        IdentificacaoExecutante autor, int? prescricaoId, int? agendamentoId,
        bool intercorrencia, SinaisVitais? sinais)
    {
        if (string.IsNullOrWhiteSpace(texto))
            throw new InvalidOperationException(
                "Escreva o que foi observado. Um registro em branco ocupa uma linha do "
                + "prontuário sem dizer nada — e some no meio dos que dizem.");

        // O nome E o conselho, na MESMA definição que a checagem usa (parcela 72). O
        // comentário da entidade já dizia que "evolução sem registro no conselho não é
        // evolução de enfermagem" — e não havia guarda em lugar nenhum.
        autor.Exigir("registrar evolução de enfermagem");

        ExigirHoraPlausivel(data, hora);

        var evolucao = new EvolucaoEnfermagem
        {
            PacienteId = pacienteId,
            PrescricaoInternaId = prescricaoId,
            AgendamentoId = agendamentoId,
            Data = data,
            Hora = hora,
            Texto = texto.Trim(),
            Intercorrencia = intercorrencia,
            AutorUsuarioId = autor.UsuarioId,
            AutorNome = autor.Nome,
            AutorConselho = autor.Conselho,
            RegistradoEm = DateTime.Now
        };

        if (sinais is { } v)
        {
            evolucao.PressaoSistolica = v.PressaoSistolica;
            evolucao.PressaoDiastolica = v.PressaoDiastolica;
            evolucao.FrequenciaCardiaca = v.FrequenciaCardiaca;
            evolucao.FrequenciaRespiratoria = v.FrequenciaRespiratoria;
            evolucao.Temperatura = v.Temperatura;
            evolucao.SaturacaoOxigenio = v.SaturacaoOxigenio;
            evolucao.Dor = v.Dor;
        }

        // A crítica mora no DOMÍNIO porque há mais de uma porta que grava (a janela da sala
        // e a do prontuário) — validar na tela cobriria uma e deixaria a outra passando.
        if (evolucao.CriticarSinaisVitais() is { } erro)
            throw new InvalidOperationException(erro);

        return evolucao;
    }

    /// <summary>
    /// Hora no FUTURO é recusada, e é regra de segurança e não de formulário: registrar
    /// adiantado é o hábito que faz aparecer como observado um paciente que saiu antes.
    /// </summary>
    private void ExigirHoraPlausivel(DateOnly data, TimeOnly hora)
    {
        var momento = data.ToDateTime(hora);
        var agora = _agora();

        if (momento > agora + FolgaDeRelogio)
            throw new InvalidOperationException(
                $"O horário {data:dd/MM/yyyy} às {hora:HH\\:mm} está no futuro. Registre o "
                + "que já foi observado — a hora é a do fato, não a de quando você digita.");
    }

    private async Task RegistrarAlergiaSePedido(
        int pacienteId, string? alergia, IdentificacaoExecutante autor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(alergia)) return;

        await _repo.AdicionarProblemaAsync(new ProblemaPaciente
        {
            PacienteId = pacienteId,
            Natureza = NaturezaProblema.Alergia,
            Descricao = alergia.Trim(),
            Situacao = SituacaoProblema.Ativo,
            Inicio = DateOnly.FromDateTime(DateTime.Today),
            Observacoes = "Registrada pela enfermagem ao observar reação durante o atendimento.",
            CriadoEm = DateTime.Now,
            CriadoPor = autor.Nome
        }, ct);
    }

    private static string Descrever(EvolucaoEnfermagem e)
    {
        var texto = $"{e.Data:dd/MM/yyyy} às {e.Hora:HH\\:mm}";
        if (e.Intercorrencia) texto += " · INTERCORRÊNCIA";
        if (e.SinaisVitaisResumidos is { } sinais) texto += $" · {sinais}";
        return texto + $" · por {e.AutorNome}"
             + (string.IsNullOrWhiteSpace(e.AutorConselho) ? "" : $" ({e.AutorConselho})");
    }
}

/// <summary>
/// Os sinais vitais de uma aferição, todos opcionais.
///
/// É um <c>record</c> à parte, e não sete parâmetros soltos, porque eles andam juntos: quem
/// chama passa o que aferiu e omite o resto, e acrescentar um oitavo sinal um dia não muda
/// a assinatura de ninguém.
/// </summary>
/// <summary>
/// As etapas do Processo de Enfermagem que a tela colheu (parcela 73).
///
/// ⚠️ Um record só, e não sete parâmetros a mais: a assinatura de
/// <c>RegistrarAsync</c> já tem dez, e um parâmetro posicional a mais é onde nasce o
/// defeito do <c>CancelarAsync(id, operador)</c> — argumento caindo na vaga errada porque
/// os dois são do mesmo tipo, sem estourar nada.
/// </summary>
/// <param name="Diagnosticos">
/// Etapas 2 e 3 — o diagnóstico com o resultado esperado. COPIADOS na colheita: corrigir a
/// redação do catálogo hoje não reescreve o que foi registrado no mês passado.
/// </param>
/// <param name="Cuidados">Etapa 4 — a prescrição de enfermagem, com a frequência de cada um.</param>
public sealed record ProcessoDeEnfermagem(
    string? Historico = null,
    string? ExameFisico = null,
    string? Avaliacao = null,
    IReadOnlyList<DiagnosticoEnfermagem>? Diagnosticos = null,
    IReadOnlyList<CuidadoEnfermagem>? Cuidados = null)
{
    public static ProcessoDeEnfermagem Nenhum { get; } = new();
}

public sealed record SinaisVitais(
    int? PressaoSistolica = null,
    int? PressaoDiastolica = null,
    int? FrequenciaCardiaca = null,
    int? FrequenciaRespiratoria = null,
    decimal? Temperatura = null,
    int? SaturacaoOxigenio = null,
    int? Dor = null);
