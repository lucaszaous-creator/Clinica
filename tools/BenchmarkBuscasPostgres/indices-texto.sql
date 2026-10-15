-- Experimento: somente no banco sintético criado pelo benchmark.
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE INDEX bench_pacientes_nome_trgm ON "Pacientes" USING gin (lower("Nome") gin_trgm_ops);
CREATE INDEX bench_pacientes_documento_trgm ON "Pacientes" USING gin ("Documento" gin_trgm_ops);
