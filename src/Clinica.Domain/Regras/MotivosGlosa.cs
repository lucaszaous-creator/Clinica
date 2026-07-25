namespace Clinica.Domain.Regras;

/// <summary>
/// Um motivo de glosa padronizado (tabela de tipos de glosa do padrão TISS/ANS),
/// com a explicação em português de recepção: o que a operadora quis dizer, por que
/// costuma acontecer, como não deixar acontecer de novo e o que juntar para recorrer.
/// </summary>
public sealed record MotivoGlosa(
    string Codigo,
    string Descricao,
    string Significado,
    string ComoEvitar,
    string Lastro,
    bool ValeRecorrer = true)
{
    public override string ToString() => $"{Codigo} — {Descricao}";
}

/// <summary>
/// Catálogo dos motivos de glosa mais comuns da tabela de domínio do TISS (ANS).
/// Padroniza o registro (antes era texto livre) e permite estatística por motivo.
/// O código vai no XML de recurso de glosa; a descrição, nas telas e relatórios.
///
/// As explicações existem porque a recepção recebe o demonstrativo da operadora com um
/// código seco e não sabe o que fazer com ele — sem entender o motivo, não há como
/// evitar a repetição nem montar o recurso.
/// </summary>
public static class MotivosGlosa
{
    public const string CodigoOutros = "9999";

