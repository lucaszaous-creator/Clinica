"""Executar como root na VPS. Sem interface nova e sem reiniciar o portal.

Diagnóstico: python3 operar-vps.py diagnostico
Prévia: python3 operar-vps.py prever LOGIN INICIO FIM
Aplicar: python3 operar-vps.py aplicar
O pacote publicado deve estar na mesma pasta deste script.
"""
import datetime as dt
import hashlib
import json
import os
import pathlib
import pwd
import re
import shutil
import subprocess
import sys

assert os.geteuid() == 0, 'Execute no console administrativo.'
os.umask(0o077)
base = pathlib.Path(__file__).resolve().parent
envfile = pathlib.Path('/etc/clinica-tablet/portal.env')
env = {}
for line in envfile.read_text().splitlines():
    if not line or line.lstrip().startswith('#') or '=' not in line:
        continue
    key, value = line.split('=', 1)
    env[key.strip()] = value.strip().strip('"').strip("'")
cs = env['ConnectionStrings__Clinica']
def campo(key, default=None):
    match = re.search(r'(?:^|;)\s*' + key + r'\s*=\s*([^;]+)', cs, re.I)
    return match.group(1).strip() if match else default
database = campo('Database')
host = campo('Host')
port = campo('Port', '5432')
if sys.argv[1] == 'diagnostico':
    print(json.dumps({'host': host, 'porta': port, 'base': database,
        'release': str(pathlib.Path('/opt/clinica-tablet/current').resolve())}), flush=True)
assert database == 'clinica' and host in ('127.0.0.1', 'localhost', '/var/run/postgresql') and port == '45432', 'Confira a base e a porta de produção.'
banco = hashlib.sha256(f'{host}:{port}/{database}'.encode()).hexdigest().upper()
work = pathlib.Path('/var/backups/clinica-recuperacao-guias')
work.mkdir(mode=0o700, exist_ok=True)
postgres = pwd.getpwnam('postgres')
os.chown(work, postgres.pw_uid, postgres.pw_gid)
plano = work / 'plano.json'

def sql(query):
    p = subprocess.run(['runuser', '-u', 'postgres', '--', 'psql', '-p', port, '-d', database,
                        '-XAt', '-v', 'ON_ERROR_STOP=1'], input=query, text=True, capture_output=True)
    assert p.returncode == 0, 'Falha na consulta administrativa; nenhuma credencial foi exibida.'
    return p.stdout.strip()

def executar(args):
    # A manutenção usa a autenticação local do administrador PostgreSQL. Não exporta
    # nem reutiliza a senha da aplicação; destino conferido com a configuração do portal.
    procenv = dict(os.environ, CLINICA_DB=f'Host={host};Port={port};Database={database};Username=postgres', TZ='America/Sao_Paulo')
    build = hashlib.file_digest((base / 'Clinica.Infrastructure.dll').open('rb'), 'sha256').hexdigest()[:12]
    runtime = pathlib.Path('/opt/clinica-recuperacao-guias-' + build)
    if not runtime.exists():
        shutil.copytree(base, runtime, ignore=shutil.ignore_patterns('*.tar.gz', 'operar.py'))
        runtime.chmod(0o755)
        (runtime / 'RecuperarGuias').chmod(0o755)
    assert hashlib.file_digest((runtime / 'RecuperarGuias.dll').open('rb'), 'sha256').digest() == hashlib.file_digest((base / 'RecuperarGuias.dll').open('rb'), 'sha256').digest(), 'Versão da ferramenta divergiu.'
    return subprocess.run(['runuser', '-u', 'postgres', '--', str(runtime / 'RecuperarGuias'), *args], env=procenv).returncode

