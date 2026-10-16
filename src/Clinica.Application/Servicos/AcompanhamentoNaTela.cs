using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>O botão ÚNICO de cada linha da fila: o próximo passo daquele paciente.</summary>
public enum PassoAcompanhamento
{
    Assumir,
    AtualizarFicha,
    ColherAutorizacao,
    Contatar,
    TentarDeNovo,
    MarcarHorario,
    ConferirAgenda,
    AcompanharPlano,
    Ver
}

/// <summary>A faixa do funil — e o filtro que o clique no funil aplica.</summary>
public enum FaixaAcompanhamento { AAssumir, ContatarHoje, EmConversa, Agendado, Voltou, Encerrado }

/// <summary>"Como foi?" — os quatro desfechos de rotina, mais o de encerrar.</summary>
public enum DesfechoContato { NaoRespondeu, PediuOutraData, AguardandoPlano, QuerMarcar, NaoQuerVoltar }

/// <summary>O que o desfecho grava: a etapa, o próximo contato e se encerra.</summary>
public sealed record GravacaoDoDesfecho(EtapaAcompanhamento Etapa, DateOnly? ProximoContato, bool Encerrar);

/// <summary>Os números do funil, sobre a fila que está na tela (Recall ou BSV, minha ou da equipe).</summary>
public sealed record FunilAcompanhamento(int AAssumir, int ContatarHoje, int Atrasados, int EmConversa,
    int Agendados, int Voltaram, int ContatadosNoPeriodo, int VoltaramDosContatados)
{
    /// <summary>
    /// Dos contatados no período, quantos voltaram — nulo sem base (0% e "não medido" são
    /// coisas diferentes). Quem voltou SEM contato não entra: a taxa mede o contato.
    /// </summary>
    public int? TaxaDeRetorno => ContatadosNoPeriodo == 0 ? null : (int)Math.Round(100.0 * VoltaramDosContatados / ContatadosNoPeriodo);
}

/// <summary>O que a fila mostra, na ordem: quem pede ação primeiro.</summary>
public sealed record FiltroAcompanhamento(
    TipoAcompanhamento Tipo,
    bool SoMinhaFila,
    FaixaAcompanhamento? Faixa = null,
    string? Busca = null,
    ModalidadeAtendimento? Modalidade = null,
    string? Convenio = null,
    string? Profissional = null,
    int? ResponsavelId = null,
    string? Motivo = null,
    bool SoTratamentoLongo = false,
    bool SoPacoteComSaldo = false,
    bool SoSemTelefoneOuAutorizacao = false,
    bool SoCancelouOuFaltou = false)
{
    /// <summary>Quantos filtros de "Mais filtros" estão ligados — o botão DIZ, senão um filtro esquecido esconde a fila.</summary>
    public int Extras => new object?[] { Modalidade, Convenio, Profissional, ResponsavelId, Motivo }.Count(x => x is not null)
        + new[] { SoTratamentoLongo, SoPacoteComSaldo, SoSemTelefoneOuAutorizacao, SoCancelouOuFaltou }.Count(x => x);
}

