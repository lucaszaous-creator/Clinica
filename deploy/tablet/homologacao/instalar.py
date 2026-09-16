"""Instalação isolada, como root, com pacote e SHA-256 conferidos antes de criar serviços."""
import hashlib
import json
import os
import pathlib
import pwd
import re
import secrets
import shlex
import subprocess
import sys
import tarfile
import time

BASE = pathlib.Path('/opt/clinica-posto-hml')
CONF = pathlib.Path('/etc/clinica-posto-hml')
DB = 'clinica_posto_hml_20260916'
ROLE = 'clinica-posto-hml'
assert os.geteuid() == 0 and len(sys.argv) == 3
pacote = pathlib.Path(sys.argv[1]).resolve()
assert pacote.is_relative_to('/home/clinica-admin/tablet-stage')
assert re.fullmatch('[a-f0-9]{64}', sys.argv[2])
assert hashlib.sha256(pacote.read_bytes()).hexdigest() == sys.argv[2]
assert CONF.joinpath('safeid.env').stat().st_mode & 0o777 == 0o600

def run(args, **kw):
    result = subprocess.run(args, capture_output=True, text=True, **kw)
    if result.returncode:
        diagnostic = CONF/'ultima-falha.log'
        with os.fdopen(os.open(diagnostic,os.O_WRONLY|os.O_CREAT|os.O_TRUNC,0o600),'w') as file:
            file.write(result.stdout+'\n'+result.stderr)
        raise RuntimeError('Etapa interrompida; diagnóstico disponível apenas ao administrador em '+str(diagnostic))
    return result.stdout.strip()

def sql(database, text):
    return run(['runuser','-u','postgres','--','psql','-p','45432','-d',database,'-XAt','-v','ON_ERROR_STOP=1'], input=text)

def write_private(path, value):
    with os.fdopen(os.open(path,os.O_WRONLY|os.O_CREAT|os.O_EXCL,0o600),'w') as f:
        f.write(value)

def pid(service):
    return run(['systemctl','show',service,'-p','MainPID','--value'])

antes = {s:pid(s) for s in ('clinica-tablet','cloudflared-site')}
production = pathlib.Path('/opt/clinica-tablet/current').resolve()
source_config = pathlib.Path('/etc/clinica-tablet/portal.env').read_text()
origem = re.search(r'Database=([^;"\n]+)', source_config).group(1)
assert origem != DB
BASE.mkdir(mode=0o755, exist_ok=True)
releases = BASE/'releases'; releases.mkdir(exist_ok=True)
with tarfile.open(pacote) as tar:
    members = tar.getmembers()
    roots = {m.name.split('/')[0] for m in members}
    assert len(roots) == 1
    name = roots.pop()
    assert re.fullmatch(r'tablet-release-[a-f0-9]{12}-[a-f0-9]{12}', name)
    for m in members:
        target = (releases/m.name).resolve()
        assert target.is_relative_to(releases) and not m.issym() and not m.islnk() and (m.isfile() or m.isdir())
    release = releases/name
    if not release.exists():
        tar.extractall(releases, filter='data')
manifest = json.loads((release/'manifesto.json').read_text(encoding='utf-8-sig'))
for file, sha in manifest['arquivos'].items():
    target = (release/file).resolve()
    assert target.is_relative_to(release) and hashlib.sha256(target.read_bytes()).hexdigest() == sha
for root, dirs, files in os.walk(release):
    os.chown(root,0,0);os.chmod(root,0o755)
    for file in files:
        target = pathlib.Path(root)/file
        os.chown(target,0,0);os.chmod(target,0o755 if file in ('Clinica.Assinaturas.Api','PrepararHomologacao','createdump') else 0o644)
try:
    pwd.getpwnam(ROLE)
except KeyError:
    run(['useradd','--system','--home-dir','/var/lib/clinica-posto-hml','--shell','/usr/sbin/nologin',ROLE])
