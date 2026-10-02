from pathlib import Path
import json,re,subprocess,xml.etree.ElementTree as ET,collections,shutil,argparse
ROOT=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser(description='Extrai referências para o comparativo visual; não modifica produtos.')
parser.add_argument('--repo',type=Path,default=ROOT/'work/corrigir-pr-231' if (ROOT/'work/corrigir-pr-231').exists() else Path.cwd())
parser.add_argument('--output',type=Path,default=Path(__file__).resolve().parent)
args=parser.parse_args()
REPO=args.repo.resolve()
OUT=args.output.resolve()
OUT.mkdir(parents=True,exist_ok=True)
B=REPO/'docs/auditoria-global-2026-10-01'
def read(name):return json.loads((B/name).read_text(encoding='utf-8'))
def git(*args):return subprocess.check_output(['git','-C',str(REPO),*args])
sha=git('rev-parse','origin/main').decode().strip()
paths=git('ls-tree','-r','--name-only',sha).decode().splitlines()
surfaces=read('cobertura-superficies.json');findings=read('achados.json');nav=read('navegacao-perfis.json')
# id | title | module | family | source suffix | findings | new title
spec='''
dia|Atendimentos do dia|recepcao|agenda|Recepcao/Views/FilaView.xaml||
grade|Grade por profissional|recepcao|week|Recepcao/Views/AgendaView.xaml||
semana|Semana|recepcao|week|Clinico/Views/MinhaSemanaView.xaml||
agendar|Marcar atendimento|recepcao|booking|Recepcao/Janelas/AgendamentoWindow.xaml||
vagas|Próximas vagas|recepcao|week|Recepcao/Janelas/ProximasVagasWindow.xaml||
espera|Lista de espera|recepcao|patients|Recepcao/Janelas/ListaEsperaPainelWindow.xaml||
confirmacoes|Confirmações|recepcao|contact|Recepcao/Views/ConfirmacoesView.xaml|A25|
retornos|Retornos solicitados|recepcao|patients|Recepcao/Views/RetornosAMarcarView.xaml|A25|
lancar|Lançar atendimento|recepcao|booking|Recepcao/Views/NovoAtendimentoView.xaml|A24|
conferir|Conferir atendimentos|recepcao|patients|Recepcao/Views/LancamentosView.xaml||
convenio|Consultas de convênio|recepcao|patients|Recepcao/Views/ConsultasView.xaml||
pacientes|Lista de pacientes|recepcao|patients|Recepcao/Views/PacientesView.xaml|A40|
cadastro|Cadastro do paciente|recepcao|form|Componentes/Cadastro/CadastroPacienteWindow.xaml|A40|
ficha|Ficha do paciente|recepcao|patient|Recepcao/Views/ResumoAdministrativoPacienteView.xaml|A07,A16,A56,A57|
autorizacoes|Autorizações e validade|recepcao|patients|Recepcao/Views/ConvenioPacienteView.xaml|A07|
documentos|Documentos|recepcao|documents|Recepcao/Views/DocumentosView.xaml|A01,A02,A03,A04,A05,A06,A29|
pagamentos|Recebimentos de pacientes|recepcao|ledger|Recepcao/Views/PagamentosView.xaml|A05,A62|
pacotes|Pacotes|recepcao|packages|Componentes/PacotesView.xaml|A08|
termos|Termos do paciente|recepcao|documents|Recepcao/Views/TermosPacienteView.xaml|A28|
privacidade|Privacidade e consentimentos|recepcao|privacy|Recepcao/Views/PrivacidadePacienteView.xaml|A57|
acompanhamento|Acompanhamento de pacientes|recepcao|contact|Recepcao/Views/AcompanhamentoView.xaml|A25,A63|Acompanhamento de retornos
equipe|Profissionais e salas|recepcao|form|Recepcao/Views/EquipeView.xaml||
meudia|Meu dia|clinico|agenda|Clinico/Views/MeuDiaView.xaml||
sessao|Atendimento|clinico|clinical|Clinico/Views/AtendimentoView.xaml|A24|Sessão clínica
pendentes|Evoluções pendentes|clinico|patients|Clinico/Views/RegistrosPendentesView.xaml||
prontuario|Prontuário|clinico|patient|Clinico/Views/ProntuarioClinicoView.xaml|A41|
anamnese|Anamnese|clinico|form|Clinico/Views/AnamneseView.xaml||
problemas|Problemas e alertas|clinico|form|Clinico/Janelas/ProblemaWindow.xaml||
medidas|Medidas|clinico|metrics|Clinico/Views/MedidasView.xaml||
dor|Evolução da dor|clinico|metrics|Clinico/Views/EvolucaoDorView.xaml||
avaliacoes|Avaliações|clinico|metrics|Clinico/Views/AvaliacoesView.xaml||
evolucaomedidas|Acompanhamento|clinico|metrics|Clinico/Views/AcompanhamentoView.xaml|A63|Evolução e medidas
exames|Exames e anexos|clinico|exams|Clinico/Views/AnexosPacienteView.xaml|A41,A61|
prescricoes|Receitas e documentos|clinico|documents|Clinico/Views/PrescricoesClinicasView.xaml|A01,A02,A03,A04,A29|
emitir|Emitir documento|clinico|editor|Componentes/DocumentoWindow.xaml|A01,A02,A04,A29|
infusao|Prescrição de infusão|clinico|infusion|Clinico/Views/PrescricaoInfusaoView.xaml||
sala|Sala de infusão|clinico|nursing|Componentes/SalaInfusaoView.xaml|A20|
execucao|Folha de execução|clinico|nursing|Componentes/FolhaExecucaoWindow.xaml|A20|
enfermagem|Passagens de enfermagem|clinico|nursing|Componentes/EnfermagemView.xaml|A20|
numeros|Meus números|clinico|metrics|Clinico/Views/MeusNumerosView.xaml||
resumo|Resumo do faturamento|faturamento|billing|Faturamento/Views/DashboardView.xaml|A32|
baixas|Pendências e baixas|faturamento|billing|Faturamento/Views/BaixaView.xaml|A67|
guias|Consultar guias|faturamento|billing|Faturamento/Views/ConsultaGuiasView.xaml||
faturados|Faturados|faturamento|billing|Faturamento/Views/FaturadosView.xaml||
glosas|Glosas e recursos|faturamento|billing|Faturamento/Views/GlosasView.xaml|A32,A37|
nc|Não conformidades|faturamento|billing|Faturamento/Views/NaoConformidadesView.xaml||
tiss|Lotes e XML TISS|faturamento|tiss|Faturamento/Views/TissView.xaml|A65,A66,A68|
retornotiss|Retorno do lote|faturamento|return|Faturamento/Alertas/RetornoLoteWindow.xaml|A64|
rodada|Rodada de pendências|faturamento|billing|Faturamento/Alertas/RodadaPendenciasWindow.xaml|A67|
relguias|Relatórios de guias|faturamento|metrics|Faturamento/Views/RelatoriosView.xaml||
regrastiss|Regras e catálogos TISS|faturamento|settings|Faturamento/Views/ParametrosView.xaml|A33,A34|
caixa|Caixa|financeiro|ledger|Financeiro/Views/CaixaView.xaml|A10,A35|
fechamento|Fechamento do caixa|financeiro|ledger|Financeiro/Views/FechamentoCaixaView.xaml||
fluxo|Fluxo de caixa|financeiro|metrics|Financeiro/Views/FluxoCaixaView.xaml||
contas|Contas a pagar e receber|financeiro|accounts|Financeiro/Views/ContasView.xaml|A27|
fixas|Contas fixas|financeiro|accounts|Financeiro/Janelas/ContasFixasWindow.xaml||
inadimplencia|Quem me deve|financeiro|ledger|Financeiro/Views/InadimplenciaView.xaml|A36|
plano|Plano de contas|financeiro|settings|Financeiro/Views/PlanoContasView.xaml||
recebiveis|Recebíveis de cartão|financeiro|ledger|Financeiro/Views/RecebiveisView.xaml||
conciliacao|Conciliação|financeiro|reconcile|Financeiro/Views/ConciliacaoView.xaml|A09,A37|Conferência dos recebimentos
extrato|Extrato do banco|financeiro|ledger|Financeiro/Views/ExtratoBancoView.xaml|A09|Movimentos do banco
taxas|Taxas e impostos|financeiro|settings|Financeiro/Views/TaxasView.xaml||
repasses|Repasses|financeiro|ledger|Financeiro/Views/RepassesView.xaml||
producao|Produção|financeiro|metrics|Financeiro/Views/ProducaoView.xaml||
resultado|Resultado do mês|financeiro|metrics|Financeiro/Views/ResultadoView.xaml|A09|Resultado e teto de despesas
estoque|Estoque|financeiro|stock|Financeiro/Views/EstoqueView.xaml|A10|
pix|Cobrança Pix|financeiro|pix|Financeiro/Janelas/CobrancaPixWindow.xaml|A58|
direcao|Painel da direção|gerente|dashboard|Gerente/Views/PainelDirecaoView.xaml|A22|
indicadores|Indicadores|gerente|metrics|Gerente/Views/IndicadoresView.xaml|A22|
metas|Metas|gerente|metrics|Gerente/Views/MetasView.xaml||
campanhas|Campanhas|gerente|campaign|Gerente/Views/CampanhasView.xaml|A31,A39|
retencao|Quem parou de vir|gerente|contact|Gerente/Views/RetencaoView.xaml|A38|
origens|De onde vêm os pacientes|gerente|metrics|Gerente/Views/OrigensView.xaml||
precos|Preço particular|gerente|prices|Componentes/PrecosParticularView.xaml|A08|Preços · Particular
precosconvenio|Preços por convênio|gerente|prices|Gerente/Views/PrecosConvenioView.xaml|A08|Preços · Convênio
rentabilidade|Rentabilidade por convênio|gerente|metrics|Gerente/Views/RentabilidadeConvenioView.xaml||
custos|Custo de taxas e impostos|gerente|metrics|Gerente/Views/CustoTransacaoView.xaml||
configuracoes|Configurações|gerente|settings|Gerente/Views/ConfiguracoesView.xaml|A33,A34,A59|
modelos|Modelos por tipo|gerente|models|Gerente/Views/ModelosTermoWindow.xaml|A21|
acessos|Acessos|gerente|access|Gerente/Views/AcessosView.xaml|A09,A60|Equipe e permissões
auditoria|Auditoria|gerente|audit|Gerente/Views/AuditoriaView.xaml||
emitidos|Documentos emitidos|gerente|documents|Gerente/Views/DocumentosEmitidosView.xaml||
guarda|Guarda do prontuário|gerente|privacy|Gerente/Views/GuardaProntuarioView.xaml||
importacao|Importar pacientes|gerente|import|Gerente/Views/ImportacaoPacientesView.xaml||
ajuda|Ajuda e suporte|comum|help|Componentes/AjudaView.xaml|A69,A70|
treinamento|Treinamento|comum|training|Treinamento/TreinamentoView.xaml|A55|
busca|Pesquisar seções|comum|search|Shell/ShellWindow.xaml|A53,A54|
conversa|Atendimentos|crm|chat||A24,A47,A48,A49|Conversas
meutrabalho|Meu dia|crm|tasks||A23,A25|Meu trabalho
supervisao|Visão geral|crm|crmmetrics||A23,A50|Supervisão ao vivo
contatoscrm|Contatos e funil|crm|crmpeople||A25|
tarefascrm|Tarefas e follow-up|crm|tasks||A25|
agendacrm|Agenda e confirmações|crm|crmcalendar||A26,A51|
recallcrm|Recall|crm|tasks||A25,A51|
operacaocrm|Operação|crm|operation||A23,A26,A51|Operação e diagnóstico
relatorioscrm|Relatórios|crm|crmmetrics||A23|
configcrm|Configurações e IA|crm|crmsettings||A18,A19|
portal|Portal web preservado|preservado|protected||A11,A12,A13,A14,A42,A43,A44,A45|
webconsulta|Web de consulta|preservado|protected||A52|
site|Site institucional|preservado|protected||A46|
'''
screens=[]
for line in spec.strip().splitlines():
 ident,title,module,family,suffix,ids,newtitle=line.split('|')
 matches=[p for p in paths if p.endswith(suffix)] if suffix else []
 assert not suffix or len(matches)==1,(ident,matches)
 source=matches[0] if matches else ''
 item={'id':ident,'title':title,'module':module,'family':family,'source':source,'findings':ids.split(',') if ids else [],'afterTitle':newtitle or title}
 screens.append(item)
