using System.Globalization;
using System.Text;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// O que a tela de Medicamentos do Consultório AFIRMA (out/2026, migrado da MedicamentosView
/// WPF para morar onde o teste alcança): o recorte da busca, a frase do resumo e a leitura da
/// planilha de importação. Quem valida limites e grava é o <c>MedicamentoCatalogoService</c>.
/// </summary>
public static class MedicamentosNaTela
{
    /// <summary>A lista desenha no máximo isto; a busca refina o resto.</summary>
    public const int Teto = 250;

    /// <summary>As quatro colunas do modelo, na ordem — é o contrato da importação.</summary>
    public static readonly IReadOnlyList<string> Colunas = ["Nome", "PrincipioAtivo", "Apresentacao", "Fabricante"];

    public const int MaximoBytes = 5_000_000;
    public const int MaximoLinhas = 5000;

    private static readonly CompareInfo Comparador = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;

    /// <summary>
    /// Casa nome, princípio ativo, apresentação e fabricante, sem acento nem caixa. Os cadastros
    /// DA CLÍNICA vêm antes da base oficial — são os que alguém corrigiu de propósito.
    /// </summary>
    public static IReadOnlyList<MedicamentoCadastro> Filtrar(IEnumerable<MedicamentoCadastro> todos, string? termo)
    {
        var t = termo?.Trim() ?? string.Empty;
        return todos
            .Where(m => t.Length == 0 || Comparador.IndexOf(
                $"{m.Nome} {m.PrincipioAtivo} {m.Apresentacao} {m.Fabricante}", t,
                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)
            .OrderByDescending(m => DaClinica(m))
            .ThenBy(m => m.Nome, StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), true))
            .ToList();
    }

    public static bool DaClinica(MedicamentoCadastro m) => m.Codigo.StartsWith("clinica:", StringComparison.Ordinal);

    /// <summary>A frase DIZ quando a lista está cortada — corte silencioso faz procurar o que não aparece.</summary>
    public static string Resumo(int encontrados, int total, bool filtrado)
    {
        var pt = CultureInfo.GetCultureInfo("pt-BR");
        var base_ = filtrado
            ? $"{encontrados.ToString("N0", pt)} de {total.ToString("N0", pt)} cadastro(s) batem com a busca"
            : $"{total.ToString("N0", pt)} cadastro(s)";
        return encontrados > Teto ? $"{base_} · exibindo os primeiros {Teto} — use a busca para refinar." : $"{base_}.";
    }

    /// <summary>O modelo de planilha: só o cabeçalho, UTF-8 com BOM (o Excel em português lê o acento).</summary>
    public static byte[] Modelo()
        => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(string.Join(';', Colunas) + "\r\n")];

    /// <summary>
    /// Lê a planilha importada. Recusa com a frase que diz o que corrigir: arquivo grande,
    /// cabeçalho diferente do modelo, linha sem as quatro colunas, mais de 5.000 linhas.
    /// Linha em branco é pulada; quem valida o CONTEÚDO de cada linha é o serviço.
    /// </summary>
    public static IReadOnlyList<MedicamentoCadastro> LerPlanilha(byte[] conteudo)
    {
        if (conteudo.Length > MaximoBytes) throw new InvalidOperationException("A planilha deve ter até 5 MB.");
        var texto = new UTF8Encoding(false).GetString(conteudo).TrimStart('﻿');
        var linhas = LinhasCsv(texto).Where(l => !(l.Count == 1 && l[0].Length == 0)).ToList();
        if (linhas.Count == 0 || !linhas[0].Select(c => c.Trim()).SequenceEqual(Colunas))
            throw new InvalidOperationException("Use o Modelo de planilha, com as quatro colunas e separador ponto e vírgula.");
        var lista = new List<MedicamentoCadastro>();
        for (var i = 1; i < linhas.Count; i++)
        {
            var c = linhas[i];
            if (c.Count != 4) throw new InvalidOperationException($"Linha {i + 1}: confira as quatro colunas da planilha.");
            lista.Add(new MedicamentoCadastro { Nome = c[0], PrincipioAtivo = c[1], Apresentacao = c[2], Fabricante = c[3] });
            if (lista.Count > MaximoLinhas) throw new InvalidOperationException("Importe até 5.000 medicamentos por vez.");
        }
        if (lista.Count == 0) throw new InvalidOperationException("A planilha não tem nenhum medicamento abaixo do cabeçalho.");
        return lista;
    }

    /// <summary>CSV com separador ';' e campos entre aspas (aspas dobradas dentro do campo).</summary>
    private static IEnumerable<List<string>> LinhasCsv(string texto)
    {
        var campos = new List<string>();
        var atual = new StringBuilder();
        var aspas = false;
        for (var i = 0; i < texto.Length; i++)
        {
            var ch = texto[i];
            if (aspas)
            {
                if (ch == '"')
                {
                    if (i + 1 < texto.Length && texto[i + 1] == '"') { atual.Append('"'); i++; }
                    else aspas = false;
                }
                else atual.Append(ch);
                continue;
            }
            switch (ch)
            {
                case '"': aspas = true; break;
                case ';': campos.Add(atual.ToString()); atual.Clear(); break;
                case '\r': break;
                case '\n':
                    campos.Add(atual.ToString()); atual.Clear();
                    yield return campos;
                    campos = [];
                    break;
                default: atual.Append(ch); break;
            }
        }
        if (atual.Length > 0 || campos.Count > 0)
        {
            campos.Add(atual.ToString());
            yield return campos;
        }
    }
}
