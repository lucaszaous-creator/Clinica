# Acompanhamento de pacientes: novos BSV e recall

Recepção, faturamento e gerência acessam a mesma tela **Acompanhamento de pacientes**. A fila começa em **A assumir**; cada pessoa pode assumir ou redistribuir um caso, sem retirá-lo da visão dos demais. Permissões individuais negadas continuam sendo respeitadas.

## Novos pacientes BSV

Na configuração da tela, a gerente seleciona o cadastro profissional do Gustavo. Durante uma consulta própria, ele encontra **Novo paciente de BSV** no consultório Windows e no portal profissional. A indicação é registrada imediatamente, sem salvar ou exigir evolução, e não duplica o paciente.

- Uma sessão futura ou de hoje, marcada para BSV ou BSV + acupuntura, retira o caso dos pendentes.
- Cancelamento ou falta devolve o caso, se não houver outra sessão válida marcada nem sessão efetivamente realizada após a indicação.
- Uma consulta comum não resolve a indicação de BSV. Agendamentos substituídos não a resolvem.
- Uma sessão passada ainda marcada como agendada aparece como **Conferir comparecimento**, para a equipe verificar a agenda.
- O registro permanece consultável nos filtros Agendado, Sessão realizada e Todas.
- Não conformidade exige motivo e justificativa. A equipe deve registrar uma tentativa antes de encerrar; casos sem possibilidade de contato podem ser encerrados pela gerente. É possível reabrir com histórico.

## Recall

Ao abrir o recall, a lista busca automaticamente pacientes sem retornar há pelo menos 60 dias. Na tela principal, escolha o número de dias, a modalidade e use **Buscar pacientes** para outro período. A busca remove filtros antigos de prazo/responsável para não esconder os novos resultados. **Mais filtros** refina os acompanhamentos já existentes. São consideradas sessões realmente realizadas, não estornadas, por paciente e modalidade. Consulta recente não mascara ausência de BSV. Uma sessão futura da modalidade impede nova inclusão.

O histórico permanece entre os meses e entre ciclos. Contatos enviados no recall anterior são preservados na criação do acompanhamento. Abrir o WhatsApp não conta como contato: depois da conversa, a funcionária registra canal, resultado, responsável e próxima data. A autorização de contato do cadastro continua obrigatória.

Filtros: paciente, situação, modalidade, prazo, convênio, profissional, responsável, somente meus, dias sem retornar, motivo, tentativas mínimas e período do último contato. Atalhos: A assumir, Meus contatos de hoje, Atrasados, Sem primeiro contato, Sem resposta e Cancelados/faltosos. Legendas identificam autorização do plano, pacote com saldo, telefone ausente e contato não autorizado. A gerente pode cadastrar outros motivos.

A lista se atualiza a cada minuto enquanto está aberta, sem interromper a edição de contatos ou filtros. Mudanças simultâneas são recusadas para evitar que uma funcionária sobrescreva a outra.

## Publicação

1. Conferir os testes dos dois repositórios e o pacote com os respectivos commits.
2. Com backup, aplicar `20260928123100_AcompanhamentoPacientesBsvRecall` a partir de `20260926120000_DevolucaoInfusaoExterna`. A migration acrescenta três tabelas, índices e motivos iniciais; não altera os registros existentes. `deploy/tablet/migracao-acompanhamento.sql` é o script idempotente gerado pelo EF.
3. Publicar API e portal juntos com `tools/empacotar-continuidade-tablet.ps1`. O atualizador concede ao usuário restrito do portal somente leitura/inclusão nas tabelas necessárias. Seguir a homologação e o aceite do pacote previstos pelo atualizador.
4. Publicar os aplicativos **Recepção, Faturamento, Gerente e Clínico**. Evitar distribuir os novos aplicativos antes da atualização do banco/API. Os outros aplicativos não precisam mudar para este recurso.
5. Na gerência, selecionar o cadastro real do Gustavo. Conferir que a seleção permanece ao sair e reabrir a configuração; a lista inicial de recall é preparada automaticamente, com busca por outro período na tela principal.
6. Conferir com paciente fictício: indicar → assumir → agendar BSV → sair dos pendentes → cancelar → voltar; repetir com BSV + acupuntura, remarcação válida e não conformidade. Confirmar acesso nos três perfis e ausência de alterações na evolução.

Em recuo, voltar API/portal/aplicativos ao pacote anterior e manter as novas tabelas e o histórico. Não executar a migration `Down` em produção. O atualizador protege o backup e registra as concessões que precisam ser revertidas.

O atualizador concede também as operações de recall ao proprietário da tabela de configurações do desktop: leitura/inclusão/alteração de acompanhamentos, leitura/inclusão de contatos e motivos, uso/leitura das três sequências. O portal permanece sem alteração de acompanhamentos. Conferir esses acessos após migrations aplicadas como administrador; ter acesso de leitura não prova que o Windows consegue salvar.

## Validação automatizada

`AcompanhamentoPacienteTests` cobre indicação, duplicidade, duas modalidades BSV, cancelamento, remarcação, falta, estorno, recall por modalidade, consentimento, prazo, concorrência, encerramento, reabertura e acesso compartilhado. A suíte usa SQLite localmente e PostgreSQL no CI pelo mecanismo `BancoDosTestes`.

`AcompanhamentoBsvHttpTests` verifica autenticação, CSRF e duplicidade no portal. `ferramentas/testar-interface-clinico.mjs`, no clinica-site, verifica o botão e a preservação do rascunho. `tools/ValidarLayoutWindows --acompanhamento` confere lista, filtros, contato e configuração em 880, 1024 e 1366 px. `tools/Clinica.Verificacao.Desktop` confere a navegação dos aplicativos.
