using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>De qual lado da clínica a folha vem.</summary>
public enum NaturezaFolha
{
    /// <summary>Papel do atendimento: sai assinado por um profissional.</summary>
    Clinico,

    /// <summary>Papel do dinheiro: comprova ou propõe valor.</summary>
    Financeiro,

    /// <summary>Papel da gestão: conferência de um período, não de uma pessoa.</summary>
    Gerencial
}

/// <summary>
/// O que a folha PRECISA para poder ser gerada. É o campo que faz a tela saber se
/// habilita o botão ou explica o que falta — botão que abre e só então diz "escolha um
/// paciente" faz a pessoa descobrir o requisito errando.
/// </summary>
public enum ExigenciaFolha
{
    /// <summary>Um paciente escolhido. As sete folhas clínicas e o orçamento.</summary>
    Paciente,

    /// <summary>Um paciente e o que já está no prontuário dele (evolução, anamnese, consentimento).</summary>
    PacienteComProntuario,

    /// <summary>Um período (de/até). O fechamento.</summary>
    Periodo,

    /// <summary>
    /// Um lançamento já realizado no caixa. O recibo comprova dinheiro que entrou, e
    /// emiti-lo sem apontar para o lançamento permitiria dois recibos do mesmo pagamento.
    /// </summary>
    LancamentoNoCaixa
}

/// <summary>
/// Uma das folhas que a clínica emite. O catálogo existe para a tela **listar as nove**
/// em vez de a pessoa ter de saber que receita sai da ficha do paciente, recibo sai do
/// caixa e fechamento saía só do app de faturamento.
/// </summary>
public sealed record FolhaCatalogo(
    string Chave,
    string Rotulo,
    string Descricao,
    NaturezaFolha Natureza,
    ExigenciaFolha Exigencia)
{
    /// <summary>Preenchido nas sete clínicas; null nas outras.</summary>
    public TipoDocumentoClinico? TipoClinico { get; init; }

    /// <summary>Preenchido no recibo e no orçamento; null nas outras.</summary>
    public TipoDocumentoFinanceiro? TipoFinanceiro { get; init; }

    /// <summary>
    /// O sistema monta o conteúdo a partir do prontuário, em vez de alguém escrever.
    /// </summary>
    public bool MontadaDoProntuario => Exigencia == ExigenciaFolha.PacienteComProntuario;
}

/// <summary>
/// Uma folha JÁ emitida, clínica ou financeira, na mesma lista.
///
/// Existe porque as duas moram em tabelas diferentes e ninguém conseguia responder "que
/// papéis saíram este mês?" — a pergunta que se faz quando o paciente liga pedindo a
/// segunda via e não sabe dizer de quê.
/// </summary>
public sealed record FolhaEmitida(
    int DocumentoId,
    NaturezaFolha Natureza,
    string Numero,
    string CodigoVerificacao,
    string FolhaRotulo,
    string? Paciente,
    string? Profissional,
    DateOnly Data,
    DateTime CriadoEm,
    string? CriadoPor,
    bool Cancelado,
    string? MotivoCancelamento)
{
    /// <summary>Valor, só nas financeiras. Null na clínica — receita não tem preço.</summary>
    public decimal? Valor { get; init; }

    /// <summary>
    /// Até quando o link publicado fica no ar (parcela 53). <c>null</c> quando o documento
    /// nunca foi publicado ou já saiu do ar.
    /// </summary>
    public DateOnly? PublicadoAte { get; init; }

    /// <summary>
    /// O documento já teve link algum dia — ou seja, tem token e um PDF assinado que o
    /// carrega no QR. É o que separa "dá para republicar" de "nunca houve link", e a
    /// diferença importa: republicar reusa o MESMO token, então o QR que o paciente já tem
    /// impresso volta a funcionar.
    /// </summary>
    public bool JaTeveLink { get; init; }
}

