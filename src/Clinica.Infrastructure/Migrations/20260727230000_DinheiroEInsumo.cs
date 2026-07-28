using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Dinheiro e insumo (parcela 4): pacotes/planos/vouchers com saldo, repasse por
    /// profissional, estoque com validade e custo, e os dois documentos financeiros que
    /// faltavam da página 21 (recibo e orçamento).
    ///
    /// PURAMENTE ADITIVA: só tabelas novas. Nenhuma coluna existente é renomeada,
    /// alterada ou removida — o faturamento em produção não vê diferença.
    /// </summary>
    [DbContext(typeof(ClinicaDbContext))]
    [Migration("20260727230000_DinheiroEInsumo")]
    public partial class DinheiroEInsumo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------- Pacotes, planos e vouchers ----------
            migrationBuilder.CreateTable(
                name: "PacotesCatalogo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SessoesIncluidas = table.Column<int>(type: "integer", nullable: true),
                    Valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    ValidadeDias = table.Column<int>(type: "integer", nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_PacotesCatalogo", x => x.Id));

            migrationBuilder.CreateTable(
                name: "PacotesPaciente",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PacienteId = table.Column<int>(type: "integer", nullable: false),
                    PacoteCatalogoId = table.Column<int>(type: "integer", nullable: true),
                    Nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SessoesContratadas = table.Column<int>(type: "integer", nullable: true),
                    Valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    DataCompra = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidoAte = table.Column<DateOnly>(type: "date", nullable: true),
                    LancamentoFinanceiroId = table.Column<int>(type: "integer", nullable: true),
                    Observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CanceladoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PacotesPaciente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PacotesPaciente_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    // O catálogo é só procedência: apagar o pacote da tabela de preços
                    // não pode apagar as vendas que ele originou.
                    table.ForeignKey(
                        name: "FK_PacotesPaciente_PacotesCatalogo_PacoteCatalogoId",
                        column: x => x.PacoteCatalogoId,
                        principalTable: "PacotesCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PacotesPaciente_Lancamentos_LancamentoFinanceiroId",
                        column: x => x.LancamentoFinanceiroId,
                        principalTable: "Lancamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ConsumosPacote",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PacotePacienteId = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    AtendimentoId = table.Column<int>(type: "integer", nullable: true),
                    AgendamentoId = table.Column<int>(type: "integer", nullable: true),
                    Observacao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CanceladoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumosPacote", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsumosPacote_PacotesPaciente_PacotePacienteId",
                        column: x => x.PacotePacienteId,
                        principalTable: "PacotesPaciente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConsumosPacote_Atendimentos_AtendimentoId",
                        column: x => x.AtendimentoId,
                        principalTable: "Atendimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ConsumosPacote_Agendamentos_AgendamentoId",
                        column: x => x.AgendamentoId,
                        principalTable: "Agendamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            // ---------- Repasse por profissional ----------
            migrationBuilder.CreateTable(
                name: "RegrasRepasse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfissionalId = table.Column<int>(type: "integer", nullable: false),
                    Base = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Percentual = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    ValorPorAtendimento = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    ModalidadeCodigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ConvenioCodigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    VigenteDe = table.Column<DateOnly>(type: "date", nullable: true),
                    VigenteAte = table.Column<DateOnly>(type: "date", nullable: true),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegrasRepasse", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegrasRepasse_Profissionais_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalTable: "Profissionais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RepassesApurados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfissionalId = table.Column<int>(type: "integer", nullable: false),
                    Inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    Fim = table.Column<DateOnly>(type: "date", nullable: false),
                    Atendimentos = table.Column<int>(type: "integer", nullable: false),
                    BaseCalculo = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    RegraDescricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LancamentoFinanceiroId = table.Column<int>(type: "integer", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CanceladoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepassesApurados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepassesApurados_Profissionais_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalTable: "Profissionais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RepassesApurados_Lancamentos_LancamentoFinanceiroId",
                        column: x => x.LancamentoFinanceiroId,
                        principalTable: "Lancamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            // ---------- Estoque ----------
            migrationBuilder.CreateTable(
                name: "ItensEstoque",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Unidade = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    EstoqueMinimo = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_ItensEstoque", x => x.Id));

            migrationBuilder.CreateTable(
                name: "MovimentosEstoque",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemEstoqueId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Quantidade = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    CustoUnitario = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    Validade = table.Column<DateOnly>(type: "date", nullable: true),
                    Lote = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    AtendimentoId = table.Column<int>(type: "integer", nullable: true),
                    PacienteId = table.Column<int>(type: "integer", nullable: true),
                    Observacao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimentosEstoque", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimentosEstoque_ItensEstoque_ItemEstoqueId",
                        column: x => x.ItemEstoqueId,
                        principalTable: "ItensEstoque",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MovimentosEstoque_Atendimentos_AtendimentoId",
                        column: x => x.AtendimentoId,
                        principalTable: "Atendimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MovimentosEstoque_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            // ---------- Recibo e orçamento ----------
            migrationBuilder.CreateTable(
                name: "DocumentosFinanceiros",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CodigoVerificacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PacienteId = table.Column<int>(type: "integer", nullable: true),
                    Destinatario = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DocumentoDestinatario = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidoAte = table.Column<DateOnly>(type: "date", nullable: true),
                    Titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Corpo = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FormaPagamento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LancamentoFinanceiroId = table.Column<int>(type: "integer", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CanceladoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentosFinanceiros", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentosFinanceiros_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DocumentosFinanceiros_Lancamentos_LancamentoFinanceiroId",
                        column: x => x.LancamentoFinanceiroId,
                        principalTable: "Lancamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ItensDocumentoFinanceiro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentoFinanceiroId = table.Column<int>(type: "integer", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    Descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Quantidade = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    ValorUnitario = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensDocumentoFinanceiro", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensDocumentoFinanceiro_DocumentosFinanceiros_DocumentoFin~",
                        column: x => x.DocumentoFinanceiroId,
                        principalTable: "DocumentosFinanceiros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ---------- Índices ----------
            migrationBuilder.CreateIndex("IX_PacotesPaciente_PacienteId", "PacotesPaciente", "PacienteId");
            migrationBuilder.CreateIndex(
                "IX_PacotesPaciente_PacoteCatalogoId", "PacotesPaciente", "PacoteCatalogoId");
            migrationBuilder.CreateIndex(
                "IX_PacotesPaciente_LancamentoFinanceiroId", "PacotesPaciente", "LancamentoFinanceiroId");

            migrationBuilder.CreateIndex(
                "IX_ConsumosPacote_PacotePacienteId", "ConsumosPacote", "PacotePacienteId");
            // A baixa automática pergunta "este atendimento já debitou?" a cada conclusão.
            migrationBuilder.CreateIndex("IX_ConsumosPacote_AtendimentoId", "ConsumosPacote", "AtendimentoId");
            migrationBuilder.CreateIndex("IX_ConsumosPacote_AgendamentoId", "ConsumosPacote", "AgendamentoId");

            migrationBuilder.CreateIndex("IX_RegrasRepasse_ProfissionalId", "RegrasRepasse", "ProfissionalId");
            migrationBuilder.CreateIndex(
                "IX_RepassesApurados_ProfissionalId_Inicio", "RepassesApurados",
                new[] { "ProfissionalId", "Inicio" });
            migrationBuilder.CreateIndex(
                "IX_RepassesApurados_LancamentoFinanceiroId", "RepassesApurados", "LancamentoFinanceiroId");

            migrationBuilder.CreateIndex(
                "IX_MovimentosEstoque_ItemEstoqueId_Data", "MovimentosEstoque",
                new[] { "ItemEstoqueId", "Data" });
            migrationBuilder.CreateIndex(
                "IX_MovimentosEstoque_AtendimentoId", "MovimentosEstoque", "AtendimentoId");
            migrationBuilder.CreateIndex(
                "IX_MovimentosEstoque_PacienteId", "MovimentosEstoque", "PacienteId");

            migrationBuilder.CreateIndex(
                "IX_DocumentosFinanceiros_Numero", "DocumentosFinanceiros", "Numero", unique: true);
            migrationBuilder.CreateIndex(
                "IX_DocumentosFinanceiros_CodigoVerificacao", "DocumentosFinanceiros",
                "CodigoVerificacao", unique: true);
            migrationBuilder.CreateIndex("IX_DocumentosFinanceiros_Data", "DocumentosFinanceiros", "Data");
            migrationBuilder.CreateIndex(
                "IX_DocumentosFinanceiros_PacienteId", "DocumentosFinanceiros", "PacienteId");
            migrationBuilder.CreateIndex(
                "IX_DocumentosFinanceiros_LancamentoFinanceiroId", "DocumentosFinanceiros",
                "LancamentoFinanceiroId");

            migrationBuilder.CreateIndex(
                "IX_ItensDocumentoFinanceiro_DocumentoFinanceiroId", "ItensDocumentoFinanceiro",
                "DocumentoFinanceiroId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("ConsumosPacote");
            migrationBuilder.DropTable("PacotesPaciente");
            migrationBuilder.DropTable("PacotesCatalogo");
            migrationBuilder.DropTable("RepassesApurados");
            migrationBuilder.DropTable("RegrasRepasse");
            migrationBuilder.DropTable("MovimentosEstoque");
            migrationBuilder.DropTable("ItensEstoque");
            migrationBuilder.DropTable("ItensDocumentoFinanceiro");
            migrationBuilder.DropTable("DocumentosFinanceiros");
        }
    }
}
