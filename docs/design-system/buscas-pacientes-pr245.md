# Buscas de pacientes — PR 245

Revisão de 08/10/2026, ampliando o contraste para a composição de marcar horário, registrar chegada e localizar pacientes nos módulos.

## Apresentação

- Busca de nome/CPF ocupa uma linha própria, com contorno visível, lupa e resultados em linhas de pelo menos 48 px.
- O dropdown duplicado é substituído pela escolha explícita na lista. O select permanece oculto como adaptador do contrato `data-*`; IDs e autorização continuam no host.
- A identidade escolhida aparece com nome por extenso, marca de confirmação e ação “Trocar paciente”. Consultar outro nome não altera a seleção atual.
- Telas que já possuem uma tabela com ações de escolher/abrir paciente conservam documento, telefone e comandos da tabela. Não há uma segunda lista concorrente. Espelhos de apenas Nome, sem ações ou campos, são omitidos quando existe o seletor.
- Setas, Home/End e Escape navegam nos resultados; Escape retorna à busca. Resultados extensos têm rolagem e expansão explícita. Carregamento, ausência de resultados e erro são apresentados.
- Agendamento reúne identificação, CPF/telefone, convênio e carteirinha antes de data, horário e profissional. A orientação do host e os avisos de conferência continuam disponíveis.
- Na agenda do dia, a busca tem mais espaço. Quando o host informa “Chegou” como próximo passo, o comando `Avancar` recebe o rótulo “Registrar chegada”. Após confirmar, a situação passa a “No local”.

## Estado assíncrono

A seleção anterior podia ficar disponível durante uma nova consulta, antes de a lista atual chegar. `Buscando` passa a incluir o debounce. O contrato compartilhado envia termo confirmado, carregamento, erro e nome do paciente efetivamente selecionado.

O componente aguarda a resposta do termo atual e a conclusão da consulta para liberar resultados. Digitar não seleciona; o host continua resolvendo a opção na lista autorizada. A identidade previamente confirmada permanece visível, inclusive quando a nova busca não encontra ninguém.

## Cobertura

| Módulo | Integração e evidência |
|---|---|
| Recepção | Agendamento, diálogo de horário, documentos, cadastro/prontuário e agenda do dia. Busca, escolha, gravação/cancelamento e chegada exercitadas pelo DOM em WebView2. |
| Clínico | Mesmos componentes no prontuário, receitas e infusão; escolha por nome/CPF exercitada no QA de buscas. Enfermagem com `Escolhido` usa o componente apenas quando acompanhada de `Seletor.Termo`. |
| Financeiro | Frontend independente e versão integrada importam os componentes compartilhados. Diálogo de lançamento exercita CPF, resultado visível, escolha e persistência do vínculo. |
| Gerente | Executável composto conferido com a mesma SuiteWebWindow; busca/seleção no agendamento e no diálogo Novo prontuário, cancelamento sem gravação, além do QA das páginas. |
| Faturamento | Shell e diálogos compartilhados; filtros existentes preservados e QA das páginas/fluxos do módulo. |

Cobertura de integração não equivale a inspecionar visualmente todas as combinações de dados e permissões. As buscas digitadas foram exercitadas especificamente nos fluxos indicados; os demais módulos também recebem a camada comum e têm suas próprias rotinas de QA.

## Evidências

- `artifacts/buscas-final-recepcao.log`: digitação, CPF, teclado, resultado vazio mantendo a identidade escolhida, resposta fora de ordem, escolha/cancelamento de prontuário, salvar/cancelar horário, filtros e registro único da chegada.
- `artifacts/buscas-final-financeiro.log`: páginas, diálogos e vínculo de paciente no lançamento pelo resultado visível.
- `artifacts/buscas-final-gerente.log`, `artifacts/buscas-final-clinico.log`, `artifacts/buscas-final-faturamento.log`: QA dos demais módulos.
- Compilação TypeScript/Vite dos dois frontends, verificação dos recursos publicados, compilação-sombra, verificação da suíte e testes de domínio/serviços.

Dados de teste são sintéticos. Os cinco QAs de interface finalizaram com código 0. A verificação da suíte e a compilação-sombra passaram; os 2.985 testes de domínio e serviços passaram, sem falhas ou testes ignorados, conforme `artifacts/testes-buscas.log`.

## Revisão solicitada ao Jev

Consulta real ao modelo `jev-1.13.0`, com fontes/recortes e logs sintéticos. O Jev não recebeu capturas nem executou o aplicativo. O resultado significa cobertura dentro desse contexto, sem certificação visual de todas as telas.

| Módulo | Parecer final | Confiança retornada |
|---|---|---:|
| Recepção | coberto_com_limites | 1,00 |
| Clínico | coberto_com_limites | 0,83 |
| Financeiro | coberto_com_limites | 0,99 |
| Faturamento | coberto_com_limites | 0,92 |
| Gerente | coberto_com_limites | 0,99 |

O primeiro pedido retornou evidência insuficiente para Gerente (confiança 0,48). A complementação acrescentou o ponto de entrada real do executável, composição do shell e teste de busca/seleção executado dentro da janela do Gerente. Nenhum código de produção mudou entre as duas consultas.

Respostas e hashes dos pedidos: [primeira consulta](buscas-jev-pr245.json) e [complementação do Gerente](buscas-jev-gerente-pr245.json). A evidência adicional está em `artifacts/buscas-gerente-integracao.log`; é reproduzível com `dotnet run --project tools/validar-suite-web/Qa.csproj -c Release -- --buscas-gerente` e também integra `--web`.



## Capturas do aplicativo

Capturadas em WebView2 com a versão recompilada e dados sintéticos:

- [Agendamento: resultados](evidencias-pr245/agendamento-busca.png).
- [Agendamento: paciente confirmado](evidencias-pr245/agendamento-paciente.png).
- [Busca sem resultado preserva a identidade anterior](evidencias-pr245/busca-sem-resultado.png).
- [Chegada registrada: No local](evidencias-pr245/chegada-confirmada.png).
- [Financeiro: paciente vinculado no diálogo](evidencias-pr245/financeiro-paciente.png).
- [Gerente: busca no agendamento](evidencias-pr245/gerente-agendamento.png).
- [Gerente: diálogo de paciente](evidencias-pr245/gerente-dialogo.png).
