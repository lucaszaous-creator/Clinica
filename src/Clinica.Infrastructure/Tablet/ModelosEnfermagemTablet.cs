using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure.Tablet;

public sealed partial class PostoTabletService
{
    private static string ChaveModeloEnfermagem(string nome)
        => new string(nome.Trim().Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray()).ToUpperInvariant();

    private static ModeloEnfermagemTablet DadosModeloEnfermagem(ModeloEvolucaoEnfermagem m)
        => new(m.Id, m.Nome, m.Texto, m.Versao.ToString("N"));

    public async Task<IReadOnlyList<ModeloEnfermagemTablet>> ModelosEnfermagemAsync(SessaoTablet s, CancellationToken ct)
    {
        await Autorizar(s, ct, Permissao.RegistrarEvolucaoEnfermagem);
        var modelos = await db.ModelosEvolucaoEnfermagem.AsNoTracking()
            .OrderBy(m => m.NomeChave).ToListAsync(ct);
        return modelos.Select(DadosModeloEnfermagem).ToArray();
    }

    public async Task<ModeloEnfermagemTablet> SalvarModeloEnfermagemAsync(
        SessaoTablet s, SalvarModeloEnfermagemTablet p, CancellationToken ct)
    {
        if (p.Idempotencia == Guid.Empty || p.Id < 0)
            throw new InvalidOperationException("Atualize a lista antes de salvar o modelo.");
        Textos(100, p.Nome);
        Textos(4000, p.Texto);
        if (string.IsNullOrWhiteSpace(p.Nome) || string.IsNullOrWhiteSpace(p.Texto))
            throw new InvalidOperationException("Informe o nome e o texto do modelo.");

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(20260922, 1)", ct);
        var u = await Autorizar(s, ct, Permissao.RegistrarEvolucaoEnfermagem);
        var hash = Hash(new { acao = "TabletModeloEnfermagem", pedido = p });
        var recibo = await db.Set<OperacaoClinicaTablet>()
            .SingleOrDefaultAsync(o => o.Id == p.Idempotencia, ct);
        if (recibo is not null)
        {
            if (recibo.UsuarioId != u.Id || recibo.PacienteId is not null || recibo.PedidoHash != hash)
                throw new ConflitoClinicoTablet("Este envio já foi usado. Atualize os modelos antes de continuar.");
            await tx.CommitAsync(ct);
            return JsonSerializer.Deserialize<ModeloEnfermagemTablet>(recibo.ResultadoJson, ContratoTablet.Json)!;
        }

        var nome = p.Nome.Trim();
        var chave = ChaveModeloEnfermagem(nome);
        if (await db.ModelosEvolucaoEnfermagem.AnyAsync(m => m.NomeChave == chave && m.Id != p.Id, ct))
            throw new InvalidOperationException("Já existe um modelo com este nome. Escolha outro nome ou edite o existente.");

        ModeloEvolucaoEnfermagem modelo;
        if (p.Id == 0)
        {
            modelo = new ModeloEvolucaoEnfermagem { CriadoPor = u.Login, CriadoEm = DateTime.Now };
            db.ModelosEvolucaoEnfermagem.Add(modelo);
        }
        else
        {
            modelo = await db.ModelosEvolucaoEnfermagem.SingleOrDefaultAsync(m => m.Id == p.Id, ct)
                ?? throw new RecursoClinicoIndisponivel();
            if (!Guid.TryParse(p.Versao, out var versao) || versao != modelo.Versao)
                throw new ConflitoClinicoTablet("O modelo foi alterado. Atualize a lista antes de editar.");
        }

        modelo.Nome = nome;
        modelo.NomeChave = chave;
        modelo.Texto = p.Texto.Trim();
        modelo.Versao = Guid.NewGuid();
        modelo.AtualizadoEm = DateTime.Now;
        modelo.AtualizadoPor = u.Login;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflitoClinicoTablet("O modelo foi alterado. Atualize a lista antes de editar.");
        }

        var resultado = DadosModeloEnfermagem(modelo);
        db.Set<OperacaoClinicaTablet>().Add(new OperacaoClinicaTablet
        {
            Id = p.Idempotencia, UsuarioId = u.Id, PacienteId = null,
            PedidoHash = hash, ResultadoJson = ContratoTablet.Serializar(resultado), CriadaEm = acesso.Agora
        });
        db.Auditoria.Add(new EventoAuditoria
        {
            Operador = u.Login, Acao = p.Id == 0 ? "TabletModeloEnfermagemCriado" : "TabletModeloEnfermagemEditado",
            Detalhe = $"Modelo de enfermagem #{modelo.Id}: {modelo.Nome}"
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return resultado;
    }
}
