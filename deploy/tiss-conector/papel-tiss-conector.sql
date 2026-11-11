-- O usuário do Postgres do conector TISS (docs/tiss-conector.md §2). Rode como postgres:
--   sudo -u postgres psql -p 45432 -d clinica -v senha="'SENHA_FORTE'" -f papel-tiss-conector.sql
-- Rodar de novo é seguro.
--
-- Ele enxerga e grava o MÍNIMO: lê a fila, a configuração, o cadastro de convênios e a guia com
-- a conferência dela; grava o desfecho na fila e a trilha. Não lê a ficha do paciente, o
-- atendimento, prontuário, agenda, financeiro nem usuários;
-- não apaga nada; não cria nem altera tabela; não é dono de nada. Diferente do papel da ponte
-- do CRM, ele NÃO recebe leitura de tabela nova: o que não está na lista, ele não vê.
--
-- ⚠️ Esta lista foi escrita a partir do código e NÃO foi medida contra um Postgres de verdade
-- (o papel da ponte tem PontePapelRestritoTests; este ainda não tem o equivalente). Se o conector
-- registrar "42501" (permissão negada) no log, falta uma linha aqui — e é para faltar mesmo,
-- até alguém decidir que ele deve alcançar aquela tabela.
--
-- Para ENVIAR o lote ele lê a guia e a conferência dela (Codigos e GuiasTissConsulta), e só:
-- tudo o que vai à operadora já está na guia conferida. A ficha do paciente (Pacientes) e o
-- atendimento (Atendimentos) continuam fora do alcance dele.
DO $$ BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'tiss_conector') THEN
    CREATE ROLE tiss_conector LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;
  END IF;
END $$;
ALTER ROLE tiss_conector PASSWORD :senha;
GRANT CONNECT ON DATABASE clinica TO tiss_conector;
GRANT USAGE ON SCHEMA public TO tiss_conector;
-- Rodar de novo zera tudo antes de conceder: GRANT sozinho não tira o que uma versão anterior deu.
REVOKE ALL ON ALL TABLES IN SCHEMA public FROM tiss_conector;
REVOKE ALL ON ALL SEQUENCES IN SCHEMA public FROM tiss_conector;

DO $$
DECLARE g text[];
BEGIN
  IF to_regclass('public."TransacoesTiss"') IS NULL THEN
    RAISE EXCEPTION 'O banco ainda não tem a tabela TransacoesTiss. Abra um dos apps já atualizados uma vez (ele aplica as migrations) e rode este arquivo de novo.';
  END IF;
  -- {tabela, privilégios}
  FOREACH g SLICE 1 IN ARRAY ARRAY[
      -- A fila: quem PÕE o pedido é o app (a tela); o conector reserva e grava o desfecho.
      ['TransacoesTiss','SELECT, UPDATE'],
      -- As guias de cada lote enviado: o conector grava o resultado da operadora POR GUIA.
      ['TransacoesTissGuias','SELECT, UPDATE'],
      -- A configuração: dados da clínica (CNPJ, registro ANS), a credencial cifrada, o endereço
      -- do web service e a chave de envio por convênio. Só leitura: quem muda é o comando de
      -- quem opera, com outro usuário do banco (docs/tiss-conector.md §4).
      ['Configuracoes','SELECT'],
      -- O cadastro do convênio: o registro ANS da operadora e a família da regra.
      ['Convenios','SELECT'],
      -- A guia e a guia ANS conferida: o que o lote leva. A carteira e o nome do beneficiário que
      -- estão na conferência são dado pessoal — é o mínimo que o envio precisa ler.
      ['Codigos','SELECT'], ['GuiasTissConsulta','SELECT'],
      -- A trilha não se lê nem se edita daqui: só se acrescenta (envio e cancelamento concluídos).
      ['Auditoria','INSERT']] LOOP
    EXECUTE format('GRANT %s ON public.%I TO tiss_conector', g[2], g[1]);
  END LOOP;
END $$;
-- O EF grava a linha da trilha com RETURNING "Id": ler SÓ essa coluna é preciso para o INSERT passar.
GRANT SELECT ("Id") ON public."Auditoria" TO tiss_conector;
-- A numeração da trilha: coluna de identidade não pede privilégio na sequência, mas um banco
-- antigo pode tê-la como serial. Concede só se a sequência existir.
DO $$ BEGIN
  IF to_regclass('public."Auditoria_Id_seq"') IS NOT NULL THEN
    GRANT USAGE ON SEQUENCE public."Auditoria_Id_seq" TO tiss_conector;
  END IF;
END $$;

-- E no pg_hba.conf, SÓ pelo socket local (acrescente antes das linhas hostssl):
--   local   clinica   tiss_conector   scram-sha-256