    /// <summary>Subconjunto prático da tabela ANS, na ordem em que aparece nos combos.</summary>
    public static readonly IReadOnlyList<MotivoGlosa> Todos = new List<MotivoGlosa>
    {
        new("1201", "Carteirinha inválida ou vencida",
            "A operadora não aceitou a carteirinha informada na guia: número errado, plano trocado ou validade já vencida na data do atendimento.",
            "Confira a validade no cadastro antes de atender — a lista de Pacientes e a ficha marcam quem está vencido, e o Novo atendimento avisa na hora.",
            "Cópia da carteirinha nova (frente e verso) e a data em que o paciente a apresentou. Se ela estava válida no dia, o print do portal com a elegibilidade daquela data resolve."),

        new("1202", "Beneficiário não identificado",
            "Os dados do paciente na guia não bateram com o cadastro da operadora — normalmente nome divergente, CPF trocado ou carteirinha digitada errada.",
            "Cadastre o nome exatamente como está na carteirinha e confira o CPF. O sistema já valida os dígitos e bloqueia CPF duplicado.",
            "Documento com foto do paciente + carteirinha, mostrando que é a mesma pessoa, e a correção do dado divergente."),

        new("1205", "Beneficiário com cobertura suspensa",
            "O plano do paciente estava suspenso (inadimplência, carência ou contrato cancelado) na data do atendimento.",
            "Na dúvida, confirme a elegibilidade no portal da operadora antes de atender — é o único jeito de saber, porque a carteirinha continua com aparência válida.",
            "Print da consulta de elegibilidade feita ANTES do atendimento. Sem ele, o recurso raramente se sustenta.",
            ValeRecorrer: false),

        new("1302", "Guia sem autorização prévia",
            "O procedimento exigia autorização (senha) e a guia foi enviada sem ela.",
            "Cadastre a autorização na ficha do paciente. Procedimento que exige senha não deve ser lançado antes de ela sair.",
            "Número da senha e a data em que foi concedida. Se ela saiu depois do atendimento, anexe o protocolo do pedido mostrando que ele foi feito antes."),

        new("1304", "Senha de autorização inválida/vencida",
            "A senha informada existe, mas não valia para aquela data ou já tinha sido usada/expirada.",
            "Registre a validade da autorização: a aba Autorizações da ficha mostra saldo e vencimento, e o Novo atendimento avisa quando a senha venceu.",
            "Senha com a data de validade e o comprovante de que o atendimento caiu dentro dela. Se venceu, peça uma nova e reapresente."),

        new("1705", "Cobrança em duplicidade",
            "A operadora entendeu que a mesma guia (mesmo paciente, procedimento e data) já tinha sido cobrada.",
            "Não relance um atendimento já lançado. O radar de glosas do sistema acusa duplicidade na hora de exportar o lote.",
            "Números das duas guias, mostrando que são atendimentos diferentes (datas ou procedimentos distintos). Se a duplicidade foi real, não recorra: cancele a segunda.",
            ValeRecorrer: false),

        new("1802", "Data de atendimento divergente da autorizada",
            "A data da guia não é a mesma que a operadora autorizou.",
            "Ao remarcar um paciente, confira se a autorização cobre a nova data — remarcar não move a senha.",
            "Autorização mostrando o período coberto e a guia com a data efetiva. Havendo remarcação, o registro do reagendamento ajuda."),

        new("1810", "Guia com preenchimento incompleto/incorreto",
            "Faltou campo obrigatório ou algum dado saiu fora do padrão TISS (dados do prestador, código do procedimento, dados do beneficiário).",
            "Mantenha os dados do prestador completos em Configurações. O validador TISS recusa o lote antes do envio quando falta estrutura obrigatória.",
            "A guia corrigida. É o motivo mais fácil de reverter: quase sempre basta reapresentar com o campo preenchido."),

        new("2001", "Procedimento não coberto pelo contrato",
            "O plano daquele paciente não cobre o procedimento executado.",
            "Confira a cobertura antes de atender pelo convênio — alguns planos não cobrem certas modalidades de acupuntura.",
            "Cláusula do contrato ou o rol da ANS que inclui o procedimento. Se realmente não há cobertura, o caminho é o particular, não o recurso.",
            ValeRecorrer: false),

        new("2002", "Procedimento não autorizado",
            "O procedimento exigia autorização específica e não foi autorizado — diferente do 1302, aqui houve pedido e ele não foi liberado.",
            "Não execute antes de a autorização sair. Se a operadora liberou por telefone, anote o número do protocolo.",
            "Protocolo do pedido e a resposta da operadora. Autorização verbal sem protocolo é a principal causa de perder este recurso."),

        new("2006", "Quantidade executada acima da autorizada",
            "Foram executadas mais sessões do que o convênio liberou naquela autorização.",
            "Cadastre a autorização na ficha do paciente: o Novo atendimento passa a mostrar o saldo e avisa na última sessão, antes de a cota estourar.",
            "Autorização com a quantidade liberada e a relação de sessões executadas. Se a cota estourou de fato, o caminho é pedir autorização complementar, não recorrer."),

        new("2018", "Prazo de validade da guia expirado",
            "A guia foi enviada depois do prazo que a operadora aceita para faturamento.",
            "Feche e envie os lotes dentro do mês. O painel de pendências mostra o envelhecimento das guias em aberto justamente para isso.",
            "Data de geração do lote e o protocolo de envio. Se o envio estava no prazo e o processamento atrasou do lado da operadora, o protocolo é o que sustenta o recurso."),

        new(CodigoOutros, "Outro motivo (detalhar na observação)",
            "Motivo fora da lista padronizada. Copie no complemento exatamente o texto que veio no demonstrativo da operadora.",
            "Registrar o texto literal permite identificar padrões e, depois, tratar o motivo recorrente.",
            "O demonstrativo da operadora com o motivo descrito.")
    };

    /// <summary>Descrição do motivo pelo código (o próprio código se não catalogado).</summary>
    public static string Descricao(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return string.Empty;
        return Todos.FirstOrDefault(m => m.Codigo == codigo)?.Descricao ?? codigo;
    }

    /// <summary>Motivo completo pelo código; null quando não catalogado.</summary>
    public static MotivoGlosa? Obter(string? codigo)
        => string.IsNullOrWhiteSpace(codigo) ? null : Todos.FirstOrDefault(m => m.Codigo == codigo);
}
