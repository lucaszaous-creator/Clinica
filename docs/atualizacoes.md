# Atualização automática do sistema

O app se atualiza sozinho via **Velopack + GitHub Releases**. Instalado uma única vez pelo instalador,
ninguém mais precisa baixar exe manualmente.

> **Nov/2026 — o código vai para um repositório PRIVADO.** As releases (instaladores, pacotes e
> `releases.<canal>.json`) passam a morar num repositório **público só de instaladores**,
> `lucaszaous-creator/clinica-atualizacoes`, sem código. Com o Clinica privado, o download direto das
> releases dele pede login e os apps deixariam de se atualizar. A transição está logo abaixo.

## O que o dono precisa criar (uma vez)

1. **O repositório** `lucaszaous-creator/clinica-atualizacoes` — **público**, vazio (só um README dizendo
   "instaladores do sistema da clínica; não há código aqui"). Sem Actions, sem branch protegida.
2. **O token** — *Fine-grained personal access token* (Settings → Developer settings → Fine-grained tokens):
   - **Resource owner:** lucaszaous-creator;
   - **Repository access:** *Only select repositories* → **só** `clinica-atualizacoes`;
   - **Permissions → Repository → Contents: Read and write** (o *Metadata: Read* entra sozinho). Nada mais;
   - **Expiration:** 1 ano (anotar a data: token vencido = release que sai só pela ponte, com aviso amarelo).
3. **O secret** no Clinica (Settings → Secrets and variables → Actions → *New repository secret*):
   - **Nome:** `ATUALIZACOES_TOKEN`; **valor:** o token acima.
4. **(Depois da transição)** a **variável** `ATUALIZACOES_PONTE` = `desligada` (Settings → Secrets and
   variables → Actions → *Variables*). Enquanto ela não existir, a ponte fica ligada.

⛔ **Nenhum token vai dentro do app.** Os dois repositórios que o app lê são públicos; o token só existe no
Actions, para PUBLICAR.

## Roteiro da transição (sem parar a clínica)

As máquinas instaladas hoje rodam uma versão que lê as releases **do Clinica**. Elas só aprendem o endereço
novo atualizando para uma versão que o conhece — e essa versão precisa estar **no Clinica**.

1. **Merge deste PR** e criação do repositório, do token e do secret (acima).
2. **Copiar as videoaulas** para o repositório novo: Actions → *Publicar videoaulas* → *Run workflow*
   (o disparo manual copia os MP4 da release `treinamento-desktop-…` do Clinica, sem sobrescrever).
3. **Publicar os cinco apps** (uma tag por `git push`, como sempre). Cada release sai nos **dois** lugares:
   no `clinica-atualizacoes` e, pela **ponte**, no Clinica. As máquinas antigas acham a versão no Clinica e
   atualizam; a versão nova já lê do `clinica-atualizacoes` primeiro.
4. **Esperar todas as máquinas atualizarem.** Como conferir: o rodapé do faturamento e a versão nos apps
   (Ajuda e suporte) de cada máquina; ou, sem ir até elas, a trilha de entrada dos apps. Inclua máquinas que
   ficam desligadas (férias, posto reserva): **a que não abriu ainda continua lendo do Clinica**.
5. **Desligar a ponte**: variável `ATUALIZACOES_PONTE` = `desligada`. A partir daí as releases saem só no
   repositório novo.
6. **Só então tornar o Clinica privado.** Uma máquina esquecida numa versão antiga para de atualizar; a
   solução é reinstalar pelo Setup do repositório novo (`…/clinica-atualizacoes/releases/latest`).

A versão nova lê o repositório novo **primeiro** e completa, canal a canal, com o Clinica enquanto o novo
não tiver os cinco apps (vence a versão maior). Por isso a ordem dos passos 1–3 não prende ninguém: publicar
antes de o repositório existir só mantém a leitura no Clinica (`FonteAtualizacaoGithub`, com teste).

### O que ainda depende do Clinica ser público

| O quê | Onde | O que acontece quando ficar privado | Ação |
|---|---|---|---|
| Atualização dos 5 apps | `FonteAtualizacaoGithub`, `release.yml` | resolvido por este PR | — |
| Videoaulas da aba Treinamento | `AcervoTreinamento` (`ReleaseDosVideos`), `publicar-videoaulas.yml` | resolvido por este PR (passo 2) | copiar os vídeos antes |
| Instalação da VPS por `curl … raw.githubusercontent.com/…/Clinica/main/deploy/vps/*.sh` | `deploy/vps/preparar-vps.sh`, `instalar-gestao.sh`, `tunel-ssh.sh`, `docs/deploy-automatico.md` | o `curl` dá 404 | só afeta **instalação nova** de VPS: copiar os scripts para a VPS (ou `curl` com token) — a VPS já instalada não usa |
| Deploy do portal, da gestão e do site | `deploy-portal.yml` | continua (roda no Actions do próprio repositório), agora gastando minutos pagos | ver "Custo" |
| Links de release em documentos | `docs/operacao-infusoes-safeid.md`, `publish-exe.bat`, este documento (antes) | link quebrado para quem não tem acesso | só histórico |
| Instalador na máquina nova | página de releases | o Setup passa a sair de `…/clinica-atualizacoes/releases/latest` | avisar o suporte |
| Tablet (portal) | publicado pelo `deploy-portal.yml` na VPS | não lê o GitHub | — |

Nenhuma tela do app abre link do GitHub além das videoaulas.

## Custo do Actions com o repositório privado

Repositório público não paga minuto; privado consome a cota do plano (GitHub Free: 2.000 min/mês; Pro:
3.000) e o excedente é cobrado — **Windows conta 2×**. Medido nos últimos 30 dias (18/10 a 18/11/2026):

