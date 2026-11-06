-- O PAPEL DE BANCO da gestão no navegador (nov/2026). Rodar como o dono das tabelas (o papel dos
-- apps da suíte), dentro do banco da clínica:  psql -d <banco> -v desktop=<papel-dono> -f papel-gestao.sql
--
-- Privilégio MÍNIMO para o que as telas do Gerente Geral fazem: ler e gravar DADOS. Nada de DDL,
-- nada de migration (quem migra são os apps da suíte), e o papel não é dono de nada — uma falha na
-- gestão não derruba tabela. A trilha de auditoria é só-acréscimo para este papel.
DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'clinica-gestao') THEN
    CREATE ROLE "clinica-gestao" LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;
  END IF;
END $$;
REVOKE CREATE ON SCHEMA public FROM "clinica-gestao";
GRANT USAGE ON SCHEMA public TO "clinica-gestao";
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO "clinica-gestao";
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO "clinica-gestao";
-- Tabelas que as próximas migrations criarem já nascem com o mesmo acesso.
ALTER DEFAULT PRIVILEGES FOR ROLE :"desktop" IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO "clinica-gestao";
ALTER DEFAULT PRIVILEGES FOR ROLE :"desktop" IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO "clinica-gestao";
-- Auditoria: acrescenta e lê, nunca altera nem apaga (o registro é a prova).
REVOKE UPDATE, DELETE ON "Auditoria" FROM "clinica-gestao";
-- O histórico das migrations é dos apps: a gestão nem lê.
REVOKE ALL ON "__EFMigrationsHistory" FROM "clinica-gestao";
-- REGISTRO CLÍNICO NÃO SE APAGA (compromisso de conformidade, ponto 1) — garantido também pelo
-- banco para este papel: as raízes do prontuário não aceitam DELETE vindo da gestão web, nem por
-- defeito de código. (Cascata de chave estrangeira roda com o dono da tabela, não com este papel.)
REVOKE DELETE ON "Evolucoes", "VersoesEvolucao", "Anamneses", "VersoesAnamnese", "AnexosProntuario",
  "DocumentosClinicos", "PrescricoesInternas", "EvolucoesEnfermagem", "AvaliacoesClinicas", "MedidasClinicas",
  "ResultadosExame", "Consentimentos", "AssinaturasDocumento", "ArquivosAssinados" FROM "clinica-gestao";
