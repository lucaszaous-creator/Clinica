namespace Clinica.Domain.Entities;

/// <summary>
/// Entidade central do sistema: cada código/guia faturável.
/// Os campos de BAIXA registram quando a secretária efetivou a guia — é o que impede
/// que o 2º código seja esquecido (a causa da perda de faturas no sistema antigo).
/// FATURAMENTO apenas: não há nenhum campo de valor recebido / recebíveis.
/// </summary>
public class CodigoFaturamento
{
    public int Id { get; set; }

    public int AtendimentoId { get; set; }
    public Atendimento? Atendimento { get; set; }

    public TipoCodigo Tipo { get; set; }

    /// <summary>
    /// Especialidade da consulta: preenchida na consulta avulsa (informada no lançamento) e na
    /// Petrobras (acupuntura faturada como consulta de especialidade, pela rotação).
    /// </summary>
    public Especialidade? Especialidade { get; set; }

    /// <summary>Código da especialidade no catálogo (permite especialidades criadas pela clínica). Null = embutida.</summary>
    public string? EspecialidadeCodigo { get; set; }

    public OrdemCodigo Ordem { get; set; }

    /// <summary>Data a partir da qual a guia pode/deve ser faturada. Para o 2º código = data do atendimento + 24h.</summary>
    public DateOnly DataPrevistaFaturamento { get; set; }

    public FormaObtencao FormaObtencao { get; set; }

    public StatusCodigo Status { get; set; } = StatusCodigo.Aberto;

    /// <summary>Texto explicativo da conduta (ex.: instrução de inversão de datas do BSV).</summary>
    public string? Descricao { get; set; }

    // ---------- Campos de BAIXA (registro de faturamento) ----------

    /// <summary>Quando a secretária registrou que esta guia foi efetivamente faturada. Nulo = ainda em aberto.</summary>
    public DateOnly? DataBaixa { get; set; }

    /// <summary>Número/código real da guia gerada no sistema do convênio.</summary>
    public string? NumeroGuiaReal { get; set; }

    public string? UsuarioBaixa { get; set; }
    public string? ObservacaoBaixa { get; set; }

    // ---------- Observação da pendência (por que ainda não foi baixada) ----------

    /// <summary>
    /// Anotação do responsável quando a guia NÃO pôde ser baixada na hora (ex.: "portal
    /// da Unimed fora do ar", "aguardando o paciente enviar o QR Code"). Fica visível na
    /// pendência para, no futuro, saber por que o caso continua em aberto. Independe da
    /// baixa — é sobre o que está impedindo a baixa.
    /// </summary>
    public string? ObservacaoPendencia { get; set; }

    /// <summary>Quando a observação da pendência foi anotada/atualizada (para exibir "há N dias").</summary>
    public DateTime? ObservacaoPendenciaEm { get; set; }

    // ---------- Não conformidade (fechamento de ciclo — "rodar as pendências") ----------

    /// <summary>
    /// Justificativa registrada quando, ao rodar as pendências, a guia não pôde ser baixada dentro do
    /// prazo do ciclo. Marcar como não conformidade SILENCIA a pendência (sai do painel e dos avisos)
    /// e deixa o motivo documentado no relatório. Só volta a ser pendência se for reaberta manualmente.
    /// </summary>
    public string? NaoConformidadeJustificativa { get; set; }

    /// <summary>Quando a guia foi marcada como não conformidade (para exibir no relatório).</summary>
    public DateTime? NaoConformidadeEm { get; set; }

    // ---------- Lote TISS (exportação à operadora) ----------

    /// <summary>Lote TISS em que a guia foi exportada. Nulo = ainda não entrou em lote.</summary>
    public int? LoteTissId { get; set; }
    public LoteTiss? Lote { get; set; }

    // ---------- Glosa (recusa do convênio) ----------

    public StatusGlosa Glosa { get; set; } = StatusGlosa.SemGlosa;
    public DateOnly? DataGlosa { get; set; }
    public string? MotivoGlosa { get; set; }

    /// <summary>Código do motivo na tabela de glosas da ANS (padroniza o registro).</summary>
    public string? MotivoGlosaCodigo { get; set; }

    /// <summary>Data-limite para recorrer da glosa junto à operadora (prazo contratual, padrão 30 dias).</summary>
    public DateOnly? DataLimiteRecurso { get; set; }

    public DateOnly? DataReapresentacao { get; set; }

    /// <summary>True quando a baixa já foi registrada.</summary>
    public bool Baixado => DataBaixa.HasValue;

    /// <summary>Glosas ativas (glosada ou reapresentada, ainda não recuperadas).</summary>
    public bool GlosaEmAberto => Glosa is StatusGlosa.Glosada or StatusGlosa.Reapresentada;

    /// <summary>Guia dispensada da pendência por ter sido justificada como não conformidade numa rodada.</summary>
    public bool EmNaoConformidade => Status == StatusCodigo.NaoConformidade;

    /// <summary>
    /// Está pendente quando ainda não teve baixa, é faturável e sua data prevista já chegou (na data de referência).
    /// Códigos NaoAplicavel (não faturáveis) e NaoConformidade (justificados na rodada) nunca são pendência.
    /// </summary>
    public bool EstaPendente(DateOnly referencia) =>
        !Baixado && Status != StatusCodigo.NaoAplicavel && Status != StatusCodigo.NaoConformidade
        && DataPrevistaFaturamento <= referencia;

