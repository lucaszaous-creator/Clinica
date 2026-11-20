# Repasse médico por guia recebida (nov/2026)

## O pedido

A clínica pediu um sistema de repasse **profissional** para os médicos, com uma regra
inegociável: **o repasse só conta depois de a guia ser conciliada** — o financeiro ou o
gerente afirmou, na Conciliação, que o convênio PAGOU aquela guia. Só então ela vira repasse.

Até aqui o repasse era feito à mão, num documento por médica e por competência (os modelos
de 09/2026 vieram anexados ao pedido): valor fixo por consulta, por convênio (Amil R$ 65,
Petrobras R$ 80, SulAmérica R$ 72), resumo por convênio, a lista de pacientes agrupada pelo
pagamento da operadora (GRD/Conta, lote de envio), a data da conferência e quem conferiu.

## O que o sistema tinha e por que não servia

A tela **Repasses** (parcela 4, `RepasseService`) apurava por **período de atendimento**:

| o que o antigo fazia | o problema para este pedido |
|---|---|
| contava os atendimentos do PERÍODO (pela agenda) | a regra fixa por atendimento pagava a consulta **antes** de o convênio pagar — exatamente o que a clínica proibiu |
| escolhia **uma** regra por profissional | Amil 65 / Petrobras 80 / SulAmérica 72 ao mesmo tempo era impossível: a regra mais específica vencia e as outras consultas sumiam da conta |
| travava o período (`RepasseApurado`) | a guia paga em novembro de uma consulta de setembro, com setembro já apurado, **nunca** entrava; e a competência real da clínica é a do PAGAMENTO (setembro trazia consultas de 06/10) |
| sem demonstrativo | o papel que vai ao médico continuava à mão |

## Opções consideradas

| | A. Remendar o antigo (filtrar por "pago") | B. **Fechamento por item recebido** (escolhida) | C. Repasse dentro da Conciliação (ao lançar a receita, já gera o repasse) |
|---|---|---|---|
| 🧑‍💻 UX | mesma tela; mas guia paga atrasada continua sem entrar, e o período travado esconde o que ficou para trás | uma tela: o que está pronto, o que está bloqueado e por quê, o que aguarda o convênio; fecha com um clique e sai o demonstrativo | quem concilia passa a decidir repasse no meio de outra tarefa; o médico não tem um fechamento para conferir |
| 🛠️ DX | nenhuma peça nova, mas a regra fica torta (período ≠ pagamento) e a trava contra pagar duas vezes continua sendo "não sobrepor período" | duas tabelas novas (`RepassesFechamentos`, `ItensRepasse`), um serviço; a trava é um **índice único no banco** por item | acopla Conciliação e repasse (dois donos na mesma porta); estorno e cancelamento viram casos especiais da conciliação |
| 🤝 CX | continua pagando antes de receber em parte dos casos — risco financeiro direto | o médico recebe um demonstrativo no leiaute que a clínica já usa; pagar duas vezes é impossível por construção | sem conferência separada, a clínica perde o momento "Patricia conferiu em 01/11" que o documento dela registra |

**Recomendação e decisão: B.** É a única que cumpre a regra da clínica por construção (só
entra item com receita realizada) e a única em que pagar duas vezes não depende de alguém
lembrar de conferir.

## Como funciona

1. **A Conciliação continua sendo a porta do "recebi".** Lançar a receita da guia como
   **realizada** é a afirmação de que o convênio pagou. Receita **prevista** (a receber) não
   conta. A Conciliação ganhou o campo opcional **Referência do pagamento (GRD / conta /
   lote)**, que agrupa o anexo do demonstrativo como o documento da clínica fazia.
2. **Cada recebimento é um item, e entra uma única vez.** Guia paga → item `G:{guia}`;
   sessão particular paga → item `P:{atendimento}`. O índice único em `ItensRepasse.Chave`
   (entre itens não cancelados) é a trava: dois postos fechando o mesmo médico ao mesmo tempo
   esbarram no banco.
3. **A competência é a do pagamento.** O fechamento de 09/2026 leva tudo o que foi
   recebido até 30/09 e ainda não foi fechado — inclusive o que ficou para trás de meses
   anteriores. Nada se perde por ter chegado atrasado.
