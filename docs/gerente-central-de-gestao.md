# Gerente: a central de gestão da clínica (nov/2026, proposta com mockup)

> **v3 · console de gestão e integrações** (mockup [`mockups/gerente-console-de-gestao.html`](mockups/gerente-console-de-gestao.html),
> fotos `mockups/png/gerente-console-*.png`). Pedido da direção: *"dá para profissionalizar e muito,
> inclusive pensando em integrações com itens dentro e fora do sistema"*.

## v3 · a camada profissional

| Peça | O que entra | Novo no código |
|---|---|---|
| **Console** | navegação lateral recolhível (64 px em 1024, faixa no celular), barra de contexto com período, comparação e filtros que valem para todas as telas, frescor dos dados ("8 de 9 fontes em dia"), link da visão, busca Ctrl+K | `ContextoDeGestao` (período, comparação, filtros) passado a todas as telas do Gerente |
| **Drill-down** | qualquer número abre uma gaveta com a decomposição, a leitura, a definição e as ações (encaminhar, abrir a origem, criar automação) | componente `Gaveta` e um `…NaTela` por indicador |
| **Dicionário de indicadores** | definição única, fórmula, entra / não entra, fonte, dono, meta e histórico versionado | `Indicador` + `VersaoIndicador` |
| **Automações** | quando → só se → então, simulação de 90 dias e 7 dias em teste. Nenhuma regra fala com o paciente | `RegraAutomatica` sobre o `Encaminhamento` da v2 |
| **Integrações** | estado, último contato, dado que passa e para onde, de cada conexão. Existem hoje: apps, CRM/WhatsApp, tablet, SafeID (só estado: motor congelado), A1, SMS, Neon, XML TISS. Novas: extrato por Open Finance (só leitura), EDI do adquirente, NFS-e, contabilidade, TISS por webservice e elegibilidade, BI só leitura, API/webhooks, registro profissional, e-mail | uma **fila de integração** só (evento, tentativa, reenvio, registro) e um adaptador por integração atrás de interface na Application |
| **Mapa de dados (LGPD)** | integração × dado × destino × base legal × quem autorizou. Integração com dado de saúde não liga sem a linha | `FluxoDeDado` |
| **Relatórios e entregas** | relatório aos sócios, pacote contábil, placar semanal, "o seu mês" de cada profissional. Acesso externo com escopo, validade e revogação | `EntregaProgramada`, `AcessoExterno` |

Princípios de toda integração:
1. Só lê, quando der.
2. O sistema propõe e a pessoa confirma: nenhuma integração escreve no dinheiro nem no prontuário sozinha.
3. A falha não para o atendimento: o evento fica na fila e é reenviado.
4. Cada integração declara o dado e o destino.
5. A credencial é cifrada.
6. Desligar não perde nada: volta-se ao processo manual de hoje.

Ordem das integrações, pelo trabalho manual que tiram:
1. Extrato do banco e EDI do adquirente.
2. NFS-e, contabilidade e BI.
3. TISS por webservice (uma por operadora).
4. Prescrição: avaliar (dado de saúde fora, sob contrato).

A navegação lateral revê a decisão de out/2026 ("menu no topo porque a clínica usa notebook")
**só no Gerente** e por medida: com os grupos da v2 a barra do topo já ocupava duas linhas em 1366.
O trilho recolhido em 64 px devolve a largura ao notebook. Os outros quatro apps continuam com o menu
no topo.

### 🧑‍💻 UX · 🛠️ DX · 🤝 CX da v3

- 🧑‍💻 **UX**: o mesmo recorte vale em todas as telas, e todo número responde "por quê?" e "quem resolve?"
  num clique. O Financeiro deixa de importar OFX e de digitar taxa, e o Faturamento deixa de subir
  XML onde há webservice.
- 🛠️ **DX**: uma fila e um contrato de adaptador para todas as integrações. Uma definição de indicador
  lida por placar, gaveta, reunião, relatório e automação. A automação reusa o encaminhamento da v2.
