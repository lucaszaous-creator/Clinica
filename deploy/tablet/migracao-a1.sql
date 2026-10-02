START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001140452_CertificadosA1Profissionais') THEN
    CREATE TABLE "CertificadosA1" (
        "UsuarioId" integer NOT NULL,
        "ProfissionalId" integer NOT NULL,
        "ArquivoProtegido" bytea NOT NULL,
        "ImpressaoDigital" character varying(64) NOT NULL,
        "Titular" character varying(300) NOT NULL,
        "ValidoAte" timestamp without time zone NOT NULL,
        "Versao" uuid NOT NULL,
        CONSTRAINT "PK_CertificadosA1" PRIMARY KEY ("UsuarioId"),
        CONSTRAINT "FK_CertificadosA1_Profissionais_ProfissionalId" FOREIGN KEY ("ProfissionalId") REFERENCES "Profissionais" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_CertificadosA1_Usuarios_UsuarioId" FOREIGN KEY ("UsuarioId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001140452_CertificadosA1Profissionais') THEN
    CREATE INDEX "IX_CertificadosA1_ProfissionalId" ON "CertificadosA1" ("ProfissionalId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001140452_CertificadosA1Profissionais') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261001140452_CertificadosA1Profissionais', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001180000_AuditoriaPreservaDetalheCompleto') THEN
    ALTER TABLE "Auditoria" ALTER COLUMN "Detalhe" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001180000_AuditoriaPreservaDetalheCompleto') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261001180000_AuditoriaPreservaDetalheCompleto', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002140919_ContinuidadeSemAssinatura') THEN
    ALTER TABLE "PrescricoesInternas" ADD "LiberadaSemAssinaturaEm" timestamp without time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002140919_ContinuidadeSemAssinatura') THEN
    ALTER TABLE "PrescricoesInternas" ADD "ModoSemAssinatura" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002140919_ContinuidadeSemAssinatura') THEN
    ALTER TABLE "ChecagensPrescricao" ADD "ExecutanteCpf" character varying(14);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002140919_ContinuidadeSemAssinatura') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261002140919_ContinuidadeSemAssinatura', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002162759_GruposInfusaoECatalogoMedicamentos') THEN
    ALTER TABLE "ItensPrescricaoInterna" ADD "GrupoInfusao" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002162759_GruposInfusaoECatalogoMedicamentos') THEN
    CREATE TABLE "MedicamentosCadastro" (
        "Codigo" character varying(48) NOT NULL,
        "Nome" character varying(200) NOT NULL,
        "PrincipioAtivo" character varying(200),
        "Apresentacao" character varying(200),
        "Fabricante" character varying(160),
        "Fonte" character varying(120) NOT NULL,
        "Ativo" boolean NOT NULL,
        "AtualizadoEm" timestamp without time zone NOT NULL,
        "AtualizadoPor" character varying(200),
        CONSTRAINT "PK_MedicamentosCadastro" PRIMARY KEY ("Codigo")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002162759_GruposInfusaoECatalogoMedicamentos') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261002162759_GruposInfusaoECatalogoMedicamentos', '8.0.11');
    END IF;
END $EF$;
COMMIT;

