"""Atualização do posto: valida pacote, salva backup e recua o serviço se falhar.

Uso: python3 atualizar-posto.py hml|producao pacote.tar.gz sha256 release-anterior [--pular-hml motivo]
Produção exige relatórios HML do mesmo pacote, salvo dispensa explícita registrada.
"""
import hashlib, json, os, pathlib, pwd, re, shutil, stat, subprocess, sys, tarfile, time
from regras_publicacao import (
    incluir_flag_false_se_ausente,
    verificar_criptografia_desativada,
    verificar_flag_desativada_no_processo,
)

# Python -O remove todos os assert usados como guardas neste atualizador. Não
# executar uma troca de produção com validações compiladas fora.
if not __debug__:
    raise RuntimeError('Atualizador não pode ser executado com python -O ou PYTHONOPTIMIZE.')

assert os.geteuid() == 0 and len(sys.argv) in (5, 7)
ambiente, arquivo, sha, anterior = sys.argv[1:5]
pular_hml = len(sys.argv) == 7 and sys.argv[5] == '--pular-hml' and bool(sys.argv[6].strip())
motivo_dispensa_hml = sys.argv[6].strip() if pular_hml else None
assert len(sys.argv) == 5 or (ambiente == 'producao' and pular_hml)
if motivo_dispensa_hml is not None and (len(motivo_dispensa_hml) > 240 or any(ord(c) < 32 for c in motivo_dispensa_hml)):
    raise ValueError('O motivo da dispensa deve ter até 240 caracteres e não pode conter controles.')
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
config_publicacao = config
assert f'Database={database};' in config.decode()
assert f'/opt/{servico}/current/portal' in config.decode()
backup = pathlib.Path('/var/backups') / f'{servico}-continuidade-{time.strftime("%Y%m%d-%H%M%S")}'
backup.mkdir(mode=0o700)

def privado(path, value):
    descritor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    identidade = os.fstat(descritor)
    try:
        with os.fdopen(descritor, 'w') as f:
            f.write(value)
            f.flush()
            os.fsync(f.fileno())
    except Exception:
        try:
            atual = os.lstat(path)
            if (atual.st_dev, atual.st_ino) == (identidade.st_dev, identidade.st_ino):
                path.unlink()
        except FileNotFoundError:
            pass
        raise
    return identidade.st_dev, identidade.st_ino

def remover_se_igual(path, identidade):
    if identidade is None:
        return
    try:
        atual = os.lstat(path)
    except FileNotFoundError:
        return
    if (atual.st_dev, atual.st_ino) == identidade:
        path.unlink()

def run(args, **kwargs):
    r = subprocess.run(args, capture_output=True, text=True, **kwargs)
    if r.returncode:
        path = backup / f'falha-{time.time_ns()}.log'
        privado(path, r.stdout + '\n' + r.stderr)
        raise RuntimeError('Etapa falhou; diagnóstico protegido no backup.')
    return r.stdout.strip()

def sql(query):
    return run(['runuser','-u','postgres','--','psql','-p','45432','-d',database,'-XAt','-v','ON_ERROR_STOP=1'],input=query)

def status(s):
    return run(['systemctl','show',s,'-p','MainPID','--value'])

consulta_credenciais_cifradas='''SELECT count(*) FROM "Configuracoes"
    WHERE "Valor" LIKE 'enc:%' '''

def verificar_sem_credenciais_cifradas():
    if sql(consulta_credenciais_cifradas)!='0':
        raise RuntimeError('A base contém credenciais protegidas; dispensa recusada sem exibir valores.')

