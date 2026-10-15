"""Consolida evidência já medida; não executa consultas nem altera aplicações."""
from pathlib import Path
import json, statistics

raiz=Path(__file__).resolve().parents[2]
base=raiz/'artifacts/buscas-postgres'
sql=json.loads((base/'selecionados/resultado.json').read_text())
antes=json.loads((base/'telas/resultado.json').read_text())['resultados']
depois=json.loads((base/'telas-virtualizadas/resultado.json').read_text())['resultados']
assert len([r for r in antes if r['fase']=='antes'])==72
assert len(depois)==216
assert len(sql['resultados'])==200
def tela(dados,nome,paciente,atraso,fase='antes'):
    valores=[r['telaProntaMs'] for r in dados if (r['tela'],r['paciente'],r['atrasoPorComandoMs'],r['fase'])==(nome,paciente,atraso,fase)]
    assert len(valores)==3
    return statistics.median(valores)/1000
def consulta(nome,fase):
    return next(r['MedianaMs'] for r in sql['resultados'] if r['Cenario']==nome and r['Fase']==fase)
linhas=['# PostgreSQL e telas Windows — ensaio de 14/10/2026','',
'Foram medidos serviços/repositórios reais e telas WPF/WebView2 reais contra PostgreSQL 16.15 local, com 50.000 pacientes fictícios e 200.000 registros por tabela clínica principal. Um paciente tem quatro registros por histórico; o cenário extenso tem 3.000. Não foram utilizados dados de pacientes reais nem alterada a produção. A referência de código é 0339b1978783f2fa7855ab86ae8366898fa1bc66, na branch de testes baseada na main; não é uma cronometragem do executável instalado em produção.','',
'## Resultado nas telas','',
'Medianas de três aberturas por cenário, em segundos. “250 ms” significa atraso artificial por comando EF, não latência medida na VPS. A primeira inicialização de componentes está incluída nas amostras; a mediana reduz sua influência.','',
'| Tela / cenário | Antes, local | Só virtualização, local | Antes, 250 ms | Só virtualização, 250 ms | Virtualização + 4 índices, 250 ms |',
'|---|---:|---:|---:|---:|---:|']
for rotulo,nome,paciente in [('Busca por nome raro','pacientes-zefirina',0),('Primeira página de pacientes','pacientes-lista',0),('Documentos, 3 mil registros','documentos',1),('Infusões, últimos 50','infusoes',1),('Prontuário, 4 evoluções','prontuario',10001),('Prontuário, 3 mil evoluções','prontuario',1),('Ficha clínica, histórico extenso','ficha',1)]:
    valores=[tela(antes,nome,paciente,0),tela(depois,nome,paciente,0),tela(antes,nome,paciente,250),tela(depois,nome,paciente,250),tela(depois,nome,paciente,250,'indices')]
    linhas.append('| '+rotulo+' | '+' | '.join(f'{v:.3f}' for v in valores)+' |')
linhas+=['','A mudança no aplicativo está em `ProntuarioClinicoView.xaml`: o painel passou a virtualizar e reciclar os controles conforme a rolagem. O histórico não foi truncado e não foi acrescentada uma camada para esconder o carregamento. Foram conferidos os 3.000 registros, a última linha por rolagem, a busca e a limpeza dos filtros. A confirmação automatizada registrou quatro containers inicialmente, em vez de criar todos de uma vez.','',
'A ficha continua com 28 comandos EF nas repetições aquecidas (29 quando precisa gravar a auditoria). A simulação confirma a sensibilidade às viagens ao banco, mesmo quando suas consultas individuais são rápidas. Foram encontrados comandos iguais com parâmetros iguais: leitura de paciente três vezes, autorizações duas vezes e problemas clínicos duas vezes. Isso identifica onde consolidar as leituras; esta alteração visual não eliminou essas consultas. Não se deve apagar a auditoria para diminuir a contagem.','',
'Documentos ainda carrega 3.000 linhas e o prontuário ainda lê todas as evoluções. A virtualização corrige a criação de controles; paginação/projeções de leitura ainda podem reduzir volume transferido. O ganho visual não deve ser apresentado como correção de todos os gargalos.','',
'## PostgreSQL: somente os quatro índices selecionados','',
'Métodos reais do repositório, cache aquecido, conexão do pool aberta antes do cronômetro; 15 amostras e dois aquecimentos por cenário/fase. A tabela usa a repetição com índices e a referência posterior sem eles, evitando atribuir ao índice o aquecimento inicial. A-B-A preservou IDs e ordem em 40 cenários.','',
'| Consulta | Sem candidatos, ms | Com quatro índices, ms |',
'|---|---:|---:|']
for rotulo,nome in [('Nome raro','nome-raro'),('Nome comum','nome-comum'),('Primeira página','lista-inicial'),('CPF inteiro','cpf-inteiro'),('Receitas do histórico extenso','receitas-paciente-1'),('Todos os documentos do histórico extenso','documentos-paciente-1'),('Evoluções do histórico extenso','evolucoes-paciente-1')]:
    linhas.append(f'| {rotulo} | {consulta(nome,"antes2"):.2f} | {consulta(nome,"completo2"):.2f} |')
