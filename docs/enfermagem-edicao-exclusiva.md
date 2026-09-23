# Edição de enfermagem no portal

## Incidente e correção
O serviço inseria a evolução e atualizava FaseAtendimento na mesma transação. Faltava UPDATE dessa coluna à role do portal. A transação falhava e a API classificava qualquer DbUpdateException como concorrência. Conceder somente UPDATE(FaseAtendimento), manter UPDATE da tabela negado e não reemitir registros clínicos.

As validações conhecidas do serviço de enfermagem agora usam o marcador de mensagem pública. Falhas técnicas não exibem detalhes de banco nem alegam edição de outra pessoa. Logs registram apenas código SQLSTATE e referência, sem conteúdo clínico.

## Reserva
- Exclusiva por agendamento para a edição de enfermagem; atendimento médico permanece independente.
- Persistida em EdicoesEnfermagemTablet, vinculada ao usuário, sessão autenticada e identificador aleatório da tela. Outra aba do mesmo usuário também é bloqueada.
- Renovação a cada 25 segundos; validade de 120 segundos. Saída da tela libera a própria reserva; falha de conexão/fechamento abrupto deixa expirar. Logout torna a reserva inativa.
- A resposta de conflito identifica o usuário. O frontend conserva o texto e oferece Retomar edição. Não usa localStorage para guardar dados clínicos.
- O salvamento confere a reserva sob o mesmo lock transacional do paciente; uma tela expirada não pode gravar sobre o trabalho de outra.
- Reenvios confirmados continuam idempotentes. Correções/vínculos antigos são recusados enquanto outra tela tem reserva ativa.

## Implantação
Migration aditiva 20260923190924_EdicaoEnfermagemExclusiva, a partir de 20260922231000_HabilitacoesDoProfissional. Publicar API e portal juntos. Papéis de cada ambiente recebem SELECT/INSERT/UPDATE/DELETE somente na nova tabela de reservas; nenhum DELETE de registros clínicos é concedido. Manter backup e release anterior. A tabela de reservas pode permanecer em rollback.

## Verificação
Testes HTTP: duas contas e duas telas, identificação do ocupante, expiração, liberação somente pelo dono, validações de sinais/horário, chegada e após aplicação, reenvio sem duplicação e guias preservadas. Testes da interface: bloqueio, texto preservado, retomada e formulário responsivo. Homologação precisa exercitar PostgreSQL com a role restrita real antes de produção.
