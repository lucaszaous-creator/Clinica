"""Atualização do posto: valida pacote, salva backup e recua o serviço se falhar.

Uso: python3 atualizar-posto.py hml|producao pacote.tar.gz sha256 release-anterior
O relatório de homologação do MESMO pacote é obrigatório antes da produção.
"""
import hashlib, json, os, pathlib, pwd, re, shutil, subprocess, sys, tarfile, time

assert os.geteuid() == 0 and len(sys.argv) == 5
ambiente, arquivo, sha, anterior = sys.argv[1:]
assert ambiente in ('hml', 'producao') and re.fullmatch('[a-f0-9]{64}', sha)
assert re.fullmatch('[a-zA-Z0-9._-]+', anterior)
servico = 'clinica-posto-hml' if ambiente == 'hml' else 'clinica-tablet'
role = servico
database = 'clinica_posto_hml_20260916' if ambiente == 'hml' else 'clinica'
base = pathlib.Path('/opt') / servico
conf = pathlib.Path('/etc') / servico
stage = pathlib.Path('/home/clinica-admin/tablet-stage')
pacote = pathlib.Path(arquivo).resolve()
assert pacote.is_relative_to(stage) and hashlib.sha256(pacote.read_bytes()).hexdigest() == sha
assert (base / 'current').resolve() == base / 'releases' / anterior
config = (conf / 'portal.env').read_bytes()
assert f'Database={database};' in config.decode()
assert f'/opt/{servico}/current/portal' in config.decode()
backup = pathlib.Path('/var/backups') / f'{servico}-continuidade-{time.strftime("%Y%m%d-%H%M%S")}'
backup.mkdir(mode=0o700)

def privado(path, value):
    with os.fdopen(os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), 'w') as f:
        f.write(value)

def run(args, **kwargs):
    r = subprocess.run(args, capture_output=True, text=True, **kwargs)
    if r.returncode:
        path = backup / f'falha-{time.time_ns()}.log'
        privado(path, r.stdout + '\n' + r.stderr)
        raise RuntimeError('Etapa falhou; diagnóstico protegido no backup.')
    return r.stdout.strip()

def sql(query):
    return run(['runuser','-u','postgres','--','psql','-p','45432','-d',database,'-XAt','-v','ON_ERROR_STOP=1'],input=query)

def ident(n):
    return '"' + n.replace('"','""') + '"'

def status(s):
    return run(['systemctl','show',s,'-p','MainPID','--value'])

def http(path):
    return run(['curl','--silent','--show-error','--max-time','4','--unix-socket',f'/run/{servico}/portal.sock',
                '--header','X-Forwarded-Proto: https','--write-out','\n%{http_code}','http://localhost'+path])

def saude():
    for _ in range(20):
        r=subprocess.run(['curl','--silent','--fail','--max-time','2','--unix-socket',f'/run/{servico}/portal.sock',
            '--header','X-Forwarded-Proto: https','http://localhost/health'],capture_output=True)
        if r.returncode==0:return True
        time.sleep(1)
    return False

