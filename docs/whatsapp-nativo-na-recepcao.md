# O WhatsApp DENTRO da Recepção, e não o CRM dentro de uma moldura (proposta, out/2026)

> **Status: proposta com mockup, nada implementado.** A direção olhou a aba WhatsApp da fase 1
> (`docs/crm-na-recepcao.md`) e resumiu: *"o que foi feito foi clonar o CRM e colar dentro do
> sistema"*. Este documento responde à pergunta que veio junto, *"como você faria?"*, com a
> comparação dos caminhos nos três eixos (UX · DX · CX) e um mockup feito sobre o CSS REAL da
> casa. O mockup está em `docs/mockups/whatsapp-nativo/` (fotos abaixo).

## 1. O que existe hoje e por que incomoda

A aba **WhatsApp** da Recepção abre o CRM inteiro (`https://crm…/?modo=recepcao`) num `iframe`,
entrando sem senha por um bilhete de uso único. O CRM esconde Agenda, Meu dia, Relacionamento e o
sino, e o resto fica igual ao navegador. Foi a escolha mais barata e a mais segura para a
fase 1, e ela resolveu o Alt+Tab e a segunda senha.

O que ela NÃO resolve, medido no que a pessoa vê:

| Sintoma | Causa |
|---|---|
| Dentro da janela da Recepção aparece outro produto: outra fonte, outros botões, outro cinza, outro "ficha do contato" | São dois design systems na mesma tela. O CRM tem o `design.css` dele; a Recepção tem os tokens do `Tokens.xaml`. |
| A ficha que o CRM mostra ao lado da conversa é a cópia que ele monta pela ponte (`ClinicPatientCard`): nome, convênio, próximas sessões | A informação verdadeira está a um clique, no mesmo app, e a tela não a usa. A agenda, a confirmação do dia, o Acompanhamento e o 2º código já estão carregados pela Recepção. |
| "Remarcar", "Confirmar" e "Cancelar" na conversa passam pelo CRM, que chama a ponte, que chama o `AgendaService` | Três saltos para uma ação que o app ao lado faz em um. |
| Sessão do CRM vence em 2 h e aparece "Entrar de novo" | A moldura não sabe renovar; o app sabe emitir bilhete à vontade. |
| A barra de cima da aba explica "Agenda e Relacionamento continuam nas abas da Recepção" | Texto que existe para pedir desculpa pelo que a tela não integra. |
| O sino da Recepção diz "Atenção no WhatsApp" e leva à aba; dentro dela a pessoa procura a conversa | O aviso e a conversa estão em sistemas diferentes. |

A regra 1 da direção para telas de quem atende (*uma porta por pergunta*) está formalmente
atendida (uma aba), mas a porta abre para outra casa.

## 2. Os caminhos comparados

