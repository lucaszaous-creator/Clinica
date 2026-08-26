# O termo que o PACIENTE assina

> Termos de consentimento do **BSV (Bloqueio Simpático Venoso)** assinados pelo paciente,
> incluindo a **declaração de jejum**. Pedido da cliente em ago/2026; é o que a My Smart
> Clinic vende como **SmartDocs** (+R$ 79/mês, ou incluso no plano ULTRA de R$ 389).
>
> ✅ **A Fase 1 está IMPLEMENTADA (parcela 66).** As decisões da clínica: assinatura em
> **tablet/touchscreen**, **modelos de texto configuráveis** (é o que resolve "quem escreve
> o termo" e "quais declarações entram" — as duas são da clínica, não nossas) e — na 3ª
> rodada — **o termo vale a partir da assinatura**: colhe-se quando o paciente aparece, sem
> esperar o dia do procedimento.
>
> A Fase 2 (link para assinar em casa) segue **não implementada**, e a seção 4 diz por quê.

## O que ficou pronto

| Onde | O quê |
|---|---|
| **Gerente → Configurações → "Escrever termos…"** | A clínica escreve o texto, as declarações e diz qual procedimento exige qual termo |
| **Recepção → Pacientes → ficha → aba Documentos** | Seção "Termo do procedimento — hoje", com o botão **"Colher assinatura…"** por termo. É a porta principal: a ficha já está aberta na frente do paciente |
| **Recepção → Fila → "⋯" do cartão** | "Colher o termo do procedimento…", e o cartão ganha o selo **"Termo pendente"** |
| **Consultório → Atendimento** | Faixa vermelha com o botão de colher, para quando o paciente já está na sala |
| **Check-in** | O alerta "falta o termo" chega sozinho pelo `ElegibilidadeService` — e daí ao agendamento, à ficha e ao Consultório |
| **Central de documentos** | Cartão "Termo de procedimento" → **Colher assinatura…**, com a escolha do termo; a segunda via sai da lista |
| **Gerente → Configurações → "Tela do paciente"** | Escolhe o monitor virado para quem assina, com o botão **Testar**. Ver §3.8 — inclusive o que comprar |

O termo **nasce numerado** e com código de conferência, como todo documento clínico —
é por eles que a clínica o acha depois.

⚠️ **A seção só aparece para quem tem `VerProntuario`.** O termo diz qual procedimento a
pessoa vai fazer e o que ela declarou sobre o próprio corpo — é dado de saúde (art. 5º, II),
e os perfis Financeiro e Faturista abrem esta mesma ficha. Ela **some**, não fica apagada:
"sem permissão" ao lado de "Termo do BSV" anunciaria que existe um BSV marcado para aquela
pessoa, que é justamente o que não se quer contar.

⚠️ **A central emite pelo caminho PRÓPRIO** (`ExigenciaFolha.TermoParaAssinar`): ela
pergunta qual termo é e abre a coleta no tablet. O que ela nunca faz é passar pela janela
genérica de documento — o texto e as declarações vêm de um MODELO, e emitir por lá daria um
papel numerado sem modelo de origem. A janela genérica **recusa** o tipo no construtor, para
a próxima porta não repetir o erro.

⚠️ **As quatro portas passam por `ColetaDeTermo.Abrir`** (no shell). Quatro montagens da
mesma janela — escopo, ViewModel, dono, recarga — divergiriam na primeira correção, e o que
elas colhem é a prova de que o paciente consentiu.

## 1. O que o pedido é, e o que ele não é

**Não é a nossa assinatura digital.** A que temos (parcelas 42 e 43) é do **PROFISSIONAL**:
e-CPF, ICP-Brasil, PAdES-B, para o documento valer perante a farmácia e o RH. A que a
cliente está pedindo é do **PACIENTE**, e é outra coisa em tudo — inclusive na lei.

**E o paciente NÃO precisa de certificado ICP-Brasil.** O termo de consentimento é
documento **entre as partes**, e a MP 2.200-2/2001 (art. 10, §2º) admite assinatura por
outro meio quando as partes o aceitam; a Lei 14.063/2020 classifica isso como assinatura
**simples**. Exigir e-CPF do paciente seria inviável e desnecessário.

⚠️ **O que dá valor a essa assinatura não é certificado: é EVIDÊNCIA.** Quem assinou, quando,
onde, diante de quem, e — o que mais importa numa contestação — **exatamente que texto ele
tinha na frente**. É isso que o desenho abaixo captura, e é isso que o rodapé do PDF pode
afirmar sem mentir.

## 2. A pergunta que decide o desenho: QUANDO o paciente assina?

O pedido chegou como um item só e são **dois documentos com naturezas opostas**:

| | Termo de consentimento do BSV | Declaração de jejum |
|---|---|---|
| O que afirma | "Fui informado dos riscos, alternativas e concordo com o procedimento" | "**Estou** em jejum de 8 horas" |
| Quando vale | A partir da assinatura | **Só hoje, minutos antes** |
| Onde faz sentido assinar | Quando o paciente aparece — na consulta em que tira dúvidas | **No balcão**, na hora |
| Assinado com antecedência | Continua valendo | **Não vale nada** |

> É essa linha que virou a caixinha `SoValeNoDiaDoProcedimento`: os dois convivem porque a
> exigência é por **modelo**, não por tipo. A clínica que quiser as duas coisas escreve o
> consentimento longo (sem prazo) e um termo curto só com o jejum (a cada sessão).

**A declaração de jejum assinada em casa na noite anterior é uma declaração sobre o
futuro** — e o valor dela é justamente ser sobre o **presente**. Isto derruba o SmartDocs
como resposta ao caso que a cliente trouxe: o link no celular resolve o primeiro documento
e é *inadequado* para o segundo, que é o que motivou o pedido.

**Conclusão que orienta tudo:** o caso BSV se resolve **inteiro** com assinatura no balcão,
que é barata e não exige infraestrutura nova. O link para assinar em casa é uma segunda
fase, opcional, e serve a outros documentos.

---

## 3. Fase 1 — a assinatura no balcão (resolve o BSV por inteiro)

O paciente do BSV **está fisicamente na clínica** minutos antes do procedimento. Ele assina
na tela — touchscreen, tablet, mouse ou mesa de assinatura (Wacom/Topaz) — e o WPF já traz
o `InkCanvas` de fábrica. **Não há dependência nova, servidor novo, custo por mensagem nem
internet obrigatória.**

### 3.1 O que entra no domínio

**Um tipo novo de documento**, e ele **não pode reaproveitar** o `Consentimento` que existe:
aquele é o **termo LGPD**, montado de `ConsentimentoService.Finalidades`, e não tem nada a
ver com risco de procedimento. Juntar os dois faria um documento responder duas perguntas
diferentes — o defeito do bit sobrecarregado da parcela 49, agora num papel.

```
TipoDocumentoClinico.TermoProcedimento   // novo valor, no FIM do enum
```

⚠️ Valor novo em `TipoDocumentoClinico` entra em `TipoDocumentoInfo.Todos`, que alimenta o
seletor de tipo de **todas** as telas de documento. Aqui isso é o objetivo — mas ele
**nasce fechado** até declarar acesso em `FolhaCatalogo` (`PermissaoVer` / `PermissaoEmitir`),
que é a regra da parcela 59: folha sem acesso declarado nasce aberta para todo mundo e
ninguém percebe até vazar.

**O texto do termo é MODELO, não código.** `ModeloDocumento` já existe com `Tipo`, `Nome`,
`Titulo` e `Corpo`: a clínica escreve o termo do BSV uma vez, e cada emissão **COPIA** —
a mesma regra do protocolo do mapa corporal e do modelo de evolução. Corrigir uma palavra
hoje não pode reescrever o que um paciente assinou no mês passado.

**As declarações são itens.** `ItemDocumento` já é usado assim no termo LGPD ("Autorizado" /
"Pendente"). Cada linha é uma afirmação que o paciente marca:

- Estou em jejum de 8 horas · **Sim / Não**
- Informei todos os medicamentos que uso · Sim / Não
- Informei minhas alergias · Sim / Não
- Não fiz uso de anticoagulante nas últimas 24h · Sim / Não

### 3.2 "Não" não impede — e isso é decisão

Marcar **Não** no jejum **não bloqueia a emissão do termo**. O documento existe para
registrar a verdade, e um paciente que chegou sem jejum é exatamente o fato que precisa
ficar escrito e assinado. Bloquear produziria o pior desfecho: ninguém emite o termo, o
procedimento acontece assim mesmo e não sobra registro nenhum.

O que o "Não" faz é **acender alerta VERMELHO na fila e no Consultório**, pelo
`ElegibilidadeService` — que é onde o projeto já pôs tudo o que "se resolve com o paciente
presente e fica caro depois". **A decisão de fazer ou adiar o BSV é do profissional, não do
software** — a regra 9 do compromisso de conformidade: não prometa garantia que o código
não dá.

### 3.3 A ordem das duas assinaturas é uma amarra técnica, não uma preferência

⚠️ **O PDF não se assina incrementalmente** — é a restrição que a parcela 42 já encontrou e
documentou. A assinatura ICP-Brasil sela uma faixa de bytes; carimbar o traço do paciente
depois **invalidaria** a assinatura do profissional.

Logo a ordem é obrigatória, e só uma funciona:

```
1. Emitir           → conteúdo COPIADO do modelo e gravado (fato imutável)
2. Paciente assina  → traço + evidência gravados no banco
3. Gerar o PDF      → o traço do paciente vai DENTRO dos bytes
4. Profissional assina com e-CPF → sela tudo, o traço do paciente incluído
5. Bytes guardados em ArquivoAssinado → a reimpressão devolve ESTES bytes
```

É o mesmo raciocínio da folha de infusão pelo avesso: lá as colunas de checagem saem **em
branco** porque quem as preenche assina no papel; aqui o traço do paciente precisa estar
**dentro** do arquivo antes do selo, porque quem sela é o profissional.

**O passo 4 é opcional.** Sem e-CPF, o PDF sai com o traço do paciente e a clínica arquiva
como sempre arquivou. Isso importa hoje: **a clínica ainda não comprou o e-CPF**, e o termo
do BSV não pode ficar esperando por ele.

### 3.4 Os campos novos

Um-para-um com o documento (um termo, uma assinatura de paciente), então moram na própria
linha — o mesmo argumento que o código já escreveu para a assinatura do profissional.
Migration **aditiva**:

| Campo | Por que existe |
|---|---|
| `PacienteAssinadoEm` | Data e hora da coleta |
| `PacienteAssinaturaMeio` | Balcão · mesa de assinatura · remoto (prepara a Fase 2) |
| `PacienteAssinaturaHash` | SHA-256 **do conteúdo que ele viu**. É a metade que responde "assinou o quê?" |
| `PacienteDocumentoConferido` | CPF/RG apresentado no ato — o que substitui o certificado |
| `ColhidoPorOperador` | Quem da clínica testemunhou. `SessaoUsuario.Atual.Operador`, **nunca** `Environment.UserName` |
| `PacienteAssinaturaRecusada` + motivo | Recusar é fato, e some sem registro se não houver onde escrevê-lo |

O **traço** (PNG, ~10 KB) vai em **tabela à parte**, como `ArquivoAssinado` e
`AnexoProntuario`: listagem de documentos não arrasta imagem.

### 3.5 O que o rodapé pode dizer — e o que não pode

Regra 9 do compromisso, e a mais antiga do projeto (o carimbo escaneado da parcela 3):
**garantia aparente é pior que ausência de garantia.**

✅ Pode: *"Assinatura eletrônica simples, colhida presencialmente em 12/08/2026 às 14:32,
diante de Ana Paula (recepção), com documento conferido. Conteúdo selado por SHA-256
`a3f9…`. Código de conferência 7K2P-9M4X."*

❌ Não pode: "assinatura digital", "assinado digitalmente", "com validade jurídica
ICP-Brasil" — nada disso é verdade sobre o traço do paciente.

### 3.6 Onde fica a porta

A lição das parcelas 39, 48 e 59: **alerta sem porta no mesmo app é pior que alerta
nenhum.** O termo do BSV precisa ser colhido **antes** do procedimento, então:

- **Recepção · Fila / check-in** — é onde o paciente está. Alerta "termo do BSV pendente"
  com o botão de colher **na mesma linha**.
- **Consultório · Meu dia** — quem faz o BSV precisa ver, ao chamar, se o termo está
  assinado. Só leitura e o botão; quem colhe é o balcão.
- **Central de documentos** e **ficha do paciente** — segunda via e histórico, pelas portas
  que já existem.

### 3.7 Como o sistema sabe que o termo é exigido

Pela **modalidade**: `ModalidadeAtendimento.BsvApenas` e `BsvComAcupuntura` já existem no
motor de regras. A configuração amarra modalidade → modelo de termo, com a distinção que a
seção 2 obriga:

| Validade | Para quê |
|---|---|
| `PorPeriodo(dias)` | O consentimento do procedimento — assinado uma vez, renovado a cada N dias |
| `PorSessao` | **A declaração de jejum** — vence sempre, exigida a cada sessão |

Sem essa separação, o sistema pediria o jejum uma vez por ano ou o consentimento toda
semana; os dois erros irritam e o primeiro é perigoso.

### 3.8 O aparelho onde o paciente assina — o modelo da MAQUININHA

> Pedido da cliente (ago/2026): *"há um dispositivo para assinar via touchscreen … quero
> saber como fazer para sempre que aparecer para o paciente assinar, o dispositivo ligar na
> hora aparecendo o lugar certo para ele assinar"*. A escolha dela foi **duas telas: ela
> controla, ele só assina**.

**O desenho.** São DUAS janelas, não uma espelhada:

| Tela da recepcionista | Tela do paciente |
|---|---|
| O texto do termo, as declarações com Sim/Não, o documento conferido, Confirmar e "Paciente recusou" | Só o texto GRANDE, as declarações como ela as marcou, e a área de assinar |

A tela dele abre **sozinha** quando o termo termina de carregar — não há botão de "enviar
para o tablet", porque um passo a mais é um passo que se esquece com o paciente esperando.
Ela nasce sem barra de título, sem botão de fechar e **por cima de tudo** (`Topmost`): quem
está do outro lado do balcão não pode fechar, mover nem alcançar o que está atrás. O que a
fecha é o fim da coleta, do lado de cá.

Enquanto o termo está na tela, o sistema **impede o monitor de dormir**
(`SetThreadExecutionState`): a proteção de tela do Windows não pode apagar o termo no meio
da leitura de alguém que lê devagar.

⚠️ **A área de assinar SOME da tela da recepcionista quando há a do paciente.** Deixar as
duas ativas permitiria ela assinar pelo paciente sem querer — e o termo diria que ele
assinou. No lugar dela fica a linha que explica que a área sumiu de propósito, porque "sumiu
o campo de assinar" se lê como defeito.

⚠️ **Sem segunda tela configurada, TUDO continua acontecendo numa janela só.** Não é um modo
degradado: é o modo de quem tem um monitor, e ele funciona por inteiro — a clínica pode
ficar meses sem comprar o touch e a feature não pode esperar por isso.

**A configuração** fica em **Gerente → Configurações → "Tela do paciente"**: um seletor de
monitor, **Salvar** e **Testar**. O Testar abre um exemplo (sem dado de paciente nenhum) na
tela escolhida — as telas do Windows se chamam `\\.\DISPLAY1` e `\\.\DISPLAY2`, e o único
jeito de saber qual é qual é ver a janela aparecer nela. Sem esse botão, o primeiro a
descobrir que o termo abriu no monitor errado seria o paciente, vendo o próprio nome e o
procedimento dele numa tela virada para a sala de espera.

⚠️ **O que se grava é o NOME do dispositivo, nunca a posição na lista.** O índice muda quando
alguém desliga um cabo ou o Windows reordena as telas depois de um reinício — e a tela do
paciente passaria a ser a da recepcionista, com o termo em tela cheia por cima do trabalho
dela, sem ninguém ter mexido em nada. Quando a tela gravada não está ligada, a configuração
**diz isso por escrito** e a coleta volta à janela única; silêncio faria a clínica concluir
que a feature quebrou quando o que houve foi um cabo solto.

#### O que comprar

O que o sistema precisa é de um **segundo monitor com toque, ligado ao mesmo PC do balcão** —
e não de um tablet. A diferença importa: um tablet Android/iPad é **outro computador**, e
para ele assinar seria preciso um servidor web, uma rede confiável no balcão e um caminho
para o traço voltar. Um monitor touch é apenas mais uma tela do Windows: o traço nasce dentro
do mesmo processo que grava o termo, e não há rede nenhuma entre a caneta e o banco.

**O que exigir na hora de comprar:**

| Item | O que pedir | Por quê |
|---|---|---|
| Tipo | Monitor **touchscreen capacitivo**, 10 pontos | O resistivo (mais barato) exige pressão e deforma o traço |
| Tamanho | **10" a 15,6"** | Menor que 10" não cabe a assinatura sem a pessoa apertar o traço |
| Conexão | **HDMI + USB** (o USB é o toque), ou **USB-C único** se o PC tiver a porta com DisplayPort | Dois cabos é o normal; o toque não vai pelo HDMI |
| Sistema | Compatível com Windows 10/11 — **HID padrão, sem driver** | Monitor que exige driver do fabricante é o que para de funcionar na próxima atualização |
| Base | Suporte que **incline**, ou base VESA + braço | O paciente assina sentado; monitor em pé força o pulso |
| Caneta | **Opcional.** O dedo funciona | Uma caneta capacitiva comum (R$ 20) ajuda quem tem a mão trêmula |

O que **não** é preciso: resolução alta (o termo é texto grande), alto-falante, webcam,
Windows embarcado, digitalizador Wacom/EMR. Um monitor touch USB de 13"–15,6" resolve, e é
o mesmo tipo que a farmácia e o cartório usam para o mesmo fim.

⚠️ **O que NÃO serve:** um tablet Android/iPad ligado por Wi-Fi (é outro computador —
precisaria da Fase 2), um "segundo monitor sem fio" (Miracast espelha a MESMA imagem, e aqui
as duas telas mostram coisas diferentes), e uma **mesa digitalizadora sem tela** (a pessoa
assinaria olhando para cima, e a assinatura sai diferente da que ela faz no papel).

Se a clínica já tem um monitor touch, **não precisa comprar nada**: basta ligá-lo,
estender a área de trabalho do Windows (não duplicar) e apontá-lo em Configurações.

---

## 4. Fase 2 — o link para assinar em casa (o SmartDocs de verdade)

⚠️ **Não é necessário para o BSV** e tem um custo que a Fase 1 não tem.

**O que temos:** `ArmazenamentoS3` + `PublicacaoDocumento` (parcela 53), validados ao vivo
contra o Cloudflare R2. **O que falta:** eles só sabem `PublicarAsync` e `RemoverAsync` —
**publicam arquivo, não recebem nada de volta.** Um S3 estático não aceita POST.

Então a Fase 2 exige **um componente novo que hoje não existe**: uma página web e um
endpoint que recebe o traço. É pequeno perto de um portal do paciente — uma página, sem
login, aberta por token —, mas é um **deployable novo**, com hospedagem, TLS, e uma
superfície de ataque que o desktop não tem (link com dado de saúde circulando por WhatsApp).

**Quando ela vale:** termo LGPD, contratos, anamnese pré-consulta — documentos que ganham
em ser lidos com calma, longe do balcão. **Quando não vale:** qualquer declaração sobre o
estado do paciente **hoje**.

## 5. As decisões que a clínica tomou (ago/2026)

1. **Fase 1 sozinha basta.** Cobre o BSV inteiro, não depende de e-CPF, não depende de
   internet e não cria deployable novo.
2. **Tablet/touchscreen.** `InkCanvas` de fábrica no WPF; a mesma tela serve para mesa de
   assinatura e mouse.
3. e 4. **Modelos de texto configuráveis.** As duas perguntas — quem escreve o termo, e
   quais declarações entram — deixam de ser decisão de código e viram tela: Configurações →
   "Escrever termos…". ⚠️ **Não há termo de fábrica**, e é decisão: um texto de
   consentimento embutido seria o sistema opinando sobre risco clínico. A lista nasce
   vazia e a tela diz o que fazer.
5. **Validade: vale a partir da assinatura** (revisto na 3ª rodada). A primeira versão
   amarrava tudo ao dia da sessão, com o argumento de que "regra com exceção que ninguém
   vai exercer é código a mais". **A cliente exerceu a exceção antes de a feature chegar à
   clínica** — e é ela quem sabe quando o paciente aparece. Hoje é uma caixinha por
   procedimento (`SoValeNoDiaDoProcedimento`), desmarcada por padrão; marcada, serve ao
   termo curto que pergunta o JEJUM, que é a única declaração que não sobrevive à
   antecedência.
   ⚠️ Seja qual for a escolha, **recusa e papel pendente contam só no DIA**: uma recusa de
   três semanas atrás não pode calar o pedido no dia do procedimento.

## 6. O que a parcela entregou

| Camada | O quê |
|---|---|
| Domínio | `TipoDocumentoClinico.TermoProcedimento`, campos de assinatura do paciente em `DocumentoClinico`, `TracoAssinatura`, `ExigenciaTermoProcedimento`, `Permissao.ColherAssinaturaPaciente` |
| Aplicação | `AssinaturaDoPacienteService` (colhe, recusa, sela, audita no mesmo `SaveChanges`), `TermoProcedimentoService` (configura e responde "falta assinar?"), `DocumentoClinicoService.EmitirTermoProcedimentoAsync` |
| Infra | Migration **aditiva** (9 colunas anuláveis + 2 tabelas) |
| PDF | Declarações com a resposta por extenso, traço desenhado no lugar da linha, rodapé de evidência |
| Telas | `AssinaturaPacienteWindow` (no **shell**), `ModelosTermoWindow` (Gerente), porta no "⋯" da fila |

**Permissão**: `ColherAssinaturaPaciente` vai para **Recepção, Profissional e Enfermagem** —
os três perfis que ficam na frente do paciente antes do procedimento. Deixar um de fora
faria o termo depender de a pessoa certa estar livre naquele minuto, e termo que atrasa o
procedimento é termo que a clínica aprende a pular.

### Os testes que fixam as regras

`TermoAssinadoPeloPacienteTests` — 19 casos. Os que mais importam, porque o defeito aqui
não faz barulho:

- **`Termo_assinado_ontem_nao_vale_para_a_sessao_de_hoje`** — se alguém trocar a chave por
  um prazo em dias, este cai.
- **`Traco_do_paciente_e_recusado_depois_da_assinatura_do_profissional`** — a ordem das
  duas assinaturas; colher depois produziria, em silêncio, um PDF cujo selo não fecha.
- **`Responder_nao_ao_jejum_grava_o_termo_e_marca_a_declaracao_negada`** — o "não" não
  impede, e vira alerta.
- **`Corrigir_o_modelo_nao_reescreve_o_termo_ja_assinado`** — aplicar COPIA (Lei
  13.787/2018).
- **`Selo_do_conteudo_acusa_quando_o_termo_e_alterado_depois_da_assinatura`** — guardar um
  hash que ninguém recalcula é guardar um número.
- **`Leitura_em_lote_da_fila_concorda_com_a_leitura_por_paciente`** — a fila lê 30 cartões
  em 3 consultas e a ficha lê um; duas definições de "falta assinar" divergiriam, e a que
  ninguém lembraria de ajustar é a do quadro, onde o erro aparece como **cartão limpo**,
  indistinguível de termo em dia.
- **`A_frase_do_rodape_nao_chama_o_traco_de_assinatura_digital`** — a regra do carimbo
  escaneado, da parcela 3.

### O que a revisão adversarial achou depois (2ª rodada)

Uma varredura em seis lentes sobre o diff pronto devolveu **20 achados**. Os que eram
defeito de verdade foram corrigidos nesta mesma parcela, e cada um virou teste:

| Defeito | Como falhava |
|---|---|
| **A seção da ficha vazava dado de saúde** | Financeiro e Faturista têm `VerFichaPaciente` e liam "Termo do BSV · BSV com acupuntura" |
| **Falha de leitura deixava as linhas do paciente ANTERIOR** | Um clique assinaria o termo de quem já saiu, em nome de quem está na frente |
| **O selo ICP-Brasil regerava o PDF SEM o traço** | O arquivo selado — que passa a ser o devolvido para sempre — sairia com a linha do paciente em branco |
| **Declaração em branco = termo cumprido** | Pular os rádios gravava "Assinado hoje" sem alerta: o procedimento aconteceria sem ninguém perguntar do jejum |
| **A central abria a janela genérica** | Papel numerado sem modelo de origem, com a pendência continuando acesa |
| **A fila colhia com o quadro em outro dia** | O termo nascia com a data de hoje e nunca casaria com aquele dia |
| **Salvar em Configurações pulava a seleção** | A gravação seguinte ia para o modelo errado, em silêncio |
| **A exigência era write-once** | A mensagem mandava "trocar o modelo" e não havia por onde — agora `ExigirAsync` TROCA |
| **Índice único inerte** | `NULL` é distinto de `NULL` no PostgreSQL: dois cliques criariam duas exigências. Família passou a gravar string VAZIA |
| **`ConteudoIntacto` sem chamador** | O hash era gravado e impresso e nada o recalculava — agora o rodapé denuncia o termo alterado |
| **Alerta em data futura** | Marcar um BSV para o mês que vem acendia vermelho impossível de atender |
| **Termo nascia sem profissional** | A via que fica 20 anos no prontuário saía com "Profissional responsável" no lugar do nome e do CRM |
| **`TemTermoPendente` calculado e nunca mostrado** | O cartão da fila ficava idêntico ao de quem já assinou |
| **O "⋯" apagado por `PodeEditarAgenda`** | A técnica de enfermagem tem o bit do ato e não conseguia colher |
| **Alerta no Consultório sem porta** | O médico lia "falta o termo" com o paciente na sala e tinha de descer ao balcão |
| **A janela do termo não registrava acesso** | Abrir e fechar sem assinar não deixava rastro — ponto 4 do compromisso LGPD |

### O que ficou de fora, e é sabido

- **A Fase 2** (link para assinar em casa) — seção 4.
- **`MeioAssinaturaPaciente.LinkRemoto`** existe no enum e **nenhum código o grava**. É
  deliberado: sem ele, o dia em que o link existir faria todo termo antigo parecer remoto.
- **Sala de infusão.** A permissão já vai para os três perfis e a janela mora no shell;
  falta a porta lá. Balcão, ficha e Consultório estão cobertos.

---

## 7. O que isto vale comercialmente

O SmartDocs deles custa **R$ 79/mês** avulso. Com a Fase 1 entregue, cobrimos o caso real
da clínica **sem** a dependência que eles têm — e a diferença que resta a favor deles
(assinar em casa) não serve para o documento que originou o pedido.

E há um ganho que eles não têm: **a nossa assinatura de paciente entra debaixo da assinatura
ICP-Brasil do profissional**, no mesmo arquivo, selada. No SmartDocs as duas coisas são
produtos separados.

---

## 8. O TERMO LGPD entra no mesmo circuito (parcela 89)

O pedido da direção foi curto e mudou uma decisão antiga:

> "quero que **TODOS** os documentos que precisam da assinatura do paciente também sejam
> enviados igual ao termo do BSV com o worker, a qual podemos enviar o link para o paciente
> assinar e retorna para nosso sistema com banco de dados salvos. Por exemplo, temos os
> Consentimentos (LGPD) no sistema de recepção: tratamento de dados pessoais e de saúde,
> compartilhamento com o convênio para faturamento, uso de imagem, mensagens de confirmação
> recall e campanhas."

### 8.1 O que faltava era o PORTÃO

Medido antes de escrever: **a coleta inteira já era genérica sobre `DocumentoClinico`**.
A janela, a segunda tela, o traço, a evidência, o selo do conteúdo, o envio pelo WhatsApp e
a volta pelo Worker olham `AguardaAssinaturaDoPaciente`, que responde por
`TipoDocumentoInfo.AssinadoPeloPaciente(tipo)` — e esse método listava **um** tipo.

Ou seja: pôr o termo LGPD no circuito custou **uma linha** de portão. Tudo o mais foi
consequência dela.

### 8.2 A inversão: o termo deixou de ser RECIBO e virou a FONTE

Até aqui o consentimento era uma **caixinha** que a recepcionista marcava na ficha
("Concedeu" / "Recusou", quatro pares de botões), e o termo impresso era o recibo do que já
estava no sistema.

⚠️ **A inversão não é preferência de leiaute — é a única forma de os dois não divergirem.**
Com o termo sendo recibo, o paciente podia responder **"Não"** ao marketing no celular e a
clínica continuar mandando campanha, porque a caixinha do balcão seguia marcada. Duas
verdades sobre o mesmo fato, e nada falha: a campanha simplesmente sai.

E havia o problema maior, que é jurídico: **a única prova do consentimento era a palavra de
quem clicou.** O art. 8º da LGPD pede manifestação do **titular**, e o §2º põe o ônus da
prova em quem trata o dado.

As três decisões da direção:

| Pergunta | Decisão |
|---|---|
| Termo e caixinha discordam — quem vence? | **A resposta assinada do paciente** |
| A caixinha continua existindo? | **Não.** O termo assinado é o único caminho |
| Um termo por finalidade ou um só? | **Um termo com as quatro declarações** |

⚠️ "Único caminho" **não** trava quem não tem celular: a coleta no balcão (traço na tela, ou
na segunda tela) continua sendo um caminho de assinatura. O que deixou de existir é o
consentimento **sem assinatura nenhuma**.

### 8.3 O vínculo é por CÓDIGO, nunca por ordem nem por rótulo

`ItemDocumento.Codigo` (coluna nova, migration aditiva) guarda o nome da
`FinalidadeConsentimento` de cada declaração.

- Casar por `Ordem` seria o **contrato de índice** que a parcela 41 trocou por nome:
  acrescentar uma finalidade no meio empurraria todas as outras, e o "Sim" do uso de imagem
  viraria autorização para compartilhar com o convênio — **sem quebrar build nenhum**.
- Casar pelo **rótulo** amarraria a decisão a um texto que a clínica pode reescrever.

O código é **copiado na emissão**, como todo o resto do documento: o termo assinado no mês
passado continua se lendo mesmo que uma finalidade seja renomeada hoje.

⚠️ Item sem código reconhecível é **ignorado, nunca adivinhado**. Ele existe em dois casos
legítimos — o termo emitido antes desta parcela, e uma finalidade que uma versão mais nova
do sistema conhece e esta não — e nos dois a resposta certa é não gravar consentimento
nenhum. Deduzir pela posição gravaria a autorização errada, que é pior do que não gravar.

### 8.4 O circuito de volta, e por que ele é o MESMO SaveChanges

`AssinaturaDoPacienteService.ColherAsync` traduz as declarações em `ConsentimentoLgpd`
**antes** do `SalvarAsync` do ato — ponto 7 do compromisso de conformidade. Senão existiria
um instante em que o termo está assinado e a clínica ainda manda campanha para quem acabou
de recusar.

Recusar o que estava vigente é uma **revogação**, registrada nos dois lugares: a linha antiga
ganha `RevogadoEm` (o consentimento de fato acabou naquele instante, e a linha continua
provando que existiu no período tratado) e uma linha nova grava a recusa. Só a linha nova
bastaria para o portão — `SituacaoAsync` lê a mais recente —, mas quem abre o histórico na
ficha veria uma autorização sem fim ao lado de uma recusa posterior e concluiria que ainda
vale.

⚠️ **Reler RASTREADO.** `ConsentimentosDoPacienteAsync` é `AsNoTracking` — ela existe para
LER —, e mutar o objeto que ela devolve não grava nada: a revogação sumiria em silêncio.

⚠️ **Uma definição só de "como se grava um consentimento".** São dois caminhos e um não pode
chamar o outro (o termo grava no mesmo `SaveChanges`; `RegistrarAsync` tem o `SalvarAsync`
dele), então o par *linha + auditoria* sai de `ConsentimentoService.Montar`. Duas montagens
divergiriam na ação de auditoria — que é justamente o nome pelo qual uma investigação
procura.

### 8.5 O PAPEL tinha de mudar junto

O desenho antigo do termo LGPD (`ListaFinalidades`) marcava um **X** quando a resposta era a
palavra `"Autorizado"` e escrevia **"Pendente"** no resto. Com as respostas em `"Sim"`/`"Não"`
ele imprimiria **toda finalidade como pendente**, e um "Não" sairia idêntico a uma pergunta
que ninguém respondeu.

Num papel que o paciente leva para provar **o que recusou**, isso é a garantia aparente que
este projeto recusa desde a parcela 3. O termo LGPD passou a usar o **mesmo desenho** do termo
de procedimento (`Declaracoes`): a resposta sai por extenso, e o "Não" sai destacado em
vermelho.

E `RespostaDeclaracao` **desceu da Application para o Domínio**, porque o termo LGPD precisa
ler a mesma resposta e o Domínio não enxerga a Application. Uma segunda cópia de "isto é um
sim?" divergiria na primeira correção.

### 8.6 As portas

| Onde | O que faz |
|---|---|
| **Recepção → ficha → aba LGPD** | Botão *Colher assinatura…*, com a situação do termo escrita ao lado |
| **Central de documentos** | O cartão "Termo de consentimento (LGPD)" **leva à coleta**, não emite papel em branco |

A janela é a **mesma** do termo de procedimento, e ela já oferece as duas formas: o traço na
tela do balcão (ou na segunda tela) e o envio do link pelo WhatsApp, que volta assinado do
celular. **Uma porta só**, porque quem escolhe a forma é quem está com a pessoa na frente.

⚠️ **O que SAIU**: os botões "Concedeu"/"Recusou" da ficha, e o botão "Termo de consentimento"
da aba Documentos (que emitia um papel montado do cadastro). O primeiro afirmava uma
manifestação que ninguém colheu; o segundo, agora, produziria um termo numerado e em branco
— e a leitura natural de um papel que saiu é que o consentimento foi colhido.

**REVOGAR fica**, e a assimetria é da lei: revogar é direito **unilateral** do titular
(art. 8º, §5º; art. 18) e a clínica é obrigada a atender de imediato — inclusive por
telefone, onde não há termo a assinar. Exigir assinatura para revogar dificultaria justamente
o lado que a LGPD manda facilitar.

### 8.7 O que a linha da finalidade passou a dizer

A situação de cada finalidade ganhou a **procedência**: `Concedido em 12/03 · termo 2026/0007`.
É o número do termo que responde *"onde está a prova?"* — sem ele a linha diria "Concedido em
12/03" e a auditoria continuaria tendo de acreditar na palavra de quem clicou, que é
exatamente o que a assinatura veio resolver. Registro anterior à parcela não tem termo, e a
frase simplesmente não afirma um que não existe.

### 8.8 O que ficou de fora, e é sabido

- **`ConsentimentoService.RegistrarAsync` sobreviveu** e não tem mais chamador de produção.
  Fica para o registro feito **fora** do termo (hoje, a montagem dos testes), com o aviso
  escrito no próprio método: quem for pendurar uma tela nele está reabrindo o caminho sem
  assinatura.
- **O termo LGPD não tem exigência por procedimento.** Ele é do paciente, não de uma sessão —
  não entra em `ExigenciaTermoProcedimento` nem no alerta do dia da fila.

### 8.9 O termo da versão ANTERIOR (2ª rodada — o defeito que a clínica encontrou)

A clínica colheu o consentimento e o alerta *"Sem consentimento LGPD de tratamento de
dados — colha no balcão"* continuou aceso. O papel resolveu o diagnóstico: a via saía com
o **rótulo** da finalidade ("Tratamento de dados pessoais e de saúde") e o detalhe **"Nunca
perguntado"** — que são exatamente o que a emissão ANTIGA escrevia — desenhados pelo
renderizador novo, com "Sim" à direita.

**O mecanismo.** Ligar o portão (`AssinadoPeloPaciente(Consentimento)`) fez, no mesmo
instante, todo termo LGPD **já emitido** pela versão anterior satisfazer
`AguardaAssinaturaDoPaciente`: não está cancelado, o paciente não assinou, não recusou. A
ficha ofereceu um deles como pendente, a coleta o **reaproveitou**, o paciente respondeu
"Sim" nas quatro declarações, o documento ficou selado e completo — e **nenhum
consentimento foi gravado**, porque os itens antigos não têm `Codigo`.

⚠️ **Nada falhou.** Build, testes e as três redes verdes; o papel saiu perfeito; e o alerta
continuou aceso. É a garantia aparente na forma mais discreta, e a mais cara: a clínica
acredita ter colhido.

**A correção tem duas metades, e uma sem a outra não resolve:**

| Metade | Onde | Por quê |
|---|---|---|
| A porta não **OFERECE** o termo antigo | ficha → `TermosLgpdComFinalidadeAsync` | senão o paciente assina e só então leva a recusa |
| `ColherAsync` **RECUSA** | `AssinaturaDoPacienteService` | senão a central, o link do WhatsApp ou uma tela futura reabrem o caminho |

E a tela deixou de **afirmar** "Assinado em 26/08" sobre um papel desses: header dizendo
que o termo foi assinado com o alerta "sem consentimento" aceso no balcão são duas verdades
sobre o mesmo fato — o defeito que esta parcela existe para acabar, cometido pela própria
correção dele. No lugar, ela diz o que houve: *"Há termo assinado de uma versão anterior do
sistema — ele não traz as respostas por finalidade e NÃO vale como manifestação do titular.
Colha um novo."*

⚠️ **`Enum.TryParse` aceita NÚMERO, e `Enum.IsDefined` não salva**: `"1"` vira uma
finalidade de verdade porque 1 É um valor definido. Quando o código guardado é o NOME, a
conferência é de **ida e volta** (`finalidade.ToString() == codigo`).

⚠️ **A pergunta "quais termos carregam finalidade" é consulta PRÓPRIA**, e não um `Include`
na leitura dos documentos da ficha: aquela alimenta a lista inteira, e puxar os itens de
todos arrastaria o `Desenho` dos relatórios de evolução — um mapa corporal por sessão — a
cada abertura de ficha. Decidir pela navegação `documento.Itens` ali seria a lição da
parcela 68 de novo: vazia em produção, cheia no teste pelo fixup do EF.

**O que a clínica precisa fazer com o que já assinou:** o termo que ela assinou no teste
não registra nada e não pode ser aproveitado — a coleta seguinte emite um novo, e é ele que
vale. O termo antigo continua na lista de documentos, como todo documento emitido (não se
apaga); se a clínica quiser tirá-lo da vista, cancela com motivo.

**A regra geral, para o próximo portão:** *ao alargar a condição que um dado satisfaz,
pergunte o que na BASE passa a satisfazê-la — e se o que passa é a mesma coisa.*
