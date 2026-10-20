using System.Globalization;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Prontuario;

namespace Clinica.Application.Modelos;

/// <summary>
/// Uma passagem de enfermagem do jeito que ela se LÊ (out/2026: subiu do
/// <c>LinhaEvolucaoEnfermagem</c> do shell WPF para cá na migração da seção "Atendimento de
/// enfermagem" e da tela Enfermagem para Blazor — a ViewModel WPF não compila no projeto de
/// teste, e o que a lista AFIRMA precisa morar onde o <c>dotnet test</c> alcança).
/// </summary>
public sealed record LinhaDaPassagem(
    int Id,
    string Hora,
    string Data,
    string Texto,
    string? SinaisVitais,
    string Autor,
    string Registro,
    bool Intercorrencia,
    bool Cancelada,
    bool Substituida,
    bool DestaSessao,
    string? Marca,
    string? Folha,
    int? AgendamentoId)
{
    /// <summary>
    /// Autor · registro · folha, com o separador resolvido AQUI: a folha é nula na passagem
    /// avulsa (a maioria), e separador fixo em volta de campo opcional deixa " · " pendurado.
    /// </summary>
    public string Assinatura => string.Join(" · ", new[] { Autor, Registro, Folha }
        .Where(p => !string.IsNullOrWhiteSpace(p)));

    /// <summary>Vigente é o que se pode corrigir ou cancelar — o resto é histórico.</summary>
    public bool Vigente => !Cancelada && !Substituida;

    /// <summary>
    /// Pode ser amarrada à sessão original: está valendo e ainda não tem horário.
    /// </summary>
    public bool PodeVincular => Vigente && AgendamentoId is null;

    /// <summary>Destaque só no que está valendo: intercorrência já corrigida é histórico.</summary>
    public bool EmDestaque => Intercorrencia && Vigente;
}

/// <summary>Uma linha do catálogo de enfermagem, como ela aparece na janela de escolha.</summary>
public sealed record ItemDoCatalogoEnfermagem(string Codigo, string Titulo, string? Detalhe);

/// <summary>Uma sessão do dia que pode receber a passagem registrada fora dela.</summary>
public sealed record SessaoParaVincular(int Id, string Rotulo);

/// <summary>
/// O que o COMPOSITOR DA PASSAGEM decide (out/2026 — as regras do
/// <c>EvolucaoEnfermagemViewModel</c> WPF, 1244 linhas, que as três portas compartilhavam: a
/// seção do Consultório, a tela Enfermagem e a janela da sala de infusão). As regras caras —
/// a hora INFORMADA, a retificação que preserva a data do fato, a alergia no mesmo
/// SaveChanges — continuam no <see cref="EvolucaoEnfermagemService"/>; aqui mora o que a tela
/// AFIRMA e lê do que a pessoa digitou.
/// </summary>
public static class PassagemDeEnfermagemNaTela
{
    public const string RotuloRegistrar = "Salvar sessão";
    public const string RotuloCorrigir = "Gravar correção";

    public const string HoraInvalida = "Informe a hora do que foi observado, no formato 14:20.";

    public const string LegendaDoRodape =
        "A hora é a do FATO observado, não a de agora — o relógio do sistema fica gravado ao lado.";

    public const string PerguntaCorrigirTitulo = "Corrigir o registro";

    public const string PerguntaCorrigir =
        "Por que o registro anterior estava errado? Ele não é apagado — fica na folha, marcado, "
        + "com esta explicação ao lado.";

    public const string PerguntaCancelarTitulo = "Cancelar o registro";

    public const string PerguntaCancelar =
        "Cancelar é para o registro lançado no paciente ou na sessão ERRADA — para corrigir o "
        + "texto, use Corrigir. Por que este registro está sendo cancelado? Ele não é apagado: "
        + "fica no prontuário, marcado.";

    public const string PerguntaVincularTitulo = "Vincular à sessão original";

    public const string PerguntaVincular =
        "Informe por que esta evolução foi registrada fora da sessão. O conteúdo, a autoria e "
        + "as datas serão preservados.";

