# Entrega ao cliente

> Como os cinco apps chegam à clínica: qual instalador vai para qual posto, o que precisa
> estar pronto antes de qualquer entrega, e em que ordem quitar o que foi vendido.
>
> O catálogo do que cada módulo entrega está em
> [`features-por-modulo.md`](features-por-modulo.md).

## Onde estamos

**As vinte e cinco parcelas estão entregues.** A 3 fechou a Recepção, a 4 fechou o
Financeiro, a 5 tirou o Gerente Geral da condição de casca que só carregava os outros — e
da 12 em diante o Financeiro e a direção ganharam o que respondiam pela metade.

| Parcela | Estado |
|---|---|
| 0 — Instalável | ✅ entregue |
| 1 — Fundação | ✅ entregue |
| 2 — Cadastro e prontuário | ✅ entregue |
| 3 — Ato clínico | ✅ entregue |
| 4 — Dinheiro e insumo | ✅ entregue |
| 5 — Inteligência | ✅ entregue |
| 6 — Integração (o fio entre os módulos) | ✅ entregue |
| 7 a 11 — Moldura, CRM, taxas, Configurações e gráficos | ✅ entregues |
| 12 a 14 — Contas, fluxo de caixa e fechamento da gaveta | ✅ entregues |
| 15 a 17 — Regime tributário, recebíveis de cartão e custo de transação | ✅ entregues |
| 18 a 20 — Retenção, rentabilidade e tabela de preço por convênio | ✅ entregues |
| 21 a 24 — Auditoria, painel da direção, inadimplência e central de documentos | ✅ entregues |
| 25 — As capacidades que existiam sem porta | ✅ entregue |

> A parcela 5 saiu **fora da ordem** de propósito: ela não dependia da 3 nem da 4. O que
> ela precisava — `Profissional` (parcela 1) e o consentimento LGPD (parcela 2) — já
> estava pronto, e segurá-la só adiaria a única tela que a direção usa.

Das **14 features** vendidas na proposta: **as 14 completas.**

| Estado | Features |
|---|---|
| ✅ Completas | 01 painel · 02 agenda · 03 fila kanban · 04 pacientes 360º · 05 prontuário/EVA · 06 mapa corporal · 07 prescrição · 08 pacotes · 09 caixa e repasses · 10 estoque · 11 NPS/recall · 12 BI · 13 permissões/LGPD · 14 TISS |

