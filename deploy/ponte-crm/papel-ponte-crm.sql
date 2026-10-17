-- O usuário do Postgres da ponte do CRM (docs/vps-do-crm.md §4). Rode como postgres:
--   sudo -u postgres psql -p 45432 -d clinica -v senha="'SENHA_FORTE'" -f papel-ponte-crm.sql
-- Ele LÊ e GRAVA (agenda, contato da rodada, SMS, trilha), mas não APAGA nada, não cria nem
-- altera tabela e não é dono de nada: migration continua sendo dos apps da clínica.
CREATE ROLE ponte_crm LOGIN PASSWORD :senha NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;
GRANT CONNECT ON DATABASE clinica TO ponte_crm;
GRANT USAGE ON SCHEMA public TO ponte_crm;
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public TO ponte_crm;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO ponte_crm;
-- Tabela nova criada por migration (dono: clinica) entra sozinha.
ALTER DEFAULT PRIVILEGES FOR ROLE clinica IN SCHEMA public GRANT SELECT, INSERT, UPDATE ON TABLES TO ponte_crm;
ALTER DEFAULT PRIVILEGES FOR ROLE clinica IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO ponte_crm;
-- E no pg_hba.conf, SÓ pelo socket local (acrescente antes das linhas hostssl):
--   local   clinica   ponte_crm   scram-sha-256
