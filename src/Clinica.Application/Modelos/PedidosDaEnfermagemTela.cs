using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>Os três recortes da caixa de pedidos da enfermagem.</summary>
public enum FiltroPedidosEnfermagem
{
    Novos,
    EmAberto,
    Historico
}

/// <summary>Um aviso da enfermagem na lista, já com as frases que a tela escreve.</summary>
public sealed record LinhaPedidoDaEnfermagem(LinhaSolicitacaoEnfermagem Pedido)
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public int Id => Pedido.Id;
    public bool Novo => Pedido.Ativa && Pedido.Situacao == "Novo";
    public string Data => Pedido.Inicio.ToString("dd/MM/yyyy", PtBr);
    public string Faixa => $"{Pedido.Inicio.ToString("HH:mm", PtBr)}–{Pedido.Fim.ToString("HH:mm", PtBr)}";
    public string Status => PedidosDaEnfermagemTela.RotuloSituacao(Pedido.Situacao);
    public string TomDoStatus => PedidosDaEnfermagemTela.TomDaSituacao(Pedido.Situacao);
    public string Titulo => $"Até {Pedido.Quantidade} atendimentos adicionais · {Pedido.Modalidade}";
    public string Observacao => string.IsNullOrWhiteSpace(Pedido.Observacao) ? "Sem observação adicional." : Pedido.Observacao;
    public string Meta => $"{Pedido.Autora} · válido até {Pedido.Validade.ToString("dd/MM/yyyy HH:mm", PtBr)}";
}

/// <summary>A caixa já recortada: a lista, as três contagens e as frases do resumo e do vazio.</summary>
public sealed record RecorteDePedidos(
    IReadOnlyList<LinhaPedidoDaEnfermagem> Linhas,
    int Novos,
    int Abertos,
    int NoDia,
    string Resumo,
    string TituloVazio,
    string TextoVazio);

/// <summary>Uma linha do histórico de um aviso.</summary>
public sealed record LinhaHistoricoDoPedido(string Acao, string Quando, string Observacao)
{
    public bool TemObservacao => !string.IsNullOrWhiteSpace(Observacao);
}

