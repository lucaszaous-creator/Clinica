import tempfile
import subprocess
import sys
import unittest
from pathlib import Path

from regras_publicacao import (
    FLAG_CRIPTOGRAFIA,
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


if __name__ == "__main__":
    unittest.main()
