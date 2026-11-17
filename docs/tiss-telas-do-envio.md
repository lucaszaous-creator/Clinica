# As telas do envio à operadora (nov/2026)

> **Pedido da direção, ao longo de 11/11/2026:** a baixa no sistema acontece junto com a Saúde
> Petrobras, sem digitar no portal; "se for envio em lote precisamos nos certificar da baixa em
> lote também"; os campos vêm preenchidos do cadastro e a secretária completa o resto; e a baixa
> manual (copiar o número do portal) continua existindo.

O motor já existia (`docs/tiss-conector.md`: a fila, o serviço de envio no servidor, a credencial)
e as mensagens também (`docs/tiss-webservice-petrobras.md`). Este documento é a última peça: **o
que a pessoa vê e clica**. Nenhuma tela fala com a operadora — toda ação entra na fila, e quem
fala é o serviço de envio.

## A baixa que envia (correção de fluxo, 11/11/2026)

> **A direção, ao ver o formulário:** *"Número da guia gerada… a guia só é gerada APÓS o envio."*
> O que ela esperava: **enviar/dar baixa → o sistema fala com a Saúde Petrobras → aparece um
> processo carregando → quando a operadora devolve a guia, ela aparece e é salva sozinha.**

A primeira versão destas telas tinha dois defeitos de fluxo, e nenhum deles quebrava nada:
pedia, no formulário de quem ia ENVIAR, um número que só existe DEPOIS do envio; e espalhava um
ato só (cobrar a guia) por três lugares — conferir aqui, enviar em Guias TISS, voltar para baixar.
**Com o envio ligado para o convênio**, o formulário de baixa passou a ser assim (com o envio
desligado ele continua **idêntico** ao de sempre):

1. **Uma ação principal: "Enviar para {convênio} e dar baixa".** O bloco "Baixa no sistema" só
   pede a data e a observação, e diz: *"O número da guia não é digitado: {convênio} o devolve
   depois do envio, e ele é gravado na baixa."*
2. O clique pergunta (é **cobrança de verdade**, de uma guia), grava a conferência
   (`ConferirGuiaAnsAsync`) e pede o envio só daquela guia (`EnfileirarEnvioAsync`). A recusa da
   fila aparece no formulário, com a frase da conferência.
3. O formulário **não fecha**: os campos ficam só leitura e um bloco de acompanhamento mostra os
   passos — *Guia conferida → Na fila de envio → Enviando para {convênio} → Resposta recebida* —,
   relendo o pedido a cada 2 s. Fechar a janela para o relógio e **não** cancela o pedido.
4. **Cada desfecho tem a sua frase e as suas saídas** (`BaixaComEnvioNaTela.Ler`, com teste):

| Desfecho | O que a tela diz | O que oferece |
|---|---|---|
| **Recebida, com número** | "Guia nº X gerada por {convênio} · protocolo P. Baixa registrada." | Copiar o número · Fechar. A baixa foi dada **sozinha**, com o número devolvido |
| **Recebida, sem número** | "{convênio} recebeu a guia (protocolo P) e não informou o número dela; a baixa ficou com o nosso número C…" | Fechar |
| **Recebida, baixa recusada** | "…recebeu a guia · protocolo P — mas a baixa NÃO foi registrada", o motivo, e que **não** precisa enviar de novo | Dar baixa com o número do portal |
| **Glosada** | "{convênio} recusou esta guia (glosa)" + o motivo + "Nada foi baixado" | Os campos voltam a ser editáveis; **Corrigir e enviar de novo** |
| **Não se sabe se chegou** | Aviso forte: conferir no portal ANTES de qualquer novo envio | **Nenhum** botão de envio; só "Dar baixa com o número do portal" |
| **A operadora recusou o envio** (resposta clara, com o motivo) | "{convênio} recusou o envio." + o motivo + "Nada foi baixado" | Os campos voltam a ser editáveis; **Corrigir e enviar de novo** (não é dúvida: nada ficou lá) |
| **O serviço não respondeu** (45 s na fila) | "O serviço de envio não respondeu." — o pedido continua na fila | Continuar esperando · Cancelar o pedido · baixa manual |
| **Não enviada** (envio desligado no servidor, pedido cancelado, falhou antes de sair) | A frase que o serviço de envio gravou | A guia volta a poder ser enviada |

5. **A baixa manual é a reserva, recolhida:** "Já tenho o número do portal (baixa manual)" abre o
   campo do número e o "Confirmar baixa" de sempre.
