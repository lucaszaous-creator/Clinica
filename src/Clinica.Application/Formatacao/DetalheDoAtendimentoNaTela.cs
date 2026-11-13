using System.Globalization;
using System.Text;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Formatacao;

/// <summary>
/// Tudo o que o banco guarda sobre UM atendimento, lido de uma vez (nov/2026). Cru: quem
/// decide o que a tela afirma é <see cref="DetalheDoAtendimentoNaTela"/>.
/// </summary>
public sealed record DadosDoDetalheDoAtendimento(
    Atendimento Atendimento,
    Agendamento? Horario,
    IReadOnlyList<LancamentoFinanceiro> Lancamentos,
    ConsumoPacote? Consumo,
    IReadOnlyList<MovimentoEstoque> Insumos,
    bool TemEvolucao);

public sealed record CampoDaFicha(string Rotulo, string Valor);

/// <summary>Uma guia do atendimento: o que é, em que pé está e o que a acompanha.</summary>
public sealed record GuiaDaFicha(string Titulo, string Numero, string Situacao, string Tom, IReadOnlyList<string> Detalhes);

/// <summary>Uma linha de dinheiro ligada ao atendimento (ou à guia dele).</summary>
public sealed record CobrancaDaFicha(string Descricao, string Valor, string Situacao, string Tom, string Detalhe);

/// <summary>
/// A FICHA DO ATENDIMENTO — o que foi feito, as guias, o que foi cobrado (nov/2026; a
/// recepção: "encontramos o paciente e o nº do atendimento, mas precisaríamos saber o que foi
/// feito naquele atendimento, o que foi cobrado, guia e outras coisas"). Tudo isso já estava
/// no banco e só aparecia na janela de ESTORNO, que é para desfazer, não para consultar.
/// </summary>
public sealed record DetalheDoAtendimento(
    int AtendimentoId,
    int PacienteId,
    string Numero,
    string Paciente,
    string Convenio,
    string Resumo,
    string? Estorno,
    IReadOnlyList<CampoDaFicha> OQueFoiFeito,
    IReadOnlyList<GuiaDaFicha> Guias,
    string ResumoDaCobranca,
    string TomDaCobranca,
    IReadOnlyList<CobrancaDaFicha> Cobrancas,
    bool ValoresOcultos,
    IReadOnlyList<string> Insumos)
{
    public bool Estornado => Estorno is not null;
}

