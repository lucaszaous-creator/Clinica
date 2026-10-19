# Proposta interativa por módulo — Antes e Depois

**Portal web preservado integralmente.** A [restrição de escopo](ESCOPO-E-PROPOSTAS-VISUAIS.md) continua obrigatória. Esta apresentação não modifica nenhum produto ou ambiente publicado.

[Abrir a apresentação local](comparativo/index.html) · [Recepção](comparativo/comparacao-recepcao.png) · [Consultório](comparativo/comparacao-clinico.png) · [Faturamento](comparativo/comparacao-faturamento.png) · [Financeiro](comparativo/comparacao-financeiro.png) · [Gerente](comparativo/comparacao-gerente.png) · [CRM](comparativo/comparacao-crm.png)

O GitHub exibe a fonte HTML; para interagir, abra `index.html` localmente mantendo `app.js`, `data.js`, `styles.css` e `logo.png` na mesma pasta. A apresentação funciona sem servidor e não envia dados.

## Como ler o Antes e o Depois

- **Antes:** reconstrução funcional baseada em rótulos, ações, campos e fluxos encontrados no código. É um leiaute esquemático, **não uma captura do aplicativo real nem uma reprodução exata de sua aparência**.
- **Depois:** proposta navegável, com exemplos fictícios e estados simulados. Onde não há mudança específica, as funções aparecem como preservadas.
- **Lado a lado:** o mesmo cenário com a situação existente e a proposta. Em telas estreitas, as versões ficam uma abaixo da outra; também é possível abrir uma de cada vez.
- **Diferenças:** situação atual, proposta e o que permanece. O detalhe abre os achados, fontes e critérios de aceite.
- **Funções atuais e fontes:** ações, abas, colunas e rótulos extraídos da fonte. Inclui controles de estados secundários; não afirma que todos aparecem ao mesmo tempo.
- **Inventário completo:** todas as 263 referências da auditoria, pesquisáveis por sistema, nome, domínio e achado. Componentes e recursos são identificados como tais.

## Cobertura

Há **97 cenas comparativas e 3 referências preservadas**, organizadas por módulo. As funções compartilhadas reaparecem no contexto pertinente, sem serem contadas novamente. O Gerente também permite explorar os módulos que compõe.

| Grupo | Cenas próprias | O que inclui |
| --- | ---: | --- |
| Recepção desktop | 22 | Dia, grade, semana, marcação, vagas, espera, confirmações, retornos, lançamento, conferência, convênio, pacientes, cadastro, ficha, autorizações, documentos, recebimentos, pacotes, termos, privacidade, acompanhamento e equipe |
| Consultório desktop | 18 | Meu dia, sessão, evoluções pendentes, prontuário, anamnese, problemas, medidas, dor, avaliações, acompanhamento, exames, receitas, emissão, prescrição de infusão, sala, execução, passagens e números |
| Faturamento desktop | 11 | Resumo, pendências e baixas, guias, faturados, glosas, não conformidades, TISS, retorno, rodada, relatórios e regras |
| Financeiro desktop | 16 | Caixa, fechamento, fluxo, contas, recorrências, inadimplência, plano de contas, cartão, conciliação, banco, taxas, repasses, produção, resultado, estoque e Pix |
| Gerente desktop | 17 | Direção, indicadores, metas, campanhas, retenção, origens, preços, rentabilidade, custos, configurações, modelos, acessos, auditoria, documentos, guarda e importação |
| Funções compartilhadas | 3 | Ajuda, treinamento e pesquisa de seções |
| CRM web | 10 | Conversas, meu trabalho, supervisão, contatos/funil, tarefas, agenda, recall, operação, relatórios e configurações/IA |
| Superfícies preservadas | 3 | Portal web, web de consulta e site institucional, sem redesenho nesta entrega |

