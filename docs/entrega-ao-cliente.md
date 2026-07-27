# Entrega ao cliente

> Como os quatro apps chegam à clínica: qual instalador vai para qual posto, o que precisa
> estar pronto antes de qualquer entrega, e em que ordem quitar o que foi vendido.
>
> O catálogo do que cada módulo entrega está em
> [`features-por-modulo.md`](features-por-modulo.md).

## Onde estamos

**3 das 6 parcelas entregues.** Com a parcela 2, a **Fase 1 da proposta está fechada**.

| Parcela | Estado |
|---|---|
| 0 — Instalável | ✅ entregue |
| 1 — Fundação | ✅ entregue |
| 2 — Cadastro e prontuário | ✅ entregue |
| 3 — Ato clínico | ⬜ próxima |
| 4 — Dinheiro e insumo | ⬜ |
| 5 — Inteligência | ⬜ |

Das **14 features** vendidas na proposta: **5 completas, 4 parciais, 5 inexistentes.**

| Estado | Features |
|---|---|
| ✅ Completas | 01 painel · 03 fila kanban · 04 pacientes 360º · 05 prontuário/EVA · 14 TISS |
| 🟡 Parciais | 02 agenda · 09 caixa · 12 BI · 13 permissões/LGPD |
| ⬜ Inexistentes | 06 mapa corporal · 07 prescrição · 08 pacotes · 10 estoque · 11 NPS/recall |

Dos **12 documentos impressos** da página 21 da proposta, **3 existem** (capa de lote,
guia TISS e fechamento). Os 9 restantes são das parcelas 3 e 4.

### O que temos e o que falta, app por app

| App | O que já entrega | O que ainda falta |
|---|---|---|
| **Faturamento** | Tudo: motor de regras, 2º código, lotes TISS, glosa e recurso, PDFs. **Em produção.** | Nada — está **congelado** de propósito |
| **Recepção** | Painel do dia, agenda multiprofissional com encaixe e lista de espera, fila em kanban, profissionais e salas, Pacientes 360º com foto e LGPD, prontuário com EVA e anexos | Mapa corporal, prescrição e os 7 documentos clínicos (parcela 3); confirmação automática por WhatsApp (parcela 5) |
| **Financeiro** | Caixa do mês, lançamento manual, conciliação com o faturamento, produção do período | Pacotes/vouchers, estoque, repasse por profissional, recibo e orçamento (parcela 4) |
| **Gerente Geral** | Carrega Recepção + Financeiro inteiros | Telas próprias de BI, NPS/recall, perfis e permissões, e a visão consolidada do faturamento (parcela 5) |

### O que falta, na ordem em que vai ser feito

- **Parcela 3 — ato clínico** (Recepção): mapa corporal com protocolo reutilizável,
  prescrição, e os 7 documentos clínicos da página 21 (receita, atestado, comparecimento,
  pedido de exame, relatório de evolução, consentimento, anamnese).
- **Parcela 4 — dinheiro e insumo** (Financeiro): pacotes/planos/vouchers com saldo,
  repasse por profissional, estoque com validade e custo, recibo e orçamento.
- **Parcela 5 — inteligência** (Gerente): BI com ocupação, no-show e produtividade; NPS e
  recall; perfis e permissões; visão consolidada lendo o faturamento; e a confirmação
  automática por WhatsApp, que é campanha e por isso vem com o recall.

### Duas afirmações da proposta que ainda não se sustentam

1. **Página 24 — "Dois apps, um banco".** São **quatro**, um por perfil.
2. **Página 23 — "Prontuário com mapa corporal e EVA"**, marcado ✓ contra os concorrentes.
   Depois da parcela 2, **metade virou verdade**: o prontuário com EVA existe. O **mapa
   corporal** não — vai na parcela 3.

Ambas precisam de decisão comercial antes de o documento ir para outro cliente.

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
| **3 — Ato clínico** | Recepção | Mapa corporal com protocolo reutilizável; prescrição; os 7 documentos clínicos | Features 06 e 07, e a página 21 |
| **4 — Dinheiro e insumo** | Financeiro | Pacotes/vouchers com saldo; repasse por profissional; estoque com validade e custo | Features 08, 09 e 10 |
| **5 — Inteligência** | Gerente | BI (ocupação, no-show, produtividade); NPS, recall e confirmação automática; perfis e permissões; visão consolidada lendo o faturamento | Features 11 e 12, a metade que falta da 13 (a LGPD saiu na parcela 2) e a confirmação automática da 02 |

A coluna "Destrava" é o que justifica a ordem: cada parcela existe porque a seguinte não
teria onde se apoiar sem ela. A fundação (1) é o caso mais claro — sem `Profissional`,
metade das features de 4 e 5 fica parada.

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
