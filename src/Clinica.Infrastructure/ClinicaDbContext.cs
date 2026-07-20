using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure;

public class ClinicaDbContext : DbContext
{
    public ClinicaDbContext(DbContextOptions<ClinicaDbContext> options) : base(options) { }

    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<Atendimento> Atendimentos => Set<Atendimento>();
    public DbSet<CodigoFaturamento> Codigos => Set<CodigoFaturamento>();
    public DbSet<Agendamento> Agendamentos => Set<Agendamento>();
    public DbSet<ParametroConvenio> Parametros => Set<ParametroConvenio>();
    public DbSet<ConfiguracaoGlobal> Configuracoes => Set<ConfiguracaoGlobal>();
    public DbSet<ConvenioCadastro> Convenios => Set<ConvenioCadastro>();
    public DbSet<Consulta> Consultas => Set<Consulta>();
    public DbSet<LoteTiss> LotesTiss => Set<LoteTiss>();
    public DbSet<EventoAuditoria> Auditoria => Set<EventoAuditoria>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Paciente>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Nome).IsRequired().HasMaxLength(200);
            e.Property(p => p.Documento).HasMaxLength(30);
            e.Property(p => p.Telefone).HasMaxLength(30);
            e.Property(p => p.Carteirinha).HasMaxLength(40);
            e.Property(p => p.Convenio).HasConversion<string>().HasMaxLength(40);
            e.Property(p => p.ConvenioCodigo).HasMaxLength(40);
            e.Property(p => p.Sexo).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.Categoria).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.ModalidadePreferida).HasConversion<string>().HasMaxLength(40);
            e.HasMany(p => p.Atendimentos).WithOne(a => a.Paciente!).HasForeignKey(a => a.PacienteId);
        });

        b.Entity<Atendimento>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Numero).HasMaxLength(30);
            e.HasIndex(a => a.Numero);
            e.Property(a => a.Modalidade).HasConversion<string>().HasMaxLength(40);
            e.Property(a => a.EspecialidadeConsulta).HasConversion<string>().HasMaxLength(30);
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
            e.Property(c => c.ObservacaoPendencia).HasMaxLength(500);
            // Hora de parede (sem fuso), como na Agenda/Auditoria — evita o erro do Npgsql com DateTime local.
            e.Property(c => c.ObservacaoPendenciaEm).HasColumnType("timestamp without time zone");
            e.Property(c => c.Glosa).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.MotivoGlosa).HasMaxLength(300);
            e.Property(c => c.MotivoGlosaCodigo).HasMaxLength(10);
            e.Ignore(c => c.Baixado);
            e.Ignore(c => c.GlosaEmAberto);
            // Índice para a consulta de pendências (códigos ainda sem baixa).
            e.HasIndex(c => new { c.DataBaixa, c.DataPrevistaFaturamento });
            // Apagar um lote não apaga as guias — elas voltam a ficar "sem lote".
            e.HasOne(c => c.Lote).WithMany(l => l.Codigos).HasForeignKey(c => c.LoteTissId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(c => c.LoteTissId);
        });

        b.Entity<LoteTiss>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(l => l.RegistroAnsOperadora).HasMaxLength(20);
            e.Property(l => l.ProtocoloOperadora).HasMaxLength(60);
            e.Property(l => l.ObservacaoRetorno).HasMaxLength(500);
            e.HasIndex(l => l.Numero).IsUnique();
        });

        b.Entity<ParametroConvenio>(e =>
        {
            e.HasKey(p => p.Convenio);
            e.Property(p => p.Convenio).HasConversion<string>().HasMaxLength(40);
            e.Property(p => p.Nome).HasMaxLength(80);
            e.Property(p => p.CategoriaComApp).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.CategoriaSemApp).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<ConfiguracaoGlobal>(e =>
        {
            e.HasKey(c => c.Chave);
            e.Property(c => c.Chave).HasMaxLength(60);
            // Sem limite: guarda também estruturas serializadas (ex.: dados do prestador em JSON).
        });

        b.Entity<ConvenioCadastro>(e =>
        {
            e.HasKey(c => c.Codigo);
            e.Property(c => c.Codigo).HasMaxLength(40);
            e.Property(c => c.Nome).HasMaxLength(80);
            e.Property(c => c.Familia).HasConversion<string>().HasMaxLength(40);
            e.Property(c => c.FormaSegundoCodigo).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.CategoriaComApp).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.CategoriaSemApp).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<Consulta>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Convenio).HasConversion<string>().HasMaxLength(40);
            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.Observacoes).HasMaxLength(500);
            e.HasOne(c => c.Paciente).WithMany(p => p.Consultas).HasForeignKey(c => c.PacienteId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(c => c.PacienteId);
            e.HasIndex(c => c.DataVencimento);
        });

        b.Entity<Agendamento>(e =>
        {
            e.HasKey(a => a.Id);
            // Hora de parede (sem fuso). Evita o erro do Npgsql com DateTime local/unspecified.
            e.Property(a => a.DataHora).HasColumnType("timestamp without time zone");
            e.Property(a => a.ModalidadePrevista).HasConversion<string>().HasMaxLength(40);
            e.Property(a => a.EspecialidadeConsulta).HasConversion<string>().HasMaxLength(30);
            e.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.Origem).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.Observacoes).HasMaxLength(500);
            e.HasOne(a => a.Paciente).WithMany().HasForeignKey(a => a.PacienteId);
            // Sem cascade a partir do atendimento (relação opcional).
            e.HasOne(a => a.Atendimento).WithMany().HasForeignKey(a => a.AtendimentoId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(a => a.DataHora);
        });

        b.Entity<EventoAuditoria>(e =>
        {
            e.HasKey(x => x.Id);
            // Hora de parede (sem fuso), como na Agenda — evita o erro do Npgsql com DateTime local.
            e.Property(x => x.DataHora).HasColumnType("timestamp without time zone");
            e.Property(x => x.Operador).IsRequired().HasMaxLength(80);
            e.Property(x => x.Acao).IsRequired().HasMaxLength(40);
            e.Property(x => x.Detalhe).HasMaxLength(500);
            e.HasIndex(x => x.DataHora);
            e.HasIndex(x => x.CodigoId);
        });

        // Controle de concorrência otimista via coluna de sistema xmin do PostgreSQL:
        // duas máquinas editando o mesmo registro não se sobrescrevem em silêncio — a
        // segunda gravação falha e o repositório traduz num aviso para atualizar a tela.
        // Só no Npgsql (os testes rodam em SQLite, que não tem xmin).
        if (Database.IsNpgsql())
        {
            b.Entity<Paciente>().Property<uint>("xmin").IsRowVersion();
            b.Entity<Atendimento>().Property<uint>("xmin").IsRowVersion();
            b.Entity<CodigoFaturamento>().Property<uint>("xmin").IsRowVersion();
            b.Entity<LoteTiss>().Property<uint>("xmin").IsRowVersion();
            b.Entity<Consulta>().Property<uint>("xmin").IsRowVersion();
            b.Entity<Agendamento>().Property<uint>("xmin").IsRowVersion();
        }
    }
}