/// <summary>
/// O que a ficha AFIRMA. Pura, para o <c>dotnet test</c> alcançar.
///
/// ⚠️ Duas fronteiras que são decisão, não esquecimento:
/// <list type="bullet">
/// <item><b>Prontuário fica de fora.</b> A ficha diz SE há evolução registrada, nunca o que
/// ela diz: a tela é do balcão, e dado de saúde é de quem tem <c>VerProntuario</c> (ponto 5 do
/// compromisso de conformidade). Por não abrir prontuário, também não registra acesso.</item>
/// <item><b>Valor é de quem vê o financeiro.</b> Sem <c>VerFinanceiro</c> a ficha diz que houve
/// cobrança, e não quanto.</item>
/// </list>
/// </summary>
public static class DetalheDoAtendimentoNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public const string SemValor = "—";

    public const string ValoresSoParaOFinanceiro =
        "Seu acesso mostra que houve cobrança, mas não os valores — eles são de quem vê o Financeiro.";

    private static string Dia(DateOnly d) => d.ToString("dd/MM/yyyy", PtBr);
    private static string Hora(DateTime d) => d.ToString("HH:mm", PtBr);
    private static string Dinheiro(decimal v) => v.ToString("C2", PtBr);

    public static DetalheDoAtendimento Montar(DadosDoDetalheDoAtendimento d, bool podeVerValores, DateOnly hoje)
    {
        var a = d.Atendimento;
        var h = d.Horario;
        var paciente = a.Paciente;
        var modalidade = CatalogoModalidades.NomeComEspecialidade(a.ModalidadeCodigo, a.Modalidade, a.EspecialidadeConsultaCodigo);
        var particular = SessaoParticular.Eh(paciente, a.Particular);
        var convenio = paciente is null
            ? "—"
            : SessaoParticular.PagadorComFicha(paciente, a.Particular);

        var feito = new List<CampoDaFicha>
        {
            new("Dia", h is null ? Dia(a.Data) : $"{Dia(a.Data)} às {Hora(h.DataHora)}"),
            new("O que foi feito", modalidade)
        };
        if (a.ColetaPreventivo) feito.Add(new("Junto", "coleta de preventivo"));
        feito.Add(new("Quem atendeu", h is null
            ? "sem horário na agenda (lançado por fora)"
            : h.Profissional?.Rotulo ?? "horário sem profissional"));
        if (h?.Sala?.Nome is { Length: > 0 } sala) feito.Add(new("Sala", sala));
        if (h is not null)
        {
            feito.Add(new("Marcado", string.IsNullOrWhiteSpace(h.CriadoPor)
                ? "sem registro de quem marcou"
                : h.CriadoEm is { } em ? $"por {h.CriadoPor} em {em.ToString("dd/MM/yyyy HH:mm", PtBr)}" : $"por {h.CriadoPor}"));
            if (h.Encaixe) feito.Add(new("Encaixe", "sim"));
            if (h.InicioAtendimentoEm is { } ini)
                feito.Add(new("Na sala", h.FimAtendimentoEm is { } fim ? $"das {Hora(ini)} às {Hora(fim)}" : $"entrou às {Hora(ini)}"));
        }
        feito.Add(new("Lançado", LancamentosNaTela.DescreverLancamento(a)));
        feito.Add(new("Pagador", convenio));
        // SE há registro, nunca O QUE ele diz (ver o resumo da classe).
        feito.Add(new("Evolução do profissional", h is null
            ? "não se aplica (sem horário na agenda)"
            : d.TemEvolucao ? "registrada no prontuário" : "sem registro no prontuário"));
        if (!string.IsNullOrWhiteSpace(a.Observacoes)) feito.Add(new("Observações", a.Observacoes!.Trim()));

        var guias = a.Codigos.OrderBy(c => c.Ordem).ThenBy(c => c.Id).Select(c => Guia(c, hoje)).ToList();
        var (resumo, tom, linhas) = Cobranca(d, particular, podeVerValores, hoje);

        return new DetalheDoAtendimento(
            a.Id, a.PacienteId,
            a.Numero ?? $"#{a.Id}",
            paciente?.Nome ?? $"Paciente {a.PacienteId}",
            convenio,
            $"{Dia(a.Data)} · {modalidade}",
            a.EstornadoEm is { } quando
                ? $"Atendimento ESTORNADO em {quando.ToString("dd/MM/yyyy HH:mm", PtBr)}"
                  + (string.IsNullOrWhiteSpace(a.EstornadoPor) ? "" : $" por {a.EstornadoPor}")
                  + (string.IsNullOrWhiteSpace(a.MotivoEstorno) ? "." : $" — {a.MotivoEstorno!.Trim()}")
                : null,
            feito, guias, resumo, tom, linhas,
            ValoresOcultos: !podeVerValores && linhas.Count > 0,
            d.Insumos.Select(m => $"{m.Item?.Nome ?? $"Item {m.ItemEstoqueId}"} — {m.Quantidade.ToString("0.###", PtBr)}").ToList());
    }

    public static GuiaDaFicha Guia(CodigoFaturamento c, DateOnly hoje)
    {
        var titulo = $"{(c.Ordem == OrdemCodigo.Segundo ? "2ª guia" : "1ª guia")} · {RotulosEnum.De(c.Tipo)}";
        var detalhes = new List<string>();
        if (!string.IsNullOrWhiteSpace(c.Descricao)) detalhes.Add(c.Descricao!.Trim());

        string situacao, tom;
        if (c.Status == StatusCodigo.NaoAplicavel)
        {
            (situacao, tom) = ("não vai ao convênio", "neutro");
            if (!string.IsNullOrWhiteSpace(c.ObservacaoBaixa)) detalhes.Add(c.ObservacaoBaixa!.Trim());
        }
        else if (c.Status == StatusCodigo.NaoConformidade)
        {
            (situacao, tom) = ("não conformidade", "perigo");
            if (!string.IsNullOrWhiteSpace(c.NaoConformidadeJustificativa)) detalhes.Add(c.NaoConformidadeJustificativa!.Trim());
        }
        else if (c.DataBaixa is { } baixa)
        {
            (situacao, tom) = ($"baixada em {Dia(baixa)}", "sucesso");
            if (!string.IsNullOrWhiteSpace(c.UsuarioBaixa)) detalhes.Add($"Baixa por {c.UsuarioBaixa}");
            if (!string.IsNullOrWhiteSpace(c.ObservacaoBaixa)) detalhes.Add(c.ObservacaoBaixa!.Trim());
        }
        else if (c.DataPrevistaFaturamento > hoje)
        {
            (situacao, tom) = ($"libera em {Dia(c.DataPrevistaFaturamento)}", "aviso");
            if (c.FormaObtencao != FormaObtencao.NaoAplica) detalhes.Add($"Como obter: {RotulosEnum.De(c.FormaObtencao)}");
        }
        else
        {
            (situacao, tom) = ($"aguardando baixa desde {Dia(c.DataPrevistaFaturamento)}", "aviso");
            if (!string.IsNullOrWhiteSpace(c.ObservacaoPendencia)) detalhes.Add(c.ObservacaoPendencia!.Trim());
        }

        if (c.Lote is { } lote) detalhes.Add($"Lote TISS nº {lote.Numero}, de {Dia(lote.DataGeracao)}");
        else if (c.LoteTissId is not null) detalhes.Add("Em lote TISS");

        if (c.Glosa != StatusGlosa.SemGlosa)
        {
            (situacao, tom) = c.Glosa switch
            {
                StatusGlosa.Glosada => ("glosada", "perigo"),
                StatusGlosa.Reapresentada => ("glosa reapresentada", "aviso"),
                _ => ("glosa recuperada", "sucesso")
            };
            var glosa = c.DataGlosa is { } dg ? $"Glosa em {Dia(dg)}" : "Glosa";
            if (!string.IsNullOrWhiteSpace(c.MotivoGlosa)) glosa += $" — {c.MotivoGlosa!.Trim()}";
            detalhes.Add(glosa);
            if (c.Glosa == StatusGlosa.Glosada && c.DataLimiteRecurso is { } limite) detalhes.Add($"Recurso até {Dia(limite)}");
            if (c.DataReapresentacao is { } reap) detalhes.Add($"Reapresentada em {Dia(reap)}");
        }

        return new GuiaDaFicha(titulo,
            string.IsNullOrWhiteSpace(c.NumeroGuiaReal) ? "sem número" : c.NumeroGuiaReal!.Trim(),
            situacao, tom, detalhes);
    }

    private static (string Resumo, string Tom, IReadOnlyList<CobrancaDaFicha> Linhas) Cobranca(
        DadosDoDetalheDoAtendimento d, bool particular, bool podeVerValores, DateOnly hoje)
    {
        var linhas = new List<CobrancaDaFicha>();
        if (d.Consumo is { } consumo)
            linhas.Add(new CobrancaDaFicha(
                $"Sessão do pacote «{consumo.Pacote?.Nome ?? "pacote"}»", SemValor, "debitada", "sucesso",
                "Paga na compra do pacote" + (string.IsNullOrWhiteSpace(consumo.CriadoPor) ? "" : $" · baixa por {consumo.CriadoPor}")));

        var entradas = d.Lancamentos.Where(l => l.Tipo == TipoLancamento.Entrada)
            .OrderBy(l => l.Data).ThenBy(l => l.Id).ToList();
        foreach (var l in entradas)
        {
            var (situacao, tom) = l.Status switch
            {
                StatusLancamento.Cancelado => ("cancelado", "neutro"),
                StatusLancamento.Previsto when l.DataVencimento is { } v && v < hoje => ($"vencido em {Dia(v)}", "perigo"),
                StatusLancamento.Previsto => (l.DataVencimento is { } v ? $"a receber, vence {Dia(v)}" : "a receber", "aviso"),
                _ => ($"recebido em {Dia(l.DataPagamento ?? l.Data)}", "sucesso")
            };
            var detalhe = new List<string>();
            if (l.CodigoFaturamentoId is not null) detalhe.Add("receita da guia");
            if (l.FormaPagamento is { } forma) detalhe.Add(RotulosEnum.De(forma));
            if (l.Parcelas is > 1) detalhe.Add($"{l.Parcelas}x");
            if (podeVerValores && l.TemDeducao) detalhe.Add($"líquido {Dinheiro(l.ValorLiquido)}");
            linhas.Add(new CobrancaDaFicha(l.Descricao, podeVerValores ? Dinheiro(l.Valor) : SemValor, situacao, tom,
                string.Join(" · ", detalhe)));
        }

        var vivas = entradas.Where(l => l.Status != StatusLancamento.Cancelado).ToList();
        if (d.Atendimento.Estornado && vivas.Count == 0 && d.Consumo is null)
            return ("Atendimento estornado — nada cobrado.", "neutro", linhas);
        if (vivas.Count == 0 && d.Consumo is not null)
            return ("Sessão de pacote — já paga na compra.", "sucesso", linhas);
        if (vivas.Count == 0)
        {
            if (particular)
                return ("Sem dinheiro registrado — a sessão está na aba Particulares da Conciliação.", "aviso", linhas);
            // Convênio SEM guia faturável não tem de onde vir dinheiro: dizer "ainda" prometeria uma receita.
            return d.Atendimento.Codigos.Any(c => c.Status != StatusCodigo.NaoAplicavel)
                ? ("Nenhuma receita lançada ainda — o dinheiro do convênio entra pela conciliação da guia.", "neutro", linhas)
                : ("Nada a receber do convênio: este atendimento não tem guia faturável.", "neutro", linhas);
        }

        var aReceber = vivas.Where(l => l.Status == StatusLancamento.Previsto).ToList();
        if (!podeVerValores)
            return (aReceber.Count == 0 ? "Cobrança registrada e recebida." : "Cobrança registrada, com valor a receber.",
                aReceber.Count == 0 ? "sucesso" : "aviso", linhas);

        var recebido = vivas.Where(l => l.Status != StatusLancamento.Previsto).Sum(l => l.Valor);
        var partes = new List<string>(2);
        if (recebido > 0m) partes.Add($"Recebido {Dinheiro(recebido)}");
        if (aReceber.Count > 0) partes.Add($"a receber {Dinheiro(aReceber.Sum(l => l.Valor))}");
        var frase = string.Join(" · ", partes);
        return (char.ToUpper(frase[0], PtBr) + frase[1..] + ".", aReceber.Count == 0 ? "sucesso" : "aviso", linhas);
    }

    // ---------------------------------------------------------------- a busca

    /// <summary>Minúsculas e sem acento: "JOAO" acha "João".</summary>
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
        var sb = new StringBuilder(texto.Length);
        foreach (var ch in texto.Trim().Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(ch));
        return sb.ToString();
    }

    /// <summary>Mínimo de caracteres para ir ao banco: com menos, "1" traria a clínica inteira.</summary>
    public const int MinimoParaNumero = 3;

    /// <summary>
    /// A busca é por NÚMERO (do atendimento ou da guia) quando o texto tem dígito e nenhuma
    /// letra: aí vale para QUALQUER data. Nome fica no período escolhido — sem isso "Maria"
    /// traria anos de histórico.
    /// </summary>
    public static bool EhBuscaPorNumero(string? texto)
    {
        var t = texto?.Trim() ?? string.Empty;
        return t.Count(char.IsDigit) >= MinimoParaNumero && !t.Any(char.IsLetter);
    }

    /// <summary>O número como o banco o guarda: sem o "#" que a tela põe em quem não tem número.</summary>
    public static string TermoDeNumero(string texto) => texto.Trim().TrimStart('#');

    /// <summary>A linha casa com o que foi digitado: nome, nº do atendimento ou nº de uma guia dele.</summary>
    public static bool Casa(LinhaLancamentoBalcao l, string? texto)
    {
        var termo = Normalizar(texto);
        if (termo.Length == 0) return true;
        return Normalizar(l.Paciente).Contains(termo, StringComparison.Ordinal)
               || Normalizar(l.Numero).Contains(termo.TrimStart('#'), StringComparison.Ordinal)
               || l.NumerosDasGuias.Any(n => Normalizar(n).Contains(termo, StringComparison.Ordinal));
    }

    public static string ResumoDaBuscaPorNumero(string termo, int achados, int teto)
        => achados == 0
            ? $"Nenhum atendimento ou guia com o número \"{termo}\", em nenhuma data."
            : achados >= teto
                ? $"Os {teto} mais recentes com \"{termo}\" no número — em qualquer data. Digite mais dígitos para afinar."
                : $"{achados} atendimento(s) com \"{termo}\" no número do atendimento ou da guia — em qualquer data.";
}