/// <summary>
/// O que a tela do Acompanhamento AFIRMA (out/2026, desenho A aprovado): o próximo passo de
/// cada linha, a faixa do funil, o degrau do progresso, o selo da situação e o que cada
/// desfecho do "Como foi?" grava. Mora aqui, e puro, porque é o que decide o que a
/// recepcionista faz em seguida — e o que decide tem de morar onde o <c>dotnet test</c> alcança.
/// </summary>
public static class AcompanhamentoNaTela
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    /// <summary>Quanto tempo "não respondeu" espera até a próxima tentativa, em dias ÚTEIS.</summary>
    public const int DiasAteNovaTentativa = 2;

    /// <summary>Quanto tempo se espera pela autorização do plano antes de cobrar de novo, em dias úteis.</summary>
    public const int DiasAteCobrarOPlano = 3;

    /// <summary>A janela do "voltaram" do funil.</summary>
    public const int DiasDoPeriodo = 30;

    public static FaixaAcompanhamento Faixa(LinhaAcompanhamento l, DateOnly hoje)
    {
        if (l.VoltouEm is not null) return FaixaAcompanhamento.Voltou;
        if (l.AgendadoPara is not null && !l.Pendente && l.MotivoEncerramento is null) return FaixaAcompanhamento.Agendado;
        if (!l.Pendente) return FaixaAcompanhamento.Encerrado;
        if (l.ResponsavelId is null) return FaixaAcompanhamento.AAssumir;
        return l.ProximoContato <= hoje ? FaixaAcompanhamento.ContatarHoje : FaixaAcompanhamento.EmConversa;
    }

    /// <summary>
    /// O botão da linha. Ordem das perguntas: quem é o dono (sem dono, ninguém liga), se dá
    /// para falar (telefone e autorização — sem eles o WhatsApp seria recusado), e só então
    /// o que a etapa pede.
    /// </summary>
    public static PassoAcompanhamento Passo(LinhaAcompanhamento l)
    {
        if (!l.Pendente) return PassoAcompanhamento.Ver;
        if (l.ResponsavelId is null) return PassoAcompanhamento.Assumir;
        if (string.IsNullOrWhiteSpace(l.Telefone)) return PassoAcompanhamento.AtualizarFicha;
        if (!l.Consentimento) return PassoAcompanhamento.ColherAutorizacao;
        if (l.Situacao == "Conferir comparecimento") return PassoAcompanhamento.ConferirAgenda;
        return l.Etapa switch
        {
            EtapaAcompanhamento.ProntoParaAgendar => PassoAcompanhamento.MarcarHorario,
            EtapaAcompanhamento.AguardandoPlano => PassoAcompanhamento.AcompanharPlano,
            _ => l.Tentativas == 0 && !l.Cancelou && !l.Faltou ? PassoAcompanhamento.Contatar : PassoAcompanhamento.TentarDeNovo
        };
    }

    public static string Rotulo(PassoAcompanhamento p) => p switch
    {
        PassoAcompanhamento.Assumir => "Assumir",
        PassoAcompanhamento.AtualizarFicha => "Atualizar telefone",
        PassoAcompanhamento.ColherAutorizacao => "Ver autorização",
        PassoAcompanhamento.Contatar => "Falar no WhatsApp",
        PassoAcompanhamento.TentarDeNovo => "Tentar de novo",
        PassoAcompanhamento.MarcarHorario => "Marcar horário",
        PassoAcompanhamento.ConferirAgenda => "Conferir na agenda",
        PassoAcompanhamento.AcompanharPlano => "Cobrar o plano",
        _ => "Ver"
    };

    /// <summary>Os cinco degraus do progresso do caso.</summary>
    public static IReadOnlyList<string> Degraus(TipoAcompanhamento tipo) => tipo == TipoAcompanhamento.NovoBsv
        ? ["Indicado", "Assumido", "Contato", "Agendou", "1ª sessão"]
        : ["Sumiu", "Assumido", "Contato", "Agendou", "Voltou"];

    /// <summary>Até que degrau o caso chegou (0 a 4).</summary>
    public static int Degrau(LinhaAcompanhamento l)
    {
        if (l.VoltouEm is not null) return 4;
        if (l.AgendadoPara is not null) return 3;
        if (l.Tentativas > 0) return 2;
        return l.ResponsavelId is null ? 0 : 1;
    }

    /// <summary>
    /// O selo da situação e o tom dele ("perigo", "aviso", "sucesso", "info" ou "" neutro).
    /// O atraso vence tudo o que ainda pede ação: é a ligação que já devia ter sido feita.
    /// </summary>
    public static (string Texto, string Tom) Selo(LinhaAcompanhamento l, DateOnly hoje)
    {
        if (l.VoltouEm is { } voltou) return (l.Tipo == TipoAcompanhamento.NovoBsv ? $"Fez a 1ª sessão em {voltou.ToString("dd/MM", PtBr)}" : $"Voltou em {voltou.ToString("dd/MM", PtBr)}", "sucesso");
        if (!l.Pendente)
            return l.AgendadoPara is { } quando && l.MotivoEncerramento is null
                ? ($"Agendado {quando.ToString("dd/MM 'às' HH:mm", PtBr)}", "info")
                : ($"Encerrado{(string.IsNullOrWhiteSpace(l.Motivo) ? "" : " · " + l.Motivo)}", "");
        if (l.Atrasado)
        {
            var dias = hoje.DayNumber - l.ProximoContato.DayNumber;
            return ($"Atrasado {dias} dia{(dias == 1 ? "" : "s")}", "perigo");
        }
        if (string.IsNullOrWhiteSpace(l.Telefone)) return ("Sem telefone", "aviso");
        if (!l.Consentimento) return ("Mensagem não autorizada", "aviso");
        if (l.Cancelou) return ("Cancelou a sessão", "aviso");
        if (l.Faltou) return ("Faltou à sessão", "aviso");
        if (l.Situacao == "Conferir comparecimento") return ("Conferir comparecimento", "aviso");
        return l.Etapa switch
        {
            EtapaAcompanhamento.ProntoParaAgendar => ("Quer marcar", "sucesso"),
            EtapaAcompanhamento.AguardandoPlano => ("Aguardando o plano", "info"),
            EtapaAcompanhamento.RetornarNaData => (l.ProximoContato > hoje ? $"Retornar em {l.ProximoContato.ToString("dd/MM", PtBr)}" : "Retornar hoje", "info"),
            EtapaAcompanhamento.SemResposta => ($"Sem resposta · {l.Tentativas}ª tentativa", "info"),
            _ => ("A contatar", "")
        };
    }

    /// <summary>A frase de contexto da linha: modalidade, convênio e o que destaca o paciente.</summary>
    public static string Contexto(LinhaAcompanhamento l)
        => string.Join(" · ", new[]
        {
            l.ModalidadeTexto, l.Convenio,
            l.Tipo == TipoAcompanhamento.Recall && l.TratamentoLongo ? $"tratamento longo ({l.TotalSessoes} sessões)" : null,
            l.PacoteComSaldo ? "pacote com saldo" : null
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

    /// <summary>
    /// O próximo dia útil N dias à frente — "tentar de novo em 2 dias" pedido numa sexta cai
    /// na terça, não no domingo, em que ninguém abre o sistema.
    /// </summary>
    public static DateOnly DiasUteis(DateOnly hoje, int dias)
    {
        var d = hoje;
        for (var i = 0; i < dias;)
        {
            d = d.AddDays(1);
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) i++;
        }
        return d;
    }

    /// <summary>A data que o desfecho propõe para o próximo contato (a tela deixa trocar).</summary>
    public static DateOnly? DataProposta(DesfechoContato d, DateOnly hoje) => d switch
    {
        DesfechoContato.NaoRespondeu => DiasUteis(hoje, DiasAteNovaTentativa),
        DesfechoContato.AguardandoPlano => DiasUteis(hoje, DiasAteCobrarOPlano),
        DesfechoContato.QuerMarcar => hoje,
        DesfechoContato.PediuOutraData => null, // a pessoa diz a data — inventá-la mentiria sobre o combinado
        _ => null
    };

    /// <summary>
    /// O que o desfecho grava. Devolve a frase do impedimento (nulo = pode gravar): "pediu
    /// outra data" sem data seria um retorno combinado para dia nenhum.
    /// </summary>
    public static (GravacaoDoDesfecho? Gravacao, string? Impedimento) Gravar(DesfechoContato d, DateOnly hoje, DateOnly? data)
    {
        if (d == DesfechoContato.NaoQuerVoltar) return (new(EtapaAcompanhamento.AContatar, null, true), null);
        var proximo = data ?? DataProposta(d, hoje);
        if (proximo is null) return (null, "Escolha o dia do próximo contato combinado com o paciente.");
        if (proximo < hoje) return (null, "O próximo contato não pode ser no passado.");
        var etapa = d switch
        {
            DesfechoContato.NaoRespondeu => EtapaAcompanhamento.SemResposta,
            DesfechoContato.PediuOutraData => EtapaAcompanhamento.RetornarNaData,
            DesfechoContato.AguardandoPlano => EtapaAcompanhamento.AguardandoPlano,
            _ => EtapaAcompanhamento.ProntoParaAgendar
        };
        return (new(etapa, proximo, false), null);
    }

    public static string Rotulo(DesfechoContato d) => d switch
    {
        DesfechoContato.NaoRespondeu => "Não respondeu",
        DesfechoContato.PediuOutraData => "Pediu outra data",
        DesfechoContato.AguardandoPlano => "Aguardando o plano",
        DesfechoContato.QuerMarcar => "Quer marcar",
        _ => "Não quer voltar"
    };

    public static string Explicacao(DesfechoContato d) => d switch
    {
        DesfechoContato.NaoRespondeu => $"Tentar de novo em {DiasAteNovaTentativa} dias úteis (dá para trocar a data)",
        DesfechoContato.PediuOutraData => "Escolher o dia que o paciente pediu",
        DesfechoContato.AguardandoPlano => $"Cobrar a autorização em {DiasAteCobrarOPlano} dias úteis",
        DesfechoContato.QuerMarcar => "Grava e abre a agenda já com o paciente",
        _ => "Encerrar com motivo — fica no histórico e pode ser reaberto"
    };

    /// <summary>Os números do funil sobre a fila do tipo e do escopo pedidos (sem os filtros de "Mais filtros").</summary>
    public static FunilAcompanhamento Funil(IEnumerable<LinhaAcompanhamento> linhas, TipoAcompanhamento tipo, bool soMinhaFila,
        int usuarioId, DateOnly hoje)
    {
        var lista = linhas.Where(l => l.Tipo == tipo && (!soMinhaFila || DaMinhaFila(l, usuarioId))).ToList();
        var faixas = lista.Select(l => (l, f: Faixa(l, hoje))).ToList();
        var inicio = hoje.AddDays(-DiasDoPeriodo);
        var voltaram = faixas.Count(x => x.f == FaixaAcompanhamento.Voltou && x.l.VoltouEm >= inicio);
        bool Contatado(LinhaAcompanhamento l) => l.UltimoContato is { } u && DateOnly.FromDateTime(u) >= inicio;
        var contatados = lista.Count(Contatado);
        var voltaramDosContatados = faixas.Count(x => x.f == FaixaAcompanhamento.Voltou && x.l.VoltouEm >= inicio && Contatado(x.l));
        return new(
            faixas.Count(x => x.f == FaixaAcompanhamento.AAssumir),
            faixas.Count(x => x.f == FaixaAcompanhamento.ContatarHoje),
            lista.Count(l => l.Atrasado),
            faixas.Count(x => x.f == FaixaAcompanhamento.EmConversa),
            faixas.Count(x => x.f == FaixaAcompanhamento.Agendado),
            voltaram, contatados, voltaramDosContatados);
    }

    /// <summary>"Minha fila" = o que é meu e o que ninguém assumiu ainda (é dali que se pega o próximo).</summary>
    public static bool DaMinhaFila(LinhaAcompanhamento l, int usuarioId) => l.ResponsavelId is null || l.ResponsavelId == usuarioId;

    /// <summary>
    /// A fila na ordem do trabalho: atrasados, depois o que vence hoje, depois quem está à
    /// espera de dono, depois o resto pela data do próximo contato. Sem faixa escolhida, só
    /// o que ainda PEDE ação (agendado, voltou e encerrado aparecem pela faixa deles).
    /// </summary>
    public static IReadOnlyList<LinhaAcompanhamento> Filtrar(IEnumerable<LinhaAcompanhamento> linhas, FiltroAcompanhamento f,
        int usuarioId, DateOnly hoje)
    {
        var termo = f.Busca?.Trim();
        var digitos = Telefone.Normalizar(termo);
        return linhas.Where(l =>
                l.Tipo == f.Tipo
                && (!f.SoMinhaFila || DaMinhaFila(l, usuarioId))
                && (f.Faixa is { } faixa ? Faixa(l, hoje) == faixa : l.Pendente)
                && (string.IsNullOrEmpty(termo)
                    || l.Paciente.Contains(termo, StringComparison.CurrentCultureIgnoreCase)
                    || l.Convenio.Contains(termo, StringComparison.CurrentCultureIgnoreCase)
                    || (digitos.Length >= 4 && Telefone.Normalizar(l.Telefone).Contains(digitos)))
                && (f.Modalidade is null || l.Modalidade == f.Modalidade)
                && (f.Convenio is null || l.Convenio == f.Convenio)
                && (f.Profissional is null || l.Profissional == f.Profissional)
                && (f.ResponsavelId is null || l.ResponsavelId == f.ResponsavelId)
                && (f.Motivo is null || l.Motivo == f.Motivo)
                && (!f.SoTratamentoLongo || l.TratamentoLongo)
                && (!f.SoPacoteComSaldo || l.PacoteComSaldo)
                && (!f.SoSemTelefoneOuAutorizacao || string.IsNullOrWhiteSpace(l.Telefone) || !l.Consentimento)
                && (!f.SoCancelouOuFaltou || l.Cancelou || l.Faltou))
            .OrderByDescending(l => l.Atrasado)
            .ThenBy(l => l.Pendente ? 0 : 1)
            .ThenBy(l => l.ProximoContato)
            .ThenBy(l => l.ResponsavelId is null ? 0 : 1)
            .ThenBy(l => l.Paciente, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>A mensagem que o WhatsApp vai abrir — vista ANTES do clique.</summary>
    public static string Mensagem(LinhaAcompanhamento l, string? nomeClinica)
        => l.Tipo == TipoAcompanhamento.Recall
            ? Modelos.MensagensDeContato.Recall(l.Paciente, l.Dias, nomeClinica)
            : Modelos.MensagensDeContato.IndicacaoBsv(l.Paciente, nomeClinica);
}
