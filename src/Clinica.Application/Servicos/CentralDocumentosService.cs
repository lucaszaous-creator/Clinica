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
    LancamentoNoCaixa,

    /// <summary>
    /// Um paciente e a ESCOLHA de qual termo ele vai assinar (parcela 66, 3ª rodada).
    ///
    /// O termo do procedimento não se escreve na janela genérica de documento — o texto e
    /// as declarações vêm de um MODELO, e é a cópia dele que o paciente lê e assina. Por
    /// isso ele tem caminho próprio: a tela pergunta qual termo é e abre a coleta no
    /// tablet, com a assinatura entrando no PDF antes do selo do profissional.
    ///
    /// ⚠️ Ele NÃO depende de haver procedimento marcado para hoje. A primeira versão desta
    /// parcela amarrava a coleta ao dia; a cliente pediu o contrário, e tem razão: quem
    /// aparece para tirar dúvidas semanas antes é justamente quem lê o termo com calma, e
    /// o dia do procedimento é quando ninguém tem tempo.
    /// </summary>
    TermoParaAssinar
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
    /// O acesso para VER esta folha — o cartão dela na central e as vias já emitidas
    /// (parcela 59).
    ///
    /// A direção viu a recepcionista alcançando os documentos. A porta da seção
    /// (<see cref="Permissao.VerDocumentos"/>) resolve metade; a outra metade é esta,
    /// porque <b>as dez folhas não são a mesma coisa</b>: receituário, atestado, pedido de
    /// exame, relatório de evolução e anamnese carregam DADO DE SAÚDE (art. 5º, II);
    /// declaração de comparecimento, termo de consentimento, recibo e orçamento não.
    ///
    /// Um bit só obrigaria a direção a escolher entre a recepcionista lendo a evolução de
    /// todo mundo e a recepcionista sem o recibo que ela emite dez vezes por dia — que é o
    /// bit sobrecarregado que a parcela 49 corrigiu, reaparecendo numa tela.
    ///
    /// ⚠️ Fica no CATÁLOGO, e não na tela, porque a central não é a única porta: o
    /// Receituário da Recepção e a aba Documentos da ficha emitem os mesmos papéis.
    /// Regra de acesso escrita numa tela só é o defeito recorrente do projeto na variante
    /// mais cara — a que dá a impressão de estar coberta.
    /// </summary>
    public required Permissao PermissaoVer { get; init; }

    /// <summary>
    /// O acesso para EMITIR esta folha. Mais forte que o de ver, na mesma família: quem lê
    /// o prontuário não necessariamente escreve nele, e receita e pedido de exame mandam
    /// alguém tomar ou fazer alguma coisa — daí <see cref="Permissao.Prescrever"/>.
    /// </summary>
    public required Permissao PermissaoEmitir { get; init; }

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
    /// De quem é a folha (parcela 62). Aditivo, <c>init</c> nulo — no financeiro o
    /// destinatário nem sempre é paciente do sistema. Existe para a REIMPRESSÃO de folha
    /// clínica poder registrar a trilha de acesso: 2ª via de receita é dado de saúde
    /// saindo em PDF, e a tela não tinha como dizer de quem.
    /// </summary>
    public int? PacienteId { get; init; }

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

    /// <summary>
    /// A chave da folha do catálogo (parcela 59). É por ela que a tela sabe qual acesso
    /// esta linha exige — para ver, e para cancelar ou republicar o link.
    ///
    /// Vazia só se um tipo novo entrar no enum sem entrar no catálogo; a lista então
    /// recusa a linha em vez de mostrá-la, que é a resposta segura para um papel cujo
    /// acesso ninguém declarou.
    /// </summary>
    public string Chave { get; init; } = string.Empty;
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
    /// ⚠️ <b>O acesso de cada folha não segue a NATUREZA dela</b> (parcela 59), e a
    /// diferença é o ponto: <c>NaturezaFolha.Clinico</c> diz de que lado da clínica a folha
    /// vem — é o que agrupa os cartões na tela —, e sete folhas são "do atendimento". Só
    /// que a <b>declaração de comparecimento</b> prova que a pessoa esteve aqui, e o
    /// <b>termo de consentimento</b> é montado do cadastro: nenhum dos dois diz o que ela
    /// tem, e os dois saem do BALCÃO o dia inteiro. Amarrar o acesso à natureza tiraria da
    /// recepção dois papéis que ela emite todo dia para proteger um dado que eles não
    /// carregam.
    public static IReadOnlyList<FolhaCatalogo> Catalogo { get; } =
    [
        new("receita", "Receituário",
            "Prescrição de fitoterápico, suplemento ou orientação de uso. Exige o profissional que assina.",
            NaturezaFolha.Clinico, ExigenciaFolha.Paciente)
        {
            TipoClinico = TipoDocumentoClinico.Receita,
            PermissaoVer = Permissao.VerProntuario,
            PermissaoEmitir = Permissao.Prescrever
        },

        new("atestado", "Atestado",
            "Afastamento por N dias. O CID só sai impresso com autorização expressa do paciente.",
            NaturezaFolha.Clinico, ExigenciaFolha.Paciente)
        {
            TipoClinico = TipoDocumentoClinico.Atestado,
            PermissaoVer = Permissao.VerProntuario,
            PermissaoEmitir = Permissao.Prescrever
        },

        // Comparecimento: prova que a pessoa ESTEVE aqui, com hora de chegada e de saída.
        // Não diz o que ela tem, e quem o entrega é o balcão — fica no cadastro.
        new("comparecimento", "Declaração de comparecimento",
            "Prova de que o paciente esteve na clínica, com hora de chegada e de saída.",
            NaturezaFolha.Clinico, ExigenciaFolha.Paciente)
        {
            TipoClinico = TipoDocumentoClinico.Comparecimento,
            PermissaoVer = Permissao.VerFichaPaciente,
            PermissaoEmitir = Permissao.EditarPaciente
        },

        new("pedido-exame", "Solicitação de exames",
            "Lista de exames pedidos. Exige o profissional que assina.",
            NaturezaFolha.Clinico, ExigenciaFolha.Paciente)
        {
            TipoClinico = TipoDocumentoClinico.PedidoExame,
            PermissaoVer = Permissao.VerProntuario,
            PermissaoEmitir = Permissao.Prescrever
        },

        // Montada do prontuário: emitir é IMPRIMIR o que já está lá, não escrever. Por
        // isso ver e emitir pedem o mesmo bit — exigir `EditarProntuario` para tirar uma
        // segunda via seria confundir ler com escrever.
        new("relatorio-evolucao", "Relatório de evolução clínica",
            "Montado do prontuário: as sessões, a queixa e a evolução da dor. Não se digita.",
            NaturezaFolha.Clinico, ExigenciaFolha.PacienteComProntuario)
        {
            TipoClinico = TipoDocumentoClinico.RelatorioEvolucao,
            PermissaoVer = Permissao.VerProntuario,
            PermissaoEmitir = Permissao.VerProntuario
        },

        new("anamnese", "Ficha de anamnese",
            "Montada do cadastro e da primeira avaliação. Não se digita.",
            NaturezaFolha.Clinico, ExigenciaFolha.PacienteComProntuario)
        {
            TipoClinico = TipoDocumentoClinico.Anamnese,
            PermissaoVer = Permissao.VerProntuario,
            PermissaoEmitir = Permissao.VerProntuario
        },

        // O termo de consentimento é montado do CADASTRO e colhido no balcão — é a peça da
        // LGPD, não do prontuário. Quem o entrega é quem recebe o paciente.
        new("consentimento", "Termo de consentimento",
            "Montado do cadastro. Revogar não apaga: cancela-se com motivo e emite-se outro.",
            NaturezaFolha.Clinico, ExigenciaFolha.PacienteComProntuario)
        {
            TipoClinico = TipoDocumentoClinico.Consentimento,
            PermissaoVer = Permissao.VerFichaPaciente,
            PermissaoEmitir = Permissao.EditarPaciente
        },

        // ⚠️ NÃO é o "consentimento" acima, que é o termo LGPD montado do cadastro. Este é o
        // consentimento do PROCEDIMENTO (o BSV, com a declaração de jejum), o texto vem de
        // um modelo escrito pela clínica, e quem assina é o PACIENTE. Duas folhas porque
        // são duas perguntas — ver o enum `TipoDocumentoClinico.TermoProcedimento`.
        //
        // `VerProntuario` para ver: o termo diz qual procedimento a pessoa vai fazer e o
        // que ela declarou sobre o próprio corpo — é dado de saúde (art. 5º, II), ao
        // contrário da declaração de comparecimento, que só prova presença.
        new("termo-procedimento", "Termo de procedimento",
            "Consentimento do procedimento e as declarações do paciente (jejum, medicações). "
            + "Assinado por ele na tela do balcão, e a assinatura entra no PDF antes do selo do profissional.",
            NaturezaFolha.Clinico, ExigenciaFolha.TermoParaAssinar)
        {
            TipoClinico = TipoDocumentoClinico.TermoProcedimento,
            PermissaoVer = Permissao.VerProntuario,
            PermissaoEmitir = Permissao.ColherAssinaturaPaciente
        },

        new("recibo", "Recibo de pagamento",
            "Comprova dinheiro que JÁ entrou. Nasce de um lançamento do caixa e fica apontando para ele.",
            NaturezaFolha.Financeiro, ExigenciaFolha.LancamentoNoCaixa)
        {
            TipoFinanceiro = TipoDocumentoFinanceiro.Recibo,
            PermissaoVer = Permissao.VerFinanceiro,
            PermissaoEmitir = Permissao.EditarFinanceiro
        },

        new("orcamento", "Orçamento",
            "Proposta do que vai custar, com validade. Os valores ficam gravados na emissão.",
            NaturezaFolha.Financeiro, ExigenciaFolha.Paciente)
        {
            TipoFinanceiro = TipoDocumentoFinanceiro.Orcamento,
            PermissaoVer = Permissao.VerFinanceiro,
            PermissaoEmitir = Permissao.EditarFinanceiro
        },

        // Conferência de um PERÍODO, não de uma pessoa: é relatório gerencial, e o bit é o
        // mesmo que guarda os relatórios do faturamento desde a parcela 49.
        new(ChaveFechamentoPeriodo, "Fechamento do período",
            "Conferência da semana ou do mês: taxa de baixa, quebra por convênio, pendências vencidas nominais e glosas em aberto.",
            NaturezaFolha.Gerencial, ExigenciaFolha.Periodo)
        {
            PermissaoVer = Permissao.VerIndicadores,
            PermissaoEmitir = Permissao.VerIndicadores
        }
    ];

    /// <summary>
    /// As folhas que ESTE conjunto de acessos alcança (parcela 59).
    ///
    /// Ponto único, no serviço e não na tela: a central, o Receituário da Recepção e a aba
    /// Documentos da ficha emitem os mesmos papéis, e regra de acesso escrita numa porta
    /// só é o defeito recorrente do projeto com o agravante de PARECER coberto.
    /// </summary>
    public static IReadOnlyList<FolhaCatalogo> CatalogoPara(Permissao acessos)
        => Catalogo.Where(f => acessos.HasFlag(f.PermissaoVer)).ToList();

    /// <summary>A folha pela chave, ou null se a chave não existe no catálogo.</summary>
    public static FolhaCatalogo? Folha(string chave)
        => Catalogo.FirstOrDefault(f => f.Chave == chave);

    /// <summary>A folha de um tipo de documento CLÍNICO. Null se o tipo não está no catálogo.</summary>
    public static FolhaCatalogo? Folha(TipoDocumentoClinico tipo)
        => Catalogo.FirstOrDefault(f => f.TipoClinico == tipo);

    /// <summary>
    /// O acesso para VER um documento clínico deste tipo.
    ///
    /// Tipo fora do catálogo cai em <see cref="Permissao.VerProntuario"/> — o mais
    /// restritivo dos dois candidatos. Papel novo cujo acesso ninguém declarou nasce
    /// FECHADO: nascer aberto é o defeito que só aparece quando já vazou.
    /// </summary>
    public static Permissao AcessoParaVer(TipoDocumentoClinico tipo)
        => Folha(tipo)?.PermissaoVer ?? Permissao.VerProntuario;

    /// <summary>O acesso para EMITIR ou CANCELAR um documento clínico deste tipo.</summary>
    public static Permissao AcessoParaEmitir(TipoDocumentoClinico tipo)
        => Folha(tipo)?.PermissaoEmitir ?? Permissao.Prescrever;

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
        CancellationToken ct = default, Permissao? acessos = null)
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
                PacienteId = d.PacienteId,
                PublicadoAte = d.PublicadoAte,
                JaTeveLink = !string.IsNullOrWhiteSpace(d.TokenPublicacao),
                Chave = Catalogo.FirstOrDefault(f => f.TipoClinico == d.Tipo)?.Chave ?? string.Empty
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
                {
                    Valor = d.ValorTotal,
                    Chave = Catalogo.FirstOrDefault(f => f.TipoFinanceiro == d.Tipo)?.Chave
                            ?? string.Empty
                }));
        }

        return lista
            .Where(f => acessos is not { } a || Alcanca(a, f.Chave))
            .OrderByDescending(f => f.Data)
            .ThenByDescending(f => f.CriadoEm)
            .ToList();
    }

    /// <summary>
    /// Este conjunto de acessos alcança a folha desta chave? Chave desconhecida é NÃO —
    /// papel cujo acesso ninguém declarou não aparece por omissão.
    /// </summary>
    private static bool Alcanca(Permissao acessos, string chave)
        => Folha(chave) is { } f && acessos.HasFlag(f.PermissaoVer);

    /// <summary>
    /// Quantas de cada folha saíram no período. Conta sobre o MESMO recorte da lista —
    /// dois números diferentes para o mesmo papel na mesma tela é pior do que um só.
    /// </summary>
    /// <remarks>
    /// O resumo conta sobre o RESULTADO do filtro de acesso, e não sobre a base: "12
    /// folhas emitidas" acima de uma lista de quatro faria a pessoa procurar as oito que
    /// faltam. É a mesma regra da auditoria (parcela 21) e do filtro da Conciliação.
    /// </remarks>
    public async Task<ResumoFolhas> ResumoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default,
        Permissao? acessos = null)
    {
        var emitidas = await EmitidasAsync(inicio, fim, ct: ct, acessos: acessos);
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
