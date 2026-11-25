import os
import sqlite3
import tempfile
import subprocess
import sys
import unittest
from pathlib import Path

from regras_publicacao import (
    LINHA_SENHA_PONTE,
    papel_ponte_para_deploy,
    concessoes_papel_ponte,
    conferencia_papel_ponte,
    faxina_depois_de_publicar,
    releases_para_remover,
    FLAG_CRIPTOGRAFIA,
    MENSAGEM_CREDENCIAIS_CIFRADAS,
    PREFIXOS_CIFRADOS_DO_CONECTOR_TISS,
    consulta_credenciais_cifradas,
    verificar_sem_credenciais_cifradas,
    incluir_flag_false_se_ausente,
    verificar_criptografia_desativada,
    verificar_flag_desativada_no_processo,
)


class RegrasPublicacaoTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.base = Path(self.temp.name)
        self.portal = self.base / "portal.env"
        self.adicional = self.base / "adicional.env"
        self.adicional.write_text("EXEMPLO_CONFIGURACAO=valor\n", encoding="utf-8")

    def tearDown(self):
        self.temp.cleanup()

    def verificar(self, portal: str, environment: str = "", arquivos: str = ""):
        self.portal.write_text(portal, encoding="utf-8")
        unidade = (
            "[Service]\n"
            f"EnvironmentFile={self.portal.as_posix()}\n"
            f"{arquivos}"
            f"{environment}"
        )
        verificar_criptografia_desativada(self.portal, unidade)

    def test_flag_ausente_no_portal_env_bloqueia(self):
        with self.assertRaisesRegex(ValueError, "explicitamente como false"):
            self.verificar("Database=clinica\n")

    def test_false_explicito_no_portal_env_e_aceito(self):
        self.verificar(f"Database=clinica\n{FLAG_CRIPTOGRAFIA}=false\n")

    def test_flag_false_e_acrescentada_somente_quando_ausente(self):
        novo = incluir_flag_false_se_ausente(b"Database=clinica\r\n")
        self.assertEqual(novo, f"Database=clinica\r\n{FLAG_CRIPTOGRAFIA}=false\r\n".encode())
        existente = f"{FLAG_CRIPTOGRAFIA}=true\n".encode()
        self.assertEqual(incluir_flag_false_se_ausente(existente), existente)

    def test_configuracao_preparada_em_memoria_pode_ser_validada_sem_escrever(self):
        self.portal.write_text("Database=clinica\n", encoding="utf-8")
        unidade = f"[Service]\nEnvironmentFile={self.portal.as_posix()}\n"
        preparada = incluir_flag_false_se_ausente(self.portal.read_bytes()).decode("utf-8")
        verificar_criptografia_desativada(self.portal, unidade, preparada)
        self.assertEqual(self.portal.read_text(encoding="utf-8"), "Database=clinica\n")

    def test_flag_efetiva_no_processo_precisa_ser_false(self):
        verificar_flag_desativada_no_processo(
            f"PATH=/usr/bin\0{FLAG_CRIPTOGRAFIA}=false\0".encode()
        )
        with self.assertRaisesRegex(ValueError, "ativa"):
            verificar_flag_desativada_no_processo(f"{FLAG_CRIPTOGRAFIA}=true\0".encode())

    def test_false_com_aspas_simples_e_duplas_e_aceito(self):
        self.verificar(f"{FLAG_CRIPTOGRAFIA}='false'\n")
        self.verificar(f'{FLAG_CRIPTOGRAFIA}="false"\n')

    def test_pamname_bloqueia_sobrescrita_na_fonte_posterior(self):
        with self.assertRaisesRegex(ValueError, "PAMName"):
            self.verificar(f"{FLAG_CRIPTOGRAFIA}=false\n", environment="PAMName=login\n")

    def test_execstart_nao_pode_alterar_a_flag(self):
        with self.assertRaisesRegex(ValueError, r"Exec\*"):
            self.verificar(
                f"{FLAG_CRIPTOGRAFIA}=false\n",
                environment=f"ExecStart=/usr/bin/env {FLAG_CRIPTOGRAFIA}=true app\n",
            )

    def test_true_com_aspas_simples_bloqueia(self):
        with self.assertRaisesRegex(ValueError, "ativa"):
            self.verificar(f"{FLAG_CRIPTOGRAFIA}='true'\n")

    def test_ultima_duplicata_true_bloqueia(self):
        with self.assertRaisesRegex(ValueError, "ativa"):
            self.verificar(f"{FLAG_CRIPTOGRAFIA}=false\n{FLAG_CRIPTOGRAFIA}=true\n")

    def test_ativacao_dividida_por_continuacao_bloqueia(self):
        com_continuacao = FLAG_CRIPTOGRAFIA[:12] + "\\\n  " + FLAG_CRIPTOGRAFIA[12:] + "=true\n"
        with self.assertRaisesRegex(ValueError, "ativa"):
            self.verificar(com_continuacao)

    def test_ambiguidade_bloqueia(self):
        with self.assertRaisesRegex(ValueError, "confirmar"):
            self.verificar(f"{FLAG_CRIPTOGRAFIA}=false # anotacao\n")

    def test_environment_do_systemd_com_true_bloqueia(self):
        with self.assertRaisesRegex(ValueError, "ativa"):
            self.verificar("", environment=f"Environment={FLAG_CRIPTOGRAFIA}=true\n")

    def test_setenvironment_do_systemd_com_true_bloqueia(self):
        with self.assertRaisesRegex(ValueError, "ativa"):
            self.verificar("", environment=f"SetEnvironment={FLAG_CRIPTOGRAFIA}=true\n")

    def test_passenvironment_da_flag_bloqueia_por_valor_indeterminado(self):
        with self.assertRaisesRegex(ValueError, "PassEnvironment"):
            self.verificar("", environment=f"PassEnvironment={FLAG_CRIPTOGRAFIA}\n")

    def test_environmentfile_adicional_tambem_e_verificado(self):
        self.adicional.write_text(f"{FLAG_CRIPTOGRAFIA}=true\n", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "ativa"):
            self.verificar(
                f"{FLAG_CRIPTOGRAFIA}=false\n",
                arquivos=f"EnvironmentFile={self.adicional.as_posix()}\n",
            )

    def test_atualizador_recusa_python_otimizado(self):
        repo = Path(__file__).resolve().parents[2]
        script = repo / "deploy/tablet/atualizar-posto.py"
        preparar = (
            "import runpy,sys,types; "
            f"sys.path.insert(0, {str(script.parent)!r}); "
            "sys.modules['pwd']=types.ModuleType('pwd'); "
            "sys.argv=['atualizar-posto.py']; "
            f"runpy.run_path({str(script)!r})"
        )
        resultado = subprocess.run(
            [sys.executable, "-O", "-c", preparar],
            cwd=repo,
            capture_output=True,
            text=True,
        )
        self.assertNotEqual(resultado.returncode, 0)
        self.assertIn("PYTHONOPTIMIZE", resultado.stderr)



