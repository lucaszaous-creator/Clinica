"""Mantém os cinco canais visíveis também para o atualizador antigo (primeiras 10 releases)."""
import hashlib
import json
import pathlib
import re
import subprocess
import time
import urllib.request

REPO = 'lucaszaous-creator/Clinica'
CANAIS = {'win': ('v', 'Clinica.Faturamento'), 'clinico': ('clinico-v', 'Clinica.Clinico'),
          'gerente': ('gerente-v', 'Clinica.Gerente'), 'recepcao': ('recepcao-v', 'Clinica.Recepcao'),
          'financeiro': ('financeiro-v', 'Clinica.Financeiro')}

def gh(*args):
    return subprocess.check_output(['gh', *args], text=True, encoding='utf-8')

def ler_json(url):
    assert url.startswith('https://github.com/' + REPO + '/releases/download/')
    with urllib.request.urlopen(url, timeout=60) as r:
        return json.loads(r.read())

def principais(releases):
    resultado = {}
    for canal, (prefixo, _) in CANAIS.items():
        candidatas = []
        for r in releases:
            m = re.fullmatch(re.escape(prefixo) + r'(\d+)\.(\d+)\.(\d+)', r['tag_name'])
            if m and not r['draft'] and not r['prerelease']:
                candidatas.append((tuple(map(int, m.groups())), r))
        if not candidatas:
            raise RuntimeError('Canal sem release publicada: ' + canal)
        resultado[canal] = max(candidatas, key=lambda par: par[0])[1]
    return resultado

def asset_atual(release_id, nome):
    atual = json.loads(gh('api', f'repos/{REPO}/releases/{release_id}'))
    assert not atual['draft'] and not atual['prerelease']
    return next((a for a in atual['assets'] if a['name'] == nome), None)

def conferir_pacote(asset, entrada):
    assert asset and asset.get('state') == 'uploaded'
    assert asset['size'] == entrada['Size']
    assert asset.get('digest', '').lower() == 'sha256:' + entrada['SHA256'].lower()

def publicar_pacote(ancora, arquivo, entrada):
    # Um 500 pode ocorrer depois de o GitHub gravar o pacote. Confira antes de repetir.
    for tentativa in range(3):
        existente = asset_atual(ancora['id'], arquivo.name)
        if existente:
            if existente.get('state') == 'starter' and existente['size'] == 0:
                gh('api', '--method', 'DELETE', f'repos/{REPO}/releases/assets/{existente["id"]}')
            else:
                conferir_pacote(existente, entrada)
                return
        try:
            gh('release', 'upload', ancora['tag_name'], str(arquivo), '--repo', REPO)
        except subprocess.CalledProcessError:
            if tentativa == 2:
                conferir_pacote(asset_atual(ancora['id'], arquivo.name), entrada)
                return
            time.sleep(5 * (tentativa + 1))
            continue
        conferir_pacote(asset_atual(ancora['id'], arquivo.name), entrada)
        return

def main():
    paginas = json.loads(gh('api', '--paginate', '--slurp', f'repos/{REPO}/releases?per_page=100'))
    releases = [r for pagina in paginas for r in pagina]
    atuais = principais(releases)
    ancora = atuais['win']
    primeiras = json.loads(gh('api', f'repos/{REPO}/releases?per_page=10&page=1'))
    assert any(r['id'] == ancora['id'] for r in primeiras), 'Release de compatibilidade fora das primeiras dez'
    pasta = pathlib.Path('artifacts/feeds-compativeis')
    pasta.mkdir(parents=True, exist_ok=True)
    for canal, origem in atuais.items():
        if origem['id'] == ancora['id']:
            continue
        nome_feed = f'releases.{canal}.json'
        feed_asset = next(a for a in origem['assets'] if a['name'] == nome_feed)
        feed = ler_json(feed_asset['browser_download_url'])
        versao = origem['tag_name'][len(CANAIS[canal][0]):]
        # Espelho apenas do pacote integral: nada depende de deltas de outra release.
        entradas = [a for a in feed['Assets'] if a['Type'] == 'Full' and a['Version'] == versao
                    and a['PackageId'] == CANAIS[canal][1]]
        assert len(entradas) == 1
        entrada = entradas[0]
        nome = entrada['FileName']
        assert pathlib.PurePath(nome).name == nome and nome.endswith('-full.nupkg')
        assert re.fullmatch(r'[a-fA-F0-9]{64}', entrada['SHA256'])
        pacote_asset = next(a for a in origem['assets'] if a['name'] == nome)
        assert pacote_asset['size'] == entrada['Size'] > 0
        espelho = {'Assets': entradas}
        atual = json.loads(gh('api', f'repos/{REPO}/releases/{ancora["id"]}'))
        assert not atual['draft'] and not atual['prerelease']
        feed_existente = next((a for a in atual['assets'] if a['name'] == nome_feed), None)
        pacote_existente = next((a for a in atual['assets'] if a['name'] == nome), None)
        if feed_existente:
            anterior = ler_json(feed_existente['browser_download_url'])
            assert all(tuple(map(int, a['Version'].split('.'))) <= tuple(map(int, versao.split('.')))
                       for a in anterior['Assets']), 'Nunca recuar um canal publicado'
            if anterior == espelho and pacote_existente:
                conferir_pacote(pacote_existente, entrada)
                print(canal + ': espelho já confere')
                continue
        if pacote_existente and pacote_existente.get('state') == 'uploaded':
            conferir_pacote(pacote_existente, entrada)
        else:
            gh('release', 'download', origem['tag_name'], '--repo', REPO, '--pattern', nome,
               '--dir', str(pasta), '--skip-existing')
            arquivo = pasta / nome
            assert arquivo.stat().st_size == entrada['Size']
            assert hashlib.sha256(arquivo.read_bytes()).hexdigest().lower() == entrada['SHA256'].lower()
            publicar_pacote(ancora, arquivo, entrada)
        # Confira novamente depois de downloads/uploads longos para não recuar versões.
        ultimo = asset_atual(ancora['id'], nome_feed)
        if ultimo:
            anterior = ler_json(ultimo['browser_download_url'])
            assert all(tuple(map(int, a['Version'].split('.'))) <= tuple(map(int, versao.split('.')))
                       for a in anterior['Assets']), 'Nunca recuar um canal publicado'
        arquivo_feed = pasta / nome_feed
        arquivo_feed.write_text(json.dumps(espelho, separators=(',', ':')), encoding='utf-8')
        # O índice só muda DEPOIS de o pacote completo estar disponível.
        gh('release', 'upload', ancora['tag_name'], str(arquivo_feed), '--repo', REPO, '--clobber')
        print(canal + ': ' + versao + ' disponível aos clientes antigos em ' + ancora['tag_name'], flush=True)

if __name__ == '__main__':
    main()