**Limite de cobertura:** os 263 arquivos inventariados não são 263 telas. Janelas auxiliares, estilos e componentes sem cena própria têm uma ficha de referência; isso não equivale a um desenho detalhado de todos os seus estados. O protótipo compara a organização e os fluxos, sem implementar regras clínicas/financeiras ou reproduzir todas as combinações de perfil e dados. A matriz de 35 combinações de aplicativo/perfil continua sendo a referência de permissões, não o menu consolidado do explorador visual.

## Diferenças que podem ser experimentadas

| Módulo | Antes | Depois | Como experimentar |
| --- | --- | --- | --- |
| Recepção / Documentos | Impressão em menu secundário; recibo depende de um destino ausente | Imprimir visível; recibo abre recebimento | Abrir Documentos e clicar em Recibo ou na impressão |
| Consultório | Salvar sessão já conclui a sessão médica; interrupção da emissão deixa risco de repetir | Efeito explícito no botão; resultado do documento com retomada pelo mesmo número | Escrever uma evolução, consultar contexto, alternar modo e concluir; emitir e retomar documento |
| Faturamento | Guia ausente no retorno conserva aceite padrão | Sem resposta separado de aceitas e glosadas | Abrir Retorno do lote; comparar as duas guias ausentes |
| Financeiro | Adiar 7d fixa o intervalo | Data negociada e motivo, com +7 dias como sugestão | Abrir Contas, escolher Depois e adiar para outra data |
| Gerente | Abrir WhatsApp registra envio | Abertura e confirmação manual separadas | Abrir Campanhas, simular abertura e depois confirmar envio |
| CRM | Conclusão troca a lista; erro de atualização fica em diálogo fechado | Lista preservada, conclusão e atualização com estados próprios | Concluir conversa com a opção de falha de atualização e tentar atualizar |

Outras diferenças estão nos respectivos cenários, incluindo devolução vinculada, data real de recebimento, XML original versus nova versão, validação XSD explícita, cadastro por entidade, nomes canônicos e retorno ao contexto.

Mudanças de regra, como substituir a rodada bloqueante de faturamento, permanecem apresentadas como propostas condicionadas a validação operacional. Não são tratadas como regras aprovadas ou já implementadas.

## Bases verificadas

- **Desktop:** `origin/main` atualizado e verificado em `cbde7e92075109de3a6a9f2415b6c84052bd136a`, incluindo a PR 231. Rótulos de 220 fontes XAML foram lidos dessa revisão. O documento único de infusão é identificado como função existente.
- **CRM:** `origin/main` verificado em `33c9ffcb836286c27a61bf1f91640a62acb5b70c`, a mesma revisão da auditoria.
- **Portais/site:** referências históricas preservadas; nenhuma mudança ou implantação realizada.
- **Achados:** os 70 registros editoriais da PR 232 continuam rastreáveis. A apresentação aplica o limite posterior que excluiu mudanças no portal.

## Verificação realizada

- Todas as 100 rotas abriram sem erro JavaScript e sem transbordamento horizontal da página, em larguras de 1600 e 390 pixels.
- Onze verificações de interação passaram: rascunho ao abrir contexto e alternar modo; data escolhida no adiamento; WhatsApp sem envio presumido; confirmação manual; conclusão separada de falha de atualização; retomada sem repetir conclusão; guia ausente como sem resposta; emissão sem nova ação de reemitir; portal sem redesenho; inventário com 263 referências.
- Capturas por módulo foram renderizadas e inspecionadas. A comparação do Consultório também foi conferida em tela estreita.
- Jev revisou por API as descrições dos seis módulos e do escopo, classificando os sete itens como coerentes. [Pedido](comparativo/jev-pedido.json) e [resposta sanitizada](comparativo/jev-revisao.json). Foi revisão textual, não inspeção das imagens nem teste do produto.

Os exemplos simulam ações localmente. Não consultam banco, não gravam pacientes, não imprimem documentos reais, não acionam certificados e não enviam mensagens.
