using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Mapa corporal da sessão (feature 06) e os protocolos que o tornam reutilizável.
///
/// A regra que dá sentido ao resto: aplicar um protocolo é COPIAR pontos para a sessão,
/// nunca apontar para ele. Depois de aplicado, o mapa é do paciente daquele dia — o
/// profissional muda um ponto porque a dor mudou de lugar, e isso não pode reescrever o
/// protocolo da clínica nem a sessão da semana passada. Prontuário é registro do que
/// aconteceu; referência viva reescreveria o passado a cada edição.
/// </summary>
public sealed class MapaCorporalService
{
    private readonly IClinicaRepositorio _repo;

    /// <summary>Quantas sessões para trás a cópia procura um mapa para repetir.</summary>
    private const int SessoesProcuradasParaTras = 12;

    public MapaCorporalService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>Mapa de uma sessão, com os pontos. Null quando a sessão não tem mapa.</summary>
    public Task<MapaCorporal?> DaEvolucaoAsync(int evolucaoId, CancellationToken ct = default)
        => _repo.ObterMapaDaEvolucaoAsync(evolucaoId, ct);

    /// <summary>
    /// Grava o mapa da sessão. Os pontos são substituídos por INTEIRO: a tela edita o
    /// desenho todo, e casar ponto a ponto o que mudou custaria mais do que regravar.
    /// </summary>
    public async Task<MapaCorporal> SalvarAsync(
        int evolucaoId, IReadOnlyList<PontoMapa> pontos, string? observacoes = null,
        string? operador = null, int? protocoloOrigemId = null, CancellationToken ct = default)
    {
        var evolucao = await _repo.ObterEvolucaoAsync(evolucaoId, ct)
            ?? throw new InvalidOperationException("Sessão não encontrada.");

        ValidarPontos(pontos);

        var mapa = await _repo.ObterMapaDaEvolucaoAsync(evolucaoId, ct);
        var novo = mapa is null;

        if (mapa is null)
        {
            mapa = new MapaCorporal
            {
                EvolucaoId = evolucaoId,
                CriadoEm = DateTime.Now,
                CriadoPor = operador
            };
            await _repo.AdicionarMapaAsync(mapa, ct);
        }
        else
        {
            await _repo.RemoverPontosDoMapaAsync(mapa.Id, ct);
            mapa.Pontos.Clear();
            mapa.AtualizadoEm = DateTime.Now;
        }

        mapa.Observacoes = Limpar(observacoes);
        mapa.ProtocoloOrigemId = protocoloOrigemId;

        var ordem = 1;
        foreach (var p in pontos)
            mapa.Pontos.Add(new PontoMapa
            {
                Face = p.Face,
                X = p.X,
                Y = p.Y,
                Nome = Limpar(p.Nome),
                Tecnica = p.Tecnica,
                Observacao = Limpar(p.Observacao),
                Ordem = ordem++
            });

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = novo ? "MapaCorporalRegistrado" : "MapaCorporalAlterado",
            Detalhe = $"Sessão de {evolucao.Data:dd/MM/yyyy} — {pontos.Count} ponto(s)",
            PacienteId = evolucao.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        return mapa;
    }

    /// <summary>Protocolos que servem a este paciente: os da clínica mais os dele.</summary>
    public Task<IReadOnlyList<ProtocoloCorporal>> ProtocolosAsync(
        int? pacienteId, CancellationToken ct = default)
        => _repo.ProtocolosCorporaisAsync(pacienteId, somenteAtivos: true, ct);

    public Task<ProtocoloCorporal?> ObterProtocoloAsync(int protocoloId, CancellationToken ct = default)
        => _repo.ObterProtocoloCorporalAsync(protocoloId, ct);