if ambiente=='producao' and pular_hml:
    # Registrar a tentativa antes de qualquer consulta de elegibilidade, migration,
    # grant, troca de symlink ou reinício. O diretório/arquivo são privados.
    ator=os.environ.get('SUDO_USER') or pwd.getpwuid(os.getuid()).pw_name
    tentativa={'ambiente':ambiente,'status':'iniciada','sha256':sha,'anterior':anterior,
        'motivo_dispensa_hml':motivo_dispensa_hml,'ator_unix':ator,
        'registrada_em':time.strftime('%Y-%m-%dT%H:%M:%S%z')}
    privado(backup/'dispensa-hml-tentativa.json',json.dumps(tentativa,ensure_ascii=False,indent=2))

    # O EnvironmentFile do serviço vem depois do ambiente global do systemd. Validamos
    # em memória o false explícito; só gravamos no portal.env após backup e pré-voo.
    config_publicacao=incluir_flag_false_se_ausente(config)

    # Esta via de publicação mantém credenciais no formato legado para não exigir
    # chave ou atualização de desktops. Examinar todas as fontes da unidade systemd
    # evita divergências de aspas, valores duplicados e arquivos de override.
    unidade=subprocess.run(['systemctl','cat',servico],capture_output=True,text=True)
    if unidade.returncode:
        raise RuntimeError('Não foi possível verificar a configuração efetiva do serviço.')
    if run(['systemctl','show',servico,'-p','NeedDaemonReload','--value']).lower()!='no':
        raise RuntimeError('A unidade systemd foi alterada sem daemon-reload; dispensa recusada.')
    if run(['systemctl','is-active',servico])!='active':
        raise RuntimeError('O serviço de produção precisa estar ativo para validar o processo atual.')
    pid_atual=status(servico)
    if not pid_atual.isdigit() or int(pid_atual)<=1:
        raise RuntimeError('Não foi possível validar o processo atual do serviço de produção.')
    try:
        verificar_flag_desativada_no_processo(
            (pathlib.Path('/proc')/pid_atual/'environ').read_bytes())
    except (OSError,ValueError) as erro:
        raise RuntimeError('O processo atual não comprova a cifra desativada; dispensa recusada.') from erro
    try:
        verificar_criptografia_desativada(
            conf/'portal.env', unidade.stdout, config_publicacao.decode('utf-8'))
    except (OSError,UnicodeError,ValueError) as erro:
        raise RuntimeError('A cifra de credenciais não está comprovadamente desativada; dispensa recusada.') from erro
    verificar_sem_credenciais_cifradas()

def ident(n):
    return '"' + n.replace('"','""') + '"'

def http(path):
    return run(['curl','--silent','--show-error','--max-time','4','--unix-socket',f'/run/{servico}/portal.sock',
                '--header','X-Forwarded-Proto: https','--header','CF-Connecting-IP: 127.0.0.1',
                '--write-out','\n%{http_code}','http://localhost'+path])

def saude():
    for _ in range(20):
        r=subprocess.run(['curl','--silent','--fail','--max-time','2','--unix-socket',f'/run/{servico}/portal.sock',
            '--header','X-Forwarded-Proto: https','http://localhost/health'],capture_output=True)
        if r.returncode==0:return True
        time.sleep(1)
    return False

