# Estornar um atendimento

Porta: **Recepção → Novo atendimento → conferência "lançados hoje" → "Estornar…"**
(bit `LancarAtendimento`).

Desfaz uma sessão lançada por engano — o paciente errado, a sessão que não aconteceu, o
duplo lançamento.

## Por que ele não existia antes

O "Remarcar" da agenda recusa horário já realizado com a frase *"Estorne o atendimento
antes"* — e **não havia estorno de atendimento nenhum no sistema**. A instrução mandava
fazer uma coisa que não existe, e a saída que sobrava era o **Cancelar**, que não tem
trava: apagava o carimbo de realizado, suspendia as guias abertas e apenas *avisava* sobre
as já baixadas.

O motivo de não existir aparece quando se lista o que o lançamento **faz**. São cinco
efeitos, em três serviços, e só um é a guia:

1. o atendimento e os códigos de faturamento;
2. as não conformidades do paciente **reabertas**;
3. a consulta do convênio **renovada**;
4. o horário marcado como realizado, com o check-in carimbado;
5. o pacote debitado, o insumo baixado do estoque e a entrada no caixa.

Um estorno que desfizesse só a guia seria um meio-estorno com cara de completo.

## Como ele funciona

**Pergunta item a item.** A janela lista o que **aquele** atendimento produziu e desfaz só
o que for marcado — porque o caso varia: às vezes o caixa do dia já foi conferido, às vezes
não. As **guias saem sempre**: são a razão do estorno.

**Nada é apagado.** Os códigos vão para "não aplicável" marcados como estornados, e o
atendimento fica no histórico com quem estornou, quando e por quê. Atendimento é lastro de
faturamento — e apagá-lo deixaria o horário órfão, dizendo "Finalizado" sobre uma sessão
que não existe mais.

**O horário volta para "Agendado"**, com os carimbos da fila limpos, e pode ser lançado de
novo — gerando um atendimento novo, com guias novas.

**O motivo é obrigatório.** É o que fica na trilha de auditoria para quem for conferir.

## Quando ele é recusado

Quando **o fato já saiu da clínica**: há guia daquele atendimento já baixada no portal, já
enviada em lote TISS, ou em não conformidade. Aí a recusa diz o que fazer — **estorne a
baixa primeiro** (ou resolva a não conformidade). Desfazer por aqui deixaria o portal do
convênio e o sistema dizendo coisas diferentes.

É a mesma trava que a troca de modalidade pela agenda já aplica, e pela mesma razão.

## O que ele deliberadamente NÃO desfaz

| | Por quê |
|---|---|
| **A consulta do convênio renovada** | É listada na janela, sem caixinha. Desfazer exigiria ressuscitar a consulta anterior, e se uma receita já foi emitida sob a nova, invalidá-la retroativamente quebra um documento clínico. O estrago de deixá-la é pequeno — o paciente fica com cobertura um pouco antes do devido, sem guia e sem dinheiro envolvidos — e é reversível à mão pela aba Consultas. |
| **As não conformidades reabertas** | Elas voltaram a ser pendência porque o paciente **apareceu** — e ele apareceu, mesmo que a sessão tenha sido lançada errado. Fechá-las de volta esconderia uma cobrança legítima. |

## Se algo falhar no meio

O estorno das guias e a liberação do horário acontecem num commit só: ou os dois, ou nada.
As reversões de fora — caixa, pacote, insumo — têm gravação própria, e uma falha ali **vira
aviso**, nunca desfaz o estorno já gravado. A tela diz o que não deu, e o caminho para
resolver à mão. (É a mesma hierarquia do fechamento da sessão: uma agulha que não bate no
estoque não pode derrubar o registro de uma sessão que aconteceu.)