    /// <summary>
    /// Copia os pontos de um protocolo para a sessão, SUBSTITUINDO o que estava lá.
    /// Devolve o mapa gravado.
    /// </summary>
    public async Task<MapaCorporal> AplicarProtocoloAsync(
        int evolucaoId, int protocoloId, string? operador = null, CancellationToken ct = default)
    {
        var protocolo = await _repo.ObterProtocoloCorporalAsync(protocoloId, ct)
            ?? throw new InvalidOperationException("Protocolo não encontrado.");

        if (protocolo.Pontos.Count == 0)
            throw new InvalidOperationException("Este protocolo não tem nenhum ponto marcado.");

        return await SalvarAsync(
            evolucaoId, PontosDoProtocolo(protocolo), observacoes: null, operador,
            protocoloOrigemId: protocolo.Id, ct);
    }

    /// <summary>Pontos de um protocolo, soltos, para a tela desenhar antes de gravar.</summary>
    public static IReadOnlyList<PontoMapa> PontosDoProtocolo(ProtocoloCorporal protocolo)
        => protocolo.Pontos
            .OrderBy(p => p.Ordem).ThenBy(p => p.Id)
            .Select(p => new PontoMapa
            {
                Face = p.Face,
                X = p.X,
                Y = p.Y,
                Nome = p.Nome,
                Tecnica = p.Tecnica,
                Observacao = p.Observacao
            })
            .ToList();

    /// <summary>
    /// Repete o mapa da última sessão do paciente que tinha um. É o gesto mais comum do
    /// tratamento — a sessão seguinte costuma repetir os pontos da anterior com um ou
    /// outro ajuste, e redesenhar tudo à mão faria o profissional simplesmente não
    /// desenhar.
    /// </summary>
    public async Task<MapaCorporal> CopiarDaSessaoAnteriorAsync(
        int evolucaoId, string? operador = null, CancellationToken ct = default)
    {
        var evolucao = await _repo.ObterEvolucaoAsync(evolucaoId, ct)
            ?? throw new InvalidOperationException("Sessão não encontrada.");

        var pontos = await PontosDaSessaoAnteriorAsync(evolucao.PacienteId, evolucaoId, ct);
        if (pontos.Count == 0)
            throw new InvalidOperationException(
                "Nenhuma sessão anterior deste paciente tem mapa corporal para repetir.");

        return await SalvarAsync(evolucaoId, pontos, observacoes: null, operador, ct: ct);
    }

    /// <summary>
    /// Pontos da última sessão do paciente que tinha mapa, SOLTOS (nada é gravado).
    /// É o que a tela usa: a secretária repete o mapa, ajusta um ponto e só então
    /// salva — gravar no clique do "repetir" deixaria no prontuário um mapa que
    /// ninguém confirmou.
    /// </summary>
    public async Task<IReadOnlyList<PontoMapa>> PontosDaSessaoAnteriorAsync(
        int pacienteId, int? evolucaoIdAtual = null, CancellationToken ct = default)
    {
        var mapa = await MapaDaSessaoAnteriorAsync(pacienteId, evolucaoIdAtual, ct);
        if (mapa is null) return Array.Empty<PontoMapa>();
        return Copiar(mapa.Pontos);
    }

