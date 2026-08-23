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
    string? Marca,
    bool EmDestaque = false)
{
    /// <summary>O momento para ordenar DENTRO de uma natureza — meia-noite quando não há hora.</summary>
    public DateTime Momento => Data.ToDateTime(Hora ?? TimeOnly.MinValue);

    /// <summary>
    /// A hora já formatada, ou NULA quando a natureza não tem hora.
    ///
    /// ⚠️ Ela existe porque a tela amarrava a <c>Visibility</c> ao <c>TimeOnly?</c> através
    /// do <c>TextoParaVisibilidade</c>, e aquele conversor faz <c>value as string</c> — que
    /// devolve <b>null para qualquer coisa que não seja string</b>, inclusive um
    /// <c>TimeOnly</c> encaixotado. Resultado: <c>Collapsed</c> para sempre, e a hora
    /// <b>nunca</b> aparecia. É o defeito do <c>BooleanToVisibilityConverter</c> sobre
    /// string (parcela 61) pelo avesso, e não falha nada: XAML bem-formado, binding válido,
    /// nenhuma exceção.
    ///
    /// O estrago não era cosmético: a evolução de enfermagem existe para responder <i>"o
    /// que observei no paciente, e A QUE HORAS"</i>, e a leitura clínica é a sequência
    /// dentro da sessão (14h20 · 14h50 · 15h10). Sem a hora, três aferições do mesmo dia
    /// saem indistinguíveis. Com o texto pronto aqui, a tela amarra <c>Text</c> E
    /// <c>Visibility</c> na MESMA propriedade.
    /// </summary>
    public string? HoraTexto => Hora?.ToString("HH\\:mm");

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

        // ⚠️ O acesso do DOCUMENTO é o do PAPEL, não o da natureza, e por isso o portão de
        // natureza aqui é o PISO (`VerFichaPaciente`) e não o teto. A declaração de
        // comparecimento e o termo de consentimento LGPD não carregam dado de saúde e saem
        // do balcão o dia inteiro (parcela 59); com o teto (`VerProntuario`), o portão
        // engolia os dois ANTES de o filtro por folha rodar, e quem tem só o cadastro
        // recebia lista vazia — o oposto do que a parcela 59 decidiu.
        //
        // Quem manda é o `Where`: ele deixa passar exatamente os papéis que ESTA pessoa
        // alcança, e é a MESMA regra que a ficha e a central usam.
        mapa[NaturezaRegistroClinico.DocumentoClinico] = Permitido(
            acessos, NaturezaRegistroClinico.DocumentoClinico,
            () => (documentos ?? [])
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
            // A HIPÓTESE entra na linha (parcela 73): é a conclusão do profissional, e é
            // o que a enfermagem precisa saber antes de pendurar qualquer coisa. Sem ela,
            // a linha dizia o que foi feito e não dizia por quê.
            string.IsNullOrWhiteSpace(e.HipoteseDiagnostica)
                ? null
                : $"hipótese: {e.HipoteseDiagnostica}"
                  + (string.IsNullOrWhiteSpace(e.CidSessao) ? string.Empty : $" ({e.CidSessao})"),
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
            // ⚠️ Quem ATENDEU, não quem DIGITOU. A lista que este componente substituiu
            // mostrava `e.Profissional?.Rotulo`; trocá-la por `CriadoPor` mudava a pergunta
            // que a linha responde — e `CriadoPor` é o LOGIN, nulo em toda sessão anterior
            // ao dia em que o sistema passou a gravá-lo. O login fica como caminho de
            // baixo, para a sessão sem profissional vinculado não sair anônima.
            e.Profissional?.Rotulo ?? e.CriadoPor,
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
                // A CONSULTA se anuncia na linha (parcela 73): o diagnóstico de enfermagem
                // é o que o médico não tem noutro lugar, e uma linha que só diz "consulta"
                // obrigaria a abrir uma por uma para saber qual interessa.
                e.EhConsulta ? "CONSULTA DE ENFERMAGEM" : null,
                e.Diagnosticos.Count == 0
                    ? null
                    : string.Join("; ", e.Diagnosticos.OrderBy(d => d.Ordem).Select(d => d.Titulo)),
                e.Cuidados.Count > 0 ? $"{e.Cuidados.Count} cuidado(s) prescrito(s)" : null,
                e.Prescricao?.Numero is { } numero ? $"folha {numero}" : null,
                $"registrado {e.RegistradoEm:dd/MM/yyyy HH\\:mm}"
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
                        : null,
            // ⚠️ O SELO de intercorrência é sinal de SEGURANÇA, e ele estava dissolvido
            // dentro do `Detalhe` — mesmo peso tipográfico do resumo da queixa, e aceso
            // também no registro CANCELADO. A lista que este componente substituiu usava
            // `Intercorrencia && Vigente`: cancelado é registro DESDITO, e alarmar por
            // ele é o alerta que se aprende a ignorar.
            EmDestaque: e.Intercorrencia && !e.Cancelada && !substituidas.Contains(e.Id)))
            .ToList();
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
