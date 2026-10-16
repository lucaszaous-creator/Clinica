using System.Globalization;
using Clinica.Application.Servicos;
using Clinica.Application.Sms;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Modelos;

// =====================================================================================
// A TELA DO CANAL DE SMS (Blazor, out/2026) — o que ela AFIRMA, puro e testado.
// A tela só desenha; cada número e cada frase daqui tem teste em CanalSmsNaTelaTests.
// =====================================================================================

/// <summary>Os contadores do período: quantos saíram, quantos falharam e quanto custou.</summary>
public sealed record ResumoDoHistoricoSms(
    int Enviadas, int Falhas, int EmAndamento, int Segmentos,
    int Confirmacoes, int Campanhas, int Testes)
{
    public int Total => Enviadas + Falhas + EmAndamento;
}

/// <summary>Como a linha do histórico se mostra.</summary>
public static class HistoricoSmsNaTela
{
    /// <summary>
    /// SEGMENTO é o que o provedor cobra — e só se cobra o que SAIU. A mensagem que falhou
    /// não entra na conta de custo (ela entra na de falhas, com o erro do provedor).
    /// </summary>
    public static ResumoDoHistoricoSms Resumir(IReadOnlyCollection<MensagemSms> mensagens) => new(
        mensagens.Count(m => m.Situacao == SituacaoSms.Enviada),
        mensagens.Count(m => m.Situacao == SituacaoSms.Falhou),
        mensagens.Count(m => m.Situacao == SituacaoSms.Enviando),
        mensagens.Where(m => m.Situacao == SituacaoSms.Enviada).Sum(m => m.Segmentos),
        mensagens.Count(m => m.Finalidade == FinalidadeSms.ConfirmacaoSessao),
        mensagens.Count(m => m.Finalidade == FinalidadeSms.Campanha),
        mensagens.Count(m => m.Finalidade == FinalidadeSms.Teste));

    public static string Situacao(MensagemSms m) => m.Situacao switch
    {
        SituacaoSms.Enviada => "Enviado",
        SituacaoSms.Falhou => m.PodeRetentar ? "Falhou — tenta de novo" : "Falhou",
        _ => "Enviando"
    };

    public static string Tom(MensagemSms m) => m.Situacao switch
    {
        SituacaoSms.Enviada => "sucesso",
        SituacaoSms.Falhou => "perigo",
        _ => "neutro"
    };

    public static string Finalidade(FinalidadeSms f) => f switch
    {
        FinalidadeSms.ConfirmacaoSessao => "Confirmação de sessão",
        FinalidadeSms.Campanha => "Campanha",
        _ => "Teste"
    };

    /// <summary>O celular como a recepção lê ("(21) 99812-3456"), não o E.164 do provedor.</summary>
    public static string Celular(string? e164)
    {
        if (string.IsNullOrWhiteSpace(e164)) return "—";
        var d = Telefone.Normalizar(e164);
        if (d.Length == 13 && d.StartsWith("55")) d = d[2..];
        return Telefone.Formatar(d);
    }

    /// <summary>Recorte do histórico por finalidade e/ou "só falhas".</summary>
    public static IReadOnlyList<MensagemSms> Filtrar(IReadOnlyList<MensagemSms> mensagens, FinalidadeSms? finalidade, bool soFalhas)
        => mensagens.Where(m => (finalidade is null || m.Finalidade == finalidade)
                                && (!soFalhas || m.Situacao == SituacaoSms.Falhou)).ToList();

    /// <summary>A frase do vazio — "não saiu nada" e "nada bate com o filtro" são respostas diferentes.</summary>
    public static (string Titulo, string Descricao) Vazio(bool filtrado, bool ligado) => filtrado
        ? ("Nenhum SMS bate com o filtro", "Limpe o filtro para ver o período inteiro.")
        : ligado
            ? ("Nenhum SMS neste período", "Os lembretes saem sozinhos com a antecedência configurada; as campanhas saem pela aba Campanha.")
            : ("Nenhum SMS neste período — o canal está desligado", "Escolha o provedor e preencha o token na aba Configuração para os lembretes começarem a sair.");
}

/// <summary>O tamanho do texto da campanha, como ele vai SAIR (rodapé incluído, sem acento).</summary>
public sealed record MedidaDoTextoSms(int Caracteres, int Segmentos, bool AcentosRemovidos, bool PassouDoTeto, bool Vazio)
{
    public string Descricao
    {
        get
        {
            if (Vazio) return "Escreva a mensagem — o rodapé de saída é acrescentado sozinho.";
            var custo = Segmentos == 1 ? "1 SMS por pessoa" : $"{Segmentos} SMS por pessoa";
            var texto = $"{Caracteres} caracteres · {custo}";
            if (AcentosRemovidos) texto += " · acentos tirados (com acento, cada SMS cabe só 70 caracteres)";
            if (PassouDoTeto) texto += $" · passa de {TextoSms.SegmentosMaximos} SMS: encurte para poder enviar";
            return texto;
        }
    }
}