    /// <summary>
    /// Guarda o mapa da sessão como protocolo reutilizável. <paramref name="daClinica"/>
    /// decide se ele vale para todo mundo ou só para este paciente.
    /// </summary>
    public async Task<ProtocoloCorporal> SalvarComoProtocoloAsync(
        int pacienteId, string nome, IReadOnlyList<PontoMapa> pontos, bool daClinica = false,
        string? descricao = null, string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new InvalidOperationException("Dê um nome ao protocolo.");

        if (pontos.Count == 0)
            throw new InvalidOperationException(
                "Marque ao menos um ponto antes de guardar o protocolo.");

        ValidarPontos(pontos);

        if (await _repo.ObterPacienteAsync(pacienteId, ct) is null)
            throw new InvalidOperationException("Paciente não encontrado.");

        var protocolo = new ProtocoloCorporal
        {
            Nome = nome.Trim(),
            Descricao = Limpar(descricao),
            PacienteId = daClinica ? null : pacienteId,
            Ativo = true,
            CriadoEm = DateTime.Now,
            CriadoPor = operador
        };

        var ordem = 1;
        foreach (var p in pontos)
            protocolo.Pontos.Add(new PontoProtocolo
            {
                Face = p.Face,
                X = p.X,
                Y = p.Y,
                Nome = Limpar(p.Nome),
                Tecnica = p.Tecnica,
                Observacao = Limpar(p.Observacao),
                Ordem = ordem++
            });

        await _repo.AdicionarProtocoloCorporalAsync(protocolo, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "ProtocoloCorporalCriado",
            Detalhe = $"{protocolo.Nome} — {protocolo.Pontos.Count} ponto(s)"
                      + (daClinica ? " (protocolo da clínica)" : string.Empty),
            PacienteId = pacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        return protocolo;
    }

    public async Task ExcluirProtocoloAsync(
        int protocoloId, string? operador = null, CancellationToken ct = default)
    {
        var protocolo = await _repo.ObterProtocoloCorporalAsync(protocoloId, ct)
            ?? throw new InvalidOperationException("Protocolo não encontrado.");

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "ProtocoloCorporalExcluido",
            Detalhe = protocolo.Nome,
            PacienteId = protocolo.PacienteId
        }, ct);

        await _repo.RemoverProtocoloCorporalAsync(protocoloId, ct);
        await _repo.SalvarAsync(ct);

        // Os mapas que aplicaram este protocolo não perdem nada: os pontos foram
        // copiados para a sessão, e só o rastro da origem fica sem destino.
    }

    /// <summary>
    /// Mapa mais recente do paciente ANTES da sessão informada. Sem sessão informada
    /// (registro novo, ainda sem Id), é simplesmente o mapa mais recente que ele tem.
    /// </summary>
    private async Task<MapaCorporal?> MapaDaSessaoAnteriorAsync(
        int pacienteId, int? evolucaoIdAtual, CancellationToken ct)
    {
        // O prontuário já vem do mais recente para o mais antigo; o desempate por Id
        // resolve duas sessões no mesmo dia.
        var evolucoes = await _repo.EvolucoesDoPacienteAsync(pacienteId, ct);
        var atual = evolucaoIdAtual is { } id ? evolucoes.FirstOrDefault(e => e.Id == id) : null;

        var anteriores = evolucoes
            .Where(e => atual is null
                        || (e.Id != atual.Id
                            && (e.Data < atual.Data || (e.Data == atual.Data && e.Id < atual.Id))))
            .Take(SessoesProcuradasParaTras)
            .ToList();

        foreach (var candidata in anteriores)
        {
            var mapa = await _repo.ObterMapaDaEvolucaoAsync(candidata.Id, ct);
            if (mapa is not null && mapa.Pontos.Count > 0)
                return mapa;
        }

        return null;
    }

    /// <summary>Cópia solta dos pontos — nada rastreado pelo EF sai daqui.</summary>
    private static IReadOnlyList<PontoMapa> Copiar(IEnumerable<PontoMapa> pontos)
        => pontos
            .OrderBy(p => p.Ordem).ThenBy(p => p.Id)
            .Select(p => new PontoMapa
            {
                Face = p.Face,
                X = p.X,
                Y = p.Y,
                Nome = p.Nome,
                Tecnica = p.Tecnica,
                Observacao = p.Observacao
            })
            .ToList();

    private static void ValidarPontos(IReadOnlyList<PontoMapa> pontos)
    {
        if (pontos.Count > MapaCorporal.MaximoPontos)
            throw new InvalidOperationException(
                $"O mapa aceita até {MapaCorporal.MaximoPontos} pontos.");

        foreach (var p in pontos)
            if (!MapaCorporal.CoordenadaValida(p.X) || !MapaCorporal.CoordenadaValida(p.Y))
                throw new InvalidOperationException(
                    "Ponto marcado fora da figura — a marcação tem de cair sobre o corpo.");
    }

    private static string? Limpar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
