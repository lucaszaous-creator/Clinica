import tempfile
import subprocess
import sys
import unittest
from pathlib import Path

from regras_publicacao import FLAG_CRIPTOGRAFIA, verificar_criptografia_desativada


class RegrasPublicacaoTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.base = Path(self.temp.name)
        self.portal = self.base / "portal.env"
        self.safeid = self.base / "safeid.env"
        self.safeid.write_text("PORTAL_SAFEID_CLIENT_ID=id\n", encoding="utf-8")

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

    def test_flag_ausente_preserva_modo_compativel(self):
        self.verificar("Database=clinica\n")

    def test_false_com_aspas_simples_e_duplas_e_aceito(self):
        self.verificar(f"{FLAG_CRIPTOGRAFIA}='false'\n")
        self.verificar(f'{FLAG_CRIPTOGRAFIA}="false"\n')

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

    def test_environmentfile_adicional_tambem_e_verificado(self):
        self.safeid.write_text(f"{FLAG_CRIPTOGRAFIA}=true\n", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "ativa"):
            self.verificar(
                "",
                arquivos=f"EnvironmentFile={self.safeid.as_posix()}\n",
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
