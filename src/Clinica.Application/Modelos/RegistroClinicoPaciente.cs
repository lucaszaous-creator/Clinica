using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Domain.Prontuario;

namespace Clinica.Application.Modelos;

/// <summary>
/// Uma linha da LINHA DO TEMPO CLÍNICA do paciente — o que aconteceu, quando, escrito por
/// quem.
///
/// ⚠️ <see cref="Natureza"/> + <see cref="Id"/> é DISCRIMINADOR, não enfeite. Os ids são
/// POR TABELA: a evolução de enfermagem nº 42 e a <c>Evolucao</c> nº 42 são registros de
/// pacientes diferentes. Uma lista fundida que carregasse só o id faria o comando de
/// cancelar da sessão apagar a evolução de enfermagem de outra pessoa — <b>não estoura,
/// não avisa</b>, e a clínica que descobre é a que está auditando o produto por escrito.
/// O comentário que documenta esse bug está em <c>PacientesView.xaml</c> desde a parcela
/// 71; o par aqui é o que permite fundir as listas no dia em que <c>Evolucao</c> ganhar
/// hora, sem reabrir o buraco.
/// </summary>
/// <param name="Data">O dia do FATO — nunca o carimbo de digitação.</param>
/// <param name="Hora">
/// A hora do fato quando ela existe. ⚠️ <c>Evolucao.Data</c> é <c>DateOnly</c>: a sessão
/// médica não tem hora, e por isso a linha do tempo NÃO ordena as duas naturezas juntas
/// — ordenar a médica às 00:00 a poria antes de todas as aferições do dia, inclusive da
/// reação que a motivou. Ordenar um prontuário por uma hora que não existe é fabricar
/// sequência de eventos num documento que responde em auditoria.
/// </param>
public sealed record RegistroClinicoPaciente(
    NaturezaRegistroClinico Natureza,
    int Id,
    DateOnly Data,
    TimeOnly? Hora,
    string Titulo,
    string? Detalhe,
    string? Autor,
    bool Vigente,
    string? Marca)
{
    /// <summary>O momento para ordenar DENTRO de uma natureza — meia-noite quando não há hora.</summary>
    public DateTime Momento => Data.ToDateTime(Hora ?? TimeOnly.MinValue);

    public string Rotulo => CatalogoRegistroClinico.Rotular(Natureza);
}

/// <summary>
/// O MONTADOR da linha do tempo clínica (parcela 72) — função PURA.
///
/// Por que pura, e por que na Application
/// --------------------------------------
/// Pela lição de <c>GradeSemana.Montar</c> (parcela 69): <b>a camada de tela do WPF não
/// compila nos testes</b>, e o que decide qual registro aparece para quem precisa de
/// teste. Aqui isso pesa mais que lá — o que se decide é se um dado de saúde é exibido a
/// alguém, e o erro tem a forma de <i>uma linha a mais numa lista</i>, que ninguém percebe.
///
/// ⚠️ O FILTRO DE ACESSO É PARÂMETRO, e não uma consulta a <c>SessaoUsuario</c>. Três telas
/// plugam este componente (a ficha da Recepção, o Consultório e a tela da Enfermagem), e
/// regra de LGPD repetida em três telas é regra que a quarta esquece.
/// </summary>
public static class LinhaDoTempoClinica
{
    /// <summary>
    /// Monta as linhas de cada natureza, já filtradas pelo acesso de quem está lendo.
    ///
    /// ⚠️ Devolve um dicionário POR NATUREZA, e não uma lista única, porque a tela mostra
    /// uma seção por vez (chips contados) — ver o comentário de
    /// <see cref="RegistroClinicoPaciente.Hora"/> para as duas razões de não fundir.
    /// </summary>
    /// <param name="acessos">
    /// As permissões EFETIVAS de quem está lendo. Natureza cuja permissão a pessoa não tem
    /// devolve lista VAZIA — não é escondida da contagem depois, é nunca montada.
    /// </param>
    public static IReadOnlyDictionary<NaturezaRegistroClinico, IReadOnlyList<RegistroClinicoPaciente>> Montar(
        Permissao acessos,
        IEnumerable<Evolucao>? sessoes = null,
        IReadOnlyDictionary<int, int>? anexosPorSessao = null,
        IEnumerable<EvolucaoEnfermagem>? enfermagem = null,
        IEnumerable<PrescricaoInterna>? infusoes = null,
        IEnumerable<DocumentoClinico>? documentos = null)
    {
        var mapa = new Dictionary<NaturezaRegistroClinico, IReadOnlyList<RegistroClinicoPaciente>>();

        mapa[NaturezaRegistroClinico.SessaoMedica] = Permitido(
            acessos, NaturezaRegistroClinico.SessaoMedica,
            () => (sessoes ?? []).Select(e => DeSessao(e, anexosPorSessao)).ToList());

        mapa[NaturezaRegistroClinico.EvolucaoEnfermagem] = Permitido(
            acessos, NaturezaRegistroClinico.EvolucaoEnfermagem,
            () => MontarEnfermagem(enfermagem ?? []));

        mapa[NaturezaRegistroClinico.PrescricaoInterna] = Permitido(
            acessos, NaturezaRegistroClinico.PrescricaoInterna,
            () => (infusoes ?? []).Select(DeInfusao).ToList());

        mapa[NaturezaRegistroClinico.DocumentoClinico] = Permitido(
            acessos, NaturezaRegistroClinico.DocumentoClinico,
            () => (documentos ?? [])
                // ⚠️ O acesso do DOCUMENTO é o do papel, não o da natureza: a declaração de
                // comparecimento e o termo de consentimento não carregam dado de saúde e
                // saem do balcão o dia inteiro (parcela 59). Deixar a natureza decidir por
                // todos tiraria da recepção dois papéis que ela entrega todo dia.
                .Where(d => acessos.HasFlag(CentralDocumentosService.AcessoParaVer(d.Tipo)))
                .Select(DeDocumento)
                .ToList());

        return mapa;
    }

