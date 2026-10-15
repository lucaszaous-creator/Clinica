-- Aviso instantâneo dos pedidos da enfermagem (out/2026) — OPCIONAL.
--
-- A migration 20261015120000_AvisosInstantaneos cria o gatilho só onde o papel do desktop
-- tem privilégio TRIGGER. As tabelas dos pedidos da enfermagem são criadas por
-- migracao-pedidos-enfermagem.sql como postgres, então lá o gatilho NÃO nasce e o
-- cabeçalho dos pedidos continua relendo a cada 15 s, como sempre.
--
-- Rodado como dono das tabelas (postgres), este script cria os dois gatilhos que faltam.
-- Idempotente; não concede privilégio nenhum; o aviso carrega só o NOME da tabela.
-- Exige que a migration AvisosInstantaneos já tenha rodado (ela cria a função).
DO $avisos$
DECLARE gatilho text; tabela text;
BEGIN
    IF to_regprocedure('public.clinica_avisar_mudanca()') IS NULL THEN
        RAISE NOTICE 'Função clinica_avisar_mudanca ausente: abra um app atualizado antes.';
        RETURN;
    END IF;
    FOR gatilho, tabela IN
        VALUES ('clinica_avisar_solicitacoes_enfermagem', 'SolicitacoesEnfermagem'),
               ('clinica_avisar_eventos_solicitacao_enfermagem', 'EventosSolicitacaoEnfermagem')
    LOOP
        CONTINUE WHEN to_regclass(format('public.%I', tabela)) IS NULL;
        CONTINUE WHEN EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = gatilho
                              AND tgrelid = format('public.%I', tabela)::regclass);
        EXECUTE format('CREATE TRIGGER %I AFTER INSERT OR UPDATE OR DELETE ON public.%I '
                       'FOR EACH STATEMENT EXECUTE FUNCTION public.clinica_avisar_mudanca()',
                       gatilho, tabela);
    END LOOP;
END
$avisos$;
