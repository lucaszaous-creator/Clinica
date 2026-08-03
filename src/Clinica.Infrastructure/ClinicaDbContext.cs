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
    public DbSet<LancamentoRecorrente> Recorrentes => Set<LancamentoRecorrente>();
    public DbSet<FechamentoCaixa> FechamentosCaixa => Set<FechamentoCaixa>();
    public DbSet<Profissional> Profissionais => Set<Profissional>();
    public DbSet<Sala> Salas => Set<Sala>();
    public DbSet<ListaEspera> ListaEspera => Set<ListaEspera>();
    public DbSet<Evolucao> Evolucoes => Set<Evolucao>();
    public DbSet<AnexoProntuario> AnexosProntuario => Set<AnexoProntuario>();
    public DbSet<ConsentimentoLgpd> Consentimentos => Set<ConsentimentoLgpd>();
    public DbSet<MedidaClinica> MedidasClinicas => Set<MedidaClinica>();
    public DbSet<ProblemaPaciente> ProblemasPaciente => Set<ProblemaPaciente>();
    public DbSet<AvaliacaoClinica> AvaliacoesClinicas => Set<AvaliacaoClinica>();
    public DbSet<RespostaAvaliacao> RespostasAvaliacao => Set<RespostaAvaliacao>();
    public DbSet<MapaCorporal> MapasCorporais => Set<MapaCorporal>();
    public DbSet<PontoMapa> PontosMapa => Set<PontoMapa>();
    public DbSet<ProtocoloCorporal> ProtocolosCorporais => Set<ProtocoloCorporal>();
    public DbSet<PontoProtocolo> PontosProtocolo => Set<PontoProtocolo>();
    public DbSet<DocumentoClinico> DocumentosClinicos => Set<DocumentoClinico>();
    public DbSet<ItemDocumento> ItensDocumento => Set<ItemDocumento>();
    public DbSet<ModeloDocumento> ModelosDocumento => Set<ModeloDocumento>();
    public DbSet<ItemModelo> ItensModelo => Set<ItemModelo>();
    public DbSet<PacoteCatalogo> PacotesCatalogo => Set<PacoteCatalogo>();
    public DbSet<PacotePaciente> PacotesPaciente => Set<PacotePaciente>();
    public DbSet<ConsumoPacote> ConsumosPacote => Set<ConsumoPacote>();
    public DbSet<RegraRepasse> RegrasRepasse => Set<RegraRepasse>();
    public DbSet<TaxaCartao> TaxasCartao => Set<TaxaCartao>();
    public DbSet<Tributo> Tributos => Set<Tributo>();
    public DbSet<PrecoConvenio> PrecosConvenio => Set<PrecoConvenio>();
    public DbSet<RepasseApurado> RepassesApurados => Set<RepasseApurado>();
    public DbSet<ItemEstoque> ItensEstoque => Set<ItemEstoque>();
    public DbSet<MovimentoEstoque> MovimentosEstoque => Set<MovimentoEstoque>();
    public DbSet<DocumentoFinanceiro> DocumentosFinanceiros => Set<DocumentoFinanceiro>();
    public DbSet<ItemDocumentoFinanceiro> ItensDocumentoFinanceiro => Set<ItemDocumentoFinanceiro>();
    public DbSet<ContatoCampanha> Contatos => Set<ContatoCampanha>();
    public DbSet<UsuarioSistema> Usuarios => Set<UsuarioSistema>();
    public DbSet<BloqueioAgenda> BloqueiosAgenda => Set<BloqueioAgenda>();
    public DbSet<MetaMensal> Metas => Set<MetaMensal>();
    public DbSet<OrcamentoCategoria> Orcamentos => Set<OrcamentoCategoria>();

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
            e.Property(a => a.SerieId).HasMaxLength(40);
            e.HasIndex(a => a.SerieId);
            e.Property(a => a.ChegadaEm).HasColumnType("timestamp without time zone");
            e.Property(a => a.ChamadoEm).HasColumnType("timestamp without time zone");
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

        // Bloqueio de agenda: férias, feriado, folga. Profissional e sala anuláveis —
        // sem os dois, o bloqueio é da clínica inteira.
        b.Entity<BloqueioAgenda>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Motivo).IsRequired().HasMaxLength(200);
            e.Property(x => x.Inicio).HasColumnType("timestamp without time zone");
            e.Property(x => x.Fim).HasColumnType("timestamp without time zone");
            e.Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");
            e.Property(x => x.CriadoPor).HasMaxLength(80);

            e.HasOne(x => x.Profissional).WithMany()
                .HasForeignKey(x => x.ProfissionalId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Sala).WithMany()
                .HasForeignKey(x => x.SalaId).OnDelete(DeleteBehavior.Cascade);

            // A consulta é sempre "o que bloqueia este intervalo": o índice do início é o
            // que evita varrer o histórico inteiro a cada marcação.
            e.HasIndex(x => x.Inicio);
        });

        // Meta mensal: uma linha por mês, indicador e dono. O índice único é a regra —
        // duas metas para o mesmo mês e indicador dariam dois alvos para a mesma pergunta,
        // e a tela escolheria um deles sem dizer qual.
        b.Entity<MetaMensal>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Indicador).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Valor).HasPrecision(14, 2);
            e.Property(x => x.Observacoes).HasMaxLength(400);
            e.Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");
            e.Property(x => x.CriadoPor).HasMaxLength(80);

            e.HasOne(x => x.Profissional).WithMany()
                .HasForeignKey(x => x.ProfissionalId).OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.Ano, x.Mes, x.Indicador, x.ProfissionalId }).IsUnique();
        });

        // Teto de gasto por categoria e mês. Índice único pela mesma razão da meta: dois
        // tetos para a mesma pergunta dariam duas réguas.
        b.Entity<OrcamentoCategoria>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Teto).HasPrecision(14, 2);
            e.Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");
            e.Property(x => x.CriadoPor).HasMaxLength(80);

            e.HasOne(x => x.Categoria).WithMany()
                .HasForeignKey(x => x.CategoriaFinanceiraId).OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.Ano, x.Mes, x.CategoriaFinanceiraId }).IsUnique();
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

        // ---- Medidas clínicas seriadas (parcela 37) ----
        b.Entity<MedidaClinica>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TipoCodigo).IsRequired().HasMaxLength(30);
            e.Property(x => x.TipoNome).IsRequired().HasMaxLength(120);
            e.Property(x => x.Unidade).HasMaxLength(20);
            // Medida clínica é número exato de balança e fita: decimal, nunca ponto
            // flutuante — 78,3 kg que grava 78,29999 vira série que não bate com o papel.
            e.Property(x => x.Valor).HasPrecision(9, 2);
            e.Property(x => x.ValorSecundario).HasPrecision(9, 2);
            e.Property(x => x.FaixaNome).HasMaxLength(120);
            e.Property(x => x.FaixaInterpretacao).HasMaxLength(500);
            e.Property(x => x.FaixaGravidade).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Observacoes).HasMaxLength(1000);
            e.Property(x => x.CriadoPor).HasMaxLength(80);
            e.Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");

            e.HasOne(x => x.Paciente).WithMany().HasForeignKey(x => x.PacienteId);
            e.HasOne(x => x.Profissional).WithMany()
                .HasForeignKey(x => x.ProfissionalId).OnDelete(DeleteBehavior.SetNull);
            // Apagar a sessão NÃO apaga a medida, pelo mesmo motivo da avaliação: o peso
            // do dia é fato, e perdê-lo por causa da exclusão de um texto quebraria a
            // curva do tratamento inteiro.
            e.HasOne(x => x.Evolucao).WithMany()
                .HasForeignKey(x => x.EvolucaoId).OnDelete(DeleteBehavior.SetNull);

            // A leitura é sempre por paciente, e quase sempre de um tipo só (a curva).
            e.HasIndex(x => new { x.PacienteId, x.TipoCodigo, x.Data });

            e.Ignore(x => x.ValorFormatado);
            e.Ignore(x => x.TemFaixa);
        });

        // ---- Lista de problemas (parcela 37) ----
        b.Entity<ProblemaPaciente>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Descricao).IsRequired().HasMaxLength(300);
            e.Property(x => x.Cid).HasMaxLength(15);
            e.Property(x => x.Natureza).HasConversion<string>().HasMaxLength(25);
            e.Property(x => x.Situacao).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Observacoes).HasMaxLength(2000);
            e.Property(x => x.MotivoDescarte).HasMaxLength(500);
            e.Property(x => x.CriadoPor).HasMaxLength(80);
            e.Property(x => x.AtualizadoPor).HasMaxLength(80);
            e.Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");
            e.Property(x => x.AtualizadoEm).HasColumnType("timestamp without time zone");

            e.HasOne(x => x.Paciente).WithMany().HasForeignKey(x => x.PacienteId);
            e.HasOne(x => x.Profissional).WithMany()
                .HasForeignKey(x => x.ProfissionalId).OnDelete(DeleteBehavior.SetNull);
            // A EvolucaoId é procedência, não dono: a lista é do PACIENTE, e apagar a
            // sessão em que a alergia foi anotada não pode apagar a alergia.
            e.HasOne(x => x.Evolucao).WithMany()
                .HasForeignKey(x => x.EvolucaoId).OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(x => new { x.PacienteId, x.Situacao });

            e.Ignore(x => x.EstaAtivo);
            e.Ignore(x => x.EhAlertaDeAtendimento);
            e.Ignore(x => x.Rotulo);
        });

        // ---- Avaliações clínicas por instrumento (parcela 36) ----
        b.Entity<AvaliacaoClinica>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.InstrumentoCodigo).IsRequired().HasMaxLength(30);
            e.Property(x => x.InstrumentoNome).IsRequired().HasMaxLength(120);
            e.Property(x => x.EspecialidadeCodigo).HasMaxLength(40);
            e.Property(x => x.Unidade).HasMaxLength(10);
            e.Property(x => x.FaixaNome).HasMaxLength(120);
            e.Property(x => x.FaixaInterpretacao).HasMaxLength(500);
            e.Property(x => x.FaixaGravidade).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.AlertaItem).HasMaxLength(500);
            e.Property(x => x.Observacoes).HasMaxLength(2000);
            e.Property(x => x.CriadoPor).HasMaxLength(80);
            e.Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");

            e.HasOne(x => x.Paciente).WithMany().HasForeignKey(x => x.PacienteId);
            e.HasOne(x => x.Profissional).WithMany()
                .HasForeignKey(x => x.ProfissionalId).OnDelete(DeleteBehavior.SetNull);
            // Apagar a sessão NÃO apaga a avaliação: ela existe sem evolução (retorno só
            // para reaplicar a escala), e perder o escore por causa da exclusão de um
            // texto quebraria a curva do tratamento inteiro.
            e.HasOne(x => x.Evolucao).WithMany()
                .HasForeignKey(x => x.EvolucaoId).OnDelete(DeleteBehavior.SetNull);

            // A leitura é sempre por paciente, e quase sempre de um instrumento só (a
            // curva de evolução do escore).
            e.HasIndex(x => new { x.PacienteId, x.InstrumentoCodigo, x.Data });

            e.Ignore(x => x.PontuacaoFormatada);
            e.Ignore(x => x.TemAlertaDeItem);
        });

        b.Entity<RespostaAvaliacao>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ItemCodigo).IsRequired().HasMaxLength(30);
            e.Property(x => x.Enunciado).IsRequired().HasMaxLength(500);
            e.Property(x => x.OpcaoRotulo).HasMaxLength(300);

            // Resposta sem a avaliação que a contém não é registro de nada.
            e.HasOne(x => x.Avaliacao).WithMany(x => x.Respostas)
                .HasForeignKey(x => x.AvaliacaoClinicaId).OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.AvaliacaoClinicaId);
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

        // ---------- Ato clínico (parcela 3) ----------
        // Mapa corporal, protocolos reutilizáveis e os documentos impressos. Tudo em
        // tabelas NOVAS: o faturamento não vê nada disto.
        b.Entity<MapaCorporal>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Observacoes).HasMaxLength(1000);
            e.Property(x => x.CriadoPor).HasMaxLength(80);
            e.Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");
            e.Property(x => x.AtualizadoEm).HasColumnType("timestamp without time zone");

            // Um mapa por sessão, e apagar a sessão leva o mapa: mapa órfão não diz
            // de quem nem de quando.
            e.HasOne(x => x.Evolucao).WithOne()
                .HasForeignKey<MapaCorporal>(x => x.EvolucaoId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.EvolucaoId).IsUnique();
        });

        b.Entity<PontoMapa>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Face).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Tecnica).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Nome).HasMaxLength(40);
            e.Property(x => x.Observacao).HasMaxLength(200);

            e.HasOne(x => x.Mapa).WithMany(x => x.Pontos)
                .HasForeignKey(x => x.MapaCorporalId).OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.MapaCorporalId);
        });

        b.Entity<ProtocoloCorporal>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Nome).IsRequired().HasMaxLength(100);
            e.Property(x => x.Descricao).HasMaxLength(500);
            e.Property(x => x.CriadoPor).HasMaxLength(80);
            e.Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");
            e.Property(x => x.AtualizadoEm).HasColumnType("timestamp without time zone");

            // O protocolo DO PACIENTE morre com ele; o DA CLÍNICA não tem dono
            // (PacienteId nulo) e nenhuma exclusão de paciente o alcança.
            e.HasOne(x => x.Paciente).WithMany()
                .HasForeignKey(x => x.PacienteId).OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.PacienteId);
            e.Ignore(x => x.EhDaClinica);
        });

        b.Entity<PontoProtocolo>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Face).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Tecnica).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Nome).HasMaxLength(40);
            e.Property(x => x.Observacao).HasMaxLength(200);

            e.HasOne(x => x.Protocolo).WithMany(x => x.Pontos)
                .HasForeignKey(x => x.ProtocoloCorporalId).OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.ProtocoloCorporalId);
        });

        b.Entity<DocumentoClinico>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Numero).IsRequired().HasMaxLength(20);
            e.Property(x => x.CodigoVerificacao).IsRequired().HasMaxLength(20);
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Titulo).HasMaxLength(200);
            e.Property(x => x.Corpo).HasMaxLength(4000);
            e.Property(x => x.Observacoes).HasMaxLength(1000);
            e.Property(x => x.Cid).HasMaxLength(20);
            e.Property(x => x.CriadoPor).HasMaxLength(80);
            e.Property(x => x.MotivoCancelamento).HasMaxLength(500);
            e.Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");
            e.Property(x => x.CanceladoEm).HasColumnType("timestamp without time zone");

            e.HasOne(x => x.Paciente).WithMany().HasForeignKey(x => x.PacienteId);
            e.HasOne(x => x.Profissional).WithMany()
                .HasForeignKey(x => x.ProfissionalId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Evolucao).WithMany()
                .HasForeignKey(x => x.EvolucaoId).OnDelete(DeleteBehavior.SetNull);

            // Número e código são a identidade da via em papel — não podem repetir.
            e.HasIndex(x => x.Numero).IsUnique();
            e.HasIndex(x => x.CodigoVerificacao).IsUnique();
            e.HasIndex(x => new { x.PacienteId, x.Data });

            e.Ignore(x => x.Cancelado);
            e.Ignore(x => x.TituloImpresso);
            e.Ignore(x => x.CidImpresso);
        });

        b.Entity<ItemDocumento>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Descricao).IsRequired().HasMaxLength(300);
            e.Property(x => x.Detalhe).HasMaxLength(1000);
            e.Property(x => x.Quantidade).HasMaxLength(60);

            e.HasOne(x => x.Documento).WithMany(x => x.Itens)
                .HasForeignKey(x => x.DocumentoClinicoId).OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.DocumentoClinicoId);
        });

        b.Entity<ModeloDocumento>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Nome).IsRequired().HasMaxLength(100);
            e.Property(x => x.Titulo).HasMaxLength(200);
            e.Property(x => x.Corpo).HasMaxLength(4000);
            e.Property(x => x.CriadoPor).HasMaxLength(80);
            e.Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");
            e.Property(x => x.AtualizadoEm).HasColumnType("timestamp without time zone");

            // Salvar um modelo com nome já usado SOBRESCREVE o anterior em vez de
            // duplicar — é o que quem clica "salvar como modelo" espera.
            e.HasIndex(x => new { x.Tipo, x.Nome }).IsUnique();
        });

        b.Entity<ItemModelo>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Descricao).IsRequired().HasMaxLength(300);
            e.Property(x => x.Detalhe).HasMaxLength(1000);
            e.Property(x => x.Quantidade).HasMaxLength(60);

            e.HasOne(x => x.Modelo).WithMany(x => x.Itens)
                .HasForeignKey(x => x.ModeloDocumentoId).OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.ModeloDocumentoId);
        });

        // ---------- Campanhas e acesso (parcela 5) ----------
        // Ambas apontam para o que já existe (paciente, agendamento, profissional) e
        // nada aponta para elas: o faturamento continua sem saber que existem.
        b.Entity<ContatoCampanha>(e =>
        {
            e.ToTable("ContatosCampanha");
            e.HasKey(x => x.Id);
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Canal).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Origem).IsRequired().HasMaxLength(60);
            e.Property(x => x.EnviadoPor).HasMaxLength(80);
            e.Property(x => x.Comentario).HasMaxLength(1000);
            e.Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");
            e.Property(x => x.EnviadoEm).HasColumnType("timestamp without time zone");
            e.Property(x => x.RespondidoEm).HasColumnType("timestamp without time zone");

            e.HasOne(x => x.Paciente).WithMany().HasForeignKey(x => x.PacienteId);
            e.HasOne(x => x.Agendamento).WithMany()
                .HasForeignKey(x => x.AgendamentoId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Atendimento).WithMany()
                .HasForeignKey(x => x.AtendimentoId).OnDelete(DeleteBehavior.SetNull);

            // A idempotência da campanha é uma regra de BANCO, não só de código: duas
            // máquinas gerando a rodada ao mesmo tempo passariam pela checagem em
            // memória e mandariam a mensagem duas vezes para o mesmo paciente.
            e.HasIndex(x => new { x.Tipo, x.Origem }).IsUnique();
            e.HasIndex(x => new { x.Status, x.Referencia });
            e.HasIndex(x => x.PacienteId);

            e.Ignore(x => x.Classe);
            e.Ignore(x => x.Encerrado);
        });

        b.Entity<UsuarioSistema>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Nome).IsRequired().HasMaxLength(120);
            e.Property(x => x.Login).IsRequired().HasMaxLength(60);
            e.Property(x => x.SenhaHash).IsRequired().HasMaxLength(120);
            e.Property(x => x.SenhaSalt).IsRequired().HasMaxLength(60);
            e.Property(x => x.Perfil).HasConversion<string>().HasMaxLength(20);
            // Permissão vai como INTEIRO: a lista de bits cresce, e gravar por nome
            // ("VerAgenda, EditarAgenda") quebraria ao renomear qualquer um deles.
            e.Property(x => x.PermissoesExtras).HasConversion<int>();
            e.Property(x => x.PermissoesNegadas).HasConversion<int>();
            e.Property(x => x.Observacoes).HasMaxLength(500);
            e.Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");
            e.Property(x => x.UltimoAcessoEm).HasColumnType("timestamp without time zone");
            e.Property(x => x.BloqueadoAte).HasColumnType("timestamp without time zone");

            // Desligar o profissional não pode apagar o usuário (nem a auditoria dele).
            e.HasOne(x => x.Profissional).WithMany()
                .HasForeignKey(x => x.ProfissionalId).OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(x => x.Login).IsUnique();

            e.Ignore(x => x.Efetivas);
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

            // Taxa da maquininha e imposto (parcela 9). O liquido NAO e coluna: e
            // calculado, para nao haver duas verdades sobre o mesmo dinheiro.
            e.Property(x => x.Adquirente).HasMaxLength(60);
            e.Property(x => x.Bandeira).HasMaxLength(40);
            e.Property(x => x.ModalidadeCartao).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.TaxaPercentual).HasPrecision(6, 2);
            e.Property(x => x.ValorTaxa).HasPrecision(14, 2);
            e.Property(x => x.AliquotaImposto).HasPrecision(6, 2);
            e.Property(x => x.ValorImposto).HasPrecision(14, 2);
            // De QUE e o imposto, copiado na emissao (parcela 15). A aliquota somada
            // responde "quanto saiu" e nao "de que" — e "de que" e o que o contador pede.
            e.Property(x => x.DetalheImposto).HasMaxLength(300);
            e.Ignore(x => x.ValorLiquido);
            e.Ignore(x => x.TemDeducao);
            // Recebivel em aberto e o atraso sao CALCULADOS — o segundo depende de HOJE,
            // e gravado estaria mentindo amanha de manha.
            e.Ignore(x => x.RecebivelEmAberto);

            // Contas a pagar e a receber (parcela 12). "Vencido" tambem nao e coluna:
            // ele depende de HOJE, e uma coluna gravada estaria mentindo amanha de manha.
            e.Property(x => x.OrigemRecorrencia).HasMaxLength(60);

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
            // "O que vence esta semana" é a consulta mais frequente do módulo.
            e.HasIndex(x => x.DataVencimento);
            // "O que a maquininha ainda deve depositar" (parcela 16). O indice e sobre a
            // previsao porque a consulta filtra por ela; o confirmado entra so como
            // condicao, e nunca sozinho.
            e.HasIndex(x => x.PrevisaoRecebimento);
            // Idempotencia da geracao de contas recorrentes: o aluguel de agosto so pode
            // nascer uma vez, mesmo com dois postos abrindo o app na mesma manha. O
            // filtro deixa os avulsos (origem nula) de fora do unico.
            e.HasIndex(x => x.OrigemRecorrencia)
                .IsUnique()
                .HasFilter(null);
        });

        b.Entity<LancamentoRecorrente>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Descricao).IsRequired().HasMaxLength(200);
            e.Property(x => x.Valor).HasPrecision(14, 2);
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Periodicidade).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.FormaPagamento).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Observacoes).HasMaxLength(500);
            e.Property(x => x.CriadoPor).HasMaxLength(80);
            e.Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");

            e.HasOne(x => x.Categoria).WithMany()
                .HasForeignKey(x => x.CategoriaFinanceiraId).OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(x => x.Ativa);
            e.Ignore(x => x.PeriodicidadeTexto);
        });

        b.Entity<FechamentoCaixa>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ValorSistema).HasPrecision(14, 2);
            e.Property(x => x.ValorContado).HasPrecision(14, 2);
            e.Property(x => x.SaidasEspecie).HasPrecision(14, 2);
            e.Property(x => x.Justificativa).HasMaxLength(500);
            e.Property(x => x.Observacoes).HasMaxLength(500);
            e.Property(x => x.MotivoReabertura).HasMaxLength(500);
            e.Property(x => x.ConferidoPor).HasMaxLength(80);
            e.Property(x => x.ConferidoEm).HasColumnType("timestamp without time zone");

            // Data NAO e unica: reabrir para recontagem guarda o fechamento anterior e
            // grava outro por cima. O que vale e o de maior Id.
            e.HasIndex(x => x.Data);

            // Esperado, Diferenca, Bateu e Situacao sao CALCULADOS. Gravar a diferenca
            // daria duas verdades sobre a mesma contagem no dia em que divergissem.
            e.Ignore(x => x.Esperado);
            e.Ignore(x => x.Diferenca);
            e.Ignore(x => x.Bateu);
            e.Ignore(x => x.Situacao);
        });

        // ---------- Dinheiro e insumo (parcela 4) ----------
        // Pacotes, repasse, estoque e os dois documentos financeiros. Como no resto do
        // financeiro, as FKs apontam do módulo PARA o faturamento, nunca o contrário.
        b.Entity<PacoteCatalogo>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Nome).IsRequired().HasMaxLength(120);
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Valor).HasPrecision(14, 2);
            e.Property(x => x.Observacoes).HasMaxLength(500);
            e.Property(x => x.CriadoPor).HasMaxLength(80);
            e.Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");
        });

        b.Entity<PacotePaciente>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Nome).IsRequired().HasMaxLength(120);
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Valor).HasPrecision(14, 2);
            e.Property(x => x.Observacoes).HasMaxLength(500);
            e.Property(x => x.CriadoPor).HasMaxLength(80);
            e.Property(x => x.MotivoCancelamento).HasMaxLength(500);
            e.Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");
            e.Property(x => x.CanceladoEm).HasColumnType("timestamp without time zone");

            e.HasOne(x => x.Paciente).WithMany().HasForeignKey(x => x.PacienteId);
            // O catálogo é só procedência: apagar um pacote da tabela de preços não pode
            // apagar as vendas que ele originou.
            e.HasOne(x => x.Catalogo).WithMany()
                .HasForeignKey(x => x.PacoteCatalogoId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Lancamento).WithMany()
                .HasForeignKey(x => x.LancamentoFinanceiroId).OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(x => x.PacienteId);

            e.Ignore(x => x.Cancelado);
            e.Ignore(x => x.SessoesUsadas);
            e.Ignore(x => x.SaldoSessoes);
            e.Ignore(x => x.Esgotado);
        });

        b.Entity<ConsumoPacote>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Observacao).HasMaxLength(300);
            e.Property(x => x.CriadoPor).HasMaxLength(80);
            e.Property(x => x.MotivoCancelamento).HasMaxLength(500);
            e.Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");
            e.Property(x => x.CanceladoEm).HasColumnType("timestamp without time zone");

            e.HasOne(x => x.Pacote).WithMany(x => x.Consumos)
                .HasForeignKey(x => x.PacotePacienteId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Atendimento).WithMany()
                .HasForeignKey(x => x.AtendimentoId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Agendamento).WithMany()
                .HasForeignKey(x => x.AgendamentoId).OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(x => x.PacotePacienteId);
            // A baixa automática pergunta "este atendimento já debitou?" a cada conclusão.
            e.HasIndex(x => x.AtendimentoId);

            e.Ignore(x => x.Cancelado);
        });

        b.Entity<RegraRepasse>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Base).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Percentual).HasPrecision(6, 2);
            e.Property(x => x.ValorPorAtendimento).HasPrecision(14, 2);
            e.Property(x => x.ModalidadeCodigo).HasMaxLength(40);
            e.Property(x => x.ConvenioCodigo).HasMaxLength(40);
            e.Property(x => x.Observacoes).HasMaxLength(500);
            e.Property(x => x.CriadoPor).HasMaxLength(80);
            e.Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");

            e.HasOne(x => x.Profissional).WithMany().HasForeignKey(x => x.ProfissionalId);

            e.HasIndex(x => x.ProfissionalId);
            e.Ignore(x => x.Descricao);
        });

        b.Entity<TaxaCartao>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Adquirente).IsRequired().HasMaxLength(60);
            e.Property(x => x.Bandeira).HasMaxLength(40);
            e.Property(x => x.Modalidade).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Percentual).HasPrecision(6, 2);
            e.Property(x => x.Observacoes).HasMaxLength(500);
            e.Property(x => x.CriadoPor).HasMaxLength(80);
            e.Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");

            e.HasIndex(x => x.Ativa);
            e.Ignore(x => x.Descricao);
        });

        b.Entity<Tributo>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Sigla).IsRequired().HasMaxLength(20);
            e.Property(x => x.Nome).IsRequired().HasMaxLength(100);
            // Quatro casas na aliquota: no Presumido a efetiva do IRPJ e 4,8% e a da CSLL
            // 2,88% — duas casas arredondariam o segundo para 2,88 e o terceiro para cima.
            e.Property(x => x.Percentual).HasPrecision(7, 4);
            e.Property(x => x.BasePercentual).HasPrecision(7, 4);
            e.Property(x => x.Observacoes).HasMaxLength(500);
            e.Property(x => x.CriadoPor).HasMaxLength(80);
            e.Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");

            // Retencao na fonte por convenio (parcela 18): a operadora retem antes de
            // depositar, e o retido SUBSTITUI o tributo geral naquele recebimento — somar
            // os dois contaria o mesmo imposto duas vezes.
            e.Property(x => x.ConvenioCodigo).HasMaxLength(40);

            e.HasIndex(x => x.Ativo);
            e.HasIndex(x => x.ConvenioCodigo);

            // Efetiva, descricao, vigencia e "retido" sao CALCULADAS — gravar a efetiva daria duas
            // verdades sobre a mesma aliquota no dia em que a base mudasse.
            e.Ignore(x => x.AliquotaEfetiva);
            e.Ignore(x => x.Descricao);
            e.Ignore(x => x.Vigencia);
            e.Ignore(x => x.RetidoNaFonte);
        });

        b.Entity<PrecoConvenio>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ConvenioCodigo).IsRequired().HasMaxLength(40);
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.Especialidade).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Valor).HasPrecision(14, 2);
            e.Property(x => x.Observacoes).HasMaxLength(500);
            e.Property(x => x.CriadoPor).HasMaxLength(80);
            e.Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");

            // A consulta da conciliacao filtra por convenio + tipo.
            e.HasIndex(x => new { x.ConvenioCodigo, x.Tipo });
            e.HasIndex(x => x.Ativo);

            // Descricao e vigencia sao CALCULADAS.
            e.Ignore(x => x.Descricao);
            e.Ignore(x => x.Vigencia);
        });

        b.Entity<RepasseApurado>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.BaseCalculo).HasPrecision(14, 2);
            e.Property(x => x.Valor).HasPrecision(14, 2);
            e.Property(x => x.RegraDescricao).HasMaxLength(200);
            e.Property(x => x.CriadoPor).HasMaxLength(80);
            e.Property(x => x.MotivoCancelamento).HasMaxLength(500);
            e.Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");
            e.Property(x => x.CanceladoEm).HasColumnType("timestamp without time zone");

            e.HasOne(x => x.Profissional).WithMany().HasForeignKey(x => x.ProfissionalId);
            e.HasOne(x => x.Lancamento).WithMany()
                .HasForeignKey(x => x.LancamentoFinanceiroId).OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(x => new { x.ProfissionalId, x.Inicio });
            e.Ignore(x => x.Cancelado);
        });

        b.Entity<ItemEstoque>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Nome).IsRequired().HasMaxLength(120);
            e.Property(x => x.Unidade).IsRequired().HasMaxLength(10);
            e.Property(x => x.EstoqueMinimo).HasPrecision(14, 3);
            e.Property(x => x.Observacoes).HasMaxLength(500);
            e.Property(x => x.CriadoPor).HasMaxLength(80);
            e.Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");
        });

        b.Entity<MovimentoEstoque>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            // Quantidade fracionada existe (ml, g), então não é inteiro.
            e.Property(x => x.Quantidade).HasPrecision(14, 3);
            e.Property(x => x.CustoUnitario).HasPrecision(14, 4);
            e.Property(x => x.Lote).HasMaxLength(60);
            e.Property(x => x.Observacao).HasMaxLength(300);
            e.Property(x => x.CriadoPor).HasMaxLength(80);
            e.Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");

            e.HasOne(x => x.Item).WithMany(x => x.Movimentos)
                .HasForeignKey(x => x.ItemEstoqueId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Atendimento).WithMany()
                .HasForeignKey(x => x.AtendimentoId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Paciente).WithMany()
                .HasForeignKey(x => x.PacienteId).OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(x => new { x.ItemEstoqueId, x.Data });
            e.HasIndex(x => x.AtendimentoId);

            e.Ignore(x => x.Sinal);
            e.Ignore(x => x.QuantidadeComSinal);
        });

        b.Entity<DocumentoFinanceiro>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Numero).IsRequired().HasMaxLength(20);
            e.Property(x => x.CodigoVerificacao).IsRequired().HasMaxLength(20);
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Destinatario).IsRequired().HasMaxLength(200);
            e.Property(x => x.DocumentoDestinatario).HasMaxLength(30);
            e.Property(x => x.Titulo).HasMaxLength(200);
            e.Property(x => x.Corpo).HasMaxLength(4000);
            e.Property(x => x.Observacoes).HasMaxLength(1000);
            e.Property(x => x.FormaPagamento).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CriadoPor).HasMaxLength(80);
            e.Property(x => x.MotivoCancelamento).HasMaxLength(500);
            e.Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");
            e.Property(x => x.CanceladoEm).HasColumnType("timestamp without time zone");

            e.HasOne(x => x.Paciente).WithMany()
                .HasForeignKey(x => x.PacienteId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Lancamento).WithMany()
                .HasForeignKey(x => x.LancamentoFinanceiroId).OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(x => x.Numero).IsUnique();
            e.HasIndex(x => x.CodigoVerificacao).IsUnique();
            e.HasIndex(x => x.Data);

            e.Ignore(x => x.Cancelado);
            e.Ignore(x => x.ValorTotal);
            e.Ignore(x => x.TituloImpresso);
        });

        b.Entity<ItemDocumentoFinanceiro>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Descricao).IsRequired().HasMaxLength(300);
            e.Property(x => x.Quantidade).HasPrecision(14, 3);
            e.Property(x => x.ValorUnitario).HasPrecision(14, 2);

            e.HasOne(x => x.Documento).WithMany(x => x.Itens)
                .HasForeignKey(x => x.DocumentoFinanceiroId).OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.DocumentoFinanceiroId);
            e.Ignore(x => x.ValorTotal);
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
            // O mapa corporal se regrava por INTEIRO (os pontos são substituídos), então
            // uma gravação por cima da outra não perderia um campo: perderia a sessão.
            b.Entity<MapaCorporal>().Property<uint>("xmin").IsRowVersion();
        }
    }
}