modo = sys.argv[1]
if modo == 'resultado':
    dados = json.loads(plano.read_text())
    pendentes = [i for i in dados['Itens'] if not i['Apta'] and i['EvolucaoId'] is not None]
    pares = ','.join(f"({int(i['Id'])},{int(i['EvolucaoId'])})" for i in pendentes)
    print(sql('''SELECT json_agg(x) FROM (
      SELECT a."Status", e."ProfissionalId" IS NULL sem_autor_profissional,
        a."ProfissionalId" IS NULL sem_medico_agenda, e."Data" <> a."DataHora"::date data_diverge,
        e."PacienteId" <> a."PacienteId" paciente_diverge, count(*) quantidade
      FROM (VALUES ''' + pares + ''') p(aid,eid)
      JOIN "Agendamentos" a ON a."Id"=p.aid JOIN "Evolucoes" e ON e."Id"=p.eid
      GROUP BY a."Status", sem_autor_profissional, sem_medico_agenda, data_diverge, paciente_diverge
    ) x''') if pares else 'Sem pendências com evolução identificada.')
    antes = json.loads(sorted(work.glob('antes-*.json'))[-1].read_text())
    aids = ','.join(str(int(s['sessao'])) for s in antes)
    antigos = ','.join(str(int(k)) for s in antes for k in s['codigos']) or '0'
    print(sql('''SELECT json_agg(x) FROM (
      SELECT c."Id", c."AtendimentoId", c."Status", c."DataPrevistaFaturamento"
      FROM "Codigos" c JOIN "Agendamentos" a ON a."AtendimentoId"=c."AtendimentoId"
      WHERE a."Id" IN (''' + aids + ') AND c."Id" NOT IN (' + antigos + ''')
    ) x'''))
    resumo = json.loads(sorted(work.glob('resumo-*.json'))[-1].read_text())
    print(json.dumps({k:v for k,v in resumo.items() if k != 'Pendencias'}, indent=2))
elif modo == 'auditar':
    print(sql('''WITH escritos AS (
      SELECT a."Id", a."Status", a."AtendimentoId", a."FimAtendimentoEm",
        (SELECT count(*) FROM "Codigos" c WHERE c."AtendimentoId"=a."AtendimentoId") codigos,
        (SELECT count(*) FROM "Codigos" c WHERE c."AtendimentoId"=a."AtendimentoId" AND c."Status" <> 'NaoAplicavel') guias
      FROM "Agendamentos" a WHERE a."DataHora" <= now() AT TIME ZONE 'America/Sao_Paulo'
        AND a."Status" IN ('Agendado','Realizado')
        AND EXISTS (SELECT 1 FROM "Evolucoes" e WHERE e."CanceladaEm" IS NULL
          AND (e."AgendamentoId"=a."Id" OR (e."AgendamentoId" IS NULL AND e."PacienteId"=a."PacienteId" AND e."Data"=a."DataHora"::date)))
    ) SELECT json_agg(x) FROM (
        SELECT "Status", "FimAtendimentoEm" IS NULL aberto, codigos, guias, count(*) sessoes,
          array_agg("Id" ORDER BY "Id") ids
        FROM escritos GROUP BY "Status", "FimAtendimentoEm" IS NULL, codigos, guias
        ORDER BY aberto DESC, "Status", codigos
    ) x;'''))
elif modo == 'diagnostico':
    print(json.dumps({'base': database, 'release': str(pathlib.Path('/opt/clinica-tablet/current').resolve()),
        'gerentes': json.loads(sql('SELECT coalesce(json_agg(x),\'[]\') FROM (SELECT "Id", "Login", "Perfil" FROM "Usuarios" WHERE "Ativo" AND "Perfil" = \'Gerente\') x')),
        'periodo': sql('SELECT min("DataHora")::date || \' a \' || max("DataHora")::date FROM "Agendamentos"'),
        'evolucoes_vigentes': sql('SELECT count(*) FROM "Evolucoes" WHERE "CanceladaEm" IS NULL')}, indent=2))
elif modo == 'prever':
    assert len(sys.argv) == 5
    if plano.exists():
        anterior = work / ('plano-anterior-' + dt.datetime.now(dt.timezone.utc).strftime('%Y%m%dT%H%M%SZ') + '.json')
        assert not anterior.exists(), 'Já existe uma prévia arquivada neste segundo.'
        plano.rename(anterior)
    sys.exit(executar(['prever', *sys.argv[2:], str(plano)]))