/// <summary>Quantas de cada folha saíram no período.</summary>
public sealed record ResumoFolhas(
    int Emitidas,
    int Canceladas,
    IReadOnlyList<(string Folha, int Vezes)> PorFolha)
{
    public bool Vazio => Emitidas == 0;
}

/// <summary>
/// A central de documentos (parcela 24).
///
/// O mockup mostrava NOVE folhas como um conjunto — receituário, atestado, declaração de
/// comparecimento, solicitação de exames, relatório de evolução, ficha de anamnese, recibo,
/// orçamento e fechamento do período. No sistema as nove existiam e **nenhuma estava no
/// mesmo lugar**: quatro saíam de uma janela dentro da ficha do paciente, três só do botão
/// certo na aba certa dessa ficha, o recibo do Caixa, o orçamento só de dentro de um pacote
/// vendido — e o fechamento do período **só do app de faturamento**, que a suíte não abre.
///
/// Quem foi treinado no mockup procurava "Documentos" e não achava. Não faltava capacidade,
/// faltava porta.
///
/// Este serviço é a porta: o catálogo das nove (com o que cada uma exige), a lista unificada
/// do que já saiu, e o fechamento do período — que precisava dos dados do prestador e por
/// isso vivia preso ao outro app.
///
/// Ele **não emite folha clínica**: quem emite é o <see cref="DocumentoClinicoService"/>,
/// que valida o profissional que assina, resolve a numeração do ano e grava o conteúdo na
/// emissão. Reimplementar aqui daria dois caminhos para o mesmo papel, e só um receberia a
/// próxima correção — a mesma razão pela qual receber dinheiro passa sempre pelo
/// <see cref="FinanceiroService"/>.
/// </summary>
public sealed class CentralDocumentosService
{
    private readonly IClinicaRepositorio _repo;
    private readonly FechamentoPdfService _fechamento;
    private readonly ParametrosService _parametros;

    public CentralDocumentosService(
        IClinicaRepositorio repo,
        FechamentoPdfService fechamento,
        ParametrosService parametros)
    {
        _repo = repo;
        _fechamento = fechamento;
        _parametros = parametros;
    }

    // ==================== O catálogo ====================

    public const string ChaveFechamentoPeriodo = "fechamento-periodo";

    /// <summary>
    /// As nove folhas do mockup, na ordem em que a clínica as usa: primeiro o que sai
    /// junto com o atendimento, depois o que sai no balcão, por último o da gestão.
    ///
    /// É estático porque é o CATÁLOGO — a lista de papéis que a clínica emite não depende
    /// do banco. O que vem do banco é o que já foi emitido.
    /// </summary>
    public static IReadOnlyList<FolhaCatalogo> Catalogo { get; } =
    [
        new("receita", "Receituário",
            "Prescrição de fitoterápico, suplemento ou orientação de uso. Exige o profissional que assina.",
            NaturezaFolha.Clinico, ExigenciaFolha.Paciente)
        { TipoClinico = TipoDocumentoClinico.Receita },

        new("atestado", "Atestado",
            "Afastamento por N dias. O CID só sai impresso com autorização expressa do paciente.",
            NaturezaFolha.Clinico, ExigenciaFolha.Paciente)
        { TipoClinico = TipoDocumentoClinico.Atestado },

        new("comparecimento", "Declaração de comparecimento",
            "Prova de que o paciente esteve na clínica, com hora de chegada e de saída.",
            NaturezaFolha.Clinico, ExigenciaFolha.Paciente)
        { TipoClinico = TipoDocumentoClinico.Comparecimento },

        new("pedido-exame", "Solicitação de exames",
            "Lista de exames pedidos. Exige o profissional que assina.",
            NaturezaFolha.Clinico, ExigenciaFolha.Paciente)
        { TipoClinico = TipoDocumentoClinico.PedidoExame },

        new("relatorio-evolucao", "Relatório de evolução clínica",
            "Montado do prontuário: as sessões, a queixa e a evolução da dor. Não se digita.",
            NaturezaFolha.Clinico, ExigenciaFolha.PacienteComProntuario)
        { TipoClinico = TipoDocumentoClinico.RelatorioEvolucao },

        new("anamnese", "Ficha de anamnese",
            "Montada do cadastro e da primeira avaliação. Não se digita.",
            NaturezaFolha.Clinico, ExigenciaFolha.PacienteComProntuario)
        { TipoClinico = TipoDocumentoClinico.Anamnese },

        new("consentimento", "Termo de consentimento",
            "Montado do cadastro. Revogar não apaga: cancela-se com motivo e emite-se outro.",
            NaturezaFolha.Clinico, ExigenciaFolha.PacienteComProntuario)
        { TipoClinico = TipoDocumentoClinico.Consentimento },

        new("recibo", "Recibo de pagamento",
            "Comprova dinheiro que JÁ entrou. Nasce de um lançamento do caixa e fica apontando para ele.",
            NaturezaFolha.Financeiro, ExigenciaFolha.LancamentoNoCaixa)
        { TipoFinanceiro = TipoDocumentoFinanceiro.Recibo },

        new("orcamento", "Orçamento",
            "Proposta do que vai custar, com validade. Os valores ficam gravados na emissão.",
            NaturezaFolha.Financeiro, ExigenciaFolha.Paciente)
        { TipoFinanceiro = TipoDocumentoFinanceiro.Orcamento },

        new(ChaveFechamentoPeriodo, "Fechamento do período",
            "Conferência da semana ou do mês: taxa de baixa, quebra por convênio, pendências vencidas nominais e glosas em aberto.",
            NaturezaFolha.Gerencial, ExigenciaFolha.Periodo)
    ];

