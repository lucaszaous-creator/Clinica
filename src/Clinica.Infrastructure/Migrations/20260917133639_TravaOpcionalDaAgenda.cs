using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TravaOpcionalDaAgenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AgendaProtegida",
                table: "Profissionais",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // A trava também alcança clientes antigos e gravações concorrentes. Alterações
            // clínicas no horário original continuam permitidas, mesmo após mudar a jornada.
            migrationBuilder.Sql("""
                CREATE FUNCTION clinica_validar_agenda_protegida() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    profissional record;
                    fim timestamp without time zone;
                BEGIN
                    IF NEW."ProfissionalId" IS NULL OR NEW."Status" NOT IN ('Agendado', 'Realizado') THEN
                        RETURN NEW;
                    END IF;
                    IF TG_OP = 'UPDATE' THEN
                        IF OLD."Status" IN ('Agendado', 'Realizado')
                           AND NEW."ProfissionalId" IS NOT DISTINCT FROM OLD."ProfissionalId"
                           AND NEW."DataHora" IS NOT DISTINCT FROM OLD."DataHora"
                           AND NEW."DuracaoMinutos" IS NOT DISTINCT FROM OLD."DuracaoMinutos"
                           AND NEW."SalaId" IS NOT DISTINCT FROM OLD."SalaId" THEN
                            RETURN NEW;
                        END IF;
                    END IF;

                    -- Serializa candidatos ao mesmo profissional até o fim da transação.
                    -- O SELECT posterior ao lock usa o estado já confirmado pelo outro posto.
                    PERFORM pg_advisory_xact_lock(17206, NEW."ProfissionalId");
                    SELECT p.* INTO profissional FROM "Profissionais" p
                        WHERE p."Id" = NEW."ProfissionalId";
                    IF NOT FOUND OR NOT profissional."AgendaProtegida" THEN RETURN NEW; END IF;
                    fim := NEW."DataHora" + make_interval(mins => COALESCE(
                        NEW."DuracaoMinutos", profissional."DuracaoPadraoMinutos", 30));

                    IF (profissional."DiasDeAtendimento" IS NOT NULL AND
                        (profissional."DiasDeAtendimento" & (1 << EXTRACT(DOW FROM NEW."DataHora")::int)) = 0)
                       OR (profissional."AtendeDas" IS NOT NULL AND profissional."AtendeAte" IS NOT NULL AND
                           (fim::date <> NEW."DataHora"::date OR NEW."DataHora"::time < profissional."AtendeDas"
                            OR fim::time > profissional."AtendeAte")) THEN
                        RAISE EXCEPTION 'Agenda protegida: este horário está fora da jornada do profissional.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_Agenda_Protegida';
                    END IF;
                    IF EXISTS (SELECT 1 FROM "Agendamentos" a
                        WHERE a."ProfissionalId" = NEW."ProfissionalId" AND a."Id" <> NEW."Id"
                          AND a."Status" IN ('Agendado', 'Realizado') AND a."DataHora" < fim
                          AND a."DataHora" + make_interval(mins => COALESCE(
                              a."DuracaoMinutos", profissional."DuracaoPadraoMinutos", 30)) > NEW."DataHora") THEN
                        RAISE EXCEPTION 'Agenda protegida: o profissional já tem atendimento neste intervalo. Atualize a agenda e escolha outro horário.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_Agenda_Protegida';
                    END IF;
                    IF EXISTS (SELECT 1 FROM "BloqueiosAgenda" b
                        WHERE b."Inicio" < fim AND b."Fim" > NEW."DataHora"
                          AND ((b."ProfissionalId" IS NULL AND b."SalaId" IS NULL)
                               OR b."ProfissionalId" = NEW."ProfissionalId" OR b."SalaId" = NEW."SalaId")) THEN
                        RAISE EXCEPTION 'Agenda protegida: existe um bloqueio neste intervalo. Escolha outro horário.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_Agenda_Protegida';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                -- A edição da jornada participa da mesma fila de reservas. O portal
                -- só precisa ler Profissionais: FOR SHARE exigiria UPDATE nessa tabela.
                CREATE FUNCTION clinica_serializar_jornada() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    PERFORM pg_advisory_xact_lock(17206, NEW."Id");
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER "TR_Jornada_Profissional"
                    BEFORE UPDATE OF "AgendaProtegida", "DiasDeAtendimento", "AtendeDas", "AtendeAte", "DuracaoPadraoMinutos"
                    ON "Profissionais" FOR EACH ROW EXECUTE FUNCTION clinica_serializar_jornada();
                CREATE TRIGGER "TR_Agenda_Protegida" BEFORE INSERT OR UPDATE ON "Agendamentos"
                    FOR EACH ROW EXECUTE FUNCTION clinica_validar_agenda_protegida();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER "TR_Jornada_Profissional" ON "Profissionais";
                DROP FUNCTION clinica_serializar_jornada();
                DROP TRIGGER "TR_Agenda_Protegida" ON "Agendamentos";
                DROP FUNCTION clinica_validar_agenda_protegida();
                """);
            migrationBuilder.DropColumn(
                name: "AgendaProtegida",
                table: "Profissionais");
        }
    }
}
