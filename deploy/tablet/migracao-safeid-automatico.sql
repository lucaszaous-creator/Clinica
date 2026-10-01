START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001015315_ConclusaoAutomaticaSafeId') THEN
    CREATE TABLE "OperacoesAssinaturaTablet" (
        "Id" uuid NOT NULL,
        "SessaoId" character varying(64) NOT NULL,
        "Tipo" character varying(20) NOT NULL,
        "Agendamento" integer NOT NULL,
        "Documento" integer NOT NULL,
        "Situacao" character varying(20) NOT NULL,
        "ChaveAtiva" character varying(80),
        "ExpiraEm" bigint NOT NULL,
        "AtualizadaEm" bigint NOT NULL,
        CONSTRAINT "PK_OperacoesAssinaturaTablet" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_OperacoesAssinaturaTablet_SessoesTablet_SessaoId" FOREIGN KEY ("SessaoId") REFERENCES "SessoesTablet" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001015315_ConclusaoAutomaticaSafeId') THEN
    CREATE UNIQUE INDEX "IX_OperacoesAssinaturaTablet_ChaveAtiva" ON "OperacoesAssinaturaTablet" ("ChaveAtiva");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001015315_ConclusaoAutomaticaSafeId') THEN
    CREATE INDEX "IX_OperacoesAssinaturaTablet_SessaoId_Id" ON "OperacoesAssinaturaTablet" ("SessaoId", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001015315_ConclusaoAutomaticaSafeId') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261001015315_ConclusaoAutomaticaSafeId', '8.0.11');
    END IF;
END $EF$;
COMMIT;