class TravaDasCredenciaisCifradasTests(unittest.TestCase):
    """A consulta de verdade, sobre uma tabela "Configuracoes" em memória (valores fictícios)."""

    def base(self, linhas):
        conexao = sqlite3.connect(":memory:")
        conexao.execute('CREATE TABLE "Configuracoes" ("Chave" TEXT PRIMARY KEY, "Valor" TEXT NOT NULL)')
        conexao.executemany('INSERT INTO "Configuracoes" VALUES (?, ?)', linhas)
        self.addCleanup(conexao.close)
        return lambda consulta: str(conexao.execute(consulta).fetchone()[0])

    def test_so_a_senha_cifrada_do_conector_tiss_passa(self):
        executar = self.base([
            ("TissSenha:Petrobras", "enc:v1:ficticio-1"),
            ("TissSenha:Amil", "enc:v1:ficticio-2"),
            ("TissLogin:Amil", "000123"),
            ("PublicacaoBucket", "bucket-de-teste"),
        ])
        verificar_sem_credenciais_cifradas(executar)

    def test_outra_chave_cifrada_recusa_com_a_mesma_frase(self):
        for chave in ("PublicacaoSecretKey", "SafeIDClientSecret", "TissLogin:Amil", "XTissSenha:Amil", "tisssenha:Amil"):
            with self.subTest(chave=chave):
                executar = self.base([(chave, "enc:v1:ficticio")])
                with self.assertRaises(RuntimeError) as erro:
                    verificar_sem_credenciais_cifradas(executar)
                self.assertEqual(str(erro.exception), MENSAGEM_CREDENCIAIS_CIFRADAS)
                self.assertNotIn("ficticio", str(erro.exception))

    def test_mistura_da_senha_do_conector_com_outra_cifrada_recusa(self):
        executar = self.base([
            ("TissSenha:Petrobras", "enc:v1:ficticio-1"),
            ("PublicacaoSecretKey", "enc:v1:ficticio-2"),
        ])
        with self.assertRaises(RuntimeError) as erro:
            verificar_sem_credenciais_cifradas(executar)
        self.assertEqual(str(erro.exception), MENSAGEM_CREDENCIAIS_CIFRADAS)

    def test_base_sem_nada_cifrado_passa(self):
        verificar_sem_credenciais_cifradas(self.base([("PublicacaoBucket", "bucket-de-teste")]))

    def test_a_lista_de_excecoes_e_so_a_senha_do_conector(self):
        self.assertEqual(PREFIXOS_CIFRADOS_DO_CONECTOR_TISS, ("TissSenha:",))
        consulta = consulta_credenciais_cifradas()
        self.assertIn("\"Valor\" LIKE 'enc:%'", consulta)
        self.assertNotIn("TissLogin", consulta)

    def test_o_atualizador_usa_a_regra_daqui(self):
        atualizador = (Path(__file__).parent / "atualizar-posto.py").read_text(encoding="utf-8")
        self.assertIn("_verificar_sem_credenciais_cifradas(sql)", atualizador)
        self.assertNotIn("LIKE 'enc:%'", atualizador)