- 🤝 **CX**: parece, e funciona como, sistema de gestão de mercado. Banco e NFS-e têm custo por uso, e
  cada integração liga separada. O mapa de dados é a resposta pronta para auditor e titular.

> **v2: o ciclo de gestão** (mockup [`mockups/gerente-ciclo-de-gestao.html`](mockups/gerente-ciclo-de-gestao.html),
> fotos `mockups/png/gerente-ciclo-*.png`). A direção viu a v1 e respondeu: *"ainda não vi grande
> diferença para um sistema de gestão"*. Ela tinha razão. A v1 reorganizava o menu e acrescentava
> leituras, e um sistema de gestão **fecha o ciclo**: planeja, mede contra a meta, encaminha o desvio
> para uma pessoa com prazo, cobra e confirma. A v2 acrescenta isso por cima da v1 (Clínica, Equipe,
> Ficha, Escala e Saúde do sistema continuam).

## v2 · o ciclo: Planejar → Acompanhar → Agir → Cobrar e fechar

A barra passa a ser **Mesa · Planejar · Acompanhar · Agir · Equipe · Administração │ Operação**.
O Gerente tem mais grupos que os outros apps, então a navegação desce para a segunda linha já abaixo
de 1440 px (nos outros apps, abaixo de 1300).

| Peça | O que faz | Já existe | Novo no banco (aditivo) |
|---|---|---|---|
| **Placar da clínica** (Mesa) | 16 indicadores em 4 perspectivas (Financeiro, Pacientes, Clínica, Equipe), cada um contra a meta, com 6 meses de tendência | os números (Indicadores, Margem, NPS, EVA, ocupação, `MetaService`) | meta para cada um dos 16 |
| **Encaminhamentos** | alerta, ocorrência, reunião ou ideia vira tarefa com responsável, prazo e critério de fim. Chega ao sino e ao chat do app de quem recebe. Os ligados a um alerta **fecham sozinhos** quando o alerta some; o atrasado volta à Mesa | sino, chat, os alertas da Mesa | `Encaminhamento` (origem, responsável, prazo, critério, evidência) |
| **Reunião de gestão** | pauta montada sozinha (ficou para trás, fora da meta, ocorrências, vencimentos, pedidos da equipe). Decisão vira encaminhamento na hora. Ata em PDF e no chat. Mede o cumprimento | PDF, chat | `Reuniao`, `Decisao` |
| **Fechamento do mês** | dez passos conferidos nos módulos (caixa, extrato, cartão, lotes, glosas, evoluções, estoque, impostos, repasses, metas) e o relatório aos sócios | os dez estados são lidos por serviços existentes | aprovação do repasse, relatório |
| **Ocorrências** | segurança do paciente (RDC 36/2013): registro em qualquer app, classificação, análise, ação e taxa por mil sessões | nada | `Ocorrencia`: entidade CLÍNICA, sem exclusão, com versão, na exportação e na guarda |
| **Contratos e obrigações** | convênios (vigência, reajuste, prazo, glosa, margem) e licenças (alvará, PGRSS, CNES, RT, AVCB, seguro), avisando com a antecedência que a renovação pede | margem, glosa e prazo | `Contrato`, `Obrigacao` |
| **Orçamento e cenários** | ano planejado × realizado e o "e se" (contratar, ocupação, reajustes) | `OrcamentoService`, Margem por sessão, regras de repasse | cenário salvo |

O encaminhamento é **gestão interna**. Ele não traz de volta as "tarefas" que saíram do CRM: não
há tarefa para paciente nem funil.

Ordem de entrega da v2:
1. Encaminhamentos e a Mesa nova (o coração do ciclo).
2. Reunião e fechamento do mês.
3. Equipe (v1), contratos e obrigações.
4. Clínica (v1), ocorrências, planejamento e Saúde do sistema (v1).

### 🧑‍💻 UX · 🛠️ DX · 🤝 CX da v2

