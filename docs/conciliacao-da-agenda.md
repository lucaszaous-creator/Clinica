# Conciliar agenda

Tela: **Recepção → Agenda → "Conciliar agenda…"** (bits `EditarAgenda` + `LancarAtendimento`).

Responde a uma pergunta só: **o que ficou pendurado entre o horário e a sessão?**

## Por que ela existe

A clínica ainda não trabalha o check-in pela agenda — a recepcionista vai direto ao Novo
atendimento e lança. Desde set/2026 o lançamento **reconhece o horário do dia** e nasce
pendurado nele: a modalidade escolhida na tela passa a valer para o horário, o check-in é
carimbado e a guia nasce ali, sem encaixe nenhum. Uma sessão, um horário.

Só que isso vale **quando a data bate**. A agenda importada do Smart Clinic trouxe centenas
de horários nas datas do sistema antigo, e o paciente aparece no dia que aparece. Quando não
bate, o atendimento vira encaixe e o horário fica **"Aguardando" para sempre**.

Horário parado não é ruído de tela:

- infla a **ocupação** da agenda e o "Meu dia" do médico com paciente que não vem;
- **fica com a evolução** da sessão de verdade — a evolução importada é distribuída pela
  ordem da hora marcada, e o horário fantasma, mais cedo, a captura; a sessão real continua
  aparecendo em "Sessões sem evolução";
- e apaga o sinal de **falta**: enquanto tudo fica em aberto, não dá para saber quem não veio.

## O que a tela mostra

Horários que continuam em aberto com a data já passada há **2 dias ou mais** (a carência: o
horário de ontem pode estar só esperando o fechamento do dia), dentro dos últimos **120
dias**. Cada linha diz de quem é, quando era, se veio da agenda importada, há quantos dias
está parada — e o fato que decide tudo: **se há sessão lançada naquele dia para aquele
paciente**.

## As três respostas

| Situação | O que fazer | O que o sistema faz |
|---|---|---|
| **O paciente não veio** | "Foi falta" | Marca falta. Entra nos indicadores de falta e no histórico de relacionamento dele. |
| **Veio, e ninguém lançou** | "Aconteceu — lançar" | Lança o atendimento **pelo horário**, datado do **dia do horário** — não de hoje. As guias nascem com a data prevista daquele dia, então já entram no painel de pendências do faturamento. |
| **Veio, e já foi lançado por fora** | *(nada, por enquanto)* | A linha diz qual é o atendimento e **o botão de lançar fica apagado**. Lançar aqui criaria um **segundo jogo de guias** para a mesma sessão. |

⚠️ **A terceira é a mais comum no backlog da migração, e é por isso que o botão é apagado
em vez de "esperto".** Encerrar o horário apontando para a sessão que já existe pede um
estado que o sistema ainda não tem: "cancelado" contaria como cancelamento nos indicadores
— uma sessão que **aconteceu** inflando o número de cancelamentos —, e "faltou" culparia o
paciente por uma falta que não houve. Enquanto esse estado não existe, a tela **impede o
estrago** em vez de fingir que resolve. Deixe essas linhas como estão.

## A segunda lista: realizados sem atendimento

Embaixo, os horários marcados como **realizados que não apontam para atendimento nenhum**.
São diferentes dos de cima: não são uma pergunta que amadurece, são um horário dizendo
"Finalizado" sobre uma sessão que **não tem guia**. E são invisíveis em todo o resto do
sistema — o kanban os mostra em "Finalizado" (a etapa é derivada do status), e o repasse do
profissional os **exclui em silêncio**, porque exige o vínculo com o atendimento: o médico
atendeu e não é pago por aquela sessão.

Não há ação automática para eles: descobrir o que aconteceu exige falar com quem atendeu.
A lista existe para que eles parem de ser invisíveis.

## O que ela nunca faz

Não decide nada sozinha. Ausência de clique não é prova de falta — a clínica sabidamente
não dá esse clique —, e um serviço que fechasse horário por conta própria estaria
adivinhando. Quem responde é quem estava no balcão.