| | 🧑‍💻 UX (quem usa) | 🛠️ DX (quem mantém) | 🤝 CX (a clínica) |
|---|---|---|---|
| **A · Moldura do CRM (hoje)** | Dois produtos numa janela; ficha duplicada; ações da agenda em três saltos; "Entrar de novo" a cada 2 h; a aba não conversa com a agenda nem com a ficha. | Quase nenhum código no Clinica (bilhete + `iframe`). Mas cada tela nova da Recepção que quer "a conversa ao lado" tem de navegar até a aba e passar um pedido pela URL (`PedidoDeConversaNoCrm`), e o `modo=recepcao` precisa esconder no CRM o que a Recepção já tem, para sempre. | Barato, sem servidor novo. "Parece profissional?": não, parece colado, e foi exatamente isso que a direção viu. |
| **B · Tela nativa Blazor sobre a API do CRM** (recomendado) | Uma tela da casa: lista, conversa e, ao lado, a ficha, a agenda, a confirmação do dia e o Acompanhamento **lidos do próprio app**. Remarcar/cancelar são os botões da agenda da Recepção. O "pede ação" entra na conversa como aviso com os botões. Nunca pede senha. | Uma tela (`Telas/Recepcao/ConversasDoWhatsApp.razor`), um cliente HTTP do CRM (`IClienteDoCrm` na Application, `ClienteDoCrmHttp` na Infrastructure) e um modelo puro do que a tela afirma (`ConversasNaTela`, com teste). O CRM continua sendo o único que fala com a Meta e com o SMS: modelos, janela de 24 h, fila, jobs, PARAR, recall, @SemDor. Duas interfaces para os mesmos dados (esta e o CRM web/celular), o que exige um contrato de API estável e versionado. | Nenhum servidor novo. A recepção ganha a tela que o produto promete; o CRM web continua para o celular e para a gestão. O risco é a regressão do que o CRM web já faz (anexos, áudio, @SemDor), por isso a fase 1 entra atrás da configuração, ao lado da moldura. |
| **C · Reescrever a caixa de entrada no Clinica e aposentar o CRM** | Igual ao B para quem atende. | É a opção "clonar" de verdade: refaz no Clinica o que o CRM já tem (webhooks da Meta, mídia, modelos aprovados, SMS, recall, IA, qualidade, operação) e perde o celular. Duas verdades de WhatsApp durante meses. | Caro e demorado; interrompe uma operação que está em produção na VPS 2. Rejeitado. |
| **D · Só a conversa, como painel lateral nas telas que já existem** (Agenda do dia, Ficha, Confirmações) | A secretária nem muda de tela: a conversa abre ao lado do horário que ela está olhando. | Tecnicamente é o B com a conversa extraída como componente (`PainelDaConversa`), sem a lista. Não vive sozinho: alguém precisa ver "quem escreveu e ninguém respondeu", e isso é a lista. | Melhor dia de quem atende. É a **fase 2 do B**, não uma alternativa a ele. |

**Recomendação: B, em três fases, com D como a segunda.** A moldura (A) fica como está até a
fase 1 do B provar o mesmo trabalho na clínica; depois sai inteira (regra 2: função que sai, sai
inteira, com `modo=recepcao` e `CRM_EMBED_ORIGINS` do CRM junto).

## 3. O mockup (interativo)

> Segunda versão, depois de a direção reprovar a primeira ("pode melhorar e muito") e pedir
> uma tela **boa e interativa**, já que ela será o WhatsApp inteiro dentro do sistema. A
> direção escolheu: **WhatsApp reconhecível, nos tokens da casa**, e os quatro fluxos
> funcionando (atender, agenda na conversa, modelos e janela de 24 h, sino e ficha em dúvida).

Feito sobre o CSS **real** do app, não sobre um desenho: `gerar.py` concatena `tokens/*.css`
(os mesmos do `Tokens.xaml`), `clinica.css`, `moldura.css`, `chat-equipe.css` e o
`conversas.css` proposto (só tokens), e embute o `app.js` (dados fictícios + renderização). Os
ícones seguem o traço do `Componentes/Icone.razor`. O que está nas fotos é o que a tela Blazor
desenharia com essas classes; o que acontece ao clicar é o que a tela faria chamando os
serviços.

```bash
python3 docs/mockups/whatsapp-nativo/gerar.py      # regenera index.html e artefato.html
# abra docs/mockups/whatsapp-nativo/index.html
#   #fechada  → a conversa fora da janela de 24 h (só modelo)     #ficha → "Qual ficha é esta pessoa?"
#   #nova     → novo contato sem ficha (buscar / cadastrar)        #vazia → nenhuma conversa escolhida
```

O que dá para FAZER no mockup, e o que cada clique prova:

