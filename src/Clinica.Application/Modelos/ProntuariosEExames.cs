using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Modelos;

// ============================================================
// As duas telas planas do Consultório (set/2026 — o handoff):
// "Prontuários" e "Exames". Os records são PROJEÇÕES do
// repositório (nunca a entidade inteira — a lista de 90 dias não
// pode arrastar o texto das evoluções nem a miniatura da foto), e
// o que decide o que a tela AFIRMA mora aqui, onde o dotnet test
// alcança — a regra da GradeSemana e do ResumoSessaoAnterior.
// ============================================================

/// <summary>
/// Um pedido de exame na tela de Exames, com a situação DERIVADA DE FATO: o número de
/// resultados vigentes amarrados a ele. "Aguardando resultado" sem o vínculo seria chute
/// com cara de registro — a garantia aparente que o projeto recusa.
/// </summary>
public sealed record PedidoDeExameLinha(
    int DocumentoId,
    string Numero,
    int PacienteId,
    string Paciente,
    DateOnly Data,
    string? PrimeiroExame,
    int TotalItens,
    int ResultadosVinculados,
    bool Cancelado,
    string? Profissional)
{
    /// <summary>"Ressonância — coluna lombar" (+ " e mais 2" quando o pedido tem vários itens).</summary>
    public string ExameRotulo
    {
        get
        {
            var primeiro = string.IsNullOrWhiteSpace(PrimeiroExame) ? "(sem itens)" : PrimeiroExame;
            return TotalItens > 1 ? $"{primeiro} e mais {TotalItens - 1}" : primeiro;
        }
    }

    public SituacaoPedidoExame Situacao => Cancelado
        ? SituacaoPedidoExame.Cancelado
        : ResultadosVinculados > 0
            ? SituacaoPedidoExame.ResultadoDisponivel
            : SituacaoPedidoExame.AguardandoResultado;

    public string SituacaoRotulo => Situacao switch
    {
        SituacaoPedidoExame.Cancelado => "Cancelado",
        SituacaoPedidoExame.ResultadoDisponivel => ResultadosVinculados == 1
            ? "Resultado disponível"
            : $"{ResultadosVinculados} resultados disponíveis",
        _ => "Aguardando resultado"
    };

    /// <summary>"Pedido 2026/0012 · 10/07/2026 · Ressonância — coluna lombar" — o rótulo do combo de vínculo.</summary>
    public string RotuloDoCombo => $"Pedido {Numero} · {Data:dd/MM/yyyy} · {ExameRotulo}";

    // O que a LINHA da tela mostra é decidido aqui, onde o teste alcança — o XAML só
    // liga visibilidade (Setter de Style dentro de Style é ilegal no WPF, e três badges
    // com bandeira é o desenho que a tela de Prescrições já usa).
    public bool EhAguardando => Situacao == SituacaoPedidoExame.AguardandoResultado;
    public bool EhDisponivel => Situacao == SituacaoPedidoExame.ResultadoDisponivel;
    public bool EhCancelado => Situacao == SituacaoPedidoExame.Cancelado;

    /// <summary>
    /// Anexar o laudo aparece em TODO pedido vivo, não só no que aguarda: um pedido de
    /// vários exames recebe vários laudos, e escondê-lo no primeiro impediria o segundo.
    /// Só o pedido CANCELADO não recebe — ele não espera nada.
    /// </summary>
    public bool MostraRegistrar => !EhCancelado;
    public bool MostraVerResultados => EhDisponivel;
    public bool MostraDetalhes => !EhDisponivel;
}

public enum SituacaoPedidoExame { AguardandoResultado, ResultadoDisponivel, Cancelado }