    public const string SemSessaoParaVincular =
        "Não há sessão elegível no dia deste registro. Confira a data do atendimento original.";

    public const string ConfirmarAnotacaoTitulo = "Voltar para anotação de passagem?";

    public const string ConfirmarAnotacao =
        "As cinco etapas que você escreveu NÃO vão para o prontuário se este registro for "
        + "gravado como anotação.\n\nElas continuam na tela — basta marcar “Consulta de "
        + "enfermagem” de novo para voltar a elas.\n\nDeseja continuar?";

    public const string Cancelado = "Registro cancelado. Ele continua no prontuário, com o motivo.";

    public const string Vinculado =
        "Evolução vinculada à sessão original. A conclusão e as guias foram preservadas.";

    public const string RegistroNaoEncontrado = "O registro não foi encontrado — recarregue a folha.";

    /// <summary>A frase de êxito do Registrar — a da alergia diz o que mais aconteceu.</summary>
    public static string Registrado(bool comAlergia) => comAlergia
        ? "Registrado. A alergia entrou na lista de problemas do paciente e vai alertar na "
          + "próxima prescrição."
        : "Evolução de enfermagem salva. A situação do atendimento e as guias foram preservadas. "
          + "A conclusão é feita pelo médico.";

    public static string Corrigindo(DateOnly data, TimeOnly hora)
        => $"Corrigindo o registro de {data:dd/MM/yyyy} às {hora:HH\\:mm}. O original fica na "
           + "folha, marcado, e a correção mantém a data do fato.";

    /// <summary>
    /// A frase que diz A QUE esta passagem se liga — três casos, não dois: ligada à folha,
    /// ligada ao HORÁRIO (entra na ficha daquela sessão) ou solta (registro do paciente).
    /// </summary>
    public static string DescreverContexto(int? prescricaoId, string? folha, int? agendamentoId)
        => prescricaoId is not null
            ? $"Durante a folha de infusão {folha}."
            : agendamentoId is not null
                ? "Ligada ao horário do atendimento — a passagem fica registrada nesta sessão."
                : "Registro do paciente — esta passagem não está ligada a uma folha de infusão.";