| Clique | O que acontece | A regra que ele prova |
|---|---|---|
| Uma conversa na lista, ↑/↓ | Abre com a divisória "1 nova mensagem"; a ficha ao lado troca | A ficha vem do Clinica, não do CRM |
| **Assumir** / **Com você** | Passa a ser sua; "Minhas" conta; a faixa diz "Com você"; evento na conversa | Fila e responsável do CRM |
| **Transferir** | Formulário da casa: para quem e uma nota; a nota vira nota interna | Nota interna nunca vai ao paciente |
| **Encerrar** / **Reabrir** | Pergunta como terminou; a conversa vai para "Encerradas" com a faixa e o Reabrir | O resultado alimenta o resumo do dia do Gerente |
| Escrever + **Ctrl+Enter** | Balão verde com ✓ → ✓✓ → ✓✓ azul; a Maria responde e a lista reordena | Ticks de entrega da Meta; a lista viva |
| **Nota interna** | O bloco inteiro fica âmbar e diz "Só a equipe vê" | Dois modos, uma caixa |
| **Anexar** | Escolhe um arquivo real; vira chip no rascunho e no balão | Mídia pelo CRM |
| **Modelos** | Lista aprovada pela Meta; escolhido, o formulário preenche as variáveis com o contexto (nome, atendimento, dia) e mostra a prévia | Modelo é a única saída fora da janela |
| **Respostas rápidas** | Entram no campo para revisar, nunca saem sozinhas | Regra 3 do CRM: a secretária revisa e envia |
| Conversa do Carlos (`#fechada`) | O campo livre some: "Janela de 24 h fechada desde…", com **Escolher modelo** e **Nota interna** | A tela nunca mente sobre o que a Meta aceita |
| **Remarcar** (no aviso ou no horário) | Vagas da agenda; escolhida, o horário de hoje vira "Remarcado", nasce o novo, o evento entra na conversa, o sino limpa e a mensagem para a paciente já vem pronta para revisar | A regra é a da agenda do Clinica; o contato entra sozinho no Acompanhamento |
| **Confirmar** / **Não vem** / **Cancelar** | Mudam o selo do horário e da linha, o evento e a Confirmação de hoje; cancelar exige motivo | O mesmo vocabulário do balcão ("Não vem — remarcar", nunca "Confirmou") |
| **Sino** | A mesma lista do app (Não vem, Sem resposta, 2 fichas); o clique abre a conversa certa e limpa o ponto | Regra 3 da direção: o que pede ação vira aviso, e o aviso leva à porta |
| **2 fichas — conferir** | "Qual ficha é esta pessoa?" com nascimento, fim do documento e próxima consulta; ligada, a agenda e a confirmação do Pedro aparecem ao lado | Regra 5: o telefone não é identidade |
| Novo contato (`#nova`) | Busca de ficha pelo nome, ou cadastrar com o telefone preenchido | O CRM nunca cria ficha; quem cria é o app |
| **+** Nova conversa | A primeira mensagem tem de ser modelo (a pessoa ainda não escreveu) | A regra da Meta, dita antes de a pessoa errar |

| 1366 × 900 | 1024 × 768 |
|---|---|
| ![](mockups/whatsapp-nativo/01-conversas-1366.png) | ![](mockups/whatsapp-nativo/10-conversas-1024.png) |
| Depois de assumir, responder e a paciente responder: ![](mockups/whatsapp-nativo/02-enviou-e-respondeu-1366.png) | A ficha por cima da conversa (botão no cabeçalho): ![](mockups/whatsapp-nativo/11-ficha-1024.png) |
| Remarcar pelas vagas da agenda: ![](mockups/whatsapp-nativo/03-remarcar-1366.png) | Remarcado: o horário de hoje apagado, o novo em cima, a mensagem pronta: ![](mockups/whatsapp-nativo/04-remarcado-1366.png) |
| Modelo aprovado com as variáveis preenchidas pelo contexto: ![](mockups/whatsapp-nativo/05-modelo-1366.png) | Fora da janela de 24 h: ![](mockups/whatsapp-nativo/06-janela-fechada-1366.png) |
| "Qual ficha é esta pessoa?": ![](mockups/whatsapp-nativo/07-conferir-ficha-1366.png) | Novo contato, buscar a ficha: ![](mockups/whatsapp-nativo/08-sem-ficha-1366.png) |
| O sino que abre a conversa: ![](mockups/whatsapp-nativo/09-sino-1366.png) | Celular (a direção abre o mockup no telefone): ![](mockups/whatsapp-nativo/13-celular-conversa.png) |

Decisões de desenho, e por quê:

- **Sem cabeçalho de página.** A moldura já diz onde a pessoa está; cada linha de cabeçalho
  em 1024 × 768 é uma linha de conversa a menos. A tela ocupa o palco inteiro e cada coluna
  rola sozinha.
- **O verde só no que é do canal**: o selo do canal no avatar, o balão enviado
  (`--verde-100`), o "conectado" e o contador de não lidas. Ação primária continua no acento
  do app. O "papel de parede" é um pontilhado da cor da borda, sem imagem.
