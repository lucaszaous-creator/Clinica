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
   guia que está num envio na fila, em andamento, ou já recebida pela operadora;
5. a regra vale para o **lote inteiro e para cada guia dele**: lote que falhou marca todas as
   guias como "falhou" e nenhuma volta sozinha; guia glosada só é reenviada por um pedido novo,
   e sai com **outro número** (`C12`, depois `C12R2`), para a operadora nunca ver o mesmo número
   duas vezes.

Elegibilidade e consulta de protocolo só perguntam: tentam de novo até 3 vezes (espera de 1 min,
depois 5 min) **quando a operadora não respondeu**. Quando ela respondeu "não" (login recusado,
mensagem fora do padrão), não há nova tentativa: repetir não muda a resposta, e login recusado
repetido pode bloquear o acesso da clínica.

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

**2. O usuário do banco** — na VPS 1, como root, com o `papel-tiss-conector.sql` que veio na pasta
publicada (`deploy/`):

```bash
sudo -u postgres psql -p 45432 -d clinica -v senha="'SENHA_FORTE'" -f papel-tiss-conector.sql
```

No `pg_hba.conf`, **antes** das linhas `hostssl`: `local clinica tiss_conector scram-sha-256`.
Depois, `systemctl reload postgresql`. O conector entra só pelo socket local, nunca por TCP.

**3. Os arquivos** — na VPS 1, como root:

```bash
useradd --system --home-dir /nonexistent --shell /usr/sbin/nologin --user-group clinica-tiss
mkdir -p /opt/clinica-tiss-conector/releases /etc/clinica-tiss-conector
chmod 700 /etc/clinica-tiss-conector
# copie o .tar.gz, confira o sha256sum e:
tar -xzf tiss-conector-XXXXXXX.tar.gz -C /opt/clinica-tiss-conector/releases
ln -sfn /opt/clinica-tiss-conector/releases/tiss-conector-XXXXXXX /opt/clinica-tiss-conector/current

cd /opt/clinica-tiss-conector/current/deploy
install -m 600 conector.env.example /etc/clinica-tiss-conector/conector.env
install -m 600 admin.env.example    /etc/clinica-tiss-conector/admin.env
```

Edite os dois arquivos em `/etc/clinica-tiss-conector/`:

- `conector.env`: a senha do usuário `tiss_conector` e a `CLINICA_CREDENCIAIS_CHAVE`. Se os apps
  e a ponte do CRM já usam essa chave, é **a mesma**. Se a clínica ainda não a tem, gere com
  `openssl rand -base64 32` e **guarde uma cópia fora do servidor**. Deixe `TISS_ENVIO_HABILITADO=false`.
- `admin.env`: a conexão do usuário do banco que os apps usam. O serviço não lê este arquivo.

**4. Cadastrar a credencial da operadora** — a senha é digitada na hora e não fica em arquivo
nem no histórico do terminal:

```bash
export TISS_WS_LOGIN='LOGIN_DA_CONECTIVIDADE'
/opt/clinica-tiss-conector/current/deploy/comando.sh credencial Petrobras --senha-na-entrada --operador 'Seu Nome'
# (digite a senha e Enter; ela não aparece no retorno)
unset TISS_WS_LOGIN
```

`Petrobras` é o código do convênio no cadastro; `--operador` é quem está fazendo, e vai para a trilha.

**5. Ligar o serviço:**

```bash
cp /opt/clinica-tiss-conector/current/deploy/clinica-tiss-conector.service /etc/systemd/system/
systemctl daemon-reload && systemctl enable --now clinica-tiss-conector
journalctl -u clinica-tiss-conector -n 20
```

O que deve aparecer: `Conector TISS no ar. Envio de guias neste servidor: desligado. Intervalo: 5s.`

**6. Conferir:**

```bash
/opt/clinica-tiss-conector/current/deploy/comando.sh situacao Petrobras
# Petrobras: login cadastrado, senha cadastrada, envio desligado.
```

Depois, peça uma elegibilidade por uma tela (quando houver) e veja a linha passar de `Pendente`
a `Concluida` no mesmo comando.

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

A Saúde Petrobras **não precisa de endereço cadastrado**: os quatro do manual dela já são o
padrão. Para outra operadora, ou para trocar: `comando.sh endereco CONVENIO envio https://…
--operador 'Seu Nome'` (idem `elegibilidade`, `status` e `cancelamento`).

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
| `… sem sucesso: sem resposta da operadora (…) Fica Pendente.` | A operadora não respondeu; o sistema tenta de novo. | Nada, se for esporádico. |
| `… sem sucesso: … LoginInvalido … Fica Falhou.` | A operadora recusou o login. | Girar a credencial. **Não** fique repetindo: pode bloquear o acesso. |
| `… Fica Falhou.` num envio | O envio não se completou. | Conferir **no portal da operadora** se a guia chegou, antes de pedir outro. |
| `Conector TISS: a volta falhou (NpgsqlException). Falhas seguidas: N.` | O banco não respondeu. | Ver o Postgres; o conector volta sozinho. |
| `42501` em qualquer lugar | Falta permissão no usuário do banco. | Rever `papel-tiss-conector.sql`. |

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
  chave) e rode `DROP ROLE tiss_conector` depois de `REVOKE`. A tabela `TransacoesTiss` fica: é o
  registro do que foi dito à operadora.

## LGPD — a decisão que ainda falta (ponto 10 do compromisso)

Dito sem enfeite:

- **O que sai do sistema.** Na elegibilidade: o número da carteira, a validade e o CNPJ da
  clínica vão para a operadora do próprio paciente. Volta o nome do beneficiário. No envio de
  guia (quando ligado), irão os dados da guia. São os mesmos dados que a secretária já digita no
  portal da mesma operadora; o que muda é o caminho.
- **Por onde passa.** O web service da Saúde Petrobras é operado por uma conectividade contratada
  pela operadora (o endereço é de terceiro). É a mesma empresa por trás do portal, pelo que o
  endereço indica — **não foi confirmado com a operadora**.
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
- **Quem vê.** A frase da tela (`Resultado`) não leva o nome. Os XML só saem do banco por
  `ObterTransacaoTissAsync`, que nenhuma tela usa hoje; quando uma tela os mostrar, isso pede
  permissão própria.
- **O que não há:** contrato de tratamento de dados com a conectividade (a relação é com a
  operadora), e resposta a incidente específica para a credencial da operadora — se ela vazar, o
  passo é trocar a senha no portal e girar a credencial aqui.

## O que não foi verificado

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