/// <summary>
/// Uma evolução na lista de Prontuários — projeção SEM os textos (a lição da parcela 74:
/// meio megabyte por lista é o preço de ler a entidade inteira para mostrar cinco colunas).
/// </summary>
public sealed record LinhaEvolucaoProntuarios(
    int EvolucaoId,
    int PacienteId,
    string Paciente,
    DateOnly Data,
    int? AgendamentoId,
    ModalidadeAtendimento? Modalidade,
    string? ModalidadeCodigo,
    Especialidade? EspecialidadeConsulta,
    int Versoes,
    string? Profissional);

public enum SituacaoLinhaProntuario { AEscrever, Registrada, Corrigida, AAssinar, Assinada, Cancelada }

public enum NaturezaLinhaProntuario { EvolucaoPendente, Evolucao, Anamnese }

/// <summary>
/// Uma linha da tela de Prontuários. A situação segue o que a linha É no domínio:
/// anamnese é DOCUMENTO e tem assinatura de verdade ("A assinar"/"Assinada"); evolução
/// não é assinável — o pendente real dela é a SESSÃO SEM REGISTRO ("A escrever").
/// Pintar "Assinada" numa evolução seria afirmar uma garantia que o código não dá.
///
/// ⚠️ Os ids são POR TABELA (a lição da parcela 71): <see cref="EvolucaoId"/> e
/// <see cref="DocumentoId"/> são campos SEPARADOS de propósito — um id único numa lista
/// fundida cancelaria o registro errado sem estourar e sem avisar.
/// </summary>
public sealed record LinhaProntuario(
    NaturezaLinhaProntuario Natureza,
    int PacienteId,
    string Paciente,
    DateOnly Data,
    string Detalhe,
    string TipoRotulo,
    SituacaoLinhaProntuario Situacao)
{
    public int? EvolucaoId { get; init; }
    public int? AgendamentoId { get; init; }
    public int? DocumentoId { get; init; }
    public string? Numero { get; init; }
    public string? Profissional { get; init; }

    public string SituacaoRotulo => Situacao switch
    {
        SituacaoLinhaProntuario.AEscrever => "A escrever",
        SituacaoLinhaProntuario.Registrada => "Registrada",
        SituacaoLinhaProntuario.Corrigida => "Corrigida",
        SituacaoLinhaProntuario.AAssinar => "A assinar",
        SituacaoLinhaProntuario.Assinada => "Assinada",
        _ => "Cancelada"
    };

    /// <summary>Linha que pede AÇÃO de quem atende — é o que sobe para o topo da lista.</summary>
    public bool PedeAcao => Situacao is SituacaoLinhaProntuario.AEscrever
        or SituacaoLinhaProntuario.AAssinar;

    // O que a linha MOSTRA é decidido aqui, onde o teste alcança; o XAML só liga
    // visibilidade. Um badge por tom (o desenho da tela de Prescrições).
    public bool TomAviso => PedeAcao;
    public bool TomSucesso => Situacao is SituacaoLinhaProntuario.Registrada
        or SituacaoLinhaProntuario.Assinada;
    public bool TomInfo => Situacao == SituacaoLinhaProntuario.Corrigida;
    public bool TomErro => Situacao == SituacaoLinhaProntuario.Cancelada;

    /// <summary>Escrever = a sessão sem registro; leva DIRETO ao atendimento daquele horário.</summary>
    public bool MostraEscrever => Natureza == NaturezaLinhaProntuario.EvolucaoPendente;
    /// <summary>Abrir = ler no prontuário do paciente (evolução) ou a folha emitida (anamnese).</summary>
    public bool MostraAbrir => Natureza != NaturezaLinhaProntuario.EvolucaoPendente;
    /// <summary>Só a ANAMNESE é assinável — evolução não tem assinatura no domínio, e
    /// pintar o botão nela prometeria uma garantia que o código não dá.</summary>
    public bool MostraAssinar => Natureza == NaturezaLinhaProntuario.Anamnese
        && Situacao == SituacaoLinhaProntuario.AAssinar;
}

