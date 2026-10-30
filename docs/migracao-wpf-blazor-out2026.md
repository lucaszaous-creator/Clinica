# Reorganização do Gerente, CRM na Mesa e migração WPF → Blazor — 18 e 19/10/2026

Este documento cobre a sequência inteira, na ordem em que chegou à `main`: a agenda e a
abertura do app (§0.1), a reorganização do Gerente em etapas com o CRM na Mesa da direção
(§0.2) e a rodada da noite de 18–19/10, que tirou o WPF do Gerente e de quase todo o resto
(§1 em diante). O que ficou aberto está no §7.

---

## 0.1 Agenda e abertura do app

| PR | O que mudou |
|---|---|
| [#262](https://github.com/lucaszaous-creator/Clinica/pull/262) (par: semdor-crm#7) | **A resposta do paciente ao lembrete vira aviso.** Confirmou, não vem (inclui quem pediu para remarcar) e sem resposta chegam ao **sino** da Recepção e do Gerente, e o painel da direção ganha os alertas `PacientesNaoVem` e `LembretesSemResposta`. Antes, a resposta só aparecia em Relacionamento → Confirmações. Regra única em `AvisosDeConfirmacao` (Application, 17 testes); sem migration. |
| [#264](https://github.com/lucaszaous-creator/Clinica/pull/264) | **A abertura não espera para sempre pela trava das migrations.** A trava só é pedida quando há migration pendente, com `pg_try_advisory_lock` e prazo de 60 s. Veio do incidente de 18/10, quando uma sessão morta segurava a trava 727411 e a Recepção e o Gerente não abriam. A lição está no CLAUDE.md e em [`crm-em-producao-out2026.md`](crm-em-producao-out2026.md) §6.1. |

## 0.2 Gerente, etapa a etapa, e o CRM na Mesa

| Etapa | PR | O que mudou | Doc |
|---|---|---|---|
| 1 | [#265](https://github.com/lucaszaous-creator/Clinica/pull/265) | **Mesa da direção em Blazor**, com o grupo Direção primeiro no menu do topo (a clínica usa notebook). O `PainelDirecao` WPF saiu. O mockup aprovado tem a logo, os temas claro e escuro e "sem cara de IA". | [`gerente-mesa-da-direcao.md`](gerente-mesa-da-direcao.md), [mockup](mockups/gerente-mesa-da-direcao-v2.html) |
| 2 | [#266](https://github.com/lucaszaous-creator/Clinica/pull/266) | **Dinheiro**: resultado por competência, "Próximos 90 dias" (`ProjecaoDeCaixaService`), o painel Caixa × competência e a aba Metas e teto. Publicado no Gerente 1.2.66. | [`gerente-dinheiro.md`](gerente-dinheiro.md), [`gerente-pendencias.md`](gerente-pendencias.md) |
| 3 | [#267](https://github.com/lucaszaous-creator/Clinica/pull/267) | **Margem por sessão**, primeira aba de Direção → Produção e margem. A conta é receita realizada menos taxa, imposto, insumo e repasse (este rateado pela receita); a sessão sem receita fica fora da conta e é **contada no rodapé**. Publicado no Gerente 1.2.67. | [`gerente-margem.md`](gerente-margem.md) |
| 4 | [#268](https://github.com/lucaszaous-creator/Clinica/pull/268) + semdor-crm#9 | **CRM na Mesa (caminho A).** A cada 30 min o CRM (`DailySummaryWorker`) manda o resumo de hoje e de ontem para `POST /crm/v1/summary/daily`. A ponte (`ClinicSummaryBridge`) grava uma linha por dia em `ResumosCrmDiarios` (migration `20261018141700`), só troca a linha por um cálculo mais novo e recusa número impossível. A Mesa ganha o bloco "Atendimento pelo CRM": conversas, tempo típico de resposta, confirmações e SMS em **segmentos** (nenhum dos dois sistemas guarda o preço da Brevo). Publicado no Gerente 1.2.68. **Ainda não ligado em produção** (§7). | [`gerente-crm.md`](gerente-crm.md), semdor-crm `docs/crm-resumo-diario.md` |
| 6 | [#269](https://github.com/lucaszaous-creator/Clinica/pull/269) | **Todas as telas do Gerente em Blazor** (`Telas/Gestao`): Indicadores, Rentabilidade, Custo de taxas e impostos, Tabela de preço por convênio, Campanhas, De onde vêm os pacientes, Auditoria, Guarda, Documentos emitidos, Acessos, Configurações e Importar pacientes. Cada uma tem a regra na Application (`…NaTela`) e teste. Nenhuma chave de navegação mudou. | [`gerente-blazor.md`](gerente-blazor.md) |

As etapas seguintes são a rodada de 18–19/10 (§1): a navegação e as Configurações do Gerente
em #271, e o resto dos módulos.

### 🧑‍💻 UX · 🛠️ DX · 🤝 CX desta parte

- 🧑‍💻 **UX** — a direção abre o Gerente na Mesa, com o que pede decisão hoje: dinheiro por
  competência e os próximos 90 dias, a margem de cada sessão e o atendimento do CRM, cada
  número levando à tela que o resolve. A recepção vê quem não vem sem abrir aba nenhuma, e o
  app não fica preso na abertura por causa de uma trava esquecida no banco.
- 🛠️ **DX** — cada número tem uma definição só, na Application e com teste (`AvisosDeConfirmacao`,
  `ProjecaoDeCaixaService`, a conta da margem, `ClinicSummaryBridge`). A ponte do CRM é
  defensiva: recusa número impossível e não troca um cálculo novo por um antigo.
- 🤝 **CX** — o custo do SMS aparece em segmentos, o que de fato se cobra, sem inventar preço.
  Toda espera por recurso compartilhado passou a ter prazo, e essa foi a lição do incidente de 18/10.

---

## A rodada de 18–19/10/2026 — migração WPF → Blazor Hybrid

O pedido da direção (o /goal da noite), por inteiro:

> Remover todo o WPF de todos os módulos, colocar todos em Blazor Hybrid, se certificar que
> está tudo funcionando, arrumar o layout da tela de configurações do módulo gerente,
> reorganizar as abas, subabas e os botões de navegação do módulo gerente. Remover questões de
> LGPD dos módulos (só os avisos e o termo), melhorar o módulo da clínica também removendo o
> WPF e colocando Blazor Hybrid, melhorar os ícones/emojis em TODOS os módulos. Remover o
> "Escolher monitor" da tela do paciente. Melhorar a abertura do sistema — ao fazer login
> todos os módulos demoravam de 15 a 30 s para mostrar os dados nas telas iniciais.

Feito **por partes**, uma PR por parte, cada uma mesclada só com o CI inteiro verde (build,
conferências, publicar e as quatro fatias de testes). Este documento é o mapa do que entrou,
do que ficou e por quê. O guia de como escrever uma tela Blazor continua sendo
[`telas-blazor.md`](telas-blazor.md); os mapas por módulo, [`recepcao-blazor.md`](recepcao-blazor.md),
[`gerente-blazor.md`](gerente-blazor.md), [`faturamento-blazor.md`](faturamento-blazor.md).

---

## 1. O que entrou na `main`

| Parte | PR | O que mudou |
|---|---|---|
| 1 | [#270](https://github.com/lucaszaous-creator/Clinica/pull/270) | **Abertura rápida dos cinco apps.** A causa foi MEDIDA (rede simulada): leituras independentes em fila, conexões físicas novas e trabalho de fundo disputando a abertura. Telas iniciais leem em paralelo (`ILeituraParalela`), pool aquecido durante o login, o trabalho de fundo espera a primeira tela (`AberturaDoApp`). Financeiro 3,2 s → 0,16 s; Consultório 3,5 s → 0,47 s. Mapa em [`abertura-rapida.md`](abertura-rapida.md). |
| 2 | [#271](https://github.com/lucaszaous-creator/Clinica/pull/271) | **Gerente**: navegação reorganizada por pergunta (Dinheiro · Desempenho · Pacientes e campanhas · Tabela de preços · Conformidade · Administração — nenhuma chave mudou); Configurações em blocos com Salvar por bloco; **saiu o "Escolher monitor da tela do paciente"** (a coleta é sempre na janela única ou pelo celular). |
| 3 | [#272](https://github.com/lucaszaous-creator/Clinica/pull/272) | **LGPD — os avisos e o termo**: sai o alerta "sem consentimento LGPD" do balcão e o termo de consentimento LGPD; a aba Privacidade ganha "Registrar autorização" por finalidade (com trilha), senão campanhas e SMS deixariam de alcançar paciente novo. Ficaram o art. 18, a trilha de acesso, a guarda de 20 anos e o prontuário imutável. |
| 4 | [#273](https://github.com/lucaszaous-creator/Clinica/pull/273) | Recepção: **fechamento da sessão** e **conferência de atendimentos** viram formulários Blazor. |
| 5 | [#274](https://github.com/lucaszaous-creator/Clinica/pull/274) | **Particular e pacotes** (catálogo, venda, consumos, preços do particular) em Blazor — 18 arquivos WPF a menos. |
| 7 | [#275](https://github.com/lucaszaous-creator/Clinica/pull/275) | Recepção: a **Grade da agenda** e os formulários do horário em Blazor. |
| 6 | [#276](https://github.com/lucaszaous-creator/Clinica/pull/276) | Consultório: **Meu dia, Minha semana, Sem evolução e Meus números** em Blazor. |
| 8 | [#277](https://github.com/lucaszaous-creator/Clinica/pull/277) | Recepção: saem as quatro telas WPF que já abriam em Blazor (~2.400 linhas mortas). |
| 9 | [#278](https://github.com/lucaszaous-creator/Clinica/pull/278) | **Ajuda e suporte** (os quatro módulos), **Pedidos de exame** e **Prontuários do posto** em Blazor. |
| 10 | [#279](https://github.com/lucaszaous-creator/Clinica/pull/279) | Consultório: **Medicamentos** em Blazor (lista de largura inteira, formulário em Modal, importação CSV com confirmação). |
| 11 | [#280](https://github.com/lucaszaous-creator/Clinica/pull/280) | Consultório: **Sessões de enfermagem** em Blazor; os tipos da fila subiram para `Clinica.Application.Tablet` com `ISessoesEnfermagem` (o mesmo serviço do tablet). |
| 12 | [#281](https://github.com/lucaszaous-creator/Clinica/pull/281) | Consultório: **Receitas e documentos** abre a MESMA tela de Documentos Blazor da Recepção, já no paciente em foco. |
| 13 | [#282](https://github.com/lucaszaous-creator/Clinica/pull/282) | **Sala de infusão** (Recepção e Consultório) em Blazor. |
| 14 | [#283](https://github.com/lucaszaous-creator/Clinica/pull/283) | Consultório: **Prescrição de infusão** no menu abre tela Blazor. |

> A numeração das partes segue a ordem em que foram escritas; a 6 e a 7 entraram na `main`
> em ordem trocada.

Os ícones: cada tela migrada usa o componente `Icone` do design system Blazor, o mesmo
conjunto de traço em todas. **Não foi feita uma varredura de ícones/emojis nas telas que
continuam WPF** (§2) — isso vai junto com a migração delas.

---

## 2. O que continua WPF, e por quê

| Onde | O que | Por quê |
|---|---|---|
| Consultório | **Tela do paciente** (`PacienteWorkspaceView` — Atendimento, Prontuário, Anamnese, Acompanhamento, Documentos…) | É o prontuário clínico inteiro: folha S-O-A-P, consulta de enfermagem COFEN, mapa corporal, ficha do atendimento. Migrar às pressas arriscava o fluxo de atendimento; é a próxima rodada. |
| Recepção e Consultório | **Enfermagem** (`EnfermagemView`, shell) | ~1.400 linhas, com o compositor da passagem de enfermagem (WPF) embutido; no exe da Recepção ela escreve na própria tela. Vai junto com a tela do paciente. |
| Dentro da tela do paciente | **Prescrições** e **Prescrição de infusão** como SEÇÕES (`PrescricoesClinicasView`, `PrescricaoInfusaoView`) | O item de MENU já abre a tela Blazor; a seção dentro da tela do paciente (WPF) continua até a tela do paciente migrar. A linha da lista (`LinhaPrescricaoInterna`) já é uma definição só, na Application. |
| Janelas chamadas pelas telas Blazor | Assinatura ICP-Brasil / SafeID, editor da prescrição de infusão, folha de execução (com a 2ª assinatura), evolução de enfermagem da folha, infusão com orientação externa, emissão de documento, coleta do termo | **SafeID e assinatura digital são CONGELADOS** ([`safeid-congelado.md`](safeid-congelado.md)) — as telas Blazor só os chamam. As demais são janelas de escrita que a tela do paciente também usa e migram com ela. |

---

## 3. Como foi feito — o padrão de cada migração

1. **A regra sai da ViewModel e vai para a Application** (`…NaTela`, estática e pura), com
   teste. É o que a tela AFIRMA (frase do resumo, tom do selo, recorte da lista); ViewModel WPF
   não compila no `dotnet test`, e regra que o teste não alcança apodrece calada.
2. **A tela é Razor** (`Clinica.Ui.Blazor/Telas/…`), herda `TelaBase`: contador de geração em
   toda carga, `FalhaDeLeitura` quando a leitura falha, **releitura silenciosa ao voltar à
   aba** (`AoVoltarAsync`), as duas barreiras de permissão (`Pode` no botão, `Exigir` no
   clique), escopo por operação, F5 pelo `AoAtalhoAsync`.
3. **O que precisa continuar WPF passa por uma PONTE**: interface em
   `Clinica.Ui.Blazor/Hospedagem` com uma implementação "indisponível" (a prévia e os testes
   respondem com uma frase — botão que volta calado é o defeito da parcela 41) e a
   implementação WPF que faz EXATAMENTE o que a ViewModel fazia, no despachante do WPF.
   Pontes desta rodada: `IJanelasDosExames`, `IJanelasDosProntuarios`, `IPostoDoConsultorio`
   (agora com `PacienteEmFoco`), `IDocumentosDaRecepcao`, `IJanelasDaSalaDeInfusao`,
   `IJanelasDaPrescricaoDeInfusao`.
4. **Ponte de tela publicada por dois módulos mora em `Clinica.Desktop.Blazor`** e é
   registrada com `TryAddSingleton` (`DocumentosNoAplicativoWpf`, `JanelasDaSalaDeInfusaoWpf`):
   uma cópia por módulo divergiria na primeira correção. Quando um módulo precisa de MAIS
   (o orçamento da Recepção), ele estende a classe e registra com `AddSingleton` — no Gerente,
   que carrega os dois, vale a mais completa, em qualquer ordem de carga.
5. **Apaga-se a View e a ViewModel WPF** no mesmo commit — e procura-se quem mais as usava:
   as ferramentas do CI (`ValidarLayoutWindows`, `VerificarInfusao`, `BenchmarkTelasPostgres`)
   passam a conferir a REGRA ou o SERVIÇO que a tela Blazor lê, em vez de montar a janela.

---

## 4. O que as redes pegaram nesta rodada

| Rede | Pegou |
|---|---|
| `PalcoTrocaRepetidaTests` | *Meus números* e *Prescrição de infusão* não reliam ao voltar à aba. Na segunda, sem paciente escolhido, a busca lê "quem tem horário hoje" ao abrir — voltar agora remonta a busca para reler a sugestão. |
| CI `conferencias` | `tools/VerificarInfusao` ainda montava a `MedicamentosView` apagada (CS0246). Passou a conferir o catálogo que a tela Blazor lê. |
| Leitura do diff | `Selo Tom="erro"` não existe (os tons são `sucesso`, `aviso`, `perigo`, `info`, `neutro`, `saida`): o selo do Prontuários saía sem cor; corrigido para `perigo`. |
| Leitura do diff | A Sala de infusão e a fila do BSV, para quem não é do perfil certo, mostrariam "falha de leitura" (o serviço recusa) — a tela agora diz de quem é a lista. |

---

## 5. 🧑‍💻 UX · 🛠️ DX · 🤝 CX

- 🧑‍💻 **UX** — as telas migradas abrem no **navegador único da janela** (sem a tela preta
  por tela), relêem em silêncio ao voltar à aba (o que outra máquina gravou aparece), dizem
  quando a lista está cortada ou filtrada, e as ações nunca voltam caladas. A abertura dos
  apps caiu de segundos para frações de segundo nas telas iniciais medidas. Documentos e
  Prescrição de infusão abrem **já no paciente que acabou de ser atendido**.
- 🛠️ **DX** — cada tela nova tem a regra na Application com teste; uma ponte por janela WPF,
  e as pontes compartilhadas moram num lugar só. Milhares de linhas de XAML/ViewModel a menos.
  O que resta de WPF está listado no §2, com o motivo — não por esquecimento.
- 🤝 **CX** — **nenhuma capacidade foi removida** (exceto o que a direção mandou tirar: os
  avisos e o termo LGPD e a escolha de monitor). SafeID intocado. Nenhuma migration nesta
  rodada; nenhuma release publicada — os apps instalados só recebem tudo isto quando a direção
  publicar a próxima versão.

---

## 6. Próxima rodada da migração

1. **Tela do paciente** (`PacienteWorkspaceView`) em Blazor, seção por seção, com as janelas
   de escrita que só ela usa.
2. **Enfermagem** junto (ela compartilha o compositor da passagem com a seção do Consultório).
3. Apagar então `PrescricoesClinicasView`/`PrescricaoInfusaoView` e suas ViewModels, que hoje
   só servem de seção da tela do paciente.

---

## 7. O que fica aberto

1. **Ligar o resumo do CRM em produção** (etapa 4). Na **VPS 1**, com o `sudo` digitado
   pela pessoa: atualizar a ponte e, depois que um Gerente 1.2.68 tiver aberto uma vez (a
   migration `20261018141700` cria `ResumosCrmDiarios`), rodar de novo
   `deploy/ponte-crm/papel-ponte-crm.sql` para o papel da ponte gravar na tabela nova. Na
   **VPS 2**: publicar o CRM com `CRM_DAILY_SUMMARY_ENABLED=true`. Até isso acontecer, o
   bloco "Atendimento pelo CRM" da Mesa fica sem dados.
2. **As 3 decisões** registradas em [`gerente-pendencias.md`](gerente-pendencias.md).
3. **Backlog do CRM**: respostas salvas com variáveis, concluir conversa com desfecho, SMS
   de entrada pelo webhook da Brevo, horário de silêncio do SMS, backup do `chaves.pfx` e o
   `crm-release.py` passar a exigir `CRM_AGENTS_FILE`.
4. **A migração da tela do paciente e da Enfermagem** (§6).