public static class TextoDaCampanhaSms
{
    public static MedidaDoTextoSms Medir(string? texto)
    {
        var limpo = (texto ?? string.Empty).Trim();
        if (limpo.Length == 0) return new MedidaDoTextoSms(0, 0, false, false, true);
        var final = LembreteSmsService.TextoDaCampanha(limpo);
        var segmentos = TextoSms.Segmentos(final);
        var acentos = !string.Equals(TextoSms.ParaGsm(limpo), limpo, StringComparison.Ordinal);
        return new MedidaDoTextoSms(final.Length, segmentos, acentos, segmentos > TextoSms.SegmentosMaximos, false);
    }
}

/// <summary>O recorte do público da campanha. Nulo é "todos".</summary>
public sealed record FiltroPublicoSms(
    DateOnly Inicio, DateOnly Fim,
    string? ConvenioCodigo = null,
    string? ModalidadeCodigo = null,
    string? EspecialidadeCodigo = null)
{
    /// <summary>O recorte por atendimento: quem FEZ tal modalidade/especialidade no período.</summary>
    public bool PorAtendimento => ModalidadeCodigo is not null || EspecialidadeCodigo is not null;
}

/// <summary>Uma opção dos seletores do público, com quantos pacientes ela alcança.</summary>
public sealed record OpcaoPublicoSms(string Chave, string Rotulo, int Pacientes);

/// <summary>O público que o recorte escolheu — ANTES do consentimento (quem conta é a prévia).</summary>
public sealed record PublicoSms(
    IReadOnlyList<int> PacienteIds,
    string Descricao,
    IReadOnlyList<OpcaoPublicoSms> Convenios,
    IReadOnlyList<OpcaoPublicoSms> Modalidades,
    IReadOnlyList<OpcaoPublicoSms> Especialidades);

/// <summary>
/// QUEM a campanha alcança, pelo recorte da tela (out/2026): o convênio da FICHA e "quem fez
/// tal modalidade/especialidade no período". A base dos atendimentos é a MESMA da tendência
/// (<see cref="TendenciaAtendimentosService.MontarFatosAsync"/>, sessões realizadas) — duas
/// definições de "fez acupuntura em setembro" dariam à campanha e ao quadro de tendência
/// públicos diferentes para a mesma frase.
///
/// O consentimento NÃO é filtrado aqui, de propósito: quem decide quem recebe é
/// <see cref="LembreteSmsService.PreverCampanhaAsync"/>, que CONTA quem ficou de fora
/// (a regra da parcela 5 — quem não consentiu aparece contado, nunca some).
///
/// As opções saem dos DADOS (fichas e atendimentos do período): oferecer um convênio sem
/// ninguém seria um filtro que só leva a "ninguém no público".
/// </summary>
public static class PublicoCampanhaSms
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static PublicoSms Montar(
        IReadOnlyList<PacienteParaContato> fichas, IReadOnlyList<FatoTendencia> fatos, FiltroPublicoSms filtro)
    {
        var (inicio, fim) = filtro.Fim < filtro.Inicio ? (filtro.Fim, filtro.Inicio) : (filtro.Inicio, filtro.Fim);
        var doPeriodo = fatos.Where(f => f.Dia >= inicio && f.Dia <= fim).ToList();

        HashSet<int>? fizeram = null;
        if (filtro.PorAtendimento)
        {
            var recorte = new FiltroTendencia(inicio, fim,
                ModalidadeCodigo: filtro.ModalidadeCodigo, EspecialidadeCodigo: filtro.EspecialidadeCodigo);
            fizeram = doPeriodo.Where(f => TendenciaAtendimentos.Passa(f, recorte)).Select(f => f.PacienteId).ToHashSet();
        }

        var ids = fichas
            .Where(p => filtro.ConvenioCodigo is null || string.Equals(ChaveConvenio(p), filtro.ConvenioCodigo, StringComparison.OrdinalIgnoreCase))
            .Where(p => fizeram is null || fizeram.Contains(p.Id))
            .Select(p => p.Id)
            .Distinct()
            .ToList();

        var convenios = fichas
            .GroupBy(ChaveConvenio, StringComparer.OrdinalIgnoreCase)
            .Select(g => new OpcaoPublicoSms(g.Key, CatalogoConvenios.Nome(g.First().ConvenioCodigo, g.First().ConvenioFamilia), g.Count()))
            .OrderByDescending(o => o.Pacientes).ThenBy(o => o.Rotulo, StringComparer.Ordinal)
            .ToList();

        List<OpcaoPublicoSms> Opcoes(DimensaoTendencia d) => doPeriodo
            .Select(f => (Fato: f, Grupo: TendenciaAtendimentos.Agrupar(f, d)))
            .Where(x => x.Grupo.Chave.Length > 0)
            .GroupBy(x => x.Grupo.Chave, StringComparer.OrdinalIgnoreCase)
            .Select(g => new OpcaoPublicoSms(g.Key, g.First().Grupo.Rotulo, g.Select(x => x.Fato.PacienteId).Distinct().Count()))
            .OrderByDescending(o => o.Pacientes).ThenBy(o => o.Rotulo, StringComparer.Ordinal)
            .ToList();

        var modalidades = Opcoes(DimensaoTendencia.Modalidade);
        var especialidades = Opcoes(DimensaoTendencia.Especialidade);

        return new PublicoSms(ids, Descrever(ids.Count, filtro with { Inicio = inicio, Fim = fim }, convenios, modalidades, especialidades),
            convenios, modalidades, especialidades);
    }

    /// <summary>A frase do público: quantos e POR QUÊ — "142 pacientes" sozinho não diz qual recorte está ligado.</summary>
    public static string Descrever(int quantos, FiltroPublicoSms filtro,
        IReadOnlyList<OpcaoPublicoSms> convenios, IReadOnlyList<OpcaoPublicoSms> modalidades, IReadOnlyList<OpcaoPublicoSms> especialidades)
    {
        string Rotulo(IReadOnlyList<OpcaoPublicoSms> opcoes, string chave)
            => opcoes.FirstOrDefault(o => string.Equals(o.Chave, chave, StringComparison.OrdinalIgnoreCase))?.Rotulo ?? chave;

        var partes = new List<string>();
        if (filtro.ConvenioCodigo is { } c) partes.Add($"convênio {Rotulo(convenios, c)}");
        if (filtro.PorAtendimento)
        {
            var oque = string.Join(" · ", new[]
            {
                filtro.ModalidadeCodigo is { } m ? Rotulo(modalidades, m) : null,
                filtro.EspecialidadeCodigo is { } e ? Rotulo(especialidades, e) : null
            }.Where(x => x is not null));
            partes.Add($"atendidos em {oque} de {filtro.Inicio.ToString("dd/MM/yyyy", PtBr)} a {filtro.Fim.ToString("dd/MM/yyyy", PtBr)}");
        }

        var quem = quantos == 1 ? "1 paciente" : $"{quantos} pacientes";
        return partes.Count == 0
            ? $"{quem} — todos os cadastrados"
            : $"{quem} — {string.Join(", ", partes)}";
    }

    private static string ChaveConvenio(PacienteParaContato p)
        => string.IsNullOrWhiteSpace(p.ConvenioCodigo) ? p.ConvenioFamilia.ToString() : p.ConvenioCodigo;
}

