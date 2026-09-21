using System.Data;
using System.Text.Json;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure.Tablet;

/// <summary>Fronteira web: identidade, vínculo e permissão antecedem toda leitura/escrita.</summary>
public sealed partial class AtendimentoTabletService(ClinicaDbContext db, IClinicaRepositorio repo,
    ProntuarioService prontuario, AgendaService agenda, DocumentoClinicoService documentos,
    PrescricaoInternaService prescricoes, PrescricaoService conferencia, TimeProvider tempo)
{
    public long Agora => tempo.GetUtcNow().ToUnixTimeMilliseconds();
    public DateOnly Hoje => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(tempo.GetUtcNow(),
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);
    private static string Operador(UsuarioSistema u) => u.Login;

    public async Task<UsuarioSistema> AutorizarAsync(SessaoTablet sessao, CancellationToken ct, Permissao? permissao = null)
    {
        // Releitura: revogação, troca de senha, permissão e vínculo têm efeito imediato.
        var s = await db.SessoesTablet.AsNoTracking().Include(x => x.Usuario).ThenInclude(u => u!.Profissional)
            .SingleOrDefaultAsync(x => x.Id == sessao.Id, ct);
        if (s?.Usuario is not {} u || s.Modo != "equipe" || s.ExpiraEm <= Agora
            || !(permissao is {} requerida ? PoliticaAtendimentoTablet.PodeUsarPosto(u) && u.Pode(requerida) : PoliticaAtendimentoTablet.PodeAtender(u)) || u.Profissional?.Ativo != true
            || u.Travado(DateTime.Now) || s.CredencialVersao != ContratoTablet.Hash(u.SenhaHash))
            throw new UnauthorizedAccessException();
        if (s.AtividadeClinicaEm is {} ultima && Agora - ultima > 900_000)
            throw new UnauthorizedAccessException();
        sessao.AtividadeClinicaEm = Agora;
        // O contexto recebido pertence ao DbContext da requisição.
        if (db.Entry(sessao).State == EntityState.Detached) db.Attach(sessao);
        db.Entry(sessao).Property(x => x.AtividadeClinicaEm).IsModified = true;
        await db.SaveChangesAsync(ct);
        return u;
    }

    private async Task<Agendamento> Horario(UsuarioSistema u, int id, CancellationToken ct)
    {
        if (!PoliticaAtendimentoTablet.PodeAtender(u)) throw new UnauthorizedAccessException();
        return await db.Agendamentos.Include(a => a.Paciente).SingleOrDefaultAsync(a => a.Id == id
            && a.ProfissionalId == u.ProfissionalId
            && (a.Status == StatusAgendamento.Agendado || a.Status == StatusAgendamento.Realizado), ct)
            ?? throw new RecursoClinicoIndisponivel();
    }

    public async Task<object> DiaAsync(SessaoTablet s, DateOnly? dia, CancellationToken ct)
    {
        var u = await AutorizarAsync(s, ct);
        var data = dia ?? Hoje;
        if (Math.Abs(data.DayNumber - Hoje.DayNumber) > 366) throw new InvalidOperationException("Escolha uma data no intervalo de um ano.");
        var inicio = data.ToDateTime(TimeOnly.MinValue); var fim = inicio.AddDays(1);
        var horarios = await db.Agendamentos.AsNoTracking().Where(a => a.ProfissionalId == u.ProfissionalId
            && a.DataHora >= inicio && a.DataHora < fim
            && (a.Status == StatusAgendamento.Agendado || a.Status == StatusAgendamento.Realizado))
            .OrderBy(a => a.DataHora).Take(300).Select(a => new {
                a.Id, a.PacienteId, Nome = a.Paciente!.Nome, Nascimento = a.Paciente.DataNascimento,
                a.DataHora, a.ModalidadeCodigo, Modalidade = a.ModalidadePrevista.ToString(),
                a.ChegadaEm, a.InicioAtendimentoEm, a.FimAtendimentoEm,
                Finalizado = a.FimAtendimentoEm != null,
                TemEvolucao = db.Evolucoes.Any(e => e.AgendamentoId == a.Id && e.CanceladaEm == null)
            }).ToListAsync(ct);
        return new { Data = data, Profissional = u.Profissional!.Nome, PodePrescrever = u.Pode(Permissao.Prescrever), Horarios = horarios };
    }

    private async Task<Evolucao?> EvolucaoAtual(int ag, CancellationToken ct)
    {
        var registros = await db.Evolucoes.Include(e => e.CamposPersonalizados).Include(e => e.Versoes)
            .Where(e => e.AgendamentoId == ag && e.CanceladaEm == null).Take(2).ToListAsync(ct);
        if (registros.Count > 1) throw new ConflitoClinicoTablet("Há mais de uma evolução vinculada. Confira o vínculo no sistema antes de editar pelo tablet.");
        return registros.SingleOrDefault();
    }
    private async Task<MapaClinicoTablet?> Mapa(int evolucao, CancellationToken ct)
    {
        var m = await db.MapasCorporais.AsNoTracking().Include(x => x.Pontos).SingleOrDefaultAsync(x => x.EvolucaoId == evolucao, ct);
        return m is null ? null : new(m.Pontos.OrderBy(p => p.Ordem).Select(p => new PontoClinicoTablet(p.Face, p.X, p.Y, p.Nome, p.Tecnica, p.Observacao)).ToArray(), m.Observacoes);
    }
    public static EvolucaoClinicaTablet Fotografar(Evolucao? e, MapaClinicoTablet? mapa)
    {
        var dto = new EvolucaoClinicaTablet(e?.Id ?? 0, "", e?.QueixaPrincipal, e?.HistoriaDoencaAtual,
            e?.ExameFisico, e?.HipoteseDiagnostica, e?.CidSessao, e?.Conduta, e?.TextoEvolucao,
            e?.Orientacoes, e?.PlanoTerapeutico, e?.EvaAntes, e?.EvaDepois, mapa);
        return dto with { Versao = ContratoTablet.Hash(ContratoTablet.Serializar(new {
            dto, e?.AtualizadoEm, e?.RetornoSugeridoEm, e?.RetornoSugeridoNota, e?.Encaminhamento,
            e?.ProfissionalId, e?.AtendimentoId,
            Campos = e?.CamposPersonalizados?.Select(c => new { c.Id, c.Valor }).OrderBy(c => c.Id)
        })) };
    }

    public async Task<object> AbrirAsync(SessaoTablet s, int id, CancellationToken ct)
    {
        var u = await AutorizarAsync(s, ct); var a = await Horario(u, id, ct);
        var e = await EvolucaoAtual(id, ct);
        var historico = await db.Evolucoes.AsNoTracking().Where(x => x.PacienteId == a.PacienteId && x.CanceladaEm == null && x.Id != (e == null ? 0 : e.Id))
            .OrderByDescending(x => x.Data).ThenByDescending(x => x.Id).Take(20)
            .Select(x => new {x.Id, x.Data, Profissional = x.Profissional == null ? x.CriadoPor : x.Profissional.Nome,
                x.QueixaPrincipal, x.HistoriaDoencaAtual, x.ExameFisico, x.HipoteseDiagnostica, x.CidSessao,
                x.Conduta, x.TextoEvolucao, x.Orientacoes, x.PlanoTerapeutico,
                TemMapa = db.MapasCorporais.Any(m => m.EvolucaoId == x.Id)}).ToListAsync(ct);
        var docs = await db.DocumentosClinicos.AsNoTracking().Where(d => d.PacienteId == a.PacienteId && d.CanceladoEm == null)
            .OrderByDescending(d => d.Id).Take(30).Select(d => new {d.Id, d.Numero, Tipo = d.Tipo.ToString(), d.Titulo,
                d.Corpo,d.CorpoFormatado, d.Observacoes,d.ObservacoesFormatadas, d.DiasAfastamento, d.Data, d.AgendamentoId, d.AssinadoEm, Proprio = d.ProfissionalId == u.ProfissionalId,
                Itens=d.Itens.OrderBy(i=>i.Ordem).Select(i=>new {i.Descricao,i.DescricaoFormatada,i.Detalhe,i.DetalheFormatado,i.Quantidade})}).ToListAsync(ct);
        var infusoes = await db.PrescricoesInternas.AsNoTracking().Where(p => p.PacienteId == a.PacienteId && p.CanceladaEm == null)
            .OrderByDescending(p => p.Id).Take(20).Select(p => new {p.Id, p.Numero, p.Indicacao,p.IndicacaoFormatada, p.Observacoes,p.ObservacoesFormatadas, p.Data,
                p.AgendamentoId, p.OrigemEnfermagem, p.OrientacaoExterna, Situacao = p.OrigemEnfermagem&&p.AssinadaEm==null&&p.Situacao==SituacaoPrescricao.Encerrada?"AguardaMedico":p.Situacao.ToString(), p.AssinadaEm, Proprio = p.ProfissionalId == u.ProfissionalId,Assinaturas=p.Assinaturas.Select(a=>new {Papel=a.Papel.ToString(),a.NomeAssinante,a.RegistroConselho,a.AssinadoEm,PrescricaoArquivada=a.ArquivoId!=null,RegistroArquivado=a.ArquivoRegistroId!=null}),
                Itens = p.Itens.OrderBy(i => i.Ordem).Select(i => new {i.Descricao,i.DescricaoFormatada,i.Dose,Via=i.Via.ToString(),i.HoraPrevista,i.SeNecessario,i.Observacoes,i.ObservacoesFormatadas,i.Diluente,i.Volume,i.TempoInfusao,i.SuspensoEm,i.MotivoSuspensao})}).ToListAsync(ct);
        var modelos = await db.ModelosEvolucao.AsNoTracking().Where(m => m.Ativo && (m.ProfissionalId == null || m.ProfissionalId == u.ProfissionalId))
            .OrderBy(m => m.Nome).Take(80).Select(m => new {m.Id, m.Nome, m.QueixaPrincipal, m.HistoriaDoencaAtual,
                m.ExameFisico, m.HipoteseDiagnostica, m.CidSessao, m.Conduta, m.TextoEvolucao, m.Orientacoes, m.PlanoTerapeutico}).ToListAsync(ct);
        var protocolos = await db.ProtocolosCorporais.AsNoTracking().Where(m => m.Ativo && (m.PacienteId == null || m.PacienteId == a.PacienteId))
            .OrderBy(m => m.Nome).Take(80).Select(m => new {m.Id, m.Nome, m.Descricao,
                Pontos = m.Pontos.OrderBy(p => p.Ordem).Select(p => new PontoClinicoTablet(p.Face, p.X, p.Y, p.Nome, p.Tecnica, p.Observacao))}).ToListAsync(ct);
        await Auditar(u, a.PacienteId, "TabletClinicoConsulta", "Consulta do atendimento e histórico", ct);
        await db.SaveChangesAsync(ct);
        var alertas = await conferencia.ContextoAsync(a.PacienteId, ct);
        return new {Agendamento = new {a.Id, a.DataHora, a.PacienteId, a.AtendimentoId, a.InicioAtendimentoEm,
                Finalizado = a.FimAtendimentoEm != null, a.FimAtendimentoEm},
            Paciente = new {a.Paciente!.Nome, Nascimento = a.Paciente.DataNascimento},
            Faturamento = await ResumoFaturamentoAsync(a, ct),
            ExigeConferenciaEnfermagem = await agenda.ExigeConferenciaEnfermagemAsync(a.Id, ct),
            OrientacaoConclusao = (await new PoliticaConclusaoService(repo).ObterAsync(ct)).OrientacaoAoSalvar,
            PendenciaConclusao = await agenda.PendenciaEnfermagemAsync(a.Id, ct) ?? (await new ConclusaoAutomaticaService(db, repo, agenda, new(repo))
                .PendenciasAsync(ConclusaoAutomaticaService.Agora, a.Id, ct)).FirstOrDefault()?.Motivo,
            Profissional = u.Profissional!.Nome, PodePrescrever = u.Pode(Permissao.Prescrever), PodeConcluir = u.Pode(Permissao.LancarAtendimento),
            Silhueta = new {Largura=SilhuetaCorporal.Largura, Altura=SilhuetaCorporal.Altura,
                Coluna = new {X=SilhuetaCorporal.ColunaX, Topo=SilhuetaCorporal.ColunaTopo, Base=SilhuetaCorporal.ColunaBase},
                Formas = SilhuetaCorporal.Formas.Select(f => f switch {
                    ElipseSilhueta el => new {Tipo="elipse", X=el.Cx, Y=el.Cy, Largura=el.Rx, Altura=el.Ry, Raio=0d},
                    RetanguloSilhueta rt => new {Tipo="retangulo", rt.X, rt.Y, rt.Largura, rt.Altura, rt.Raio},
                    _ => throw new InvalidOperationException("Forma de mapa indisponível.")})},
            Evolucao = Fotografar(e, e is null ? null : await Mapa(e.Id, ct)), Historico = historico,
            Documentos = docs, Infusoes = infusoes, Modelos = modelos, Protocolos = protocolos,
            Alertas = new {Alergias = alertas.Alergias.Select(a => a.Descricao), MedicacoesEmUso = alertas.MedicacoesEmUso.Select(a => a.Descricao)}};
    }

    public async Task<MapaClinicoTablet?> CopiarMapaAsync(SessaoTablet s, int agendamento, int evolucao, CancellationToken ct)
    {
        var u = await AutorizarAsync(s, ct); var a = await Horario(u, agendamento, ct);
        if (!await db.Evolucoes.AnyAsync(e => e.Id == evolucao && e.PacienteId == a.PacienteId && e.CanceladaEm == null, ct))
            throw new RecursoClinicoIndisponivel();
        await Auditar(u, a.PacienteId, "TabletClinicoCopiaMapa", "Consulta de mapa anterior", ct);
        await db.SaveChangesAsync(ct);
        return await Mapa(evolucao, ct);
    }

    public async Task<T> Escrever<T>(SessaoTablet s, int agendamento, Guid chave, object pedido,
        Func<UsuarioSistema, Agendamento, Task<T>> acao, CancellationToken ct)
    {
        if (chave == Guid.Empty) throw new InvalidOperationException("Atualize a página antes de salvar.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (db.Database.IsNpgsql()) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(20260916, {agendamento})", ct);
        var u = await AutorizarAsync(s, ct); var a = await Horario(u, agendamento, ct);
        var hash = ContratoTablet.Hash(ContratoTablet.Serializar(new {Acao = typeof(T).Name, pedido}));
        var recibo = await db.Set<OperacaoClinicaTablet>().SingleOrDefaultAsync(o => o.Id == chave, ct);
        if (recibo is not null)
        {
            if (recibo.UsuarioId != u.Id || recibo.AgendamentoId != a.Id || recibo.PedidoHash != hash)
                throw new ConflitoClinicoTablet("Este envio já foi usado para outra operação. Atualize antes de continuar.");
            await tx.CommitAsync(ct);
            return JsonSerializer.Deserialize<T>(recibo.ResultadoJson, ContratoTablet.Json)!;
        }
        var resultado = await acao(u, a);
        db.Set<OperacaoClinicaTablet>().Add(new() {Id = chave, UsuarioId = u.Id, AgendamentoId = a.Id,
            PedidoHash = hash, ResultadoJson = ContratoTablet.Serializar(resultado), CriadaEm = Agora});
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return resultado;
    }

    public Task<ResultadoGravacaoTablet> SalvarAsync(SessaoTablet s, int id, SalvarAtendimentoTablet pedido, CancellationToken ct)
        => Escrever(s, id, pedido.Idempotencia, pedido, async (u, a) =>
        {
            if(pedido.Evolucao is null) throw new InvalidOperationException("Informe a evolução.");
            if(pedido.Finalizar || pedido.ConcluirAoSalvar) {
                await agenda.ExigirConclusaoClinicaAsync(id, u.Id, ct);
                if (!pedido.ConcluirAoSalvar) await agenda.ConferirEnfermagemParaConclusaoAsync(id, pedido.HouveEnfermagem, ct);
            }
            var anterior = await EvolucaoAtual(id, ct);
            if (a.Status is not (StatusAgendamento.Agendado or StatusAgendamento.Realizado) || a.FimAtendimentoEm is not null)
                throw new ConflitoClinicoTablet("Este atendimento já foi concluído. Confira o histórico.");
            if (anterior is not null && anterior.ProfissionalId != u.ProfissionalId) throw new RecursoClinicoIndisponivel();
            var atual = Fotografar(anterior, anterior is null ? null : await Mapa(anterior.Id, ct));
            if (pedido.Evolucao.Id != atual.Id || pedido.Evolucao.Versao != atual.Versao)
                throw new ConflitoClinicoTablet("O atendimento mudou em outro acesso. Seu texto continua na tela; confira a versão atual antes de salvar.");
            var p = pedido.Evolucao;
            ValidarTextos(20_000, p.HistoriaDoencaAtual, p.ExameFisico, p.Conduta, p.TextoEvolucao);
            ValidarTextos(1000,p.QueixaPrincipal,p.HipoteseDiagnostica,p.PlanoTerapeutico);
            ValidarTextos(2000,p.Orientacoes);
            ValidarTextos(20, p.CidSessao);
            if (p.Mapa is {} mapa) {
                if(mapa.Pontos is null || mapa.Pontos.Length > MapaCorporal.MaximoPontos) throw new InvalidOperationException("Use até 80 pontos no mapa.");
                ValidarTextos(1000, mapa.Observacoes); foreach(var ponto in mapa.Pontos) {
                    if(ponto is null) throw new InvalidOperationException("Confira os pontos do mapa.");
                    ValidarTextos(40, ponto.Nome);ValidarTextos(200,ponto.Observacao);
                }
            }
            var dados = new Evolucao {Id = atual.Id, PacienteId = a.PacienteId, ProfissionalId = u.ProfissionalId,
                AgendamentoId = a.Id, AtendimentoId = a.AtendimentoId, Data = DateOnly.FromDateTime(a.DataHora),
                QueixaPrincipal = p.QueixaPrincipal, HistoriaDoencaAtual = p.HistoriaDoencaAtual, ExameFisico = p.ExameFisico,
                HipoteseDiagnostica = p.HipoteseDiagnostica, CidSessao = p.CidSessao, Conduta = p.Conduta,
                TextoEvolucao = p.TextoEvolucao, Orientacoes = p.Orientacoes, PlanoTerapeutico = p.PlanoTerapeutico,
                EvaAntes = p.EvaAntes, EvaDepois = p.EvaDepois, CamposPersonalizados = null!,
                RetornoSugeridoEm = anterior?.RetornoSugeridoEm, RetornoSugeridoNota = anterior?.RetornoSugeridoNota,
                Encaminhamento = anterior?.Encaminhamento};
            var salvo = await prontuario.SalvarAsync(dados, Operador(u), ct: ct, mapa: p.Mapa is null ? null : new MapaCorporal {
                Observacoes = p.Mapa.Observacoes, Pontos = p.Mapa.Pontos.Select((p, i) => new PontoMapa {
                    Face = p.Face, X = p.X, Y = p.Y, Nome = p.Nome, Tecnica = p.Tecnica, Observacao = p.Observacao, Ordem = i + 1}).ToList()});
            int guias = 0; string[] avisos = [];
            if (pedido.Finalizar || pedido.ConcluirAoSalvar)
            {
                var fim = await agenda.ConcluirAtendimentoClinicoAsync(a.Id, Operador(u), ct, u.Id, pedido.HouveEnfermagem, permitirEnfermagemPosterior: pedido.ConcluirAoSalvar);
                guias = fim.Atendimento.Codigos.Count(c => c.Status != StatusCodigo.NaoAplicavel);
                avisos = fim.Avisos.ToArray();
            }
            return new ResultadoGravacaoTablet(salvo.Id, Fotografar(salvo, await Mapa(salvo.Id, ct)).Versao,
                pedido.Finalizar || pedido.ConcluirAoSalvar, a.AtendimentoId, guias, avisos);
        }, ct);

    public Task<ResultadoDocumentoTablet> EmitirAsync(SessaoTablet s, int id, EmitirDocumentoTablet pedido, CancellationToken ct)
        => Escrever<ResultadoDocumentoTablet>(s, id, pedido.Idempotencia, pedido, async (u, a) =>
        {
            if (!u.Pode(Permissao.Prescrever)) throw new UnauthorizedAccessException();
            if (a.Status is not (StatusAgendamento.Agendado or StatusAgendamento.Realizado) || a.FimAtendimentoEm is not null)
                throw new ConflitoClinicoTablet("O atendimento está concluído. Abra um novo atendimento para emitir.");
            var e = await EvolucaoAtual(a.Id, ct);
            return await EmitirConteudoAsync(u,a.PacienteId,a.Id,e?.Id,pedido,ct);
        }, ct);

    internal async Task<ResultadoDocumentoTablet> EmitirConteudoAsync(UsuarioSistema u,int paciente,int? agendamento,int? evolucao,EmitirDocumentoTablet pedido,CancellationToken ct)
    {
            ValidarTextos(20_000, pedido.Texto);
            ValidarTextos(pedido.Tipo=="infusao"?2000:1000,pedido.Observacoes);
            if (string.IsNullOrWhiteSpace(pedido.Texto)) throw new InvalidOperationException("Escreva o conteúdo da prescrição.");
            if (pedido.Tipo == "infusao")
            {
                ValidarTextos(120, pedido.Diluente);ValidarTextos(60,pedido.Volume,pedido.TempoInfusao);
                if (!Enum.IsDefined(pedido.Via)) throw new InvalidOperationException("Escolha uma via de administração válida.");
                if(pedido.Itens is { } itensModelo) new Clinica.Domain.ModeloInfusao(pedido.Indicacao,pedido.Observacoes,itensModelo.Select(i=>new Clinica.Domain.ItemModeloInfusao(i.Descricao,i.DescricaoFormatada,i.Dose,i.Diluente,i.Volume,i.Via,i.TempoInfusao,i.SeNecessario,i.Observacoes,i.ObservacoesFormatadas)).ToArray()).Guardar();
                var p = await prescricoes.CriarAsync(paciente, u.ProfissionalId, agendamento, evolucao, Operador(u), ct);
                await prescricoes.SalvarRascunhoAsync(p.Id, pedido.Indicacao, pedido.Observacoes, pedido.Itens is {Length:>0} ? pedido.Itens.Select(i=>new ItemPrescricaoInterna {Descricao=i.Descricao,DescricaoFormatada=i.DescricaoFormatada,Dose=i.Dose,Diluente=i.Diluente,Volume=i.Volume,Via=i.Via,TempoInfusao=i.TempoInfusao,SeNecessario=i.SeNecessario,HoraPrevista=i.HoraPrevista,Observacoes=i.Observacoes,ObservacoesFormatadas=i.ObservacoesFormatadas}).ToArray() : [new ItemPrescricaoInterna {
                    Descricao = pedido.Texto, DescricaoFormatada=pedido.CorpoFormatado, Diluente = pedido.Diluente, Volume = pedido.Volume,
                    TempoInfusao = pedido.TempoInfusao, Via = pedido.Via}], Operador(u), pedido.AssinaturaEnfermagem, ct,pedido.IndicacaoFormatada,pedido.ObservacoesFormatadas);
                await Auditar(u, paciente, "TabletClinicoPrescricao", "Infusão em rascunho", ct);
                return new(p.Id, "infusao", p.Numero);
            }
            var tipo = pedido.Tipo switch {"receita" => TipoDocumentoClinico.Receita, "exame" => TipoDocumentoClinico.PedidoExame,
                "atestado" => TipoDocumentoClinico.Atestado, "comparecimento" => TipoDocumentoClinico.Comparecimento,
                "relatorio" => TipoDocumentoClinico.RelatorioEvolucao, "anamnese" => TipoDocumentoClinico.Anamnese, _ => throw new InvalidOperationException("Escolha um tipo de documento disponível.")};
            var doc = await documentos.EmitirAsync(new DocumentoClinico {PacienteId = paciente,
                ProfissionalId = u.ProfissionalId, AgendamentoId = agendamento, EvolucaoId = evolucao,
                Data = Hoje, Tipo = tipo, Corpo = pedido.Texto, CorpoFormatado=pedido.CorpoFormatado, Observacoes = pedido.Observacoes,ObservacoesFormatadas=pedido.ObservacoesFormatadas,
                DiasAfastamento = tipo == TipoDocumentoClinico.Atestado ? pedido.DiasAfastamento : null,
                Itens = tipo == TipoDocumentoClinico.PedidoExame ? [new ItemDocumento {Descricao="Solicitação conforme texto acima."}] : []}, Operador(u), ct);
            return new(doc.Id, "documento", doc.Numero);
    }

    public async Task<(UsuarioSistema Usuario, int PacienteId)> ExigirDocumentoAsync(SessaoTablet s,
        int agendamento, string tipo, int documento, bool assinar, CancellationToken ct)
    {
        if(agendamento==0)
        {
            var profissional=await AutorizarAsync(s,ct,tipo=="execucao"?Permissao.ChecarPrescricao:Permissao.VerProntuario);
            if(assinar && tipo!="execucao" && !profissional.Pode(Permissao.Prescrever)) throw new UnauthorizedAccessException();
            int? paciente=tipo is "infusao" or "execucao"
                ? await db.PrescricoesInternas.Where(p=>p.Id==documento && p.CanceladaEm==null
                    && (tipo!="execucao" || p.Situacao==SituacaoPrescricao.Assinada || p.Situacao==SituacaoPrescricao.Encerrada || (p.OrigemEnfermagem && p.AssinadaEm == null && p.Situacao == SituacaoPrescricao.Encerrada))
                    && (!assinar || tipo!="execucao" || !p.OrigemEnfermagem || p.RegistradaPorUsuarioId==profissional.Id)
                    && (!assinar || tipo=="execucao" || p.ProfissionalId==profissional.ProfissionalId)).Select(p=>(int?)p.PacienteId).SingleOrDefaultAsync(ct)
                : tipo=="documento" ? await db.DocumentosClinicos.Where(d=>d.Id==documento && d.CanceladoEm==null
                    && (!assinar || d.ProfissionalId==profissional.ProfissionalId&&(d.Tipo==TipoDocumentoClinico.Receita||d.Tipo==TipoDocumentoClinico.Atestado||d.Tipo==TipoDocumentoClinico.PedidoExame||d.Tipo==TipoDocumentoClinico.Comparecimento||d.Tipo==TipoDocumentoClinico.RelatorioEvolucao||d.Tipo==TipoDocumentoClinico.Anamnese))).Select(d=>(int?)d.PacienteId).SingleOrDefaultAsync(ct) : null;
            if(paciente is null) throw new RecursoClinicoIndisponivel();
            return(profissional,paciente.Value);
        }
        var u = await AutorizarAsync(s, ct); var a = await Horario(u, agendamento, ct);
        if (assinar && !u.Pode(Permissao.Prescrever)) throw new UnauthorizedAccessException();
        var existe = tipo == "infusao"
            ? await db.PrescricoesInternas.AnyAsync(p => p.Id == documento && p.PacienteId == a.PacienteId
                && p.CanceladaEm == null && (!assinar || (p.ProfissionalId == u.ProfissionalId && p.AgendamentoId == a.Id)), ct)
            : tipo == "documento" && await db.DocumentosClinicos.AnyAsync(d => d.Id == documento && d.PacienteId == a.PacienteId
                && d.CanceladoEm == null && (!assinar || (d.ProfissionalId == u.ProfissionalId && d.AgendamentoId == a.Id
                    && (d.Tipo == TipoDocumentoClinico.Receita || d.Tipo == TipoDocumentoClinico.Atestado || d.Tipo == TipoDocumentoClinico.PedidoExame || d.Tipo == TipoDocumentoClinico.Comparecimento || d.Tipo == TipoDocumentoClinico.RelatorioEvolucao || d.Tipo == TipoDocumentoClinico.Anamnese))), ct);
        if (!existe) throw new RecursoClinicoIndisponivel();
        return (u, a.PacienteId);
    }
    public Task<ResultadoModeloMapaTablet> SalvarModeloMapaAsync(SessaoTablet s, int id, SalvarModeloMapaTablet pedido, CancellationToken ct)
        => Escrever<ResultadoModeloMapaTablet>(s,id,pedido.Idempotencia,pedido,async(u,a)=>
        {
            ValidarTextos(100,pedido.Nome);
            if(pedido.Mapa?.Pontos is not {} pontos || pontos.Length is <1 or >80 || pontos.Any(p=>p is null))
                throw new InvalidOperationException("Marque entre 1 e 80 pontos antes de guardar o modelo.");
            ValidarTextos(1000,pedido.Mapa.Observacoes);
            foreach(var p in pontos) {ValidarTextos(40,p.Nome);ValidarTextos(200,p.Observacao);}
            // Sem opção de tornar dados deste paciente globais pela fronteira web.
            var modelo=await new MapaCorporalService(repo).SalvarComoProtocoloAsync(a.PacienteId,pedido.Nome,
                pontos.Select((p,i)=>new PontoMapa {Face=p.Face,X=p.X,Y=p.Y,Nome=p.Nome,Tecnica=p.Tecnica,Observacao=p.Observacao,Ordem=i+1}).ToArray(),
                descricao:pedido.Mapa.Observacoes,operador:Operador(u),ct:ct);
            return new(modelo.Id);
        },ct);

    private Task Auditar(UsuarioSistema u, int paciente, string acao, string detalhe, CancellationToken ct)
        => repo.RegistrarAuditoriaAsync(new EventoAuditoria {Operador = Operador(u), PacienteId = paciente, Acao = acao, Detalhe = detalhe}, ct);
    private static void ValidarTextos(int max, params string?[] textos)
    {if (textos.Any(t => t?.Length > max)) throw new InvalidOperationException($"Use no máximo {max} caracteres por campo.");}
}
