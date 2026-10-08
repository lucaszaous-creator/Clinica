# Ajustes da homologação — PR 245

Revisão de 08/10/2026, na mesma PR, sem merge, instalação ou publicação em produção.
Três agentes dividiram a reprodução e correção dos fluxos de Recepção/Clínico,
Faturamento/Financeiro/Gerente e apresentação; o agente principal integrou as mudanças,
consultou o Jev e executou as verificações gerais.

## Relatos e correções

| Relato | Correção e alcance |
| --- | --- |
| Todas as telas brancas, sem identificar seções ou ações | Identidade compartilhada: fundo azul acinzentado, cartões delimitados, cabeçalhos com contraste, campos de leitura identificáveis, botões azuis e ações de risco vermelhas com texto. Inclui os cinco módulos, entrada e assinatura. |
| Agenda diária e acompanhamento excedem a largura | Listas extensas centradas no paciente passam a cartões responsivos. Identidade, contexto principal e todas as ações continuam visíveis; demais colunas ficam em detalhes expansíveis. |
| Profissionais mostra somente Filtrar | O contrato tinha ações, mas nenhuma coluna. Agora mostra nome, quantidade e estado do filtro. |
| Marcar atendimento/horário confuso | Etapas de paciente, data/profissional e modalidade; contexto do paciente em resumo com dados expansíveis, observações e conferência final. Títulos internos estruturam o formulário, em vez de aparecerem como nomes de campo. |
| Semana com muitos atendimentos ilegível | Sessões ordenadas por dia em cartões de largura legível; dias se distribuem conforme a janela. Disponibilidade e dados completos permanecem acessíveis. Grade complementar apenas quando o volume e a simultaneidade permitem leitura. |
| Documentos não mostra pacientes buscados | A consulta retornava dados, mas a lista não tinha colunas nem ação para selecionar. Foram registrados Nome/CPF/Telefone e seleção da linha atual, com permissão conferida no C#. |
| Buscas semelhantes em outros módulos | Gerente: seleção e Nome/CPF/Convênio em Auditoria e Guarda. Testes pelo DOM de nome, CPF, resultado vazio, seleção, outro prontuário e resposta atrasada nos fluxos afetados. |
| Novo prontuário abre o paciente anterior | Reprodução confirmou reutilização do foco anterior. O comando agora abre o seletor web existente; cancelar conserva foco/vínculos, confirmar outro paciente troca o foco e remove os vínculos anteriores, com autorização relida após a escolha. |
| Deslocamento lateral transfere para outra tabela | Restauração de rolagem usa página/diálogo/seção/tabela, substituindo índices. Listas da semana e detalhes abertos também preservam sua posição. |
| Confirmações mostram só título e botões | O subtítulo estava dentro de um cabeçalho oculto por CSS. Perguntas, avisos e instruções agora aparecem no corpo do diálogo e são associados ao diálogo para acessibilidade. |
| Agendamento permitido aparece indisponível | Marcar sem emitir guia exige EditarAgenda. Lançar atendimento e emitir guia mantêm suas permissões, com nova leitura no serviço antes da gravação. |
| Ponto decimal alterava valores | Reprodução pelo DOM mostrou `12.5` persistindo como `125`. O leitor compartilhado agora preserva frações com ponto ou vírgula e valores monetários formatados. |

Os cartões mantêm comandos e campos, inclusive copiar valor/linha/tabela pelo menu de
contexto. A navegação continua no topo, com hover, clique e teclado. As mudanças não
substituem serviços, cálculos ou regras de negócio por JavaScript.

## Consulta ao Jev

Consulta efetiva ao modelo `jev-1.13.0` pela API TypeSafe, com trechos de código e
descrição sintética dos problemas, sem imagens ou dados reais de pacientes.
O retorno priorizou metadados dos resultados da busca (confiança 0,73), lista por dia
para semana densa (0,88) e restauração de rolagem por índice como hipótese de falha
(0,83). É orientação sobre essas escolhas, não execução de testes nem aprovação
de todos os módulos. Pedido e resposta estão em `artifacts/jev-ajustes-web/`, fora do Git.

## Verificações executadas

