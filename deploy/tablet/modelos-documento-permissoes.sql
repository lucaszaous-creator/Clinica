-- Executar como administrador na implantação do portal com edição de modelos.
-- Os modelos são compartilhados; a API exige a permissão Prescrever.
BEGIN;
GRANT SELECT, INSERT, UPDATE ON TABLE public."ModelosDocumento" TO "clinica-tablet";
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public."ItensModelo" TO "clinica-tablet";
GRANT USAGE, SELECT ON SEQUENCE public."ModelosDocumento_Id_seq", public."ItensModelo_Id_seq" TO "clinica-tablet";
COMMIT;
SELECT tabela,privilegio,has_table_privilege('clinica-tablet',tabela,privilegio) AS permitido
FROM (VALUES ('public."ModelosDocumento"','SELECT'),('public."ModelosDocumento"','INSERT'),
             ('public."ModelosDocumento"','UPDATE'),('public."ItensModelo"','SELECT'),
             ('public."ItensModelo"','INSERT'),('public."ItensModelo"','UPDATE'),('public."ItensModelo"','DELETE')) AS p(tabela,privilegio);
