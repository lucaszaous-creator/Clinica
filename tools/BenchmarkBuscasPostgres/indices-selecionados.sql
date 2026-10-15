-- Acrescentar aos dois GIN de indices-texto.sql, somente no fixture sintético.
CREATE INDEX bench_pacientes_ordem ON "Pacientes" ("Nome", "Id");
CREATE INDEX bench_documentos_paciente_tipo ON "DocumentosClinicos" ("PacienteId", "Tipo", "Data" DESC, "Id" DESC);
