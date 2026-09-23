using System.Data;
using System.Text.Json;
using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure.Tablet;

public sealed partial class PostoTabletService
{
    private static string VersaoModeloEvolucao(ModeloEvolucao m) => Hash(new
    {
        m.Id, m.Nome, m.ProfissionalId, m.Ativo,
        AtualizadoEm = m.AtualizadoEm?.ToString("yyyy-MM-ddTHH:mm:ss.ffffff", System.Globalization.CultureInfo.InvariantCulture),
        m.QueixaPrincipal, m.HistoriaDoencaAtual, m.ExameFisico, m.HipoteseDiagnostica,
        m.CidSessao, m.TextoEvolucao, m.Conduta, m.Orientacoes, m.PlanoTerapeutico
    });

    private static ModeloEvolucaoTablet DadosModeloEvolucao(ModeloEvolucao m) => new(
        m.Id, m.Nome, m.ProfissionalId is null, m.Ativo, VersaoModeloEvolucao(m),
        m.QueixaPrincipal, m.HistoriaDoencaAtual, m.ExameFisico, m.HipoteseDiagnostica,
        m.CidSessao, m.TextoEvolucao, m.Conduta, m.Orientacoes, m.PlanoTerapeutico);

    public async Task<IReadOnlyList<ModeloEvolucaoTablet>> ModelosEvolucaoAsync(SessaoTablet s, CancellationToken ct)
    {
        var u = await Autorizar(s, ct, Permissao.EditarProntuario);
        var modelos = await db.ModelosEvolucao.AsNoTracking()
            .Where(m => m.Ativo && (m.ProfissionalId == null || m.ProfissionalId == u.ProfissionalId))
            .OrderBy(m => m.Nome).ThenBy(m => m.Id).ToListAsync(ct);
        return modelos.Select(DadosModeloEvolucao).ToArray();
    }