if not sql('postgres',f"SELECT 1 FROM pg_roles WHERE rolname='{ROLE}';"):
    sql('postgres',f'CREATE ROLE "{ROLE}" LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;')
assert sql('postgres',f"SELECT count(*) FROM pg_roles WHERE rolname='{ROLE}' AND rolcanlogin AND NOT (rolsuper OR rolcreatedb OR rolcreaterole OR rolinherit OR rolreplication OR rolbypassrls);") == '1'
assert sql('postgres',f"SELECT count(*) FROM pg_auth_members WHERE member=(SELECT oid FROM pg_roles WHERE rolname='{ROLE}');") == '0'
if not sql('postgres',f"SELECT 1 FROM pg_database WHERE datname='{DB}';"):
    sql('postgres',f'CREATE DATABASE "{DB}" OWNER postgres;')
marker = CONF/'base-preparada.json'
if not marker.exists():
    # Só contas clínicas e cadastro profissional; o preparador não consulta prontuários de origem.
    result = run(['runuser','-u','postgres','--',str(release/'preparar/PrepararHomologacao'),origem,DB],env={**os.environ,'TZ':'America/Sao_Paulo'})
    write_private(marker,json.dumps({'banco':DB,'backend':manifest['backend'],'resultado':result}))

# Lista de operações necessária aos fluxos do posto e da coleta; sem DDL ou DELETE do prontuário.
leitura = '''Usuarios Profissionais Pacientes Agendamentos Atendimentos Codigos Parametros Configuracoes Convenios Modalidades Especialidades Salas BloqueiosAgenda
Evolucoes VersoesEvolucao ModelosEvolucao Anamneses ValoresCampoPersonalizado CamposPersonalizadosProntuario
AnexosProntuario Consentimentos MedidasClinicas ResultadosExame ArquivosResultadoExame AnexosPaciente ArquivosAnexoPaciente ProblemasPaciente AvaliacoesClinicas RespostasAvaliacao
MapasCorporais PontosMapa ProtocolosCorporais PontosProtocolo DocumentosClinicos ItensDocumento ModelosDocumento ItensModelo PrescricoesInternas ItensPrescricaoInterna ChecagensPrescricao
EvolucoesEnfermagem DiagnosticosEnfermagem CuidadosEnfermagem ChecagensCuidado AssinaturasDocumento ArquivosAssinados TracosAssinatura ExigenciasTermo
Autorizacoes SessoesTablet ColetasTablet ViasAssinadasPaciente OperacoesClinicasTablet EtapasFechamentoSessao PacotesPaciente PacotesCatalogo ConsumosPacote PrecosConvenio PrecosParticular'''.split()
inserir = '''Auditoria Agendamentos Atendimentos Codigos Evolucoes VersoesEvolucao MapasCorporais PontosMapa ProtocolosCorporais PontosProtocolo
DocumentosClinicos ItensDocumento PrescricoesInternas ItensPrescricaoInterna ChecagensPrescricao ProblemasPaciente AssinaturasDocumento ArquivosAssinados AnexosProntuario
Consentimentos TracosAssinatura SessoesTablet ColetasTablet ViasAssinadasPaciente OperacoesClinicasTablet EtapasFechamentoSessao'''.split()
atualizar = '''Agendamentos Atendimentos Codigos Evolucoes MapasCorporais DocumentosClinicos PrescricoesInternas ItensPrescricaoInterna SessoesTablet ColetasTablet Autorizacoes'''.split()
commands = [f'REVOKE ALL ON DATABASE "{DB}" FROM PUBLIC;',f'GRANT CONNECT ON DATABASE "{DB}" TO "{ROLE}";',
            'REVOKE CREATE ON SCHEMA public FROM PUBLIC;',f'GRANT USAGE ON SCHEMA public TO "{ROLE}";']
for privilege, tables in [('SELECT',leitura),('INSERT',inserir),('UPDATE',atualizar),('DELETE',['PontosMapa','PontosProtocolo','ItensDocumento'])]:
    commands.append(f'GRANT {privilege} ON '+','.join('"'+t+'"' for t in tables)+f' TO "{ROLE}";')
