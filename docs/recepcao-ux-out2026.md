# Recepção: busca de paciente, visão geral da agenda e horários de atendimento (out/2026)

> Pedido da direção: *"mudar as telas de buscas por pacientes tanto para buscar quanto para
> marcar atendimento, emitir documentos e afins respeitando nosso design system e também
> precisamos de uma boa visão geral da agenda. Queremos também melhorias sobre esse negócio
> de jornada de trabalho, que porra é essa? Tem uma coisa confusa no sistema"* — e deixou o
> desenho conosco.

## 1. O que era confuso na "jornada" (medido no código, não suposto)

| Sintoma | Causa |
|---|---|
| Duas telas editavam a mesma coisa, com frases diferentes | o formulário do profissional tinha "dias e horário" e a janela "Jornada, trava e bloqueios" também — uma dizia "nenhum dia = qualquer dia", a outra "nenhum dia = todos os dias" |
| Não dava para dizer a semana real de ninguém | UM horário para todos os dias (`AtendeDas`/`AtendeAte`): "segunda de manhã e quinta à tarde", ou o almoço, não cabiam |
| A Visão geral gritava "Jornada incompleta" e "—" | quem não preencheu era tratado como erro, com dois avisos laranja de largura inteira |
| "Trava" | uma palavra técnica para "recusar marcação fora do horário, em agenda fechada e em cima de outro paciente", escondida num interruptor sem explicação |
| A palavra "jornada" | é termo de RH (jornada de trabalho), não o que a recepção pergunta: **"quando o doutor atende?"** |

## 2. A decisão: "Horário de atendimento", por dia, com faixas, num editor só

**O que muda no domínio** (`Profissional.HorariosAtendimento`, migration aditiva
`HorariosAtendimentoPorDia`): cada dia da semana tem as SUAS faixas
(`1=08:00-12:00,14:00-18:00;4=14:00-19:00`). `FaixasEm(dia)` é a única pergunta: lista vazia =
não atende nesse dia; nulo = sem horário cadastrado (qualquer hora, como sempre foi). Os três
campos antigos continuam gravados (a união das faixas) para o binário anterior e a web de
leitura. Todos os leitores passaram a perguntar `FaixasEm`: a grade, a busca de vaga, as
pílulas de horário, os vãos livres, a ocupação (cada faixa medida sozinha — o almoço não é
vago) e os indicadores.

**O que muda na tela**
- A palavra **jornada sai da interface**; entra **"Horário de atendimento"**.
- **Um editor só** (`ConfiguracaoAgendaService.SalvarHorariosAsync`), aberto de Profissionais e
  salas e de "Mais ações" da agenda. O formulário do profissional mostra o resumo e leva ao
  editor — não edita mais horas (e PRESERVA o que leu, a regra do lugar 6).
- O editor é uma grade da semana: cada dia com um interruptor "atende" e as faixas
  ("+ faixa" para a tarde), "copiar segunda para os outros dias" e a frase do resultado
  ("seg · qua 08:00–12:00 e 14:00–18:00 · qui 14:00–19:00") antes de salvar.
- A trava vira uma frase: **"Recusar marcação fora deste horário, em agenda fechada ou em cima
  de outro paciente"** — desligada por padrão, com o lembrete de que a agenda da clínica avisa
  e não impede.
- Quem não cadastrou horário **não é erro**: a linha diz "horário não cadastrado" com o botão
  para cadastrar, e a ocupação conta só quem tem horário ("ocupação de 3 de 6 profissionais").

### Comparação das opções

| Opção | 🧑‍💻 UX | 🛠️ DX | 🤝 CX |
|---|---|---|---|
| Manter um horário para todos os dias e só trocar os textos | menos confuso, continua não cabendo a semana real | nada novo | a clínica continua sem conseguir cadastrar quem atende manhã e tarde em dias diferentes |
| **Faixas por dia num campo de texto + editor único** (escolhida) | descreve qualquer semana; um lugar para mexer | uma coluna aditiva e uma função (`FaixasEm`) que todos os leitores chamam | nenhuma migração de dados; o legado continua valendo até alguém editar |
| Tabela própria `HorariosProfissional` (linha por faixa) | igual | tabela, mapeamento, migration maior, mais leituras com `Include` | mais peças para divergir sem ganho para a tela |

## 3. A busca de paciente: um componente, três telas

`BuscaDePacienteNaTela` (Blazor, shell das telas) é o passo **"Quem é o paciente?"** de
Pacientes, Lançar/Marcar atendimento e Documentos — o desenho do mockup 05, já aprovado no
Novo atendimento WPF: coluna central de 760 px, campo de 48 px, a sugestão "com horário hoje"
(uma leitura da agenda do dia, opt-in), lista com moldura e cabeçalho, ↑ ↓ Enter Esc, o vazio
que oferece "Cadastrar «nome»" e o terceiro estado quando a busca falha. Escolhido o paciente,
a busca recolhe e vira a `FaixaDoPaciente` (documento, idade, telefone, convênio, "Trocar").

| Opção | 🧑‍💻 UX | 🛠️ DX | 🤝 CX |
|---|---|---|---|
| Cada tela com a sua busca (como era: três desenhos) | a recepcionista acha que abriu outro programa | três cópias que divergem | suporte explicando a mesma coisa três vezes |
| **Um componente de tela inteira + a faixa** (escolhida) | o mesmo gesto em toda tela; teclado completo | uma regra num lugar | o banco só é consultado quando alguém digita |

## 4. A visão geral da agenda

Uma **matriz profissional × dia**: cada célula diz quantos atendimentos e quanto do horário
está ocupado (barra), cinza onde ele não atende, hachura onde a agenda está fechada; clicar
abre o dia. No topo, quatro números (atendimentos, ocupação, horas livres, faltas e
cancelamentos) e, no lugar dos avisos laranja, UMA linha discreta com quem está sem horário e
o botão para cadastrar. O modo mês vira calendário de calor; o modo dia, uma linha do tempo
por profissional.

## 5. UX · DX · CX

- 🧑‍💻 **UX** — uma palavra que a recepção usa ("horário de atendimento"), um lugar para mexer,
  a mesma busca de paciente em toda tela, uma visão geral que se lê de relance.
- 🛠️ **DX** — `FaixasEm` é a pergunta única do horário; `BuscaDePacienteNaTela` é a busca única;
  o que as telas afirmam mora na Application, com teste.
- 🤝 **CX** — sem migração de dados nem perda: o cadastro antigo continua valendo até ser
  editado; a release é aditiva.