/// <summary>A frase do resultado da campanha — o que saiu, o que falhou e quem ficou de fora, contado.</summary>
public static class ResultadoDaCampanhaSms
{
    public static string Descrever(ResultadoCampanhaSms r)
    {
        var partes = new List<string> { r.Enviados == 1 ? "1 SMS enviado" : $"{r.Enviados} SMS enviados" };
        if (r.Falhas > 0) partes.Add($"{r.Falhas} falha(s) — veja o erro na aba Lembretes");
        if (r.JaEnviados > 0) partes.Add($"{r.JaEnviados} já tinham recebido esta campanha");
        if (r.Recusados > 0) partes.Add($"{r.Recusados} ficaram de fora (sem consentimento ou sem celular)");
        return string.Join(" · ", partes) + ".";
    }
}

/// <summary>
/// A configuração do canal como a tela a edita (out/2026). O TOKEN nunca volta para a tela
/// depois de salvo: o campo nasce vazio, e vazio no Salvar quer dizer MANTER o gravado —
/// a mesma regra da senha do SMTP. Aqui fica a regra, para o teste alcançá-la.
/// </summary>
public static class ConfiguracaoSmsNaTela
{
    /// <summary>Os campos a gravar: o token digitado, ou o GRAVADO quando o campo veio vazio.</summary>
    public static CamposSms ParaSalvar(CamposSms digitados, string? tokenGravado)
        => string.IsNullOrWhiteSpace(digitados.Token)
            ? digitados with { Token = tokenGravado }
            : digitados with { Token = digitados.Token.Trim() };

    /// <summary>Os campos como a tela os recebe: tudo, MENOS o token.</summary>
    public static CamposSms ParaTela(CamposSms gravados) => gravados with { Token = null };

    public static string Provedor(ProvedorSms p) => p switch
    {
        ProvedorSms.Zenvia => "Zenvia",
        ProvedorSms.Twilio => "Twilio",
        ProvedorSms.Generico => "Outro provedor (API JSON)",
        _ => "Desligado"
    };

    /// <summary>
    /// A frase de estado do canal: ligado (com o provedor), desligado por escolha, ou o que
    /// falta — "desligado" e "meio configurado" são situações diferentes para quem configura.
    /// </summary>
    public static (string Texto, string Tom) Estado(CamposSms campos)
    {
        if (OpcoesSms.De(campos) is { } o)
            return ($"Ligado · {Provedor(o.Provedor)} · lembrete {o.AntecedenciaHoras}h antes"
                    + (o.AdiantarFimDeSemana ? ", adiantando o fim de semana para a sexta" : string.Empty) + ".", "sucesso");
        if (campos.Provedor == ProvedorSms.Desligado)
            return ("Desligado — nenhum SMS sai. Escolha o provedor e preencha o que ele pede.", "neutro");
        return ($"Ainda desligado: {OpcoesSms.OQueFalta(campos) ?? "a configuração está incompleta."}", "aviso");
    }
}
