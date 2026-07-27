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

## Pré-requisito bloqueante: a conexão

**Hoje Recepção, Financeiro e Gerente só sobem se o Faturamento estiver instalado na mesma
máquina.** Eles leem a connection string da pasta dele como fallback
(`Clinica.Desktop.Shell/Configuracao/ConexaoStore.cs`).

Com um app por perfil isso é inviável: o balcão não terá faturamento. **Sem a tela de setup
própria, nenhum dos três instala** — é a parcela 0, e ela não é opcional.

## Release e versão por app

Com quatro apps evoluindo em ritmos diferentes, a release conjunta obriga a **republicar o
faturamento por mudança que não é dele** — download e reinício reais numa máquina em
produção, sem nenhum ganho para quem opera. Cada app passa a ter sua tag e sua versão.

| App | packId | Canal | Tag |
|---|---|---|---|
| Faturamento | `Clinica.Faturamento` | `win` | `vX.Y.Z` |
| Recepção | `Clinica.Recepcao` | `recepcao` | `recepcao-vX.Y.Z` |
| Financeiro | `Clinica.Financeiro` | `financeiro` | `financeiro-vX.Y.Z` |
| Gerente Geral | `Clinica.Gerente` | `gerente` | `gerente-vX.Y.Z` |

**`packId` e canal do faturamento não mudam nunca** — mexer neles faz as instalações
existentes perderem o canal de auto-update e pararem de atualizar.

Funciona porque o `GetReleaseFeed` do Velopack percorre as releases e pula as que não têm o
`releases.<canal>.json` do canal pedido, em vez de olhar só a mais recente.

### A contrapartida: migration só aditiva, para sempre

Com versões diferentes em campo por padrão, **conviver com elas deixa de ser exceção**.
Coluna nova o EF do app antigo ignora sem problema; **renomear ou remover algo que o
faturamento usa derruba a clínica**. Não há exceção a essa regra enquanto houver mais de um
app instalado.

## O que já dá para entregar hoje

| App | Pronto para o cliente? |
|---|---|
| Faturamento | ✅ Sim — está em produção |
| Recepção | ⚠️ Só a fila do dia, e **não instala sozinho** (parcela 0) |
| Financeiro | ⚠️ Caixa, conciliação e produção; **não instala sozinho** |
| Gerente | ⚠️ Reúne Recepção e Financeiro; sem telas de faturamento |

Na prática: **hoje só o Faturamento é entregável.** Os outros três dependem da parcela 0.

## As parcelas

| Parcela | Módulo | Entrega | Destrava |
|---|---|---|---|
| **0 — Instalável** | Todos | Tela de setup própria da suíte; release e versão por app | Instalar qualquer app **sem** o Faturamento na máquina |
| **1 — Fundação** | Recepção | `Profissional` + `Sala`; agenda multiprofissional com encaixe e lista de espera; fila em kanban; painel próprio | Features 02 e 03 — e pré-requisito de 05, 09, 12 e 13 |
| **2 — Cadastro e prontuário** | Recepção | Pacientes 360º com consentimento LGPD; prontuário com evolução e escala EVA | Features 04 e 05 — fecha a Fase 1 da proposta |
| **3 — Ato clínico** | Recepção | Mapa corporal com protocolo reutilizável; prescrição; os 7 documentos clínicos | Features 06 e 07, e a página 21 |
| **4 — Dinheiro e insumo** | Financeiro | Pacotes/vouchers com saldo; repasse por profissional; estoque com validade e custo | Features 08, 09 e 10 |
| **5 — Inteligência** | Gerente | BI (ocupação, no-show, produtividade); NPS e recall; perfis, permissões e LGPD; visão consolidada lendo o faturamento | Features 11, 12 e 13 |

A ordem não é negociável nos dois primeiros passos: a **0** é o que torna qualquer app
instalável, e a **1** cria `Profissional`, sem o qual metade das outras features não tem
onde se apoiar.

## Instalação numa clínica nova

1. Configurar o banco (Neon/PostgreSQL) e ter a connection string em mãos.
2. Instalar o app do perfil em cada posto.
3. No **primeiro** app aberto, informar a conexão na tela de setup (parcela 0). Ela fica
   criptografada por usuário do Windows (DPAPI) em `%APPDATA%\ClinicaSemDor`.
4. Os demais apps da mesma máquina reaproveitam essa configuração automaticamente.
5. As migrations sobem sozinhas na abertura, serializadas por advisory lock — dois apps
   abrindo às 8h não brigam.

## O que NÃO entra em nenhuma parcela

**Fase 4 da arquitetura** (migrar o faturamento para módulo) está **cancelada**: é, por
definição, encostar no app em produção. O custo aceito é o design system e o log
duplicados entre `Clinica.Desktop` e `Clinica.Desktop.Shell`, permanentemente.

O Gerente Geral enxerga o faturamento por **telas próprias de leitura** sobre os serviços
compartilhados, não herdando as telas do app. Ver `arquitetura-multi-exe.md`.