/// <summary>
/// O que a tela "Pedidos da enfermagem" AFIRMA (out/2026, migração para Blazor). Morava no
/// <c>PedidosEnfermagemViewModel</c> WPF — onde o teste não alcança — e subiu para cá com as
/// MESMAS palavras e as MESMAS regras:
/// - <b>"Novo"</b> é o aviso ATIVO ainda não visto; <b>"Em aberto"</b> é todo ativo (novo ou
///   visto); <b>"Histórico"</b> é o que não está mais ativo (encerrado, retirado, expirado).
/// - As três contagens são SEMPRE sobre a caixa inteira, nunca sobre o recorte: é o número
///   que diz o que há para fazer, com qualquer filtro marcado.
/// - "Somente o dia" recorta pelo DIA do período pedido (o início), não pelo dia do envio.
/// - Caixa COMPARTILHADA: sem responsável; abrir um aviso novo é LÊ-LO para todo o balcão.
/// </summary>
public static class PedidosDaEnfermagemTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// A mesma régua do serviço (<see cref="AcessoPlanejamentoEnfermagem.Tratar"/>), lida da
    /// sessão: só quem ENTROU, com perfil de Recepção ou Gerente e os três bits da agenda e da
    /// ficha. Sem login a caixa não abre — ela é da recepção, não de "qualquer um".
    /// </summary>
    public static bool Permitido(bool autenticado, PerfilAcesso perfil, Permissao efetivas)
    {
        const Permissao exigidas = Permissao.VerAgenda | Permissao.EditarAgenda | Permissao.VerFichaPaciente;
        return autenticado && perfil is PerfilAcesso.Recepcao or PerfilAcesso.Gerente && (efetivas & exigidas) == exigidas;
    }

    /// <summary>A frase de quem não alcança a caixa.</summary>
    public const string SemAcesso = "Os pedidos da enfermagem são da recepção e da direção, com acesso à agenda e à ficha do paciente. Fale com a direção da clínica.";

    public static string RotuloSituacao(string situacao) => situacao switch
    {
        "Novo" => "Novo",
        "Visto" => "Visto pela recepção",
        "Encerrado" => "Aviso encerrado",
        "Retirado" => "Retirado pela enfermagem",
        "Expirado" => "Expirado",
        _ => situacao
    };

    /// <summary>O tom do selo: novo pede ação (aviso), visto ainda está aberto (info), o resto é histórico.</summary>
    public static string TomDaSituacao(string situacao) => situacao switch
    {
        "Novo" => "aviso",
        "Visto" => "info",
        "Encerrado" => "sucesso",
        _ => "neutro"
    };

    public static RecorteDePedidos Recortar(
        IReadOnlyList<LinhaSolicitacaoEnfermagem> pedidos, FiltroPedidosEnfermagem filtro, bool soDia, DateTime dia)
    {
        var d = dia.Date;
        var novos = pedidos.Count(p => p.Ativa && p.Situacao == "Novo");
        var abertos = pedidos.Count(p => p.Ativa);
        var noDia = pedidos.Count(p => p.Ativa && p.Inicio.Date == d);
        var linhas = pedidos.Where(p => filtro switch
                {
                    FiltroPedidosEnfermagem.Novos => p.Ativa && p.Situacao == "Novo",
                    FiltroPedidosEnfermagem.EmAberto => p.Ativa,
                    _ => !p.Ativa
                } && (!soDia || p.Inicio.Date == d))
            .Select(p => new LinhaPedidoDaEnfermagem(p))
            .ToList();
        var resumo = $"{linhas.Count} {(linhas.Count == 1 ? "aviso" : "avisos")} · {(soDia ? "dia consultado" : "todas as datas")}";
        var tituloVazio = filtro == FiltroPedidosEnfermagem.Novos ? "Nenhum aviso novo" : "Nenhum aviso neste filtro";
        var textoVazio = soDia
            ? "Desmarque o filtro do dia para consultar as outras datas."
            : "Os pedidos enviados pela enfermagem aparecerão aqui.";
        return new(linhas, novos, abertos, noDia, resumo, tituloVazio, textoVazio);
    }

    // ==================== O detalhe de um aviso ====================

    public static string DetalheFaixa(LinhaSolicitacaoEnfermagem p) => $"Pedido #{p.Id} · {p.Autora}";

    public static string DetalheTitulo(LinhaSolicitacaoEnfermagem p)
        => $"{p.Inicio.ToString("dd/MM/yyyy", PtBr)} · {p.Inicio.ToString("HH:mm", PtBr)} às {p.Fim.ToString("HH:mm", PtBr)}";

    public static string DetalheQuantidade(LinhaSolicitacaoEnfermagem p)
        => $"Até {p.Quantidade} atendimentos adicionais · {p.Modalidade}";

    public static string DetalheObservacao(LinhaSolicitacaoEnfermagem p)
        => string.IsNullOrWhiteSpace(p.Observacao) ? "Sem observação adicional." : p.Observacao;

    public static string DetalheMeta(LinhaSolicitacaoEnfermagem p)
        => $"Válido até {p.Validade.ToString("dd/MM/yyyy", PtBr)} às {p.Validade.ToString("HH:mm", PtBr)}"
           + (p.VistaEm is { } v
               ? $" · Visto por {p.VistaPor} em {v.ToString("dd/MM/yyyy", PtBr)} às {v.ToString("HH:mm", PtBr)}"
               : string.Empty);

    /// <summary>A agenda do período mudou desde o envio — e a quantidade NÃO é vaga reservada.</summary>
    public static string TextoAgendaAlterada(DetalheSolicitacaoEnfermagem d)
        => $"Havia {d.Pedido.TotalReferencia} agendados; agora há {d.Periodo.Total}. Confira horários, modalidades e bloqueios antes de marcar. A quantidade solicitada não representa vagas reservadas.";

    public static string ResumoPeriodo(ResumoPeriodoEnfermagem p)
        => $"{p.Total} agendados · {p.Bsv} BSV · {p.BsvAcupuntura} BSV + acupuntura · {p.OutrasModalidades} outras modalidades";

    public static IReadOnlyList<string> Bloqueios(ResumoPeriodoEnfermagem p)
        => p.Bloqueios.Select(b => $"{b.Inicio.ToString("HH:mm", PtBr)}–{b.Fim.ToString("HH:mm", PtBr)} · {b.Recurso}").ToList();

    public static IReadOnlyList<LinhaHistoricoDoPedido> Historico(DetalheSolicitacaoEnfermagem d)
        => d.Historico.Select(h => new LinhaHistoricoDoPedido(h.Acao,
            $"{h.Em.ToString("dd/MM/yyyy", PtBr)} às {h.Em.ToString("HH:mm", PtBr)} · {h.Autor}", h.Observacao)).ToList();
}
