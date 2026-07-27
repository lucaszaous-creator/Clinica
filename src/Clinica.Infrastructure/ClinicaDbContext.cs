using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure;

public class ClinicaDbContext : DbContext
{
    public ClinicaDbContext(DbContextOptions<ClinicaDbContext> options) : base(options) { }

    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<PacienteFoto> PacientesFotos => Set<PacienteFoto>();
    public DbSet<AutorizacaoSessoes> Autorizacoes => Set<AutorizacaoSessoes>();
    public DbSet<Atendimento> Atendimentos => Set<Atendimento>();
    public DbSet<CodigoFaturamento> Codigos => Set<CodigoFaturamento>();
    public DbSet<Agendamento> Agendamentos => Set<Agendamento>();
    public DbSet<ParametroConvenio> Parametros => Set<ParametroConvenio>();
    public DbSet<ConfiguracaoGlobal> Configuracoes => Set<ConfiguracaoGlobal>();
    public DbSet<ConvenioCadastro> Convenios => Set<ConvenioCadastro>();
    public DbSet<ModalidadeCadastro> Modalidades => Set<ModalidadeCadastro>();
    public DbSet<EspecialidadeCadastro> Especialidades => Set<EspecialidadeCadastro>();
    public DbSet<Consulta> Consultas => Set<Consulta>();
    public DbSet<LoteTiss> LotesTiss => Set<LoteTiss>();
    public DbSet<EventoAuditoria> Auditoria => Set<EventoAuditoria>();
    public DbSet<CategoriaFinanceira> CategoriasFinanceiras => Set<CategoriaFinanceira>();
    public DbSet<LancamentoFinanceiro> Lancamentos => Set<LancamentoFinanceiro>();
    public DbSet<Profissional> Profissionais => Set<Profissional>();
    public DbSet<Sala> Salas => Set<Sala>();
    public DbSet<ListaEspera> ListaEspera => Set<ListaEspera>();
    public DbSet<Evolucao> Evolucoes => Set<Evolucao>();
    public DbSet<AnexoProntuario> AnexosProntuario => Set<AnexoProntuario>();
    public DbSet<ConsentimentoLgpd> Consentimentos => Set<ConsentimentoLgpd>();

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
            e.Property(p => p.ModalidadePreferidaCodigo).HasMaxLength(40);
            // Hora de parede (sem fuso), como na Agenda — evita o erro do Npgsql com DateTime local.
            e.Property(p => p.FotoAtualizadaEm).HasColumnType("timestamp without time zone");
            e.Ignore(p => p.TemFoto);
            e.Ignore(p => p.CarteirinhaVencida);
            e.HasMany(p => p.Atendimentos).WithOne(a => a.Paciente!).HasForeignKey(a => a.PacienteId);
        });

        // Retrato em tamanho cheio numa tabela própria: a lista de pacientes carrega só
        // a miniatura, e os bytes grandes só vêm do banco quando a ficha os pede.
        b.Entity<PacienteFoto>(e =>
        {
            e.ToTable("PacientesFotos");
            e.HasKey(f => f.PacienteId);
            e.Property(f => f.PacienteId).ValueGeneratedNever();
            e.Property(f => f.Conteudo).IsRequired();
            e.Property(f => f.AtualizadaEm).HasColumnType("timestamp without time zone");
            e.HasOne(f => f.Paciente).WithOne(p => p.Foto)
                .HasForeignKey<PacienteFoto>(f => f.PacienteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Cota de sessões liberada pelo convênio (previne a glosa 2006).
        b.Entity<AutorizacaoSessoes>(e =>
        {
            e.ToTable("Autorizacoes");
            e.HasKey(a => a.Id);
            e.Property(a => a.Numero).HasMaxLength(40);
            e.Property(a => a.Convenio).HasConversion<string>().HasMaxLength(40);
            e.Property(a => a.ConvenioCodigo).HasMaxLength(40);
            e.Property(a => a.Observacoes).HasMaxLength(500);
            e.HasOne(a => a.Paciente).WithMany().HasForeignKey(a => a.PacienteId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(a => a.PacienteId);
            e.HasIndex(a => a.DataValidade);
        });

        b.Entity<Atendimento>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Numero).HasMaxLength(30);
            e.HasIndex(a => a.Numero);
            e.Property(a => a.Modalidade).HasConversion<string>().HasMaxLength(40);
            e.Property(a => a.ModalidadeCodigo).HasMaxLength(40);
            e.Property(a => a.EspecialidadeConsulta).HasConversion<string>().HasMaxLength(30);
            e.Property(a => a.EspecialidadeConsultaCodigo).HasMaxLength(40);
            e.Property(a => a.Categoria).HasConversion<string>().HasMaxLength(20);
            e.HasMany(a => a.Codigos).WithOne(c => c.Atendimento!).HasForeignKey(c => c.AtendimentoId);
        });

        b.Entity<CodigoFaturamento>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Tipo).HasConversion<string>().HasMaxLength(40);
            e.Property(c => c.Especialidade).HasConversion<string>().HasMaxLength(30);
            e.Property(c => c.EspecialidadeCodigo).HasMaxLength(40);
            e.Property(c => c.Ordem).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.FormaObtencao).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.NumeroGuiaReal).HasMaxLength(60);
            e.Property(c => c.UsuarioBaixa).HasMaxLength(80);
            e.Property(c => c.ObservacaoPendencia).HasMaxLength(500);
            // Hora de parede (sem fuso), como na Agenda/Auditoria — evita o erro do Npgsql com DateTime local.
            e.Property(c => c.ObservacaoPendenciaEm).HasColumnType("timestamp without time zone");
            e.Property(c => c.NaoConformidadeJustificativa).HasMaxLength(500);
            e.Property(c => c.NaoConformidadeEm).HasColumnType("timestamp without time zone");
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

        b.Entity<ModalidadeCadastro>(e =>
        {
            e.HasKey(c => c.Codigo);
            e.Property(c => c.Codigo).HasMaxLength(40);
            e.Property(c => c.Nome).HasMaxLength(80);
            e.Property(c => c.Base).HasConversion<string>().HasMaxLength(40);
        });

        b.Entity<EspecialidadeCadastro>(e =>
        {
            e.HasKey(c => c.Codigo);
            e.Property(c => c.Codigo).HasMaxLength(40);
            e.Property(c => c.Nome).HasMaxLength(80);
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
            e.Property(a => a.ModalidadeCodigo).HasMaxLength(40);
            e.Property(a => a.EspecialidadeConsulta).HasConversion<string>().HasMaxLength(30);
            e.Property(a => a.EspecialidadeConsultaCodigo).HasMaxLength(40);
            e.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.Origem).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.Observacoes).HasMaxLength(500);
            e.HasOne(a => a.Paciente).WithMany().HasForeignKey(a => a.PacienteId);
            // Sem cascade a partir do atendimento (relação opcional).
            e.HasOne(a => a.Atendimento).WithMany().HasForeignKey(a => a.AtendimentoId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(a => a.DataHora);

            // Fundação da recepção: recursos disputados e carimbos do kanban. Todos
            // opcionais — desligar um profissional não pode apagar a agenda dele.
            e.Property(a => a.ChegadaEm).HasColumnType("timestamp without time zone");
            e.Property(a => a.InicioAtendimentoEm).HasColumnType("timestamp without time zone");
            e.HasOne(a => a.Profissional).WithMany()
                .HasForeignKey(a => a.ProfissionalId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(a => a.Sala).WithMany()
                .HasForeignKey(a => a.SalaId).OnDelete(DeleteBehavior.SetNull);
            // A grade da agenda filtra por profissional dentro de um dia.
            e.HasIndex(a => new { a.ProfissionalId, a.DataHora });
            // Propriedades calculadas (Etapa, FimPrevisto, Rotulo…) vivem só em memória.
            e.Ignore(a => a.Etapa);
            e.Ignore(a => a.DuracaoEfetiva);
            e.Ignore(a => a.FimPrevisto);
            e.Ignore(a => a.OcupaAgenda);
        });

        // ---------- Fundação da recepção (parcela 1) ----------
        // Profissional e Sala são os recursos que a agenda disputa; a lista de espera
        // é quem entra quando um deles vaga.
        b.Entity<Profissional>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Nome).IsRequired().HasMaxLength(120);
            e.Property(p => p.NomeCurto).HasMaxLength(40);
            e.Property(p => p.RegistroConselho).HasMaxLength(40);
            e.Property(p => p.EspecialidadeCodigo).HasMaxLength(40);
            e.Property(p => p.Telefone).HasMaxLength(20);
            e.Property(p => p.Email).HasMaxLength(120);
            e.Property(p => p.Cor).HasMaxLength(9);
            e.Property(p => p.Observacoes).HasMaxLength(500);
            e.Ignore(p => p.Rotulo);
            e.HasIndex(p => p.Nome);
        });

        b.Entity<Sala>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Nome).IsRequired().HasMaxLength(80);
            e.Property(s => s.Observacoes).HasMaxLength(500);
            e.HasIndex(s => s.Nome).IsUnique();
        });

        b.Entity<ListaEspera>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.ModalidadeCodigo).HasMaxLength(40);
            e.Property(l => l.Periodo).HasConversion<string>().HasMaxLength(20);
            e.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(l => l.Observacoes).HasMaxLength(500);
            e.Property(l => l.CriadoEm).HasColumnType("timestamp without time zone");
            e.Property(l => l.ResolvidoEm).HasColumnType("timestamp without time zone");

            e.HasOne(l => l.Paciente).WithMany().HasForeignKey(l => l.PacienteId);
            e.HasOne(l => l.Profissional).WithMany()
                .HasForeignKey(l => l.ProfissionalId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(l => l.Agendamento).WithMany()
                .HasForeignKey(l => l.AgendamentoId).OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(l => l.Status);
        });

        // ---------- Prontuário e LGPD (parcela 2) ----------
        // A evolução é do PACIENTE; o vínculo com atendimento/agendamento é opcional,
        // porque a sessão acontece antes de a guia existir (e a particular não gera guia).
        b.Entity<Evolucao>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.QueixaPrincipal).HasMaxLength(1000);
            e.Property(x => x.Conduta).HasMaxLength(4000);
            e.Property(x => x.TextoEvolucao).HasMaxLength(4000);
            e.Property(x => x.Orientacoes).HasMaxLength(2000);
            e.Property(x => x.CriadoPor).HasMaxLength(80);
            e.Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");
            e.Property(x => x.AtualizadoEm).HasColumnType("timestamp without time zone");

            e.HasOne(x => x.Paciente).WithMany().HasForeignKey(x => x.PacienteId);
            e.HasOne(x => x.Profissional).WithMany()
                .HasForeignKey(x => x.ProfissionalId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Atendimento).WithMany()
                .HasForeignKey(x => x.AtendimentoId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Agendamento).WithMany()
                .HasForeignKey(x => x.AgendamentoId).OnDelete(DeleteBehavior.SetNull);

            // O prontuário é sempre lido por paciente, do mais recente para o mais antigo.
            e.HasIndex(x => new { x.PacienteId, x.Data });

            e.Ignore(x => x.VariacaoEva);
            e.Ignore(x => x.TemParEva);
        });

        b.Entity<AnexoProntuario>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.NomeArquivo).IsRequired().HasMaxLength(200);
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.TipoConteudo).HasMaxLength(120);
            e.Property(x => x.Descricao).HasMaxLength(500);
            e.Property(x => x.CriadoPor).HasMaxLength(80);
            e.Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");

            // Apagar a evolução leva os anexos junto: anexo órfão não é prontuário.
            e.HasOne(x => x.Evolucao).WithMany(x => x.Anexos)
                .HasForeignKey(x => x.EvolucaoId).OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.EvolucaoId);
        });

        b.Entity<ConsentimentoLgpd>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Finalidade).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.VersaoTermo).HasMaxLength(40);
            e.Property(x => x.RegistradoPor).HasMaxLength(80);
            e.Property(x => x.Observacoes).HasMaxLength(500);
            e.Property(x => x.RegistradoEm).HasColumnType("timestamp without time zone");
            e.Property(x => x.RevogadoEm).HasColumnType("timestamp without time zone");

            e.HasOne(x => x.Paciente).WithMany().HasForeignKey(x => x.PacienteId);

            e.HasIndex(x => new { x.PacienteId, x.Finalidade });
            e.Ignore(x => x.Vigente);
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

        // ---------- Financeiro ----------
        // O dinheiro vive só aqui: as entidades de faturamento seguem sem campo de valor.
        // As FKs apontam do financeiro PARA o faturamento (sentido único), e são
        // opcionais — uma despesa da clínica não tem guia nem paciente.
        b.Entity<CategoriaFinanceira>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Codigo).IsRequired().HasMaxLength(40);
            e.Property(c => c.Nome).IsRequired().HasMaxLength(80);
            e.Property(c => c.Tipo).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(c => c.Codigo).IsUnique();
        });

        b.Entity<LancamentoFinanceiro>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Descricao).IsRequired().HasMaxLength(200);
            // Dinheiro em decimal exato — nunca ponto flutuante.
            e.Property(x => x.Valor).HasPrecision(14, 2);
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.FormaPagamento).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Convenio).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.ConvenioCodigo).HasMaxLength(40);
            e.Property(x => x.Observacoes).HasMaxLength(500);
            e.Property(x => x.CriadoPor).HasMaxLength(80);
            e.Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");

            e.HasOne(x => x.Categoria).WithMany()
                .HasForeignKey(x => x.CategoriaFinanceiraId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Paciente).WithMany()
                .HasForeignKey(x => x.PacienteId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Atendimento).WithMany()
                .HasForeignKey(x => x.AtendimentoId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.CodigoFaturamento).WithMany()
                .HasForeignKey(x => x.CodigoFaturamentoId).OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(x => x.Data);
            e.HasIndex(x => x.Status);
            // Conciliação: achar rápido o lançamento de uma guia.
            e.HasIndex(x => x.CodigoFaturamentoId);
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
            b.Entity<LancamentoFinanceiro>().Property<uint>("xmin").IsRowVersion();
            // Prontuário: dois postos abrindo a mesma evolução não podem sobrescrever
            // o texto um do outro em silêncio.
            b.Entity<Evolucao>().Property<uint>("xmin").IsRowVersion();
        }
    }
}
