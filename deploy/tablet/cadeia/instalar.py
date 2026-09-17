"""Configura as CAs públicas no perfil de um serviço, sem alterar a confiança global."""
import hashlib
import json
import os
import pathlib
import pwd
import re
import subprocess
import sys
import tarfile
import time

assert os.geteuid() == 0 and len(sys.argv) == 3
assert sys.argv[1] in ('homologacao', 'producao')
assert re.fullmatch('[a-f0-9]{64}', sys.argv[2])
service = 'clinica-posto-hml' if sys.argv[1] == 'homologacao' else 'clinica-tablet'
other = 'clinica-tablet' if service == 'clinica-posto-hml' else 'clinica-posto-hml'
archive = pathlib.Path('/home/clinica-admin/tablet-stage/cadeia-safeid-v5.tar.gz')
assert hashlib.sha256(archive.read_bytes()).hexdigest() == sys.argv[2]
target = pathlib.Path('/opt', service, 'cadeia-safeid-v5-20260916')
assert target.resolve() == target
existed = target.exists()
target.mkdir(mode=0o755, exist_ok=True)
with tarfile.open(archive) as tar:
    for member in tar.getmembers():
        assert (target / member.name).resolve().is_relative_to(target)
        assert member.isfile() or member.isdir()
        if existed and member.isfile():
            assert hashlib.sha256((target / member.name).read_bytes()).digest() == hashlib.sha256(tar.extractfile(member).read()).digest()
    if not existed:
        tar.extractall(target, filter='data')
for path in target.rglob('*'):
    os.chown(path, 0, 0)
    os.chmod(path, 0o755 if path.is_dir() or path.name == 'InstalarCadeia' else 0o644)

def run(args, **kw):
    r = subprocess.run(args, capture_output=True, text=True, **kw)
    if r.returncode:
        raise RuntimeError('Configuracao da cadeia interrompida: ' + r.stdout + r.stderr)
    return r.stdout.strip()

other_pid = run(['systemctl', 'show', other, '-p', 'MainPID', '--value'])
home = pwd.getpwnam(service).pw_dir
assert home == '/var/lib/' + service
pid = run(['systemctl', 'show', service, '-p', 'MainPID', '--value'])
environment = dict(v.split(b'=', 1) for v in pathlib.Path('/proc', pid, 'environ').read_bytes().split(b'\0') if b'=' in v)
assert environment.get(b'HOME', home.encode()) == home.encode()
tool = str(target / 'InstalarCadeia')
certs = str(target / 'certificados')
run([tool, 'validar', certs])
env = {**os.environ, 'HOME': home}
result = run(['runuser', '-u', service, '--', tool, 'instalar', certs], env=env)
verified = run(['runuser', '-u', service, '--', tool, 'verificar', certs], env=env)
run(['systemctl', 'restart', service])
healthy = False
for _ in range(30):
    r = subprocess.run(['curl', '--silent', '--fail', '--unix-socket', '/run/' + service + '/portal.sock',
                        '-H', 'X-Forwarded-Proto: https', 'http://localhost/health'], capture_output=True)
    if r.returncode == 0:
        healthy = True
        break
    time.sleep(1)
assert healthy
assert other_pid == run(['systemctl', 'show', other, '-p', 'MainPID', '--value'])
report = {'ambiente': sys.argv[1], 'cadeia': 'ICP-Brasil v5 / RFB v4 / Safeweb RFB v5',
          'resultado': result, 'verificacao': verified, 'servico_saudavel': healthy,
          'outro_ambiente_preservado': True, 'pacote_sha256': sys.argv[2]}
pathlib.Path('/home/clinica-admin/tablet-stage', 'cadeia-' + sys.argv[1] + '.json').write_text(json.dumps(report))
print(json.dumps(report))
