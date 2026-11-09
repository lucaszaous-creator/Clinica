# Particular por horário — "a paciente tem plano, mas eu não atendo o plano dela"

**Nov/2026.** Pedido das médicas, em vídeo (a tela do Consultório, o dedo no "Unimed Costa do
Sol Intercâmbio" do cabeçalho de uma consulta que foi particular):

> "Essa paciente aqui tem plano. Mas o atendimento dela por mim foi particular, porque eu não
> atendo o plano dela. A gente tem que encontrar alguma forma de discriminar quando a consulta
> é particular — a endocrinologista não atende a Unimed, eu não atendo, a psiquiatra não
> atende. Para a gente ter um relatório de quantas consultas particulares têm. Uma forma de já
> no agendamento sinalizar que, embora a paciente tenha convênio, o atendimento foi particular,
> e isso entrar no sistema de gestão como consulta particular."

E o pedido da direção por cima: marcar particular hoje não tem **preço**, **lastro** nem
**vínculo**, e é preciso olhar a **cobrança no ato da marcação**.

## O que existia, medido antes de escrever

- "Particular" era só **o convênio da ficha** com `GeraGuia` desmarcado (parcela 60). O horário
  não tinha onde dizer "esta sessão é particular": o convênio é lido da ficha em todo lugar
  (motor de regras, fechamento, fila, relatórios).
- A saída que sobrava era **trocar o convênio da ficha para Particular** — e a acupuntura da
  mesma paciente, que É pelo plano, parava de gerar guia. Ou deixar como estava, e a consulta
  nascia com guia da Unimed: pendência de 2º código de uma guia que nunca vai existir, consulta
  do convênio "renovada" por quem não o atende, e nenhuma pergunta de dinheiro no Finalizar.
  **Nada falhava** nos dois caminhos.
- O que **já existia e não era alcançado**: a tabela de preço do particular por modalidade e
  especialidade (`PrecoParticular`), a pergunta de dinheiro no Finalizar com "pago agora" ou
  "fica a receber" (`FechamentoSessaoService`), o vínculo da receita com a sessão
  (`LancamentoFinanceiro.AtendimentoId`) e a aba **Particulares** da Conciliação (sessão
  realizada sem receita). Preço, lastro e vínculo existiam — só para quem tinha o particular
  **na ficha**. Faltava a porta: dizer isso do **horário**.

## As opções — onde mora o "particular"

| | 🧑‍💻 UX (quem usa) | 🛠️ DX (quem mantém) | 🤝 CX (a clínica) |
|---|---|---|---|
| **A. Trocar o convênio da ficha** (o que dava para fazer) | Um clique a mais na ficha, e outro para voltar; quem esquece de voltar quebra a sessão seguinte | Nada a escrever | A guia da acupuntura pelo plano some em silêncio; o relatório conta a PESSOA como particular, não a consulta |
| **B. Marca no horário** (`Agendamento.Particular` → `Atendimento.Particular`) — **a escolhida** | Um botão de dois estados no formulário de marcar, com o nome do plano de um lado e "Particular" do outro; o preço aparece na hora | Dois booleanos aditivos e UMA regra (`SessaoParticular`) que todos os leitores perguntam; o motor de regras não muda — as guias nascem "não aplicáveis", o mesmo mecanismo do particular de ficha | A ficha fica intacta; a mesma paciente tem consulta particular e acupuntura pelo plano; sem risco para o faturamento de convênio (quem não marca, não muda) |
| **C. Convênio por horário** (o horário escolhe QUALQUER convênio) | Resolve também o paciente com dois planos | O motor de regras, o TISS, a carteirinha, a cota e a autorização leem `paciente.Convenio` em dezenas de pontos: reescrita do coração do faturamento | Risco alto no app que fatura a clínica hoje, para um caso (dois planos) que ninguém pediu |
| **D. Automático pelo profissional** (cadastrar quais convênios cada um aceita) | Zero clique quando o cadastro está certo; surpresa quando não está | Cadastro novo (profissional × convênio) e mais uma fonte de verdade | Bom como **sugestão** por cima da B (pré-marcar "Particular" quando quem atende não aceita o plano) — fica como próximo passo, não substitui a marca |

**Recomendação (e o que foi feito): B.** A D entra depois, como pré-seleção do mesmo botão.

## O que foi entregue

**1 · Marcar.** As duas portas de marcação — o formulário do horário (grade, agenda do dia,
lista de espera, conversa do WhatsApp) e o Marcar/Lançar do balcão — ganharam a escolha
**"Atendimento por: [plano da ficha] | [Particular]"** (`EscolhaDeFormaDeAtendimento`). Some
quando a ficha já é particular (não há o que escolher). Marcado particular:

- a tela mostra **o preço da tabela do particular** para aquela modalidade/especialidade (ou o
  pacote que debita), pela mesma frase do Finalizar — três telas, um número;
- os avisos do plano (carteirinha, cota, consulta a renovar, autorização) **calam**; dívida,
  termo e pacote continuam;
- ficha "a definir" pode ser marcada sem escolher convênio — a falta de guia é a escolha
  registrada.

**2 · Atender e cobrar.** A sessão nasce **sem guia a faturar** (códigos "não aplicáveis": o
registro fica, nada vira pendência), **não renova** a consulta do convênio e **não reescreve**
o semáforo da ficha. O Finalizar **pergunta do dinheiro** e propõe o preço de tabela; quem não
cobra na hora cai na aba **Particulares** da Conciliação, como "Particular (ficha: Unimed…)". A
receita fica ligada à sessão e **não leva o plano** como pagador.

**3 · Ver.** Selo "Particular" no cartão da grade e na janela do horário; a linha da agenda do
dia diz "Particular (ficha: Unimed…)"; o cabeçalho do Consultório — o ponto do vídeo — ganha
**"Este atendimento é PARTICULAR — sem guia do convênio"** ao lado do plano da paciente.

**4 · Contar.** O relatório **Atendimentos por médico** ganhou a coluna **Particulares** (na
tela, no PDF e, pela coluna Convênio, no CSV) — o "quantas consultas particulares têm".

**5 · Corrigir o que já aconteceu.** A consulta do vídeo JÁ tinha sido atendida pelo plano. A
janela do horário atendido ganhou **"Foi particular…"** (e a volta, "Foi pelo convênio…"):
troca só essa resposta, sem estornar a sessão — dispensa as guias abertas, grava a trilha e põe
a sessão na conciliação dos particulares. Guia já baixada, em lote TISS ou em não conformidade
**impede** a troca (o fato já saiu da clínica).

### 🧑‍💻 UX
Um clique na marcação, com o nome do plano escrito no botão; o preço aparece antes de salvar; o
vermelho de "carteirinha vencida" não aparece em consulta particular; o Consultório diz que o
atendimento aberto é particular. Nenhuma tela nova, nenhum item de menu novo (**uma porta por
pergunta**: no horário em aberto a troca é no próprio Remarcar; no atendido, na janela dele).
Falha ao ler o preço não impede marcar — só não afirma valor.

### 🛠️ DX
- A pergunta "esta sessão é particular?" mora em **`Clinica.Domain/Regras/SessaoParticular`**.
  Quem lê só o convênio da ficha (ou só a marca) erra — os leitores (fechamento, fila, grade,
  relatório por médico, recuperação de guias, tablet, conciliação, cabeçalho) perguntam ali.
- O motor de regras **não foi tocado**: `AtendimentoService.AplicarParticular` torna os códigos
  "não aplicáveis" quando a sessão é particular, exatamente como já fazia para a ficha.
- **Nulo preserva**: `RemarcarAsync(particular: null)` e `LancarNoHorarioAsync(particular: null)`
  mantêm a marca — o Empurrar, o arrastar da grade e o faturamento remarcam sem saber dela.
- Migration **aditiva** (`ParticularPorHorario`): duas colunas `boolean` com padrão falso — o
  valor certo para toda linha antiga ("quem decide é a ficha").
- Textos de tela em `FormaDeAtendimentoNaTela` e `HorarioDaAgendaNaTela` (Application).
- Rede: `ParticularPorHorarioTests` (16 testes, do marcar ao relatório) e a linha da
  `TraducaoNoNpgsqlTests`.

### 🤝 CX
- **Risco em produção**: quem não marca "Particular" tem exatamente o comportamento de antes
  (teste `Quem_nao_marca_particular_continua_gerando_guia_como_sempre`). O faturamento
  (`Clinica.Desktop`) não ganhou tela nem opção nova; ele só deixa de ver guia das sessões que a
  recepção marcou como particulares — que é o objetivo.
- **Dinheiro**: a consulta particular de quem tem plano deixa de sair de graça sem ninguém ver —
  ela é perguntada no Finalizar e fica na conciliação até ter receita.
- **Publicar**: Recepção, Gerente Geral, Consultório e Financeiro (a moldura Blazor é dos
  quatro). O faturamento não precisa de release por isto.

## A cobrança NO ATO DA MARCAÇÃO — verificado, e ainda não construído

Hoje o dinheiro do particular entra em três momentos: **no Finalizar** (pago agora ou fica a
receber), **depois**, pela Conciliação, e **antes** só pela venda de **pacote** (que já grava o
dinheiro e debita a sessão no dia). Cobrar a consulta avulsa **na marcação** é diferente dos
três, porque o dinheiro entra **antes de a sessão existir** — e aí aparecem perguntas que o
código não pode responder sozinho.

| | 🧑‍💻 UX | 🛠️ DX | 🤝 CX |
|---|---|---|---|
| **1. Preço dito na marcação, cobrança no Finalizar** (o que está entregue) | A recepção informa o valor ao telefone; cobra com o paciente na frente | Nada novo | Não segura a vaga; falta sem aviso não custa nada ao paciente |
| **2. Conta a receber nascida na marcação** (prevista, com vencimento no dia da consulta, ligada ao horário) | Marcou → a conta existe; receber antes, no dia ou depois é a mesma tela "Receber no balcão" que já existe | Uma coluna aditiva (`LancamentoFinanceiro.AgendamentoId`), o Finalizar reconhecer a conta em vez de propor outra, e o cancelamento do horário cancelar a prevista | "Quem me deve" passa a mostrar consulta futura — precisa separar do que está vencido; nada de devolução, porque nada foi recebido |
| **3. Receber na marcação (sinal ou valor cheio)** | O Pix cai antes; a vaga fica garantida | A 2 + o que fazer com dinheiro RECEBIDO quando o horário é cancelado, remarcado ou vira falta | É aqui que a clínica ganha (menos falta) e é aqui que mora o risco: devolução, crédito e retenção são **política da clínica**, não regra de sistema |

**Recomendação:** a **2 como base** (é o "lastro e vínculo" desde a marcação, sem mexer em
dinheiro recebido) e a **3 por cima dela**, depois de a direção responder:

1. Paciente pagou adiantado e **cancelou com antecedência**: devolve, ou vira crédito para a
   próxima consulta?
2. Pagou e **faltou**: a clínica retém (tudo? um sinal?), ou vale a mesma regra do cancelamento?
3. O adiantado é **o valor cheio** ou **um sinal**? Se sinal, percentual fixo ou digitado?
4. **Remarcar** leva o pagamento junto (parece óbvio que sim — confirmar)?

Com essas quatro respostas a opção 3 vira uma parcela pequena; sem elas, qualquer escolha
feita no código seria uma política financeira decidida por quem programa.

## O que ficou de fora, com o motivo

- **Pré-marcar pelo profissional** (opção D): depende de cadastrar quais convênios cada um
  aceita. É o próximo passo natural — o botão já existe, falta quem o pré-selecione.
- **Preço por PROFISSIONAL**: a tabela do particular é por modalidade + especialidade. Se duas
  médicas da mesma especialidade cobram valores diferentes, a tabela ainda não separa.
- **Série e posto do tablet**: o serviço aceita a marca (`AgendarSerieAsync`), mas a marcação
  em série e o encaixe do tablet não têm o botão; marcam pelo convênio e a recepção corrige no
  horário.
- **Indicadores do Gerente por convênio**: os que contam a PESSOA pelo convênio da ficha
  continuam assim; o que conta a SESSÃO como particular é o relatório por médico e a
  conciliação. Revisar os painéis um a um é parcela própria.
- **Redes que não rodaram aqui**: a suíte contra o **Postgres** e os testes do **tablet**
  (`deploy/tablet`, `Clinica.Assinaturas.Tests`). A migration é só `AddColumn boolean`, e a
  consulta nova está na `TraducaoNoNpgsqlTests`; ainda assim, vale a rodada com
  `CLINICA_TESTES_POSTGRES` antes de publicar.

Evidências (prévia com dados fictícios): `docs/evidencias/particular-por-horario/`.