assert sql(f"SELECT count(*) FROM pg_roles WHERE rolname='{role}' AND NOT (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls)") == '1'
assert sql(f"SELECT count(*) FROM pg_class WHERE relnamespace='public'::regnamespace AND relowner=(SELECT oid FROM pg_roles WHERE rolname='{role}')") == '0'
assert sql('SELECT count(*) FROM "__EFMigrationsHistory" WHERE "MigrationId"=\'20260916193849_PostoClinicoTablet\'') == '1'
with tarfile.open(pacote) as tar:
    membros = tar.getmembers()
    raizes = {pathlib.PurePosixPath(m.name).parts[0] for m in membros}
    assert len(raizes)==1
    nome = raizes.pop()
    assert re.fullmatch('tablet-continuidade-[a-f0-9]{12}-[a-f0-9]{12}',nome)
    release = base / 'releases' / nome
    assert not release.is_symlink()
    for m in membros:
        path = pathlib.PurePosixPath(m.name)
        assert not path.is_absolute() and '..' not in path.parts and (m.isfile() or m.isdir())
    manifest = json.loads(tar.extractfile(nome+'/manifesto.json').read().decode('utf-8-sig'))
    assert manifest['contrato'] in (2,3)
    migracao = manifest['migracao_nova']
    anteriores_migracao={
        '20260917185911_EnfermagemVinculadaEValidacaoInfusao':'20260917133639_TravaOpcionalDaAgenda',
        '20260923190924_EdicaoEnfermagemExclusiva':'20260922231000_HabilitacoesDoProfissional',
    }
    assert migracao is False or (manifest['contrato']==3 and migracao in anteriores_migracao)
    if migracao:
        ultima=sql('SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1')
        assert ultima in (anteriores_migracao[migracao],migracao),'Base mudou; conferir antes de migrar'
        assert 'migracao-enfermagem.sql' in manifest['arquivos']
    assert nome==f"tablet-continuidade-{manifest['backend'][:12]}-{manifest['interface'][:12]}"
    assert {m.name[len(nome)+1:] for m in membros if m.isfile()} == set(manifest['arquivos']) | {'manifesto.json'}
    for rel,digest in manifest['arquivos'].items():
        assert hashlib.sha256(tar.extractfile(nome+'/'+rel).read()).hexdigest()==digest
    if ambiente=='producao':
        aceite=json.loads((stage/(nome+'-hml.json')).read_text())
        assert aceite['saudavel'] and aceite['sha256']==sha
        teste=json.loads((stage/(nome+'-aceite-hml.json')).read_text())
        assert teste['aprovado'] and teste['sha256']==sha
    if release.exists():
        # Uma interrupção anterior aos grants pode deixar a extração pronta.
        # Reutilizar somente a cópia integral e idêntica do pacote aprovado.
        existentes = list(release.rglob('*'))
        assert not any(p.is_symlink() for p in existentes)
        assert {p.relative_to(release).as_posix() for p in existentes if p.is_file()} == set(manifest['arquivos']) | {'manifesto.json'}
        assert json.loads((release/'manifesto.json').read_text(encoding='utf-8-sig')) == manifest
        for rel,digest in manifest['arquivos'].items():
            assert hashlib.sha256((release/rel).read_bytes()).hexdigest()==digest
    else:
        tar.extractall(base/'releases') # todos os membros e hashes foram conferidos acima

for f in [release,*release.rglob('*')]:
    os.chown(f,0,0)
    f.chmod(0o755 if f.is_dir() else 0o644)
for rel,digest in manifest['arquivos'].items():
    f=release/rel
    assert hashlib.sha256(f.read_bytes()).hexdigest()==digest
    f.chmod(0o644)
(release/'app/Clinica.Assinaturas.Api').chmod(0o755)
privado(backup/'manifesto.json',json.dumps(manifest))
shutil.copyfile(conf/'portal.env',backup/'portal.env');(backup/'portal.env').chmod(0o600)
dump=backup/'banco.dump'
with open(dump,'xb') as f:
    dump.chmod(0o600)
    result=subprocess.run(['runuser','-u','postgres','--','pg_dump','-p','45432','-Fc','-d',database],stdout=f,stderr=subprocess.PIPE)
assert result.returncode==0 and dump.stat().st_size>0
protegidos=['postgresql@16-main','pgweb','cloudflared-site','clinica-safeid','clinica-safeid-hml','ssh','fail2ban',
            'clinica-tablet' if ambiente=='hml' else 'clinica-posto-hml']
antes={s:status(s) for s in protegidos}
conceder=[];revogar=[]

def grant(priv,table):
    if sql(f"SELECT has_table_privilege('{role}','\"{table}\"','{priv}')")=='t':return
    conceder.append(f'GRANT {priv} ON {ident(table)} TO {ident(role)};')
    revogar.append(f'REVOKE {priv} ON {ident(table)} FROM {ident(role)};')

inserir='Anamneses VersoesAnamnese MedidasClinicas AnexosPaciente ArquivosAnexoPaciente ResultadosExame EvolucoesEnfermagem ModelosDocumento'.split()
grant('SELECT','VersoesAnamnese')
for tabela in inserir:
    grant('INSERT',tabela)
    # Tabelas 1:1, como ArquivosAnexoPaciente, usam a chave do registro pai.
    # Consultar apenas colunas existentes evita erro em tabelas sem Id próprio.
    sequence=sql(f"SELECT COALESCE(pg_get_serial_sequence('\"{tabela}\"',attname),'') FROM pg_attribute WHERE attrelid='\"{tabela}\"'::regclass AND attname='Id' AND NOT attisdropped")
    if sequence:
        for priv in ('USAGE','SELECT'):
            if sql(f"SELECT has_sequence_privilege('{role}','{sequence}','{priv}')")!='t':
                conceder.append(f'GRANT {priv} ON SEQUENCE {sequence} TO {ident(role)};')
                revogar.append(f'REVOKE {priv} ON SEQUENCE {sequence} FROM {ident(role)};')