- 🧑‍💻 **UX**: a direção sabe onde está fora da meta, quem está cuidando e o que venceu, sem depender
  de memória. A equipe não precisa abrir o Gerente: o encaminhamento chega ao sino e ao chat do app em
  que ela trabalha. A reunião começa com a pauta pronta e termina com a ata enviada.
- 🛠️ **DX**: uma peça central nova (`Encaminhamento`), com a regra de "fecha sozinho" num lugar só,
  reaproveitando os alertas que a Mesa já calcula. Quatro famílias de entidade, todas aditivas. Placar,
  fechamento e cenários leem serviços existentes, e cada tela leva `…NaTela` com teste.
- 🤝 **CX**: o produto muda de patamar, de "sistema da recepção e do faturamento" para "sistema que
  gere a clínica". Responde ao que auditor e Vigilância perguntam. Releases: o Gerente sempre, e os
  quatro apps da suíte quando o sino e o botão de ocorrência chegarem a eles. O faturamento não é
  republicado.

---

## v1 (a base que a v2 mantém)

Pedido da direção: *"o módulo do gerente tem que virar um sistema central de gestão —
administrativa, clínica, financeira e outros"*. A decisão do desenho foi delegada.

Mockup navegável, montado com o CSS real das telas Blazor (tokens + `clinica.css` +
moldura) e dados fictícios: [`mockups/gerente-central-de-gestao.html`](mockups/gerente-central-de-gestao.html).
Fotos em 1366 px: `mockups/png/gerente-central-*.png`.

## O diagnóstico

O Gerente carrega os seis módulos e põe por cima o grupo **Direção**, organizado em torno do
dinheiro. A Mesa da direção e o financeiro estão bem cobertos. Três áreas não estão:

| Área | Hoje |
|---|---|
| Financeira | ✅ completa (Resultado, 90 dias, Margem, Convênio, Custo, Metas) |
| Clínica | só alertas soltos na pauta (`ProntuarioEmAberto`, `InfusaoAguardando`, sessões sem evolução); a Auditoria do atendimento está em *Desempenho* |
| Equipe | espalhada em seis lugares: Profissionais e salas (Recepção), Acessos (Administração), Produção (Desempenho), Repasses (Financeiro), Meus números (Consultório), Comissão de recuperação |
| Sistema | o estado do backup fica numa aba de Configurações; validade do A1, versão dos apps e saúde da ponte do CRM não aparecem para a direção |

## As opções

| | A. Somar telas aos grupos atuais | **B. Pilares de gestão** ⭐ | C. Gestão na web de leitura |
|---|---|---|---|
| 🧑‍💻 UX | rápido, mas *Desempenho* e *Administração* viram gaveta; a equipe segue sem dono | a direção pensa por área, e cada pilar responde uma pergunta com a porta que resolve | bom para longe, mas a web é só leitura e não vai para a internet |
| 🛠️ DX | nenhuma peça de estrutura; dívida de organização cresce | nenhuma chave muda; telas novas de LEITURA sobre serviços existentes | segunda superfície com regra de acesso e de rede própria |
| 🤝 CX | barato; "parece caixa com relatório" | parece sistema de gestão de clínica; publica só o Gerente (e a Recepção no PR 2) | custo de servidor e risco de dado de saúde fora (ponto 10) |

**Decisão: B.**

## O que entra, o que muda, o que sai

A barra do Gerente: **Mesa · Clínica · Equipe · Dinheiro · Desempenho · Pacientes ·
Administração │ Operação**. "Operação" é um menu de quatro colunas (Recepção, Consultório,
Financeiro, Faturamento de guias) com as telas dos outros módulos — nada some, a busca
(Ctrl+F) continua achando tudo. Nenhuma chave de tela muda (contrato entre módulos).

