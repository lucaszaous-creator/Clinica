# Contraste operacional — PR 245

Revisão de 08/10/2026 motivada pelo relato de campos aparentemente invisíveis. A camada `cores-semdor.css`, importada por último nos dois frontends, alcança Recepção, Clínico, Faturamento, Gerente e Financeiro (integrado e independente). Os recursos em `wwwroot` foram recompilados.

## Alterações

- Campos, buscas, seletores, editor rico e seleção de mês com contorno cinza visível em repouso. Rótulos, conteúdo, placeholders e setas usam os neutros existentes.
- Desabilitado e somente leitura distinguem-se por fundo e borda tracejada; erro e foco têm tratamento explícito. Checkbox mantém marca de seleção e alternativa nativa em cores forçadas.
- KPIs com hierarquia de número, rótulo e detalhe; situação funcional preservada. Curvas e legendas financeiras usam a mesma cor, evitando discrepância entre ponto e curva.
- Filtros ativos com azul sólido e texto branco; linha selecionada com marca lateral; cabeçalho ordenado com indicador visível.
- Menu superior plano, sem o destaque em cápsula rejeitado pelo proprietário. Aba ativa com linha reta de 2 px; teclado e submenus preservados.

## Medição no navegador

Medição de cores computadas dos componentes React, com dados fictícios:

| Par | Contraste |
|---|---:|
| Contorno Cinza.500 / branco | 4,83:1 |
| Placeholder Cinza.500 / branco | 4,83:1 |
| Rótulo Cinza.700 / branco | 10,31:1 |
| Número Cinza.900 / branco | 17,74:1 |
| Texto branco / filtro azul ativo | 9,88:1 |

Conferidos campo vazio/preenchido, seleção de convênio, erro, somente leitura, desabilitado, checkbox, busca sem acentos, limpeza e combinação com situação. No Financeiro: resumo, busca de movimentos, limpeza e navegação plana (`background: transparent`, `border-radius: 0`, linha de 2 px). Estas medidas são dos elementos amostrados; não constituem certificação de acessibilidade integral.

## Jev

Consulta efetiva ao `jev-1.13.0`, solicitada pelo proprietário. Cinco pedidos separados incluíram o CSS final integral, declarações visuais das camadas anteriores, integração/empacotamento e evidências disponíveis de testes sintéticos. Os recortes das camadas anteriores não incluem todas as propriedades de layout nem contextos de media queries. Não foram enviadas imagens, credenciais ou dados reais de pacientes.

| Módulo | Resultado retornado | Confiança retornada |
|---|---|---:|
| Recepção | coberto_com_limites | 0,65 |
| Clínico | coberto_com_limites | 0,49 |
| Faturamento | coberto_com_limites | 0,64 |
| Gerente | coberto_com_limites | 0,51 |
| Financeiro | coberto_com_limites | 0,45 |

A resposta desta primeira rodada está versionada em [contraste-jev-pr245.json](contraste-jev-pr245.json). Pedidos completos estão em `artifacts/jev-contraste/`. As confianças moderadas e os limites de contexto impedem tratar o resultado como garantia de todas as telas. O Jev complementa os testes e a inspeção das capturas; não executou o aplicativo.

## Validação executada

- TypeScript e build Vite dos dois frontends.
- 2.985 testes de domínio/serviços aprovados; zero falhas.
- Verificador da suíte: 225 XAML, 10 projetos e 143 construtores. Compilação-sombra: 11 projetos WPF.
- Recepção, `qa/recepcao-web --buscas`: busca digitada, seleção/troca de paciente, respostas atrasadas, cancelamento, permissões, filtros combinados e agenda densa em 1366/900 px. Logs em `artifacts/buscas-final-recepcao.log`; capturas em `artifacts/recepcao-buscas/`.
- Gerente e Financeiro integrado, `tools/validar-suite-web --web`: páginas em 1440/1100/900 px. Log `artifacts/buscas-final-gerente.log`.
- Financeiro independente, `tools/validar-design-financeiro --web`: 15 páginas em três dimensões, formulários, gravação/cancelamento, busca e seleção pelo DOM e revogação de acesso. Log `artifacts/buscas-final-financeiro.log`.
- Clínico, `tests/Clinica.Clinico.Web.Qa --web`: contratos, fluxos e páginas em 1440/1044/900 px, incluindo gesto e cancelamento no mapa corporal. Log `artifacts/buscas-final-clinico.log`.
- Faturamento, `tools/validar-faturamento-web --web`: consultas, baixas, glosas, cancelamento, assinatura e páginas em três dimensões. Log `artifacts/buscas-final-faturamento.log`.

Os testes de interface usam WebView2 real e banco sintético. A rodada final e as capturas do redesenho das buscas estão documentadas em [buscas-pacientes-pr245.md](buscas-pacientes-pr245.md). Sem merge, release ou publicação em produção.