for tabela in ('Anamneses','MedidasClinicas','ProblemasPaciente'):grant('UPDATE',tabela)
if sql(f"SELECT has_column_privilege('{role}','\"EvolucoesEnfermagem\"','FaseAtendimento','UPDATE')")!='t':
    conceder.append(f'GRANT UPDATE ("FaseAtendimento") ON "EvolucoesEnfermagem" TO {ident(role)};')
    revogar.append(f'REVOKE UPDATE ("FaseAtendimento") ON "EvolucoesEnfermagem" FROM {ident(role)};')
if migracao and sql(f"SELECT has_column_privilege('{role}','\"EvolucoesEnfermagem\"','AgendamentoId','UPDATE')")!='t':
    conceder.append(f'GRANT UPDATE ("AgendamentoId") ON "EvolucoesEnfermagem" TO {ident(role)};')
    revogar.append(f'REVOKE UPDATE ("AgendamentoId") ON "EvolucoesEnfermagem" FROM {ident(role)};')
for coluna in ('AtendimentoId','CodigoFaturamentoId','Tipo','Status'):
    if sql(f"SELECT has_column_privilege('{role}','\"Lancamentos\"','{coluna}','SELECT')")!='t':
        conceder.append(f'GRANT SELECT ({ident(coluna)}) ON "Lancamentos" TO {ident(role)};')
        revogar.append(f'REVOKE SELECT ({ident(coluna)}) ON "Lancamentos" FROM {ident(role)};')
privado(backup/'permissoes-aplicar.sql','\n'.join(conceder))
privado(backup/'permissoes-recuar.sql','\n'.join(revogar))
privado(backup/'release-anterior.txt',anterior)
if migracao=='20260923190924_EdicaoEnfermagemExclusiva':
    conceder.append(f'GRANT SELECT, INSERT, UPDATE, DELETE ON "EdicoesEnfermagemTablet" TO {ident(role)};')
    revogar.append(f'REVOKE SELECT, INSERT, UPDATE, DELETE ON "EdicoesEnfermagemTablet" FROM {ident(role)};')
elif sql('SELECT to_regclass(\'"EdicoesEnfermagemTablet"\') IS NOT NULL')=='t':
    for priv in ('SELECT','INSERT','UPDATE','DELETE'):grant(priv,'EdicoesEnfermagemTablet')
aplicado=False;mudou=False
try:
    if migracao:
        # Migration aditiva e idempotente. O recuo preserva as colunas e todos os registros.
        schema=(release/'migracao-enfermagem.sql').read_text(encoding='utf-8-sig')
        assert migracao in schema and not re.search(r'\b(?:DROP|TRUNCATE)\s|\bDELETE\s+FROM\b',schema,re.I)
        sql("SET lock_timeout='5s'; SET statement_timeout='60s';\n"+schema)
        assert sql(f'SELECT count(*) FROM "__EFMigrationsHistory" WHERE "MigrationId"=\'{migracao}\'')=='1'
    sql('BEGIN;\n'+'\n'.join(conceder)+'\nCOMMIT;');aplicado=True
    link=base/'current-novo'
    assert not link.exists() and not link.is_symlink()
    link.symlink_to(release);os.replace(link,base/'current');mudou=True
    run(['systemctl','restart',servico]);assert saude()
    assert http('/api/posto/pendencias').endswith('\n401')
    assert http('/api/posto/modelos-documento').endswith('\n401')
    pagina=http('/profissional/');assert pagina.endswith('\n200') and 'nav-pendencias' in pagina
    assert (conf/'portal.env').read_bytes()==config
    assert {s:status(s) for s in protegidos}==antes
    relatorio={'ambiente':ambiente,'sha256':sha,'release':str(release),'anterior':anterior,'backup':str(backup),
        'saudavel':True,'servicos_preservados':True,'migracao_nova':migracao,'concessoes':len(conceder)}
    destino=stage/(nome+('-hml.json' if ambiente=='hml' else '-producao.json'))
    privado(destino,json.dumps(relatorio,ensure_ascii=False,indent=2))
    usuario=pwd.getpwnam('clinica-admin');os.chown(destino,usuario.pw_uid,usuario.pw_gid)
    print('Atualização saudável. Relatório: '+str(destino))
except Exception:
    if mudou:
        link=base/'current-recuo';assert not link.exists() and not link.is_symlink()
        link.symlink_to(base/'releases'/anterior);os.replace(link,base/'current')
    if aplicado:sql('BEGIN;\n'+'\n'.join(revogar)+'\nCOMMIT;')
    if mudou:run(['systemctl','restart',servico]);assert saude()
    raise