    /// <summary>A folha pela chave, ou null se a chave não existe no catálogo.</summary>
    public static FolhaCatalogo? Folha(string chave)
        => Catalogo.FirstOrDefault(f => f.Chave == chave);

    /// <summary>Rótulo da folha correspondente a um documento clínico.</summary>
    public static string RotularClinico(TipoDocumentoClinico tipo)
        => Catalogo.FirstOrDefault(f => f.TipoClinico == tipo)?.Rotulo
           ?? TipoDocumentoInfo.Rotular(tipo);

    /// <summary>Rótulo da folha correspondente a um documento financeiro.</summary>
    public static string RotularFinanceiro(TipoDocumentoFinanceiro tipo)
        => Catalogo.FirstOrDefault(f => f.TipoFinanceiro == tipo)?.Rotulo
           ?? TipoDocumentoFinanceiroInfo.Rotular(tipo);

    // ==================== O que já saiu ====================

    /// <summary>
    /// As folhas emitidas no período, clínicas e financeiras na MESMA lista, da mais
    /// recente para a mais antiga.
    ///
    /// **Cancelada aparece marcada, nunca sumindo.** Documento não se apaga neste sistema:
    /// cancela-se com motivo e emite-se outro. Esconder a cancelada faria a lista mentir
    /// sobre o que o paciente levou para casa, que é justamente o que a segunda via precisa
    /// reproduzir.
    /// </summary>
    /// <param name="chaveFolha">Uma folha só, ou null para todas.</param>
    /// <param name="pacienteId">Um paciente só, ou null para todos.</param>
    public async Task<IReadOnlyList<FolhaEmitida>> EmitidasAsync(
        DateOnly inicio, DateOnly fim, string? chaveFolha = null, int? pacienteId = null,
        CancellationToken ct = default)
    {
        var folha = chaveFolha is null ? null : Folha(chaveFolha);

        // Chave que não existe no catálogo não devolve "tudo": devolve nada. Filtro que
        // silenciosamente vira "sem filtro" faz a pessoa concluir que há mais papel emitido
        // do que há.
        if (chaveFolha is not null && folha is null) return [];

        var lista = new List<FolhaEmitida>();

        // Fechamento do período não é gravado: é conferência, montada na hora a partir das
        // guias. Filtrar por ele devolve lista vazia, e isso é a verdade — não há segunda
        // via de um relatório que se refaz igual sempre que se pede.
        var soGerencial = folha is { Natureza: NaturezaFolha.Gerencial };
        if (soGerencial) return [];

        if (folha is null || folha.Natureza == NaturezaFolha.Clinico)
        {
            var clinicos = await _repo.DocumentosClinicosNoPeriodoAsync(
                inicio, fim, folha?.TipoClinico, pacienteId, ct);

            lista.AddRange(clinicos.Select(d => new FolhaEmitida(
                d.Id, NaturezaFolha.Clinico, d.Numero, d.CodigoVerificacao,
                RotularClinico(d.Tipo),
                d.Paciente?.Nome, d.Profissional?.Nome,
                d.Data, d.CriadoEm, d.CriadoPor,
                d.Cancelado, d.MotivoCancelamento)
            {
                PublicadoAte = d.PublicadoAte,
                JaTeveLink = !string.IsNullOrWhiteSpace(d.TokenPublicacao)
            }));
        }

        if (folha is null || folha.Natureza == NaturezaFolha.Financeiro)
        {
            var financeiros = await _repo.DocumentosFinanceirosAsync(inicio, fim, ct);

            lista.AddRange(financeiros
                .Where(d => folha?.TipoFinanceiro is null || d.Tipo == folha.TipoFinanceiro)
                .Where(d => pacienteId is null || d.PacienteId == pacienteId)
                .Select(d => new FolhaEmitida(
                    d.Id, NaturezaFolha.Financeiro, d.Numero, d.CodigoVerificacao,
                    RotularFinanceiro(d.Tipo),
                    // No financeiro quem recebe o papel não é sempre o paciente (pai,
                    // empresa, plano): o destinatário é quem vale na lista.
                    d.Destinatario, null,
                    d.Data, d.CriadoEm, d.CriadoPor,
                    d.Cancelado, d.MotivoCancelamento)
                { Valor = d.ValorTotal }));
        }

        return lista
            .OrderByDescending(f => f.Data)
            .ThenByDescending(f => f.CriadoEm)
            .ToList();
    }

