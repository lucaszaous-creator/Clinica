-- PROPOSTA PARA REVISÃO. Não é migration e não é executada pelo aplicativo.
-- Validar planos e volume no ambiente de destino antes de aprovar produção.
-- Executar cada comando fora de transação; CONCURRENTLY mantém escritas permitidas,
-- mas consome CPU/I/O. Conferir indisvalid/indisready e definições já existentes.
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE INDEX CONCURRENTLY "IX_Pacientes_Nome_Trigram"
 ON public."Pacientes" USING gin (lower("Nome") gin_trgm_ops);
CREATE INDEX CONCURRENTLY "IX_Pacientes_Documento_Trigram"
 ON public."Pacientes" USING gin ("Documento" gin_trgm_ops);
CREATE INDEX CONCURRENTLY "IX_Pacientes_Nome_Id"
 ON public."Pacientes" ("Nome", "Id");
CREATE INDEX CONCURRENTLY "IX_DocumentosClinicos_PacienteId_Tipo_Data_Id"
 ON public."DocumentosClinicos" ("PacienteId", "Tipo", "Data" DESC, "Id" DESC);
ANALYZE public."Pacientes";
ANALYZE public."DocumentosClinicos";

-- Reversão específica, caso aprovada/necessária, também fora de transação:
-- DROP INDEX CONCURRENTLY public."IX_Pacientes_Nome_Trigram";
-- DROP INDEX CONCURRENTLY public."IX_Pacientes_Documento_Trigram";
-- DROP INDEX CONCURRENTLY public."IX_Pacientes_Nome_Id";
-- DROP INDEX CONCURRENTLY public."IX_DocumentosClinicos_PacienteId_Tipo_Data_Id";
-- Não remover pg_trgm: a extensão pode servir a outras consultas.
