# Ações visíveis e hierarquia — PR 245

Padrão aprovado pelo proprietário em 08/10/2026, com extensão às demais telas da suíte.

## Apresentação

- Ação principal em azul SemDor preenchido. Ações auxiliares das barras de página, seção e diálogo usam texto e ícones azuis sobre branco, sem competir com salvar ou confirmar.
- Nas linhas de listas, ações auxiliares conservam contorno azul. Remoções mantêm vermelho funcional. Ações indisponíveis são distintas e continuam obedecendo ao host.
- Ícones Lucide locais acompanham os comandos conhecidos de salvar, buscar, atualizar, imprimir, editar e demais operações. A ponte HTML e os componentes React usam o mesmo catálogo.
- No atendimento, iniciar e reabrir só aparecem quando aplicáveis. Concluir manualmente aparece para o Gerente, conforme a regra já usada no WPF. Documentos e ações da ficha são faixas alinhadas; comandos da sessão ficam junto ao título Atendimento, antes da EVA. Salvar sessão fica destacado à direita.
- Em larguras menores, as ações quebram linha sem truncar rótulos. A visibilidade segue o estado e o perfil informados pelo host.
- Marcar horário tem uma barra de confirmação que permanece acessível durante a rolagem. O botão tem rótulo explícito e destaque azul. O comando e sua habilitação continuam no C#.
- Campos mantêm contorno legível, conteúdo escuro e superfície branca. Seletores, botões e expansões de detalhes têm indicação azul de interação, inclusive na prescrição de infusão.

## Simplificação da evolução e regras confirmadas

- Removidas da evolução as tabelas Alertas clínicos, Alertas e Campos complementares da clínica. Os campos personalizados continuam acessíveis pelo botão Campos complementares, no mesmo rascunho da sessão.
- A prescrição de infusão deixa de mostrar a observação geral. O valor já gravado continua no ViewModel e na persistência; observações dos medicamentos permanecem.
- Entrar por Atender inicia o horário do dia quando o estado e a permissão permitem. Iniciar não deve permanecer como botão redundante após esse ato.
- Editar a evolução não a grava automaticamente. Salvar sessão continua explícito e destacado; para o médico, também conclui o atendimento e gera as guias aplicáveis.
- A conclusão por prazo é configurável e vem desativada por padrão; 24 horas é o prazo inicial. Conta desde a última gravação da evolução médica elegível, não desde uma alteração ainda não salva. Depende das condições do serviço e do processamento enquanto a suíte está em execução. A configuração da cliente não foi consultada.

## Integração

Os módulos Recepção, Clínico, Gerente e Faturamento usam o shell compartilhado. O Financeiro independente também importa os mesmos componentes e o CSS final `cores-semdor.css`. O catálogo `icones-acoes.tsx` é usado tanto por `BotaoReact` quanto pelos botões reconciliados de `HtmlReact`, incluindo formulários e diálogos.

O contrato `data-*`, as permissões, os estados de habilitação, as confirmações e a persistência não foram substituídos por regras JavaScript.

## Verificação reproduzível

- `tests/Clinica.Clinico.Web.Qa --web`: ações clínicas expostas, hierarquia visual, posição acima da EVA e larguras 1440/1044/900; documentos, infusão cancelada, mapa e navegação. Cenário vinculado confere Concluir para o Gerente, cancelamento, início persistido e visibilidade para o médico após o início.
- `qa/recepcao-web --buscas`: busca e escolha de paciente, confirmação de agendamento visível durante a rolagem, gravação, cancelamento, permissões e chegada.
- `tools/validar-design-financeiro --web`: páginas, formulários, persistência, cancelamento e acesso revogado.
- `tools/validar-suite-web --web`: composição do Gerente e Financeiro integrado, buscas e formulários.
- `tools/validar-faturamento-web --web`: páginas e fluxos de Faturamento em três dimensões.

