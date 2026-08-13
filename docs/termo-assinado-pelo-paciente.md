# O termo que o PACIENTE assina

> Termos de consentimento do **BSV (Bloqueio Simpático Venoso)** assinados pelo paciente,
> incluindo a **declaração de jejum**. Pedido da cliente em ago/2026; é o que a My Smart
> Clinic vende como **SmartDocs** (+R$ 79/mês, ou incluso no plano ULTRA de R$ 389).
>
> ✅ **A Fase 1 está IMPLEMENTADA (parcela 66).** As decisões da clínica, tomadas antes da
> primeira linha: assinatura em **tablet/touchscreen**, **modelos de texto configuráveis**
> (é o que resolve "quem escreve o termo" e "quais declarações entram" — as duas são da
> clínica, não nossas) e validade **por sessão**.
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
| **Central de documentos** | O cartão **leva à ficha** em vez de emitir (ver abaixo); a segunda via sai da lista de documentos |

O termo **nasce numerado** e com código de conferência, como todo documento clínico —
é por eles que a clínica o acha depois.

⚠️ **A seção só aparece para quem tem `VerProntuario`.** O termo diz qual procedimento a
pessoa vai fazer e o que ela declarou sobre o próprio corpo — é dado de saúde (art. 5º, II),
e os perfis Financeiro e Faturista abrem esta mesma ficha. Ela **some**, não fica apagada:
"sem permissão" ao lado de "Termo do BSV" anunciaria que existe um BSV marcado para aquela
pessoa, que é justamente o que não se quer contar.

⚠️ **A central de documentos NÃO emite o termo** (`ExigenciaFolha.ProcedimentoDoDia`): o
cartão diz "Abrir a ficha" e navega, como o recibo faz com o Caixa. Emitir solto produziria
um papel numerado sem modelo de origem e sem declarações — a pendência do dia continuaria
acesa e a pessoa acreditaria ter resolvido. A janela genérica de documento **recusa** o tipo
no construtor, para a próxima porta não repetir o erro.

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
| Quando vale | Uma vez (ou renovado por período) | **Só hoje, minutos antes** |
| Onde faz sentido assinar | Em casa, com calma, lendo antes | **No balcão**, na hora |
| Assinado na véspera | Continua valendo | **Não vale nada** |

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
5. **Validade POR SESSÃO.** Não existe campo de prazo, e o `ExigenciaTermoProcedimento`
   explica por quê: regra com exceção que ninguém vai exercer é código a mais para manter
   e mais uma resposta possível para a mesma pergunta.

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