    /// <summary>
    /// Quantas de cada folha saíram no período. Conta sobre o MESMO recorte da lista —
    /// dois números diferentes para o mesmo papel na mesma tela é pior do que um só.
    /// </summary>
    public async Task<ResumoFolhas> ResumoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default)
    {
        var emitidas = await EmitidasAsync(inicio, fim, ct: ct);
        if (emitidas.Count == 0) return new ResumoFolhas(0, 0, []);

        return new ResumoFolhas(
            emitidas.Count,
            emitidas.Count(f => f.Cancelado),
            emitidas.GroupBy(f => f.FolhaRotulo)
                .Select(g => (Folha: g.Key, Vezes: g.Count()))
                .OrderByDescending(x => x.Vezes)
                .ThenBy(x => x.Folha)
                .ToList());
    }

    // ==================== Fechamento do período ====================

    /// <summary>
    /// Gera o PDF do fechamento do período.
    ///
    /// Existe aqui porque, até esta parcela, o <see cref="FechamentoPdfService"/> só era
    /// chamado pelo app de faturamento — que está congelado e que a suíte não abre. A folha
    /// existia e ninguém da suíte conseguia tirá-la.
    ///
    /// Resolve os dados do prestador por dentro: são os mesmos que já estão em
    /// Configurações, e obrigar a tela a buscá-los seria repetir a busca em cada chamador.
    /// </summary>
    public async Task<byte[]> GerarFechamentoPeriodoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default)
    {
        if (fim < inicio)
            throw new InvalidOperationException(
                "O fim do período é anterior ao início — confira as datas.");

        var prestador = await _parametros.ObterPrestadorAsync(ct);
        return await _fechamento.GerarAsync(inicio, fim, prestador, ct);
    }
}