/// <summary>Montadores PUROS das duas telas — recebem listas, devolvem linhas.</summary>
public static class ListaDeProntuarios
{
    /// <summary>
    /// Junta sessões sem evolução ("A escrever"), evoluções escritas e anamneses
    /// emitidas numa lista só: quem pede AÇÃO primeiro, depois a data mais recente.
    /// Evolução cancelada não entra (registro desdito mora no histórico do paciente,
    /// não na lista de trabalho); anamnese cancelada entra MARCADA — documento numerado
    /// nunca some (a regra da central de documentos).
    /// </summary>
    public static IReadOnlyList<LinhaProntuario> Montar(
        IReadOnlyList<RegistroPendente> pendentes,
        IReadOnlyList<LinhaEvolucaoProntuarios> evolucoes,
        IReadOnlyList<DocumentoClinico> anamneses)
    {
        var linhas = new List<LinhaProntuario>();

        foreach (var p in pendentes)
            linhas.Add(new LinhaProntuario(
                NaturezaLinhaProntuario.EvolucaoPendente,
                p.PacienteId, p.PacienteNome, DateOnly.FromDateTime(p.DataHora),
                p.Modalidade, "Evolução", SituacaoLinhaProntuario.AEscrever)
            {
                AgendamentoId = p.AgendamentoId,
                Profissional = p.Profissional
            });

        foreach (var e in evolucoes)
            linhas.Add(new LinhaProntuario(
                NaturezaLinhaProntuario.Evolucao,
                e.PacienteId, e.Paciente, e.Data, DetalheDaSessao(e), "Evolução",
                e.Versoes > 0 ? SituacaoLinhaProntuario.Corrigida : SituacaoLinhaProntuario.Registrada)
            {
                EvolucaoId = e.EvolucaoId,
                AgendamentoId = e.AgendamentoId,
                Profissional = e.Profissional
            });

        foreach (var d in anamneses)
            linhas.Add(new LinhaProntuario(
                NaturezaLinhaProntuario.Anamnese,
                d.PacienteId, d.Paciente?.Nome ?? "Paciente", d.Data, $"Ficha {d.Numero}",
                "Anamnese",
                d.Cancelado
                    ? SituacaoLinhaProntuario.Cancelada
                    : d.AssinadoEletronicamente
                        ? SituacaoLinhaProntuario.Assinada
                        : SituacaoLinhaProntuario.AAssinar)
            {
                DocumentoId = d.Id,
                Numero = d.Numero,
                Profissional = d.Profissional?.Nome
            });

        return linhas
            .OrderByDescending(l => l.PedeAcao)
            .ThenByDescending(l => l.Data)
            .ThenByDescending(l => l.EvolucaoId ?? l.DocumentoId ?? l.AgendamentoId ?? 0)
            .ToList();
    }

    /// <summary>
    /// O que a coluna "Especialidade" do mockup mostra: a MODALIDADE da sessão
    /// ("Acupuntura", "BSV"), e a especialidade quando a sessão é uma Consulta — o mesmo
    /// caminho de baixo da consulta de guias (parcela 45). Sem horário ligado, a
    /// evolução avulsa não afirma modalidade nenhuma.
    /// </summary>
    public static string DetalheDaSessao(LinhaEvolucaoProntuarios e)
    {
        if (e.Modalidade is not { } modalidade) return "—";

        var nome = CatalogoModalidades.Nome(e.ModalidadeCodigo, modalidade);
        // ⚠️ Código em branco (nulo OU vazio — NULL não é único no Postgres, então quem
        // grava normaliza família como string vazia) cai na FAMÍLIA, nunca em Base(null),
        // que devolveria o padrão da base e negaria a especialidade de toda Consulta.
        var baseDaModalidade = string.IsNullOrWhiteSpace(e.ModalidadeCodigo)
            ? modalidade
            : CatalogoModalidades.Base(e.ModalidadeCodigo);
        return baseDaModalidade == ModalidadeAtendimento.Consulta
               && e.EspecialidadeConsulta is { } esp
            ? $"{nome} · {RotulosEnum.De(esp)}"
            : nome;
    }
}
