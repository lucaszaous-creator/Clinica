# As telas do envio à operadora (nov/2026)

> **Pedido da direção, ao longo de 11/11/2026:** a baixa no sistema acontece junto com a Saúde
> Petrobras, sem digitar no portal; "se for envio em lote precisamos nos certificar da baixa em
> lote também"; os campos vêm preenchidos do cadastro e a secretária completa o resto; e a baixa
> manual (copiar o número do portal) continua existindo.

O motor já existia (`docs/tiss-conector.md`: a fila, o serviço de envio no servidor, a credencial)
e as mensagens também (`docs/tiss-webservice-petrobras.md`). Este documento é a última peça: **o
que a pessoa vê e clica**. Nenhuma tela fala com a operadora — toda ação entra na fila, e quem
fala é o serviço de envio.

## Onde cada coisa ficou

| O quê | Caminho na interface | Aparece quando |
|---|---|---|
| **Conferir e deixar pronta para envio** | Faturamento → Pendências e baixas → **Dar baixa** numa guia da Saúde Petrobras → rodapé do formulário | o envio pelo web service está **ligado** para o convênio |
| **Consultar na operadora** (elegibilidade) | o mesmo formulário, sob os dados do beneficiário; e na ficha do paciente, cartão **"Pode ser atendido hoje?"** | o convênio tem **credencial gravada** |
| **Selo de envio** da guia | Faturamento → Pendências e baixas, na coluna "Modalidade e código" de cada linha | a guia é de convênio com web service |
| **Envio à operadora** (montar o lote, acompanhar, baixar as recebidas) | Faturamento → **Guias TISS** → aba **Envio pelo web service** | sempre (com o envio desligado, a aba diz onde ligar e não oferece envio) |
| **Credencial e chave de envio** | Faturamento → Configurações → Convênios → botão **Web service** na linha do convênio | o convênio é da família Petrobras e já foi salvo |

## O caminho de uma guia

1. **Dar baixa** abre a guia ANS preenchida (cadastro do profissional, ficha, configurações).
   A pessoa confere e escolhe: **Confirmar baixa** (como sempre, com o número copiado do portal)
   ou **Conferir e deixar pronta para envio** (grava a conferência, sem número e sem baixa).
   O rodapé diz: *"Pronta para envio não é enviada."*
2. O painel de pendências passa a mostrar **Pronta para envio** na linha.
3. Em **Guias TISS → Envio pelo web service**, a guia está em "Prontas para envio". Marca-se
   uma ou várias (até 100); o que impede uma guia aparece **na linha dela**, antes do clique.
4. **Enviar à operadora** pergunta: quantas guias, para quem, e que é **cobrança de verdade**.
   Confirmado, o pedido entra na fila — e a tela diz **"Na fila: ainda não foi à operadora"**.
5. A lista se atualiza sozinha enquanto houver pedido na fila ou em andamento. O envio termina
   como **Recebido**, **Recebido com glosa** (o motivo na linha da guia), **Protocolado** (há guia
   sem resposta: "Consultar situação na operadora"), **Não enviado** ou **Não se sabe se chegou**.
6. **Dar baixa nas recebidas · N** dá baixa, em nome de quem clicou, nas guias que a operadora
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
