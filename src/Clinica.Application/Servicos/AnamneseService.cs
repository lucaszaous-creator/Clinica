using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// A anamnese do PACIENTE (parcela 75) — colher, revisar e recuperar o que ela dizia antes.
///
/// Ver <see cref="AnamnesePaciente"/> para por que ela não é a evolução com mais campos nem
/// a lista de problemas com texto livre.
/// </summary>
public sealed class AnamneseService
{
    private readonly IClinicaRepositorio _repo;

    public AnamneseService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>
    /// A anamnese do paciente, ou <c>null</c> quando ainda não foi colhida.
    ///
    /// ⚠️ Devolve NULL em vez de um objeto em branco, e isso é decisão: "ainda não
    /// perguntamos" e "perguntamos e não há nada" são respostas diferentes, e a segunda é a
    /// que o objeto vazio contaria. A tela precisa distinguir as duas para não escrever
    /// "sem antecedentes" sobre uma ficha que ninguém abriu.
    /// </summary>
    public Task<AnamnesePaciente?> DoPacienteAsync(int pacienteId, CancellationToken ct = default)
        => _repo.AnamneseDoPacienteAsync(pacienteId, ct);

    /// <summary>
    /// Grava a anamnese. Cria na primeira vez; nas seguintes, GUARDA o que ela dizia antes e
    /// sobrescreve.
    ///
    /// ⚠️ O versionamento é o ponto 2 do compromisso de conformidade e o art. 3º da Lei
    /// 13.787/2018: sem ele, corrigir "nega tabagismo" para "tabagista" apagaria a
    /// informação de que a pessoa havia negado — que é o que uma perícia procura.
    ///
    /// ⚠️ Anamnese VAZIA é recusada. Não é preciosismo: a tela grava no botão Salvar, e sem
    /// esta recusa um clique sem nada digitado criaria a linha, carimbaria "revisada hoje" e
    /// faria a ficha afirmar que a anamnese foi colhida. Registro em branco que parece
    /// registro é pior do que registro nenhum.
    /// </summary>
    public async Task<AnamnesePaciente> SalvarAsync(
        int pacienteId, AnamnesePaciente dados, string? operador = null,
        string? motivoDaCorrecao = null, CancellationToken ct = default)
    {
        if (await _repo.ObterPacienteAsync(pacienteId, ct) is null)
            throw new InvalidOperationException("Paciente não encontrado.");

        Limpar(dados);

        if (dados.EstaVazia)
            throw new InvalidOperationException(
                "Escreva ao menos um dos campos da anamnese. Gravar em branco faria a ficha "
                + "dizer que ela foi colhida.");

        var destino = await _repo.AnamneseDoPacienteAsync(pacienteId, ct);
        var nova = destino is null;

        if (nova)
        {
            destino = new AnamnesePaciente
            {
                PacienteId = pacienteId,
                CriadaEm = DateTime.Now,
                CriadaPor = operador
            };
            await _repo.AdicionarAnamneseAsync(destino, ct);
        }
        else
        {
            GuardarVersao(destino!, operador, motivoDaCorrecao);
            destino!.AtualizadaEm = DateTime.Now;
            destino.AtualizadaPor = operador;
        }

        destino.AntecedentesPessoais = dados.AntecedentesPessoais;
        destino.AntecedentesFamiliares = dados.AntecedentesFamiliares;
        destino.HabitosDeVida = dados.HabitosDeVida;
        destino.HistoriaObstetrica = dados.HistoriaObstetrica;
        destino.RevisaoDeSistemas = dados.RevisaoDeSistemas;
        destino.Observacoes = dados.Observacoes;

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = nova ? "AnamneseColhida" : "AnamneseRevisada",
            PacienteId = pacienteId,
            Detalhe = nova
                ? "Anamnese colhida"
                : $"Anamnese revisada — versão anterior guardada (v{destino.Versoes.Count})"
                  + (string.IsNullOrWhiteSpace(motivoDaCorrecao)
                      ? string.Empty
                      : $" — motivo: {motivoDaCorrecao.Trim()}")
        }, ct);

        await _repo.SalvarAsync(ct);
        return destino;
    }

    /// <summary>
    /// O que a anamnese já disse, da versão mais antiga para a mais nova.
    ///
    /// Guardar a versão e não ter por onde lê-la seria o defeito recorrente do projeto na
    /// variante mais cara — aqui o leitor que falta é uma perícia.
    /// </summary>
    public Task<IReadOnlyList<VersaoAnamnese>> VersoesAsync(
        int anamneseId, CancellationToken ct = default)
        => _repo.VersoesDaAnamneseAsync(anamneseId, ct);

    /// <summary>
    /// Congela o conteúdo ATUAL antes de ele ser sobrescrito. A numeração sai da contagem do
    /// que já existe, e não de um contador guardado na entidade: um campo a mais seria uma
    /// segunda verdade sobre a mesma coisa.
    /// </summary>
    private static void GuardarVersao(AnamnesePaciente atual, string? operador, string? motivo)
        => atual.Versoes.Add(new VersaoAnamnese
        {
            AnamnesePacienteId = atual.Id,
            Versao = atual.Versoes.Count + 1,
            AntecedentesPessoais = atual.AntecedentesPessoais,
            AntecedentesFamiliares = atual.AntecedentesFamiliares,
            HabitosDeVida = atual.HabitosDeVida,
            HistoriaObstetrica = atual.HistoriaObstetrica,
            RevisaoDeSistemas = atual.RevisaoDeSistemas,
            Observacoes = atual.Observacoes,
            SubstituidaEm = DateTime.Now,
            SubstituidaPor = operador,
            Motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim()
        });

    /// <summary>Espaço em branco vira nulo: dois "vazios" diferentes na mesma coluna.</summary>
    private static void Limpar(AnamnesePaciente a)
    {
        a.AntecedentesPessoais = Texto(a.AntecedentesPessoais);
        a.AntecedentesFamiliares = Texto(a.AntecedentesFamiliares);
        a.HabitosDeVida = Texto(a.HabitosDeVida);
        a.HistoriaObstetrica = Texto(a.HistoriaObstetrica);
        a.RevisaoDeSistemas = Texto(a.RevisaoDeSistemas);
        a.Observacoes = Texto(a.Observacoes);
    }

    private static string? Texto(string? v)
        => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
