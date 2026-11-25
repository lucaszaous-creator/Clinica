# Deploy automático do portal, da gestão e do site público na VPS (nov/2026)

> Decisão da direção: **o merge na `main` publica** o portal (a API em `Clinica` + as telas em
> `clinica-site`) e a gestão web na VPS, sem aprovação manual.

## Como funciona

`.github/workflows/deploy-portal.yml` roda no merge na `main` do Clinica que mexe no portal ou na
gestão, **a cada 15 min** (para pegar o merge do `clinica-site`) e à mão (Run workflow, que
republica mesmo sem mudança).

1. **Há o que publicar?** Compara a `main` do `clinica-site` e o último commit do Clinica que toca
   o portal com o par já implantado. Par igual → para em segundos. Sem os secrets `VPS_DEPLOY` e
   `SITE_READ_TOKEN`, não publica nada.
2. **Conferir:** testes da API do portal, do tablet e da gestão web, o atualizador e o catálogo.
3. **Publicar:** empacota (o portal sem migration), entra pelo túnel da Cloudflare (Access, sem
   porta 22 aberta) e roda `sudo clinica-deploy portal|gestao`. Na VPS: confere o SHA-256, faz
   backup, troca o release, testa a saúde e **volta sozinho ao anterior se falhar**.

4. **Site público** (`clinicasemdormacae.com.br`): o branch `publicado` do `clinica-site`, que o CI
   de lá só gera quando o portão de conteúdo passa. Vai antes do portal, com marcador próprio, então
   mudar só o site não reinicia o portal. Na VPS, `clinica-deploy site` roda o `infra/atualizar.py`
   do `clinica-site`: confere cada arquivo pelo manifesto, prepara `releases/<commit>`, troca o
   `current` e volta ao anterior se o Nginx não responder. O `site-atualizar.timer` do roteiro antigo
   não é mais necessário.

Um par que falhou é tentado de novo na janela seguinte de 6 h (ou já, com Run workflow). A
**migration** não é deste workflow: sai pelo release do app (o Gerente aplica ao abrir). Se o
portal precisar de tabela que ainda não existe, o atualizador recusa e nada muda. A gestão entra
depois de `deploy/vps/instalar-gestao.sh` (uma vez, como root, com o portal novo no ar) e de uma rota
`^/gestao` no túnel acima da do portal; o deploy seguinte instala o primeiro release. Até lá, o
deploy a pula.

## Configurar uma vez (só a VPS)

1. **Túnel do SSH (à parte do túnel do site):** o `cloudflared-site` é proibido de abrir conexão TCP
   local (o `probe.py` confere a cada partida; é o que protege o banco), então o SSH tem conector
   próprio. Em Networking → Tunnels → **Create Tunnel** → Cloudflared, nome `clinica-ssh`; copie o
   token (o `eyJ…` do comando de instalação) e adicione a rota `ssh.clinicasemdormacae.com.br` →
   `ssh://localhost:22`. Na VPS, como root:
   `bash <(curl -fsSL https://raw.githubusercontent.com/lucaszaous-creator/Clinica/main/deploy/vps/tunel-ssh.sh)`.
2. **Access:** em Zero Trust → Access → *Service Auth*, crie um service token `github-deploy`. Em
   *Applications*, crie uma aplicação *Self-hosted* para `ssh.clinicasemdormacae.com.br`, com uma
   política **Service Auth** que inclua esse token.
3. **Na VPS, como root:**
   ```bash
   bash <(curl -fsSL https://raw.githubusercontent.com/lucaszaous-creator/Clinica/main/deploy/vps/preparar-vps.sh)
   ```
   O script pede o Client ID e o Client Secret do token e imprime **uma linha**.
4. **GitHub:** Clinica → Settings → Secrets and variables → Actions → *New repository secret*
   `VPS_DEPLOY` = a linha.
5. **Leitura do `clinica-site` (repositório privado):** em github.com → Settings → Developer
   settings → *Fine-grained tokens* → *Generate new token*, com o repositório `clinica-site` e a
   permissão **Contents: Read-only**, sem vencimento ou com vencimento longo. Salve no Clinica como
   o secret `SITE_READ_TOKEN`. Na próxima janela de 15 min, o deploy roda.

Pré-requisito do portal: `/opt/clinica-tablet/current` apontando para o release no ar. Em set/2026
ele não foi encontrado (`docs/retomada-publicacao-portal.md`). Sem isso, o deploy falha sem mexer
em nada.

## Se der errado

- **"Conferir" vermelho:** nada foi publicado. Corrija na `main`.
- **"Publicar" vermelho:** o atualizador já voltou ao release anterior. O log do passo e
  `/var/backups/clinica-tablet-continuidade-*` dizem o que houve.
- **Voltar uma versão:** reverta o commit na `main` (o deploy publica o revert) ou, no console,
  aponte `current` para o release anterior e reinicie o serviço.
