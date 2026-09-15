using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
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

    public async Task<IReadOnlyList<ResumoMapaAnterior>> HistoricoAsync(
        int pacienteId, int? evolucaoIdAtual = null, DateOnly? dataSessao = null, CancellationToken ct = default)
    {
        var data = await DataDaSessaoAsync(pacienteId, evolucaoIdAtual, dataSessao, ct);
        return await _repo.HistoricoMapasAsync(pacienteId, data, evolucaoIdAtual, ct);
    }

    /// <summary>Copia pontos e observações de uma sessão escolhida, sem gravar nem alterar a origem.</summary>
    public async Task<MapaCorporal> CopiarParaEdicaoAsync(int pacienteId, int evolucaoOrigemId,
        int? evolucaoIdAtual = null, DateOnly? dataSessao = null, CancellationToken ct = default)
    {
        var data = await DataDaSessaoAsync(pacienteId, evolucaoIdAtual, dataSessao, ct);
        var origem = await _repo.ObterEvolucaoAsync(evolucaoOrigemId, ct)
            ?? throw new InvalidOperationException("Sessão de origem não encontrada.");
        if (origem.PacienteId != pacienteId || origem.CanceladaEm is not null || origem.Data > data
            || (evolucaoIdAtual is { } atual && origem.Data == data && origem.Id >= atual))
            throw new InvalidOperationException("Escolha uma sessão anterior vigente deste paciente.");
        var mapa = await _repo.ObterMapaDaEvolucaoAsync(origem.Id, ct);
        if (mapa is null || mapa.Pontos.Count == 0)
            throw new InvalidOperationException("Esta sessão não tem pontos para copiar.");
        return new MapaCorporal { Observacoes = mapa.Observacoes,
            ProtocoloOrigemId = mapa.ProtocoloOrigemId, Pontos = Copiar(mapa.Pontos).ToList() };
    }

    private async Task<DateOnly> DataDaSessaoAsync(int pacienteId, int? evolucaoIdAtual,
        DateOnly? dataSessao, CancellationToken ct)
    {
        if (evolucaoIdAtual is not { } id) return dataSessao ?? DateOnly.FromDateTime(DateTime.Today);
        var atual = await _repo.ObterEvolucaoAsync(id, ct);
        if (atual is null || atual.PacienteId != pacienteId || atual.CanceladaEm is not null)
            throw new InvalidOperationException("A sessão atual não pertence a este paciente ou foi cancelada.");
        return atual.Data;
    }

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

        var mapa = await PrepararGravacaoAsync(evolucao, pontos, observacoes, operador, protocoloOrigemId, ct: ct);
        await _repo.SalvarAsync(ct);
        return mapa!;
    }

    /// <summary>Prepara o mapa no mesmo contexto da evolução; o chamador confirma os dois juntos.</summary>
    internal async Task<MapaCorporal?> PrepararGravacaoAsync(Evolucao evolucao,
        IReadOnlyList<PontoMapa> pontos, string? observacoes, string? operador, int? protocoloOrigemId,
        bool ignorarNovoVazio = false, CancellationToken ct = default)
    {
        ValidarConteudo(pontos, observacoes);

        var mapa = evolucao.Id == 0 ? null : await _repo.ObterMapaDaEvolucaoAsync(evolucao.Id, ct);
        if (ignorarNovoVazio && mapa is null && pontos.Count == 0 && string.IsNullOrWhiteSpace(observacoes)) return null;
        var novo = mapa is null;

        if (mapa is null)
        {
            mapa = new MapaCorporal
            {
                Evolucao = evolucao,
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

    internal static void ValidarConteudo(IReadOnlyList<PontoMapa> pontos, string? observacoes)
    {
        ValidarPontos(pontos);
        if (observacoes?.Length > 1000)
            throw new InvalidOperationException("As observações do mapa aceitam até 1000 caracteres.");
        if (pontos.Any(p => p.Nome?.Length > 40 || p.Observacao?.Length > 200))
            throw new InvalidOperationException("Use até 40 caracteres no nome do ponto e 200 na observação.");
    }

    private static string? Limpar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