commands.append(f'GRANT UPDATE ("UltimoAcessoEm","TentativasFalhas","BloqueadoAte") ON "Usuarios" TO "{ROLE}";')
commands.append(f'GRANT SELECT ("Id") ON "Auditoria" TO "{ROLE}";') # INSERT RETURNING do EF, sem leitura do conteúdo de auditoria.
sql(DB,'\n'.join(commands))
for table in inserir:
    sequence = sql(DB,f"SELECT pg_get_serial_sequence('\"{table}\"','Id');") if table not in ('EtapasFechamentoSessao','ViasAssinadasPaciente') else ''
    if sequence:
        sql(DB,f'GRANT USAGE, SELECT ON SEQUENCE {sequence} TO "{ROLE}";')
# Falhar se o papel dedicado possuir qualquer leitura/escrita em tabela da produção.
assert sql(origem,f"SELECT count(*) FROM pg_tables WHERE schemaname='public' AND (has_table_privilege('{ROLE}',quote_ident(schemaname)||'.'||quote_ident(tablename),'SELECT,INSERT,UPDATE,DELETE'));") == '0'
if not (CONF/'base-verificada.json').exists():
    result = run(['runuser','-u',ROLE,'--',str(release/'preparar/PrepararHomologacao'),'verificar',DB],env={**os.environ,'TZ':'America/Sao_Paulo'})
    write_private(CONF/'base-verificada.json',json.dumps({'backend':manifest['backend'],'resultado':result}))
if not (CONF/'portal.env').exists():
    code = secrets.token_urlsafe(36)
    write_private(CONF/'portal.env',f'''ASPNETCORE_ENVIRONMENT=Production
TZ=America/Sao_Paulo
Portal__Demo=false
Portal__Homologacao=true
Portal__Habilitado=true
Portal__AtendimentoHabilitado=true
Portal__SafeId__Habilitado=false
Portal__Socket=/run/clinica-posto-hml/portal.sock
Portal__Interface=/opt/clinica-posto-hml/current/portal
Portal__DiretorioChaves=/var/lib/clinica-posto-hml/chaves
Portal__CodigoTablet={code}
Portal__Modelos__0=1
Portal__Modelos__1=2
HTTPS_PROXY=http://127.0.0.1:18743
NO_PROXY=localhost,127.0.0.1
ConnectionStrings__Clinica="Host=/var/run/postgresql;Port=45432;Database={DB};Username={ROLE};Timeout=10;Maximum Pool Size=5;Include Error Detail=false"
''')
current=BASE/'current'
if current.is_symlink():
    assert current.resolve()==release
else:
    current.symlink_to(release, target_is_directory=True)
for unit in ('clinica-safeid-hml.service','clinica-posto-hml.service'):
    pathlib.Path('/etc/systemd/system',unit).write_bytes((release/'configuracao/homologacao'/unit).read_bytes())
run(['systemctl','daemon-reload'])
run(['systemctl','enable','--now','clinica-posto-hml.service'])
for _ in range(30):
    health=subprocess.run(['curl','--silent','--fail','--unix-socket','/run/clinica-posto-hml/portal.sock','--header','X-Forwarded-Proto: https','http://localhost/health'],capture_output=True,text=True)
    if health.returncode==0:break
    time.sleep(1)
assert health.returncode==0
assert pathlib.Path('/opt/clinica-tablet/current').resolve()==production
assert antes=={s:pid(s) for s in antes}
report={'estado':'HOMOLOGACAO_PRIVADA_INSTALADA','banco':DB,'backend':manifest['backend'],'interface':manifest['interface'],'producao_preservada':True,'safeid_ativo':False}
pathlib.Path('/home/clinica-admin/tablet-stage/resultado-posto.json').write_text(json.dumps(report))
print(json.dumps(report))