custo=next(c for c in sql['custos'] if c['fase']=='completo2')
referencia=next(c for c in sql['custos'] if c['fase']=='antes2')
linhas+=['',
'Selecionados para proposta manual: GIN de `lower(Nome)`; GIN de `Documento`; B-tree de `(Nome, Id)`; B-tree de documento por `(PacienteId, Tipo, Data DESC, Id DESC)`. O teste inicial incluiu dez candidatos; a repetição com somente quatro preservou os ganhos relevantes sem os outros seis. A decisão é específica deste fixture, não uma rejeição geral desses índices.','',
f'Os quatro índices ocuparam {sum(custo["indices"].values())/1024/1024:.2f} MiB no fixture após o ensaio. A inserção sintética de mil pacientes teve mediana de {referencia["escrita1000P50Ms"]:.2f} ms sem candidatos e {custo["escrita1000P50Ms"]:.2f} ms com índices. Esse teste usa rollback e não representa custo de escrita em documentos, WAL sustentado ou concorrência real.','',
'A busca digitada inclui o debounce do aplicativo. Por isso a redução de milissegundos na consulta não se transforma no mesmo fator de redução no tempo da tela. Termos sem trigramas úteis, como um ou dois caracteres, precisam de avaliação própria. Não foram alteradas as regras de acentos, filtros, permissões ou ordenação.','',
'## Limites e pendências','',
'- Máquina local: Ryzen 5 5500, 12 processadores lógicos, cerca de 16 GiB de RAM; PostgreSQL 16.15, shared_buffers de 128 MiB. Não representa a capacidade da VPS.','- Os cenários não são ensaio concorrente de vários postos. Atraso artificial não simula banda, TLS, pool remoto, contenção, antivírus ou carga do site na VPS.','- As janelas de teste ficam fora da área visível. Prontidão significa resultado no modelo e layout/etapa Render do WPF; no WebView também exige linhas no DOM e dois quadros de animação. Não mede a apresentação física do monitor.','- O instante “dados prontos” inclui serviços, materialização e observação do modelo. O tempo até obter um reader não equivale ao tempo de transferência completa. Comandos paralelos não podem ser somados como decomposição do tempo total.','- A referência visual teve 72 medições completas; depois da correção foram 216, com e sem índices e reversão. A rodada preliminar que esperava ApplicationIdle foi descartada. Três amostras permitem uma comparação exploratória por mediana, não um p95 confiável de telas.','- O ensaio não confirma a causa da tela preta em Agenda/Pedidos da Enfermagem no computador da clínica. Ele demonstrou e corrigiu um gargalo diferente, no histórico extenso.','- Falta medição ponta a ponta em homologação representativa ou no posto com acesso autorizado. Não se deve recomendar upgrade da VPS apenas com estes números. Nenhum índice foi aplicado em produção.','',
'## Evidência e reprodução','',
'- Ferramentas: `tools/BenchmarkBuscasPostgres` e `tools/BenchmarkTelasPostgres`; instruções e limites no README da primeira.','- Dados brutos: `artifacts/buscas-postgres/resultado.json`, `selecionados/resultado.json`, `telas/resultado.json` e `telas-virtualizadas/resultado.json`.','- Planos SQL: arquivos `*-plano.json` nas pastas do benchmark.','- Validação de rolagem/filtros: `telas-virtualizadas/validacao-historico.log`.','- Consultas ao Jev: `jev-plano-resultado.json`, `jev-plano-telas-resultado.json`, `jev-renderizacao-resultado.json` e revisão final. A revisão não substitui o ensaio e não autoriza produção.','- SQL revisável e reversão: `tools/BenchmarkBuscasPostgres/indices.sql`; separado das migrations automáticas.','']
(raiz/'docs/desempenho-postgres-telas-2026-10-14.md').write_text('\n'.join(linhas),encoding='utf-8')
resumo=json.loads((base/'resumo.json').read_text())
selecionados=json.loads((base/'selecionados/resumo.json').read_text())
jev=json.loads((base/'jev-resultado-resultado.json').read_text())
evidencias=raiz/'docs/desempenho-postgres-telas-2026-10-14'
evidencias.mkdir(exist_ok=True)
(evidencias/'medicoes.json').write_text(json.dumps({'sql':selecionados,'telas_antes':resumo['telas'],'telas_virtualizadas':resumo['telas-virtualizadas'],'jev':jev},ensure_ascii=False,indent=2),encoding='utf-8')
with (raiz/'docs/desempenho-postgres-telas-2026-10-14.md').open('a',encoding='utf-8') as f:
    f.write('\nA revisão final do Jev classificou as conclusões como adequadas. A confiança informada foi 0,97 para a interpretação limitada dos ganhos, 0,46 para os índices, 0,55 para as telas e 0,91 para manter produção pendente. É uma revisão das evidências enviadas, não uma execução independente dos testes. O retorno está preservado, com os resumos numéricos, em `docs/desempenho-postgres-telas-2026-10-14/medicoes.json`.\n')
print('Relatório consolidado.')
