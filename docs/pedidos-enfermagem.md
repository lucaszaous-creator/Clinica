# Pedidos da enfermagem para a recepção

A técnica consulta a agenda no portal, escolhe um dia e usa **Solicitar à recepção** para informar quantidade adicional, modalidade, período e observação opcional. O pedido vira um aviso compartilhado na agenda da recepção. Não cria, edita, reserva ou cancela agendamentos e não distribui responsáveis.

## Fluxo entregue

- Portal: semana com contagens; selecionar um dia mostra pacientes, horários, modalidades, médicos e salas. O formulário aparece somente no diálogo de solicitação, com a data preenchida. A validade termina no início do período escolhido.
- Recepção: contador de novos, abertos e do dia; caixa de avisos independente do filtro de médico. Abrir registra **Visto pela recepção**, visível também no portal. Atalho abre o dia e remove filtros de médico e sala. Encerrar é opcional e guarda um retorno; não significa que houve agendamento.
- A autora pode retirar o próprio aviso. Avisos vencidos, encerrados ou retirados deixam de contar como abertos. A caixa geral inclui os últimos 30 dias e datas futuras de até um ano; a consulta por dia do portal alcança um ano para cada lado.
- Não há classificação de pacientes como fáceis/difíceis, cálculo automático de capacidade, saldo de vagas ou reserva. A avaliação do grupo permanece com a técnica.

## Contrato e autorização

`IPlanejamentoEnfermagemService` concentra as regras compartilhadas. Consulta semanal traz somente contagens; detalhes do dia são paginados em 50, com totais e referência calculados sobre todos os horários. Sobreposições, duração do profissional, outras modalidades e bloqueios entram na leitura.

Enfermagem: perfil Enfermagem ativo, profissional vinculado e ativo, permissões existentes VerAgenda e RegistrarEvolucaoEnfermagem. Recepção: perfil Recepcao/Gerente e permissões existentes VerAgenda, EditarAgenda e VerFichaPaciente. O serviço reconsulta o usuário em cada operação. Nenhuma permissão de agenda é ampliada.

A API `/api/posto/enfermagem/planejamento` exige sessão da equipe e CSRF nos POSTs. Expõe `/semana`, `/periodo`, `/pedidos`, detalhe e retirada; não expõe marcar visto/encerrar para o portal. O host Windows valida sessão, contexto, IDs presentes na lista e ação registrada.

Solicitações possuem recibo de idempotência, versão concorrente e histórico. A referência da agenda é conferida no envio; mudança posterior aparece no detalhe, mesmo se a contagem continuar igual. Falhas não aparecem como agenda vazia. A notificação não é uma transação de reserva de horário.

## Verificação

- `dotnet test tests/Clinica.Tests`: 2.972 testes aprovados; testes dedicados cobrem reenvio, autorização, revogação, validade, autoria, concorrência, paginação, alterações de agenda, sobreposição e semana.
- `dotnet test tests/Clinica.Assinaturas.Tests`: 22 testes aprovados, incluindo sessão, CSRF, papel, semana, envio real pela API, leitura pela recepção e retirada sem alterar horários.
- `TraducaoNoNpgsqlTests`: tradução da consulta real com duração e navegação, sem abrir banco externo.
- `dotnet run --project tools/validar-planejamento-enfermagem`: WebView2 real, SQLite sintético, caixa compartilhada, leitura, cancelamento de confirmação, encerramento, atalho e larguras 620/900/1100. Capturas em `artifacts/planejamento-enfermagem`.
- Portal em `clinica-site`: `node ferramentas/testar-planejamento-enfermagem.mjs`, testes de navegação existentes, consulta/envio/retry/retirada, cancelamento sem envio, erros, escape de texto, 320/820/1180 com fonte 200% e WCAG AA. O navegador usa contrato simulado; a API é exercitada separadamente pelos testes HTTP.
- Verificações da suíte, compilação-sombra, espelho de tokens e regras do atualizador.

## Jev

Consulta efetiva ao `jev-1.13.0` em 14/10/2026, com trechos do serviço, contrato, rotas, adaptador Windows, componente da recepção e módulo do portal, além do pedido atualizado e evidências sintéticas. O retorno estruturado selecionou:

| Pergunta | Resultado |
|---|---|
| Agenda primeiro e solicitação contextual | adequado |
| Caixa compartilhada sem assumir responsável | adequado |
| Novo caminho para a técnica escrever na agenda | sem_caminho_novo |
| Cuidado prioritário | interpretar_como_vaga |

A resposta não contém explicações livres nem execução independente. É validação do fluxo e dos trechos enviados, não certificação clínica ou de segurança. A quantidade continua apresentada como solicitação adicional, sujeita à conferência da recepção.

## Preparação para publicação

A migration aditiva `20261014192239_PedidosEnfermagem` cria apenas `SolicitacoesEnfermagem` e `EventosSolicitacaoEnfermagem`. O SQL idempotente está em `deploy/tablet/migracao-pedidos-enfermagem.sql` e parte de `20261005193646_FormatoDaEvolucao`. O atualizador e o empacotador reconhecem essa etapa; os privilégios novos atingem somente as duas tabelas e suas sequências, para portal e desktop. O histórico recebe SELECT/INSERT, sem UPDATE/DELETE.

Publicar juntos a API, o frontend do portal e a recepção. A migration deve estar aplicada antes de disponibilizar as novas telas. A versão anterior da interface pode continuar funcionando com o esquema aditivo. Em eventual recuo, preservar as tabelas e seus avisos; não executar Down para reverter apenas a interface.

Nenhum ambiente de produção foi alterado. PostgreSQL real e concessões no servidor ainda devem ser conferidos em homologação; a validação local usa SQLite e tradução Npgsql, além do CI previsto para PostgreSQL.
