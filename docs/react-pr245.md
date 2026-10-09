# React na suíte desktop — PR 245

A apresentação dos cinco módulos passa a usar React, TypeScript e CSS locais dentro do WebView2. O contrato de páginas, campos e comandos com o C# permanece: não há cópia de regras clínicas, financeiras, autorização ou persistência no navegador.

## Componentes e estado

- `SuiteReact` renderiza a navegação superior, pesquisa de telas, região principal, avisos e diálogos da suíte. O Financeiro independente usa shell e resumo em JSX.
- `PaginaReact`, `SecaoReact`, `CampoReact`, `IndicadoresReact`, `TabelaReact`, `ListaResponsivaReact` e `AcoesReact` compõem as telas registradas. Os campos mantêm os atributos do contrato da ponte, limites e rascunhos por contexto.
- Chaves estáveis por contexto, campo e ID de registro permitem atualizar resultados sem recriar os controles. Trocar de paciente/contexto continua limpando os rascunhos anteriores.
- Agendas especializadas, treinamento, gráficos e parte da composição clínica/marcação conservam seus geradores de apresentação, convertidos em árvores React por `HtmlReact`. Não se substitui mais o `innerHTML` do aplicativo inteiro. O editor rico é uma ilha DOM explícita para preservar seleção e formatação; canvas e câmera mantêm seus controles existentes.
- `flushSync` se restringe ao limite com a ponte imperativa: a atualização precisa estar concluída antes de ligar controles especializados e restaurar posições. Os efeitos dos componentes cuidam da sincronização dos campos.
- O Vite resolve uma única instância de React/ReactDOM em cada aplicativo, inclusive ao importar os componentes compartilhados do Financeiro.

## Composição visual

Superfícies distinguem contexto, filtros e conteúdo. O cabeçalho clínico tem contraste próprio; botões primários usam azul e estados mantêm texto junto às cores semânticas. Não há faixas decorativas nos cartões. Ações secundárias ficam em menus acessíveis, mantendo todos os comandos.

Os seletores de pacientes com contrato `Seletor.Termo`/`Seletor.Selecionado` mostram sugestões retornadas pelo host durante a digitação, sem seleção automática. É possível usar setas, Enter e Escape, ou ver todos os resultados. O seletor original permanece disponível e a escolha percorre a mesma ponte de autorização.

No resumo financeiro, o resultado ocupa uma superfície petróleo, entradas e saídas têm cores funcionais, filtros ficam agrupados e movimentos usam linhas com ações compactas. Em larguras menores, os dados ganham rótulos verticais.

As transições são curtas e aplicadas à entrada de página, abertura de diálogo/menu e interação com controles. O React conserva os elementos durante respostas da busca; essas respostas não reiniciam a animação da página. `prefers-reduced-motion` desativa movimentos e transições. Não se anima valor clínico nem se introduz atraso artificial em gravações.

## Consulta de direção

Dois agentes participaram da implementação. O Jev 1.13.0 recebeu uma consulta anterior à implementação sobre reconciliação, movimento e cor, sem dados de pacientes. Escolheu componentes reconciliados com ponte C# preservada, transições discretas e cor funcional. Pedido e resposta estão em `artifacts/jev-react/`. Essa consulta não representa inspeção nem aprovação visual das telas finais.

## Homologação

Os testes WebView2 usam dados sintéticos e incluem identidade DOM, foco/cursor, digitação sem Enter, respostas atrasadas, seleção programática, reordenação de registros, editor rico, novo contexto e movimento reduzido. Os testes funcionais existentes continuam sendo executados contra a apresentação React nos cinco módulos.

O pacote portátil `1.0.245-test.20261008.3` usa perfil de teste separado e atualização automática desativada. A PR continua em rascunho. Não há merge, release, nova migration nem envio à produção. Extraia o pacote inteiro e utilize banco de homologação conforme `teste-pr245.md`.

## Evidências desta edição

- `artifacts/testes-react.log`: 2.975 testes aprovados, zero falhas.
- `artifacts/sombra-react.log`: C# dos 11 projetos WPF compilado.
- `artifacts/qa-react-suite-final.log`: contratos 76/97, menus/teclado, identidade DOM/foco, sugestões, editor, contextos, 14 colunas sem rolagem horizontal e 197 sessões semanais.
- `artifacts/qa-react-recepcao-buscas.log` e `qa-react-recepcao-fluxos.log`: busca/seleção durante digitação, resposta fora de ordem, prontuário, agenda densa, profissionais, gravações/cancelamento e permissões.
- `artifacts/qa-react-clinico-final.log`: páginas em três tamanhos, fluxos clínicos e gesto/cancelamento no mapa corporal.
- `artifacts/qa-react-financeiro.log`: páginas/formulários, movimento reduzido, busca com foco, privacidade, menu de histórico, paciente e decimal persistidos.
- `artifacts/qa-react-faturamento.log` e `qa-react-buscas-gerais.log`: páginas/formulários, TISS, baixa/glosa/NC, buscas reais dos módulos, seletores, usuários e decimais.
- `artifacts/qa-react-gerente.log`: rotas do Gerente e Financeiro compartilhado em três tamanhos.

São cenários automatizados com banco sintético; não equivalem à inspeção manual de toda combinação de dados, perfil e equipamento, nem à homologação da clínica.