| Tela | Situação | De onde vêm os números |
|---|---|---|
| **Mesa** | muda: pulso das 5 áreas, pauta com filtro por área, "a equipe agora" | `PainelDirecaoService` (+ os alertas novos abaixo) |
| **Clínica › Qualidade assistencial** | nova | sessões sem evolução, folhas de infusão, termos, EVA do prontuário |
| Clínica › Auditoria do atendimento · Documentos emitidos · Guarda | mudam de grupo | telas existentes |
| **Equipe › Visão da equipe** | nova | `EquipeService`, jornada, produção, `RepasseService`, `AcessoService` |
| **Equipe › Ficha do profissional** | nova | as mesmas, de uma pessoa, + trilha de auditoria |
| **Equipe › Escala e ausências** | nova | jornada + `BloqueioAgenda` (ausência = bloqueio com tipo) |
| Equipe › Profissionais e salas · Acessos | mudam de grupo | a mesma tela da Recepção (mesma chave) · Acessos existente |
| **Administração › Saúde do sistema** | nova | cópia de segurança, certificados, versão por máquina, ponte do CRM, banco |
| Administração › Auditoria | muda de grupo (o grupo Conformidade deixa de existir) | tela existente |
| Dinheiro | "Tabela de preços" entra como aba; Margem, Convênio e Custo viram uma aba | telas existentes |

Regras de tela da casa: **uma porta por pergunta** (Equipe reusa as chaves; "quem parou o
tratamento" mostra o número e leva ao Relacionamento, sem segunda lista); **o que pede ação
vira aviso** (ausência com horários marcados entra no sino da Recepção como "remarcar"; A1 a
vencer, app parado e backup vencido entram na pauta); **a mensagem ao paciente a secretária
revisa** (a ausência não cancela nem escreve sozinha).

LGPD (pontos 4 e 5): a Clínica mostra NÚMEROS com `VerIndicadores`; a lista nominal de
pacientes pede `VerProntuario` e registra o acesso (`AcessoProntuarioService`).

Fica de fora, de propósito: ponto eletrônico e folha de pagamento (outro produto, Portaria
671 do MTE); gerar lote TISS pelo Gerente (decisão já registrada); segunda lista de recall.

## Peças novas no banco (todas aditivas)

- **Tipo de ausência** em `BloqueioAgenda` (férias, licença, congresso, folga) — hoje só há o
  motivo em texto.
- **Documentos do profissional com validade** (registro no conselho, certificado, título).
- **Versão por máquina**: cada app grava, na abertura, máquina + app + versão + hora. É o que
  permite dizer "RECEPCAO-02 está para trás"; sem isso a tela não pode afirmar nada (regra 9).

## Entregas

1. **A barra e a Equipe** — pilares, menu Operação, Visão da equipe e Ficha. Só leitura, sem migration.
2. **Escala e ausências** — grade, ausência com bloqueio e aviso no sino da Recepção, documentos com validade.
3. **Clínica** — Qualidade assistencial e a lista nominal protegida.
4. **Saúde do sistema e a Mesa nova** — versão por máquina, certificados, backup; pulso e filtro.

Cada PR: `…NaTela` com teste na Application, sementes na prévia, fotos em 1024/1366 com os
formulários abertos, `TelasBlazorAbremTests` e o release só do app que carrega a mudança.

## 🧑‍💻 UX · 🛠️ DX · 🤝 CX

- 🧑‍💻 **UX** — a direção abre o app e vê as cinco áreas; o que está vermelho leva à tela que
  resolve. "Como está o Dr. Rafael?" vira uma ficha em vez de seis telas. Férias viram bloqueio
  de agenda e aviso para a recepção sem ninguém precisar lembrar. Responsivo em 1024, 1366 e
  celular (colunas empilham abaixo de 1180 px).
- 🛠️ **DX** — telas de leitura sobre serviços que já existem; nenhuma chave muda; três peças
  novas no banco, aditivas; a regra do aviso de remarcar mora no sino, num lugar só; o que a
  tela afirma mora em `…NaTela` com teste.
- 🤝 **CX** — o Gerente passa a parecer o sistema de gestão da clínica. Releases: Gerente em
  todas as entregas, Recepção no PR 2 (aviso no sino); o faturamento não é republicado. O risco
  é de hábito ("onde foi a Conformidade?"), coberto pela busca, que mostra o caminho novo.
