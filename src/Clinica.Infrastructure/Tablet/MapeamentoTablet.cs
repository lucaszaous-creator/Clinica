using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure.Tablet;

internal static class MapeamentoTablet
{
    public static void Aplicar(ModelBuilder b)
    {
        b.Entity<OperacaoClinicaTablet>(e =>
        {
            e.ToTable("OperacoesClinicasTablet");
            e.HasKey(x => x.Id);
            e.Property(x => x.PedidoHash).HasMaxLength(64);
            e.HasIndex(x => new {x.UsuarioId, x.AgendamentoId});
            e.HasOne<UsuarioSistema>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Agendamento>().WithMany().HasForeignKey(x => x.AgendamentoId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<SessaoTablet>(e =>
        {
            e.ToTable("SessoesTablet");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.CredencialVersao).HasMaxLength(64);
            e.Property(x => x.Dispositivo).HasMaxLength(64);
            e.Property(x => x.Modo).HasMaxLength(20);
            e.Property(x => x.Versao).IsConcurrencyToken();
            e.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.ExpiraEm);
        });
        b.Entity<ColetaTablet>(e =>
        {
            e.ToTable("ColetasTablet");
            e.HasKey(x => x.Id);
            e.Property(x => x.SessaoId).HasMaxLength(64);
            e.Property(x => x.Operadora).HasMaxLength(200);
            e.Property(x => x.IdentidadeConferida).HasMaxLength(150);
            e.Property(x => x.ChaveAtiva).HasMaxLength(160);
            e.Property(x => x.Estado).HasMaxLength(30);
            e.Property(x => x.ConteudoHash).HasMaxLength(64);
            e.Property(x => x.SubmissaoHash).HasMaxLength(64);
            e.Property(x => x.Falha).HasMaxLength(60);
            e.Property(x => x.Versao).IsConcurrencyToken();
            e.HasOne(x => x.Sessao).WithMany().HasForeignKey(x => x.SessaoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Documento).WithMany().HasForeignKey(x => x.DocumentoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Paciente).WithMany().HasForeignKey(x => x.PacienteId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.DocumentoId).IsUnique();
            e.HasIndex(x => x.ChaveAtiva).IsUnique();
            e.HasIndex(x => new { x.Estado, x.ExpiraEm });
        });
        b.Entity<ViaAssinadaPaciente>(e =>
        {
            e.ToTable("ViasAssinadasPaciente");
            e.HasKey(x => x.DocumentoId);
            e.Property(x => x.DocumentoId).ValueGeneratedNever();
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.Property(x => x.EvidenciaSha256).HasMaxLength(64);
            e.HasOne(x => x.Documento).WithOne().HasForeignKey<ViaAssinadaPaciente>(x => x.DocumentoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Coleta).WithOne().HasForeignKey<ViaAssinadaPaciente>(x => x.ColetaId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