6. "Conferir e deixar pronta para envio" virou **"Deixar para enviar em lote"**, secundário.
7. **Reabrir "Dar baixa"** de uma guia que já tem pedido (na fila, enviada, recebida, glosada ou
   em dúvida) abre direto no acompanhamento — não oferece enviar de novo.
8. **Na aba "Envio pelo web service"**, a resposta que chega com a tela aberta já dá baixa nas
   recebidas; o botão "Dar baixa nas recebidas" fica para quem fechou a tela antes da resposta.

**Quem assina a baixa automática:** quem está **logado na tela que a registra** — que é quem pediu
o envio, porque a baixa sozinha só acontece na mesma abertura (do formulário ou da aba) em que o
envio foi pedido. Quem reabre depois vê "Registrar a baixa" e assina com o próprio login. O serviço
de envio (conector) **não** dá baixa: ele só grava a resposta da operadora.

### Opções comparadas (o fluxo)

| | A — o formulário envia e acompanha (feito) | B — manter três passos (conferir, enviar na aba, baixar) | C — o serviço de envio dá a baixa sozinho no servidor |
|---|---|---|---|
| 🧑‍💻 UX | um clique e uma espera à vista; o número aparece na tela | três telas para um ato; o formulário pedia um número que não existe | zero clique, mas a baixa aparece "do nada" e sem ninguém olhando o desfecho |
| 🛠️ DX | uma classe pura (`BaixaComEnvioNaTela`) + o relógio na view; a fila e a baixa são as mesmas | nada novo | a baixa passaria a ter um autor que não é gente, e a regra do `DarBaixaAsync` teria de aceitar isso |
| 🤝 CX | o que a direção descreveu; a baixa continua com nome de pessoa na trilha | o motivo da reclamação | baixa sem responsável e sem a data escolhida por quem fatura |

### 🧑‍💻 UX · 🛠️ DX · 🤝 CX desta correção

- 🧑‍💻 **UX** — Um clique em vez de três telas. O "processo carregando" é o passo atual pulsando
  (sem movimento para quem pediu menos movimento). O bloco de acompanhamento fica **preso ao topo**
  do formulário rolado: a foto de 1024 mostrou que, sem isso, quem clica no rodapé via só campos
  apagados e o clique parecia não ter feito nada. Nenhum estado promete o que não houve: "na fila"
  não é "enviada", recebida sem baixa diz que falta a baixa, e a dúvida não tem botão de envio.
- 🛠️ **DX** — O que a tela afirma mora em `BaixaComEnvioNaTela` (fases, passos, frases, o que cada
  desfecho permite), com `BaixaComEnvioNaTelaTests`; o Razor só desenha. O relógio é da view
  (`PeriodicTimer` + contador de geração + `Dispose`). `BaixaQueEnviaBlazorTests` clica o fluxo
  inteiro sobre os serviços de verdade. **A guarda do reenvio em dúvida desceu para a fila**
  (`FilaTissService`: `conferidasNoPortal`), porque agora há DUAS telas que pedem envio — trava só
  de tela é trava que a próxima tela esquece.
- 🤝 **CX** — É o fluxo que a direção descreveu, e o envio continua desligado por padrão em dois
  níveis (convênio e servidor). Risco novo e assumido: o clique do formulário é cobrança de verdade
  de uma guia — por isso a pergunta, e por isso exige o acesso de dar baixa.

### O nome que a operadora devolve na elegibilidade

"Consultar na operadora" passou a mostrar **"Na operadora: NOME · na ficha: Nome. Os nomes
conferem."** (ou, em destaque, que são DIFERENTES — a ficha não é alterada). Sem coluna nova: o
serviço de envio escreve o nome no `Resultado` da transação ("… Nome na operadora: X."), e
`EnvioTissNaTela.ConferenciaDoNome` compara ignorando maiúsculas e acentos.

### O que ficou diferente do pedido nesta correção

- **O rótulo leva o nome do catálogo**: "Enviar para Petrobras e dar baixa" (o convênio se chama
  "Petrobras" no catálogo da clínica; o texto do pedido dizia "Saúde Petrobras").
- **Enviar pelo formulário exige o acesso de dar baixa** (`BaixarGuia`): o clique termina em baixa.
- **Quem REABRE uma guia já recebida vê o botão "Registrar a baixa"**, não a baixa sozinha: abrir
  um formulário não deve gravar nada sem clique, e a baixa leva o login de quem clicou.
- **A baixa das recebidas é por ENVIO** (`BaixarRecebidasAsync`): registrar pelo formulário de uma
  guia de um lote baixa também as outras recebidas daquele lote, com a mesma data.
