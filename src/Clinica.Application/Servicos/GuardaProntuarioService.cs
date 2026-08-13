using Clinica.Application.Abstracoes;
using Clinica.Domain;

namespace Clinica.Application.Servicos;

/// <summary>
/// A situação da guarda do prontuário de um paciente.
/// </summary>
/// <param name="UltimoRegistro">
/// A data do registro mais recente de QUALQUER natureza. Null = prontuário sem nada
/// dentro, e aí não há guarda a contar (ver <see cref="SituacaoGuarda.SemRegistro"/>).
/// </param>
/// <param name="Origem">De onde veio essa data ("sessão", "avaliação", "documento emitido"…).</param>
public sealed record SituacaoGuarda(
    int PacienteId,
    string PacienteNome,
    DateOnly? UltimoRegistro,
    string? Origem,
    int Sessoes,
    int SessoesCanceladas,
    int Documentos,
    int Avaliacoes,
    int Medidas,
    int Prescricoes)
{
    /// <summary>Não há nada guardado para este paciente — nem prazo a contar.</summary>
    public bool SemRegistro => UltimoRegistro is null;

    public DateOnly? VenceEm => UltimoRegistro is { } d ? GuardaProntuario.VenceEm(d) : null;

    public bool CumpriuPrazo(DateOnly hoje)
        => UltimoRegistro is { } d && GuardaProntuario.CumpriuPrazo(d, hoje);

    public string Descrever(DateOnly hoje)
        => UltimoRegistro is { } d
            ? GuardaProntuario.Descrever(d, hoje)
            : "Sem registro clínico — não há prazo de guarda a contar.";

    /// <summary>Tudo o que está sob guarda, somado. É o número que a clínica mostra a quem a audita.</summary>
    public int TotalDeRegistros
        => Sessoes + SessoesCanceladas + Documentos + Avaliacoes + Medidas + Prescricoes;
}

/// <summary>O retrato da guarda na clínica inteira.</summary>
public sealed record ResumoGuarda(
    int Pacientes,
    int ComRegistro,
    int JaCumpriramPrazo,
    DateOnly? RegistroMaisAntigo,
    int TotalDeRegistros)
{
    /// <summary>
    /// Nenhum prontuário elegível a eliminação é a resposta NORMAL numa clínica com menos
    /// de 20 anos de casa, e a tela diz isso — senão "0" se lê como consulta quebrada.
    /// </summary>
    public bool NadaElegivel => JaCumpriramPrazo == 0;
}

/// <summary>
/// A GUARDA DE 20 ANOS — Lei 13.787/2018, art. 6º (parcela 52).
///
/// Por que este serviço existe
/// ---------------------------
/// A cliente fez, por escrito, a pergunta de auditoria de fornecedor: <i>"como você
/// garante retenção e recuperação desses dados durante esse período?"</i>. Até esta
/// parcela a resposta honesta era "não garanto" — e ela tinha três metades faltando:
///
/// 1. O sistema APAGAVA registro clínico (quatro caminhos com <c>Remove()</c>). Resolvido
///    na mesma parcela: cancela-se com motivo, e os métodos de exclusão foram tirados do
///    repositório para que nenhuma tela futura os alcance.
/// 2. O sistema não sabia CALCULAR o prazo. Resolvido em <see cref="GuardaProntuario"/>.
/// 3. Não havia como MOSTRAR a resposta. É o que este serviço serve à tela.
///
/// A terceira parece a menos importante e não é: numa auditoria, garantia que não se
/// consegue demonstrar tem o mesmo valor que garantia que não existe. Quem contrata não
/// audita o código-fonte — audita a tela.
///
/// O que conta como "último registro"
/// ----------------------------------
/// O mais recente de QUALQUER coisa que o prontuário guarda: sessão (mesmo cancelada —
/// ela continua sob guarda), avaliação, medida clínica e documento emitido. Contar só as
/// sessões daria prazo curto demais para o paciente que só fez exames, e prazo é piso: se
/// erra, tem de errar para MAIS.
///
/// <b>Este serviço não elimina nada, e o sistema também não</b> — ver a nota em
/// <see cref="GuardaProntuario"/>. Vencido o prazo, o prontuário fica ELEGÍVEL, e a
/// decisão é da clínica com a comissão de revisão que o art. 7º prevê.
/// </summary>
public sealed class GuardaProntuarioService
{
    private readonly IClinicaRepositorio _repo;

