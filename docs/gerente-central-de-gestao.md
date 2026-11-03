# Gerente: a central de gestão da clínica (nov/2026, proposta com mockup)

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