    /// <summary>
    /// O prazo de decisão da rodada venceu: a guia continua pendente e já se passaram
    /// <paramref name="prazoDias"/> dias (padrão 10) desde o ATENDIMENTO do paciente. A partir daí o
    /// sistema EXIGE uma decisão — baixa ou não conformidade — e bloqueia o uso até que ela seja tomada.
    /// Sem o atendimento carregado, conta a partir da data prevista de faturamento.
    /// </summary>
    public bool PrazoDecisaoVencido(DateOnly referencia, int prazoDias)
    {
        if (!EstaPendente(referencia)) return false;
        var origem = Atendimento?.Data ?? DataPrevistaFaturamento;
        return referencia.DayNumber - origem.DayNumber >= prazoDias;
    }

    /// <summary>Aplica a baixa (registro de faturamento).</summary>
    public void DarBaixa(DateOnly data, string? numeroGuia, string? usuario, string? observacao)
    {
        DataBaixa = data;
        NumeroGuiaReal = numeroGuia;
        UsuarioBaixa = usuario;
        ObservacaoBaixa = observacao;
        Status = StatusCodigo.Baixado;
    }

    /// <summary>
    /// Registra (ou limpa) a observação sobre por que a guia ainda não foi baixada.
    /// Texto vazio limpa a anotação. Carimba a data para a pendência mostrar desde quando.
    /// </summary>
    public void RegistrarObservacaoPendencia(string? observacao)
    {
        var texto = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
        ObservacaoPendencia = texto;
        ObservacaoPendenciaEm = texto is null ? null : DateTime.Now;
    }

    /// <summary>
    /// Marca a guia como não conformidade ao rodar as pendências: registra a justificativa e SILENCIA
    /// a pendência (deixa de aparecer no painel e nos avisos). Só é possível numa guia ainda em aberto.
    /// </summary>
    public void MarcarNaoConformidade(string justificativa)
    {
        if (Baixado)
            throw new InvalidOperationException("Uma guia já baixada não pode virar não conformidade.");
        if (string.IsNullOrWhiteSpace(justificativa))
            throw new InvalidOperationException("A não conformidade exige uma justificativa.");

        Status = StatusCodigo.NaoConformidade;
        NaoConformidadeJustificativa = justificativa.Trim();
        NaoConformidadeEm = DateTime.Now;
    }

    /// <summary>
    /// Reabre uma não conformidade (quando aparece solução): a guia volta a ser pendência e pode ser
    /// baixada. Limpa os dados da não conformidade — a trilha de auditoria guarda o histórico.
    /// </summary>
    public void ReabrirNaoConformidade()
    {
        if (Status != StatusCodigo.NaoConformidade) return;
        Status = StatusCodigo.Aberto;
        NaoConformidadeJustificativa = null;
        NaoConformidadeEm = null;
    }

    /// <summary>
    /// Estorna a baixa (reabre a pendência) — usado quando a baixa foi feita por engano.
    /// Limpa os dados de baixa e registra a nota de estorno para auditoria.
    /// </summary>
    public void EstornarBaixa(string? motivo, string? usuario)
    {
        if (!Baixado) return;

        var guiaAnterior = NumeroGuiaReal;
        DataBaixa = null;
        NumeroGuiaReal = null;
        UsuarioBaixa = null;
        Status = StatusCodigo.Aberto;

        var quem = string.IsNullOrWhiteSpace(usuario) ? "?" : usuario;
        var quando = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        var motivoTxt = string.IsNullOrWhiteSpace(motivo) ? "" : $" — {motivo}";
        ObservacaoBaixa = $"[Estornado em {quando} por {quem}{motivoTxt}] (guia anterior: {guiaAnterior})";
    }

    /// <summary>Registra a glosa de uma guia já faturada (baixada).</summary>
    public void RegistrarGlosa(DateOnly data, string? motivo, string? motivoCodigo = null, int prazoRecursoDias = 30)
    {
        if (!Baixado)
            throw new InvalidOperationException("Só é possível glosar uma guia já faturada (com baixa).");
        Glosa = StatusGlosa.Glosada;
        DataGlosa = data;
        MotivoGlosa = motivo;
        MotivoGlosaCodigo = motivoCodigo;
        DataLimiteRecurso = data.AddDays(prazoRecursoDias);
        DataReapresentacao = null;
    }

    /// <summary>Dias restantes do prazo de recurso (negativo = prazo estourado). Nulo quando não há glosa em aberto.</summary>
    public int? DiasParaFimRecurso(DateOnly referencia)
        => GlosaEmAberto && DataLimiteRecurso is { } limite ? limite.DayNumber - referencia.DayNumber : null;

    /// <summary>Marca a guia glosada como reapresentada ao convênio.</summary>
    public void Reapresentar(DateOnly data)
    {
        if (Glosa != StatusGlosa.Glosada)
            throw new InvalidOperationException("Só é possível reapresentar uma guia glosada.");
        Glosa = StatusGlosa.Reapresentada;
        DataReapresentacao = data;
    }

    /// <summary>Marca a glosa como recuperada (aceita após reapresentação).</summary>
    public void MarcarGlosaRecuperada()
    {
        if (Glosa is StatusGlosa.SemGlosa or StatusGlosa.Recuperada)
            return;
        Glosa = StatusGlosa.Recuperada;
    }
}
