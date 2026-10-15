"""Gera fichas de cobertura e a prestação de contas das avaliações do Jev."""
import collections,json
from pathlib import Path
b=Path(__file__).resolve().parents[1]/'docs/auditoria-global-2026-10-01'
d=json.loads((b/'inventario.json').read_text(encoding='utf-8'))
fs=json.loads((b/'evidencias.json').read_text(encoding='utf-8'))
votes={}
for p in sorted((b/'jev').glob('*-resultado.json')):votes.update(json.loads(p.read_text())['answers'])
def domain(s):
    p=s['arquivo']
    if s['repo']=='semdor-crm':return 'CRM','Conservar conversa/paciente confirmado, filtro da fila, estado do envio e próximo responsável.'
    if s['repo']=='clinica-site':
        if 'profissional' in p:return 'Portal profissional','Preservar paciente e sessão, distinguir salvar/concluir/assinar e oferecer retomada da mesma operação.'
        if p.startswith('portal/'):return 'Coleta de termos','Distinguir equipe e paciente, identidade, assinatura recebida e arquivamento; retomar sem nova emissão.'
        return 'Site institucional','Identificar finalidade, manter destino canônico e facilitar contato sem prometer confirmação automática de horário.'
    if '/Styles/' in p or p.endswith('/App.xaml'):return 'Recurso compartilhado','Verificar seus consumidores antes de consolidar; este arquivo não é uma tela independente.'
    for key,label,advice in [('Recepcao','Recepção','Manter contexto de paciente/horário e oferecer continuação nos estados vazios, com permissões do balcão.'),('Clinico','Clínico','Separar sessão e histórico longitudinal; manter autoria, estado de salvamento e contexto do paciente.'),('Faturamento','Faturamento','Preservar guia/lote e período entre consulta, correção, retorno e recurso; distinguir ausência de resposta de aceite.'),('Financeiro','Financeiro','Explicitar valor, data efetiva e origem do movimento; preservar vínculo, conferência e possibilidades de correção.'),('Gerente','Gestão','Separar indicador, operação e cadastro; levar do alerta ao registro correto com filtros e permissão.'),('Clinica.Web','Web de leitura','Explicitar recorte e limites; permitir continuidade autorizada sem tornar leitura em alteração.')]:
        if key in p:return label,advice
    return 'Componentes e shell','Confirmar quem abre o componente, a entidade em contexto, a saída/cancelamento e o resultado da ação; compartilhar sem duplicar a regra.'
lines=['# Revisão por superfície','',
       'Uma ficha por arquivo identificado na extração. A triagem do Jev recebeu os controles relevantes de todos os 263 arquivos. A revisão editorial aprofundou os fluxos descritos nos achados; **não houve execução de cada tela nem leitura manual integral de todos os arquivos**. As recomendações de domínio abaixo são roteiros de melhoria/verificação, não defeitos adicionais contabilizados. Estilos e App.xaml permanecem no inventário para não serem confundidos com telas perdidas.','',
       'Para cada rótulo, campo e ação individual, consulte [CATALOGO.md](CATALOGO.md) e [controles.csv](controles.csv). Para implementações, consulte [funcoes.csv](funcoes.csv). Ausência de achado específico não significa aprovação da interface.','']
rows=[]
for i,s in enumerate(d['superficies'],1):
    ident=f'S{i:03}';p=s['arquivo'];repo=s['repo'];dom,advice=domain(s)
    controls=[c for c in d['controles'] if (c['repo'],c['arquivo'])==(repo,p)]
    labels=list(dict.fromkeys(c['rotulo'] or c['acao'] for c in controls if c['tipo'] in ('Button','button','TabItem','MenuItem','a','h1','h2') and (c['rotulo'] or c['acao'])))
    related=[f['id'] for f in fs if any(e['repo']==repo and (e['arquivo']==p or Path(e['arquivo']).stem.replace('ViewModel','').replace('View','')==Path(p).stem.replace('View','')) for e in f['evidencias'])]
    v=votes[ident];url=f'https://github.com/lucaszaous-creator/{repo}/blob/{d["bases"][repo]["commit"]}/{p}'
    support=dom=='Recurso compartilhado'
    row={**s,'id':ident,'dominio':dom,'recurso':support,'controles':len(controls),'achados_relacionados':related,'triagem_jev':v,'recomendacao_de_dominio':advice};rows.append(row)
    lines += [f'## {ident} — {repo} / {p}','',f'**Domínio:** {dom}. **Natureza:** '+('recurso/composição, não página autônoma.' if support else 'arquivo de interface/template; alcance depende de composição e estado.'),
              f'**Origem:** [código congelado]({url}) · {s["linhas"]} linhas · {len(controls)} controles extraídos.','',
              '**Abas/ações/títulos identificados:** '+('; '.join(x.replace('\n',' ') for x in labels) if labels else 'nenhum rótulo de ação extraído; examinar bindings, composição e consumidores.')+'.','',
              '**Achados relacionados por arquivo/nome de ViewModel:** '+(', '.join(f'[{x}](ACHADOS.md#{x.lower()})' for x in related) if related else 'nenhum específico consolidado; não equivale a ausência de problema.')+'.','',
              f'**Triagem Jev:** `{v["choice"]}` · confiança retornada {v["confidence"]:.2f}.','',
              '**Verificação/melhoria recomendada:** '+advice,'']