elif modo == 'aplicar':
    dados = json.loads(plano.read_text())
    assert dados['Banco'] == banco, 'A prévia pertence a outra base.'
    aptas = [i for i in dados['Itens'] if i['Apta']]
    assert aptas, 'Não há sessões aptas no plano.'
    assert all(i.get('Escrita') is True for i in aptas), 'Há sessão sem a confirmação Escrito; gere a prévia atualizada.'
    pares = ','.join(f"({int(i['Id'])},{int(i['EvolucaoId'])})" for i in aptas)
    def retrato():
        return json.loads(sql('''SELECT json_agg(x) FROM (
          SELECT a."Id" sessao, a."AtendimentoId" atendimento, a."Status" status,
            a."FimAtendimentoEm" fim, a."DataHora" data,
            md5((to_jsonb(e) - ARRAY['AtendimentoId','AgendamentoId','AtualizadoEm'])::text) evolucao,
            (SELECT coalesce(json_object_agg(c."Id", md5(to_jsonb(c)::text)), '{}'::json)
               FROM "Codigos" c WHERE c."AtendimentoId"=a."AtendimentoId") codigos
          FROM (VALUES ''' + pares + ''') p(aid,eid)
          JOIN "Agendamentos" a ON a."Id"=p.aid JOIN "Evolucoes" e ON e."Id"=p.eid
        ) x'''))
    antes = retrato()
    stamp = dt.datetime.now(dt.timezone.utc).strftime('%Y%m%dT%H%M%SZ')
    dump = work / f'clinica-antes-{stamp}.dump'
    with dump.open('xb') as f:
        p = subprocess.run(['runuser', '-u', 'postgres', '--', 'pg_dump', '-p', port, '-d', database,
                            '-Fc', '--no-owner', '--no-acl'], stdout=f, stderr=subprocess.PIPE)
    assert p.returncode == 0 and dump.stat().st_size > 0, 'Backup falhou: correção não executada.'
    with dump.open('rb') as f:
        p = subprocess.run(['runuser', '-u', 'postgres', '--', 'pg_restore', '--list'], stdin=f,
                           stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
    assert p.returncode == 0, 'Arquivo de backup inválido: correção não executada.'
    manifest = work / f'backup-{stamp}.json'
    with manifest.open('x') as f:
        json.dump({'Banco': banco, 'CriadoEm': dt.datetime.now(dt.timezone.utc).isoformat(),
                   'Arquivo': str(dump), 'Sha256': hashlib.file_digest(dump.open('rb'), 'sha256').hexdigest().upper()}, f)
    os.chown(dump, postgres.pw_uid, postgres.pw_gid)
    os.chown(manifest, postgres.pw_uid, postgres.pw_gid)
    print('Backup criado e catálogo de restauração verificado.', flush=True)
    (work / f'antes-{stamp}.json').write_text(json.dumps(antes))
    resultados_anteriores = set(work.glob('plano.json.resultado-*.jsonl'))
    rc = executar(['aplicar', str(plano), str(manifest)])
    arquivos = set(work.glob('plano.json.resultado-*.jsonl')) - resultados_anteriores
    assert len(arquivos) == 1, 'Confira o resultado da execução; nenhum arquivo único identificado.'
    resultados = [json.loads(line) for line in arquivos.pop().read_text().splitlines()]
    depois = {s['sessao']: s for s in retrato()}
    novas_conclusoes = 0
    for a in antes:
        d = depois[a['sessao']]
        assert a['evolucao'] == d['evolucao'] and a['data'] == d['data'], 'Conteúdo clínico mudou durante a manutenção; conferir concorrência.'
        assert a['atendimento'] is None or a['atendimento'] == d['atendimento'], 'Atendimento original divergiu.'
        assert all(d['codigos'].get(k) == v for k, v in a['codigos'].items()), 'Uma guia anterior mudou durante a manutenção; conferir concorrência.'
        if any(r['Id'] == a['sessao'] and r['Sucesso'] for r in resultados):
            assert d['status'] == 'Realizado' and d['fim'] is not None, 'Conclusão não confirmada.'
            novas_conclusoes += int(a['fim'] is None or a['status'] != 'Realizado')
    resumo = {'Processadas': len(resultados), 'EncerradasAgora': novas_conclusoes,
        'GuiasCriadas': sum(r['Guias'] for r in resultados), 'Falhas': sum(not r['Sucesso'] for r in resultados),
        'EvolucoesPreservadas': True, 'GuiasAnterioresPreservadas': True,
        'PortalAtivo': subprocess.run(['systemctl', 'is-active', '--quiet', 'clinica-tablet']).returncode == 0,
        'Pendencias': [{'Sessao': i['Id'], 'Motivo': i['Situacao']} for i in dados['Itens'] if not i['Apta']]}
    (work / f'resumo-{stamp}.json').write_text(json.dumps(resumo, indent=2))
    print(json.dumps({k: v for k,v in resumo.items() if k != 'Pendencias'}, indent=2))
    sys.exit(rc)
else:
    raise SystemExit('Modo inválido. Use diagnostico, prever ou aplicar.')
