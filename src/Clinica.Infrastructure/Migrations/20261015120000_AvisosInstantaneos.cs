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

            // ⚠️ Cada gatilho só nasce onde o papel que roda a migration TEM o privilégio
            // TRIGGER. Na clínica, as tabelas dos pedidos da enfermagem são criadas pelo
            // atualizador do posto (deploy/tablet) como postgres, e o papel do desktop recebe só
            // SELECT/INSERT/UPDATE nelas: um CREATE TRIGGER incondicional fazia a migration
            // falhar com 42501 e o app NÃO ABRIA. Sem o gatilho a tela segue pelo relógio, como
            // sempre; o script deploy/tablet/avisos-instantaneos-enfermagem.sql cria os dois que
            // faltam, rodado como dono das tabelas.
            migrationBuilder.Sql("""
                DO $avisos$
                DECLARE gatilho text; tabela text;
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'clinica_avisar_mudanca'
                                   AND pronamespace = 'public'::regnamespace) THEN
                        IF NOT has_schema_privilege(current_user, 'public', 'CREATE') THEN
                            RETURN;
                        END IF;
                        EXECUTE $f$
                            CREATE FUNCTION public.clinica_avisar_mudanca() RETURNS trigger
                            LANGUAGE plpgsql AS $corpo$
                            BEGIN
                                PERFORM pg_notify('clinica_mudancas', TG_TABLE_NAME);
                                RETURN NULL;
                            END;
                            $corpo$
                        $f$;
                    END IF;

                    FOR gatilho, tabela IN
                        VALUES ('clinica_avisar_agendamentos', 'Agendamentos'),
                               ('clinica_avisar_solicitacoes_enfermagem', 'SolicitacoesEnfermagem'),
                               ('clinica_avisar_eventos_solicitacao_enfermagem', 'EventosSolicitacaoEnfermagem')
                    LOOP
                        CONTINUE WHEN to_regclass(format('public.%I', tabela)) IS NULL;
                        CONTINUE WHEN EXISTS (SELECT 1 FROM pg_trigger
                                              WHERE tgname = gatilho
                                                AND tgrelid = format('public.%I', tabela)::regclass);
                        CONTINUE WHEN NOT has_table_privilege(current_user, format('public.%I', tabela), 'TRIGGER');
                        EXECUTE format('CREATE TRIGGER %I AFTER INSERT OR UPDATE OR DELETE ON public.%I '
                                       'FOR EACH STATEMENT EXECUTE FUNCTION public.clinica_avisar_mudanca()',
                                       gatilho, tabela);
                    END LOOP;
                END
                $avisos$;
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
