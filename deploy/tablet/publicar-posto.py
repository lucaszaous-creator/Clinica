"""Publica o pacote homologado, com backup prévio, permissões específicas e recuo automático."""
import hashlib, json, os, pathlib, re, shlex, shutil, socket, subprocess, sys, time, urllib.request, urllib.parse

assert os.geteuid()==0
STAGE=pathlib.Path('/home/clinica-admin/tablet-stage')
BACKUP=pathlib.Path('/var/backups/clinica-posto-producao-20260916')
CONF=pathlib.Path('/etc/clinica-tablet')
BASE=pathlib.Path('/opt/clinica-tablet')
NAME='tablet-release-970b553cb536-55d0727d86b8'
SOURCE=pathlib.Path('/opt/clinica-posto-hml/releases')/NAME
RELEASE=BASE/'releases'/NAME
PREVIOUS=BASE/'releases/7ff21079094f-c4ab81a8afb3'
ROLE='clinica-tablet'
def run(args, **kwargs):
    result=subprocess.run(args,capture_output=True,text=True,**kwargs)
    if result.returncode:
        private(BACKUP/'ultima-falha.log',result.stdout+'\n'+result.stderr)
        raise RuntimeError('Etapa não concluída; conferir diagnóstico privado no backup.')
    return result.stdout.strip()
def sql(query):
    return run(['runuser','-u','postgres','--','psql','-p','45432','-d','clinica','-XAt','-v','ON_ERROR_STOP=1'],input=query)
def private(path,value):
    with os.fdopen(os.open(path,os.O_WRONLY|os.O_CREAT|os.O_TRUNC,0o600),'w') as file:file.write(value)
def ident(name):return '"'+name.replace('"','""')+'"'
def pid(name):return run(['systemctl','show',name,'-p','MainPID','--value'])
def health():
    for _ in range(30):
        result=subprocess.run(['curl','--silent','--fail','--max-time','2','--unix-socket','/run/clinica-tablet/portal.sock','--header','X-Forwarded-Proto: https','http://localhost/health'],capture_output=True,text=True)
        if result.returncode==0 and '"ok"' in result.stdout:return True
        time.sleep(1)
    return False
def acl():
    rows=sql('''SELECT json_build_array('TABLE',n.nspname,c.relname,a.privilege_type,NULL)::text
        FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace CROSS JOIN LATERAL aclexplode(c.relacl) a
        WHERE n.nspname='public' AND c.relkind IN ('r','p') AND a.grantee=(SELECT oid FROM pg_roles WHERE rolname='clinica-tablet')
        UNION ALL SELECT json_build_array('SEQUENCE',n.nspname,c.relname,a.privilege_type,NULL)::text
        FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace CROSS JOIN LATERAL aclexplode(c.relacl) a
        WHERE n.nspname='public' AND c.relkind='S' AND a.grantee=(SELECT oid FROM pg_roles WHERE rolname='clinica-tablet')
        UNION ALL SELECT json_build_array('TABLE',n.nspname,c.relname,a.privilege_type,col.attname)::text
        FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace JOIN pg_attribute col ON col.attrelid=c.oid
        CROSS JOIN LATERAL aclexplode(col.attacl) a WHERE n.nspname='public' AND a.grantee=(SELECT oid FROM pg_roles WHERE rolname='clinica-tablet');''')
    return {tuple(json.loads(row)) for row in rows.splitlines()}
assert (BASE/'current').resolve()==PREVIOUS
assert BACKUP.joinpath('banco.dump').stat().st_size>0 and BACKUP.stat().st_mode&0o777==0o700
assert (CONF/'portal.env').read_bytes()==(BACKUP/'portal.env').read_bytes()
assert sql("SELECT NOT (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls) FROM pg_roles WHERE rolname='clinica-tablet'")=='t'
assert sql("SELECT count(*) FROM pg_class WHERE relnamespace='public'::regnamespace AND relowner=(SELECT oid FROM pg_roles WHERE rolname='clinica-tablet')")=='0'
manifest=json.loads((SOURCE/'manifesto.json').read_text(encoding='utf-8-sig'))
for name,sha in manifest['arquivos'].items():
    target=(SOURCE/name).resolve();assert target.is_relative_to(SOURCE) and hashlib.sha256(target.read_bytes()).hexdigest()==sha
if not RELEASE.exists():shutil.copytree(SOURCE,RELEASE)
for name,sha in manifest['arquivos'].items():assert hashlib.sha256((RELEASE/name).read_bytes()).hexdigest()==sha
tunnel_pid=pid('cloudflared-site');before_acl=acl()
private(BACKUP/'acl-anterior.json',json.dumps(sorted(before_acl,key=str)))