- **Glosada também oferece a baixa manual** (a pessoa pode ter resolvido no portal).
- **"Não se sabe se chegou" ficou mais estreito do que na primeira versão destas telas.** Antes, todo
  envio que saiu e falhou era tratado como dúvida — inclusive o lote que a operadora **recusou com
  resposta clara** (versão do padrão inválida, por exemplo), que não deixa nada lá. Agora a dúvida é
  o que o serviço de envio marcou como dúvida (`FilaTiss.MarcaDeDuvida` no erro gravado; o corte do
  texto preserva a marca). A recusa clara tem rótulo próprio ("Recusado pela operadora") e não pede
  declaração para reenviar — foram três testes do motor que mostraram a diferença.
- **A espera pelo serviço é de 45 s** (`BaixaComEnvioNaTela.EsperaPeloServico`) — número escolhido,
  não medido contra o serviço em produção.

## Onde cada coisa ficou

| O quê | Caminho na interface | Aparece quando |
|---|---|---|
| **Enviar e dar baixa** (principal) e **Deixar para enviar em lote** | Faturamento → Pendências e baixas → **Dar baixa** numa guia da Saúde Petrobras → rodapé do formulário | o envio pelo web service está **ligado** para o convênio |
| **Consultar na operadora** (elegibilidade) | o mesmo formulário, sob os dados do beneficiário; e na ficha do paciente, cartão **"Pode ser atendido hoje?"** | o convênio tem **credencial gravada** |
| **Selo de envio** da guia | Faturamento → Pendências e baixas, na coluna "Modalidade e código" de cada linha | a guia é de convênio com web service |
| **Envio à operadora** (montar o lote, acompanhar, baixar as recebidas) | Faturamento → **Guias TISS** → aba **Envio pelo web service** | sempre (com o envio desligado, a aba diz onde ligar e não oferece envio) |
| **Credencial e chave de envio** | Faturamento → Configurações → Convênios → botão **Web service** na linha do convênio | o convênio é da família Petrobras e já foi salvo |

## O caminho de uma guia

1. **Dar baixa** abre a guia ANS preenchida (cadastro do profissional, ficha, configurações).
   A pessoa confere e clica **Enviar para {convênio} e dar baixa** (ver "A baixa que envia", acima):
   o formulário acompanha o pedido e registra a baixa quando a operadora responde. As outras duas
   saídas: **Deixar para enviar em lote** (grava a conferência, sem número e sem baixa — é o que
   os passos 2 a 6 descrevem) e a **baixa manual**, recolhida, com o número copiado do portal.
2. O painel de pendências passa a mostrar **Pronta para envio** na linha.
3. Em **Guias TISS → Envio pelo web service**, a guia está em "Prontas para envio". Marca-se
   uma ou várias (até 100); o que impede uma guia aparece **na linha dela**, antes do clique.
4. **Enviar à operadora** pergunta: quantas guias, para quem, e que é **cobrança de verdade**.
   Confirmado, o pedido entra na fila — e a tela diz **"Na fila: ainda não foi à operadora"**.
5. A lista se atualiza sozinha enquanto houver pedido na fila ou em andamento. O envio termina
   como **Recebido**, **Recebido com glosa** (o motivo na linha da guia), **Protocolado** (há guia
   sem resposta: "Consultar situação na operadora"), **Não enviado** ou **Não se sabe se chegou**.
6. Com a tela aberta, a baixa das recebidas é dada **sozinha** quando a resposta chega. **Dar baixa nas recebidas · N** (para quem fechou a tela antes) dá baixa, em nome de quem clicou, nas guias que a operadora
   recebeu, com o número que ela devolveu. Glosada e sem resposta ficam de fora, à vista. A falha
   de uma guia (o formato do número do convênio, por exemplo) aparece na linha dela.

### Os estados, como a tela os escreve

| A guia / o envio | O que a tela diz | Cor | O que se oferece |
|---|---|---|---|
| Nunca conferida | Não enviada | neutro | Dar baixa (painel) |
| Conferida, fora de envio | **Pronta para envio** | azul | marcar e enviar |
| Pedido feito, ninguém buscou | **Na fila**: ainda não foi à operadora | azul | Cancelar pedido |
| A operadora recebeu | Recebida pela operadora, protocolo N | verde | Dar baixa nas recebidas |
| A operadora glosou | Glosada: código - motivo | vermelho | (pede gente: corrigir e pedir de novo) |
| Protocolada, sem resposta sobre ela | Enviada, protocolo N: sem resposta sobre esta guia | âmbar | Consultar situação na operadora |
| Pedido cancelado ou recusado antes de sair | Não enviado (com o motivo) | neutro | a guia volta a "pronta" |
| **A mensagem saiu e não há resposta em que confiar** | **Não se sabe se chegou** — "Confira no portal da operadora ANTES de enviar essas guias de novo" | vermelho, com a borda do bloco | **nada com um clique** |

