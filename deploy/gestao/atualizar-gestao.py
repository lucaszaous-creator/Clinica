#!/usr/bin/env python3
"""Atualização da GESTÃO WEB na VPS (nov/2026): confere o pacote, troca o release e VOLTA se falhar.

Uso (root, chamado pelo /usr/local/sbin/clinica-deploy): atualizar-gestao.py pacote.tar.gz sha256
A primeira instalação é manual (docs/gestao-web.md); este script só atualiza um release que já existe.
"""
import hashlib, json, pathlib, re, shutil, subprocess, sys, tarfile, time

if not __debug__:
    raise RuntimeError('Não execute com python -O: as conferências são assert.')
assert len(sys.argv) == 3
pacote, sha = pathlib.Path(sys.argv[1]).resolve(), sys.argv[2]
assert re.fullmatch('[a-f0-9]{64}', sha) and hashlib.sha256(pacote.read_bytes()).hexdigest() == sha, 'SHA-256 do pacote não confere'
base = pathlib.Path('/opt/clinica-gestao')
servico = 'clinica-gestao'
socket = '/run/clinica-gestao/gestao.sock'
anterior = (base / 'current').resolve()
assert anterior.parent == base / 'releases' and anterior.is_dir(), 'Instale a gestão à mão uma vez antes (docs/gestao-web.md)'

def run(cmd):
    return subprocess.run(cmd, check=True, capture_output=True, text=True).stdout.strip()

def saude():
    # Sem cookie, a porta da gestão manda de volta ao login do portal (302): o processo subiu,
    # leu o catálogo, as chaves e a configuração. Até 60 s para o Blazor aquecer.
    for _ in range(30):
        r = subprocess.run(['curl', '-s', '-o', '/dev/null', '-w', '%{http_code}', '--unix-socket', socket,
                            '-H', 'Accept: text/html', 'http://localhost/gestao/'], capture_output=True, text=True)
        if r.stdout.strip() == '302':
            return True
        time.sleep(2)
    return False

with tarfile.open(pacote) as tar:
    nomes = tar.getnames()
    raiz = nomes[0].split('/')[0]
    assert re.fullmatch(r'gestao-web-[a-f0-9]{12}', raiz)
    for m in tar.getmembers():
        p = pathlib.PurePosixPath(m.name)
        assert not p.is_absolute() and '..' not in p.parts and (m.isfile() or m.isdir()) and p.parts[0] == raiz
    release = base / 'releases' / f'{raiz}-{time.strftime("%Y%m%d%H%M%S")}'
    tmp = base / 'releases' / f'.{release.name}.tmp'
    try:
        tar.extractall(tmp, filter='data')
    except TypeError:  # Python sem o filtro (anterior a 3.12): os membros já foram conferidos acima
        tar.extractall(tmp)
manifesto = json.loads((tmp / raiz / 'manifesto.json').read_text())
for rel, h in manifesto['arquivos'].items():
    assert hashlib.sha256((tmp / raiz / rel).read_bytes()).hexdigest() == h, f'arquivo alterado: {rel}'
(tmp / raiz).rename(release)
tmp.rmdir()
run(['chown', '-R', 'root:clinica-gestao', str(release)])
run(['chmod', '-R', 'u=rwX,g=rX,o=', str(release)])
(release / 'app' / 'Clinica.Gestao.Web').chmod(0o750)

def apontar(destino):
    link = base / 'current-novo'
    if link.is_symlink() or link.exists():
        link.unlink()
    link.symlink_to(destino)
    link.replace(base / 'current')

apontar(release)
run(['systemctl', 'restart', servico])
if not saude():
    apontar(anterior)
    run(['systemctl', 'restart', servico])
    ok = saude()
    print(json.dumps({'resultado': 'recuou', 'release': release.name, 'anterior': anterior.name, 'saude_apos_recuo': ok}))
    sys.exit(1)
# Guarda os cinco últimos releases (o atual e quatro para voltar à mão).
for velho in sorted((p for p in (base / 'releases').iterdir() if p.is_dir() and p != release and p != anterior),
                    key=lambda p: p.stat().st_mtime)[:-3]:
    shutil.rmtree(velho)
print(json.dumps({'resultado': 'publicado', 'release': release.name, 'anterior': anterior.name, 'commit': manifesto['commit']}))
