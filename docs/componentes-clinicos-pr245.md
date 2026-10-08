# Componentes e composição da interface — PR 245

## Direção visual

Em 08/10/2026 o proprietário pediu seguir de perto o Salte original. A primeira implementação web do Financeiro, preservada em `8894ffa` e `973abb1`, foi recuperada para comparação. A página pública atual do Salte redireciona ao produto Workers; ela não foi usada como referência do sistema clínico. Não foi localizada uma captura autêntica do Salte clínico para afirmar reprodução exata.

O resumo financeiro retoma resultado à esquerda, entradas e saídas empilhadas à direita, valores ao lado das curvas e composição complementar horizontal. A navegação continua no topo conforme instrução posterior do proprietário. A iteração jade/sálvia foi descartada. Fundo cinza neutro distingue a área de trabalho das superfícies brancas; ações e estados usam cor pontual, texto legível e bordas discretas.

Após ver as capturas dessa composição, o proprietário confirmou: “é esse modo de design que queremos” e pediu cores e movimentos encaixados aos módulos. A estrutura foi preservada. Estados explicitamente reconhecidos recebem verde (concluído/agendado), âmbar (pendente/aguardando), vermelho (interrompido) e lavanda (em curso/seleção), sempre mantendo o texto. Não se infere gravidade clínica nem estado de pagamento a partir de texto livre. Entradas e saídas financeiras usam verde e rosa, com sinal e descrição.

Diálogos entram com deslocamento de 8px em 180ms; navegação entre páginas usa 4px em 140ms, sem esmaecer toda a tela. Menus e avisos têm abertura curta. Curvas financeiras mudam de opacidade por 180ms somente quando o traçado muda; os números não contam artificialmente. A preferência de movimento reduzido desativa deslocamentos e transições. Campos, foco, cursor e contexto continuam estáveis em atualizações de dados.

## Implementação

- React e TypeScript continuam controlando a apresentação, com Mantine para campos, botões, painéis e diálogos. Cada raiz tem um único provider. Os recursos são compilados e distribuídos localmente.
- Recepção: lista de atendimentos com horário, paciente, profissional, situação, próximo passo e ação direta. Comandos adicionais ficam em Mais ações; campos adicionais permanecem nos detalhes. Os profissionais mantêm nomes, contagens e filtro ativo.
- Agendamento: área de preenchimento contínua e contexto do paciente ao lado, reposicionado abaixo em telas menores. Modalidades preservam comandos e campos originais.
- Clínico: identidade do paciente, abas horizontais, menus de documentos/ações, ficha organizada em identificação, tratamento e alertas. Histórico por sessão mantém os registros e ações. Editores ricos, mapa corporal e controles especializados preservam seus adaptadores e contratos.
- Agenda: FullCalendar com Lista, Semana e Dia, horários densos legíveis e abertura do dia completo. Disponibilidade e bloqueios continuam acessíveis. Datas consultadas e alterações continuam no host C#; nenhum plugin Premium é usado.
- Faturamento: composições por tarefa para guias, pendências, baixados, glosas, não conformidades, relatórios e TISS, com todos os campos e seções do contrato.
- Gestão: composições de consulta, indicadores e configurações; seções não especializadas continuam disponíveis no renderizador compartilhado.
- TanStack Table organiza as linhas das tabelas comuns e do resumo financeiro. Motion fornece transições curtas que respeitam movimento reduzido. Atualizações de busca mantêm identidade dos controles, foco e cursor.

Os cinco módulos usam esses componentes, mas isso não significa que cada uma das 76 rotas tenha uma composição exclusiva. As rotas restantes e formulários usam componentes comuns; nenhum fluxo foi removido por não ter composição especializada. C#, permissões, serviços, cálculos e persistência permanecem como autoridade.

## Evidências

O inventário registra 76 páginas e 97 formulários, sem divergências de contrato. A suíte de domínio/aplicação passou com 2.975 testes. TypeScript e os builds dos dois frontends passaram.

Testes WebView2 com dados sintéticos cobrem navegação em tamanhos de notebook, digitação sem Enter/blur, respostas de busca fora de ordem, seleção e troca de paciente, menus por teclado, contexto das ações, formulários com gravação/cancelamento, privacidade e acesso revogado. A recepção inclui 34 atendimentos, 170 acompanhamentos e 197 sessões semanais. O calendário verifica os sete dias, abertura dos eventos adicionais e retorno à lista completa.

Logs locais desta revisão: `artifacts/qa-cores-motion-recepcao.log`, `qa-cores-motion-clinico.log`, `qa-cores-motion-financeiro.log`, `qa-cores-motion-faturamento.log`, `qa-cores-motion-suite.log`. Capturas sintéticas ficam nas pastas de cada harness. Testes funcionais não equivalem à aprovação estética do proprietário e não certificam todos os fluxos de produção.

Dois agentes participaram da implementação e da recuperação da referência. Jev foi consultado sobre direção de composição antes da reconstrução; retornou preferência por fluxos específicos e hierarquia operacional. Não recebeu capturas, não executou a interface e não aprovou o resultado visual final. Pedido e resposta locais: `artifacts/jev-componentes/`.

## Entrega de teste

`tools/gerar-executaveis-teste.ps1` prepara a edição portátil `.4` dos cinco aplicativos, com perfil separado `ClinicaSemDor-Teste-PR245` e atualização automática desativada. A configuração habitual da instalação não é herdada. Não há release, merge ou publicação em produção. Impressoras, câmera, integrações externas e PostgreSQL não foram exercitados nesta revisão visual; precisam da homologação do ambiente de teste.


### Agenda por situação — cenário de homologação atualizado

O cenário denso mantém 34 horários na data escolhida (10 concluídos, quatro em atendimento e 20 a atender), além dos horários anteriores ainda sem conclusão. Em 08/10/2026, há 12 pendências anteriores: a fila correta contém 46 registros, com 23 por profissional. Esses pacientes não devem desaparecer para ajustar uma contagem de teste. O teste calcula essa quantidade pela semana sintética para continuar válido em outros dias.

A verificação cruza situação e profissional: dez concluídos no total, cinco do segundo profissional; dez a atender desse profissional, sem misturar os dois em atendimento. Ao limpar os filtros, devem voltar todos os registros, inclusive as conclusões pendentes anteriores. A captura `fila-conclusao-pendente-<largura>.png` registra esse recorte; a execução e o resultado devem ser conferidos no log mais recente de homologação.