4. **A regra é por item**, na data do atendimento: profissional + convênio + tipo de guia +
   modalidade, a mais específica vence, com vigência (reajuste não reescreve o passado).
   Valor fixo por guia ou percentual do que entrou.
   **A tabela de valores é configurada pelo Gerente** (pedido do dono, nov/2026): a tela
   **Repasse médico** fica no grupo *Tabela de preços* do Gerente, ao lado dos preços por
   convênio — quem negocia o valor com o médico é a direção, como quem negocia a tabela
   com o convênio. É a MESMA tela que o Financeiro abre a partir de Repasses (uma porta
   só). Editar exige `EditarFinanceiro` (o perfil Gerente tem todas as permissões).
   **Reajustar** encerra a regra antiga na véspera e cria a nova num único registro de
   auditoria — "quem mudou a Amil de 65 para 70, e desde quando?" tem resposta.
5. **O que não pode entrar aparece, com o motivo**: sem regra (com atalho para cadastrar),
   sem profissional no horário, particular de período já apurado no modelo antigo.
6. **Aguardando o convênio** é informado, nunca somado: as guias do médico já faturadas e
   ainda não pagas — é a pergunta que o médico faz ("e as minhas outras consultas?").
7. **Fechar e conferir** grava o fechamento com tudo congelado (paciente, convênio, guia,
   regra, valores), o nome de quem conferiu (o login, nunca a máquina), a saída **prevista**
   no caixa e a auditoria, num único `SaveChanges`.
8. **Pagar** realiza a saída. **Cancelar** pede motivo, devolve os itens para o próximo
   fechamento e cancela a saída prevista; o fechamento fica marcado, nunca some.
   Fechamento pago não se cancela: estorna-se o pagamento no Caixa antes.
9. **Estorno**: se uma receita já fechada é cancelada depois (estorno de atendimento,
   correção da conciliação), o desconto entra **negativo no próximo fechamento** do médico.
   O fechamento antigo, conferido e talvez pago, não é reescrito.
10. **Demonstrativo em PDF** no leiaute que a clínica já usava (resumo por convênio, valor
    por consulta, anexo por convênio e pagamento, assinatura de quem conferiu) e **CSV**.

## O modelo antigo

`RepasseApurado` continua no banco e visível, só leitura, no histórico — é registro de
dinheiro pago. O botão "Apurar" saiu da tela (função que saiu, sai inteira). Sessão
particular cujo atendimento cai num período já apurado pelo modelo antigo aparece
**bloqueada** no novo, para não ser paga duas vezes na transição.

## UX · DX · CX

- 🧑‍💻 **UX** — quem fecha o repasse vê, numa tela, por médico: o que está pronto, quanto, o
  que está bloqueado e por quê, e o que ainda depende do convênio. Fechar é um clique com
  confirmação do total; o PDF sai na hora. Nada some: bloqueado, cancelado e estornado
  aparecem marcados.
- 🛠️ **DX** — a regra mora em `FechamentoRepasseService` (Application), com testes do caso
  real da clínica, contra SQLite e Postgres. O contrato da tela é
  `Modelos/RepassePorGuiaRecebida.cs`. A trava contra pagar duas vezes é do banco. Para não
  quebrar: o item tem de continuar nascendo SÓ de receita **realizada** — status novo em
  `LancamentoFinanceiro` obriga a reler este serviço.
- 🤝 **CX** — o médico recebe o mesmo documento de antes, agora gerado e conferível, e a
  clínica deixa de correr o risco de pagar consulta que o convênio glosou ou ainda não pagou.
  Migration só aditiva (duas tabelas e uma coluna). Apps a publicar: **Financeiro** e
  **Gerente Geral** (que carrega o módulo Financeiro). O faturamento não muda.

## O que NÃO foi feito, e por quê

- **Exigir extrato bancário (OFX) para o item entrar.** A clínica pediu "conciliado e
  afirmou que recebeu" — é a receita realizada. Exigir também o `ConciliadoEm` do extrato
  deixaria de fora toda clínica que não importa OFX. Se a direção quiser, é um filtro a mais
  no mesmo serviço.
- **Glosa parcial proporcional.** O repasse por valor fixo paga a guia recebida; o
  percentual incide sobre o valor que de fato entrou, o que já reflete o pago a menor.
- **Envio do demonstrativo ao médico por e-mail/WhatsApp.** Fica para quando a direção
  decidir o canal (dado de paciente indo para fora: ver o ponto 10 da conformidade).