reads='''Usuarios Profissionais Pacientes Agendamentos Atendimentos Codigos Parametros Configuracoes Convenios Modalidades Especialidades Salas BloqueiosAgenda
Evolucoes VersoesEvolucao ModelosEvolucao Anamneses ValoresCampoPersonalizado CamposPersonalizadosProntuario
AnexosProntuario Consentimentos MedidasClinicas ResultadosExame ArquivosResultadoExame AnexosPaciente ArquivosAnexoPaciente ProblemasPaciente AvaliacoesClinicas RespostasAvaliacao
MapasCorporais PontosMapa ProtocolosCorporais PontosProtocolo DocumentosClinicos ItensDocumento ModelosDocumento ItensModelo PrescricoesInternas ItensPrescricaoInterna ChecagensPrescricao
EvolucoesEnfermagem DiagnosticosEnfermagem CuidadosEnfermagem ChecagensCuidado AssinaturasDocumento ArquivosAssinados TracosAssinatura ExigenciasTermo
Autorizacoes SessoesTablet ColetasTablet ViasAssinadasPaciente OperacoesClinicasTablet EtapasFechamentoSessao PacotesPaciente PacotesCatalogo ConsumosPacote PrecosConvenio PrecosParticular'''.split()
inserts='''Auditoria Agendamentos Atendimentos Codigos Evolucoes VersoesEvolucao MapasCorporais PontosMapa ProtocolosCorporais PontosProtocolo
DocumentosClinicos ItensDocumento PrescricoesInternas ItensPrescricaoInterna ChecagensPrescricao ProblemasPaciente AssinaturasDocumento ArquivosAssinados AnexosProntuario
Consentimentos TracosAssinatura SessoesTablet ColetasTablet ViasAssinadasPaciente OperacoesClinicasTablet EtapasFechamentoSessao'''.split()
updates='Agendamentos Atendimentos Codigos Evolucoes MapasCorporais DocumentosClinicos PrescricoesInternas ItensPrescricaoInterna SessoesTablet ColetasTablet Autorizacoes'.split()
grants=[]
for privilege,tables in [('SELECT',reads),('INSERT',inserts),('UPDATE',updates),('DELETE',['PontosMapa','PontosProtocolo','ItensDocumento'])]:
    grants.append(f'GRANT {privilege} ON '+','.join(map(ident,tables))+f' TO "{ROLE}";')
grants+=['GRANT SELECT ("Id") ON "Auditoria" TO "clinica-tablet";',
    'GRANT UPDATE ("Categoria") ON "Pacientes" TO "clinica-tablet";',
    'GRANT UPDATE ("UltimoAcessoEm","TentativasFalhas","BloqueadoAte") ON "Usuarios" TO "clinica-tablet";']
for table in inserts:
    if table in ('EtapasFechamentoSessao','ViasAssinadasPaciente','OperacoesClinicasTablet'):continue
    sequence=sql(f"SELECT pg_get_serial_sequence('\"{table}\"','Id');")
    if sequence:grants.append(f'GRANT USAGE, SELECT ON SEQUENCE {sequence} TO "{ROLE}";')
schema=(RELEASE/'migracao-tablet.sql').read_text(encoding='utf-8-sig').replace('START TRANSACTION;','').replace('COMMIT;','')
private(BACKUP/'migracao-e-grants.sql',"SET lock_timeout='5s'; SET statement_timeout='120s'; BEGIN;\n"+schema+'\n'+'\n'.join(grants)+'\nCOMMIT;')

# Proxy independente da homologação; TLS passa intacto e só os hosts do SafeID são aceitos.
proxy=pathlib.Path('/opt/clinica-safeid-proxy');proxy.mkdir(mode=0o755,exist_ok=True)
(proxy/'safeid-proxy.py').write_text((RELEASE/'configuracao/homologacao/safeid-proxy.py').read_text().replace('PORT = 18743','PORT = 18744'))
unit=(RELEASE/'configuracao/homologacao/clinica-safeid-hml.service').read_text().replace('para homologacao','do posto clinico').replace('/opt/clinica-posto-hml/current/configuracao/homologacao/safeid-proxy.py','/opt/clinica-safeid-proxy/safeid-proxy.py')
pathlib.Path('/etc/systemd/system/clinica-safeid.service').write_text(unit)
safe=(pathlib.Path('/etc/clinica-posto-hml/safeid.env')).read_text().replace('https://homologacao.clinicasemdormacae.com.br/safeid/retorno','https://portal.clinicasemdormacae.com.br/safeid/retorno')
private(CONF/'safeid.env',safe)
env=(CONF/'portal.env').read_text()
for key,value in {'Portal__AtendimentoHabilitado':'true','Portal__Homologacao':'false','Portal__SafeId__Habilitado':'true','HTTPS_PROXY':'http://127.0.0.1:18744','NO_PROXY':'localhost,127.0.0.1'}.items():
    env=re.sub(r'^'+re.escape(key)+r'=.*$',key+'='+value,env,flags=re.M) if re.search(r'^'+re.escape(key)+r'=',env,re.M) else env.rstrip()+'\n'+key+'='+value+'\n'
