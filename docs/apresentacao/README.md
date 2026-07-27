# Apresentação comercial — Clínica SemDor

`apresentacao-semdor.html` é a apresentação de venda/demonstração do sistema de faturamento,
em página única (HTML autocontido, sem dependências externas).

- **Design system real**: usa os tokens da marca (azul-royal `#123A9E`, símbolo navy `#07329A`,
  neutros frios, semáforo) espelhados de `src/Clinica.Desktop/Styles/Tokens.xaml`.
- **Telas reais**: mockups do shell (sidebar + topbar + breadcrumb) e das telas Pendências,
  Novo atendimento, Rodar pendências (NC), Guias TISS e Controle de glosas, com o layout e as
  colunas do app.
- **Dados de demonstração**: pacientes, guias e convênios são fictícios.

## Como usar

Abra o arquivo em qualquer navegador. Tem alternância claro/escuro no topo e é responsivo.
Para gerar um PDF, use **Imprimir → Salvar como PDF** no navegador.

## Divergências conhecidas

Material de venda **não** acompanha o código automaticamente. O mapa completo entre o que
foi prometido e o que cada módulo entrega está em
[`docs/features-por-modulo.md`](../features-por-modulo.md).

⚠️ Atenção: existe uma **segunda** peça comercial, a proposta da suíte
(`ApresentacaoSemDor.pdf`, 26 páginas), que vende 14 features em três fases. Ela não está
versionada aqui, e duas afirmações dela precisam de correção antes de ir a outro cliente:

- **Página 24 — "Dois apps, um banco".** São **quatro** apps, um por perfil.
- **Página 23 — comparativo com concorrentes** marca ✓ em "Prontuário com mapa corporal e
  EVA" para a SemDor. Essas features não existem (são as 05 e 06 do catálogo).

Nesta apresentação do faturamento, o texto ainda diz que a guia vence "10 dias após o
atendimento" — desde o PR #44 o prazo conta da **data prevista de faturamento**.
