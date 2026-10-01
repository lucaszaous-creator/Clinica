START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926120000_DevolucaoInfusaoExterna') THEN
    ALTER TABLE "PrescricoesInternas" ADD "DevolvidaEm" timestamp without time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926120000_DevolucaoInfusaoExterna') THEN
    ALTER TABLE "PrescricoesInternas" ADD "MotivoDevolucao" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926120000_DevolucaoInfusaoExterna') THEN
    ALTER TABLE "PrescricoesInternas" ADD "DevolvidaPorUsuarioId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926120000_DevolucaoInfusaoExterna') THEN
    ALTER TABLE "PrescricoesInternas" ADD "RetificaPrescricaoId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926120000_DevolucaoInfusaoExterna') THEN
    CREATE UNIQUE INDEX "IX_PrescricoesInternas_RetificaPrescricaoId" ON "PrescricoesInternas" ("RetificaPrescricaoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926120000_DevolucaoInfusaoExterna') THEN
    ALTER TABLE "PrescricoesInternas" ADD CONSTRAINT "FK_PrescricoesInternas_PrescricoesInternas_RetificaPrescricaoId" FOREIGN KEY ("RetificaPrescricaoId") REFERENCES "PrescricoesInternas" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926120000_DevolucaoInfusaoExterna') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260926120000_DevolucaoInfusaoExterna', '8.0.11');
    END IF;
END $EF$;
COMMIT;