def substituir_portal_env(esperado,novo):
    caminho=conf/'portal.env'
    estado=os.lstat(caminho)
    if not stat.S_ISREG(estado.st_mode) or estado.st_uid!=0 or caminho.read_bytes()!=esperado:
        raise RuntimeError('portal.env mudou desde o pré-voo; não será sobrescrito.')
    temporario=conf/('.portal.env-publicacao-'+str(time.time_ns()))
    flags=os.O_WRONLY|os.O_CREAT|os.O_EXCL
    if hasattr(os,'O_NOFOLLOW'):flags|=os.O_NOFOLLOW
    descritor=os.open(temporario,flags,0o600)
    try:
        os.fchown(descritor,estado.st_uid,estado.st_gid)
        os.fchmod(descritor,0o600)
        with os.fdopen(descritor,'wb',closefd=False) as f:
            f.write(novo);f.flush();os.fsync(f.fileno())
        os.fchmod(descritor,stat.S_IMODE(estado.st_mode))
        os.fsync(descritor)
    except Exception:
        temporario.unlink(missing_ok=True)
        raise
    finally:
        os.close(descritor)
    try:
        os.replace(temporario,caminho)
    except Exception:
        temporario.unlink(missing_ok=True)
        raise
    portal_env_estado['alterado']=True
    pasta_fd=os.open(conf,os.O_RDONLY|getattr(os,'O_DIRECTORY',0))
    try:
        try:os.fsync(pasta_fd)
        except OSError:pass
    finally:os.close(pasta_fd)

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
        '20261002162759_GruposInfusaoECatalogoMedicamentos':'20261002140919_ContinuidadeSemAssinatura',
        '20261002140919_ContinuidadeSemAssinatura':'20261001180000_AuditoriaPreservaDetalheCompleto',
        '20261001180000_AuditoriaPreservaDetalheCompleto':'20261001140452_CertificadosA1Profissionais',
        '20261001140452_CertificadosA1Profissionais':'20260928211121_ChecagemNaoExecutavel',
        '20260928211121_ChecagemNaoExecutavel':'20260926120000_DevolucaoInfusaoExterna',
        '20260917185911_EnfermagemVinculadaEValidacaoInfusao':'20260917133639_TravaOpcionalDaAgenda',
        '20260923190924_EdicaoEnfermagemExclusiva':'20260922231000_HabilitacoesDoProfissional',
        '20260926120000_DevolucaoInfusaoExterna':'20260925170000_DataRealizacaoInfusao',
        '20260928123100_AcompanhamentoPacientesBsvRecall':'20260926120000_DevolucaoInfusaoExterna',
    }
    arquivos_migracao={
        '20261002162759_GruposInfusaoECatalogoMedicamentos':'migracao-a1.sql',
        '20261002140919_ContinuidadeSemAssinatura':'migracao-a1.sql',
        '20261001180000_AuditoriaPreservaDetalheCompleto':'migracao-a1.sql',
        '20261001140452_CertificadosA1Profissionais':'migracao-a1.sql',
        '20260928211121_ChecagemNaoExecutavel':'migracao-fluxos-enfermagem.sql',
        '20260917185911_EnfermagemVinculadaEValidacaoInfusao':'migracao-enfermagem.sql',
        '20260923190924_EdicaoEnfermagemExclusiva':'migracao-enfermagem.sql',
        '20260926120000_DevolucaoInfusaoExterna':'migracao-infusao.sql',
        '20260928123100_AcompanhamentoPacientesBsvRecall':'migracao-acompanhamento.sql',
    }
    assert migracao is False or (manifest['contrato']==3 and migracao in anteriores_migracao)
    if migracao:
        ultima=sql('SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1')
        permitidas={anteriores_migracao[migracao],migracao}
        if migracao in ('20261001140452_CertificadosA1Profissionais','20261001180000_AuditoriaPreservaDetalheCompleto','20261002140919_ContinuidadeSemAssinatura'):
            # Identificador imutável já aplicado: preservar o histórico, sem reativar o provedor.
            permitidas.update({'20261001140452_CertificadosA1Profissionais','20261001180000_AuditoriaPreservaDetalheCompleto','20260928211121_ChecagemNaoExecutavel','20261001015315_ConclusaoAutomaticaSafeId'})
        if migracao=='20260928211121_ChecagemNaoExecutavel':
            permitidas.update({'20260928123100_AcompanhamentoPacientesBsvRecall','20260928210520_DiluicaoUnicaInfusao'})
        assert ultima in permitidas,'Base mudou; conferir antes de migrar'
        assert arquivos_migracao[migracao] in manifest['arquivos']
    assert nome==f"tablet-continuidade-{manifest['backend'][:12]}-{manifest['interface'][:12]}"
    assert {m.name[len(nome)+1:] for m in membros if m.isfile()} == set(manifest['arquivos']) | {'manifesto.json'}
    for rel,digest in manifest['arquivos'].items():
        assert hashlib.sha256(tar.extractfile(nome+'/'+rel).read()).hexdigest()==digest
    if ambiente=='producao' and not pular_hml:
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
protegidos=['postgresql@16-main','pgweb','cloudflared-site','ssh','fail2ban',
            'clinica-tablet' if ambiente=='hml' else 'clinica-posto-hml']
antes={s:status(s) for s in protegidos}
conceder=[];revogar=[]

def grant(priv,table,destinatario=role):
    if sql(f"SELECT has_table_privilege('{destinatario}','\"{table}\"','{priv}')")=='t':return
    conceder.append(f'GRANT {priv} ON {ident(table)} TO {ident(destinatario)};')
    revogar.append(f'REVOKE {priv} ON {ident(table)} FROM {ident(destinatario)};')

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
if sql('SELECT to_regclass(\'"EdicoesEnfermagemTablet"\') IS NOT NULL')=='t':
    for priv in ('SELECT','INSERT','UPDATE','DELETE'):grant(priv,'EdicoesEnfermagemTablet')
