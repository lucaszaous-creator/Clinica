# Conector do web service TISS — o servidor que fala com a operadora

**Pedido da direção (nov/2026):** parar de digitar no portal da operadora o que o sistema já
sabe, começando pela Saúde Petrobras. A elegibilidade pelo web service já foi provada com o login
da clínica (`docs/tiss-webservice-petrobras.md`). Este documento cobre a peça que faltava para
isso sair da linha de comando e chegar às telas: **um serviço no servidor** que lê uma fila no
banco, fala com a operadora e grava o resultado.

Regra de fundo, decidida pela direção: **nada é enviado à operadora sem alguém, logado,
autorizar aquela guia específica.**

## Em uma tela

| Pergunta | Resposta |
|---|---|
| O que é? | `src/Clinica.Tiss.Conector`: um serviço Linux (systemd) na VPS 1, ao lado do banco. Não escuta em porta nenhuma; só faz conexões de saída (banco local e HTTPS da operadora). |
| Como a tela fala com ele? | Pela tabela `TransacoesTiss`. A tela chama `FilaTissService` (grava o pedido); o conector reserva, processa e grava o desfecho; a tela relê. |
| O que funciona hoje? | Com rede de mentira, de ponta a ponta: **elegibilidade, envio de lote, situação do protocolo e cancelamento**, mais a conferência da guia sem baixa e a baixa em lote das recebidas. Na operadora de verdade, **só a elegibilidade foi provada**. |
| O que ainda NÃO está provado? | Que a operadora aceita o lote, devolve o protocolo na hora, devolve o número da guia dela e aceita cancelamento por este canal. Nada disso foi enviado a ela; ver "O que não foi verificado" e `docs/tiss-webservice-petrobras.md`. Faltam também as telas e a migration (integração). |
| O que precisa estar ligado para uma guia sair? | **Duas chaves**, as duas desligadas por padrão: `TISS_ENVIO_HABILITADO=true` no servidor **e** o envio ligado para o convênio. |
| O envio é de uma guia ou de várias? | De um **lote** de 1 a 100 guias (uma guia só é o lote com um item). A operadora responde **por guia**, e a baixa em lote só alcança o que ela recebeu. Ver "Envio em lote e baixa em lote". |
| Uma guia pode ir duas vezes? | O sistema não reenvia sozinho, nunca — nem o lote, nem guia alguma dele. Ver "A regra que não se negocia". |
| Onde fica a senha da operadora? | Só o MD5 dela, cifrado, na configuração do banco. A chave que abre fica numa variável de ambiente no servidor, fora do banco. Ninguém lê a senha de volta. |
| Depende de quê para entrar em produção? | Da migration das tabelas e das telas (integração), da instalação deste roteiro, da **decisão LGPD** (fim do documento) e de um primeiro envio de UMA guia acompanhado no portal. |

## UX · DX · CX

### As opções para "quem fala com a operadora"

| | 🧑‍💻 UX (quem fatura) | 🛠️ DX (quem mantém) | 🤝 CX (a clínica) |
|---|---|---|---|
| **(A) Conector no servidor + fila no banco** ✅ | A tela pede e continua livre; o resultado aparece em segundos. Se a operadora está fora do ar, a consulta tenta de novo sozinha e a tela mostra em que pé está. Fechar o app não perde o pedido. | Um projeto novo no molde da ponte do CRM; a regra de fila mora num serviço da Application com teste; a rede é trocada por uma de mentira nos testes. Um lugar só guarda a credencial. | A senha da operadora fica em UMA máquina, não em cada computador do balcão. Um serviço a mais para instalar e vigiar na VPS. Custo de máquina: desprezível (um processo parado a maior parte do tempo). |
| **(B) O app desktop chama a operadora direto** | Resposta na hora quando tudo vai bem. Quando a operadora demora, a tela espera; se o app fecha no meio de um envio, ninguém sabe se a guia chegou. | Sem serviço novo. Mas a credencial (ou a chave que a abre) teria de estar em todo computador da clínica, e a regra "não reenviar" dependeria de cada app estar na mesma versão. Cinco apps, um deles o faturamento em produção. | Menos uma peça no servidor. Mais risco: senha da operadora espalhada, IP de saída diferente por máquina (se a operadora um dia liberar por IP, quebra), e nenhum registro central do que foi dito à operadora. |
| **(C) Hub/intermediário pago** | Depende do fornecedor. | Menos código de mensagem; mais um contrato técnico para acompanhar. | Custo por guia ou mensalidade; os dados dos pacientes passam por mais uma empresa, o que exige contrato de tratamento de dados e a decisão do ponto 10 **antes** do código (`CLAUDE.md`, compromisso de conformidade). Nenhum hub confirmado cobre a Saúde Petrobras (a conectividade dela é da própria operadora). |

**Recomendação: (A).** É a única em que a credencial fica num lugar só, o envio sobrevive ao
fechamento do app e a regra contra duplicidade é uma, no servidor. (B) fica como o que já existe
para diagnóstico: a ferramenta `tools/TissPetrobras`, que não grava nada.

### O que esta entrega faz com cada eixo

- 🧑‍💻 **UX** — Nenhuma tela muda nesta entrega: ela é o motor. O que a tela vai poder mostrar
  está em `TransacaoTiss.Resultado`, sempre uma frase curta e verdadeira: "Elegível. Validade da
  carteira: 31/12/2027.", "Não elegível: …", "O envio pelo web service está desligado para
  Petrobras. Nada foi enviado à operadora.", "Falhou: confira no portal da operadora antes de pedir
  de novo.". Três estados finais que não se confundem: **Concluída** (a operadora respondeu o que
  se perguntou), **Falhou** (alguém precisa olhar) e **Cancelada** (nada saiu).
- 🛠️ **DX** — Peças novas: 2 entidades (`TransacaoTiss` e as guias do lote, `TransacaoTissGuia`),
  2 serviços (`FilaTissService`, `CredenciaisTissService`), 10 métodos de repositório, 1 projeto (`Clinica.Tiss.Conector`) e a
  pasta `deploy/tiss-conector/`. A ordem que importa (reservar → conferir as chaves → preparar →
  **carimbar o envio** → rede → gravar) mora num lugar só, o `ExecutorDeTransacoesTiss`; um
  processador novo só monta a mensagem e lê a resposta. Mais de 100 testes na família, sem rede real.
- 🤝 **CX** — Risco em produção hoje: baixo. O serviço não escuta em porta, o usuário dele no banco lê seis tabelas (sem a ficha do paciente) e grava em três, e o envio de guias está atrás de duas chaves desligadas. O
  que muda no dia de quem atende: nada, até uma tela usar a fila. O que a clínica passa a ter de
  cuidar: uma senha de operadora cadastrada no servidor (e trocada quando a do portal mudar) e a
  cópia da chave de cifra fora do servidor.

## As três frentes ligadas (nov/2026): o que mudou

A fila e o conector (este documento), as mensagens do padrão (`docs/tiss-webservice-petrobras.md`,
"Lote, situação do protocolo e cancelamento") e a guia conferida na baixa foram ligadas. O que isso
acrescentou:

| O que | Onde | Em uma frase |
|---|---|---|
| Os três processadores falam com a operadora | `ProcessadoresDeGuia.cs` | Envio de lote, situação do protocolo e cancelamento montam a mensagem do padrão e leem a resposta. |
| Um número de guia só | `FilaTissService.NumeroDoPrestador` → `NumeroGuiaPrestadorTiss.Gerar` | "C12", depois "C12R2": a fila reserva o número que a mensagem sabe ler de volta. |
| Um transporte só | `TissWebserviceCliente.EnviarAsync` | O conector usa o mesmo da ferramenta de diagnóstico; o `TransporteTissHttp` saiu. |
| Endereços da Saúde Petrobras | `CredenciaisTissService.EnderecoAsync` / `EnderecoPadrao` | Os quatro endereços do manual são o padrão do convênio: ligar o envio não exige digitar URL. Continua podendo ser trocado. |
| Conferir sem baixar | `FaturamentoService.ConferirGuiaAnsAsync` | Grava a guia ANS conferida para ela poder ser ENVIADA antes da baixa. `DarBaixaAsync` não mudou. |
| Baixa das recebidas | `BaixaDasGuiasEnviadasService.BaixarRecebidasAsync` | Dá baixa, com quem está logado, em cada guia que a operadora recebeu. |
| Leituras para as telas | `FilaTissService.ProntasParaEnvioAsync`, `SelosDeEnvioAsync`, `EnviosRecentesAsync` | A lista de onde se monta o lote, o selo da linha e os envios com as guias. |

### Como a resposta da operadora vira desfecho

As decisões são as das mensagens; o conector só as aplica:

| A operadora… | Envio de lote | Situação do protocolo | Cancelamento |
|---|---|---|---|
| fez o que se pediu | `Concluida`, protocolo + resultado por guia | `Concluida`; atualiza as guias do envio | `Concluida`; a guia sai da baixa e fica livre |
| respondeu que NÃO (`mensagemErro`, "não cancelado") | `Falhou`; guias livres para um pedido novo | `Falhou` | `Falhou`; a guia continua recebida |
| não deixou saber (falha de login/schema, erro SOAP, página de erro, "em processamento") | `Falhou` + "confira no portal"; **nunca** reenvio | `Falhou`, sem repetir | `Falhou` + "confira no portal"; **nunca** repetido |
| não respondeu (rede) | `Falhou` + "confira no portal"; **nunca** reenvio | volta à fila (até 3 vezes) | `Falhou` + "confira no portal" |

⚠️ O padrão **não traz situação por guia** na consulta de protocolo: cada guia volta com números,
valores e glosas. Guia citada sem glosa conta como recebida; com glosa, glosada.

### O caminho de uma guia, do balcão à baixa

1. **Conferir** (`ConferirGuiaAnsAsync`): a guia ANS é gravada, sem baixa. Só guia que pede a
   conferência (Saúde Petrobras) e que ainda não tem baixa. Reconferir substitui.
2. **Montar o lote** (`ProntasParaEnvioAsync` → `EnfileirarEnvioAsync`): é o ato que autoriza
   aquelas guias. A recusa vem por linha.
3. **O conector envia.** Antes de qualquer coisa sair ele confere de novo se cada guia ainda
   existe, ainda está sem baixa e ainda está conferida; uma guia fora recusa o lote inteiro.
4. **A operadora responde por guia.** O selo de cada linha (`SelosDeEnvioAsync`) diz: na fila,
   enviada, recebida, glosada (com o motivo), falhou (com o motivo) ou cancelada.
5. **Dar baixa nas recebidas** (`BaixarRecebidasAsync`): quem clica é quem assina a baixa.
6. **Consultar o protocolo** (`EnfileirarStatusDoEnvioAsync`) atualiza as mesmas guias; uma
   glosada que a operadora acata passa a recebida e entra na próxima baixa.

### A baixa das recebidas, com as regras que ela herda

- O número da baixa é o que a operadora devolveu; se ela não devolve, o número que a clínica
  mandou ("C12"), e a observação diz isso e o protocolo.
- É a **mesma** `DarBaixaAsync`. Por isso a **regra do formato do número por convênio**
  (`RegraNumeroGuia`) vale aqui: a Petrobras está cadastrada como "letras e números, sem
  pontuação", e um número devolvido com hífen ou barra é **recusado naquela linha**, com a frase
  da regra. A guia continua pendente e continua na lista; resolve-se dando baixa nela pela tela,
  com o número conferido no portal — ou, se a operadora de fato numera com pontuação, ajustando o
  formato do convênio em Configurações. **Não se sabe ainda que formato ela devolve.**
- Falha numa guia não aborta as outras; a lista volta com a frase de cada uma.
- Chamar duas vezes não baixa duas vezes: a leitura só traz o que ainda não tem baixa.
- Guia baixada à mão no meio do caminho simplesmente não aparece mais.

### UX · DX · CX desta ligação

Opções para "quando a guia ANS é gravada", já que o envio precisa dela antes da baixa:

| | 🧑‍💻 UX | 🛠️ DX | 🤝 CX |
|---|---|---|---|
| **(A) Conferir como ato próprio, sem baixa** ✅ | A pessoa confere a guia uma vez; o envio e a baixa a reaproveitam. A baixa manual continua igual. | Um método novo no caminho único da baixa, sem mudar `DarBaixaAsync`; a baixa das enviadas reapresenta a guia gravada. | O que vai à operadora foi conferido por alguém, com nome e hora na trilha. |
| (B) Enviar o que o sistema montar sozinho, sem conferência | Nenhum passo a mais. | Menos código. | Contraria o pedido da direção (toda guia Petrobras passa pela conferência) e manda à operadora dado que ninguém viu. |
| (C) Dar baixa primeiro e enviar depois | Fluxo de hoje, com um passo a mais no fim. | Nada novo na baixa. | A baixa afirmaria "efetivada no convênio" antes de a operadora ter recebido: garantia aparente. |

- 🧑‍💻 **UX** — Nenhuma tela muda nesta entrega. O que as telas vão poder mostrar ficou pronto e com
  a frase certa: a recusa por linha ao montar o lote, o selo por guia ("Recebida pela operadora,
  protocolo 987654321", "Glosada: 1001 - …", "Falhou: …", "Não enviada: O envio … está
  desligado"), e o resumo da baixa ("1 guia(s) baixada(s); 1 NÃO: [12] Este convênio usa letras
  e números na guia, sem pontuação…").
- 🛠️ **DX** — Saíram duas duplicatas (o transporte e a convenção do número). Um processador é
  agora "abrir a credencial + montar o envelope + ler a resposta"; a ordem que protege contra
  duplicidade continua num lugar só. Os testes de ponta a ponta usam respostas conferidas no XSD
  oficial e conferem no mesmo XSD o XML que sai.
- 🤝 **CX** — O conector passou a ler a guia e a conferência dela (carteira e nome do beneficiário
  estão ali), e **não** a ficha do paciente nem o atendimento. O envio continua atrás das duas
  chaves desligadas. O que a clínica ganha quando ligar: a guia digitada uma vez, o número e o
  protocolo voltando sozinhos, e a glosa aparecendo na linha no mesmo dia.

## Como funciona

```
 app (tela)                      banco (VPS 1)                    conector (VPS 1)            operadora
 ───────────                     ─────────────                    ────────────────            ─────────
 FilaTissService
   .Enfileirar…  ───────────►  TransacoesTiss: Pendente
                                + linha na Auditoria
                                                    ◄───────────  reserva (UPDATE condicional)
                               TransacoesTiss: EmEnvio
                                                                  confere as 2 chaves de envio
                                                                  monta a mensagem
                               EnvioIniciadoEm + XML sem senha ◄─ carimba ANTES da rede
                                                                  ─────────── HTTPS ──────────►
                                                                  ◄────────── resposta ────────
                               Concluida / Falhou / Pendente  ◄─  grava o desfecho
 FilaTissService
   .ObterAsync   ◄───────────  Resultado (frase para a tela)
```

### Os estados

| Estado | Quer dizer | Volta sozinho para a fila? |
|---|---|---|
| `Pendente` | Na fila. | — |
| `EmEnvio` | Um conector reservou. | Se a reserva vencer (5 min): elegibilidade e status, sim; envio e cancelamento cuja mensagem já saiu, **nunca** (viram `Falhou`). |
| `Concluida` | A operadora respondeu o que se pediu. | Não. |
| `Falhou` | Não deu certo; alguém precisa olhar. | Não. |
| `Cancelada` | Nada saiu: quem pediu desistiu, ou o conector recusou (envio desligado, sem credencial, sem endereço). | Não. |

### A regra que não se negocia: envio de guia nunca é reenviado sozinho

Quando a rede cai no meio de um envio, **ninguém sabe se a operadora recebeu**. Reenviar é
arriscar a mesma guia duas vezes (glosa 1308, "guia já apresentada"). Por isso:

1. o conector **carimba** `EnvioIniciadoEm` e guarda o XML **antes** de ir à rede;
2. transação que escreve na operadora (envio, cancelamento) e já tem esse carimbo só termina:
   `Concluida` ou `Falhou`. Não importa o que o processador peça, a fila não a devolve;
3. se o conector morrer depois de carimbar, a transação é encerrada como `Falhou` com a frase
   "Confira no portal da operadora antes de pedir um novo envio" — e **não** é entregue a outro conector;
4. pedir de novo é ato de **gente**: um pedido novo, com operador e trilha. A fila recusa a
   guia que está num envio na fila, em andamento, ou já recebida pela operadora — e, desde a
   correção de fluxo de 11/11/2026, recusa também a guia cujo último envio **saiu e falhou**
   ("não se sabe se chegou"), salvo quando o pedido a declara em `conferidasNoPortal`: alguém
   conferiu no portal que ela não está lá. A declaração é gravada na auditoria
   (`ReenvioTissAposConferenciaNoPortal`, com o operador e a guia). Envio que
   falhou ANTES de sair não pede declaração, e a **recusa clara** da operadora (ela respondeu que
   não, com o motivo) também não: a dúvida se reconhece pela frase "Confira no portal da operadora
   antes de pedir um novo envio" no erro gravado (`FilaTiss.MarcaDeDuvida`), que a fila preserva
   mesmo quando corta um erro comprido (`FilaTiss.CortarErro`);
5. a regra vale para o **lote inteiro e para cada guia dele**: lote que falhou marca todas as
   guias como "falhou" e nenhuma volta sozinha; guia glosada só é reenviada por um pedido novo,
   e sai com **outro número** (`C12`, depois `C12R2`), para a operadora nunca ver o mesmo número
   duas vezes.

Elegibilidade e consulta de protocolo só perguntam (com o freio de "Limite de chamadas", abaixo): tentam de novo até 3 vezes (espera de 1 min,
depois 5 min) **quando a operadora não respondeu**. Quando ela respondeu "não" (login recusado,
mensagem fora do padrão), não há nova tentativa: repetir não muda a resposta, e login recusado
repetido pode bloquear o acesso da clínica.

### Limite de chamadas (nov/2026)

Pedido do dono, logo depois da instalação na VPS: **um freio para a clínica não ser bloqueada pela
operadora.** Até aqui não havia nenhum: o laço emendava a próxima transação assim que a anterior
terminava, e a mesma carteira pedida duas vezes ia duas vezes à operadora. A Saúde Petrobras **não
publica limite**; os números abaixo são escolha nossa, cautelosa, e mudam por variável de ambiente.

| Freio | O que faz | Onde mora | Padrão (variável) |
|---|---|---|---|
| **Intervalo mínimo** | Entre o fim de uma ida e o começo da próxima à **mesma operadora** (reconhecida pelo servidor do web service), espera. Vale para toda transação: elegibilidade, envio, situação do protocolo e cancelamento. | `IntervaloEntreChamadasTiss`, chamado pelo `ExecutorDeTransacoesTiss` **antes do carimbo** | 3 s (`TISS_INTERVALO_MINIMO_SEGUNDOS`, 0 a 60; 0 desliga) |
| **Teto diário de elegibilidade** | Por convênio, contado pelas **idas reais** do dia na hora da clínica. Batido o teto, a consulta volta a `Pendente` com a frase "Limite diário de consultas à operadora atingido; a consulta sai amanhã ou quando a direção aumentar o limite." Não é falha nem recusa da operadora, e a reserva desfeita não gasta tentativa. É conferida de novo a cada 15 min (o teto pode ter subido). | `FilaTissService.AdiarPorLimiteDiarioAsync`, chamado pelo executor antes de preparar a mensagem | 300 (`TISS_LIMITE_DIARIO_ELEGIBILIDADE`, 0 a 100000) |
| **Mesma carteira no mesmo dia** | `EnfileirarElegibilidadeAsync` devolve a consulta que ainda está na fila, ou a **concluída hoje**, da mesma carteira no mesmo convênio, em vez de criar outra. A tela mostra a resposta com "consultado hoje às HH:MM". Uma nova ida só pelo botão **Consultar de novo** (`consultarDeNovo: true`), que conta no teto; nem ele duplica uma consulta que ainda está na fila. | `FilaTissService.EnfileirarElegibilidadeAsync` e `EnvioTissNaTela.DaElegibilidade` | — |

**O que conta como "ida real".** Só a transação cuja mensagem **saiu** no dia: a que tem o carimbo
`EnvioIniciadoEm`, posto imediatamente antes da rede. A soma é a das tentativas delas
(`IClinicaRepositorio.IdasDeElegibilidadeTissAsync`). A transação recusada antes da rede (sem
credencial, sem registro ANS, envio desligado) termina sem o carimbo e **não conta**, mesmo com
`Tentativas = 1` — foi o caso real da VPS. Não houve migration: o carimbo já existia.

**Ver o consumo.** `comando.sh situacao Petrobras` mostra, na linha do convênio, as idas reais de
hoje e o teto deste servidor: `Petrobras: login cadastrado, senha cadastrada, envio desligado, hoje
1 de 300 consultas.` A linha de partida no `journalctl` diz os dois números configurados, e cada
consulta adiada aparece como `… adiada: teto diário de 300 consultas atingido. Fica Pendente.`

**Subir o teto.** Mude `TISS_LIMITE_DIARIO_ELEGIBILIDADE` em `/etc/clinica-tiss-conector/conector.env` e
`systemctl restart clinica-tiss-conector`. As consultas paradas saem na reconferência seguinte (até
15 min), sem ninguém pedir de novo.

**A regra do envio não mudou.** O intervalo acontece **antes** do carimbo e do primeiro byte; ele só
atrasa uma ida, nunca a repete. Envio e cancelamento sem resposta continuam terminando em `Falhou`
com "confira no portal", e o teto diário não vale para eles (só para elegibilidade).

#### UX · DX · CX do limite

Opções para "como frear":

| | 🧑‍💻 UX | 🛠️ DX | 🤝 CX |
|---|---|---|---|
| **(A) Intervalo por operadora + teto diário de elegibilidade + reaproveitar a carteira do dia** ✅ | Quem pergunta duas vezes a mesma carteira tem a resposta na hora, com a hora da consulta. Em dia normal ninguém percebe os 3 s. Batido o teto, a frase diz o que acontece e quando. | Três peças pequenas, cada uma num lugar: o intervalo no conector (memória do processo), o teto e o reaproveitamento na Application, com teste. Sem migration. | A clínica deixa de bombardear a operadora com repetição; o risco de bloqueio cai sem tirar função de ninguém. Os números mudam no servidor, sem release dos apps. |
| (B) Só o intervalo | Igual em dia normal; a mesma carteira continua indo várias vezes. | Uma peça só. | Não protege de um dia de muitas consultas repetidas, que é o padrão que mais parece abuso para a operadora. |
| (C) Teto rígido também para envio de guia | O faturamento poderia parar no meio do mês por um número que ninguém mediu. | Mais uma regra no caminho do envio, o mais delicado do sistema. | Risco de atrasar faturamento; e envio já sai por ato explícito de alguém, em lote de até 100. |

- 🧑‍💻 **UX** — Na tela da elegibilidade: a resposta reaproveitada aparece na hora com "Resposta da
  operadora, consultado hoje às 14:02."; o botão vira **Consultar de novo**; o teto aparece como
  aviso de atenção, com o pedido ainda cancelável. Nada pisca e nenhuma consulta some.
- 🛠️ **DX** — Peças novas: `IntervaloEntreChamadasTiss` (conector), `AdiarPorLimiteDiarioAsync` e
  `IdasDeElegibilidadeHojeAsync` (fila), dois métodos de repositório com teste de tradução, duas
  variáveis no `conector.env`. O relógio do intervalo é injetável: os testes avançam o tempo sem
  esperar. Testes em `LimiteDeChamadasTissTests`, `ConectorTissTests` e `EnvioTissNaTelaTests`.
- 🤝 **CX** — Menos chance de a operadora bloquear o login da clínica (que pararia a elegibilidade
  e, depois, o envio). Custo: nenhum. O que muda no dia: a carteira consultada de manhã não é
  consultada de novo à tarde sem alguém pedir.

**Limites honestos.** O intervalo é por processo e em memória: depois de um reinício a primeira ida
sai sem esperar (um reinício leva mais que 3 s). A contagem soma as tentativas das transações que
saíram no dia; uma consulta que saiu, ficou sem resposta e, numa segunda reserva, foi recusada
antes da rede contaria uma tentativa a mais (erra para o lado cauteloso). Uma cadeia de novas
tentativas que atravessa a meia-noite conta inteira no dia da última saída. Nada disso foi medido
contra a operadora: **não sabemos o limite real dela**.

### Envio em lote e baixa em lote

Pedido do dono: "se for envio em lote precisamos nos certificar da baixa em lote também".

**O pedido.** `EnfileirarEnvioAsync(convênio, lista de guias, operador)` cria UMA transação com um
item por guia (`TransacaoTissGuia`). Antes de entrar na fila, cada guia é conferida, e a recusa
vem **por guia**, com a frase que a tela mostra na linha dela:

| Recusa | Frase |
|---|---|
| Lista vazia | "Escolha ao menos uma guia para enviar à operadora." (do lote) |
| Mais de 100 | "Um lote leva até 100 guias e este tem N: divida o envio." (do lote) |
| Guia sem a guia ANS conferida | "Confira a guia no padrão ANS antes de enviar: esta ainda não foi conferida." |
| Guia já baixada | "Esta guia já tem baixa: não é enviada de novo." |
| Guia de outro convênio | "Esta guia é de outro convênio (X): um envio leva guias de um convênio só." |
| Tipo de guia diferente | "Um envio leva um só tipo de guia: separe as guias de consulta das de procedimento (SP/SADT)." |
| Guia já em outro envio | "…já está num envio à operadora, na fila ou em andamento." / "…já foi recebida pela operadora (protocolo N). Falta dar baixa, não enviar de novo." / "…num envio já protocolado e ainda sem resposta: consulte o protocolo…" |
| Repetida na lista / não encontrada | frase própria de cada uma |

**Uma guia recusada recusa o pedido inteiro** (`EnvioTissRecusadoException`, com a conferência
dentro). Lote enviado pela metade sem a pessoa saber é pior que lote não enviado: a tela mostra
as frases, a pessoa tira as linhas e pede de novo. Para avisar **antes** do clique, a tela chama
`ConferirEnvioAsync`, que é a mesma conta sem gravar nada.

**O resultado.** A operadora pode receber o lote e glosar uma guia. Por isso `ConcluirAsync`
grava o protocolo na transação e, em cada item, a situação daquela guia:

| Situação do item | Quer dizer | A baixa segue? |
|---|---|---|
| `Recebida` | A operadora recebeu esta guia. | **Sim** |
| `Glosada` | Ela respondeu com glosa nesta guia (o motivo fica na linha). | Não: alguém precisa olhar. |
| `Pendente` | Ainda sem resposta sobre esta guia (a operadora não a citou). | Não: consulte o protocolo. |
| `Falhou` | O lote não foi, ou não se sabe se foi. | Não. |

Duas regras de cautela: guia que a resposta **não cita** não vira "recebida" por omissão quando
a operadora detalhou as outras; e resposta **sem protocolo e sem detalhe** não marca guia nenhuma
como recebida. Só quando há protocolo e a operadora não detalha guia alguma é que o lote inteiro
conta como recebido.

**A consulta de status** (`EnfileirarStatusDoEnvioAsync(idDoEnvio, operador)`) atualiza os
**mesmos itens** do lote consultado: uma guia que estava sem resposta passa a recebida ou glosada.

**A baixa em lote.** O conector não dá baixa (não tem sessão de ninguém). O que fica pronto é a
leitura `GuiasRecebidasSemBaixaAsync(idDoEnvio)`: as guias `Recebida` que ainda não têm baixa,
cada uma com o número devolvido pela operadora (quando ela devolve), o número que a clínica mandou
e o protocolo do lote. O que a integração liga em cima disso:

1. a tela do lote acompanha a transação e mostra o resultado por linha;
2. um botão "Dar baixa nas recebidas" percorre `GuiasRecebidasSemBaixaAsync` e chama a mesma
   `DarBaixaAsync` de sempre, com o operador logado: o número é o da operadora ou, se ela não
   devolveu, o número do prestador, com o protocolo na observação;
3. como a leitura só traz o que **ainda não tem baixa**, clicar duas vezes não baixa duas vezes,
   e uma baixa que falhou no meio não se perde: a guia continua na lista;
4. glosada, pendente e falha ficam fora, à vista, com o motivo — a baixa delas pede gente.

### A credencial

- Guardada por convênio na configuração do banco: o login em claro e o **MD5 da senha** cifrado
  (AES-GCM, `ProtecaoSegredoGlobal`). A senha em claro não é guardada em lugar nenhum.
- A chave que abre (`CLINICA_CREDENCIAIS_CHAVE`) fica no arquivo de ambiente do serviço, fora do banco.
- Quem cadastra não lê de volta: a tela recebe só "login X, tem senha: sim/não".
- A senha não vai para log, exceção, auditoria nem XML guardado — há um teste para cada um.

⚠️ **Limite honesto:** a chave de cifra é a mesma que protege as outras credenciais da clínica
na mesma tabela (armazenamento, SafeID). O usuário do banco do conector lê a tabela de
configuração inteira, então **um conector comprometido alcançaria as outras credenciais cifradas**.
Separar isso pede uma chave própria para o TISS ou uma tabela própria — fica registrado como
melhoria, não está feito.

## O que entra na integração (para quem liga as telas)

Assinaturas públicas:

```csharp
// FilaTissService — a tela pede e acompanha
Task<TransacaoTiss>  EnfileirarElegibilidadeAsync(string convenioCodigo, string numeroCarteira, DateOnly? validadeCarteira, int? pacienteId, string operador, CancellationToken ct = default)
Task<ConferenciaDeEnvioTiss> ConferirEnvioAsync(string convenioCodigo, IReadOnlyList<int> codigoIds, CancellationToken ct = default)   // não grava: a recusa por guia, para a tela
Task<TransacaoTiss>  EnfileirarEnvioAsync(string convenioCodigo, IReadOnlyList<int> codigoIds, string operador, CancellationToken ct = default) // 1 a 100; lança EnvioTissRecusadoException (.Conferencia.PorGuia)
Task<TransacaoTiss>  EnfileirarEnvioGuiaAsync(string convenioCodigo, int codigoId, string operador, CancellationToken ct = default)             // o lote com um item
Task<TransacaoTiss>  EnfileirarStatusDoEnvioAsync(int transacaoDeEnvioId, string operador, CancellationToken ct = default)                      // a resposta atualiza os itens do envio
Task<IReadOnlyList<GuiaTissRecebidaSemBaixa>> GuiasRecebidasSemBaixaAsync(int transacaoId, CancellationToken ct = default)                      // (ItemId, CodigoId, NumeroGuiaPrestador, NumeroGuiaOperadora?, Protocolo?)
Task<TransacaoTiss>  EnfileirarStatusProtocoloAsync(string convenioCodigo, string protocolo, int? codigoId, string operador, CancellationToken ct = default)
Task<TransacaoTiss>  EnfileirarCancelamentoGuiaAsync(string convenioCodigo, int codigoId, string numeroGuiaPrestador, string? protocolo, string operador, CancellationToken ct = default)
Task<bool>           CancelarAsync(int id, string operador, CancellationToken ct = default)          // só enquanto Pendente
Task<TransacaoTiss?> ObterAsync(int id, CancellationToken ct = default)                              // sem os XML, COM as guias do lote (.Guias)
Task<IReadOnlyList<TransacaoTiss>> DaGuiaAsync(int codigoId, CancellationToken ct = default)
Task<IReadOnlyList<TransacaoTiss>> RecentesAsync(string? convenioCodigo = null, int limite = 50, CancellationToken ct = default)

// FilaTissService — o conector executa
Task<TransacaoTiss?> ReservarProximaAsync(string consumidor, CancellationToken ct = default)
Task                 MarcarEnvioIniciadoAsync(int id, string? xmlEnviado, CancellationToken ct = default)
Task                 ConcluirAsync(int id, ConclusaoTransacaoTiss conclusao, CancellationToken ct = default)   // conclusao.Guias = um ResultadoDaGuiaTiss por guia
Task<EstadoTransacaoTiss> FalharAsync(int id, string erro, bool podeTentarDeNovo, string? xmlRecebido = null, CancellationToken ct = default)
Task                 RecusarAsync(int id, string motivo, CancellationToken ct = default)

// CredenciaisTissService
Task SalvarAsync(string convenioCodigo, string login, string senha, string operador, CancellationToken ct = default)
Task RemoverAsync(string convenioCodigo, string operador, CancellationToken ct = default)
Task<CredencialTissNaTela> ParaATelaAsync(string convenioCodigo, CancellationToken ct = default)      // (Login, TemSenha)
Task<CredencialTiss?> AbrirParaEnvioAsync(string convenioCodigo, CancellationToken ct = default)      // só no servidor
Task<bool> EnvioLigadoAsync(string convenioCodigo, CancellationToken ct = default)
Task DefinirEnvioAsync(string convenioCodigo, bool ligado, string operador, CancellationToken ct = default)
Task<Uri?> EnderecoAsync(string convenioCodigo, TipoTransacaoTiss tipo, CancellationToken ct = default)
Task DefinirEnderecoAsync(string convenioCodigo, TipoTransacaoTiss tipo, string endereco, string operador, CancellationToken ct = default)

// IClinicaRepositorio
Task AdicionarTransacaoTissAsync(TransacaoTiss transacao, CancellationToken ct = default)
Task<TransacaoTiss?> ObterTransacaoTissAsync(int id, CancellationToken ct = default)
Task<IReadOnlyList<TransacaoTiss>> TransacoesTissReservaveisAsync(DateTime agora, DateTime reservaVencidaAntes, int limite, CancellationToken ct = default)
Task<bool> ReservarTransacaoTissAsync(int id, int versaoVista, string consumidor, DateTime agora, CancellationToken ct = default)
Task RecarregarTransacaoTissAsync(TransacaoTiss transacao, CancellationToken ct = default)
Task<IReadOnlyList<TransacaoTiss>> TransacoesTissDaGuiaAsync(int codigoId, CancellationToken ct = default)
Task<IReadOnlyList<CodigoFaturamento>> CodigosParaEnvioTissAsync(IReadOnlyCollection<int> codigoIds, CancellationToken ct = default)
Task<IReadOnlyList<TransacaoTissGuia>> ItensDeEnvioTissDasGuiasAsync(IReadOnlyCollection<int> codigoIds, CancellationToken ct = default)
Task<IReadOnlyList<GuiaTissRecebidaSemBaixa>> GuiasTissRecebidasSemBaixaAsync(int transacaoId, CancellationToken ct = default)
Task<IReadOnlyList<TransacaoTiss>> TransacoesTissRecentesAsync(string? convenioCodigo, int limite, CancellationToken ct = default)
```

Pendências de quem integra:

1. **A migration.** Esta branch não tem migration, de propósito (uma só, na integração). Enquanto
   ela não existir, `MontagemDoSistemaTests` reprova por "modelo com mudança pendente": é esperado.
   As tabelas são `TransacoesTiss` e `TransacoesTissGuias`; o mapeamento está em `ClinicaDbContext`.
2. **As telas.** Nenhuma tela foi tocada; a tabela acima diz o que cada uma chama.
3. **O papel de banco** (`papel-tiss-conector.sql`) agora dá leitura de `Codigos` e `GuiasTissConsulta`
   (o lote precisa) e de `TransacoesTissGuias`; continua sem a ficha do paciente.
4. **Permissão de tela.** `FilaTissService` exige o operador, mas não confere permissão: quem
   decide quem pode pedir envio é a tela (as duas barreiras), como nas outras.

### Como ligar as telas (o motor está pronto; as telas, não)

> **Feito em 11/11/2026:** as telas existem. O que cada uma faz, onde fica no menu e as opções
> comparadas estão em [`tiss-telas-do-envio.md`](tiss-telas-do-envio.md). A tabela abaixo continua
> valendo como o contrato entre a tela e o motor.

O conector **não** chama `DarBaixaAsync`: baixa é ato de quem está logado, e o conector não tem
sessão. O que cada tela chama:

| Tela | Chamada | O que mostra |
|---|---|---|
| Formulário da guia ANS, botão "Conferir e guardar" | `FaturamentoService.ConferirGuiaAnsAsync(codigoId, dados, operador)` | A recusa de validação, campo a campo, como na baixa |
| "Montar envio" | `FilaTissService.ProntasParaEnvioAsync(convenio)` e, a cada marcação, `ConferirEnvioAsync` | A lista e a frase de recusa na linha |
| "Enviar à operadora" | `FilaTissService.EnfileirarEnvioAsync(convenio, ids, operador)` | `EnvioTissRecusadoException.Conferencia.PorGuia` se algo mudou |
| Painel de pendências, selo da linha | `FilaTissService.SelosDeEnvioAsync(ids)` | `SeloDeEnvioTiss.Texto` (a frase já vem pronta) |
| "Envios" | `FilaTissService.EnviosRecentesAsync(convenio)` / `ObterAsync(id)` | O lote e as guias, com situação e motivo |
| "Dar baixa nas recebidas" | `BaixaDasGuiasEnviadasService.BaixarRecebidasAsync(transacaoId, data, operador)` | `BaixaDasGuiasEnviadas.Resumo` e as falhas por linha |
| "Consultar protocolo" | `FilaTissService.EnfileirarStatusDoEnvioAsync(transacaoId, operador)` | O selo muda sozinho |
| "Cancelar na operadora" | `FilaTissService.EnfileirarCancelamentoGuiaAsync(convenio, codigoId, numeroGuiaPrestador, protocolo, operador)` | Selo "Cancelada" quando o recibo confirmar |

Assinaturas novas desta ligação:

```csharp
// FaturamentoService
Task ConferirGuiaAnsAsync(int codigoId, DadosGuiaConsultaAns guia, string? operador, CancellationToken ct = default)

// GuiaAnsNaBaixa (era privado em GuiaAnsNaBaixaService)
static DadosGuiaConsultaAns DaGuia(GuiaTissConsulta g)

// BaixaDasGuiasEnviadasService(IClinicaRepositorio, FilaTissService, FaturamentoService)
Task<BaixaDasGuiasEnviadas> BaixarRecebidasAsync(int transacaoId, DateOnly dataBaixa, string operador, CancellationToken ct = default)
static string NumeroDaBaixa(GuiaTissRecebidaSemBaixa guia)   static string Observacao(GuiaTissRecebidaSemBaixa guia)
// BaixaDasGuiasEnviadas { IReadOnlyList<BaixaDeGuiaEnviada> Guias; int Baixadas; Falhas; string Resumo }
// BaixaDeGuiaEnviada(int CodigoId, string NumeroUsado, string? Falha) { bool Baixada }

// FilaTissService — leituras para as telas
Task<IReadOnlyList<GuiaProntaParaEnvioTiss>> ProntasParaEnvioAsync(string convenioCodigo, int limite = 200, CancellationToken ct = default)
Task<IReadOnlyDictionary<int, SeloDeEnvioTiss>> SelosDeEnvioAsync(IReadOnlyCollection<int> codigoIds, CancellationToken ct = default)
Task<IReadOnlyList<TransacaoTiss>> EnviosRecentesAsync(string? convenioCodigo = null, int limite = 30, CancellationToken ct = default)
// GuiaProntaParaEnvioTiss(CodigoId, PacienteId, PacienteNome, DataAtendimento, Tipo, CodigoProcedimento, ValorProcedimento, ConferidaPor, ConferidaEm)
// SeloDeEnvioTiss(EstadoDoEnvioDaGuia Estado, string Texto, string? Protocolo, int? TransacaoId) + static Do(TransacaoTissGuia?)
// EstadoDoEnvioDaGuia { NaoEnviada, NaFila, Enviada, Recebida, Glosada, Falhou, Cancelada }

// CredenciaisTissService
static Uri? EnderecoPadrao(Convenio familia, TipoTransacaoTiss tipo)

// IClinicaRepositorio (+4)
void DescartarAlteracoesPendentes()
Task<IReadOnlyList<CodigoFaturamento>> GuiasConferidasParaEnvioTissAsync(IReadOnlyCollection<int> codigoIds, CancellationToken ct = default)
Task<IReadOnlyList<GuiaProntaParaEnvioTiss>> GuiasTissProntasParaEnvioAsync(string convenioCodigo, int limite, CancellationToken ct = default)
Task<IReadOnlyList<TransacaoTiss>> EnviosTissRecentesComGuiasAsync(string? convenioCodigo, int limite, CancellationToken ct = default)
```

Baixa automática, sem ninguém clicar, fica para depois de a clínica ver o envio funcionar algumas
semanas — e seria decisão da direção.

## Instalação na VPS (passo a passo, para quem opera)

Pré-requisitos: a VPS 1 com o Postgres da clínica (`docs/banco-na-vps.md`); os apps já na versão
que cria a tabela `TransacoesTiss`, abertos ao menos uma vez (é o app que aplica a migration).
Nada aqui abre porta no firewall.

**1. Publicar** — no computador de quem instala (Linux ou WSL, com o SDK 8), nunca na VPS:

```bash
deploy/tiss-conector/publicar.sh        # gera tiss-conector-XXXXXXX/ e o .tar.gz
sha256sum tiss-conector-XXXXXXX.tar.gz  # anote, para conferir depois de copiar
```

**2 a 4, de uma vez: `instalar.sh`** — na VPS 1, como root, de dentro da pasta `deploy/` do pacote
publicado (nov/2026; é o roteiro que funcionou na primeira instalação, com as três correções abaixo):

```bash
cd tiss-conector-XXXXXXX/deploy
sudo bash instalar.sh Petrobras Amil      # os convênios cuja credencial você quer gravar agora
# ou, conferindo o pacote:  ESPERADO_SHA256=… sudo -E bash instalar.sh Petrobras Amil
```

Ele confere o pacote e o banco, instala em `/opt/clinica-tiss-conector`, cria os dois usuários do
banco com senha aleatória, escreve `conector.env` e `admin.env`, acrescenta as duas regras no
`pg_hba.conf` (com cópia de segurança), sobe o serviço e pede o login e a senha de cada convênio
listado. Pode rodar de novo: o que já está feito, ele mantém. **O envio de guias continua desligado.**

As três correções (nov/2026, achadas na primeira instalação):

1. **Aspas na conexão.** `ConnectionStrings__Clinica="Host=…;Port=…;…"`. Sem aspas, o `;` cortava a
   linha quando o `comando.sh` carregava o arquivo. O `comando.sh` agora lê `CHAVE=VALOR` linha a
   linha, sem passar pelo shell, com ou sem aspas — como o systemd lê o `EnvironmentFile`.
2. **Um usuário do banco para quem opera: `tiss_operacao`** (`papel-tiss-operacao.sql`). O `admin.env`
   usava a senha do usuário dos apps, que alcança o banco inteiro; o novo só lê e grava
   `Configuracoes` e acrescenta na `Auditoria`. Regra no `pg_hba.conf`:
   `local clinica tiss_operacao scram-sha-256`.
3. **O SQL entra pela entrada padrão**, não por `-f`: o usuário `postgres` não lê a pasta do conector.

Os passos, se for fazer à mão:

```bash
# o usuário do serviço e o de quem opera (como root, na pasta deploy/ do pacote)
sudo -u postgres psql -p 45432 -d clinica -v ON_ERROR_STOP=1 -v senha="'SENHA_FORTE_1'" < papel-tiss-conector.sql
sudo -u postgres psql -p 45432 -d clinica -v ON_ERROR_STOP=1 -v senha="'SENHA_FORTE_2'" < papel-tiss-operacao.sql
```

No `pg_hba.conf`, **antes** da primeira regra ativa: `local clinica tiss_conector scram-sha-256` e
`local clinica tiss_operacao scram-sha-256`. Depois, `systemctl reload postgresql`. Os dois entram
só pelo socket local, nunca por TCP.

```bash
useradd --system --home-dir /nonexistent --shell /usr/sbin/nologin --user-group clinica-tiss
mkdir -p /opt/clinica-tiss-conector/releases /etc/clinica-tiss-conector
chmod 700 /etc/clinica-tiss-conector
tar -xzf tiss-conector-XXXXXXX.tar.gz -C /opt/clinica-tiss-conector/releases
ln -sfn /opt/clinica-tiss-conector/releases/tiss-conector-XXXXXXX /opt/clinica-tiss-conector/current
cd /opt/clinica-tiss-conector/current/deploy
install -m 600 conector.env.example /etc/clinica-tiss-conector/conector.env
install -m 600 admin.env.example    /etc/clinica-tiss-conector/admin.env
```

Edite os dois arquivos em `/etc/clinica-tiss-conector/` (a conexão **entre aspas**):

- `conector.env`: a senha do `tiss_conector` e a `CLINICA_CREDENCIAIS_CHAVE`. Se os apps e a ponte
  do CRM já usam essa chave, é **a mesma**. Se a clínica ainda não a tem, gere com
  `openssl rand -base64 32` e **guarde uma cópia fora do servidor**. Deixe `TISS_ENVIO_HABILITADO=false`.
- `admin.env`: a senha do `tiss_operacao`. O serviço não lê este arquivo.

**Cadastrar a credencial da operadora** — a senha é digitada na hora e não fica em arquivo nem no
histórico do terminal:

```bash
export TISS_WS_LOGIN='LOGIN_DO_PORTAL'
/opt/clinica-tiss-conector/current/deploy/comando.sh credencial Petrobras --senha-na-entrada --operador 'Seu Nome'
# (digite a senha e Enter; ela não aparece no retorno)
unset TISS_WS_LOGIN
```

`Petrobras` é o código do convênio no cadastro (`Amil` para a Amil); `--operador` é quem está
fazendo, e vai para a trilha.

**5. Ligar o serviço:**

```bash
cp /opt/clinica-tiss-conector/current/deploy/clinica-tiss-conector.service /etc/systemd/system/
systemctl daemon-reload && systemctl enable --now clinica-tiss-conector
journalctl -u clinica-tiss-conector -n 20
```

O que deve aparecer: `Conector TISS no ar. Envio de guias neste servidor: desligado. Intervalo: 5s. Entre idas à mesma operadora: 3s. Teto diário de elegibilidade por convênio: 300.`

**6. Conferir:**

```bash
/opt/clinica-tiss-conector/current/deploy/comando.sh situacao Petrobras
# Petrobras: login cadastrado, senha cadastrada, envio desligado, hoje 0 de 300 consultas.
#   operadora Saúde Petrobras, registro ANS 422631 no catálogo; código do prestador: o CNPJ da clínica;
#   elegibilidade em https://webservices.crc.com.br/tiss/ws/40100/tissVerificaElegibilidade (padrão do catálogo).
```

Depois, peça uma elegibilidade por uma tela (quando houver) e veja a linha passar de `Pendente`
a `Concluida` no mesmo comando.

## As operadoras do conector (nov/2026): Saúde Petrobras e Amil

> **Amil segurada (18/11/2026):** a Amil desativou o token e só aceita QR Code (só no portal); até a resposta dela sobre o QR pelo web service, a Amil sobe **só com a elegibilidade** — envio de guias (`EnvioDeGuiaNasTelas: false` + `envio Amil`) e autorização (`autorizacao Amil`) ficam desligados, e a baixa e a aba Dia dela são as de antes do #363 (`AmilSeguradaTests`).
> Antes de ligar `autorizacao Amil`, atualize o conector: o 1f79d2bd não conhece os tipos `Autorizacao`/`CancelaAutorizacao` e para a fila inteira se encontrar um deles (provado contra o banco migrado).

Quem decide "esta família de convênio tem web service" é **um catálogo só**, em
`src/Clinica.Application/Servicos/OperadorasTiss.cs`. Operadora nova é uma linha nele; nenhum
outro arquivo do conector, da fila ou das telas conhece nome de operadora.

| | Saúde Petrobras | Amil |
|---|---|---|
| Registro ANS (público) | 422631 | 326305 |
| `codigoPrestadorNaOperadora` | CNPJ da clínica | **login do web service** (o login do portal é o código do credenciado) |
| Endereços padrão | `https://webservices.crc.com.br/tiss/ws/40100/…` | APIM: `https://api.servicos.grupoamil.com.br/api-tiss-…/v4.01.00` |
| Padrão | TISS 4.01.00, SOAP, login e senha (MD5) no cabeçalho | o mesmo |
| Versão do padrão | 4.01.00 em tudo | **4.03.00** no lote, no status do protocolo e no demonstrativo (`api-tiss-…/v4.03.00`); elegibilidade e cancelamento em 4.01.00 |
| Codificação da mensagem na rede | ISO-8859-1 (a do padrão) | **UTF-8** (o APIM é WCF/basicHttpBinding: respondeu HTTP 415 a `charset=ISO-8859-1`) |
| Elegibilidade | sim | **sim** |
| Guia em que a consulta vai | guia de consulta (inclusive o BSV) | **guia SP/SADT**, com senha de autorização e token (o Manual do Credenciado manda cobrar consulta "exclusivamente em guias de SP/SADT") |
| Envio de guias nas telas | sim (a guia passa pela conferência ANS na baixa) | **pronto e desligado** (nov/2026): conferência SP/SADT, lote e arquivo prontos e testados; `EnvioDeGuiaNasTelas` falso deixa a Amil fora das telas de envio e da conferência na baixa — ver "Amil: o envio de guias" |
| Prazos | — | envio do 1º ao 5º dia útil, das 7h às 19h — **regra da fila** (o lote fora dela fica agendado; a Amil recusa com 3091 até na homologação); cobrança em até 90 dias do atendimento — aviso, vermelho quando a guia vence antes da próxima janela |

Fonte da Amil: Portal do Credenciado Amil → Material de apoio → Manuais ("Manual de Utilização de
Web Service para prestadores" e a tabela "Endereços web service Amil 4.01.00", com o L7 atual e o
APIM novo). Os endereços L7 (`https://api.amil.com.br/operadora/tiss/<serviço>/soap/v40100`) e o
"link único" que o manual manda pedir ao Núcleo TISS ficam de fora do código: se a Amil indicar
um deles, ele é **cadastrado por cima do padrão**, sem novo deploy:

```bash
comando.sh endereco Amil todos https://ENDERECO_QUE_A_AMIL_INDICOU --operador 'Seu Nome'
```

**O que vence o quê**, por convênio:

| Dado | 1º (cadastrado) | 2º (catálogo) | 3º |
|---|---|---|---|
| Endereço de cada serviço | `comando.sh endereco` | `OperadorasTiss` | — (sem endereço, nada sai) |
| Registro ANS | cadastro do convênio (Configurações do faturamento) | `OperadorasTiss` | o da clínica (dados do prestador) |
| Código do prestador | `comando.sh codigo CONVENIO cnpj\|login\|CODIGO` | regra da operadora no catálogo | CNPJ da clínica |

O registro do catálogo vem **antes** do da clínica porque o da clínica é um só: com duas operadoras,
ele mandaria a mensagem da Amil com o registro da Petrobras.

**O código do prestador** (`codigoPrestadorNaOperadora`, até 14 caracteres no XSD 4.01.00) vai no
cabeçalho e em `dadosPrestador`. O XSD não diz o que ele é — diz só o tamanho. Na Amil, o padrão é
o login do portal; se o login tiver mais de 14 caracteres, o conector recusa antes de chamar a
operadora e manda cadastrar o código (`comando.sh codigo Amil CODIGO --operador 'Seu Nome'`).
`comando.sh codigo Amil padrao` volta à regra do catálogo. Nenhum código, CNPJ ou login real está no
repositório: tudo entra pelo servidor, na instalação.

**Na tela**: a consulta de elegibilidade aparece na ficha do paciente (aba "Convênio, contatos e
LGPD") para paciente de convênio da família Amil **quando a credencial da Amil estiver gravada**.
O formulário "Web service" do convênio, em Configurações do faturamento, passa a abrir na linha da
Amil — só com a credencial e um aviso de que, por enquanto, ela serve para a elegibilidade.

**Na agenda** (nov/2026): a aba "Dia" da Agenda (Recepção → Agenda → Dia, tela `AgendaDoDia`) tem o botão discreto
"Consultar elegibilidade", que só aparece quando há convênio com consulta automática (família do
catálogo + credencial gravada). Ele abre um painel com os pacientes do dia mostrado na aba (convênio
e carteirinha da ficha; quem não dá para consultar fica recolhido, com o motivo) e uma aba "Avulso"
(carteirinha digitada, ou CPF — que só acha a ficha: o padrão 4.01.00 não tem CPF na
`verificaElegibilidade`). Quem pede a consulta é o mesmo componente da ficha, sobre a mesma fila,
com o intervalo, o teto diário e o resultado do dia. A lista e as frases: `ElegibilidadeNaAgendaService`.

### Amil: TISS 4.03.00 no lote (nov/2026, resposta do Núcleo TISS)

A Amil pede o padrão **4.03.00**. O lote só existe em `api-tiss-lote-guias/v4.03.00` (o WSDL abre, com
namespace `…/tissloteguias/v40300`; em `/v4.01.00` dá 404). A versão é dado do catálogo, por tipo de
transação (`OperadoraTiss.Versoes`, `VersaoTiss(tipo)`), e chega ao `Padrao` do cabeçalho pelo
remetente; versão que o sistema não conhece é recusada antes da rede.

| Transação da Amil | Versão | Endereço |
|---|---|---|
| Lote de guias | 4.03.00 | `api-tiss-lote-guias/v4.03.00` |
| Status do protocolo | 4.03.00 | `api-tiss-solicitacao-status-protocolo/v4.03.00` |
| Demonstrativo de retorno | 4.03.00 | `api-tiss-solicitacao-demonstrativo-retorno/v4.03.00` (só o endereço: a transação ainda não existe no sistema) |
| Elegibilidade | 4.01.00 | `api-tiss-verifica-elegibilidade/v4.01.00` — funcionou em produção assim; não mexemos |
| Cancelamento de guia | 4.01.00 | `api-tiss-cancela-guia/v4.01.00` (fora do escopo desta rodada) |

**O que muda da 4.01.00 para a 4.03.00 na guia SP/SADT** (XSD oficiais, Componente de Comunicação 202511,
comparados tipo a tipo): a estrutura de `ctm_sp-sadtGuia`, `ct_autorizacaoSADT` (senha, `codValidacao`,
`ausenciaCodValidacao`), `ctm_sp-sadtAtendimento` e `ct_guiaValorTotal` é a MESMA, sem campo novo nem
obrigatório novo; `valorUnitario` e `valorTotal` do procedimento executado passam de `st_decimal8-2`
para `st_decimal10-2` (o sistema continua limitando a 999.999,99, que cabe nos dois); o CBO 223575 entra
no domínio; e o `Padrao` aceita 4.02.00 e 4.03.00. A mensagem é validada contra os XSD 4.03.00 em
`tests/Clinica.Tests/Tiss/xsd-4.03.00` (a mesma mensagem é recusada pelo XSD 4.01.00 — há teste).

**Até 100 guias por lote** (o máximo do XSD, que a Amil confirmou): acima disso o sistema DIVIDE sozinho
(`FilaTissService.EnfileirarEnvioEmLotesAsync`, um pedido por lote de até 100, na ordem marcada). Todas as
partes são conferidas antes de qualquer uma entrar na fila: uma guia recusada recusa o envio inteiro.

### Amil: o envio de guias (pronto, desligado — nov/2026)

**O que a Amil exige**, lido nos manuais dela (Shared/Convenios/amil: Manual do Credenciado jul/2026,
Manual de Faturamento Eletrônico v3.1, FAQ do faturamento eletrônico, Lista de glosas da prévia):

- **A consulta vai em guia SP/SADT** ("as consultas realizadas devem ser informadas e cobradas
  exclusivamente em guias de SP/SADT"). O faturamento do portal só tem SP/SADT, honorário individual e
  resumo de internação. A acupuntura também vai em SP/SADT, como sessão.
- **Senha de autorização em toda guia**: sem ela a prévia bloqueia (1403/894 "Senha/Token não
  informado", 1403/862 "Senha inexistente"). Senha eletiva vale 60 dias.
- **Atendimento confirmado pelo beneficiário** (token de 6 dígitos, QR Code ou reconhecimento facial),
  no portal, no dia do atendimento; a data da confirmação deve ser a do atendimento. Sem confirmação:
  bloqueio 3096/893. Sem token, só com justificativa e com a guia assinada + documento com foto
  anexados no portal (bloqueio 991 se faltar). Mais de 5% de atendimentos sem token abre auditoria.
  "O prestador deve incluir nos arquivos os respectivos códigos de autorização."
- **Prazo**: guia em até 90 dias do atendimento (bloqueio 1701/405 "data de atendimento superior ao
  permitido"); envio do 1º ao 5º dia útil, das 7h às 19h (visto no portal).
- **O protocolo precisa ser FINALIZADO no portal** ("Finalizar faturamento"), uma vez por referência —
  inclusive o que chega por web service ou importação de XML (FAQ 40: todos aparecem em Protocolos,
  com prévia de glosas, e podem ser cancelados e reenviados).

**Onde vai o token no XML.** O XSD 4.01.00 tem o lugar: `dadosAutorizacao/codValidacao` (st_texto10) na
guia SP/SADT, ao lado da `senha`, da `dataAutorizacao` e da `dataValidadeSenha`; sem token, o motivo vai em
`dadosAutorizacao/ausenciaCodValidacao` (`dm_ausenciaCodValidacao`, 01 a 07). O QR Code e o reconhecimento
facial não têm campo no XML: eles confirmam no portal, e a guia leva a senha.

**O que o sistema faz.** Na baixa, a guia da Amil é conferida como SP/SADT (`GuiaAnsNaBaixa`, o MESMO
lugar da Petrobras; o modelo vem do catálogo, `OperadoraTiss.ModeloDaGuia`): senha, data da
autorização, validade, token ou motivo, tipo de atendimento (04 consulta, 03 outras terapias), caráter
e descrição do procedimento. Cada glosa de bloqueio que dá para pegar antes vira a frase do campo
(1403, 1406, 1814, 3096, prazo); as de alerta (1405, 991) viram aviso. O lote sai pelo
`TissLoteGuiasWs` de sempre, em UTF-8, para o endereço APIM do catálogo, e nunca repete um lote enviado
(a mesma trava da Petrobras). O **arquivo de lote** (Lotes e XML TISS) já leva a guia SP/SADT
conferida: é o arquivo para subir no SisAmil (TISS → Importação de arquivos XML), o caminho que
funciona hoje sem web service. A janela de envio é regra da fila (abaixo, "A janela de envio é regra da
fila"); o prazo de 90 dias é aviso na baixa e no painel de envio (`PrazosTiss`).

**Para ligar** (depois da resposta do Núcleo TISS): `EnvioDeGuiaNasTelas: true` em
`OperadorasTiss.Amil` (uma linha, um PR), publicar, e no servidor `comando.sh envio Amil ligar`. Até lá a
baixa de quem fatura a Amil não muda.

**Amil: o que falta saber** (perguntado ao Núcleo TISS, suportexml@amil.com.br) **e o que muda conforme a resposta:**

| # | Pergunta | Se a resposta for… | O que muda |
|---|---|---|---|
| 1 | Qual é o "link único" do web service da clínica? | um endereço | `comando.sh endereco Amil todos <link> --operador …`; sem deploy |
| 2 | As guias confirmadas por token, que o portal já digita sozinho ("Digitadas Automaticamente"), também vão no lote? | **não** (o portal já as tem) | o envio pelo sistema fica só para as que a clínica digita; a conferência continua valendo para o arquivo/registro |
|   |   | **sim** | nada: o lote leva todas |
| 3 | O protocolo enviado pelo web service precisa ser finalizado no portal? | **sim** (é o que o manual diz) | a tela de envio ganha o lembrete "finalize no portal"; o sistema não finaliza por web service |
| 4 | A janela é mesmo do 1º ao 5º dia útil, das 7h às 19h? Feriado conta? | **respondido pela homologação**: vale também para o web service (3091) | a janela virou regra da fila; feriados ainda em aberto: o sistema conta só seg–sex |
| 5 | Existe ambiente de homologação? | **sim** | cadastrar o endereço de homologação com `comando.sh endereco` e testar antes de ligar; **não**: o primeiro lote real é de UMA guia, acompanhado no portal |

**Respondido (17/11/2026)** — nos manuais do portal (Shared/Convenios/amil/README.md, "Respostas achadas
nos manuais") e por telefone, pelo dono, com a Amil:

| Pergunta | Resposta | O que o sistema faz |
|---|---|---|
| O `codigoPrestadorNaOperadora` é o login do portal? | **Sim**: a tela de login pede o "código do Credenciado", e o login é esse código | manda o login (regra do catálogo `LoginDoWebService`) |
| Como a Amil identifica o profissional executante? | pelo **CPF**, com conselho, número e UF (cadastrados em "Meus Dados" no portal); **não existe** código de profissional | a equipe (`equipeSadt`) vai com o CPF; sem CPF a guia não passa |
| Como informar a confirmação? | **Pelo web service, só pelo token**, em `codValidacao` (confirmado pela Amil por telefone em 17/11/2026). QR Code e reconhecimento facial são só do portal, e o QR não gera código para digitar | "Como o paciente confirmou": token ou não confirmou |
| Feriado conta na janela? | **Não**: "para os dias não úteis será acrescentado o próximo dia útil" | dia útil sem os feriados nacionais (`FeriadosNacionais`) |

**Decisão do dono (17/11/2026):** a Amil é faturada **só pelo sistema**, com o token na guia; a recepção
**não confirma no portal**, porque a confirmação lá cria sozinha a guia no faturamento do portal (seria a
mesma cobrança duas vezes).

**A única pergunta aberta é prática:** o token informado na guia pelo web service, **sem a confirmação no
portal**, é aceito como confirmação, ou vem a glosa 3096 ("atendimento não confirmado pelo beneficiário")?
Isso se prova no primeiro lote real (ou com o Núcleo TISS).

**A codificação (nov/2026).** A primeira elegibilidade real da Amil voltou **HTTP 415** com corpo vazio:
o WSDL dela é SOAP 1.1 (`soapAction=""`, document) servido por WCF, e o `basicHttpBinding` só aceita
`text/xml; charset=utf-8`. A codificação agora é dado do catálogo (`OperadoraTiss.Codificacao`): a
declaração `<?xml … encoding=…?>`, os bytes e o `Content-Type` saem juntos dela
(`TissWebserviceCliente.Codificar`); a Saúde Petrobras sai byte a byte como antes. O **hash** do
epílogo não muda: ele é do TEXTO das tags em ISO-8859-1 (Componente de Comunicação), não dos bytes —
na elegibilidade, que só leva dígitos e códigos, o MD5 é o mesmo em qualquer codificação. Se a Amil
recusar o hash de uma mensagem com acento (guia), a pergunta a fazer a ela é se o hash é sobre UTF-8.

**HTTP de erro sem mensagem SOAP.** 4xx (415, 404, 403, 401) sem envelope SOAP vira "não foi possível"
**sem nova tentativa** (`FilaTiss.RecusaDefinitivaDoHttp`; 408 e 429 continuam passageiros): repetir
não muda a resposta e cada ida gasta a cota do dia. O motivo na linha e no log leva o status e até 300
caracteres do corpo, sem a carteira e sem segredo. 5xx e falta de resposta continuam tentando de novo.

**Tirar uma transação Pendente da fila:** `comando.sh cancelar ID --operador 'Seu Nome'` (o ID é a
primeira coluna do `comando.sh situacao`). É a mesma regra do "Cancelar" das telas: só sai o que ainda
não foi pego pelo conector; o que já está em envio ou terminou não muda (código de saída 1).

### Resultado do primeiro teste na homologação da Amil (nov/2026, conector 3811074b)

- `ambiente Amil homologacao` parou em `42501: permission denied for table Convenios`: o comando usa o
  `tiss_operacao`, que lê o cadastro de convênios (a família da operadora). Corrigido em
  `papel-tiss-operacao.sql` (`GRANT SELECT ON public."Convenios"`, depois do REVOKE do início). Os dois
  papéis agora são MEDIDOS: `PapeisDoConectorTissTests` roda o serviço e cada comando sobre um banco que
  anota o SQL e compara com os GRANTs, nos dois sentidos (nada usado sem permissão, nada concedido sem uso).
  Na VPS: rode de novo os dois `papel-tiss-*.sql` (é seguro) ou mantenha o GRANT manual, que é o mesmo.
- `testar-elegibilidade`: **HTTP 200 · Elegível**. O ponto único de homologação funciona com o
  SOAPAction `""` (o padrão do WSDL): o serviço é decidido pela mensagem.
- `testar-lote --exemplo`: **HTTP 200 · recusa 3091** ("COBRANÇA FORA DO PRAZO ESTIPULADO NO CONTRATO -
  ENVIO DO FATURAMENTO SÓ PODE SER REALIZADO NOS DIAS 01; 02; 03; 04; 05"). O XML 4.03.00, o login, o
  endereço e o SOAPAction foram aceitos; a recusa é de regra. **A janela vale para o web service, até na
  homologação.**
- `ambiente Amil producao` voltou certo.

### A janela de envio é regra da fila (nov/2026)

A janela é dado do catálogo (`OperadoraTiss.JanelaDeEnvio`; a Saúde Petrobras não tem):

- **Lote pedido fora da janela** entra na fila **agendado** para a próxima abertura (1º dia útil, 7h,
  horário de Brasília — `HoraDaClinica`): `ProximaTentativaEm` e a frase "Agendado para DD/MM/AAAA às
  07:00 …". O painel de envio mostra "Agendado" com a data; a baixa mostra "Agendado para a janela de
  envio" (e não "o serviço não respondeu"). Pode ser **cancelado** até lá.
- **O conector reconfere** antes de enviar: lote que ele pegue fora da janela volta agendado, sem gastar
  tentativa e sem sair nada.
- **Recusa 3091** (`FilaTiss.GlosaForaDaJanela`) que chegar mesmo assim volta o lote a **agendado, sem
  gastar tentativa**, e limpa o carimbo de envio (a operadora disse que não recebeu: pode sair de novo).
  O agendamento é para o **próximo dia útil da mesma janela** (uma ida por dia, sem laço) e só vai para o
  mês seguinte quando a janela acaba — um feriado que a operadora não conta custa um dia, não o mês (e
  os 90 dias). Para operadora sem janela, 3091 é recusa comum.
- **Feriados:** feriado não é dia útil da janela (o portal: "para os dias não úteis será acrescentado o
  próximo dia útil"). O sistema não tinha calendário; agora tem a lista fixa dos feriados NACIONAIS
  (`Clinica.Domain.Regras.FeriadosNacionais`: 1/1, 21/4, 1/5, 7/9, 12/10, 2/11, 15/11, 20/11, 25/12 e a
  Paixão de Cristo, calculada pela Páscoa), usada em `PrazosTiss.DiaUtilDoMes`. Ficam de fora os
  feriados estaduais e municipais e os pontos facultativos (Carnaval, Corpus Christi): se a Amil os contar,
  o agendamento pode cair num deles, ela recusa com 3091 e o lote volta agendado para o próximo dia útil.
- **Prazo de 90 dias:** a guia que vence ANTES da próxima janela (ou já venceu) tem aviso **vermelho**
  na linha do painel de envio e na baixa (`PrazosTiss.AvisoDaGuia`, `ConferenciaDeEnvioTiss.PrazoGrave`,
  `PropostaGuiaAns.AlertaDePrazo`). Continua aviso, nunca bloqueio.
- **`testar-lote` fora da janela** avisa antes de chamar ("só aceita lote do 1º ao 5º dia útil…; próxima
  janela: DD/MM às HH:mm") e não chama; `--mesmo-assim` chama (para ver a recusa 3091).

### Amil: a guia SP/SADT campo a campo (nov/2026, Shared/Convenios/amil/08)

Regras que vêm do catálogo (`OperadorasTiss.Amil`), nunca de `if Amil`:

- **Equipe** (`EquipeComProfissional`): a SP/SADT leva `equipeSadt` com o profissional executante pelo
  **CPF** (cadastro da equipe → Identificação), mais conselho, número, UF, CBO e grau "12 - Clínico". A
  Amil identifica o profissional pelo CPF; não existe código de profissional. Sem CPF, a conferência
  recusa: "Cadastre o CPF de {profissional}…" (1203/992).
- **Confirmação**: "Como o paciente confirmou: Token (6 dígitos) · Não confirmou". O token — os 6 dígitos
  que o paciente mostra no app Amil Clientes, só dígitos — vai em `codValidacao`. "Não confirmou" exige
  o motivo (`ausenciaCodValidacao`, só os de consultório com código no XSD 4.03.00: 07 sem celular, 03
  instabilidade de sistema, 04 não quis informar o token; o "outros" do portal não tem código no padrão)
  E a declaração "guia assinada e documento com foto anexados no portal hoje" (a 991 é bloqueio). QR Code
  e reconhecimento facial não existem no sistema: são só do portal.
- **Horas** (37/38): do consultório (início e fim do atendimento registrados no "Atender"/"Finalizar");
  sem eles, da agenda (horário marcado e o fim previsto pela duração). A proposta diz de onde vieram.
- **Número da guia na operadora** (7): vai quando o sistema o tem (`NumeroGuiaReal`).
- **Duplicidade** (`RecusaDuplicidade`, 1807/882): a mesma cobrança — carteira, procedimento e dia — no
  mesmo lote ou numa guia já enviada que ainda a prende não entra no lote.
- **90 dias** (`PrazoDeCobrancaBloqueia`, 1701/405): guia vencida não entra no lote, nem a que venceu
  esperando a janela (o conector reconfere antes de enviar); antes de vencer, aviso vermelho.
- A Saúde Petrobras não muda em nada disso.
- **Prévia**: a baixa da Amil só aparece com o envio dela ligado; para fotografá-la, a prévia registra
  `EnvioTissLigadoNaPrevia` (o app nunca: há teste) e tem a semente `SementeGuiaAmilNaBaixa`.

### Amil: homologação e os comandos de teste (nov/2026)

O Núcleo TISS respondeu: há **homologação** num endereço único,
`https://api-dev.servicos.grupoamil.com.br/api-tiss-servico-test` (o `?wsdl` dá 404, então o serviço é
decidido pela mensagem), com o login e a senha do portal e qualquer carteira válida de produção; o lote
leva **até 100 guias**; e a **confirmação de entrega continua no portal** (enviado não é entregue).

- `comando.sh ambiente Amil homologacao|producao --operador NOME` aponta TODOS os serviços da Amil para a
  homologação (por cima até do endereço cadastrado) ou de volta. Vai para a trilha
  (`AmbienteTissHomologacao`/`AmbienteTissProducao`), e o `situacao Amil` mostra o ambiente em destaque.
  **Enquanto a Amil estiver em homologação, a fila de produção recusa os pedidos das telas** (a
  consulta de elegibilidade da ficha e da agenda termina recusada, com a frase do ambiente): nada da
  tela vai à homologação. Volte para produção ao terminar.
- `testar-elegibilidade`, `testar-lote` e `testar-protocolo` (`TestesNaHomologacao`): só rodam com o
  convênio em homologação e conferem que o endereço resolvido é o dela; funcionam com o envio desligado;
  fazem UMA ida; mostram a resposta (protocolo, ou o HTTP com até 300 caracteres do corpo, sem a senha) e
  **não gravam nada nas guias nem na fila** — só uma linha `TesteTissHomologacao` na trilha, sem carteira.
  Rodam com o usuário do serviço (`conector.env`); o `ambiente` usa o `admin.env`.
- `testar-lote --guias ID[,ID…]` lê as guias Amil conferidas (SP/SADT) e as manda com um número próprio de
  teste (`H<guia>T<hora>`). Como o envio da Amil está desligado, a baixa ainda não confere guia Amil: o
  caminho de hoje é `--exemplo --carteira N`, uma guia SP/SADT fictícia em memória (contratado = a
  clínica; profissional "PROFISSIONAL DE TESTE", CRM 000000/RJ — a homologação pode recusá-lo, e saber
  isso é parte do teste), com `--senha`, `--token`, `--procedimento` (padrão 10101012) e `--valor`
  (padrão 1,00) opcionais. Sem `--token`, vai `ausenciaCodValidacao` 03.
- `--soap-action VALOR` muda o cabeçalho SOAPAction (padrão `""`, o do WSDL). Se o ponto único da
  homologação responder que não reconhece o serviço, é isto que se tenta.
- Depois de um envio REAL recebido, o painel de envio mostra "Enviado. Confirme a entrega no portal da
  Amil (protocolo N): enviado não é entregue." (`EnvioTissNaTela.LembreteDeEntrega`).

**Roteiro do primeiro teste na homologação** (na VPS 1, como root; `C=/opt/clinica-tiss-conector/current/deploy/comando.sh`):

0. O conector instalado tem de ser deste commit ou posterior (`publicar.sh` + `instalar.sh`, acima). Para
   `--guias`, o banco também precisa da migration `GuiaSpSadtDaAmil` (aplicada quando um app desta versão
   abre); para `--exemplo`, elegibilidade e protocolo, não.
1. Conferir: `sudo $C situacao Amil` → login e senha cadastrados, envio desligado, ambiente de produção.
2. Apontar para a homologação: `sudo $C ambiente Amil homologacao --operador 'Seu Nome'` e de novo
   `sudo $C situacao Amil` → a linha `*** Amil: AMBIENTE DE HOMOLOGAÇÃO … ***`.
3. Provar o ambiente: `sudo $C testar-elegibilidade Amil --carteira CARTEIRA_VALIDA --operador 'Seu Nome'`
   → "Elegível"/"Não elegível" é resposta da homologação; `LoginInvalido` = credencial; HTTP 404/405/415 ou
   "não reconhece o serviço" = tentar `--soap-action` (ver acima) ou perguntar ao Núcleo TISS.
4. Um lote de uma guia — **do 1º ao 5º dia útil, das 7h às 19h** (fora disso o comando avisa e não chama;
   `--mesmo-assim` chama e a Amil devolve 3091): `sudo $C testar-lote Amil --exemplo --carteira CARTEIRA_VALIDA --senha SENHA_DE_UMA_AUTORIZACAO --token TOKEN --operador 'Seu Nome'`
   (sem senha e token, a homologação deve devolver a glosa de senha/token — também é resultado).
   → `PROTOCOLO: N` = recebido; senão, a frase da operadora ou o HTTP com o trecho do corpo.
5. A situação do protocolo: `sudo $C testar-protocolo Amil --protocolo N --operador 'Seu Nome'`.
6. **Voltar para produção:** `sudo $C ambiente Amil producao --operador 'Seu Nome'` e `sudo $C situacao Amil`.
   O envio continua desligado; a elegibilidade das telas volta a funcionar.

### Amil: a autorização pelo sistema e o token na recepção (nov/2026)

**O que muda para a recepção.** A senha de autorização deixa de ser copiada do Portal do Credenciado e o
token deixa de ser digitado na baixa (que acontece dias depois, com o paciente já em casa):

1. **Chegou.** A recepção marca "Chegou" (aba Dia, Início ou o formulário do horário). Com a chave da Amil
   ligada, o sistema pede a autorização sozinho, pela fila do conector (`solicitacaoProcedimento` 4.03.00).
2. **Autorizado.** A senha, a data e a validade ficam guardadas no atendimento (`AutorizacoesTiss`), e a
   linha da agenda mostra o selo **"Autorizado · peça o token ao paciente"** com o campo do token.
3. **Token.** O paciente abre o app Amil Clientes e mostra os 6 dígitos; a recepção digita e guarda
   (fica quem digitou e quando). O selo vira **"Autorizado · token ok"**. Sem token: **"Sem token…"** e o
   motivo da tabela TISS (sem celular, instabilidade, não quis informar), ali mesmo → selo "Não confirmou".
   Enquanto faltar token de alguém autorizado hoje, a aba Dia mostra em amarelo
   **"Faltou o token de N pacientes Amil hoje"**.
4. **Baixa.** O pop-up abre com a senha, a data, a validade e o token **carregados e travados**, o selo
   "autorizado pela Amil em DD/MM", e o resto que o sistema já sabe (executante pelo CPF, conselho e CBO;
   TUSS e valor da tabela da Amil; datas e horas). A pessoa confere e confirma. Corrigir o token na baixa
   é exceção ("Corrigir"): grava, e a trilha diz que mudou (`GuiaAnsDiferenteDaAutorizacao`, sem o token).
   Sem autorização do sistema, a baixa avisa "Autorização feita fora do sistema" e os campos ficam livres.

**As respostas da Amil** (`ct_situacaoAutorizacao`, `statusSolicitacao`):

| Resposta | O que o sistema faz | O que a recepção vê |
|---|---|---|
| 1 autorizado / 7 parcial | guarda senha, data, validade (e o número na operadora) | "Autorizado · peça o token ao paciente" |
| 2 em análise / 4–5 aguardando | a MESMA transação volta à fila e pergunta a situação (`solicitacao-status-autorizacao`) a cada **30 min**, até **16 vezes** (8 h); passado o teto, para | "Autorização em análise"; depois do teto, o motivo manda conferir no portal |
| 3 negado, ou `mensagemErro` | guarda o motivo (código e descrição) | "Autorização negada" + motivo, e "Pedir de novo" |
| sem resposta, HTTP de erro, corpo que não é TISS | **não se sabe** — nunca "negado"; a mensagem que saiu não é repetida sozinha | "Autorização sem resposta — não é negativa. Confira antes de pedir de novo." |
| 6 cancelada | guarda como cancelada | "Pedir autorização" volta |

**Pedido não é cobrança, mas não duplica.** "Pedir autorização"/"Pedir de novo" (na coluna Status da
linha, mesmas permissões do Chegou) devolve a autorização que está na fila, em análise ou valendo: um
horário tem **uma** autorização valendo. Pedir de novo só depois de negada, cancelada, vencida ou sem
resposta. O número da guia de solicitação é `A<id>`.

**Desmarcado, falta ou cancelado.** Pedido ainda na fila: é cancelado e nada sai. Senha emitida (ou em
análise): o sistema pede o cancelamento pelo `cancela-guia` 4.03.00. **O XSD não tem cancelamento por
senha**: cancela-se a **guia de solicitação** (`tipoGuia` 1) pelo número dela no prestador e, quando veio,
na operadora. Substituído (a sessão aconteceu por fora) não cancela.

**Acupuntura em série.** Um pedido **por sessão**, no Chegou de cada uma: o manual (p. 22) pede validação
a cada novo atendimento, e o token nasce com cada autorização (p. 25). O XSD aceita `quantidadeSolicitada`
> 1, mas o manual não diz se uma senha para N sessões vale com um token por sessão. **Pergunta aberta ao
Núcleo TISS:** "uma solicitação com quantidade N para acupuntura em série é aceita, e cada sessão gera
token próprio?"

**A chave.** `comando.sh autorizacao Amil ligar|desligar --operador NOME` (admin.env), **desligada** por
padrão e separada do envio de guias (`envio`) e de `TISS_ENVIO_HABILITADO`. Desligada: a agenda não pede,
não mostra selo, campo nem botão, e o conector recusa o que estiver na fila. `situacao Amil` mostra
"autorização LIGADA/desligada". Endereços (catálogo, trocáveis por `endereco Amil autorizacao|status-autorizacao|cancelamento URL`):
`api-tiss-solicitacao-procedimento/v4.03.00`, `api-tiss-solicitacao-status-autorizacao/v4.03.00`,
`api-tiss-cancela-guia/v4.03.00` (o cancelamento de guia da Amil também passou a 4.03.00, Manual p. 53).

**Dados e LGPD.** O conector lê só a fotografia do pedido na autorização (carteira, procedimento,
profissional) — `GRANT SELECT, UPDATE ON "AutorizacoesTiss"` no `papel-tiss-conector.sql`, medido por
`PapeisDoConectorTissTests`; não lê agenda nem ficha. O **token** é dado de autenticação do beneficiário:
fica só na autorização e na guia, nunca em log nem na trilha, e fora da baixa aparece mascarado (`••••13`).
A senha também não vai para a frase da fila nem para a trilha.

**Testar na homologação** — `testar-autorizacao` (usuário do serviço, `conector.env`): só com a Amil em
homologação, UMA ida, nada gravado além da linha `TesteTissHomologacao` (sem carteira e sem senha).

```bash
C=/opt/clinica-tiss-conector/current/deploy/comando.sh
sudo $C situacao Amil                                   # login/senha cadastrados, autorização desligada
sudo $C ambiente Amil homologacao --operador 'Seu Nome'
sudo $C testar-autorizacao Amil --carteira CARTEIRA_VALIDA --procedimento 10101012 \
     --cpf-profissional CPF_DO_PROFISSIONAL --crm NUMERO --uf 33 --cbo 225125 --operador 'Seu Nome'
#   "Autorizado … Senha da homologação: X" = o caminho funciona; "Em análise" = idem, com análise;
#   "Negado: código - motivo" = resposta da Amil (ex.: carteira, CBO); HTTP 404/415 = --soap-action ou Núcleo TISS.
sudo $C ambiente Amil producao --operador 'Seu Nome'
```

O XSD 4.03.00 **não tem CPF do profissional** na solicitação (o solicitante vai por conselho, número, UF
e CBO): `--cpf-profissional` é conferido e citado na saída, mas não vai na mensagem; `--crm`, `--uf`
(código IBGE, 33 = RJ) e `--cbo` vão. Sem eles, vai o profissional de mentira (CRM 000000/RJ, CBO 225125).

#### UX · DX · CX

- 🧑‍💻 **UX** — a recepção não abre o portal: marca Chegou, pede o token ao paciente presente e digita 6
  dígitos na própria linha; o amarelo do dia lembra quem falta enquanto ainda dá. A baixa vira conferir e
  confirmar. Falha de rede nunca aparece como "negado".
- 🛠️ **DX** — uma entidade (`AutorizacaoTiss`, migration aditiva), um serviço (`AutorizacaoTissService`:
  pedir, cancelar, token, selo, aviso, divergência — com teste), dois processadores no conector e a
  mesma fila (sem tabela de agendamento nova: a pergunta da situação reaproveita a transação). Regra na
  Application; Razor só desenha (`AutorizacaoNaLinha`). Testes: `TissAutorizacaoWsTests`,
  `TissAutorizacaoFluxoTests`, `PapeisDoConectorTissTests`.
- 🤝 **CX** — menos glosa de senha (1403) e de token (3096/1406), sem copiar à mão; risco controlado por
  chave própria desligada, homologação antes e nenhum reenvio automático do que saiu.

### Testar a elegibilidade da Amil na VPS (primeira vez)

1. Publicar este commit (`deploy/tiss-conector/publicar.sh`) e instalar por cima (`sudo bash instalar.sh Amil`),
   ou só gravar a credencial se o pacote já for este:
   `export TISS_WS_LOGIN='LOGIN_DO_PORTAL_AMIL'; comando.sh credencial Amil --senha-na-entrada --operador 'Seu Nome'; unset TISS_WS_LOGIN`.
2. `comando.sh situacao Amil` deve dizer: login e senha cadastrados, envio desligado, `operadora Amil,
   registro ANS 326305 no catálogo`, `código do prestador: o login do web service` e a elegibilidade
   no endereço `api-tiss-verifica-elegibilidade/v4.01.00 (padrão do catálogo)`.
3. Se o cadastro do convênio Amil tiver um registro ANS diferente de 326305, ele vence: confira em
   Configurações do faturamento.
4. Peça a elegibilidade de UMA carteira conhecida pela ficha do paciente (ou pela baixa da guia) e
   acompanhe: `journalctl -u clinica-tiss-conector -f` e `comando.sh situacao Amil`.
5. Leitura do desfecho: "Elegível"/"Não elegível" é resposta da Amil; `LoginInvalido` pede conferir o
   login e a senha do portal (não repita: pode bloquear o acesso); recusa por código do prestador ou
   `RemetenteInvalido` pede `comando.sh codigo Amil cnpj` ou o código do credenciado; resposta que não
   é XML, HTTP 404 ou 405 pede o "link único" do Núcleo TISS (`comando.sh endereco Amil todos …`).

## Operação

### Girar a credencial (a senha do portal mudou, ou alguém saiu da clínica)

Repita o passo 4 com o login e a senha novos. Vale na próxima transação; não precisa reiniciar o
serviço. Para tirar a credencial: `comando.sh credencial Petrobras --remover --operador 'Seu Nome'`
(o conector passa a recusar as transações do convênio, com a frase na tela).

Trocar a **chave de cifra** (`CLINICA_CREDENCIAIS_CHAVE`) é outra coisa: ela protege também as
outras credenciais da clínica e é compartilhada com os apps e a ponte. Não troque só aqui.

### Ligar e desligar o envio de guias por convênio

São **duas chaves**, e uma guia só sai com as duas ligadas:

| Chave | Onde | Como |
|---|---|---|
| Do servidor | `/etc/clinica-tiss-conector/conector.env` | `TISS_ENVIO_HABILITADO=true` e `systemctl restart clinica-tiss-conector` |
| Do convênio | configuração no banco | `comando.sh envio Petrobras ligar --operador 'Seu Nome'` (ou `desligar`); vale na próxima transação, sem reiniciar |

As operadoras do catálogo (`OperadorasTiss`: Saúde Petrobras e Amil) **não precisam de endereço
cadastrado**: os do manual de cada uma já são o padrão. Para trocar, ou para operadora fora do
catálogo: `comando.sh endereco CONVENIO envio https://… --operador 'Seu Nome'` (idem
`elegibilidade`, `status` e `cancelamento`; `todos` grava o mesmo endereço nos quatro).

**Desligar na pressa:** `comando.sh envio Petrobras desligar --operador 'Seu Nome'`. Transação de
envio que já estava na fila é recusada com a frase "O envio pelo web service está desligado para
Petrobras. Nada foi enviado à operadora." A que já estava no meio da ida termina normalmente.

⚠️ **Com as duas chaves ligadas, o lote SAI para a operadora, em produção** (o manual dela não tem
homologação). Antes de ligar: a decisão LGPD registrada, e o primeiro envio com UMA guia, com
alguém olhando o portal para conferir se ela chegou e com que número.

### O que olhar no log

```bash
journalctl -u clinica-tiss-conector -f
```

| Linha | Quer dizer | O que fazer |
|---|---|---|
| `Transação TISS 12 (Elegibilidade, Petrobras) concluída.` | Tudo certo. | — |
| `… recusada antes do envio: …` | Nada saiu: envio desligado, sem credencial ou sem endereço. | Ler o motivo; ele também está na tela. |
| `… adiada: teto diário de 300 consultas atingido. Fica Pendente.` | O convênio bateu o teto de elegibilidade do dia. | Nada, se for esperado; para subir, ver "Limite de chamadas". |
| `… sem sucesso: sem resposta da operadora (…) Fica Pendente.` | A operadora não respondeu; o sistema tenta de novo. | Nada, se for esporádico. |
| `… sem sucesso: … LoginInvalido … Fica Falhou.` | A operadora recusou o login. | Girar a credencial. **Não** fique repetindo: pode bloquear o acesso. |
| `… sem sucesso: a operadora recusou a mensagem (HTTP 415): … Fica Falhou.` | A operadora não leu a mensagem (formato, endereço, proteção). Não se repete sozinho. | Ler o trecho do corpo; 415 = codificação/cabeçalho, 404/405 = endereço, 403 = proteção da operadora. |
| `… HTTP 502 sem mensagem TISS. Corpo: …` | Página de erro do gateway da operadora. | Nada, se for esporádico; o trecho diz quem respondeu. |
| `… Fica Falhou.` num envio | O envio não se completou. | Conferir **no portal da operadora** se a guia chegou, antes de pedir outro. |
| `Conector TISS: a volta falhou (NpgsqlException). Falhas seguidas: N.` | O banco não respondeu. | Ver o Postgres; o conector volta sozinho. |
| `42501` em qualquer lugar | Falta permissão no usuário do banco. | Rever `papel-tiss-conector.sql` (o serviço) ou `papel-tiss-operacao.sql` (o `comando.sh`). |

O log traz o número da transação, o tipo, o convênio e o desfecho. **Nunca** carteira, nome,
login ou senha — para saber de quem é a transação 12, olhe a tela, com o seu login.

### Rollback

- **Parar tudo:** `systemctl disable --now clinica-tiss-conector`. As telas continuam abrindo; os
  pedidos ficam `Pendente` na fila e nada vai à operadora. A baixa manual não depende do conector.
- **Voltar à versão anterior:** `ln -sfn /opt/clinica-tiss-conector/releases/tiss-conector-ANTERIOR
  /opt/clinica-tiss-conector/current && systemctl restart clinica-tiss-conector`.
- **Só o envio:** desligue uma das duas chaves (acima).
- **Antes de religar depois de uma parada longa:** olhe a fila (`comando.sh situacao`). Pedido de
  envio antigo que ficou `Pendente` **sai** quando o serviço voltar com as chaves ligadas. Se não
  for para sair, cancele-o pela tela antes de religar.
- **Remover de vez:** pare o serviço, apague `/etc/clinica-tiss-conector` (a credencial some com a
  chave) e rode `DROP ROLE tiss_conector` e `DROP ROLE tiss_operacao` depois de `REVOKE`. A tabela `TransacoesTiss` fica: é o
  registro do que foi dito à operadora.

## LGPD — a decisão que ainda falta (ponto 10 do compromisso)

Dito sem enfeite:

- **O que sai do sistema.** Na elegibilidade: o número da carteira, a validade e o CNPJ da
  clínica vão para a operadora do próprio paciente. Volta o nome do beneficiário. No envio de
  guia (quando ligado), irão os dados da guia. São os mesmos dados que a secretária já digita no
  portal da mesma operadora; o que muda é o caminho.
- **Por onde passa.** O web service da Saúde Petrobras é operado por uma conectividade contratada
  pela operadora (o endereço é de terceiro). É a mesma empresa por trás do portal, pelo que o
  endereço indica — **não foi confirmado com a operadora**. O da Amil é um domínio do próprio grupo
  da operadora (`api.servicos.grupoamil.com.br`), publicado no portal do credenciado; onde ficam os
  servidores dele também **não foi confirmado**. Na Amil, o que vai como código do prestador é o
  login do portal (não o CNPJ).
- **O que o compromisso exige.** O ponto 10 do `CLAUDE.md` diz que dado clínico novo em serviço
  externo é transferência a decidir, e que nenhuma integração nova manda dado para fora sem
  passar por essa decisão. **Essa decisão não está registrada.** A elegibilidade já foi feita uma
  vez em produção, por autorização direta da direção para o teste; isso não é a decisão do ponto 10.
- **O que eu recomendo registrar** em `docs/conformidade-lgpd.md` antes de qualquer tela usar a
  fila: (a) que o destinatário é a operadora do beneficiário, para faturamento do atendimento —
  a base legal provável é a de execução de serviço de saúde e de transações administrativas dele
  (LGPD, art. 11, II, "a" e "f", e §4º, II); (b) quem é o operador da conectividade e onde ficam
  os servidores dele (é a pergunta que decide se há transferência internacional); (c) que o
  canal é o oficial da operadora. **Isto é leitura técnica, não parecer jurídico**: quem valida é
  o encarregado de dados ou o jurídico da clínica.
- **O que passa a ficar guardado e não ficava.** Na tabela `TransacoesTiss`: a carteira consultada,
  o XML enviado (sem a senha) e a resposta da operadora, que traz o **nome do beneficiário**. É
  dado pessoal novo em repouso. Não há prazo de descarte definido nem rotina de limpeza — fica
  como pendência a decidir junto (guardar pelo prazo de recurso de glosa parece o mínimo útil).
- **Quem vê.** Desde 11/11/2026 a frase da tela (`Resultado`) da ELEGIBILIDADE leva o nome devolvido
  ("Nome na operadora: X."), para a recepção conferir com a ficha — vê quem já vê a ficha do paciente.
  O log continua sem nome. Os XML só saem do banco por
  `ObterTransacaoTissAsync`, que nenhuma tela usa hoje; quando uma tela os mostrar, isso pede
  permissão própria.
- **O que não há:** contrato de tratamento de dados com a conectividade (a relação é com a
  operadora), e resposta a incidente específica para a credencial da operadora — se ela vazar, o
  passo é trocar a senha no portal e girar a credencial aqui.

## O que não foi verificado

- **A autorização pelo sistema (nov/2026) nunca foi à Amil**, nem à homologação: está provado que o
  pedido, a pergunta da situação e o cancelamento da guia de solicitação saem válidos no XSD 4.03.00 e
  que as respostas escritas segundo o XSD são lidas certo. Não se sabe se a Amil aceita a solicitação
  com o login como código do prestador, se responde "autorizado" na hora para consulta (o manual diz que
  sim), se a validade vem na resposta, se o `cancela-guia` cancela uma guia de **solicitação**
  (`tipoGuia` 1) e se uma solicitação com quantidade N vale para acupuntura em série. A migration
  `AutorizacaoTissDaAmil` não rodou contra Postgres.

- **A Amil nunca foi chamada** (nem o WSDL, por este código): o primeiro teste real é na VPS. Não se
  sabe se o endereço APIM responde com o login e a senha do portal ou se exige o "link único"; se o
  `soapAction` vazio é aceito (é o do WSDL da ANS e o que funciona na Petrobras); se o login vai bem
  como `codigoPrestadorNaOperadora` ou se ela espera o CNPJ ou outro código; e se a resposta segue o
  XSD como a da Petrobras. Os endereços de lote, protocolo e cancelamento estão no catálogo, mas o
  `?wsdl` de lote e de protocolo responde 404 (informação da coordenação, 16/11/2026).
- **O usuário `tiss_operacao`** foi descrito a partir do script que funcionou na VPS; o `instalar.sh`
  e o `comando.sh` novos foram conferidos por sintaxe (`bash -n`) e a leitura do `.env` por um teste
  de linha de comando, não executados numa VPS.

- **Envio de lote, situação do protocolo e cancelamento nunca foram enviados à operadora.** O que
  está provado é que a mensagem é válida no XSD 4.01.00 e que as respostas previstas pelo XSD
  são lidas certo. Se a Saúde Petrobras aceita o lote por este canal, se devolve o protocolo na
  hora, se devolve o número da guia dela (e em que formato), se aceita número de guia com letra
  ("C12") e número de lote escolhido pela clínica, e o que faz no cancelamento: **não se sabe**.
  A lista completa está em `docs/tiss-webservice-petrobras.md`.
- **A versão.** As mensagens são 4.01.00, a do manual do web service dela e a da elegibilidade
  que funcionou. Há registro de que ela exige 4.03.00 no faturamento pelo portal; qual vale no
  web service de lote só o primeiro envio dirá.
- **O número do lote** ("9" + o número da transação, 12 dígitos) é escolha nossa para não colidir
  com os lotes em arquivo; não foi conferido com a operadora.
- **Nenhuma chamada real** foi feita por este código: todos os testes usam rede de mentira. A
  elegibilidade real foi provada pela ferramenta `tools/TissPetrobras`, que usa as mesmas mensagens,
  mas não este transporte.
- **O papel de banco não foi medido** contra um Postgres de verdade (não havia um na máquina de
  quem escreveu). A lista saiu da leitura do código.
- **A suíte contra o Postgres não rodou** (`CLINICA_TESTES_POSTGRES`), e a tabela ainda não tem
  migration. A reserva usa `ExecuteUpdate`, que os testes exercitam em SQLite; a consulta da fila
  tem teste de tradução para o Npgsql, a reserva não.
- **O serviço não foi instalado** em servidor nenhum: a unidade systemd, o `publicar.sh` e o
  `comando.sh` foram escritos e conferidos por sintaxe, não executados numa VPS.
- **Um conector por vez** é o desenho; dois ao mesmo tempo não pegam a mesma linha (testado), mas
  não há por que subir dois.
