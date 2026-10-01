START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001140452_CertificadosA1Profissionais') THEN
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

CREATE INDEX "IX_CertificadosA1_ProfissionalId" ON "CertificadosA1" ("ProfissionalId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261001140452_CertificadosA1Profissionais', '8.0.11');
    END IF;
END $EF$;

-- A auditoria preserva a justificativa inteira junto ao contexto, sem truncar registros.
DO $EF$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001180000_AuditoriaPreservaDetalheCompleto') THEN
        ALTER TABLE "Auditoria" ALTER COLUMN "Detalhe" TYPE text;
        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
        VALUES ('20261001180000_AuditoriaPreservaDetalheCompleto', '8.0.11');
    END IF;
END $EF$;

COMMIT;
