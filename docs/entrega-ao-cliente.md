# Entrega ao cliente

> Como os quatro apps chegam à clínica: qual instalador vai para qual posto, o que precisa
> estar pronto antes de qualquer entrega, e em que ordem quitar o que foi vendido.
>
> O catálogo do que cada módulo entrega está em
> [`features-por-modulo.md`](features-por-modulo.md).

## Onde estamos

**5 das 6 parcelas entregues.** Com a parcela 3, a **Recepção está completa**; com a 5,
o Gerente Geral deixou de ser uma casca que só carrega os outros. Falta a **parcela 4**,
no Financeiro.

| Parcela | Estado |
|---|---|
| 0 — Instalável | ✅ entregue |
| 1 — Fundação | ✅ entregue |
| 2 — Cadastro e prontuário | ✅ entregue |
| 3 — Ato clínico | ✅ entregue |
| 4 — Dinheiro e insumo | ⬜ a única que falta |
| 5 — Inteligência | ✅ entregue |

> A parcela 5 saiu **fora da ordem** de propósito: ela não dependia da 3 nem da 4. O que
> ela precisava — `Profissional` (parcela 1) e o consentimento LGPD (parcela 2) — já
> estava pronto, e segurá-la só adiaria a única tela que a direção usa.

Das **14 features** vendidas na proposta: **11 completas, 1 parcial, 2 inexistentes.**

| Estado | Features |
|---|---|
| ✅ Completas | 01 painel · 02 agenda · 03 fila kanban · 04 pacientes 360º · 05 prontuário/EVA · 06 mapa corporal · 07 prescrição · 11 NPS/recall · 12 BI · 13 permissões/LGPD · 14 TISS |
| 🟡 Parciais | 09 caixa (falta o repasse) |
| ⬜ Inexistentes | 08 pacotes · 10 estoque |

Dos **12 documentos impressos** da página 21 da proposta, **10 existem**. Os 2 restantes
(recibo e orçamento) são do Financeiro, na parcela 4.

### O que temos e o que falta, app por app

| App | O que já entrega | O que ainda falta |
|---|---|---|
| **Faturamento** | Tudo: motor de regras, 2º código, lotes TISS, glosa e recurso, PDFs. **Em produção.** | Nada — está **congelado** de propósito |
| **Recepção** | Painel do dia, agenda multiprofissional com encaixe e lista de espera, fila em kanban, profissionais e salas, Pacientes 360º com foto e LGPD, prontuário com EVA e anexos, mapa corporal com protocolo e os 7 documentos clínicos | Nada da proposta |
| **Financeiro** | Caixa do mês, lançamento manual, conciliação com o faturamento, produção do período | Pacotes/vouchers, estoque, repasse por profissional, recibo e orçamento (parcela 4) |
| **Gerente Geral** | Carrega Recepção + Financeiro inteiros **e** as telas da direção: indicadores (ocupação, no-show, produtividade), campanhas (confirmação, NPS e recall), acessos com perfis e permissões, e a visão de leitura do faturamento | Nada da parcela 5 — o que falta aqui chega junto com as parcelas 3 e 4 dos outros módulos |

### O que falta, na ordem em que vai ser feito

- **Parcela 4 — dinheiro e insumo** (Financeiro): pacotes/planos/vouchers com saldo,
  repasse por profissional, estoque com validade e custo, recibo e orçamento.

A parcela 5 (inteligência) já foi entregue — ver
[Inteligência e acesso](#inteligência-e-acesso--resolvido-na-parcela-5).

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

## Topologia: quatro apps, um por perfil

Cada posto de trabalho instala **só o app do seu perfil**. Todos falam com o mesmo
PostgreSQL — não há comunicação entre eles.

| Posto | App | Instalador | O que faz |
|---|---|---|---|
| Faturista | Faturamento | `Clinica.Faturamento-win-Setup.exe` | Pendências, guias, lotes TISS, glosas |
| Balcão / consultório | Recepção | `Clinica.Recepcao-recepcao-Setup.exe` | Agenda, fila, cadastro, prontuário |
| Administrativo | Financeiro | `Clinica.Financeiro-financeiro-Setup.exe` | Caixa, pacotes, estoque |
| Direção | Gerente Geral | `Clinica.Gerente-gerente-Setup.exe` | Todos os módulos + BI, NPS e permissões |

> São **quatro** — a proposta comercial (página 24) diz *"Dois apps, um banco"*. Ver
> [Duas afirmações da proposta que ainda não se sustentam](#duas-afirmações-da-proposta-que-ainda-não-se-sustentam).

O Gerente Geral **carrega os módulos dos outros** — quem o instala não precisa da Recepção
nem do Financeiro na mesma máquina.

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

Com quatro apps evoluindo em ritmos diferentes, a release conjunta obrigava a **republicar
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
  oferece criar o primeiro usuário (Gerente). O **Faturamento continua sem login** — está
  congelado e roda num posto só.

A migration (`20260727220000_CampanhasEAcesso`) é **puramente aditiva**: só as tabelas
novas `ContatosCampanha` e `Usuarios`.

## Os quatro são instaláveis

Desde a parcela 0, **os quatro apps instalam e rodam sozinhos** — nenhum depende de o
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
| **4 — Dinheiro e insumo** | Financeiro | Pacotes/vouchers com saldo; repasse por profissional; estoque com validade e custo | Features 08, 09 e 10 |

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
