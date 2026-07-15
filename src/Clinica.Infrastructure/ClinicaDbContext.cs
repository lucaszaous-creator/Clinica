using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure;

public class ClinicaDbContext : DbContext
{
    public ClinicaDbContext(DbContextOptions<ClinicaDbContext> options) : base(options) { }

    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<Atendimento> Atendimentos => Set<Atendimento>();
    public DbSet<CodigoFaturamento> Codigos => Set<CodigoFaturamento>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Paciente>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Nome).IsRequired().HasMaxLength(200);
            e.Property(p => p.Documento).HasMaxLength(30);
            e.Property(p => p.Telefone).HasMaxLength(30);
            e.Property(p => p.Convenio).HasConversion<string>().HasMaxLength(40);
            e.Property(p => p.Sexo).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.Categoria).HasConversion<string>().HasMaxLength(20);
            e.HasMany(p => p.Atendimentos).WithOne(a => a.Paciente!).HasForeignKey(a => a.PacienteId);
        });

        b.Entity<Atendimento>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Modalidade).HasConversion<string>().HasMaxLength(40);
            e.Property(a => a.Categoria).HasConversion<string>().HasMaxLength(20);
            e.HasMany(a => a.Codigos).WithOne(c => c.Atendimento!).HasForeignKey(c => c.AtendimentoId);
        });

        b.Entity<CodigoFaturamento>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Tipo).HasConversion<string>().HasMaxLength(40);
            e.Property(c => c.Especialidade).HasConversion<string>().HasMaxLength(30);
            e.Property(c => c.Ordem).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.FormaObtencao).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.NumeroGuiaReal).HasMaxLength(60);
            e.Property(c => c.UsuarioBaixa).HasMaxLength(80);
            e.Ignore(c => c.Baixado);
            // Índice para a consulta de pendências (códigos ainda sem baixa).
            e.HasIndex(c => new { c.DataBaixa, c.DataPrevistaFaturamento });
        });
    }
}