elif migracao=='20260923190924_EdicaoEnfermagemExclusiva':
    conceder.append(f'GRANT SELECT, INSERT, UPDATE, DELETE ON "EdicoesEnfermagemTablet" TO {ident(role)};')
    revogar.append(f'REVOKE SELECT, INSERT, UPDATE, DELETE ON "EdicoesEnfermagemTablet" FROM {ident(role)};')
if sql('SELECT to_regclass(\'"CertificadosA1"\') IS NOT NULL')=='t':
    for priv in ('SELECT','INSERT','UPDATE','DELETE'):grant(priv,'CertificadosA1')
elif migracao in ('20261001140452_CertificadosA1Profissionais','20261001180000_AuditoriaPreservaDetalheCompleto','20261002140919_ContinuidadeSemAssinatura'):
    conceder.append(f'GRANT SELECT, INSERT, UPDATE, DELETE ON "CertificadosA1" TO {ident(role)};')
    revogar.append(f'REVOKE SELECT, INSERT, UPDATE, DELETE ON "CertificadosA1" FROM {ident(role)};')
if migracao == '20261002162759_GruposInfusaoECatalogoMedicamentos':
    desktop=sql('SELECT pg_get_userbyid(relowner) FROM pg_class WHERE oid=\'"Configuracoes"\'::regclass')
    assert re.fullmatch(r'[a-zA-Z0-9_-]+',desktop) and desktop != role
    existe=sql('SELECT to_regclass(\'"MedicamentosCadastro"\') IS NOT NULL')=='t'
    # Cadastro é administrado pelo desktop; a API usa os medicamentos já prescritos.
    for priv in ('SELECT','INSERT','UPDATE'):
        if existe:grant(priv,'MedicamentosCadastro',desktop)
        else:
            conceder.append(f'GRANT {priv} ON "MedicamentosCadastro" TO {ident(desktop)};')
            revogar.append(f'REVOKE {priv} ON "MedicamentosCadastro" FROM {ident(desktop)};')
if migracao in ('20260928123100_AcompanhamentoPacientesBsvRecall','20260928211121_ChecagemNaoExecutavel'):
    # A migration é aplicada como postgres. O dono das tabelas do desktop também
    # precisa gravar o recall; conceder só ao portal deixa o Windows sem INSERT.
    desktop=sql('SELECT pg_get_userbyid(relowner) FROM pg_class WHERE oid=\'"Configuracoes"\'::regclass')
    assert re.fullmatch(r'[a-zA-Z0-9_-]+',desktop) and desktop != role
    for tabela,privilegios in {'Acompanhamentos':('SELECT','INSERT','UPDATE'),
            'ContatosAcompanhamento':('SELECT','INSERT'),'MotivosAcompanhamento':('SELECT','INSERT')}.items():
        existe=sql(f"SELECT to_regclass('{ident(tabela)}') IS NOT NULL")=='t'
        for priv in privilegios:
            if existe:grant(priv,tabela,desktop)
            else:
                conceder.append(f'GRANT {priv} ON {ident(tabela)} TO {ident(desktop)};')
                revogar.append(f'REVOKE {priv} ON {ident(tabela)} FROM {ident(desktop)};')
        sequence=ident(tabela+'_Id_seq')
        for priv in ('USAGE','SELECT'):
            if not existe or sql(f"SELECT has_sequence_privilege('{desktop}','{sequence}','{priv}')")!='t':
                conceder.append(f'GRANT {priv} ON SEQUENCE {sequence} TO {ident(desktop)};')
                revogar.append(f'REVOKE {priv} ON SEQUENCE {sequence} FROM {ident(desktop)};')
    # O portal somente indica e consulta. Alteração do recall fica nos desktops.
    for tabela in ('Acompanhamentos','ContatosAcompanhamento','MotivosAcompanhamento'):
        existe=sql(f"SELECT to_regclass('{ident(tabela)}') IS NOT NULL")=='t'
        privilegios=('SELECT',) if tabela=='MotivosAcompanhamento' else ('SELECT','INSERT')
        for priv in privilegios:
            if existe:
                grant(priv,tabela)
            else:
                conceder.append(f'GRANT {priv} ON {ident(tabela)} TO {ident(role)};')
                revogar.append(f'REVOKE {priv} ON {ident(tabela)} FROM {ident(role)};')
        if tabela!='MotivosAcompanhamento':
            sequence=ident(tabela+'_Id_seq')
            for priv in ('USAGE','SELECT'):
                if not existe or sql(f"SELECT has_sequence_privilege('{role}','{sequence}','{priv}')")!='t':
                    conceder.append(f'GRANT {priv} ON SEQUENCE {sequence} TO {ident(role)};')
                    revogar.append(f'REVOKE {priv} ON SEQUENCE {sequence} FROM {ident(role)};')