As capturas usam WebView2 e dados sintéticos. Essa cobertura não certifica todas as combinações de dados, zoom, perfis e equipamentos da clínica.

## Capturas

- [Atendimento do médico iniciado: Salvar sessão sem comandos redundantes](evidencias-pr245/atendimento-acoes.png).
- [Comandos da sessão em notebook de 900 px](evidencias-pr245/atendimento-sessao-900.png).
- [Controles da prescrição de infusão](evidencias-pr245/infusao-acoes.png).
- [Confirmação de agendamento acessível durante a rolagem](evidencias-pr245/agendamento-confirmacao.png).
- [Diálogo financeiro com confirmação destacada e ação auxiliar](evidencias-pr245/financeiro-acoes.png).

- [Gerente: ações compartilhadas](evidencias-pr245/gerente-acoes.png).
- [Faturamento: detalhes e ações compartilhadas](evidencias-pr245/faturamento-acoes.png).

## Resultados desta revisão

- TypeScript e Vite dos dois frontends aprovados; recursos publicados conferidos.
- 2.985 testes aprovados, sem falhas ou testes ignorados. Verificação estática de 225 XAML/10 projetos/143 construtores e compilação-sombra dos 11 projetos WPF aprovadas.
- WebView2 clínico: edição e troca de abas mantêm rascunho sem gravar; Salvar persiste. Campos complementares dos tipos texto/lista/sim-não editados pelo diálogo e recuperados do SQLite. Três tabelas removidas do corpo principal, sem perder as respostas. Médico com Salvar explícito; gerente com confirmação manual; Iniciar some após o carimbo persistido.
- Infusão: 34 verificações com ViewModels e banco isolado, incluindo preservação da observação geral preexistente e das observações por medicamento, rascunho, liberação, execução e cancelamento.
- As execuções dos cinco módulos documentadas acima validaram a camada de apresentação compartilhada. O QA clínico foi repetido após os ajustes finais de campos e visibilidade. A recepção foi repetida após restaurar o preenchimento azul do filtro de profissional selecionado.

## Jev — escopo e limites

O proprietário solicitou revisão adicional pelo Jev. A análise de apresentação dos cinco módulos está em [acoes-jev-pr245.json](acoes-jev-pr245.json), classificada como `coberto_com_limites`. Após os ajustes clínicos, uma nova consulta recebeu fontes e resultados de testes reais com dados sintéticos: [funcionalidades-jev-pr245.json](funcionalidades-jev-pr245.json).

A revisão funcional classificou os cinco quesitos como `coerente_com_evidencias`: início/gravação/conclusão, evolução simplificada, infusão, recepção e demais módulos. Jev analisou o material fornecido; não operou o aplicativo, não inspecionou imagens e não certificou todos os cenários possíveis. A execução funcional foi realizada pelos testes locais.

## Correção da sincronização no CI Windows

O build de `78ed66c` falhou no clique de Campos complementares: o QA encontrou o botão do contexto anterior, rolou, e a resposta assíncrona da navegação reiniciou a rolagem antes do clique. A compilação e os testes de serviços/PostgreSQL daquele commit passaram.

O teste de evolução agora aguarda o novo `data-contexto`. O helper de ações reencontra e alcança o botão com limite de tentativas quando a geometria muda antes do clique; não repete um clique já disparado e continua rejeitando ações ausentes, desabilitadas, ocultas ou permanentemente inacessíveis. A regressão `RolagemTardia.cs` força um reset entre rolar e clicar, exige exatamente um clique visível e confirma zero cliques adicionais num botão fora da área acessível. O fluxo clínico completo passou localmente com essa regressão. A alteração é restrita ao QA, sem mudar regras ou apresentação do aplicativo.

A revisão adicional do Jev para esta correção de QA está em [correcao-build-jev-pr245.json](correcao-build-jev-pr245.json): sincronização e regressão classificadas como `coerente_com_evidencias`, somente por análise das fontes e do log local.
