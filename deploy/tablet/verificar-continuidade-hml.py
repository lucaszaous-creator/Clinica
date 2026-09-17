"""Aceite HTTP com dados fictícios; nunca aceita URL de produção.
Uso: python verificar-continuidade-hml.py credencial.json SHA256 resultado.json
A credencial existente é lida em memória e não é incluída na saída.
"""
import base64, datetime, hashlib, http.cookiejar, json, pathlib, re, sys, urllib.request, uuid

credencial, sha, destino = sys.argv[1:]
assert re.fullmatch('[a-f0-9]{64}',sha)
cred=json.loads(pathlib.Path(credencial).read_text(encoding='utf-8-sig'))
base='https://homologacao.clinicasemdormacae.com.br'
assert cred['url'].startswith(base+'/')
http=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
csrf=None
def api(path,body=None,raw=False):
    headers={'Accept':'application/json'}
    if csrf:headers['X-CSRF-TOKEN']=csrf
    if body is not None:headers['Content-Type']='application/json'
    req=urllib.request.Request(base+'/api'+path,data=None if body is None else json.dumps(body).encode(),headers=headers)
    with http.open(req,timeout=60) as r:
        assert r.status==200 and 'no-store' in r.headers.get('Cache-Control','')
        value=r.read()
        return value if raw else json.loads(value) if value else None
def post(path,**body):
    body['idempotencia']=str(uuid.uuid4())
    first=api(path,body)
    assert api(path,body)==first,'Reenvio divergente'
    return first

csrf=api('/sessao')['csrf']
api('/entrar',{'login':cred['usuario'],'senha':cred['senha'],'codigoTablet':cred['codigo_tablet']})
csrf=api('/sessao')['csrf']
marcador='HOMOLOGAÇÃO — paciente fictício'
pacientes=api('/posto/pacientes/buscar',{'busca':marcador})
assert pacientes and all(p['nome'].startswith(marcador) for p in pacientes)
p=pacientes[0]['id'];path=f'/posto/pacientes/{p}'
f=api(path);assert f['paciente']['nome'].startswith(marcador) and f['podeEditarFicha']
hoje=datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=-3))).date().isoformat()
texto='TESTE FICTÍCIO de continuidade. Sem valor assistencial.'
post(path+'/anamnese',versao=f['versaoAnamnese'],antecedentesPessoais=texto,motivo='Ensaio em homologação')
f=api(path)
post(path+'/anamnese',versao=f['versaoAnamnese'],antecedentesPessoais=texto+' Revisada.',motivo='Ensaio do histórico')
assert api(path+'/anamnese/versoes')
f=api(path);tipo=next(t for t in f['tiposMedida'] if t.get('rotuloSegundoValor'))
med=post(path+'/medidas',data=hoje,tipoCodigo=tipo['codigo'],valor=120,valorSecundario=80)
m=next(m for m in api(path)['medidas'] if m['id']==med['id'])
post(path+f"/medidas/{m['id']}/cancelar",versao=m['versao'],motivo='Aferição fictícia de teste')
prob=post(path+'/problemas',id=0,natureza=1,descricao='Alerta fictício de teste',inicio=hoje)
a=next(a for a in api(path)['problemas'] if a['id']==prob['id'])
post(path+f"/problemas/{a['id']}/situacao",versao=a['versao'],situacao=2,motivo='Registro fictício de teste')
post(path+'/exames',data=hoje,nome='Exame fictício',valor='Teste de transcrição')
# PDF válido mínimo, seguido de comentário de preenchimento: exercita limite HTTP ampliado.
pdf=b'%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n2 0 obj<</Type/Pages/Count 0/Kids[]>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n%'+b'X'*1_100_000
anexo=post(path+'/anexos',data=hoje,titulo='Arquivo fictício de aceite HTTP',nomeArquivo='teste.pdf',tipoConteudo='application/pdf',conteudo=base64.b64encode(pdf).decode())
assert hashlib.sha256(api(path+f"/anexos/{anexo['id']}/conteudo",raw=True)).digest()==hashlib.sha256(pdf).digest()
post(path+'/modelos-documento',nome='Modelo fictício '+uuid.uuid4().hex[:12],tipo=0,texto=texto)
assert api('/posto/modelos-documento')
doc=post(path+'/documentos',tipo='receita',texto=texto)
rpath=path+f"/rascunhos/documento/{doc['id']}";r=api(rpath)
nova=post(rpath,versao=r['versao'],motivo='Teste de substituição',corpo=texto+' Nova via.')
rpath=path+f"/rascunhos/documento/{nova['id']}";r=api(rpath)
post(rpath+'/cancelar',versao=r['versao'],motivo='Encerramento do teste fictício')
inf=post(path+'/documentos',tipo='infusao',texto=texto,assinaturaEnfermagem=True)
rpath=path+f"/rascunhos/infusao/{inf['id']}";r=api(rpath)
nova=post(rpath,versao=r['versao'],motivo='Teste de infusão',indicacao='Ensaio sem valor assistencial',assinaturaEnfermagem=True,itens=r['itens'])
rpath=path+f"/rascunhos/infusao/{nova['id']}";r=api(rpath)
post(rpath+'/cancelar',versao=r['versao'],motivo='Encerramento do teste fictício')
iniciado=post(path+'/atender',modalidade=0,motivo=texto)
horario=iniciado['agendamentoId'];apath=f'/clinico/atendimentos/{horario}'
aberto=api(apath);e=aberto['evolucao'];e['textoEvolucao']=texto
salvo=post(apath+'/salvar',evolucao=e,finalizar=True,houveEnfermagem=False)
assert salvo['finalizado'] and salvo['atendimentoId'] and salvo['guias']>0
aberto=api(apath);assert aberto['faturamento']['atendimentoId']==salvo['atendimentoId']
assert aberto['faturamento']['guias'] and aberto['faturamento']['conclusaoClinica']
api('/posto/pendencias');api('/sair',{})
relatorio={'aprovado':True,'sha256':sha,'ambiente':'hml','dados_ficticios':True,
    'validado':['HTTP autenticado/CSRF','idempotencia','anamnese e histórico','medidas/cancelamento','alergia/descarte','exame','anexo >1MB e integridade','modelo','rascunhos documento/infusao','conclusão/protocolo/guias','pendências e situação operacional'],
    'pendente':['assinaturas SafeID reais de médico e enfermagem','aceite operacional da recepção em produção'],
    'agendamento_ficticio':horario,'atendimento_ficticio':salvo['atendimentoId'],'guias':salvo['guias']}
pathlib.Path(destino).write_text(json.dumps(relatorio,ensure_ascii=False,indent=2),encoding='utf-8')
print('Aceite HTTP de homologação aprovado; relatório sem dados pessoais salvo.')