- 2.975 testes de `Clinica.Tests` aprovados, incluindo 17 casos de leitura decimal,
  culturas do Windows, valores inválidos, zero e quantidades.
- Compilação-sombra: C# de 11 projetos WPF. Verificação da suíte: 225 XAML,
  10 projetos e 143 construtores. Contratos web: 76 páginas e 97 formulários,
  zero divergências.
- WebView2 real: regressão de tabela removida sem transferência de rolagem,
  superfícies com cores distintas, 197 cartões da semana em 900/1366 pixels,
  sem perda de sessões nem vazamento horizontal, detalhes e rolagem diária preservados.
- Faturamento/Gerente/Financeiro composto: digitação real, busca, seleção, gravação
  e cancelamento; metas `12.5` e `12,5` persistem o mesmo valor.
- Recepção/Clínico: seis buscas pelo DOM com 232 fichas, CPF fora dos resultados
  iniciais, resposta atrasada descartada, troca de prontuário, novo prontuário com
  cancelamento e outro paciente, agendamento gravado/cancelado, texto visível do
  comprovante e permissões separadas de agenda/emissão de guia.
- Cenários de serviço no WebView2: 34 itens na agenda diária, 170 acompanhamentos
  e 197 sessões na semana, todos com identidade e ação de abrir, em 900/1366 pixels.
- Profissionais: Todos com 34, dois profissionais com 17 cada, nomes/contagens/estado
  visíveis; filtrar o segundo mostra 17 e voltar a Todos restaura 34, em 900/1366 pixels.
- Recepção completa: cadastro/convênio/foto, agenda/remarcação/cancelamento,
  recebimento parcial, cancelamento/replay de diálogo, ferramentas e permissões;
  50 capturas. Clínico completo: 20 páginas, 46 contratos de diálogo próprios e
  compartilhados, fluxos clínicos/infusão/enfermagem e mapa corporal;
  60 capturas de rota, mais gesto do mapa.
- Financeiro original: 15 páginas em três dimensões, menus, teclado, formulários,
  permissões e revogação; CPF digitado sem sair do campo, seleção e lançamento
  preservam paciente e valor `12.5` no banco.
- Faturamento completo: oito rotas, 13 contratos de formulário, operações reais
  de baixa/glosa/NC/TISS, entrada, assinatura e contingência; 40 capturas.
- Gerente e Financeiro composto: 31 páginas em três dimensões, sem erro,
  vazamento horizontal ou ferramentas do topo sobrepostas.
- Cinco aplicativos compilados em Release com `--no-incremental`, zero erros.
  Os avisos existentes do MVVM Toolkit permanecem nos logs.
- Verificador Windows de infusão: 15 verificações aprovadas, incluindo modelo,
  reabertura, preparos, persistência e liberação para enfermagem.

Comandos e harnesses: `qa/recepcao-web --buscas/--fluxos/--web`,
`tests/Clinica.Clinico.Web.Qa`, `tools/validar-faturamento-web --buscas-ui-only/--web`,
`tools/validar-design-financeiro --web` e `tools/validar-suite-web --rolagem-only/--web`.
Use o projeto `.csproj` de cada diretório com `dotnet run -c Release --` seguido da opção.
Logs e capturas ficam em `artifacts/`, sempre com dados fictícios.

## Executáveis e limites

Geração reproduzível: `tools/gerar-executaveis-teste.ps1`, com
`ClinicaTesteLocal=true`, configuração separada e atualização automática desativada.
Extraia o ZIP inteiro, mantendo `WebSuite` ao lado dos executáveis; instruções em
`docs/teste-pr245.md`.

Os cinco executáveis portáteis foram gerados novamente após a correção de novo
prontuário e abriram até a configuração inicial sem banco, identificados como edição
de teste PR 245. Essa abertura não substitui os testes de fluxos dos harnesses.

As operações automatizadas usam SQLite sintético. PostgreSQL, equipamentos físicos e
integrações externas dependem da homologação em ambiente separado. Os testes cobrem
os cenários documentados; não certificam todas as combinações de dados, perfis ou
integrações. Nenhum envio real, release ou alteração do canal de produção foi realizado.