- **Desligar o deploy automático:** apague o secret `VPS_DEPLOY`.
- **"A base contém credenciais protegidas; dispensa recusada sem exibir valores."** A publicação
  em produção sem homologação (`--pular-hml`) roda com a cifra de credenciais DESLIGADA e sem a
  chave, então recusa qualquer valor `enc:` em `Configuracoes` que o portal precisaria abrir. A
  regra mora em `deploy/tablet/regras_publicacao.py` (`verificar_sem_credenciais_cifradas`).

### A exceção do conector TISS (nov/2026)

O deploy de 18/11/2026 (run 44372401783) parou nesse passo por causa de `TissSenha:Amil` e
`TissSenha:Petrobras`, gravadas cifradas pelo conector TISS. Desde então a trava **dispensa só os
prefixos de `PREFIXOS_CIFRADOS_DO_CONECTOR_TISS`** — hoje **`TissSenha:`**, a única chave que o
`CredenciaisTissService` cifra (o login `TissLogin:` fica em claro) — e continua recusando qualquer
outra credencial cifrada, inclusive a mistura das duas.

- **Por que é seguro:** quem abre `TissSenha:*` é só o conector (`AbrirParaEnvioAsync`), no servidor
  dele, com a chave dele. O portal (`Clinica.Assinaturas.Api`) não referencia nada do TISS; a /gestao
  hospeda telas que só perguntam se a senha EXISTE (`ParaATelaAsync`: `!IsNullOrEmpty`, sem decifrar);
  `deploy/tablet` não lê essas chaves.
- **Um lugar só:** a lista é a mesma no C# (`CredenciaisTissService.PrefixosCifradosForaDoPortal`) e no
  Python; `CredenciaisCifradasForaDoPortalTests` falha se as duas divergirem, se o serviço cifrar uma
  chave fora delas ou se surgir um método que grava sem entrar no teste.
- **Testes:** `python3 -m unittest discover -s deploy/tablet -p 'test_*.py'` (a consulta de verdade, em
  SQLite: só `TissSenha:` passa; outra cifrada recusa com a mesma frase; a mistura recusa).

## UX · DX · CX

- 🧑‍💻 **UX:** a melhoria chega ao portal e ao site público até cerca de 15 min depois do merge. Uma atualização que
  derrube a saúde volta sozinha. O risco que sobra é o defeito que **não** derruba a saúde, já que
  ninguém aprova antes.
- 🛠️ **DX:** acabou o roteiro de console a cada publicação. As peças são o workflow, o
  `clinica-deploy` e o `atualizar-gestao.py`. O `clinica-site` não precisa avisar: o Clinica o lê com
  um token só de leitura. A dispensa de homologação fica registrada no relatório como **dispensa**, nunca
  como aprovação.
- 🤝 **CX:** sem máquina nova. O job das 15 min para em segundos num runner Linux, então o custo é
  baixo. O merge passa a ser a decisão de publicar. Dois secrets, e a chave da VPS só executa um
  comando, chegando pelo túnel.

## Disco da VPS: faxina e conferência (nov/2026)

**O que aconteceu em 24/11/2026:**
- O disco da VPS encheu: 82 releases em `/opt/clinica-tablet/releases` e os pacotes de todos os deploys em
  `/home/clinica-admin/tablet-stage`.
- O Postgres caiu às 19:40:23 (PANIC: sem espaço) e voltou às 19:40:56. Às 20:23 ele recusou conexões novas até a
  limpeza.
- O deploy da assinatura na gestão falhou no `scp`.

**O que mudou:**
- **Faxina depois de publicar** (`deploy/tablet/regras_publicacao.py` → `faxina_depois_de_publicar`, chamada no fim
  do `atualizar-posto.py`):
  - roda só depois de uma publicação saudável;
  - mantém o release novo, o anterior (o recuo) e os 5 mais novos, cada um com o seu `-runtime`;
  - apaga o pacote já instalado do stage;
  - se falhar, é aviso: nunca desfaz a publicação;
  - vai dentro do pacote, então não precisa reinstalar o `clinica-deploy`.
- **Espaço livre na VPS** (`deploy-portal.yml`): antes de qualquer `scp`, o workflow confere `df /`. Com menos de
  5G livres, para com "Disco da VPS quase cheio" e não copia nem troca nada.
- **Ficam para a direção decidir** (nada apagado):
  - `/opt/clinica-posto-hml/releases` (46 releases, 7,9G);
  - os backups por publicação em `/var/backups/clinica-tablet-continuidade-*` (81, cerca de 5G);
  - os dumps diários em `/var/backups/clinica` (1,5G).

**UX · DX · CX:**
- 🧑‍💻 **UX:** a clínica não fica sem banco por causa do disco do deploy.
- 🛠️ **DX:** a regra é pura e tem teste (`test_regras_publicacao.py`); o aviso do workflow diz o que fazer.
- 🤝 **CX:** um risco de produção a menos, sem custo de Actions.
