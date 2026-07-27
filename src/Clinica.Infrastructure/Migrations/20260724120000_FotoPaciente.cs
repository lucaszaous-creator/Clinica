using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Retrato do paciente (capturado pela webcam da recepção). A miniatura fica na
    /// linha do paciente, para os avatares da lista; a foto cheia vai para a tabela
    /// PacientesFotos, carregada só quando a ficha precisa dela.
    /// </summary>
    [DbContext(typeof(ClinicaDbContext))]
    [Migration("20260724120000_FotoPaciente")]
    public partial class FotoPaciente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>("FotoMiniatura", "Pacientes", "bytea", nullable: true);
            migrationBuilder.AddColumn<DateTime>("FotoAtualizadaEm", "Pacientes", "timestamp without time zone", nullable: true);

            migrationBuilder.CreateTable(
                name: "PacientesFotos",
                columns: table => new
                {
                    PacienteId = table.Column<int>(type: "integer", nullable: false),
                    Conteudo = table.Column<byte[]>(type: "bytea", nullable: false),
                    AtualizadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PacientesFotos", x => x.PacienteId);
                    table.ForeignKey(
                        name: "FK_PacientesFotos_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("PacientesFotos");
            migrationBuilder.DropColumn("FotoAtualizadaEm", "Pacientes");
            migrationBuilder.DropColumn("FotoMiniatura", "Pacientes");
        }
    }
}