A última linha é a regra que não se negocia (`docs/tiss-conector.md`, "envio de guia nunca é
reenviado sozinho"), trazida para a tela em dois lugares:

- o **bloco do envio** não tem botão nenhum — só o aviso;
- a fila devolve essas guias à lista das **prontas** (pedir de novo é ato de gente), e ali cada
  uma nasce **travada**: a caixa de marcar fica apagada, o "marcar todas" a pula, e o envio é
  recusado se ela estiver marcada. Para liberar, o botão **"Conferi no portal: não está lá"**
  abre uma pergunta em que a pessoa declara o que conferiu. Só então a guia pode ser marcada —
  e enviar continua sendo outro clique, com a pergunta do envio. A liberação vale para aquela
  sessão da tela e não é gravada.

A guia **glosada** e a do envio que **falhou antes de sair** também voltam às prontas, sem trava,
com o motivo escrito na linha ("Corrija a guia antes de enviar de novo").

No **painel de pendências** o selo é curto (Não enviada · Pronta para envio · Na fila · Enviada ·
Recebida · Glosada · Envio falhou) e a frase inteira — protocolo, motivo — vem logo abaixo, em
texto: a coluna tem ~140 px, e a frase dentro do selo virava um bloco de quatro linhas.

## Opções comparadas

### Onde mora o envio: aba em "Guias TISS", tela nova ou seção no painel?

| | A — aba em Guias TISS (feito) | B — item novo no menu ("Envio à operadora") | C — seção no painel de pendências |
|---|---|---|---|
| 🧑‍💻 UX | "o que foi à operadora" continua tendo UMA porta; a aba aparece ao lado dos lotes em arquivo, que respondem à mesma pergunta | mais um item no menu para a mesma pergunta — a regra "uma porta por pergunta" | o painel já tem 60+ linhas e três blocos; o envio ficaria abaixo da dobra |
| 🛠️ DX | um componente (`EnvioAOperadoraPainel`) dentro de uma tela que já existe; nenhuma chave de navegação nova, nenhum catálogo a regerar | tela nova = chave no módulo, catálogo da gestão web, semente e teste de menu | a tela mais delicada do faturamento ganharia mais um relógio e mais estado |
| 🤝 CX | nada novo para achar; quem fatura já abre essa tela | treinar "onde fica" | risco de a rodada e o envio se atrapalharem na mesma tela |

### Onde mora a credencial: no formulário do convênio ou em formulário próprio?

| | A — formulário próprio, pelo botão "Web service" da linha (feito) | B — campos dentro do formulário do convênio |
|---|---|---|
| 🧑‍💻 UX | grava **na hora**, com resposta na própria janela; a senha some ao gravar | o formulário do convênio só vale no "Salvar configurações" da tela de trás: uma senha "pendente" numa tela aberta |
| 🛠️ DX | chama o `CredenciaisTissService` direto (que já audita); o `ConvenioForm` não muda | misturaria dois ciclos de gravação no mesmo formulário |
| 🤝 CX | a credencial tem trilha própria, e quem só renomeia um convênio não passa perto da senha | — |

### "Pronta para envio": botão a mais na baixa ou tela separada de conferência?

| | A — segundo botão no formulário de baixa (feito) | B — tela só de conferência |
|---|---|---|
| 🧑‍💻 UX | a mesma guia, a mesma conferência, dois desfechos lado a lado; a baixa manual não muda de lugar | duas telas com os mesmos 25 campos |
| 🛠️ DX | um método a mais no formulário; `ConferirGuiaAnsAsync` já existia | duplicaria o formulário da guia |
| 🤝 CX | com o envio desligado o formulário fica **idêntico** ao de antes | — |

## 🧑‍💻 UX

- **Cada estado tem um nome e nenhum se parece com o outro.** "Pronta", "na fila", "enviada",
  "recebida" e "não se sabe se chegou" são frases diferentes, com cores diferentes.
- **A recusa vem na linha, antes do clique**: marcar uma guia já confere o envio; se algo mudou
  entre a conferência e o pedido (outra máquina deu baixa), a recusa volta na linha e **nada** é
  enviado — nem as guias que estavam em ordem.
- **A pergunta do envio** diz quantas guias, para qual operadora, e que é cobrança de verdade.
- **Nada pisca.** A releitura de 5 em 5 segundos é silenciosa, só acontece enquanto há pedido na
  fila ou em andamento, e para quando a pessoa sai da aba.
- **A senha nunca aparece**: o campo nasce vazio, não tem o valor escrito de volta na página e é
  recriado vazio depois de gravar. A tela só diz "há uma senha gravada".
- **Elegibilidade com três desfechos**: elegível (verde), não elegível com o motivo (vermelho) e
  "não foi possível consultar" com o motivo (âmbar). Sem serviço de envio, depois de um minuto a
  tela diz "o serviço de envio não respondeu" e oferece cancelar o pedido.
- Vistas na prévia em 1366 e 1024, com o formulário aberto (capturas com a entrega).

## 🛠️ DX

- **Regra só na Application**: `EnvioTissNaTela` (puro, com teste) decide rótulo, cor, o que cabe
  em cada estado, a pergunta do envio, a crítica da credencial e a leitura da elegibilidade.
- Peças novas: `Componentes/EnvioAOperadoraPainel.razor`, `Componentes/ElegibilidadeNaOperadora.razor`
  e `Formularios/Faturamento/WebServiceDoConvenioForm.razor`. Estilo em `telas/fat-tiss.css`, só token.
- A trava da guia em dúvida é `EnvioTissNaTela.DaPronta` (a ressalva de cada pronta, a partir do
  selo do último envio); a liberação é estado da tela, de propósito: gravar "conferi no portal"
  pediria coluna e trilha, e isso é decisão para depois do primeiro uso real.
- `BaixaGuiaForm` ganhou o parâmetro `AoConferir`: conferir **não** é concluir, e quem está atrás
  não pode dizer "baixa registrada" sobre uma guia que continua pendente.
- O selo do painel custa **duas leituras por convênio com web service** (selos + prontas), nunca
  uma por linha; falha nessa leitura não derruba o painel.
- **Nenhuma migration, nenhuma coluna nova, nenhum serviço da fila alterado.**
- Testes: `EnvioTissNaTelaTests` (as frases e as cores) e `TelasDoEnvioBlazorTests` (bUnit sobre o
  serviço de verdade: pronta sem baixa, envio de duas, guia impedida, resultado → selo → baixa das
  recebidas, credencial, chave desligada, acesso negado). A semente `Sementes/EnvioTiss.cs` deixa
  uma guia em cada estado, e a suíte que abre cada tela e clica cada botão passa por elas.

## 🤝 CX

- **Com o envio desligado nada muda** para quem fatura: o formulário de baixa é o de sempre.
- **Três chaves antes de qualquer guia sair**: a do convênio (esta tela), a do servidor e o
  serviço de envio instalado. A tela diz isso ao lado da chave.
- **A baixa manual continua** — é o caminho de volta quando o envio falha ou fica em dúvida.
- Permissões: enviar, consultar e cancelar pedem **gerenciar lotes TISS**; conferir e dar baixa
  pedem **dar baixa em guia**; a credencial e a chave pedem **configurar o faturamento**.

## O que ficou diferente do pedido, e por quê

- **O nome devolvido pela operadora não aparece na elegibilidade.** A fila guarda só a frase do
  resultado; o nome fica no XML, no servidor, e a leitura das telas não traz o XML (decisão do
  conector, para a ficha do paciente não trafegar). A tela mostra o nome **da ficha** e manda
  conferir no portal. Mostrar o nome da operadora pede uma coluna ou um campo no resultado.
- **A credencial só é gravada onde a chave de proteção do servidor existe.** Num computador da
  clínica sem essa chave, o serviço recusa com a frase que diz onde cadastrar, e a tela a mostra.
  A chave de envio (ligar/desligar) e a leitura "há senha gravada" funcionam em qualquer posto.
- **O botão de elegibilidade depende da credencial**, não da chave de envio: consultar não é
  cobrar, e sem credencial o pedido ficaria parado para sempre.
- **A liberação "conferi no portal" não fica registrada** (nem coluna nem auditoria): é uma
  pergunta respondida na tela. O pedido de envio que vem depois é auditado como qualquer outro.
- **"Cancelar na operadora" (guia já recebida) não tem botão**: o pedido falava em cancelar o que
  ainda não saiu, e cancelamento na operadora é ato raro e irreversível que merece decisão própria.

## O que não foi verificado

- Nenhuma operadora foi chamada e não há serviço de envio na prévia nem nos testes: os desfechos
  foram gravados pelo mesmo `FilaTissService` que o serviço de envio usa.
- O app Windows de verdade, o teclado (Tab, Ctrl+S com o formulário por cima) e a gestão web.
- A suíte contra o Postgres.
- A gravação da credencial num posto real (depende da chave do servidor naquele ambiente).
