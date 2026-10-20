using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>Um ponto do mapa como a tela o mostra: numerado 1..n na ordem de marcação.</summary>
public sealed class PontoNaTela
{
    public required FaceCorpo Face { get; init; }

    /// <summary>Fração da largura da figura (0 a 1) — nunca pixel: quem converte o clique é a tela.</summary>
    public required double X { get; init; }

    /// <summary>Fração da altura da figura (0 a 1).</summary>
    public required double Y { get; init; }

    public string? Nome { get; set; }
    public TecnicaPonto Tecnica { get; set; } = TecnicaPonto.Agulha;
    public string? Observacao { get; set; }
    public int Numero { get; set; }

    /// <summary>"3 · IG4 · agulha" — o que a legenda escreve ao lado do número.</summary>
    public string Rotulo => string.IsNullOrWhiteSpace(Nome)
        ? $"{Numero} · {RotulosEnum.De(Tecnica).ToLowerInvariant()}"
        : $"{Numero} · {Nome} · {RotulosEnum.De(Tecnica).ToLowerInvariant()}";

    public string FaceRotulo => Face == FaceCorpo.Frente ? "frente" : "costas";
}

/// <summary>
/// O MAPA CORPORAL da sessão em edição (out/2026: a <c>MapaCorporalViewModel</c> do shell sem
/// WPF). O mapa é 1:1 com a evolução e só se grava DEPOIS dela — quem grava é o Salvar da
/// folha, com <see cref="ParaGravacao"/>. Aplicar protocolo e repetir sessão anterior
/// COPIAM (nunca apontam): corrigir o protocolo hoje não reescreve a sessão de ontem.
///
/// As coordenadas são NORMALIZADAS (0 a 1) sobre a figura de
/// <see cref="SilhuetaCorporal.Largura"/>×<see cref="SilhuetaCorporal.Altura"/> — a mesma
/// silhueta do PDF e da tela WPF, uma definição só.
/// </summary>
public sealed class MapaDaSessao
{
    private RascunhoDoMapa? _antesDaUltimaAcao;

    public List<PontoNaTela> Pontos { get; } = [];
    public string? Observacoes { get; set; }
    public int? ProtocoloOrigemId { get; private set; }

    public IEnumerable<PontoNaTela> Frente => Pontos.Where(p => p.Face == FaceCorpo.Frente);
    public IEnumerable<PontoNaTela> Costas => Pontos.Where(p => p.Face == FaceCorpo.Costas);

    /// <summary>Há algo a gravar — pontos ou observações (a regra do <c>TemAlgoParaGravar</c>).</summary>
    public bool TemConteudo => Pontos.Count > 0 || !string.IsNullOrWhiteSpace(Observacoes);

    public bool PodeDesfazer => _antesDaUltimaAcao is not null;

    public string Resumo => Pontos.Count == 0
        ? "Nenhum ponto marcado. Clique na figura para marcar."
        : $"{Pontos.Count} ponto(s) — {Frente.Count()} de frente, {Costas.Count()} de costas.";

    /// <summary>Marca um ponto. Devolve a recusa (frase) ou nulo quando marcou.</summary>
    public string? Marcar(FaceCorpo face, double x, double y, string? nome, TecnicaPonto tecnica)
    {
        if (!MapaCorporal.CoordenadaValida(x) || !MapaCorporal.CoordenadaValida(y))
            return "O ponto ficou fora da figura.";
        if (Pontos.Count >= MapaCorporal.MaximoPontos)
            return $"O mapa aceita até {MapaCorporal.MaximoPontos} pontos.";

        Guardar();
        Pontos.Add(new PontoNaTela
        {
            Face = face, X = x, Y = y,
            Nome = string.IsNullOrWhiteSpace(nome) ? null : nome.Trim(),
            Tecnica = tecnica
        });
        Reindexar();
        return null;
    }

    public void Remover(PontoNaTela ponto)
    {
        if (!Pontos.Contains(ponto)) return;
        Guardar();
        Pontos.Remove(ponto);
        Reindexar();
    }

    public void Limpar()
    {
        if (Pontos.Count == 0) return;
        Guardar();
        Pontos.Clear();
        ProtocoloOrigemId = null;
        Reindexar();
    }

    /// <summary>Desfaz a ÚLTIMA alteração dos pontos (uma só — o atalho do engano, não um histórico).</summary>
    public bool Desfazer()
    {
        if (_antesDaUltimaAcao is not { } anterior) return false;
        Restaurar(anterior);
        _antesDaUltimaAcao = null;
        return true;
    }

    /// <summary>Troca o desenho inteiro (abrir a sessão, copiar uma anterior, aplicar protocolo).</summary>
    public void Substituir(IEnumerable<PontoMapa> pontos, string? observacoes, int? protocoloOrigemId, bool guardarParaDesfazer)
    {
        if (guardarParaDesfazer) Guardar();
        Pontos.Clear();
        foreach (var p in pontos)
            Pontos.Add(new PontoNaTela { Face = p.Face, X = p.X, Y = p.Y, Nome = p.Nome, Tecnica = p.Tecnica, Observacao = p.Observacao });
        Observacoes = observacoes;
        ProtocoloOrigemId = protocoloOrigemId;
        Reindexar();
    }

    /// <summary>O protocolo que deixou de existir solta a procedência.</summary>
    public void EsquecerProtocolo(int protocoloId)
    {
        if (ProtocoloOrigemId == protocoloId) ProtocoloOrigemId = null;
    }

    public IReadOnlyList<PontoMapa> ParaDominio()
        => Pontos.Select(p => new PontoMapa
        {
            Face = p.Face, X = p.X, Y = p.Y, Nome = p.Nome, Tecnica = p.Tecnica, Observacao = p.Observacao
        }).ToList();

    /// <summary>O mapa como o <c>ProntuarioService.SalvarAsync</c> o recebe.</summary>
    public MapaCorporal ParaGravacao() => new()
    {
        Pontos = ParaDominio().ToList(),
        Observacoes = Observacoes,
        ProtocoloOrigemId = ProtocoloOrigemId
    };

    private void Guardar() => _antesDaUltimaAcao = new RascunhoDoMapa(ParaDominio(), Observacoes, ProtocoloOrigemId);

    private void Restaurar(RascunhoDoMapa r)
    {
        Pontos.Clear();
        foreach (var p in r.Pontos)
            Pontos.Add(new PontoNaTela { Face = p.Face, X = p.X, Y = p.Y, Nome = p.Nome, Tecnica = p.Tecnica, Observacao = p.Observacao });
        Observacoes = r.Observacoes;
        ProtocoloOrigemId = r.ProtocoloOrigemId;
        Reindexar();
    }

    private void Reindexar()
    {
        var numero = 1;
        foreach (var p in Pontos) p.Numero = numero++;
    }

    private sealed record RascunhoDoMapa(IReadOnlyList<PontoMapa> Pontos, string? Observacoes, int? ProtocoloOrigemId);
}
