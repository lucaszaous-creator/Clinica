using System.Globalization;

namespace Clinica.Domain.Entities;

/// <summary>
/// O que a sessão tem de DESENHÁVEL, guardado dentro do documento emitido (parcela 79).
///
/// ⚠️ Ele é COPIADO na emissão, nunca lido do prontuário na hora de imprimir — e essa é a
/// regra mais antiga do <see cref="DocumentoClinico"/>: <b>a segunda via tem de sair
/// idêntica à que o paciente levou</b>. Ler o mapa na impressão faria a via de hoje mostrar
/// pontos que a de ontem não tinha, porque a sessão foi corrigida no meio — e o papel que o
/// paciente já guardou passaria a não bater com o que o sistema reimprime.
///
/// É a mesma decisão de "aplicar COPIA, nunca aponta" do protocolo do mapa corporal, do
/// modelo de evolução e da escala aplicada. Aqui ela não é só desenho: é a Lei 13.787/2018.
/// </summary>
/// <param name="EvaAntes">Dor no começo da sessão, quando medida.</param>
/// <param name="EvaDepois">Dor no fim.</param>
/// <param name="Pontos">As marcações do mapa corporal daquela sessão.</param>
public sealed record DesenhoDaSessao(
    int? EvaAntes = null,
    int? EvaDepois = null,
    IReadOnlyList<PontoDaSessao>? Pontos = null)
{
    public static DesenhoDaSessao Nenhum { get; } = new();

    public IReadOnlyList<PontoDaSessao> PontosOuVazio => Pontos ?? [];

    /// <summary>Privado: quem responde "há desenho?" para fora é o <c>Serializar</c>
    /// devolvendo nulo — duas formas de perguntar o mesmo divergem na primeira correção.</summary>
    private bool TemAlgoADesenhar => EvaAntes is not null || PontosOuVazio.Count > 0;

    /// <summary>
    /// A forma gravada. Chaveada (<c>eva=…;pontos=…</c>) e não posicional, para o campo
    /// poder crescer sem que uma via antiga deixe de ser lida: chave desconhecida é
    /// IGNORADA, e a que faltar simplesmente não vem. Documento clínico dura 20 anos, e
    /// nesse prazo o formato muda mais de uma vez.
    /// </summary>
    public string? Serializar()
    {
        if (!TemAlgoADesenhar) return null;

        var partes = new List<string>();

        if (EvaAntes is { } a)
            partes.Add($"eva={N(a)}>{N(EvaDepois)}");

        if (PontosOuVazio.Count > 0)
            partes.Add("pontos=" + string.Join("|", PontosOuVazio.Select(p => string.Join(",",
                p.Face == FaceCorpo.Costas ? "C" : "F",
                D(p.X),
                D(p.Y),
                p.Numero.ToString(CultureInfo.InvariantCulture),
                p.Tecnica.ToString(),
                Escapar(p.Rotulo)))));

        return string.Join(";", partes);
    }

    /// <summary>
    /// A leitura de volta. <b>Tolerante por ponto</b>: um campo estragado descarta AQUELA
    /// marcação e não a folha inteira — a alternativa é uma ficha que se recusa a imprimir,
    /// com o paciente esperando, por causa de um número mal gravado. O que não se faz é
    /// inventar: ponto ilegível some, e o que sobra é o que de fato foi gravado.
    /// </summary>
    public static DesenhoDaSessao Ler(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return Nenhum;

        int? antes = null, depois = null;
        var pontos = new List<PontoDaSessao>();

        foreach (var parte in texto.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var corte = parte.IndexOf('=');
            if (corte <= 0) continue;

            var chave = parte[..corte];
            var valor = parte[(corte + 1)..];

            switch (chave)
            {
                case "eva":
                    var eva = valor.Split('>');
                    if (eva.Length == 2)
                    {
                        antes = LerInt(eva[0]);
                        depois = LerInt(eva[1]);
                    }
                    break;

                case "pontos":
                    foreach (var bruto in valor.Split('|', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var campos = bruto.Split(',');
                        if (campos.Length < 4) continue;
                        if (!double.TryParse(campos[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)) continue;
                        if (!double.TryParse(campos[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)) continue;
                        if (!int.TryParse(campos[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero)) continue;
                        if (!MapaCorporal.CoordenadaValida(x) || !MapaCorporal.CoordenadaValida(y)) continue;

                        // Técnica desconhecida vira `Outra` em vez de descartar o ponto: a
                        // marcação aconteceu, e o que se perde é o nome da agulha — sumir com
                        // ela apagaria do papel um ato que foi praticado. É a mesma escolha do
                        // conversor tolerante de enum (parcela 67), pelo mesmo motivo: o app
                        // velho lê a via nova sem mentir sobre o que não entende.
                        var tecnica = campos.Length > 4
                            && Enum.TryParse<TecnicaPonto>(campos[4], out var t) ? t : TecnicaPonto.Outra;

                        pontos.Add(new PontoDaSessao(
                            campos[0] == "C" ? FaceCorpo.Costas : FaceCorpo.Frente,
                            x, y, numero, tecnica,
                            campos.Length > 5 ? Desescapar(campos[5]) : null));
                    }
                    break;
            }
        }

        return new DesenhoDaSessao(antes, depois, pontos.Count > 0 ? pontos : null);
    }

    /// <summary>Copia o mapa de uma sessão para a forma que vai dentro do documento.</summary>
    public static DesenhoDaSessao De(Evolucao evolucao, MapaCorporal? mapa)
    {
        var pontos = (mapa?.Pontos ?? [])
            .OrderBy(p => p.Ordem).ThenBy(p => p.Id)
            .Select((p, i) => new PontoDaSessao(
                p.Face, p.X, p.Y,
                // ⚠️ A legenda numera 1..n na ORDEM, e não pelo campo `Ordem` cru: ele pode
                // ter buracos depois de alguém remover uma marcação, e uma folha que mostra
                // "1, 2, 5" faz quem lê procurar os pontos 3 e 4 que não existem.
                i + 1,
                p.Tecnica,
                p.Nome))
            .ToList();

        return new DesenhoDaSessao(evolucao.EvaAntes, evolucao.EvaDepois,
            pontos.Count > 0 ? pontos : null);
    }

    /// <summary>
    /// Quantos pontos de cada técnica — "2 agulha · 2 eletroacupuntura · 1 moxa".
    ///
    /// É a leitura que o <see cref="TecnicaPonto"/> existe para permitir desde a parcela 3
    /// ("o que dá para contar depois sem ler prontuário") e que nenhuma folha mostrava. Ela
    /// responde, de relance, o que a legenda ponto a ponto só responde somando na cabeça.
    /// </summary>
    public IReadOnlyList<(string Tecnica, int Quantidade)> ResumoPorTecnica()
        => PontosOuVazio
            .GroupBy(p => p.Tecnica)
            .Select(g => (RotulosTecnica.De(g.Key), g.Count()))
            .OrderByDescending(x => x.Item2).ThenBy(x => x.Item1)
            .ToList();

    private static string N(int? v) => v?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static string D(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);

    private static int? LerInt(string s)
        => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    // O nome do ponto é digitado pelo profissional e pode trazer os separadores do formato
    // — "IG4, VB20" num campo só. Sem escape ele partiria a linha em dois pontos tortos.
    private static string Escapar(string? valor)
        => (valor ?? string.Empty)
            .Replace("\\", "\\\\").Replace(",", "\\c").Replace("|", "\\p").Replace(";", "\\s");

    private static string? Desescapar(string valor)
    {
        var texto = valor
            .Replace("\\s", ";").Replace("\\p", "|").Replace("\\c", ",").Replace("\\\\", "\\");
        return string.IsNullOrWhiteSpace(texto) ? null : texto;
    }
}

/// <summary>Uma marcação copiada para dentro do documento emitido.</summary>
public sealed record PontoDaSessao(
    FaceCorpo Face, double X, double Y, int Numero, TecnicaPonto Tecnica, string? Rotulo)
{
    /// <summary>"3 · IG4 — agulha" — a linha da legenda ao lado da figura.</summary>
    public string Legenda => string.IsNullOrWhiteSpace(Rotulo)
        ? $"{Numero} · {RotulosTecnica.De(Tecnica)}"
        : $"{Numero} · {Rotulo} — {RotulosTecnica.De(Tecnica).ToLowerInvariant()}";
}

/// <summary>
/// O rótulo da técnica em pt-BR. Existe porque o PDF é a única saída do sistema em que o
/// enum não passa por <c>RotulosEnum</c> — aquele mora na camada de TELA, e o papel é
/// montado na Application. Sem isto a folha imprimiria "Eletroacupuntura" junto de
/// "Auriculoterapia" e, no dia em que alguém acrescentasse um valor com duas palavras,
/// o identificador cru (parcela 41) sairia impresso e entregue ao paciente.
/// </summary>
public static class RotulosTecnica
{
    public static string De(TecnicaPonto tecnica) => tecnica switch
    {
        TecnicaPonto.Agulha => "Agulha",
        TecnicaPonto.Eletroacupuntura => "Eletroacupuntura",
        TecnicaPonto.Moxa => "Moxa",
        TecnicaPonto.Ventosa => "Ventosa",
        TecnicaPonto.Auriculoterapia => "Auriculoterapia",
        TecnicaPonto.Laser => "Laser",
        _ => "Outra técnica"
    };
}
