using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Aviso instantâneo de mudança (out/2026): o banco avisa os postos abertos quando a agenda
    /// ou um pedido da enfermagem muda, em vez de cada posto descobrir relendo por relógio.
    /// </summary>
    /// <remarks>
    /// Só ACRESCENTA (uma função e três gatilhos); nenhuma coluna, nenhuma regra, nenhum dado.
    /// O gatilho é por COMANDO (FOR EACH STATEMENT), não por linha: empurrar trinta sessões
    /// gera um aviso, não trinta. O aviso sai só no COMMIT (é assim que o <c>pg_notify</c>
    /// funciona), avisos iguais na mesma transação são fundidos pelo próprio Postgres, e ele
    /// leva só o NOME da tabela — nenhum dado de paciente atravessa o canal.
    ///
    /// <c>pg_notify</c> não exige privilégio em tabela nenhuma: o papel restrito do portal
    /// continua gravando sem precisar de permissão nova.
    /// </remarks>
    public partial class AvisosInstantaneos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (!ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)) return;

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION clinica_avisar_mudanca() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    PERFORM pg_notify('clinica_mudancas', TG_TABLE_NAME);
                    RETURN NULL;
                END;
                $$;

                CREATE TRIGGER clinica_avisar_agendamentos
                    AFTER INSERT OR UPDATE OR DELETE ON "Agendamentos"
                    FOR EACH STATEMENT EXECUTE FUNCTION clinica_avisar_mudanca();
                CREATE TRIGGER clinica_avisar_solicitacoes_enfermagem
                    AFTER INSERT OR UPDATE OR DELETE ON "SolicitacoesEnfermagem"
                    FOR EACH STATEMENT EXECUTE FUNCTION clinica_avisar_mudanca();
                CREATE TRIGGER clinica_avisar_eventos_solicitacao_enfermagem
                    AFTER INSERT OR UPDATE OR DELETE ON "EventosSolicitacaoEnfermagem"
                    FOR EACH STATEMENT EXECUTE FUNCTION clinica_avisar_mudanca();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (!ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)) return;

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS clinica_avisar_agendamentos ON "Agendamentos";
                DROP TRIGGER IF EXISTS clinica_avisar_solicitacoes_enfermagem ON "SolicitacoesEnfermagem";
                DROP TRIGGER IF EXISTS clinica_avisar_eventos_solicitacao_enfermagem ON "EventosSolicitacaoEnfermagem";
                DROP FUNCTION IF EXISTS clinica_avisar_mudanca();
                """);
        }
    }
}
