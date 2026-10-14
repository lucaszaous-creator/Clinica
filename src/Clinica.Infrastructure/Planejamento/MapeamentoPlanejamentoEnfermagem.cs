using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure.Planejamento;

internal static class MapeamentoPlanejamentoEnfermagem
{
    internal static void Aplicar(ModelBuilder b)
    {
        b.Entity<SolicitacaoEnfermagem>(e =>
        {
            e.ToTable("SolicitacoesEnfermagem");
            e.HasIndex(x => new { x.AutoraId, x.Idempotencia }).IsUnique();
            e.HasIndex(x => new { x.Inicio, x.Situacao });
            e.Property(x => x.PedidoHash).HasMaxLength(64);
            e.Property(x => x.AgendaReferencia).HasMaxLength(64);
            e.Property(x => x.Observacao).HasMaxLength(1000);
            e.Property(x => x.Situacao).HasMaxLength(30);
            e.Property(x => x.VistaPor).HasMaxLength(200);
            e.Property(x => x.VistaEm).HasColumnType("timestamp without time zone");
            e.Property(x => x.Modalidade).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.Versao).IsConcurrencyToken();
            foreach (var nome in new[] { "Inicio", "Fim", "Validade", "CriadaEm" })
                e.Property<DateTime>(nome).HasColumnType("timestamp without time zone");
            e.HasOne(x => x.Autora).WithMany().HasForeignKey(x => x.AutoraId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<EventoSolicitacaoEnfermagem>(e =>
        {
            e.ToTable("EventosSolicitacaoEnfermagem");
            e.HasIndex(x => new { x.SolicitacaoEnfermagemId, x.Idempotencia }).IsUnique();
            e.Property(x => x.Em).HasColumnType("timestamp without time zone");
            e.Property(x => x.Autor).HasMaxLength(200);
            e.Property(x => x.Acao).HasMaxLength(40);
            e.Property(x => x.Observacao).HasMaxLength(1000);
            e.Property(x => x.PedidoHash).HasMaxLength(64);
            e.HasOne(x => x.Solicitacao).WithMany(x => x.Eventos).HasForeignKey(x => x.SolicitacaoEnfermagemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<UsuarioSistema>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
