# Entrega ao cliente

> Como os quatro apps chegam à clínica: qual instalador vai para qual posto, o que precisa
> estar pronto antes de qualquer entrega, e em que ordem quitar o que foi vendido.
>
> O catálogo do que cada módulo entrega está em
> [`features-por-modulo.md`](features-por-modulo.md).

## Topologia: quatro apps, um por perfil

Cada posto de trabalho instala **só o app do seu perfil**. Todos falam com o mesmo
PostgreSQL — não há comunicação entre eles.

| Posto | App | Instalador | O que faz |
|---|---|---|---|
| Faturista | Faturamento | `Clinica.Faturamento-win-Setup.exe` | Pendências, guias, lotes TISS, glosas |
| Balcão / consultório | Recepção | `Clinica.Recepcao-recepcao-Setup.exe` | Agenda, fila, cadastro, prontuário |
| Administrativo | Financeiro | `Clinica.Financeiro-financeiro-Setup.exe` | Caixa, pacotes, estoque |
| Direção | Gerente Geral | `Clinica.Gerente-gerente-Setup.exe` | Todos os módulos + BI, NPS e permissões |

> A proposta comercial (página 24) diz *"Dois apps, um banco"*. São quatro. A página
> precisa ser corrigida antes de ir para outro cliente.

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

## O que já dá para entregar hoje

| App | Pronto para o cliente? |
|---|---|
| Faturamento | ✅ Sim — está em produção |
| Recepção | ✅ Painel, agenda multiprofissional, fila em kanban e cadastro da equipe |
| Financeiro | ✅ Instala e roda sozinho; caixa, conciliação e produção |
| Gerente | ✅ Instala e roda sozinho; reúne Recepção e Financeiro (sem telas de faturamento) |

Com a parcela 0 entregue, **os quatro são instaláveis**. O que varia é quanto cada um já
entrega de conteúdo — ver [`features-por-modulo.md`](features-por-modulo.md).

## As parcelas

| Parcela | Módulo | Entrega | Destrava |
|---|---|---|---|
| ~~**0 — Instalável**~~ ✅ | Todos | Tela de setup própria da suíte; release e versão por app | Instalar qualquer app **sem** o Faturamento na máquina |
| ~~**1 — Fundação**~~ ✅ | Recepção | `Profissional` + `Sala`; agenda multiprofissional com encaixe e lista de espera; fila em kanban; painel próprio | Features 01 e 03 entregues, 02 sem a confirmação automática — e destrava 05, 09, 12 e 13 |
| **2 — Cadastro e prontuário** | Recepção | Pacientes 360º com consentimento LGPD; prontuário com evolução e escala EVA | Features 04 e 05 — fecha a Fase 1 da proposta |
| **3 — Ato clínico** | Recepção | Mapa corporal com protocolo reutilizável; prescrição; os 7 documentos clínicos | Features 06 e 07, e a página 21 |
| **4 — Dinheiro e insumo** | Financeiro | Pacotes/vouchers com saldo; repasse por profissional; estoque com validade e custo | Features 08, 09 e 10 |
| **5 — Inteligência** | Gerente | BI (ocupação, no-show, produtividade); NPS e recall; perfis, permissões e LGPD; visão consolidada lendo o faturamento | Features 11, 12 e 13 |

As parcelas **0 e 1 estão entregues**. A **2** é a próxima: prontuário com evolução e
escala EVA é a afirmação mais exposta da proposta (página 23) e o que ainda não existe.

A única coisa que ficou de fora da parcela 1 é a **confirmação automática** por WhatsApp
(feature 02): o envio de 1 clique existe na agenda, mas automatizar o disparo é campanha
— vai junto com o recall e o NPS, na parcela 5.

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
