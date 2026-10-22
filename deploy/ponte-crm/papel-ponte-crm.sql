-- O usuário do Postgres da ponte do CRM (docs/vps-do-crm.md §4). Rode como postgres:
--   sudo -u postgres psql -p 45432 -d clinica -v senha="'SENHA_FORTE'" -f papel-ponte-crm.sql
-- Rodar de novo é seguro (depois de uma atualização que crie tabela que a ponte precise gravar).
--
-- Ele LÊ tudo, mas só GRAVA onde a ponte grava: a agenda, a guia que nasce ou é suspensa com
-- ela, o contato da rodada, o SMS, o recall, a fila de envios ao paciente e a trilha. Não apaga nada, não cria nem altera
-- tabela e não é dono de nada. Usuários, prontuário e financeiro ficam só para leitura: se a
-- credencial vazar, o estrago não alcança o banco inteiro.
--
-- A lista é MEDIDA, não suposta: PontePapelRestritoTests roda os fluxos da ponte (confirmar,
-- cancelar, marcar com guia no agendamento, remarcar, recall, o lembrete de SMS e a fila de
-- envios ao paciente) com ESTE
-- arquivo contra um Postgres de verdade. Fluxo novo que grave em outra tabela reprova ali
-- com 42501 antes de reprovar na clínica.
DO $$ BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'ponte_crm') THEN
    CREATE ROLE ponte_crm LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;
  END IF;
END $$;
ALTER ROLE ponte_crm PASSWORD :senha;
GRANT CONNECT ON DATABASE clinica TO ponte_crm;
GRANT USAGE ON SCHEMA public TO ponte_crm;
-- Rodar de novo zera a escrita antes de conceder: uma versão anterior deste arquivo dava
-- INSERT/UPDATE em TODAS as tabelas, e GRANT sozinho não tira nada.
REVOKE INSERT, UPDATE, DELETE, TRUNCATE ON ALL TABLES IN SCHEMA public FROM ponte_crm;
ALTER DEFAULT PRIVILEGES FOR ROLE clinica IN SCHEMA public REVOKE INSERT, UPDATE ON TABLES FROM ponte_crm;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO ponte_crm;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO ponte_crm;

DO $$
DECLARE g text[];
BEGIN
  IF to_regclass('public."Agendamentos"') IS NULL THEN
    RAISE EXCEPTION 'O banco ainda não tem as tabelas da clínica. Abra um dos apps uma vez (ele aplica as migrations) e rode este arquivo de novo.';
  END IF;
  -- {tabela, privilégios}: o que os fluxos da ponte criam e mudam; a categoria do paciente
  -- quando a guia nasce na marcação (só UPDATE); e a trilha, que não se edita (só INSERT).
  FOREACH g SLICE 1 IN ARRAY ARRAY[
      ['Agendamentos','INSERT, UPDATE'], ['Atendimentos','INSERT, UPDATE'], ['Codigos','INSERT, UPDATE'],
      ['ContatosCampanha','INSERT, UPDATE'], ['MensagensSms','INSERT, UPDATE'],
      ['Acompanhamentos','INSERT, UPDATE'], ['ContatosAcompanhamento','INSERT, UPDATE'],
      ['ResumosCrmDiarios','INSERT, UPDATE'],
      -- A fila de envios ao paciente (out/2026): quem PÕE o pedido é o desktop; a ponte só
      -- reserva, autoriza e registra o resultado — UPDATE, e só.
      ['EnviosPacienteCrm','UPDATE'],
      -- A equipe pergunta ao sistema pelo WhatsApp da clínica (out/2026): a pergunta e a resposta
      -- ficam na conversa da pessoa com o sistema (já lida), o pedido à recepção nasce com o
      -- cartão no # Recepção, e o aviso do desfecho pelo WhatsApp é marcado no pedido.
      ['MensagensEquipe','INSERT, UPDATE'], ['ConversasEquipe','INSERT, UPDATE'],
      ['LeiturasEquipe','INSERT, UPDATE'],
      ['PedidosRecepcao','INSERT, UPDATE'], ['EventosPedidoRecepcao','INSERT'],
      -- O documento pedido pelo WhatsApp (@SemDor fase 4): a ponte grava só o RASCUNHO e o link;
      -- quem emite e assina é o portal. Nenhum privilégio sobre DocumentosClinicos.
      ['PedidosDocumentoWhatsApp','INSERT'],
      ['Pacientes','UPDATE'], ['Auditoria','INSERT']] LOOP
    IF to_regclass(format('public.%I', g[1])) IS NOT NULL THEN
      EXECUTE format('GRANT %s ON public.%I TO ponte_crm', g[2], g[1]);
    ELSE
      -- Acompanhamentos e ContatosAcompanhamento nascem pelo script do posto (deploy/tablet).
      RAISE NOTICE 'Tabela % ainda não existe: rode este arquivo de novo depois que ela for criada.', g[1];
    END IF;
  END LOOP;
END $$;

-- O login do CRM é o do sistema (ClinicTeamBridge): conferir a senha conta as tentativas,
-- trava depois de 5 erros e marca o último acesso — as TRÊS colunas abaixo, e só elas. A
-- senha (SenhaHash/SenhaSalt), o perfil e as permissões continuam só para leitura: quem
-- tiver esta credencial não troca senha nem se dá acesso. (O REVOKE de tabela acima não
-- tira privilégio de coluna, então rodar de novo não duplica nem apaga isto.)
DO $$
BEGIN
  IF to_regclass('public."Usuarios"') IS NOT NULL THEN
    GRANT UPDATE ("TentativasFalhas", "BloqueadoAte", "UltimoAcessoEm") ON public."Usuarios" TO ponte_crm;
  END IF;
END $$;

-- Tabela nova criada por migration (dono: clinica) entra só para LEITURA. Se um fluxo da
-- ponte passar a gravar nela, o teste acima reprova e ela entra na lista.
ALTER DEFAULT PRIVILEGES FOR ROLE clinica IN SCHEMA public GRANT SELECT ON TABLES TO ponte_crm;
ALTER DEFAULT PRIVILEGES FOR ROLE clinica IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO ponte_crm;
-- E no pg_hba.conf, SÓ pelo socket local (acrescente antes das linhas hostssl):
--   local   clinica   ponte_crm   scram-sha-256