privado(backup/'permissoes-aplicar.sql','\n'.join(conceder))
privado(backup/'permissoes-recuar.sql','\n'.join(revogar))
privado(backup/'release-anterior.txt',anterior)
portal_env_estado={'alterado':False}
aplicado=False;mudou=False
relatorio_identidade=None
try:
    if migracao:
        # Migration aditiva e idempotente. O recuo preserva as colunas e todos os registros.
        schema=(release/arquivos_migracao[migracao]).read_text(encoding='utf-8-sig')
        assert migracao in schema and not re.search(r'\b(?:DROP|TRUNCATE)\s|\bDELETE\s+FROM\b',schema,re.I)
        sql("SET lock_timeout='5s'; SET statement_timeout='60s';\n"+schema)
        assert sql(f'SELECT count(*) FROM "__EFMigrationsHistory" WHERE "MigrationId"=\'{migracao}\'')=='1'
    sql('BEGIN;\n'+'\n'.join(conceder)+'\nCOMMIT;');aplicado=True
    if pular_hml:
        verificar_sem_credenciais_cifradas()
    if config_publicacao!=config:
        substituir_portal_env(config,config_publicacao)
    link=base/'current-novo'
    assert not link.exists() and not link.is_symlink()
    link.symlink_to(release);os.replace(link,base/'current');mudou=True
    run(['systemctl','restart',servico]);assert saude()
    assert http('/api/posto/pendencias').endswith('\n401')
    assert http('/api/posto/modelos-documento').endswith('\n401')
    pagina=http('/profissional/');assert pagina.endswith('\n200') and 'nav-pendencias' in pagina
    pid=status(servico)
    assert pid.isdigit() and int(pid)>1
    if pular_hml:
        verificar_flag_desativada_no_processo((pathlib.Path('/proc')/pid/'environ').read_bytes())
        verificar_sem_credenciais_cifradas()
    assert (conf/'portal.env').read_bytes()==config_publicacao
    assert {s:status(s) for s in protegidos}==antes
    relatorio={'ambiente':ambiente,'sha256':sha,'release':str(release),'anterior':anterior,'backup':str(backup),
        'saudavel':True,'servicos_preservados':True,'migracao_nova':migracao,'concessoes':len(conceder),
        'hml_ignorada':pular_hml,'motivo_dispensa_hml':motivo_dispensa_hml}
    destino=stage/(nome+('-hml.json' if ambiente=='hml' else '-producao.json'))
    relatorio_identidade=privado(destino,json.dumps(relatorio,ensure_ascii=False,indent=2))
    usuario=pwd.getpwnam('clinica-admin');os.chown(destino,usuario.pw_uid,usuario.pw_gid)
    print('Atualização saudável. Relatório: '+str(destino))
except Exception as causa:
    erros_recuo=[]
    if relatorio_identidade is not None:
        try:remover_se_igual(destino,relatorio_identidade)
        except Exception as erro:erros_recuo.append(erro)
    if mudou:
        try:
            link=base/'current-recuo';assert not link.exists() and not link.is_symlink()
            link.symlink_to(base/'releases'/anterior);os.replace(link,base/'current')
        except Exception as erro:erros_recuo.append(erro)
    if aplicado:
        try:sql('BEGIN;\n'+'\n'.join(revogar)+'\nCOMMIT;')
        except Exception as erro:erros_recuo.append(erro)
    if portal_env_estado['alterado']:
        try:substituir_portal_env(config_publicacao,config)
        except Exception as erro:erros_recuo.append(erro)
    if mudou:
        try:
            run(['systemctl','restart',servico]);assert saude()
        except Exception as erro:erros_recuo.append(erro)
    if erros_recuo:
        raise RuntimeError(f'Publicação falhou e o recuo teve {len(erros_recuo)} erro(s); conferir backup privado {backup}.') from causa
    raise