- **Balões agrupados**: mensagens seguidas do mesmo lado perdem o espaço entre si e o canto
  reto; a hora e os ticks ficam dentro do balão, como no WhatsApp.
- **O que pede ação é um aviso DENTRO da conversa**, acima do campo de escrever, com os botões
  da agenda. Ele some quando a ação é feita, e o evento fica na linha do tempo.
- **Nota interna muda a cor do bloco inteiro**, não só um chip: a diferença entre "vai ao
  paciente" e "só a equipe vê" não pode depender de ler um rótulo.
- **Em 1024 a ficha vira painel por cima**; no celular, um painel por vez com "voltar". E a
  bolinha do chat da equipe, que em 1024 caía sobre o Enviar, ganhou o canto: o rodapé do
  campo deixa o lugar dela.
- **Caret, seleção e barra de rolagem** seguem a paleta; o foco de teclado é o anel da casa.

Onde cada coisa vem:

- **Coluna 1 e 2** (do CRM: `GET /api/conversations`, `GET /api/conversations/{id}`,
  `POST …/reply`, `…/notes`, `…/claim`, `…/transfer`, `…/close`, `…/read`,
  `…/attachments`, `…/patient`, `GET /api/templates/{sender}`). O selo de cada linha vem da
  MESMA decisão do sino (`AvisosDeConfirmacao`), não de um segundo cálculo.
- **Coluna 3** (do próprio Clinica, pelos serviços que a Recepção já usa): cabeçalho da ficha
  (`FichaDoBalcao.Cabecalho`), telefone confere, carteirinha e vencimento, autorização de
  mensagens do termo do BSV, tratamento em curso; os horários com Confirmar / Remarcar /
  Cancelar (`AgendaService`); a confirmação de hoje (`AvisosDeConfirmacaoService`); o
  Acompanhamento (o contato continua sendo gravado pelo CRM via ponte, como hoje; a tela só
  mostra).
- **"Qual ficha é esta pessoa?"** e a busca de ficha do novo contato usam a busca do próprio
  app (`BuscaDePacienteNaTela`); só a LIGAÇÃO da ficha à conversa vai ao CRM.

## 4. Como se constrói (a fase 1)

Peças novas, e onde cada regra mora:

| Peça | Camada | O que faz | Teste |
|---|---|---|---|
| `IClienteDoCrm` | Application/Abstracoes | Listar conversas, abrir uma, responder, nota, assumir, transferir, encerrar, marcar lida, modelos, anexar, ligar ficha. Erros viram `RecusaDoCrm` (texto para a tela), nunca exceção solta no evento. | fake em memória para as telas |
| `ClienteDoCrmHttp` | Infrastructure | `HttpClient` com `CookieContainer` por sessão; entra com um bilhete (`IEntradaNoCrmService`) e **renova sozinho** quando o CRM responde 401 (o app pode emitir bilhete à vontade; a pessoa nunca vê "Entrar de novo"). Lê o `csrf` de `GET /api/session` e o manda em toda escrita. | contra um servidor falso (`WireMock`-like já existe para a ponte em `PonteClinicTeamTests`) |
| `ConversasNaTela` | Application/Modelos | PURO: o selo de cada linha, o texto da janela de 24 h, o que "pede ação" e qual botão oferece, a ordem da lista, a classificação da entrega. | `ConversasNaTelaTests` |
| `ConversasDoWhatsApp.razor` + `conversas.css` | Ui.Blazor | A tela do mockup. Escopo por operação, contador de geração, terceiro estado (`FalhaDeLeitura`: "o CRM não respondeu", distinto de "nenhuma conversa"), `Exigir(EditarAgenda)` nos botões que escrevem, `Dialogos` para confirmar transferir/encerrar, `Modal` para a ficha. | `TelasBlazorAbremTests` (semente `SementeConversasDoWhatsApp`), fotos em 1024 e 1366 |
| `ChaveWhatsAppCrm` | Modulo.Recepcao | O MESMO item de menu. Qual tela abre decide-se por configuração do Gerente (`ParametrosService`), para a fase 1 rodar ao lado da moldura na clínica. | `ModuloRecepcaoTests` |

Decisões já tomadas aqui, para não reabrir:

- **Atualização**: leitura a cada 7 s enquanto a aba está visível (é o que o CRM web faz hoje;
  `setInterval` de 7 s em `app.js`), com contador de geração e sem "Carregando" na releitura. SSE
  fica para quando o CRM oferecer; não é pré-requisito.
- **O que fica no CRM, sem discussão**: Meta e webhooks, modelos aprovados, janela de 24 h,
  mídia, SMS, recall, PARAR, @SemDor, qualidade, operação, o CRM web no celular.
- **O que a tela lê do Clinica, nunca do CRM**: ficha, agenda, confirmação, Acompanhamento,
  consultas a renovar. O `ClinicPatientCard` da ponte continua existindo para o CRM web.
- **O contrato da API do CRM passa a ser contrato**: hoje `/api/*` é interno do front do CRM.
  Antes da fase 1, o CRM documenta e versiona (`/api/v1/…` ou cabeçalho) o que a tela usa, e
  o `ClinicContracts.cs` ganha os records da resposta, idênticos nos dois repositórios como
  sempre.
- **O item do menu continua "WhatsApp"**: é a palavra de quem atende. O título da tela diz "da
  clínica" porque o número é o oficial, não o do computador.

### As fases

1. **A tela nativa, ao lado da moldura** (configuração do Gerente escolhe). Lista, conversa,
   contexto, ficha, modelos, anexos. Sem gravação de áudio e sem @SemDor na primeira versão
   (os dois continuam no CRM web).
2. **A conversa como painel** nas telas que hoje "navegam até a aba" (Agenda do dia, Ficha do
   balcão, Confirmações, Acompanhamento): o `PedidoDeConversaNoCrm` vira abrir o painel, sem
   trocar de tela. É o caminho D.
3. **A moldura sai inteira**: `WhatsAppNoCrm.razor`, `whatsapp-crm.css`, o `modo=recepcao`
   e `CRM_EMBED_ORIGINS` no CRM, a frase "continuam nas abas da Recepção". A entrada por
   bilhete FICA (é o que o cliente HTTP usa).

## 5. UX · DX · CX da recomendação

- 🧑‍💻 **UX**: uma tela da casa, no idioma da casa. A pergunta da secretária com o paciente no
  telefone ("ela vem hoje? tem horário terça? a carteirinha vale?") se responde na mesma
  janela, e a ação que a resposta pede (remarcar) é um botão ao lado da mensagem. Nenhuma
  senha, nenhum "Entrar de novo", nenhum cinza diferente. Em 1024 × 768 (o notebook mais
  apertado da clínica) a conversa e a lista cabem e o paciente abre por cima.
- 🛠️ **DX**: cinco peças novas, cada regra num lugar só (o que a tela afirma em
  `ConversasNaTela`, o que pede ação em `AvisosDeConfirmacao`, a agenda no `AgendaService`),
  um cliente HTTP testável contra servidor falso, e o CRM intocado por dentro. O custo real é o
  contrato da API, que hoje não existe como contrato. O `app.js` do mockup é o mapa do que a
  tela Blazor precisa (estados, ações, o que cada clique muda), e o `conversas.css` entra como
  está. Lição já escrita em `docs/agentes/convencoes.md`: classes de tela nova levam o prefixo
  da tela (`entrada`, `saida` e `dica` já eram do comum e o mockup colidiu com as três).
- 🤝 **CX**: nenhum servidor novo; o CRM em produção não para. A clínica recebe o que o produto
  prometeu ("a mesa de trabalho da secretária") em vez de duas mesas encostadas. O risco de
  regressão fica contido pela fase 1 ao lado da moldura: se algo faltar, a direção volta a
  moldura num campo de configuração, sem release.

## 6. O que NÃO foi feito, e por quê

- **Nenhuma linha de código do app**: a direção pediu o desenho e o caminho; o código vem
  depois do "sim", fase a fase, cada uma com o seu PR.
- **O contrato da API do CRM não foi congelado**: é trabalho do repositório `semdor-crm`, e
  é o primeiro PR da fase 1.
- **Gravação de áudio e @SemDor na tela nativa**: ficam no CRM web até a fase 1 estar na
  clínica; entrar com tudo de uma vez é o que faria a regressão que o CX acima evita.
- **Relacionamento do CRM no celular**: fora do escopo, continua como a direção decidiu.
