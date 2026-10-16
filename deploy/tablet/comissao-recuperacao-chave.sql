-- Chave da comissão de recuperação para "Acompanhamentos" (out/2026).
-- Rodar no servidor como o DONO da tabela (postgres), DEPOIS de o app ter aplicado a migration
-- 20261016134007_ComissaoRecuperacao. A migration só cria esta chave quando o papel do desktop
-- tem REFERENCES em "Acompanhamentos" — em produção não tem, porque a tabela nasceu pelo
-- script do posto. Idempotente: rodar duas vezes não faz nada na segunda.
DO $$
BEGIN
    IF to_regclass('"ComissoesRecuperacao"') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM pg_constraint
                       WHERE conname = 'FK_ComissoesRecuperacao_Acompanhamentos_AcompanhamentoPaciente~') THEN
        ALTER TABLE "ComissoesRecuperacao"
            ADD CONSTRAINT "FK_ComissoesRecuperacao_Acompanhamentos_AcompanhamentoPaciente~"
            FOREIGN KEY ("AcompanhamentoPacienteId") REFERENCES "Acompanhamentos" ("Id") ON DELETE RESTRICT;
    END IF;
END $$;