| Workflow | Execuções/mês | Minutos por execução | Minutos cobrados/mês |
|---|---|---|---|
| CI do PR (`build-exe`) | 134 (≈ 98 PRs) | 12 Windows (×2) + 39 Linux = 63 | ≈ 6.300 a 8.400 |
| Deploy do portal (cron a cada 15 min + push) | 735 | ≈ 3 Linux | ≈ 2.200 |
| Release (por app) | 75 | 4 Windows (×2) + 1 Linux = 9 | ≈ 680 |
| Videoaulas e outros | ~40 | ≈ 1 | ≈ 40 |
| **Total** | | | **≈ 9.200 a 11.300** |

Acima da cota, a ≈ US$ 0,008 por minuto cobrado: **≈ US$ 55 a 75 por mês** no Free, ≈ US$ 50 a 65 no Pro
(preço público do GitHub conhecido em 2026; confira a página de cobrança antes de decidir).

Como reduzir, do maior para o menor ganho:

1. **Deploy do portal:** o cron de 15 em 15 minutos responde por ~2.000 min. Ele só procura o que publicar;
   trocar para de hora em hora (ou só push + disparo manual) corta ~75–100% disso.
2. **CI só quando o código muda:** `paths-ignore` para `docs/**` e `*.md` no `build-exe.yml`; PR só de
   documento não precisa dos 6 lotes.
3. **Cancelar rodada velha do mesmo PR** (`concurrency` com `cancel-in-progress: true` no `build-exe.yml`):
   cada push novo num PR hoje roda tudo de novo.
4. **Runner próprio** (self-hosted) na máquina Windows de trabalho ou na VPS: minuto não cobrado; o custo vira
   manter a máquina ligada e atualizada — e runner próprio em repositório privado é seguro, ao contrário do público.
5. Os 2 jobs Windows (`conferencias`, `publicar`) são os que contam 2×; o que puder ir para Linux corta pela metade.

## Como funciona no app

1. **Na abertura**: o sistema consulta as releases e, havendo versão nova, baixa e aplica (limite de tempo
   para rede lenta/offline não travar a abertura). Ver o PR da "atualização antes do login" para a tela
   "Atualizando…" e as versões obrigatórias.
2. **Com o app aberto**: versões publicadas durante o expediente são baixadas em segundo plano e aplicadas ao
   fechar; a próxima abertura já entra atualizada.
3. **Botão "Atualizar para vX.Y.Z"** (faturamento, rodapé da sidebar): aplica na hora uma versão já baixada.
4. A versão em uso aparece no rodapé.

Falhas de rede/GitHub são silenciosas: o sistema continua funcionando na versão atual e tenta de novo depois.

## Instalação correta (uma vez por máquina)

1. Abra <https://github.com/lucaszaous-creator/clinica-atualizacoes/releases> (até a transição terminar, as
   mesmas versões também estão nas releases do Clinica).
2. Baixe e execute o Setup do app (tabela abaixo).
3. Pronto — a partir daí as versões novas chegam sozinhas.

⚠️ **Não distribua o exe portátil** (`publish-exe.bat` ou artefato "Build EXE" do Actions) para uso na
clínica: nesse formato o Velopack se considera não-instalado e o auto-update fica desativado.

## Como publicar uma versão nova

```bash
git tag recepcao-v1.2.80
git push origin recepcao-v1.2.80
```

Ou **Actions → "Release (instalador + auto-update)" → Run workflow**, escolhendo o app, a versão e, se for o
caso, **Atualização obrigatória**. A versão que traz migration (desde a última versão daquele app) sai
obrigatória sozinha.

## Os apps e os canais

| App | Instalador | Canal |
|---|---|---|
| Faturamento | `Clinica.Faturamento-win-Setup.exe` | `win` (padrão — **não mudar**) |
| Recepção | `Clinica.Recepcao-recepcao-Setup.exe` | `recepcao` |
| Consultório | `Clinica.Clinico-clinico-Setup.exe` | `clinico` |
| Financeiro | `Clinica.Financeiro-financeiro-Setup.exe` | `financeiro` |
| Gerente Geral | `Clinica.Gerente-gerente-Setup.exe` | `gerente` |

⚠️ Mudar o `packId` ou o canal do faturamento faz as instalações que já existem perderem o canal de
auto-update e **pararem de atualizar**.

## Arquivos envolvidos

| Papel | Arquivo |
|---|---|
| De onde ler (repositório novo, com o Clinica de reserva na transição) | `src/Clinica.Desktop.Shell/Atualizacao/FonteAtualizacaoGithub.cs` |
| Cliente de update do faturamento | `src/Clinica.Desktop/UpdateService.cs` |
| Cliente de update dos apps da suíte | `src/Clinica.Desktop.Shell/Shell/AtualizadorSuite.cs` |
| Publicação (os dois repositórios, a ponte, a marca de obrigatória) | `.github/workflows/release.yml` |
| Canais visíveis para clientes antigos | `.github/workflows/sincronizar-atualizacoes.yml`, `tools/sincronizar-atualizacoes.py` |
| Videoaulas | `src/Clinica.Desktop.Shell/Treinamento/AcervoTreinamento.cs`, `.github/workflows/publicar-videoaulas.yml` |

## UX · DX · CX

- 🧑‍💻 **UX** — quem atende não vê diferença: a atualização continua chegando sozinha, durante e depois da troca.
- 🛠️ **DX** — o endereço mora em duas constantes (`FonteAtualizacaoGithub.Repositorio`/`RepositorioAnterior`);
  a regra da transição tem teste (`AtualizacaoCanaisTests`); o token só existe no Actions.
- 🤝 **CX** — o código deixa de ser público sem parar a clínica; o custo novo é o do Actions (acima), que
  tem três cortes baratos.