    public GuardaProntuarioService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>A guarda do prontuário de um paciente.</summary>
    public async Task<SituacaoGuarda> DoPacienteAsync(
        int pacienteId, CancellationToken ct = default)
    {
        var paciente = await _repo.ObterPacienteAsync(pacienteId, ct)
            ?? throw new InvalidOperationException("Paciente não encontrado.");

        // As canceladas entram: elas continuam sob guarda, e é justamente por isso que
        // deixaram de ser apagadas. Vale para avaliação e medida também — até aqui as
        // duas listas vinham sem as canceladas, e o prazo podia sair do registro errado.
        var sessoes = await _repo.EvolucoesDoPacienteAsync(pacienteId, true, ct);
        var avaliacoes = await _repo.AvaliacoesDoPacienteAsync(
            pacienteId, null, incluirCanceladas: true, ct);
        var medidas = await _repo.MedidasDoPacienteAsync(
            pacienteId, null, incluirCanceladas: true, ct);
        var documentos = await _repo.DocumentosDoPacienteAsync(pacienteId, ct);
        // A lista de problemas também é registro datado do prontuário: um paciente cujo
        // último fato clínico é um problema anotado (uma alergia descoberta, um
        // diagnóstico) teria o prazo contado do registro errado sem ela.
        var problemas = await _repo.ProblemasDoPacienteAsync(pacienteId, somenteAtivos: false, ct);
        // A folha de infusão é registro clínico como qualquer outro: ela diz o que entrou
        // no paciente e a que horas. Ficou de fora da primeira versão desta classe, o que
        // contrariava a própria regra 8 do CLAUDE.md — entidade clínica entra na guarda e
        // na exportação. Sem ela, o prazo de um paciente que só recebe infusão seria
        // calculado pelo registro errado (ou não seria calculado).
        var prescricoes = await _repo.PrescricoesInternasDoPacienteAsync(pacienteId, int.MaxValue, ct);

        var candidatos = new List<(DateOnly Data, string Origem)>();

        foreach (var s in sessoes) candidatos.Add((s.Data, "sessão"));
        foreach (var a in avaliacoes) candidatos.Add((a.Data, "avaliação"));
        foreach (var m in medidas) candidatos.Add((m.Data, "medida"));
        // DocumentoClinico.Data é a data do ato (a do atestado, a da receita) — é ela
        // que interessa à guarda, e não o carimbo de criação da linha.
        foreach (var d in documentos) candidatos.Add((d.Data, "documento emitido"));
        foreach (var pr in prescricoes) candidatos.Add((pr.Data, "prescrição de infusão"));
        // O problema entra pela data que ele CARREGA: o início declarado quando há, e o
        // dia do registro quando não há (problema sem início ainda é fato anotado).
        foreach (var pb in problemas)
            candidatos.Add((pb.Inicio ?? DateOnly.FromDateTime(pb.CriadoEm), "problema anotado"));

        var ultimo = candidatos.Count == 0
            ? default((DateOnly Data, string Origem)?)
            : candidatos.OrderByDescending(c => c.Data).First();

        return new SituacaoGuarda(
            pacienteId,
            paciente.Nome,
            ultimo?.Data,
            ultimo?.Origem,
            Sessoes: sessoes.Count(s => !s.Cancelada),
            SessoesCanceladas: sessoes.Count(s => s.Cancelada),
            Documentos: documentos.Count,
            Avaliacoes: avaliacoes.Count,
            Medidas: medidas.Count,
            Prescricoes: prescricoes.Count);
    }

    /// <summary>
    /// O retrato da clínica: quantos prontuários estão sob guarda e se algum já cumpriu
    /// o prazo.
    ///
    /// Varre paciente a paciente de propósito, e a tela avisa que pode demorar: é uma
    /// leitura de auditoria, feita algumas vezes por ano, não um painel que abre todo dia.
    /// Otimizá-la antes de alguém reclamar seria trocar clareza por velocidade num lugar
    /// onde a clareza é o produto.
    /// </summary>
    public async Task<ResumoGuarda> ResumoAsync(DateOnly hoje, CancellationToken ct = default)
    {
        var pacientes = await _repo.BuscarPacientesAsync(null, null, ct);

        var comRegistro = 0;
        var cumpriram = 0;
        var total = 0;
        DateOnly? maisAntigo = null;

        foreach (var p in pacientes)
        {
            ct.ThrowIfCancellationRequested();

            var situacao = await DoPacienteAsync(p.Id, ct);
            total += situacao.TotalDeRegistros;

            if (situacao.UltimoRegistro is not { } ultimo) continue;

            comRegistro++;
            if (situacao.CumpriuPrazo(hoje)) cumpriram++;
            if (maisAntigo is null || ultimo < maisAntigo) maisAntigo = ultimo;
        }

        return new ResumoGuarda(pacientes.Count, comRegistro, cumpriram, maisAntigo, total);
    }
}