mods=[{'id':'recepcao','name':'Recepção','where':'Desktop Windows','start':'dia','intent':'Da marcação ao recebimento, com o paciente identificado.'}, {'id':'clinico','name':'Consultório','where':'Desktop Windows','start':'sessao','intent':'Sessão, prontuário e documentos no mesmo contexto.'}, {'id':'faturamento','name':'Faturamento','where':'Desktop Windows','start':'retornotiss','intent':'Da guia à resposta da operadora, com estados explícitos.'}, {'id':'financeiro','name':'Financeiro','where':'Desktop Windows','start':'contas','intent':'Caixa, obrigações e conferência do dinheiro.'}, {'id':'gerente','name':'Gerente','where':'Desktop Windows','start':'direcao','intent':'Direção, relacionamento e administração da suíte.'}, {'id':'crm','name':'CRM','where':'Web · central de conversas','start':'conversa','intent':'Conversa, agenda e próxima tarefa conectadas.'}, {'id':'preservado','name':'Web preservada','where':'Fora desta proposta de mudança','start':'portal','intent':'Portal preservado. Site e consulta web também sem redesenho nesta entrega.'}]
def extract(path):
 raw=git('show',sha+':'+path).decode('utf-8-sig')
 try: root=ET.fromstring(raw)
 except ET.ParseError:return {'actions':[],'tabs':[],'columns':[],'labels':[]}
 result={'actions':[],'tabs':[],'columns':[],'labels':[]}
 for e in root.iter():
  kind=e.tag.split('}')[-1]
  attr='Content' if kind in ('Button','CheckBox','RadioButton','MenuItem') else 'Header' if kind in ('TabItem','DataGridTextColumn','DataGridTemplateColumn','DataGridCheckBoxColumn') else 'Text'
  val=e.attrib.get(attr,'').strip()
  if not val or val.startswith('{') or len(val)>190 or any(0xe000<=ord(c)<=0xf8ff for c in val):continue
  key='actions' if kind in ('Button','MenuItem') else 'tabs' if kind=='TabItem' else 'columns' if kind.startswith('DataGrid') else 'labels'
  if val not in result[key]:result[key].append(val)
 result['url']=f'https://github.com/lucaszaous-creator/Clinica/blob/{sha}/{path}'
 return result
