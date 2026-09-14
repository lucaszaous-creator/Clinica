# Correção do fluxo de atendimento — 14/09/2026

Esta alteração trata os seis achados do [review funcional](review-funcional-2026-09-14.md), feito sobre a base `61d5cd4`. O objetivo é concluir a sessão e suas guias no mesmo atendimento, preservando o prontuário e permitindo recuperar falhas sem relançar a sessão.

## Comportamento entregue

| Situação | Comportamento corrigido |
|---|---|
| Médico finaliza a sessão | O fim, a presença, o atendimento e as guias são gravados juntos. A evolução continua salva separadamente para não perder o texto em uma falha administrativa. |
| Sessão antiga tem fim, mas continua agendada | O Consultório oferece **Concluir atendimento pendente** no mesmo horário. |
| Recepção prepara atendimento e guias | A sessão permanece aberta para o médico. Para lançamento retrospectivo, a recepção marca explicitamente **O atendimento já aconteceu — registrar como realizado**. |
| Outro profissional escreveu a evolução | O vínculo explícito com a sessão é reconhecido, independentemente do autor, sem carregar o texto clínico nas consultas de indicadores. |
| Conclusão ficou incompleta | A fila de hoje inclui sessões encerradas ainda agendadas e sessões iniciadas em dias anteriores, identificadas como **Conclusão pendente**, com a data original. |
| Há múltiplos horários ou registros antigos sem vínculo | O sistema deixa de distribuir evoluções pela ordem dos horários. No Consultório, o operador autorizado confere o histórico e escolhe **Vincular registro**. A ação mantém texto e autoria, guarda versão e gera auditoria. |
| Fechamento é repetido após uma falha ou em duas estações | Cada etapa de pacote, estoque e caixa tem um comprovante interno gravado na mesma transação do efeito. Uma repetição equivalente reutiliza o resultado. Parâmetros diferentes exigem conferência, em vez de uma nova cobrança silenciosa. |

O lançamento antecipado também deixa de ser tratado pela conciliação como outro atendimento que substituiria o próprio horário.

## Recuperação operacional

1. Abra o atendimento original na fila ou no Consultório.
2. Se houver uma evolução antiga sem vínculo e mais de uma possibilidade, confira o histórico e selecione o registro correto antes de vinculá-lo.
3. Use **Concluir atendimento pendente**. Corrija eventual cadastro de convênio indicado pela tela e repita no mesmo horário.
4. Se apenas uma etapa de estoque ou caixa falhar, confira o aviso e retome o fechamento. As etapas já confirmadas são reutilizadas.

Duplicidades e lançamentos históricos anteriores a esta correção não são excluídos nem reconciliados automaticamente. Devem ser conferidos pelos responsáveis; o sistema não pode presumir que dois registros representam a mesma sessão clínica.

## Banco e distribuição

A migration `20260914133708_EtapasDoFechamentoDaSessao` é aditiva: cria apenas a tabela de comprovantes das etapas. Não altera nem remove prontuários, guias ou lançamentos existentes. No PostgreSQL, uma trava transacional por atendimento serializa essas etapas entre estações.

A proteção depende dos módulos atualizados e da migration aplicada. Comprovantes não são retroativamente inventados para operações feitas por versões antigas. A atualização das estações deve ser coordenada antes de usar a retomada em paralelo.

Esta entrega não publica uma versão nem altera o banco de produção. A compilação e os testes usam código e dados de teste.

## Cobertura de regressão

Os testes adicionais cobrem conclusão atômica, falha antes do commit, retomada de sessão parcialmente encerrada, guias antecipadas, autoria substituta, associação ambígua, vínculo auditado, pendências após a virada do dia, repetição de caixa/estoque, rollback entre efeito e comprovante e recuperação parcial. A execução PostgreSQL inclui duas conexões independentes disputando a mesma cobrança, além de aplicar as migrations reais.

As verificações da entrega abrangem a solução Windows, a compilação-sombra dos dez projetos WPF, os validadores de XAML e tokens e a suíte de testes. O resultado final das execuções fica registrado no PR e no CI. Testes automatizados não substituem a homologação da operação nas estações da clínica.