    public async Task<ModeloEvolucaoTablet> SalvarModeloEvolucaoAsync(
        SessaoTablet s, SalvarModeloEvolucaoTablet p, CancellationToken ct)
    {
        if (p.Idempotencia == Guid.Empty || p.Id < 0)
            throw ErroFormularioTablet.Criar("Atualize os modelos antes de salvar.");
        Textos(100, p.Nome);
        Textos(2000, p.QueixaPrincipal, p.Conduta, p.Orientacoes);
        Textos(4000, p.HistoriaDoencaAtual, p.ExameFisico, p.TextoEvolucao);
        Textos(1000, p.HipoteseDiagnostica, p.PlanoTerapeutico);
        Textos(20, p.CidSessao);
        if (string.IsNullOrWhiteSpace(p.Nome)) throw ErroFormularioTablet.Criar("Informe o nome do modelo.");

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(20260922, 2)", ct);
        var u = await Autorizar(s, ct, Permissao.EditarProntuario);
        var hash = Hash(new { acao = "TabletModeloEvolucao", pedido = p });
        var recibo = await db.Set<OperacaoClinicaTablet>().SingleOrDefaultAsync(o => o.Id == p.Idempotencia, ct);
        if (recibo is not null)
        {
            if (recibo.UsuarioId != u.Id || recibo.PacienteId is not null || recibo.PedidoHash != hash)
                throw new ConflitoClinicoTablet("Este envio já foi usado. Atualize os modelos antes de continuar.");
            await tx.CommitAsync(ct);
            return JsonSerializer.Deserialize<ModeloEvolucaoTablet>(recibo.ResultadoJson, ContratoTablet.Json)!;
        }

        if (p.Compartilhado && !u.Pode(Permissao.GerenciarUsuarios))
            throw new UnauthorizedAccessException("Somente a gerência pode alterar modelos compartilhados.");
        var nome = p.Nome.Trim();
        var dono = p.Compartilhado ? null : u.ProfissionalId;
        var duplicado = await db.ModelosEvolucao.AnyAsync(m => m.Id != p.Id && m.ProfissionalId == dono
            && m.Nome.ToLower() == nome.ToLower(), ct);
        if (duplicado) throw ErroFormularioTablet.Criar("Já existe um modelo com esse nome neste escopo.");
        ModeloEvolucao modelo;
        if (p.Id == 0)
        {
            if (!p.Ativo) throw ErroFormularioTablet.Criar("Um modelo novo deve começar ativo.");
            modelo = new ModeloEvolucao { ProfissionalId = dono, CriadoPor = u.Login };
            db.ModelosEvolucao.Add(modelo);
        }
        else
        {
            modelo = await db.ModelosEvolucao.SingleOrDefaultAsync(m => m.Id == p.Id, ct)
                ?? throw new RecursoClinicoIndisponivel();
            if (modelo.ProfissionalId != dono) throw new UnauthorizedAccessException();
            ConferirVersao(p.Versao, VersaoModeloEvolucao(modelo));
        }
        modelo.Nome = nome;
        modelo.QueixaPrincipal = p.QueixaPrincipal?.Trim();
        modelo.HistoriaDoencaAtual = p.HistoriaDoencaAtual?.Trim();
        modelo.ExameFisico = p.ExameFisico?.Trim();
        modelo.HipoteseDiagnostica = p.HipoteseDiagnostica?.Trim();
        modelo.CidSessao = p.CidSessao?.Trim();
        modelo.TextoEvolucao = p.TextoEvolucao?.Trim();
        modelo.Conduta = p.Conduta?.Trim();
        modelo.Orientacoes = p.Orientacoes?.Trim();
        modelo.PlanoTerapeutico = p.PlanoTerapeutico?.Trim();
        modelo.Ativo = p.Ativo;
        modelo.AtualizadoEm = DateTime.Now;
        if (!modelo.TemConteudo) throw ErroFormularioTablet.Criar("Escreva ao menos um campo do modelo.");
        await db.SaveChangesAsync(ct);
        // O PostgreSQL persiste microssegundos e devolve Kind=Unspecified. A versão
        // enviada ao navegador precisa ser calculada sobre o valor relido do banco.
        await db.Entry(modelo).ReloadAsync(ct);
        var resultado = DadosModeloEvolucao(modelo);
        db.Set<OperacaoClinicaTablet>().Add(new OperacaoClinicaTablet
        {
            Id = p.Idempotencia, UsuarioId = u.Id, PacienteId = null,
            PedidoHash = hash, ResultadoJson = ContratoTablet.Serializar(resultado), CriadaEm = acesso.Agora
        });
        db.Auditoria.Add(new EventoAuditoria
        {
            Operador = u.Login, Acao = p.Ativo ? "TabletModeloEvolucaoSalvo" : "TabletModeloEvolucaoArquivado",
            Detalhe = $"Modelo de evolução #{modelo.Id}: {modelo.Nome}"
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return resultado;
    }

    public async Task<ResultadoFichaTablet> SalvarModeloDocumentoGlobalAsync(
        SessaoTablet s, NovoModeloDocumentoTablet p, CancellationToken ct)
    {
        if (p.Idempotencia == Guid.Empty || p.Id < 0)
            throw ErroFormularioTablet.Criar("Atualize os modelos antes de salvar.");
        Textos(100, p.Nome);
        Textos(20000, p.Texto);
        Textos(250000, p.CorpoFormatado);
        if (!TiposEditaveis.Contains(p.Tipo) || string.IsNullOrWhiteSpace(p.Nome))
            throw ErroFormularioTablet.Criar("Confira o tipo e o nome do modelo.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(20260922, 3)", ct);
        var u = await Autorizar(s, ct, Permissao.Prescrever);
        var hash = Hash(new { acao = "TabletModeloDocumentoGlobal", pedido = p });
        var recibo = await db.Set<OperacaoClinicaTablet>().SingleOrDefaultAsync(o => o.Id == p.Idempotencia, ct);
        if (recibo is not null)
        {
            if (recibo.UsuarioId != u.Id || recibo.PacienteId is not null || recibo.PedidoHash != hash)
                throw new ConflitoClinicoTablet("Este envio já foi usado. Atualize os modelos antes de continuar.");
            await tx.CommitAsync(ct);
            return JsonSerializer.Deserialize<ResultadoFichaTablet>(recibo.ResultadoJson, ContratoTablet.Json)!;
        }
        if (await db.ModelosDocumento.AnyAsync(m => m.Id != p.Id && m.Tipo == p.Tipo
            && m.Nome.ToLower() == p.Nome.Trim().ToLower(), ct))
            throw ErroFormularioTablet.Criar("Já existe um modelo com esse nome e tipo.");
        if (p.Id != 0)
        {
            var anterior = await repo.ObterModeloDocumentoAsync(p.Id, ct) ?? throw new RecursoClinicoIndisponivel();
            ConferirVersao(p.Versao, VersaoModelo(anterior));
        }
        var modelo = await Documentos.SalvarModeloAsync(new ModeloDocumento
        {
            Id = p.Id, Nome = p.Nome, Tipo = p.Tipo, Corpo = p.Texto,
            CorpoFormatado = p.CorpoFormatado, ParaInfusao = p.ParaInfusao,
            ConfiguracaoInfusao = p.ConfiguracaoInfusao, Ativo = true
        }, u.Login, ct, substituirPorNome: false);
        var resultado = new ResultadoFichaTablet(modelo.Id);
        db.Set<OperacaoClinicaTablet>().Add(new OperacaoClinicaTablet
        {
            Id = p.Idempotencia, UsuarioId = u.Id, PacienteId = null,
            PedidoHash = hash, ResultadoJson = ContratoTablet.Serializar(resultado), CriadaEm = acesso.Agora
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return resultado;
    }
}