cache={}
for sc in screens:
 if sc['source']:
  cache.setdefault(sc['source'],extract(sc['source']));sc['current']=cache[sc['source']]
 else:sc['current']={'actions':[],'tabs':[],'columns':[],'labels':[]}
# Full source inventory stays navigable; resources never masquerade as independent screens.
catalog=[]
for src in surfaces:
 item={k:src[k] for k in ('id','repo','arquivo','dominio','recurso','achados_relacionados')}
 if src['repo']=='Clinica' and src['arquivo'] in paths and src['arquivo'].endswith('.xaml'):
  cache.setdefault(src['arquivo'],extract(src['arquivo']));item['current']=cache[src['arquivo']]
 else:
  controls=[c for c in read('inventario.json')['controles'] if c['repo']==src['repo'] and c['arquivo']==src['arquivo']]
  item['current']={'actions':list(dict.fromkeys(c['rotulo'] for c in controls if c['tipo'].lower() in ['button','a'] and c['rotulo'] and not c['rotulo'].startswith('{'))),'tabs':[],'columns':[],'labels':list(dict.fromkeys(c['rotulo'] for c in controls if c['rotulo'] and len(c['rotulo'])<140 and not c['rotulo'].startswith('{')))[:90]}
 item['screens']=[s['id'] for s in screens if s['source']==src['arquivo'] and src['repo']=='Clinica']
 item['status']='Preservado' if src['repo']=='clinica-site' or '/Clinica.Web/' in src['arquivo'] else 'Recurso compartilhado' if src['recurso'] else 'Comparação visual' if item['screens'] else 'Referência de componente'
 catalog.append(item)
