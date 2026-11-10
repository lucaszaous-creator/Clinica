# Consulta vencida / a renovar — o selo na agenda e o contador no Início (nov/2026)

**Pedido da direção:** "a secretária precisa ter visualmente que há uma consulta a renovar ou
consulta vencida."

Até aqui o aviso existia, mas só para quem ia buscá-lo: a tela **Consultas de convênio**, o
lançamento do atendimento, o formulário de marcação, a ficha e o item "Conferir convênio e
cota…" escondido no "⋯" da linha. Na agenda — onde a secretária passa o dia — não havia sinal.

## O que mudou

| Onde | O que aparece |
|---|---|
| **Agenda do dia** (linha) | selo **Consulta vencida** (vermelho) ou **Consulta a renovar** (âmbar); a dica da linha traz a frase com a data |
| **Grade de horários** (cartão) | o mesmo selo; no cartão de meia hora, que só tem uma linha, um **!** vermelho/âmbar antes da hora, com a frase na dica |
| **Meu dia** (Consultório) | o mesmo selo ao lado do nome — quem atende vê o que a recepção vê |
| **Janela do horário** | o mesmo selo, ao lado de Encaixe/Particular |
| **Início da Recepção** | contador **Consultas a renovar** — "de pacientes que vêm hoje pelo convênio" — que abre as Consultas de convênio já em "Precisa renovar" |

Fotos (prévia, dados fictícios) em `docs/evidencias/consulta-a-renovar-na-agenda/`.

## Quando o selo fala — e quando se cala

A regra mora num lugar só: `ConsultaService.ARenovarNosHorariosAsync` (quem decide o horário é
`HorarioCobraRenovacao`; quem decide o paciente é o mesmo `Avaliar` da aba Consultas — a agenda
nunca discorda da aba). Testes em `ConsultaARenovarNaAgendaTests`.

- **Só horário em aberto** (marcado, no local, em atendimento). Cancelado, falta e sessão já
  concluída não levam selo.
- **Só o que vai pelo convênio.** Sessão particular — pela ficha ou marcada assim — não depende
  de consulta.
- **O horário de CONSULTA não leva selo**: ele é a renovação.
- **Quem nunca emitiu consulta não leva selo** (acenderia para a base inteira no primeiro dia —
  a mesma escolha do painel de pendências). Essa pessoa continua na aba Consultas, em "Nunca emitiu".
- **A data de referência é a do HORÁRIO**, não hoje: na grade da semana, a consulta que vale na
  segunda pode estar vencida na sexta.
- Na Agenda do dia o teto continua de **três selos** por linha (termo e guia pendente vêm antes);
  quando o teto corta o selo, a frase continua na dica da linha.

## Opções comparadas

| | A — selo na agenda + contador no Início (feito) | B — só o contador no Início | C — bloquear o "Chegou" com consulta vencida |
|---|---|---|---|
| 🧑‍💻 UX | o sinal está onde ela olha; zero clique para ver | um clique e uma troca de tela para saber QUEM | impede o check-in de quem está na frente dela |
| 🛠️ DX | uma regra, três leitores, uma ida ao banco por carga | menor | regra nova de recusa numa porta que o faturamento também usa |
| 🤝 CX | renovação cobrada com o paciente presente | depende de alguém lembrar de abrir | fila parada no balcão por causa de papel |

A **C** fica de fora de propósito: recusa nova no check-in é mudança de comportamento em
produção, e não foi pedida.

## UX · DX · CX

- 🧑‍💻 **UX** — o selo aparece na linha e no cartão sem clique; vermelho só para o que impede
  faturar, âmbar para o que dá para renovar agora. O contador do Início leva à lista já filtrada
  (o filtro fica visível no combo e sai com "Limpar filtro"). Falha de leitura: o selo some e a
  agenda continua; o contador mostra "—" e "não foi possível conferir", nunca zero.
- 🛠️ **DX** — nenhuma tabela nem migration. Peças novas: `ARenovarNosHorariosAsync` +
  `HorarioCobraRenovacao` (Application), `PedidoDasConsultas` (a ponte Início → Consultas, no
  desenho do `PedidoDaAgendaDoDia`). `SelosDaFila.Montar`, `CartaoDaGrade`, `MeuDiaNaTela.Montar` e `ItensDoBalcao`
  ganharam um parâmetro opcional. Custo: uma consulta a mais por carga da agenda/grade/Início
  (`ConsultasDosPacientesAsync`, já existente, só dos pacientes que cobram).
- 🤝 **CX** — nada muda para quem fatura: o faturamento (`Clinica.Desktop`) não foi tocado. Para
  publicar: Recepção, Gerente e Consultório (agenda do balcão e Meu dia).

## Regras de tela (como cada uma foi atendida)

1. *Uma porta por pergunta* — não há tela nova; o contador leva à Consultas de convênio.
3. *O que pede ação vira aviso* — é exatamente isto: o aviso saiu de dentro do "⋯".
6. *Responsivo* — fotografado em 1024 e 1366.

## O que NÃO foi feito

- Selo no **CRM** (a agenda de lá lê a ponte; fica para quando a direção pedir).
- Selo em sessão **já concluída**: depois da sessão o aviso não evita mais nada — quem trata
  disso é a aba Consultas e o painel de pendências.
- Aviso no **sino**: o selo e o contador já estão na tela de trabalho; sino para dezenas de
  pacientes por dia viraria ruído. Se a direção quiser, a regra já está num lugar só.
- A suíte no **Postgres** não rodou: não há coluna, migration nem consulta nova (a leitura
  reutiliza `ConsultasDosPacientesAsync`).
