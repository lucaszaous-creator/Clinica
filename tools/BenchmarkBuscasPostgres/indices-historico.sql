-- Candidatos: comparar antes de escolher os índices de entrega.
CREATE INDEX bench_pacientes_ordem ON "Pacientes" ("Nome", "Id");
CREATE INDEX bench_documentos_paciente_ordem ON "DocumentosClinicos" ("PacienteId", "Data" DESC, "Id" DESC);
CREATE INDEX bench_documentos_paciente_tipo ON "DocumentosClinicos" ("PacienteId", "Tipo", "Data" DESC, "Id" DESC);
CREATE INDEX bench_prescricoes_paciente_ordem ON "PrescricoesInternas" ("PacienteId", "Data" DESC, "Id" DESC);
CREATE INDEX bench_prescricoes_ultima ON "PrescricoesInternas" ("PacienteId", "Data" DESC, "Hora" DESC, "Id" DESC)
 WHERE "CanceladaEm" IS NULL AND NOT "OrigemEnfermagem" AND "Situacao" IN ('Assinada', 'Liberada', 'Encerrada');
CREATE INDEX bench_agendamentos_paciente_ordem ON "Agendamentos" ("PacienteId", "DataHora" DESC, "Id" DESC);
CREATE INDEX bench_agendamentos_realizados ON "Agendamentos" ("PacienteId") WHERE "Status" = 'Realizado';
CREATE INDEX bench_evolucoes_paciente_ordem ON "Evolucoes" ("PacienteId", "Data" DESC, "Id" DESC) WHERE "CanceladaEm" IS NULL;
