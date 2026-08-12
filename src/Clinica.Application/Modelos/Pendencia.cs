using Clinica.Domain;

namespace Clinica.Application.Modelos;

/// <summary>Semáforo de urgência exibido no dashboard.</summary>
public enum NivelUrgencia
{
    Verde,    // dentro do prazo
    Amarelo,  // vence hoje / muito em breve
    Vermelho, // atrasado
    Cinza     // não conformidade parada (documentada): reativa quando o paciente volta ou é resolvida
}

/// <summary>Uma guia/código pendente de baixa (a causa da perda de faturas).</summary>
public sealed record PendenciaCodigo(
    int CodigoId,
    int PacienteId,
    string PacienteNome,
    Convenio Convenio,
    TipoCodigo Tipo,
    OrdemCodigo Ordem,
    DateOnly DataPrevista,
    FormaObtencao FormaObtencao,
    int DiasEmAtraso,
    NivelUrgencia Urgencia,
    string? Descricao,
    /// <summary>Telefone do paciente (para abrir o WhatsApp direto da pendência). Nulo = sem contato no cadastro.</summary>
    string? PacienteTelefone = null,
    /// <summary>Anotação do responsável sobre por que a guia ainda não foi baixada (nula = sem anotação).</summary>
    string? ObservacaoPendencia = null,
    /// <summary>Quando a observação foi anotada/atualizada.</summary>
    DateTime? ObservacaoPendenciaEm = null,
    /// <summary>True quando esta linha é uma NÃO CONFORMIDADE parada (semáforo cinza, reabrível).</summary>
    bool EhNaoConformidade = false)
{
    /// <summary>
    /// Código do convênio no catálogo. <see cref="Convenio"/> é a FAMÍLIA de REGRA — duas
    /// operadoras podem compartilhá-la —, então é só por ele que se chega ao nome que a
    /// clínica cadastrou. Resolver pela família faria toda operadora personalizada
    /// aparecer na tela como "Personalizado".
    /// </summary>
    public string? ConvenioCodigo { get; init; }

    /// <summary>
    /// Modalidade do ATENDIMENTO que gerou a guia (parcela 61) — é o que responde "12
    /// pendências de quê?" no filtro do painel. Aditivo, <c>init</c> com padrão nulo,
    /// pela regra de sempre: este record é compartilhado com o faturamento em produção.
    /// </summary>
    public ModalidadeAtendimento? Modalidade { get; init; }

    /// <summary>
    /// Especialidade da guia: a do CÓDIGO quando ele a tem, senão a do atendimento — o
    /// mesmo caminho de baixo da consulta de guias (parcela 45), e pela mesma razão: sem
    /// ele, a guia de um atendimento com especialidade declarada ficaria fora do filtro
    /// da própria especialidade.
    /// </summary>
    public Especialidade? Especialidade { get; init; }

    /// <summary>True quando há uma observação registrada (para destacar a linha na tela).</summary>
    public bool TemObservacao => !string.IsNullOrWhiteSpace(ObservacaoPendencia);
}

/// <summary>Uma consulta a renovar (cobre laudos, receitas e dúvidas — 22 dias Unimed / 30 dias Amil).</summary>
public sealed record PendenciaConsulta(
    int PacienteId,
    string PacienteNome,
    Convenio Convenio,
    DateOnly DataVencimento,
    int DiasParaVencer,
    NivelUrgencia Urgencia,
    /// <summary>Telefone do paciente (para abrir o WhatsApp direto da pendência). Nulo = sem contato no cadastro.</summary>
    string? PacienteTelefone = null);

/// <summary>Uma glosa em aberto com prazo de recurso correndo (perder o prazo = perder a guia de vez).</summary>
public sealed record PendenciaRecursoGlosa(
    int CodigoId,
    string PacienteNome,
    Convenio Convenio,
    TipoCodigo Tipo,
    string? NumeroGuia,
    DateOnly? DataGlosa,
    string MotivoResumo,
    DateOnly DataLimiteRecurso,
    int DiasParaFimPrazo,
    NivelUrgencia Urgencia,
    /// <summary>
    /// Telefone do paciente. Aqui ele não serve ao WhatsApp como nas outras pendências:
    /// serve a quem RECORRE da glosa, que precisa falar com o paciente para obter o
    /// documento que sustenta o recurso — e o prazo está correndo. Era o único dos quatro
    /// modelos de pendência sem contato, e por isso a linha da glosa era a única em que a
    /// secretária tinha de abrir o cadastro para achar o número.
    /// </summary>
    string? PacienteTelefone = null);

/// <summary>Carteirinha vencida ou a vencer (carteirinha vencida = guia recusada na origem).</summary>
public sealed record PendenciaCarteirinha(
    int PacienteId,
    string PacienteNome,
    Convenio Convenio,
    string? Carteirinha,
    DateOnly Validade,
    int DiasParaVencer,
    NivelUrgencia Urgencia,
    /// <summary>Telefone do paciente (para abrir o WhatsApp direto da pendência). Nulo = sem contato no cadastro.</summary>
    string? PacienteTelefone = null);
