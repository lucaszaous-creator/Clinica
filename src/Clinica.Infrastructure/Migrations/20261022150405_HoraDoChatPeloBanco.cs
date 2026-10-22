using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// A hora do chat vem do BANCO, como no WhatsApp (out/2026). O <c>EnviadaEm</c> de cada mensagem
    /// e o <c>UltimaEm</c> da lista de conversas eram o <c>DateTime.Now</c> do computador de quem
    /// enviou: com um computador do balcão adiantado, a mensagem mostrava 10:36 acima de uma de 10:34.
    /// Um gatilho carimba a hora do servidor (horário de Brasília) na gravação — sem ida a mais ao
    /// banco (o orçamento de `ChatEquipeIdasAoBancoTests` continua o mesmo) e para todas as portas:
    /// o chat dos apps, a resposta do Sistema SemDor, o pedido à recepção e a ponte do WhatsApp.
    ///
    /// Só no Postgres, e só onde o papel pode criar gatilho nas tabelas (a regra das tabelas do
    /// posto, CLAUDE.md): sem o gatilho, a hora continua sendo a de quem enviou, como antes.
    /// <c>clock_timestamp()</c> e não <c>now()</c>: <c>now()</c> é a hora do começo da transação.
    /// </summary>
    public partial class HoraDoChatPeloBanco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (!ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)) return;
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION public.clinica_chat_hora_do_banco() RETURNS trigger
                LANGUAGE plpgsql AS $hora$
                BEGIN
                    IF TG_TABLE_NAME = 'MensagensEquipe' THEN
                        NEW."EnviadaEm" := clock_timestamp() AT TIME ZONE 'America/Sao_Paulo';
                    ELSIF TG_OP = 'INSERT' OR NEW."UltimaMensagemId" IS DISTINCT FROM OLD."UltimaMensagemId" THEN
                        -- A lista de conversas: a hora da última mensagem, quando ela troca.
                        NEW."UltimaEm" := clock_timestamp() AT TIME ZONE 'America/Sao_Paulo';
                    END IF;
                    RETURN NEW;
                END
                $hora$;

                DO $chat$
                BEGIN
                    IF has_table_privilege(current_user, 'public."MensagensEquipe"', 'TRIGGER')
                       AND NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = 'clinica_chat_hora_mensagem') THEN
                        CREATE TRIGGER clinica_chat_hora_mensagem BEFORE INSERT ON public."MensagensEquipe"
                            FOR EACH ROW EXECUTE FUNCTION public.clinica_chat_hora_do_banco();
                    END IF;
                    IF has_table_privilege(current_user, 'public."ConversasEquipe"', 'TRIGGER')
                       AND NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = 'clinica_chat_hora_conversa') THEN
                        CREATE TRIGGER clinica_chat_hora_conversa BEFORE INSERT OR UPDATE ON public."ConversasEquipe"
                            FOR EACH ROW EXECUTE FUNCTION public.clinica_chat_hora_do_banco();
                    END IF;
                END
                $chat$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (!ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)) return;
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS clinica_chat_hora_mensagem ON public."MensagensEquipe";
                DROP TRIGGER IF EXISTS clinica_chat_hora_conversa ON public."ConversasEquipe";
                DROP FUNCTION IF EXISTS public.clinica_chat_hora_do_banco();
                """);
        }
    }
}
