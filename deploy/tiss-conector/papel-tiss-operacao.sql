-- O usuário do Postgres de QUEM OPERA o conector TISS (docs/tiss-conector.md §4): é o do
-- /etc/clinica-tiss-conector/admin.env, carregado só pelo comando.sh, nunca pelo serviço.
-- Serve para gravar a credencial da operadora, o endereço, o código do prestador e a chave de
-- envio por convênio — e a linha da trilha de cada mudança.
--
-- Rode como root, entregando o arquivo PELA ENTRADA (o usuário postgres não lê a pasta do conector):
--   sudo -u postgres psql -p 45432 -d clinica -v ON_ERROR_STOP=1 -v senha="'SENHA_FORTE'" < papel-tiss-operacao.sql
-- Rodar de novo é seguro.
--
-- Ele substitui, no admin.env, a senha do usuário dos APPS (que alcança o banco inteiro): este
-- só lê e grava a configuração e só ACRESCENTA na trilha. Não lê paciente, guia nem fila; não apaga
-- nada; não cria nem altera tabela.
DO $$ BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'tiss_operacao') THEN
    CREATE ROLE tiss_operacao LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;
  END IF;
END $$;
ALTER ROLE tiss_operacao PASSWORD :senha;
GRANT CONNECT ON DATABASE clinica TO tiss_operacao;
GRANT USAGE ON SCHEMA public TO tiss_operacao;
-- Rodar de novo zera tudo antes de conceder: GRANT sozinho não tira o que uma versão anterior deu.
REVOKE ALL ON ALL TABLES IN SCHEMA public FROM tiss_operacao;
REVOKE ALL ON ALL SEQUENCES IN SCHEMA public FROM tiss_operacao;

GRANT SELECT, INSERT, UPDATE ON public."Configuracoes" TO tiss_operacao;
GRANT INSERT ON public."Auditoria" TO tiss_operacao;
-- O EF grava a linha da trilha com RETURNING "Id": ler SÓ essa coluna é preciso para o INSERT passar.
GRANT SELECT ("Id") ON public."Auditoria" TO tiss_operacao;
DO $$ BEGIN
  IF to_regclass('public."Auditoria_Id_seq"') IS NOT NULL THEN
    GRANT USAGE ON SEQUENCE public."Auditoria_Id_seq" TO tiss_operacao;
  END IF;
END $$;

-- E no pg_hba.conf, SÓ pelo socket local (antes da primeira regra ativa):
--   local   clinica   tiss_operacao   scram-sha-256
