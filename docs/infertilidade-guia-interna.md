# Atendimento de infertilidade: a guia interna em que ele é lançado (nov/2026)

**Pedido da direção** (áudios de 10/11/2026): "cadastrar o marido para consulta, atendimento de
infertilidade, e sinalizar na guia de quem vai passar — geriatria ou psiquiatria. Isso tem que
estar no sistema sinalizado para a gente saber para onde está lançando esse paciente, para
depois ficar rastreável. Já no agendamento."

**Premissa, dita pela direção e gravada aqui:** é controle INTERNO — relatório e separação de
repasse. **Não vai para o convênio.** O campo foi desenhado em cima dessa frase:

> `Agendamento.InfertilidadeGuiaDe` é classificação interna. Não entra em regra de guia, código
> TISS, lote nem XML. Quem atende continua sendo o profissional do horário, e nada do que o
> sistema emite para a operadora lê deste campo. `InfertilidadeGuiaInternaTests.
> A_marca_e_so_classificacao_interna_as_guias_geradas_sao_as_mesmas` quebra se alguém o ligar
> à geração de guia.

## O que foi feito

- **Na marcação** (formulário do horário e Marcar do balcão): a chave **Atendimento de
  infertilidade** e, ligada, o seletor **Lançar na guia de** — Geriatria ou Psiquiatria. Marcou
  a chave e não escolheu a guia: recusa com a frase que diz o que falta.
- **Na agenda**: o selo **"Infertilidade · guia Geriatria"** na Agenda do dia, na Grade, na
  janela do horário e no Meu dia do Consultório, com a dica que diz que é controle interno.
- **Na edição do horário**: dá para trocar ou tirar a guia; a troca fica na **trilha de
  auditoria** (quem, quando, de qual para qual).
- Quem remarca sem saber da marca (arrastar na grade, Empurrar, o app de faturamento) a
  **preserva**.

## Opções comparadas

| | A — marca + guia interna no horário (feito) | B — só a observação do horário | C — "caso do casal" com linha do tempo |
|---|---|---|---|
| 🧑‍💻 UX | um clique e uma escolha; selo na agenda | texto livre que ninguém filtra | mais completo, mais telas |
| 🛠️ DX | uma coluna, uma regra, um componente | zero | tabela e tela novas |
| 🤝 CX | rastreável e contável desde já | não gera relatório | fica para depois, se a clínica quiser |

## UX · DX · CX

- 🧑‍💻 **UX** — a secretária marca na mesma tela em que marca o horário; a guia é obrigatória
  quando a chave está ligada; o selo aparece sem clique para ela e para quem atende.
- 🛠️ **DX** — uma coluna aditiva (`20261110124442_InfertilidadeGuiaInterna`); regra e textos em
  `InfertilidadeNaTela` (Application); componente único `EscolhaDeInfertilidade`; a gravação
  passa pela `AgendaService` (`AgendarAsync` / `RemarcarAsync`: nulo preserva, vazio limpa).
  As guias possíveis são uma lista de dois códigos — para acrescentar outra, é uma linha.
- 🤝 **CX** — a clínica passa a saber, por horário, em qual guia interna cada atendimento de
  infertilidade foi lançado. Publicar: Recepção, Gerente e Consultório.

## O que NÃO foi feito

- **O relatório** (por período: quantos atendimentos de infertilidade em cada guia interna, com
  quem atendeu) e a **regra de repasse** por guia — o dado já está gravado; a tela é o próximo passo.
- O **vínculo marido ↔ esposa** entre as duas fichas.
- A marca no **Lançar** (atendimento sem horário prévio), no chamado da **lista de espera**, na
  **série** e na marcação pelo **CRM**.
- A suíte completa e o `TelasBlazorAbremTests` ficam com o CI do PR; os testes do tablet não rodaram.