    /// <summary>
    /// A hora do FATO, digitada. "14:20" ou o que a cultura pt-BR entender; o que não se lê
    /// é NULO — e a tela diz por quê, em vez de gravar a hora de agora no lugar.
    /// </summary>
    public static TimeOnly? LerHora(string? texto)
    {
        var t = (texto ?? string.Empty).Trim();
        if (t.Length == 0) return null;
        if (TimeOnly.TryParseExact(t, "HH\\:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var hora))
            return hora;
        return TimeOnly.TryParse(t, CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.None, out hora) ? hora : null;
    }

    public static int? Inteiro(string? texto)
        => int.TryParse((texto ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>
    /// Aceita "36,4" e "36.4": a técnica digita com vírgula, e o teclado numérico do notebook
    /// manda ponto. Recusar um dos dois faria do teclado um requisito de registro clínico.
    /// </summary>
    public static decimal? Decimal(string? texto)
    {
        var limpo = (texto ?? string.Empty).Trim().Replace('.', ',');
        return decimal.TryParse(limpo, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out var v) ? v : null;
    }

    /// <summary>
    /// Os sinais vitais desta passagem, ou nulo quando nada foi aferido. A PA chega num campo
    /// só ("120/80") e quem a separa é <see cref="PressaoArterialTexto"/>.
    /// </summary>
    public static SinaisVitais? LerSinaisVitais(
        string? pressao, string? cardiaca, string? respiratoria, string? temperatura,
        string? saturacao, string? dor)
    {
        var (s, d) = PressaoArterialTexto.Separar(pressao);
        var sinais = new SinaisVitais(
            Inteiro(s), Inteiro(d), Inteiro(cardiaca), Inteiro(respiratoria),
            Decimal(temperatura), Inteiro(saturacao), Inteiro(dor));
        return sinais == new SinaisVitais() ? null : sinais;
    }

    /// <summary>
    /// O acesso venoso, ou nulo quando nada foi dito dele. ⚠️ Nulo é "não avaliado", e não
    /// "não há acesso": gravar o vazio como afirmação de ausência é inventar um achado.
    /// </summary>
    public static AcessoVenoso? LerAcesso(string? local, string? calibre, DateOnly? puncionadoEm)
    {
        var acesso = new AcessoVenoso(
            string.IsNullOrWhiteSpace(local) ? null : local.Trim(),
            string.IsNullOrWhiteSpace(calibre) ? null : calibre.Trim(),
            puncionadoEm);
        return acesso == new AcessoVenoso() ? null : acesso;
    }

    /// <summary>
    /// As etapas que ficaram vazias — a frase que AVISA sem impedir (COFEN 358/2009): quem
    /// colhe o histórico agora e fecha a avaliação depois da infusão está fazendo o certo.
    /// </summary>
    public static string? EtapasEmFalta(
        bool consultaCompleta, string? historico,
        IReadOnlyList<(string Titulo, string? ResultadoEsperado)> diagnosticos,
        int cuidados, string? avaliacao)
    {
        if (!consultaCompleta) return null;

        var faltam = new List<string>();
        if (string.IsNullOrWhiteSpace(historico)) faltam.Add("histórico");
        if (diagnosticos.Count == 0) faltam.Add("diagnóstico");
        if (diagnosticos.Count > 0 && diagnosticos.All(d => string.IsNullOrWhiteSpace(d.ResultadoEsperado)))
            faltam.Add("resultado esperado");
        if (cuidados == 0) faltam.Add("prescrição de enfermagem");
        if (string.IsNullOrWhiteSpace(avaliacao)) faltam.Add("avaliação");

        return faltam.Count == 0
            ? null
            : "Processo de enfermagem incompleto — falta: " + string.Join(", ", faltam)
              + ". Você pode registrar assim e completar depois (COFEN 358/2009).";
    }

    /// <summary>
    /// ALGUMA das cinco etapas tem conteúdo — é o que decide se desmarcar a consulta custa
    /// alguma coisa (e só então a tela pergunta). Não é <see cref="EtapasEmFalta"/> pelo
    /// avesso: aquele responde "o que falta", este "o que já foi escrito".
    /// </summary>
    public static bool ConsultaTemConteudo(
        string? historico, string? exameFisico, string? avaliacao,
        IEnumerable<string?> titulosDosDiagnosticos, IEnumerable<string?> descricoesDosCuidados)
        => !string.IsNullOrWhiteSpace(historico)
           || !string.IsNullOrWhiteSpace(exameFisico)
           || !string.IsNullOrWhiteSpace(avaliacao)
           || titulosDosDiagnosticos.Any(t => !string.IsNullOrWhiteSpace(t))
           || descricoesDosCuidados.Any(c => !string.IsNullOrWhiteSpace(c));

    /// <summary>
    /// Nada foi digitado desta passagem. ⚠️ Olha TUDO o que grava — sinais vitais, acesso,
    /// alergia e as cinco etapas —, não só o texto: é a lição da parcela 74
    /// (<c>SessaoEmBranco</c> decide se a tela pergunta; usá-la para decidir se grava descartou
    /// a sessão mais comum da casa).
    /// </summary>
    public static bool CompositorEmBranco(
        string? texto, string? alergiaObservada, SinaisVitais? sinais, AcessoVenoso? acesso,
        string? historico, string? exameFisico, string? avaliacao,
        IEnumerable<string?> titulosDosDiagnosticos, IEnumerable<string?> descricoesDosCuidados)
        => string.IsNullOrWhiteSpace(texto)
           && string.IsNullOrWhiteSpace(alergiaObservada)
           && sinais is null
           && acesso is null
           && !ConsultaTemConteudo(historico, exameFisico, avaliacao, titulosDosDiagnosticos, descricoesDosCuidados);

    /// <summary>
    /// A lista de passagens, da mais recente para a mais antiga; as corrigidas e canceladas
    /// aparecem MARCADAS, nunca sumindo (é o que a auditoria de enfermagem procura).
    /// </summary>
    /// <param name="mostrarData">Fora da folha de infusão a lista é do paciente e precisa da data.</param>
    /// <param name="agendamentoAberto">O horário em curso — o que acende "DESTA SESSÃO".</param>
    public static IReadOnlyList<LinhaDaPassagem> Linhas(
        IEnumerable<EvolucaoEnfermagem> lista, bool mostrarData, int? agendamentoAberto)
    {
        var todas = lista.ToList();
        var substituidas = todas
            .Where(e => e.RetificaEvolucaoId is not null)
            .Select(e => e.RetificaEvolucaoId!.Value)
            .ToHashSet();

        return [.. todas
            .OrderByDescending(e => e.Data).ThenByDescending(e => e.Hora).ThenByDescending(e => e.Id)
            .Select(e => Linha(e, substituidas.Contains(e.Id), mostrarData, agendamentoAberto))];
    }

    public static LinhaDaPassagem Linha(
        EvolucaoEnfermagem e, bool substituida, bool mostrarData, int? agendamentoAberto)
    {
        var marca = e.Cancelada
            ? $"CANCELADA — {e.MotivoCancelamento}"
            : substituida
                ? "CORRIGIDA — vale o registro seguinte"
                : e.EhRetificacao
                    ? $"corrige o registro anterior — {e.MotivoRetificacao}"
                    : null;

        return new LinhaDaPassagem(
            Id: e.Id,
            Hora: e.Hora.ToString("HH\\:mm", CultureInfo.InvariantCulture),
            Data: mostrarData ? e.Data.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : string.Empty,
            Texto: e.Texto,
            // Sinais e ACESSO na mesma linha: o acesso é o achado que a próxima punção consulta.
            SinaisVitais: e.AcessoResumo is { } acesso
                ? (e.SinaisVitaisResumidos is { } sv ? $"{sv} · acesso: {acesso}" : $"acesso: {acesso}")
                : e.SinaisVitaisResumidos,
            Autor: string.IsNullOrWhiteSpace(e.AutorConselho) ? e.AutorNome : $"{e.AutorNome} · {e.AutorConselho}",
            Registro: $"registrado {e.RegistradoEm.ToString("dd/MM/yyyy HH\\:mm", CultureInfo.InvariantCulture)}",
            Intercorrencia: e.Intercorrencia,
            Cancelada: e.Cancelada,
            Substituida: substituida,
            // Nulo dos dois lados NÃO casa: sem horário aberto nada é "desta sessão".
            DestaSessao: agendamentoAberto is { } aberto && e.AgendamentoId == aberto,
            Marca: marca,
            Folha: e.Prescricao?.Numero,
            AgendamentoId: e.AgendamentoId);
    }

    /// <summary>
    /// As sessões do dia que podem receber a passagem (Agendado ou Realizado), na ordem do
    /// dia, com quem atende e se já foram concluídas — a janela "Vincular à sessão" do WPF.
    /// </summary>
    public static IReadOnlyList<SessaoParaVincular> SessoesParaVincular(IEnumerable<Agendamento> doDia)
        => [.. doDia
            .Where(a => a.Status is StatusAgendamento.Agendado or StatusAgendamento.Realizado)
            .OrderBy(a => a.DataHora)
            .Select(a => new SessaoParaVincular(a.Id,
                $"{a.DataHora.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)} · sessão #{a.Id} · "
                + $"{a.Profissional?.Rotulo ?? "Responsável não informado"} · "
                + (a.FimAtendimentoEm is not null ? "Concluída" : "Em aberto")))];

    // ==================== O catálogo em janela ====================

    public const string TituloDiagnosticos = "Diagnósticos de enfermagem";

    public const string ExplicacaoDiagnosticos =
        "Lista desta clínica — NÃO é a NANDA-I, que é licenciada. Escolher um traz junto os "
        + "cuidados que ele costuma pedir, para você desmarcar o que não vale. O que não estiver "
        + "aqui se escreve à mão.";

    public const string TituloCuidados = "Cuidados de enfermagem";

    public const string ExplicacaoCuidados =
        "O que a ENFERMAGEM prescreve. A frequência vem sugerida e é sua para ajustar: "
        + "“verificar o acesso” sem dizer de quanto em quanto tempo é lembrete, não prescrição. "
        + "O que não estiver aqui se escreve à mão.";

    /// <summary>Os diagnósticos do catálogo com o que a caixinha antiga nunca mostrava.</summary>
    public static IReadOnlyList<ItemDoCatalogoEnfermagem> Diagnosticos(string? termo)
        => [.. CatalogoEnfermagem.BuscarDiagnosticos(termo)
            .Select(d => new ItemDoCatalogoEnfermagem(d.Codigo, d.Titulo,
                $"Resultado esperado: {d.ResultadoEsperado}"
                + (d.Cuidados.Count > 0 ? $"  ·  traz {d.Cuidados.Count} cuidado(s)" : string.Empty)))];

    public static IReadOnlyList<ItemDoCatalogoEnfermagem> Cuidados(string? termo)
        => [.. CatalogoEnfermagem.BuscarCuidados(termo)
            .Select(c => new ItemDoCatalogoEnfermagem(c.Codigo, c.Descricao,
                string.IsNullOrWhiteSpace(c.FrequenciaSugerida) ? null : $"Frequência sugerida: {c.FrequenciaSugerida}"))];

    /// <summary>
    /// A lista filtrada DIZ que está filtrada — "6 no catálogo" sozinho faria concluir que a
    /// clínica só tem seis cuidados cadastrados.
    /// </summary>
    public static string ResumoDoCatalogo(string? termo, int itens, int total, int noPlano)
    {
        var t = (termo ?? string.Empty).Trim();
        var jaNoPlano = noPlano > 0 ? $", {noPlano} já no plano." : ".";
        return t.Length > 0
            ? $"“{t}” — {itens} de {total} no catálogo{jaNoPlano}"
            : $"{itens} no catálogo{jaNoPlano}";
    }
}

/// <summary>Uma linha do PLANO DE CUIDADOS do dia — a etapa 4 da COFEN na tela de quem executa.</summary>
public sealed record LinhaDoPlano(int CuidadoId, string Redacao, string Registro, string Selo, bool Pendente);

/// <summary>
/// O PLANO DE CUIDADOS DE HOJE (out/2026 — as frases do <c>PlanoDeCuidadosViewModel</c> WPF).
/// As regras de pendência (SOS não é trabalho atrasado, a corrigida aparece marcada) moram
/// em <see cref="CuidadoDoDia"/>; aqui mora o que a tela escreve sobre elas.
/// </summary>
public static class PlanoDeCuidadosNaTela
{
    public const string FalhaDeLeitura = "Não foi possível ler o plano de cuidados.";

    public const string PerguntaNaoFeitoTitulo = "Por que não foi realizado?";

    public static string PerguntaNaoFeito(string redacao)
        => $"{redacao}\n\nEx.: paciente ausente, recusou, material em falta, condição não ocorreu.";

    public static string HoraInvalida(string? hora)
        => $"Hora inválida (\"{hora}\"). Escreva no formato 14:30 — é o horário em que o cuidado "
           + "foi executado, não o de agora.";

    public static IReadOnlyList<LinhaDoPlano> Linhas(PlanoDeCuidadosDoDia? plano)
        => plano is null ? [] : [.. plano.Cuidados.Select(Linha)];

    public static LinhaDoPlano Linha(CuidadoDoDia c) => new(
        c.CuidadoId,
        c.Redacao,
        // A CORRIGIDA aparece marcada, nunca sumindo — apagar da vista o registro corrigido é
        // o gesto que a auditoria de enfermagem procura.
        string.Join("\n", c.Checagens.Select(Descrever)),
        // O "se necessário" tem selo PRÓPRIO: não é trabalho atrasado, é a condição que não ocorreu.
        c.Pendente ? "aguardando"
            : c.SeNecessario && c.Vigentes.Count == 0 ? "se necessário"
            : string.Empty,
        c.Pendente);

    private static string Descrever(ChecagemCuidado x)
    {
        var texto = x.Linha;
        if (!string.IsNullOrWhiteSpace(x.Justificativa)) texto += $" — {x.Justificativa}";
        if (x.EhRetificacao) texto += $" — corrige o anterior: {x.MotivoRetificacao}";
        return texto;
    }

    public static string Resumo(PlanoDeCuidadosDoDia? plano) => plano is null
        ? "Nenhum plano de cuidados prescrito para este paciente."
        : $"{plano.Cuidados.Count} cuidado(s) prescrito(s) em "
          + $"{plano.PrescritoEm.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} por {plano.PrescritoPor} — "
          + (plano.Pendentes == 0 ? "tudo registrado hoje." : $"{plano.Pendentes} aguardando registro hoje.");

    public static string Registrado(string redacao, SituacaoChecagem situacao, TimeOnly hora)
        => situacao == SituacaoChecagem.Realizado
            ? $"Registrado: {redacao} — {hora:HH\\:mm}."
            : $"Registrado como NÃO realizado: {redacao} — {hora:HH\\:mm}.";
}

/// <summary>
/// A TELA ENFERMAGEM e a SEÇÃO "Atendimento de enfermagem" (out/2026 — as frases do
/// <c>EnfermagemViewModel</c> e do <c>AtendimentoEnfermagemViewModel</c> WPF, num lugar só).
/// </summary>
public static class EnfermagemNaTela
{
    /// <summary>A página da lista "Todos os pacientes" — a mesma do SeletorPacienteViewModel WPF.</summary>
    public const int TamanhoDaPagina = 100;

    public const string SemFicha =
        "O seu acesso não permite ver a ficha dos pacientes. Peça em Acessos a permissão \"Ver ficha do paciente\".";

    public const string SemProntuario =
        "O seu acesso não permite ler o prontuário. Peça em Acessos a permissão \"Ver prontuário\".";

    public const string PortasNaoVerificadas =
        "Não foi possível conferir o termo do dia e a folha de infusão deste paciente. Confira na "
        + "Sala de infusão antes de administrar qualquer coisa.";

    public const string ContextoNaoVerificado =
        "Não foi possível conferir alergias e contexto clínico deste paciente. Confira no "
        + "prontuário antes de administrar qualquer coisa.";

    /// <summary>
    /// Quem não escreve pelo lado Y lê a passagem em modo LEITURA — o XY da parcela 72 — e a
    /// tela DIZ por que os campos estão apagados.
    /// </summary>
    public static string? AvisoDeLeitura(bool podeRegistrar) => podeRegistrar
        ? null
        : "Você está lendo o registro da enfermagem. Escrever aqui exige a permissão "
          + "“Registrar evolução de enfermagem”, que a direção concede em Acessos.";

    /// <summary>O resumo da listagem "Todos os pacientes" (paginada) — ou de uma busca.</summary>
    public static string ResumoDaBusca(string? termo, int quantidade, int pagina)
        => string.IsNullOrWhiteSpace(termo)
            ? $"Todos os pacientes — {quantidade} na página {pagina}."
            : $"Busca “{termo.Trim()}” — {quantidade} paciente(s) na página {pagina}.";

    /// <summary>Idade e convênio — o que se sabe sem ir ao banco de novo.</summary>
    public static string ContextoBasico(Paciente paciente, DateOnly hoje)
    {
        var partes = new List<string>();
        // A idade só entra quando é plausível: "1851 anos" gasta a linha dizendo o que ninguém usa.
        if (IdadeDoPaciente.Anos(paciente.DataNascimento, hoje) is { } idade)
            partes.Add(idade == 1 ? "1 ano" : $"{idade} anos");
        if (!string.IsNullOrWhiteSpace(paciente.ConvenioNome)) partes.Add(paciente.ConvenioNome);
        return string.Join(" · ", partes);
    }

    /// <summary>A alergia do contexto, separada porque é a única que muda de cor (o ícone de alerta é do <c>Aviso</c>).</summary>
    public static string? Alerta(IEnumerable<ProblemaPaciente> alergias)
        => ProblemaPacienteService.ResumirAlergias(alergias);

    /// <summary>A última aferição — o valor isolado quase não diz nada; a diferença diz.</summary>
    public static string? UltimaAfericao(IEnumerable<EvolucaoEnfermagem> evolucoes)
    {
        var ultima = evolucoes.FirstOrDefault(e => !e.Cancelada && e.TemSinaisVitais);
        return ultima?.SinaisVitaisResumidos is { } sinais
            ? $"{sinais} às {ultima.Hora.ToString("HH\\:mm", CultureInfo.InvariantCulture)} de {ultima.Data.ToString("dd/MM", CultureInfo.InvariantCulture)}"
            : null;
    }

    /// <summary>O termo do dia no contexto: "a conferir" quando o paciente já assinou no celular.</summary>
    public static string ContextoDoTermo(SituacaoTermo pendente) => pendente.AssinaturaRemotaAguardaConferencia
        ? $"termo assinado no celular, a conferir: {pendente.NomeDoTermo}"
        : $"termo pendente: {pendente.NomeDoTermo}";

    /// <summary>O botão da folha na SEÇÃO do Consultório: número e o que falta.</summary>
    public static string FolhaDaSecao(string numero, int pendentes) => $"Folha {numero} · {pendentes} item(ns)";

    public static string ContextoDaFolha(string numero, int pendentes) => $"infusão {numero} · {pendentes} item(ns) aguardando";

    /// <summary>
    /// O horário DESTE paciente hoje, para a passagem nascer ligada à sessão (o mesmo critério do
    /// EntregaDoPaciente WPF): quem já está na sala VENCE — com duas sessões no mesmo dia, o
    /// registro pertence à que está acontecendo, e presumir a primeira o amarraria à sessão
    /// errada. Cancelado e substituído não contam.
    /// </summary>
    public static Agendamento? HorarioDeHoje(IEnumerable<Agendamento> agenda, int pacienteId)
    {
        var doPaciente = agenda
            .Where(a => a.PacienteId == pacienteId
                        && a.Status is not (StatusAgendamento.Cancelado or StatusAgendamento.Substituido))
            .OrderBy(a => a.DataHora)
            .ToList();
        return doPaciente.FirstOrDefault(a => a.InicioAtendimentoEm is not null && a.FimAtendimentoEm is null)
               ?? doPaciente.FirstOrDefault();
    }

    /// <summary>
    /// As partes CLÍNICAS da linha de contexto (medicação em uso, última aferição, peso), na
    /// ordem em que a tela as escreve. O que não existe não entra — "em uso: " vazio gasta a
    /// linha dizendo nada. O peso sai em pt-BR FIXO: a linha é lida, e "72.5" numa clínica
    /// brasileira é dado com cara de erro.
    /// </summary>
    public static IReadOnlyList<string> PartesClinicas(
        IEnumerable<ProblemaPaciente> medicacoesEmUso,
        IEnumerable<EvolucaoEnfermagem> evolucoes,
        IEnumerable<MedidaClinica> ultimasMedidas)
    {
        var partes = new List<string>();
        var emUso = medicacoesEmUso.Select(m => m.Rotulo).Where(r => !string.IsNullOrWhiteSpace(r)).ToList();
        if (emUso.Count > 0) partes.Add("em uso: " + string.Join(", ", emUso));
        if (UltimaAfericao(evolucoes) is { } afericao) partes.Add(afericao);
        if (ultimasMedidas.FirstOrDefault(m => m.TipoCodigo == Clinica.Domain.Medidas.CatalogoMedidas.Peso) is { } peso)
            partes.Add(string.Format(PtBr, "peso {0:0.#} kg ({1:dd/MM/yyyy})", peso.Valor, peso.Data));
        return partes;
    }

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Junta o contexto básico às partes lidas, pulando o que não existe.</summary>
    public static string JuntarContexto(string? basico, IEnumerable<string?> partes)
        => string.Join(" · ", new[] { basico }.Concat(partes).Where(p => !string.IsNullOrWhiteSpace(p)));
}
