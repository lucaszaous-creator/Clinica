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

## Os três tipos de atendimento (áudio de 10/11, 09:56)

"Pode dividir o meu atendimento em três: infertilidade, ginecologia e suplementação para
gestante — fica ainda mais caracterizado o tipo de atendimento."

- O tipo de atendimento é a **especialidade da consulta**. Ginecologia já existia; **Infertilidade**
  e **Suplementação para gestante** entram pelo catálogo (`EspecialidadeCatalogoService.DaCasa`),
  nunca pelo enum embutido. A clínica pode renomear ou desativar qualquer um nas Configurações.
- Com isso a chave separada "atendimento de infertilidade" da primeira versão **saiu**: quem
  escolhe a consulta de Infertilidade já disse o que é, e a tela só pergunta em qual guia lançar
  (uma porta por pergunta).
- ⚠️ `FaturamentoCongeladoTests` foi atualizado de propósito: os dois tipos passam a aparecer no
  seletor de especialidade do app de faturamento **a partir da próxima release dele** (a
  garantia é de código, não de linha no banco). Nenhuma regra de guia os conhece.
- ⚠️ Onde a **cobertura do convênio** restringe as especialidades (Petrobras: geriatria,
  ginecologia e psiquiatria), consulta de Infertilidade ou de Suplementação **pelo plano** é
  recusada — entra como particular, ou o gerente acrescenta o tipo à lista do plano.

## O que foi feito

- **Na marcação** (formulário do horário e Marcar do balcão): ao escolher a consulta de
  **Infertilidade**, aparece o seletor **Lançar na guia de** — Geriatria ou Psiquiatria. Sem a
  guia escolhida, a tela recusa com a frase que diz o que falta.
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
  quando o tipo é Infertilidade; o selo aparece sem clique para ela e para quem atende.
- 🛠️ **DX** — uma coluna aditiva (`20261110124442_InfertilidadeGuiaInterna`); regra e textos em
  `InfertilidadeNaTela` (Application); componente único `EscolhaDeInfertilidade`; a gravação
  passa pela `AgendaService` (`AgendarAsync` / `RemarcarAsync`: nulo preserva, vazio limpa).
  As guias possíveis são uma lista de dois códigos — para acrescentar outra, é uma linha.
- 🤝 **CX** — a clínica passa a saber, por horário, em qual guia interna cada atendimento de
  infertilidade foi lançado. Publicar: Recepção, Gerente e Consultório.

## O relatório (10/11/2026 — "precisamos lastrear e deixar nos relatórios")

Uma porta por pergunta: o dado entrou no relatório que a gerência já usa para a folha e o
repasse — **Financeiro → Repasses → Atendimentos por profissional** — em vez de numa tela nova.

- **Dois quadros** (na visão geral e no detalhe de cada profissional): **Consultas por tipo de
  atendimento** (Infertilidade, Ginecologia, Suplementação para gestante… com atendimentos,
  particulares e recebido) e **Infertilidade por guia interna** (Geriatria × Psiquiatria).
- **Na linha do atendimento**: o tipo no procedimento ("Consulta · Infertilidade") e o selo
  "guia interna: Geriatria".
- **CSV**: duas colunas novas no FIM ("Tipo de atendimento", "Guia interna (infertilidade)") —
  a planilha de quem já usava as dez primeiras não desloca.
- **PDF**: o procedimento leva a guia interna, e o cabeçalho traz as duas contagens.
- O tipo é lido pelo CATÁLOGO: os tipos novos não estão no enum, e ler só o enum os deixaria fora
  do relatório em silêncio (era o que o relatório fazia até aqui).
- 🛠️ Quem conta é `AtendimentosPorProfissionalService.ContarPorTipo` / `ContarPorGuiaInterna`,
  com teste; a tela só desenha (`ContagensDoRelatorio`). Sem consulta nova ao banco.
- 📸 A foto achou um defeito do próprio acréscimo: com os quadros abaixo, o corpo flexível do
  formulário espremia a tabela dos profissionais até UMA linha. Corrigido em `repasses.css`.

## O que NÃO foi feito

- A **regra de repasse** por guia interna (percentual por profissional) — o relatório mostra
  a contagem e o recebido; a conta do repasse continua a das regras já cadastradas.
- O relatório conta atendimentos REALIZADOS (o mesmo critério da folha); o que está só marcado
  aparece na agenda, pelo selo.
- O **vínculo marido ↔ esposa** entre as duas fichas.
- A marca no **Lançar** (atendimento sem horário prévio), no chamado da **lista de espera**, na
  **série** e na marcação pelo **CRM**.
- A suíte completa e o `TelasBlazorAbremTests` ficam com o CI do PR; os testes do tablet não rodaram.