(b/'REVISAO-POR-SUPERFICIE.md').write_text('\n'.join(lines),encoding='utf-8')
(b/'cobertura-superficies.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
surface=collections.Counter(v['choice'] for k,v in votes.items() if k.startswith('S'))
finding=collections.Counter(v['choice'] for k,v in votes.items() if k.startswith('A'))
lines=['# Participação e revisão do Jev','',
       'Foi utilizado o Jev real pela API TypeSafe (`jev-1.13.0`, endpoint `/v1/systemone`), conforme solicitado pelo usuário. Não foi um agente interno com o nome Jev. A API usada devolve escolhas estruturadas e confiança; **não devolveu uma justificativa textual por escolha nesta consulta**. Nenhum raciocínio explicativo foi inventado para representar sua resposta.','',
       '## Primeira passagem: todas as superfícies','',
       'Foram enviados rótulos e vínculos de controles de 263 arquivos, em cinco lotes. O objetivo era priorizar revisão de navegação, ações e sobreposição. Recursos de estilo e bindings sem contexto podem receber insuficiente. As classes são focos de investigação, não diagnóstico de defeito.','',
       '| Classe | Arquivos |','| --- | ---: |',*[f'| {k} | {n} |' for k,n in sorted(surface.items())],'',
       'Cada voto está associado à sua superfície em [REVISAO-POR-SUPERFICIE.md](REVISAO-POR-SUPERFICIE.md). Os pedidos conservam os controles efetivamente fornecidos, permitindo conferir a cobertura.','',
       '## Segunda passagem: evidências dos 70 achados','',
       'Quatorze lotes de cinco achados levaram descrição, classe, ressalvas, proposta e trechos de código com linhas. Os trechos são limitados; matrizes de navegação e observações Git exigem evidências complementares. Foram resolvidas 146 referências de código.','',
       '| Voto | Achados |','| --- | ---: |',*[f'| {k} | {n} |' for k,n in sorted(finding.items())],'',
       '| ID | Voto | Confiança retornada | Título |','| --- | --- | ---: | --- |']
for f in fs:
    v=votes[f['id']];lines.append(f'| [{f["id"]}](ACHADOS.md#{f["id"].lower()}) | {v["choice"]} | {v["confidence"]:.2f} | {f["titulo"]} |')
lines += ['', '## Tratamento editorial das divergências','',
          '**A17: voto não sustentado.** O trecho de OrganizacaoNavegacao enviado não demonstra a divergência entre checkouts. O voto é preservado. A afirmação verificável sobre commits é sustentada pelo levantamento Git de [VERSOES-E-COBERTURA.md](VERSOES-E-COBERTURA.md) e `versoes-locais.json`, não por esse trecho. A expressão do título “sistemas diferentes” deve ser lida como **revisões diferentes do código**, não como prova de produtos diferentes implantados. O commit da produção permanece não verificado.','',
          '**A02:** a sequência persistir → falhar depois → liberar Emitindo e a ausência de guarda por DocumentoEmitidoId sustentam um caminho estático de nova emissão. Não houve reprodução em banco; o ensaio proposto continua necessário.','',
          '**A08, A10 e A22:** as conclusões dependem da composição completa; a matriz executada com declarações reais e filtro reproduzido é evidência complementar. Não são capturas de menu em produção.','',
          '**A11, A13 e A15:** a função de Documentos está no texto/código do portal; a navegação sem rota e os candidatos sem referência exigem confirmação em execução. Reflexão ou composição dinâmica podem alterar o alcance.','',
          '**A19:** a comparação Git sustenta a quantidade de cópias; não prova execução duplicada. A condição histórica indicada no próprio CRM é preservada.','',
          '**A23, A27, A28 e A63:** são propostas de organização/clareza e possibilidades de percurso. Não há medição de confusão, cliques ou frequência. A preferência precisa de validação com quem opera.','',
          '**A70:** o texto absoluto está no código, mas o julgamento de sua inadequação editorial depende dos estados e exceções descritos em outros achados; permanece prioridade de clareza.','',
          'Votos sustentados também não dispensam teste. A confiança retornada pelo modelo não é probabilidade calibrada de defeito, gravidade nem frequência. A responsabilidade pelas conclusões e recomendações deste relatório permanece editorial.','',
          '## Dados enviados e rastreabilidade','',
          'Os pedidos contêm material de código, rótulos, nomes de arquivos, commits e propostas desta auditoria. Não foi consultado nem enviado banco de pacientes. As respostas salvas foram reduzidas a modelo solicitado, escolhas, confiança, uso e SHA-256 do pedido. A chave foi lida de arquivo externo ao repositório. Não há repetição automática após falha/resultado incerto.','',
          'Todos os arquivos `*-resultado.json` têm seu pedido correspondente em `jev/`. O validador desta entrega confere hashes, conjuntos de IDs e cobertura. Alterar uma evidência exige uma nova rodada identificável, sem reaproveitar silenciosamente o voto antigo.','']
(b/'REVISAO-JEV.md').write_text('\n'.join(lines),encoding='utf-8')
print(f'{len(rows)} fichas; {sum(finding.values())} achados revisados.')