class FaxinaDepoisDePublicarTests(unittest.TestCase):
    """Nov/2026: o disco da VPS encheu com releases e pacotes antigos e o Postgres parou de gravar."""

    def test_ficam_o_current_o_anterior_e_os_cinco_mais_novos_com_o_runtime_junto(self):
        releases = {f"r{i:02d}": float(i) for i in range(1, 11)}
        releases["r01-runtime"] = 1.0
        releases["r10-runtime"] = 10.0
        sai = releases_para_remover(releases, {"r03", "r02"})
        self.assertEqual(sai, ["r01", "r01-runtime", "r04", "r05"])  # ficam r10..r06 (5 mais novos) + r03 e r02 (manter)

    def test_poucos_releases_nada_sai(self):
        self.assertEqual(releases_para_remover({"a": 1.0, "b": 2.0}, {"a"}), [])

    def test_apaga_na_pasta_e_o_pacote_instalado(self):
        with tempfile.TemporaryDirectory() as d:
            base = Path(d) / "servico"; pasta = base / "releases"; pasta.mkdir(parents=True)
            for i in range(1, 9):
                (pasta / f"r{i}").mkdir()
                os.utime(pasta / f"r{i}", (i, i))
            pacote = Path(d) / "pacote.tar.gz"; pacote.write_bytes(b"x")
            saiu = faxina_depois_de_publicar(base, {"r8", "r1"}, pacote)
            self.assertEqual(sorted(p.name for p in pasta.iterdir()), ["r1", "r4", "r5", "r6", "r7", "r8"])
            self.assertIn("pacote.tar.gz", saiu)
            self.assertFalse(pacote.exists())

PAPEL = (Path(__file__).resolve().parents[1] / "ponte-crm" / "papel-ponte-crm.sql").read_text(encoding="utf-8-sig")


class PapelDaPonteNoDeploy(unittest.TestCase):
    """O deploy reaplica o papel da ponte e confere (nov/2026: a fila de envios parou com 42501)."""

    def test_reaplica_sem_trocar_a_senha(self):
        script = papel_ponte_para_deploy(PAPEL)
        self.assertNotIn(LINHA_SENHA_PONTE, script)
        self.assertNotIn("PASSWORD :senha", script)
        self.assertIn("REVOKE INSERT, UPDATE, DELETE, TRUNCATE ON ALL TABLES IN SCHEMA public FROM ponte_crm", script)

    def test_linha_da_senha_mudou_recusa(self):
        with self.assertRaises(ValueError):
            papel_ponte_para_deploy(PAPEL.replace(LINHA_SENHA_PONTE, "ALTER ROLE ponte_crm PASSWORD 'x';"))

    def test_le_as_concessoes_do_arquivo(self):
        c = set(concessoes_papel_ponte(PAPEL))
        for esperado in [("EnviosPacienteCrm", "UPDATE", ()), ("LiberacoesDeTrava", "UPDATE", ()),
                         ("AvisosDeLiberacao", "INSERT", ()), ("AvisosDeLiberacao", "UPDATE", ()),
                         ("Auditoria", "INSERT", ()), ("BilhetesEntradaCrm", "UPDATE", ("UsadoEm",)),
                         ("Usuarios", "UPDATE", ("TentativasFalhas", "BloqueadoAte", "UltimoAcessoEm"))]:
            self.assertIn(esperado, c)
        self.assertNotIn(("EnviosPacienteCrm", "INSERT", ()), c, "quem põe o pedido é o desktop")

    def test_conferencia_pergunta_cada_concessao(self):
        q = conferencia_papel_ponte(PAPEL)
        self.assertIn("has_table_privilege('ponte_crm', 'public.\"EnviosPacienteCrm\"', 'UPDATE')", q)
        self.assertIn("has_column_privilege('ponte_crm', 'public.\"Usuarios\"', 'BloqueadoAte', 'UPDATE')", q)
        esperadas = sum(max(1, len(colunas)) for _, _, colunas in concessoes_papel_ponte(PAPEL))
        self.assertEqual(q.count("SELECT '"), esperadas, "uma pergunta por tabela-privilégio e por coluna")
        self.assertTrue(q.rstrip().endswith(";"))


if __name__ == "__main__":
    unittest.main()