shared={'clinico':['grade','semana','pacientes','ficha','autorizacoes','termos','privacidade'], 'financeiro':['pacotes','precos'], 'gerente':[s['id'] for s in screens if s['module'] in ['recepcao','clinico','financeiro','faturamento']]}
data={'main':sha,'date':'01/10/2026','modules':mods,'screens':screens,'shared':shared,'catalog':catalog,'findings':findings,'navigation':nav,'bases':read('bases.json'),'limits':'Antes: reconstrução funcional do código, não captura de produção. Depois: proposta navegável, com dados fictícios. Não substitui verificação de regras e permissões no produto.'}
(OUT/'data.js').write_text('window.PROPOSAL = '+json.dumps(data,ensure_ascii=False,separators=(',',':'))+';\n',encoding='utf-8')
logo=REPO/'src/Clinica.Desktop.Shell/Assets/Marca/logo-cor.png'
if not (OUT/'logo.png').exists():shutil.copyfile(logo,OUT/'logo.png')
(OUT/'coverage.json').write_text(json.dumps({'main':sha,'screens':len(screens),'modules':len(mods),'inventory':len(catalog),'byModule':dict(collections.Counter(s['module'] for s in screens))},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'screens':len(screens),'sources':len(cache),'catalog':len(catalog),'byModule':dict(collections.Counter(s['module'] for s in screens))},ensure_ascii=False))