    private static IReadOnlyList<RegistroClinicoPaciente> Permitido(
        Permissao acessos, NaturezaRegistroClinico natureza,
        Func<IReadOnlyList<RegistroClinicoPaciente>> montar)
        => acessos.HasFlag(CatalogoRegistroClinico.Obter(natureza).PermissaoVer)
            ? montar()
            : [];

    private static RegistroClinicoPaciente DeSessao(
        Evolucao e, IReadOnlyDictionary<int, int>? anexos)
    {
        var detalhe = string.Join(" · ", new[]
        {
            string.IsNullOrWhiteSpace(e.QueixaPrincipal) ? null : e.QueixaPrincipal,
            string.IsNullOrWhiteSpace(e.Conduta) ? null : $"conduta: {e.Conduta}",
            e.EvaAntes is { } antes && e.EvaDepois is { } depois
                ? $"EVA {antes} → {depois}"
                : null,
            anexos?.GetValueOrDefault(e.Id) is > 0 and var n ? $"{n} anexo(s)" : null
        }.Where(p => !string.IsNullOrWhiteSpace(p)));

        return new RegistroClinicoPaciente(
            NaturezaRegistroClinico.SessaoMedica,
            e.Id,
            e.Data,
            // Sem hora, e é decisão: ver o comentário de RegistroClinicoPaciente.Hora.
            null,
            string.IsNullOrWhiteSpace(e.TextoEvolucao) ? "Sessão registrada" : e.TextoEvolucao,
            string.IsNullOrWhiteSpace(detalhe) ? null : detalhe,
            e.CriadoPor,
            !e.Cancelada,
            e.Cancelada ? $"CANCELADA — {e.MotivoCancelamento}" : null);
    }

    private static IReadOnlyList<RegistroClinicoPaciente> MontarEnfermagem(
        IEnumerable<EvolucaoEnfermagem> lista)
    {
        var todas = lista.ToList();

        // Substituída pela retificação: fica na lista, marcada — nunca some (parcela 52).
        var substituidas = todas
            .Where(e => e.RetificaEvolucaoId is not null)
            .Select(e => e.RetificaEvolucaoId!.Value)
            .ToHashSet();

        return todas.Select(e => new RegistroClinicoPaciente(
            NaturezaRegistroClinico.EvolucaoEnfermagem,
            e.Id,
            e.Data,
            e.Hora,
            e.Texto,
            string.Join(" · ", new[]
            {
                e.SinaisVitaisResumidos,
                e.Intercorrencia ? "INTERCORRÊNCIA" : null,
                e.Prescricao?.Numero is { } numero ? $"folha {numero}" : null
            }.Where(p => !string.IsNullOrWhiteSpace(p))),
            string.IsNullOrWhiteSpace(e.AutorConselho)
                ? e.AutorNome
                : $"{e.AutorNome} · {e.AutorConselho}",
            !e.Cancelada && !substituidas.Contains(e.Id),
            e.Cancelada
                ? $"CANCELADA — {e.MotivoCancelamento}"
                : substituidas.Contains(e.Id)
                    ? "CORRIGIDA — vale o registro seguinte"
                    : e.EhRetificacao
                        ? $"corrige o registro anterior — {e.MotivoRetificacao}"
                        : null)).ToList();
    }

    private static RegistroClinicoPaciente DeInfusao(PrescricaoInterna p)
        => new(
            NaturezaRegistroClinico.PrescricaoInterna,
            p.Id,
            p.Data,
            p.Hora,
            string.IsNullOrWhiteSpace(p.Indicacao) ? $"Folha {p.Numero}" : p.Indicacao,
            $"{p.Numero} · {p.Itens.Count} item(ns) · {p.Realizados} realizado(s) · "
            + $"{p.NaoRealizados} não realizado(s) · {p.Pendentes} aguardando",
            p.Profissional?.Rotulo,
            !p.Cancelada,
            p.Cancelada ? $"CANCELADA — {p.MotivoCancelamento}" : null);

    private static RegistroClinicoPaciente DeDocumento(DocumentoClinico d)
        => new(
            NaturezaRegistroClinico.DocumentoClinico,
            d.Id,
            d.Data,
            null,
            $"{TipoDocumentoInfo.Rotular(d.Tipo)} {d.Numero}",
            d.AssinadoEletronicamente
                ? $"assinado digitalmente em {d.AssinadoEm:dd/MM/yyyy}"
                : $"conferência {d.CodigoVerificacao}",
            d.Profissional?.Rotulo,
            !d.Cancelado,
            d.Cancelado ? $"CANCELADO — {d.MotivoCancelamento}" : null);
}
