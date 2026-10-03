import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('sincronizar', Path(__file__).with_name('sincronizar-atualizacoes.py'))
sync = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sync)


def release(id, draft=False, prerelease=False):
    return {'id': id, 'draft': draft, 'prerelease': prerelease}


class AncoraTests(unittest.TestCase):
    def test_preserva_faturamento_se_ainda_visivel(self):
        win, clinico = release(1), release(2)
        self.assertEqual(sync.escolher_ancora({'win': win, 'clinico': clinico}, [clinico, win]), win)

    def test_usa_app_atual_visivel_quando_faturamento_sai_da_primeira_pagina(self):
        win, clinico = release(1), release(2)
        antigas = [release(i) for i in range(10, 19)]
        atuais = {'win': win, 'clinico': clinico}
        ancora = sync.escolher_ancora(atuais, [clinico, *antigas])
        self.assertEqual(ancora, clinico)
        # O laço de sincronização também espelha o canal win nessa nova referência.
        self.assertEqual([canal for canal, origem in atuais.items() if origem['id'] != ancora['id']], ['win'])

    def test_ignora_releases_alheias_rascunhos_e_pre_releases(self):
        win, clinico, gerente = release(1), release(2), release(3)
        pagina = [release(90), release(2, draft=True), release(1, prerelease=True), gerente]
        self.assertEqual(sync.escolher_ancora({'win': win, 'clinico': clinico, 'gerente': gerente}, pagina), gerente)

    def test_recusa_quando_nenhum_app_atual_e_descoberto(self):
        with self.assertRaisesRegex(RuntimeError, 'Nenhuma release atual'):
            sync.escolher_ancora({'win': release(1)}, [release(99)])


if __name__ == '__main__':
    unittest.main()