private(CONF/'portal.env.preparado',env)
dropdir=pathlib.Path('/etc/systemd/system/clinica-tablet.service.d');dropdir.mkdir(exist_ok=True)
drop=dropdir/'posto.conf'
assert not drop.exists()
drop_text='''[Unit]
After=clinica-safeid.service
Requires=clinica-safeid.service
[Service]
EnvironmentFile=/etc/clinica-tablet/safeid.env
InaccessiblePaths=-/etc/clinica-posto-hml -/opt/clinica-posto-hml -/var/lib/clinica-posto-hml -/run/clinica-posto-hml
'''
run(['systemctl','daemon-reload']);run(['systemctl','enable','--now','clinica-safeid'])
assert run(['systemctl','is-active','clinica-safeid'])=='active'
for _ in range(50):
    try:
        with socket.create_connection(('127.0.0.1',18744),timeout=1):break
    except OSError:time.sleep(.2)
else:raise RuntimeError('Proxy SafeID não iniciou; portal anterior preservado.')
credentials={key:shlex.split(value)[0] for key,value in (line.split('=',1) for line in safe.splitlines())}
request=urllib.request.Request('https://pscsafeweb.safewebpss.com.br/Service/Microservice/OAuth/api/v0/oauth/client_token',
    data=urllib.parse.urlencode({'grant_type':'client_credentials','client_id':credentials['Portal__SafeId__ClientId'],'client_secret':credentials['Portal__SafeId__ClientSecret']}).encode(),
    headers={'Content-Type':'application/x-www-form-urlencoded'})
with urllib.request.build_opener(urllib.request.ProxyHandler({'https':'http://127.0.0.1:18744'})).open(request,timeout=25) as response:
    assert len(json.loads(response.read(100000)).get('access_token',''))>20,'Credenciais não validadas; publicação interrompida.'
credentials=None;request=None

# Só fatos clínicos reais serão gravados pela equipe. Esta publicação não cria atendimentos de teste.
assert sql('SELECT count(*) FROM "ColetasTablet" WHERE "Estado"=\'recebido\'')=='0','Há coleta sendo processada; tentar novamente após concluir.'
switched=False
try:
    sql((BACKUP/'migracao-e-grants.sql').read_text())
    additions=acl()-before_acl
    revoke=[]
    for kind,schema_name,name,privilege,column in sorted(additions,key=str):
        revoke.append(f'REVOKE {privilege}'+(' ('+ident(column)+')' if column else '')+f' ON {kind} {ident(schema_name)}.{ident(name)} FROM "{ROLE}";')
    private(BACKUP/'recuar-permissoes.sql','\n'.join(revoke))
    # Leitura autenticada com o próprio papel de execução, sem ler dados de pacientes.
    check='BEGIN READ ONLY; SELECT "AtividadeClinicaEm" FROM "SessoesTablet" LIMIT 0; SELECT "PacienteId" FROM "OperacoesClinicasTablet" LIMIT 0; COMMIT;'
    run(['runuser','-u',ROLE,'--','psql','-p','45432','-d','clinica','-XAt','-v','ON_ERROR_STOP=1'],input=check)
    run(['systemctl','stop','clinica-tablet'])
    switched=True
    (CONF/'portal.env.preparado').replace(CONF/'portal.env')
    drop.write_text(drop_text)
    next_link=BASE/'current-posto';next_link.symlink_to(RELEASE,target_is_directory=True);next_link.replace(BASE/'current')
    run(['systemctl','daemon-reload']);run(['systemctl','start','clinica-tablet'])
    assert health(),'Portal não respondeu à conferência privada.'
    assert pid('cloudflared-site')==tunnel_pid
    report={'estado':'POSTO_CLINICO_PRODUCAO_ATIVO','backend':manifest['backend'],'interface':manifest['interface'],'url':'https://portal.clinicasemdormacae.com.br/profissional/','backup':str(BACKUP),'tunnel_preservado':True,'dados_ficticios_inseridos':False,'safeid_ativo':True}
    (STAGE/'resultado-producao.json').write_text(json.dumps(report))
    print(json.dumps(report))
except Exception:
    if switched:
        subprocess.run(['systemctl','stop','clinica-tablet'],capture_output=True)
        shutil.copy2(BACKUP/'portal.env',CONF/'portal.env')
        if drop.exists():drop.unlink()
        previous_link=BASE/'current-recuo';previous_link.symlink_to(PREVIOUS,target_is_directory=True);previous_link.replace(BASE/'current')
    if (BACKUP/'recuar-permissoes.sql').exists():sql((BACKUP/'recuar-permissoes.sql').read_text())
    run(['systemctl','daemon-reload']);run(['systemctl','start','clinica-tablet'])
    assert health(),'Verificar serviço; recuo não confirmou saúde.'
    raise