> Completa quer dizer **entregue como o produto se propôs a fazer**. As duas ressalvas
> que continuam valendo estão em [Duas afirmações da proposta que ainda precisam de
> decisão](#duas-afirmações-da-proposta-que-ainda-precisam-de-decisão).

Dos **12 documentos impressos** da página 21 da proposta, **os 12 existem** — a página
está fechada.

### O que temos e o que falta, app por app

| App | O que já entrega | O que ainda falta |
|---|---|---|
| **Faturamento** | O ciclo da guia: motor de regras, 2º código, pendências, baixa, lotes TISS, glosa e recurso, PDFs. Login com permissões granulares (parcela 45). **Em produção.** | Nada. Descongelado na parcela 45, a pedido da cliente; o lançamento de atendimento e as consultas saíram para a Recepção na 46 |
| **Recepção** | Painel do dia, agenda multiprofissional com encaixe, lista de espera que diz **quem chamar** quando um horário vaga, fila em kanban, profissionais e salas, Pacientes 360º com foto e LGPD, prontuário com EVA e anexos, mapa corporal com protocolo, os 7 documentos clínicos e a central com as 9 folhas (incluindo a conferência pelo código impresso) | Nada da proposta |
| **Financeiro** | Caixa do mês, lançamento manual, conciliação com o faturamento, produção do período, contas a pagar/receber com conta fixa, fluxo de caixa mês a mês, fechamento da gaveta, recebíveis de cartão, taxas e regime tributário, "quem me deve", pacotes com saldo e devolução de sessão, estoque com alerta e **custo por sessão**, repasse por profissional, plano de contas, recibo e orçamento | Nada da proposta |
| **Gerente Geral** | Carrega Recepção + Financeiro inteiros **e** as telas da direção: painel de abertura com os alertas do dia, indicadores (ocupação, no-show, produtividade), custo de taxas e impostos, rentabilidade e tabela de preço por convênio, auditoria, campanhas (confirmação, NPS e recall), acessos com perfis e permissões, e a visão de leitura do faturamento | Nada da proposta |

### O que falta

Nada da proposta comercial. O que existe daqui em diante é evolução, não entrega
pendente — e as duas ressalvas registradas logo abaixo, que são decisão comercial.

A lista da evolução levantada no código (agendamento em série, apuração mensal por
tributo, metas, LGPD além do consentimento e conciliação bancária) está no fim de
[`features-por-modulo.md`](features-por-modulo.md#o-que-ainda-não-existe). **Nenhuma delas
foi vendida** — estão registradas para não serem redescobertas do zero.

### Duas afirmações da proposta que ainda precisam de decisão

1. **Página 24 — "Dois apps, um banco".** São **quatro**, um por perfil.
2. **Feature 07 — "assinatura e carimbo digitais".** O que existe é o carimbo do
   profissional (nome e registro no conselho), a linha de assinatura e um código de
   conferência que acha o documento no sistema. **Não** há certificado ICP-Brasil —
   chamar isso de assinatura digital seria mentir sobre o que a via garante. Validade
   jurídica de assinatura eletrônica é escopo novo.

A **página 23** ("Prontuário com mapa corporal e EVA") deixou de ser divergência: a EVA
saiu na parcela 2 e o mapa corporal saiu na 3 — a afirmação agora é inteiramente
verdadeira.

E uma terceira, que a parcela 5 criou ao entregar a feature 02: **"confirmação automática
por WhatsApp"**. O que ficou automático é a RODADA — o sistema descobre quem confirmar,
escreve a mensagem, respeita a LGPD e não repete ninguém. O **disparo é um clique por
paciente**, de propósito: o número é o WhatsApp da clínica, e envio em massa automatizado
por ali leva ao bloqueio do número. Se o cliente entendeu "automática" como "sozinho, sem
ninguém clicar", a alternativa real é contratar a API oficial do WhatsApp Business — o que
é decisão comercial, não técnica, e precisa ser dita antes de virar cobrança.

## Topologia: cinco apps, um por perfil

Cada posto de trabalho instala **só o app do seu perfil**. Todos falam com o mesmo
PostgreSQL — não há comunicação entre eles.

| Posto | App | Instalador | O que faz |
|---|---|---|---|
| Faturista | Faturamento | `Clinica.Faturamento-win-Setup.exe` | Pendências, guias, lotes TISS, glosas |
| Balcão | Recepção | `Clinica.Recepcao-recepcao-Setup.exe` | Agenda, fila, cadastro, prontuário |
| Consultório (médico, fisioterapeuta) | Consultório | `Clinica.Clinico-clinico-Setup.exe` | Meu dia (kanban com chamada de paciente), atendimento, evolução da dor, escalas por especialidade, prescrições com assinatura ICP-Brasil, folha de infusão e sala de infusão |
| Administrativo | Financeiro | `Clinica.Financeiro-financeiro-Setup.exe` | Caixa, pacotes, estoque |
| Direção | Gerente Geral | `Clinica.Gerente-gerente-Setup.exe` | Todos os módulos + BI, NPS e permissões |

> São **cinco** — a proposta comercial (página 24) diz *"Dois apps, um banco"*. Ver
> [Duas afirmações da proposta que ainda precisam de decisão](#duas-afirmações-da-proposta-que-ainda-precisam-de-decisão).

O Gerente Geral **carrega os módulos dos outros** — quem o instala não precisa da Recepção,
do Consultório nem do Financeiro na mesma máquina.

## A conexão — resolvido na parcela 0

Até a parcela 0, Recepção, Financeiro e Gerente só subiam se o Faturamento estivesse
instalado na mesma máquina: liam a connection string da pasta dele. Com um app por posto
isso era inviável — o balcão não tem faturamento.

Agora **cada app tem tela de setup própria** (`Clinica.Desktop.Shell/Shell/SetupWindow`):
no primeiro acesso a clínica cola a connection string (ou a URI da Neon), testa e salva.
Salvar só libera depois de o teste passar — conexão que não abre, gravada, transforma o
próximo erro em mistério.

A configuração fica criptografada por usuário do Windows (DPAPI) em
`%APPDATA%\ClinicaSemDor` e **vale para todos os apps da suíte** naquela máquina: configura
uma vez, os outros aproveitam. Se o Faturamento já estiver instalado ali, a dele continua
sendo lida como alternativa e a tela nem aparece.

Se a conexão falhar depois (senha trocada, servidor mudou), o app oferece reconfigurar
em vez de só mostrar o erro e fechar.

## Release e versão por app — entregue na parcela 0

Com cinco apps evoluindo em ritmos diferentes, a release conjunta obrigava a **republicar
o faturamento por mudança que não era dele** — download e reinício reais numa máquina em
produção, sem nenhum ganho para quem opera. Cada app tem agora sua tag, sua versão e sua
release.

| App | packId | Canal | Publicar com |
|---|---|---|---|
| Faturamento | `Clinica.Faturamento` | `win` | `git tag v1.2.3` |
| Recepção | `Clinica.Recepcao` | `recepcao` | `git tag recepcao-v1.0.0` |
| Financeiro | `Clinica.Financeiro` | `financeiro` | `git tag financeiro-v1.0.0` |
| Gerente Geral | `Clinica.Gerente` | `gerente` | `git tag gerente-v1.0.0` |

Ou pela aba **Actions → "Release" → Run workflow**, escolhendo o app e a versão.
O faturamento **não** recebe `--channel` no `vpk`: ele fica no canal padrão, e passar o
parâmetro mudaria o nome do feed e quebraria o auto-update já instalado.

**`packId` e canal do faturamento não mudam nunca** — mexer neles faz as instalações
existentes perderem o canal de auto-update e pararem de atualizar.

Funciona porque o `GetReleaseFeed` do Velopack percorre as releases e pula as que não têm o
`releases.<canal>.json` do canal pedido, em vez de olhar só a mais recente.

### A contrapartida: migration só aditiva, para sempre

Com versões diferentes em campo por padrão, **conviver com elas deixa de ser exceção**.
Coluna nova o EF do app antigo ignora sem problema; **renomear ou remover algo que o
faturamento usa derruba a clínica**. Não há exceção a essa regra enquanto houver mais de um
app instalado.

## A fundação — resolvido na parcela 1

O agendamento não sabia **com quem** nem **onde** — não existia `Profissional` nem `Sala`.
Sem isso a agenda só podia ser uma lista única (dois consultórios não cabiam na mesma
tela), o prontuário não teria como dizer quem atendeu, e repasse, produtividade e perfis
de acesso ficavam todos parados atrás do mesmo buraco.

A parcela 1 cria `Profissional`, `Sala` e `ListaEspera`, e dá ao agendamento os campos
que faltavam. Com isso a Recepção passa a ter:

| Tela | O que resolve |
|---|---|
| **Painel** | O dia visto do balcão: quem chegou, quem espera, ocupação por profissional, taxa de falta — e as guias pendentes **dos pacientes de hoje** |
| **Agenda** | Uma coluna por profissional, com sala, duração, **encaixe** e a **lista de espera** ao lado |
| **Fila de hoje** | Kanban Aguardando → Chegou → Em atendimento → Finalizado, com tempo de espera à vista |
| **Profissionais e salas** | O cadastro que destrava tudo o mais |

Três decisões que valem registrar:

- **O choque de horário é por intervalo e por recurso.** Marcar 14h30 sobre uma sessão de
  30 min que começou às 14h é o mesmo choque; o que colide é o profissional ou a sala
  (respeitando a capacidade dela — sala com duas macas comporta dois). A agenda **recusa**
  e a recepção pode **assumir o encaixe**, que fica registrado.
- **O kanban não inventou status.** As colunas saem de dois carimbos de hora novos
  (`ChegadaEm`, `InicioAtendimentoEm`); o faturamento continua vendo o `StatusAgendamento`
  de sempre. "Concluir" é o antigo check-in e fica no fim do fluxo: a guia nasce quando a
  sessão de fato aconteceu.
- **Quem não informa profissional nem sala não é barrado.** É exatamente o caminho do
  faturamento: ele avisa na tela e marca assim mesmo, como sempre fez.

A migration é **puramente aditiva** (tabelas novas e colunas novas anuláveis), como manda
a regra de conviver com versões diferentes em campo.

## Cadastro e prontuário — resolvido na parcela 2

A parcela 2 fecha a **Fase 1 da proposta**. A Recepção ganha duas coisas que não existiam
em lugar nenhum do sistema:

**Pacientes 360º.** Lista à esquerda, ficha à direita — o balcão trabalha com o paciente
na frente, e mandar navegar para outra seção custaria um clique e o contexto a cada
atendimento. A ficha reúne cadastro, foto (tirada ali, pela webcam do balcão), histórico
de sessões e guias, consentimentos LGPD e o prontuário.

**Prontuário com escala EVA.** Cada sessão registra a dor **antes e depois**, mais queixa,
conduta, evolução e orientações, com anexos (fotos da região, laudos, exames).

Quatro decisões que valem registrar:

- **A elegibilidade passou para o balcão.** Carteirinha vencida e cota de sessões
  estourada só apareciam na hora de faturar — quando a sessão já aconteceu e a glosa é
  certa. A ficha agora responde "pode ser atendido hoje?" antes do atendimento. Ela
  **informa, nunca impede**: quem decide é a clínica.
- **A EVA vale em par.** Medir só antes não diz se o tratamento funcionou. A evolução da
  dor só conta as sessões com as duas medidas, e a tela diz quantas são — o número nunca
  parece mais firme do que é.
- **Consentimento LGPD é fato datado, não interruptor.** Conceder, recusar e revogar
  criam registros novos; revogar **não apaga** o anterior, que continua provando o
  consentimento do período em que os dados foram tratados. É isso que a lei pede.
- **A webcam mudou de lugar.** `CameraServico` e `Retrato` foram copiados para o shell da
  suíte: a câmera está no balcão, e o shell não pode referenciar o executável congelado
  do faturamento. Mesmo débito de duplicação do design system — e as constantes de
  tamanho da foto precisam continuar iguais nos dois, porque gravam na mesma tabela.

A migration continua **puramente aditiva**: só tabelas novas (`Evolucoes`,
`AnexosProntuario`, `Consentimentos`).

## Ato clínico — resolvido na parcela 3

A parcela 3 fecha a Recepção. Ela entrega o que a proposta vendeu como **mapa corporal**
(feature 06), **prescrição** (feature 07) e os **sete documentos impressos** da página 21
que saem do balcão.

**Mapa corporal.** Duas figuras — frente e costas — em que o profissional clica para
marcar onde aplicou, com a técnica (agulha, eletro, moxa, ventosa, aurículo, laser). O
mapa é 1:1 com a sessão do prontuário: é a mesma sessão vista de outro jeito, e some com
ela. Um conjunto de pontos pode ser guardado como **protocolo** — da clínica ("Lombalgia
— padrão", vale para todo mundo) ou do paciente ("o esquema da dona Maria") — e
reaplicado nas próximas sessões; e há o atalho que resolve o dia a dia: **repetir o mapa
da sessão anterior**.

**Documentos clínicos.** Um só registro numerado por ano (`2026/0001`) com código de
conferência impresso no rodapé. Quatro são escritos pelo profissional (receita, atestado,
declaração de comparecimento, pedido de exame), com **modelos** reutilizáveis; três o
sistema monta do que já tem (relatório de evolução com a EVA, termo de consentimento
LGPD, anamnese).

Quatro decisões que valem registrar:

- **Aplicar um protocolo é copiar pontos, nunca apontar para ele.** Se fosse referência,
  corrigir um ponto hoje reescreveria o protocolo da clínica — e, pior, a sessão da
  semana passada. Prontuário é registro do que aconteceu.
- **Documento emitido é fato.** Não se apaga nem se reescreve: cancela-se com motivo e
  emite-se outro. A linha cancelada continua na ficha, porque a via em papel não some
  por ser apagada do sistema. É a mesma lógica do consentimento revogado da parcela 2.
- **A segunda via sai igual à primeira.** O conteúdo é gravado na emissão, não remontado
  na hora de imprimir — inclusive nos documentos montados do prontuário. Um relatório
  reimpresso em dezembro não pode "crescer" porque houve sessões em novembro.
- **O CID só entra no atestado com autorização expressa do paciente.** O campo fica
  gravado (é dado clínico), mas não vai para o papel sem a autorização — e a tela avisa
  antes, para ninguém entregar o documento achando que o diagnóstico foi junto.

A migration é **puramente aditiva**: só tabelas novas (`MapasCorporais`, `PontosMapa`,
`ProtocolosCorporais`, `PontosProtocolo`, `DocumentosClinicos`, `ItensDocumento`,
`ModelosDocumento`, `ItensModelo`).

## Dinheiro e insumo — resolvido na parcela 4

A parcela 4 fecha o Financeiro: **pacotes** (feature 08), **repasse por profissional** — a
metade que faltava da feature 09 —, **estoque** (feature 10) e os dois documentos que
faltavam da página 21, **recibo e orçamento**.

**Pacotes, planos e vouchers.** A clínica cadastra o que vende (catálogo com preço,
sessões e validade), vende ao paciente e o saldo passa a ser cobrado sozinho: ao concluir
a sessão na Recepção, o pacote **que vence primeiro** é debitado automaticamente. Plano
sem número de sessões é livre dentro da validade; voucher é o crédito avulso.

**Repasse.** Regra por profissional (percentual da receita ou valor por atendimento), com
vigência. A tela mostra o cálculo antes de fechar — quantos atendimentos, quanta receita
entrou, qual regra — e a apuração cria a saída prevista no caixa.

**Estoque.** Entrada com lote e validade, baixa por sessão, perda com motivo. Alerta de
reposição e de vencimento, custo médio e custo por atendimento.

Cinco decisões que valem registrar:

- **A venda copia o catálogo.** Mudar o preço de tabela em novembro não pode reescrever o
  que o paciente comprou em março.
- **A situação do pacote é calculada, não guardada.** Um pacote gravado como "Ativo"
  viraria mentira à meia-noite do vencimento, e ninguém roda tarefa noturna aqui.
- **O saldo do estoque é a soma dos movimentos**, nunca um total guardado — que é como o
  estoque para de bater. E saída maior que o saldo é recusada: estoque negativo não
  existe no mundo.
- **O repasse incide sobre a receita que ENTROU**, não sobre o que foi faturado; pagar
  percentual de dinheiro que ainda não chegou descapitaliza a clínica no mês em que o
  convênio atrasa. E **apurar trava o período**, porque repasse pago duas vezes é dinheiro
  que não volta.
- **Recibo e orçamento seguem a regra dos documentos clínicos**: não se apagam, cancelam-se
  com motivo, e os valores ficam gravados na emissão — a segunda via de um recibo de
  R$ 300 não pode sair R$ 350 porque a tabela subiu.

A migration é **puramente aditiva**: só tabelas novas (`PacotesCatalogo`,
`PacotesPaciente`, `ConsumosPacote`, `RegrasRepasse`, `RepassesApurados`, `ItensEstoque`,
`MovimentosEstoque`, `DocumentosFinanceiros`, `ItensDocumentoFinanceiro`).

## Inteligência e acesso — resolvido na parcela 5

Até aqui o Gerente Geral era uma casca: carregava Recepção e Financeiro e não tinha nada
de seu. A parcela 5 dá a ele as quatro telas que só fazem sentido para a direção — e, de
quebra, fecha a última metade da feature 02 e a feature 13 inteira.

| Tela | O que responde |
|---|---|
| **Indicadores** | Como a clínica está: ocupação, no-show, produtividade por profissional, NPS e a evolução mês a mês |
| **Faturamento** | Estamos perdendo faturamento, e onde? Taxa de baixa, glosa, envelhecimento das pendências e quebra por convênio — **só leitura** |
| **Campanhas** | O que estamos fazendo a respeito: confirmação da sessão, pesquisa de satisfação e recall de quem sumiu |
| **Acessos** | Quem entra na suíte, com qual perfil e o que cada um pode fazer |

Cinco decisões que valem registrar:

- **O Gerente lê o faturamento; não o opera.** Com a Fase 4 cancelada, a alternativa não
  foi herdar as telas do app congelado, e sim uma tela própria sobre os MESMOS serviços
  compartilhados. Sendo leitura, não existe risco de duas máquinas disputarem a mesma
  guia — e o app em produção segue intocado.
- **"Automática" é a rodada, não o disparo.** O sistema descobre quem contatar, escreve a
  mensagem, aplica a LGPD e não repete ninguém; o envio continua sendo um clique por
  paciente. O número é o WhatsApp da clínica, e disparo em massa automatizado por ali
  termina com o número bloqueado — perder o canal para economizar cliques é mau negócio.
  Isso está registrado como divergência da proposta em `features-por-modulo.md`.
- **Confirmar a sessão não é marketing; NPS e recall são.** Avisar o paciente sobre o
  horário que ele mesmo pediu é transacional e não exige consentimento. As outras duas só
  saem para quem consentiu — e quem não consentiu **aparece contado** no resultado da
  rodada, para a clínica ir colher o consentimento no balcão, em vez de sumir da lista.
- **Número sem base de cálculo aparece como "—", nunca como zero.** Ocupação sem agenda,
  NPS sem resposta e queda de dor sem par de medidas devolvem "não medido". É a mesma
  regra do painel de pendências: falha nunca pode ser exibida como sucesso.
- **Base sem usuário abre o "primeiro acesso" em vez de trancar a porta.** Recepção,
  Financeiro e Gerente passaram a pedir login; se não há ninguém cadastrado, a tela
  oferece criar o primeiro usuário (Gerente). O **Faturamento entrou nessa lista na
  parcela 45**, a pedido da cliente: sem sessão, a trilha de auditoria dele assinava toda
  baixa com o usuário do Windows — o mesmo nome para as duas pessoas que dividem o balcão.

A migration (`20260727220000_CampanhasEAcesso`) é **puramente aditiva**: só as tabelas
novas `ContatosCampanha` e `Usuarios`.

## Os cinco são instaláveis

Desde a parcela 0, **os apps da suíte instalam e rodam sozinhos** — nenhum depende de o
Faturamento estar na mesma máquina. O que varia é quanto cada um já entrega de conteúdo:
o inventário está em [Onde estamos](#onde-estamos), e o detalhe feature a feature em
[`features-por-modulo.md`](features-por-modulo.md).

## As parcelas

| Parcela | Módulo | Entrega | Destrava |
|---|---|---|---|
| ~~**0 — Instalável**~~ ✅ | Todos | Tela de setup própria da suíte; release e versão por app | Instalar qualquer app **sem** o Faturamento na máquina |
| ~~**1 — Fundação**~~ ✅ | Recepção | `Profissional` + `Sala`; agenda multiprofissional com encaixe e lista de espera; fila em kanban; painel próprio | Features 01 e 03 entregues, 02 sem a confirmação automática — e destrava 05, 09, 12 e 13 |
| ~~**2 — Cadastro e prontuário**~~ ✅ | Recepção | Pacientes 360º com consentimento LGPD; prontuário com evolução e escala EVA | Features 04 e 05 — **fecha a Fase 1 da proposta** |
| ~~**3 — Ato clínico**~~ ✅ | Recepção | Mapa corporal com protocolo reutilizável; prescrição com modelos; os 7 documentos clínicos | Features 06 e 07, e 7 dos 12 documentos da página 21 — **fecha a Recepção** |
| ~~**5 — Inteligência**~~ ✅ | Gerente | BI (ocupação, no-show, produtividade); campanhas de confirmação, NPS e recall; perfis, permissões e login; visão de leitura do faturamento | Features 11, 12 e 13, e a metade que faltava da 02 |
| ~~**4 — Dinheiro e insumo**~~ ✅ | Financeiro | Pacotes/vouchers com saldo e baixa automática; repasse por profissional; estoque com validade e custo; plano de contas; recibo e orçamento | Features 08, 09 e 10, e os 2 documentos que faltavam da página 21 — **fecha o Financeiro** |
| ~~**6 — Integração**~~ ✅ | Recepção → todos | Concluir a sessão passa a debitar o pacote, baixar o insumo e lançar o caixa | O fio entre os módulos: os serviços existiam e **ninguém os chamava** |
| ~~**7 a 11**~~ ✅ | Todos | Moldura da suíte (sidebar por tema, busca, breadcrumb), prontuário/prescrições/CRM na sidebar, taxas e impostos, Configurações fora do app congelado, gráficos e exportação CSV | Nasceram da comparação do cliente entre os mockups e o sistema rodando |
| ~~**12 a 17**~~ ✅ | Financeiro | Contas a pagar/receber com conta fixa; fluxo de caixa mês a mês; fechamento da gaveta; regime tributário; recebíveis de cartão; custo de transação na direção | O módulo sabia o que aconteceu, não *quando* — e nada sobre o dinheiro FÍSICO |
| ~~**18 a 20**~~ ✅ | Gerente + Financeiro | Retenção na fonte por convênio; rentabilidade por operadora; tabela de preço cadastrada na direção e usada no balcão | O encontro do que o faturamento produz com o que o financeiro recebe |
| ~~**21 a 24**~~ ✅ | Gerente + Recepção | Auditoria (a trilha que ninguém lia); painel da direção; inadimplência; central das 9 folhas | Quatro dados que o sistema gravava e nenhuma tela mostrava |
| ~~**25 — Capacidades sem porta**~~ ✅ | Todos | Custo por sessão; devolver sessão ao pacote; desfazer confirmação de depósito; quem chamar da lista de espera; conferir documento pelo código; apagar modelo e protocolo | Seis serviços prontos e testados que **nenhuma tela chamava** — nenhuma feature nova |
| ~~**26 — A Recepção no balcão**~~ ✅ | Recepção | Elegibilidade ao marcar e no check-in; rodada de confirmação com porta na Recepção; bloqueio de agenda (férias, feriado, folga); agendamento em série; visão de semana; direitos do titular (exportar e anonimizar) | Seis capacidades que existiam **em outro lugar** — nenhuma no ponto onde a decisão é tomada |
| ~~**27 — Os módulos se falam nos dois sentidos**~~ ✅ | Faturamento ↔ Financeiro ↔ Recepção | A glosa derruba a receita que já estava contada; guia glosada marcada na conciliação; conta vencida e guia glosada chegam ao balcão; alerta na direção | Todas as pontes iam **para a frente** — o convênio recusava a guia e ninguém no dinheiro ficava sabendo |
| ~~**28 a 32 — A rodada noturna**~~ ✅ | Gerente, Financeiro e Recepção | Metas e apuração por tributo; folha do dia, comprovante, aniversariantes, padrão de falta, busca no prontuário e remarcação em lote; inventário, lista de compras, teto de gasto, resultado do mês e extratos em CSV; quem parou de vir | 30 melhorias em cinco lotes — nenhuma tocou o faturamento congelado |
| ~~**36 — O consultório**~~ ✅ | Consultório (novo app) | Quinto executável, na máquina de quem atende: Meu dia com as sessões sem evolução escrita, atendimento com EVA e as últimas sessões abertas ao lado, painel de evolução da dor com as duas curvas, escalas por especialidade (PHQ-9, GAD-7, Oswestry, Katz, FINDRISC) e a carteira de pacientes | O sistema tinha agenda, caixa e BI, e **nada para quem atende** — a evolução ficava "para depois" e ninguém cobrava |
| ~~**37 a 43 — O lado clínico inteiro**~~ ✅ | Consultório + Recepção | Medidas seriadas e lista de problemas com alergia; kanban com chamada de paciente atravessando os módulos; prescrições emitidas por quem atende; conferência de alergia em toda receita; botões e rótulos que o cliente reprovou; **folha de infusão com checagem de enfermagem** e **assinatura qualificada ICP-Brasil** nos documentos que saem da clínica (Leis 14.063/2020 e 5.991/1973) | A capacidade clínica existia pela metade e no módulo errado — e a folha de infusão e a assinatura não existiam |
| ~~**44 a 51 — Faturamento fora do congelamento**~~ ✅ | Faturamento + Recepção | Formato do número da guia por convênio; login com permissões granulares; filtros na consulta de guias; atendimento avulso no balcão com prévia das guias; ficha do paciente em abas; cota, pacote, recall e sala de infusão com porta na Recepção; crítica do número da guia a cada tecla | Os três pedidos da cliente dentro do app que fatura, sem tirar capacidade de quem a usava |
| ~~**52 a 59 — Conformidade e acabamento**~~ ✅ | Todos | Os dez pontos da auditoria LGPD da cliente (prontuário que não se apaga, versões, trilha de leitura, guarda de 20 anos, exportação, política de backup); publicação da receita com QR; sidebar do Gerente em rail; agenda em linha do tempo; kanban que se arrasta; CPF único; permissão granular dos documentos | A cliente auditou por escrito, e o placar vivo está em `docs/conformidade-lgpd.md` |
| ~~**60 — Esteira única, Particular e revisão do CRM**~~ ✅ | Todos | O atendimento avulso entrou na esteira da agenda (guia, pacote, insumo e caixa no mesmo ato); o convênio PARTICULAR (`GeraGuia`); venda de pacote no balcão; lote TISS por operadora; cinco brechas de permissão fechadas no faturamento; filtros de pesquisa em 15 telas | Duas frentes paralelas (PRs #119 e #120) — a sessão avulsa atendia de graça quem tinha pacote, e o lote engolia as guias de todas as operadoras num XML só |
| ~~**61 — O circuito clínico fechado**~~ ✅ | Consultório + Recepção + Gerente | Impressão e suspensão de item na sala de infusão; folha achada pelo código impresso; transições completas e arrasto no Meu dia; permissão própria de mover a fila (`MovimentarFila`); trilha de acesso nas telas clínicas que faltavam e "quem abriu este prontuário" no Gerente; folhas aguardando checagem no painel da direção; `CircuitoClinicoTests` | A auditoria de prontidão do módulo clínico — quem executa a prescrição não alcançava a folha impressa, e o quadro do médico só andava se o balcão clicasse |

| ~~**62 — Prontidão da Recepção**~~ ✅ | Recepção | Pacotes com porta que abre e venda liberada ao balcão; o caixa do Finalizar de volta para quem não é do Financeiro; o recado do lançamento (NC reaberta, 2º código) chegando ao balcão; terceiro estado onde faltava; bloqueio de agenda e remarcação em lote alcançáveis; trilha de acesso nos documentos e nos dois exports; mensagem de sucesso que a tela não mostrava em 5 janelas; releitura de fundo na agenda e no painel; descarte de resposta fora de ordem em 3 telas | A auditoria de prontidão do balcão. Nenhum dos defeitos QUEBRAVA nada — build verde, 1441 testes verdes, e quem descobre é a recepcionista com o paciente na frente |
| ~~**63 — As features que faltavam**~~ ✅ | Todos | Visão por SALA e vão fechado na agenda (fecha a feature 02); modelo de evolução; CID-10 com busca e conferência; conciliação bancária por OFX; histórico da guia no faturamento; tirar a receita do ar e dizer para onde o QR aponta | A auditoria de features. Achou também o bug mais grave da rodada, que não estava na lista: **cancelar uma receita não a tirava do ar** — a doc afirmava que sim desde a parcela 53 |
| ~~**72 — X · Y · XY: o atendimento médico e o de enfermagem**~~ ✅ | Consultório + Recepção + shell | A linha do tempo clínica (sessão médica, enfermagem, infusão e documentos) num componente só, nas TRÊS portas; a alergia conferida na ADMINISTRAÇÃO e não só na assinatura; COREN obrigatório; `CatalogoRegistroClinico` como ponto único das nove naturezas, lido pela guarda, pela exportação, pelo art. 18 II e pela linha do tempo; a curva de pressão mesclando consultório e enfermagem com a procedência escrita; seis bloqueadores de conformidade da ficha | O pedido foi "o médico enxerga X, a enfermagem Y, e o que se completa vira XY" — e a medição mostrou que **nenhuma permissão nova era necessária: faltava PORTA**. O mapa está em `docs/atendimento-medico-e-enfermagem.md` |

A coluna "Destrava" é o que justifica a ordem: cada parcela existe porque a seguinte não
teria onde se apoiar sem ela. A fundação (1) é o caso mais claro — sem `Profissional`,
metade das features de 4 e 5 ficava parada. A parcela 5 saiu antes das 3 e 4 justamente
porque essa dependência já estava paga: ela não precisava de nada que as duas entregam.

## Instalação numa clínica nova

1. Configurar o banco (Neon/PostgreSQL) e ter a connection string em mãos.
2. Instalar o app do perfil em cada posto.
3. No **primeiro** app aberto, informar a conexão na tela de setup, testar e salvar. Ela
   fica criptografada por usuário do Windows (DPAPI) em `%APPDATA%\ClinicaSemDor`.
4. Os demais apps da mesma máquina reaproveitam essa configuração automaticamente.
5. As migrations sobem sozinhas na abertura, serializadas por advisory lock — dois apps
   abrindo às 8h não brigam.

## O que NÃO entra em nenhuma parcela

**Fase 4 da arquitetura** (migrar o faturamento para módulo) está **cancelada**: é, por
definição, encostar no app em produção. O custo aceito é o design system e o log
duplicados entre `Clinica.Desktop` e `Clinica.Desktop.Shell`, permanentemente.

O Gerente Geral enxerga o faturamento por **telas próprias de leitura** sobre os serviços
compartilhados, não herdando as telas do app. Ver `arquitetura-multi-exe.md`.
