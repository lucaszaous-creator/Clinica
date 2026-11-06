# Deploy automático do portal e da gestão na VPS (nov/2026)

> Decisão da direção: **o merge na `main` publica** o portal (a API em `Clinica` + as telas em
> `clinica-site`) e a gestão web na VPS, sem aprovação manual.

## O que roda, e quando

| Gatilho | Onde | O que faz |
|---|---|---|
| merge na `main` do **Clinica** que mexe na API, na gestão, nas camadas compartilhadas ou em `deploy/` | `.github/workflows/deploy-portal.yml` | confere, empacota os dois repositórios e publica |
| merge na `main` do **clinica-site** que mexe em `portal/**` | `clinica-site/.github/workflows/disparar-deploy-portal.yml` | avisa o workflow do Clinica (`repository_dispatch`) |
| à mão | Actions → "Deploy do portal e da gestão" → Run workflow | o mesmo, quando precisar republicar |

**Passos do workflow:**

1. **Conferir:** testes da API do portal, do tablet e da gestão web (redes que o CI do PR não roda), o atualizador do posto e o catálogo da gestão.
2. **Empacotar:** portal sem migration (`-SemMigracao`) e gestão web.
3. **Publicar:** pelo túnel da Cloudflare (Access, sem porta 22 aberta), copia os pacotes e roda `sudo clinica-deploy portal|gestao`.

**Na VPS, para cada serviço:** confere o SHA-256, faz backup, troca o release, testa a saúde e
**volta sozinho para o anterior se falhar**. Um deploy por vez.

**O que NÃO é deste workflow:** os apps de computador e a **migration** do banco. A migration sai
pelo release do Velopack (Actions → "Release"). Se o portal precisar de tabela nova, publique antes o
app (o Gerente aplica a migration ao abrir). Caso contrário, o atualizador do posto **recusa**
("Tabela … ausente") e nada muda em produção.

## Configurar uma vez

### Na VPS (console root da Locaweb)

1. **Pré-requisito:** a estrutura que o atualizador espera precisa existir: `/opt/clinica-tablet/releases/<release>`
   com o link `current`, e `/opt/clinica-gestao/...` instalada à mão uma vez (`docs/gestao-web.md`).
   Em set/2026 o `current` do portal não foi encontrado (`docs/retomada-publicacao-portal.md`):
   alinhe isso antes. Enquanto não estiver alinhado, o deploy **falha sem mexer em nada**.
2. **Usuário de deploy, sem senha e sem shell de root:**
   ```bash
   useradd -m -s /bin/bash clinica-deploy
   install -d -m 0700 -o clinica-deploy -g clinica-deploy /home/clinica-deploy/stage /home/clinica-deploy/.ssh
   ssh-keygen -t ed25519 -N '' -C github-deploy -f /root/github-deploy        # a privada vai para o GitHub
   install -m 0600 -o clinica-deploy -g clinica-deploy /root/github-deploy.pub /home/clinica-deploy/.ssh/authorized_keys
   install -m 0755 -o root -g root deploy/vps/clinica-deploy /usr/local/sbin/clinica-deploy
   install -m 0440 -o root -g root deploy/vps/sudoers-clinica-deploy /etc/sudoers.d/clinica-deploy && visudo -cf /etc/sudoers.d/clinica-deploy
   ```
   Em `/home/clinica-deploy/.ssh/authorized_keys`, prefixe a chave com
   `restrict,from="127.0.0.1,::1"`: ela só vale chegando pelo túnel.
3. **SSH pelo túnel, sem porta 22 aberta:** no `config.yml` do cloudflared, acrescente
   `- hostname: ssh.clinicasemdormacae.com.br` → `service: ssh://localhost:22` (antes da regra final).
   No painel Zero Trust, crie a aplicação Access para esse endereço com a política **Service Auth**
   e um *service token* só para o GitHub.
4. **Impressão digital do servidor** para o GitHub conferir: `ssh-keyscan -t ed25519 localhost`.
   Troque `localhost` pelo endereço `ssh.clinicasemdormacae.com.br` na linha copiada.

### No GitHub

**Clinica** → Settings → Environments → **`producao`**:

- **Deployment branches:** só `main`. Sem *required reviewers*: a decisão foi publicar sem aprovação.
- **Secrets:**

| Secret | Valor |
|---|---|
| `VPS_SSH_HOST` | `ssh.clinicasemdormacae.com.br` |
| `VPS_SSH_KEY` | conteúdo de `/root/github-deploy` (a chave **privada**) |
| `VPS_KNOWN_HOSTS` | a linha do `ssh-keyscan` |
| `CF_ACCESS_CLIENT_ID` / `CF_ACCESS_CLIENT_SECRET` | o service token do Access |
| `SITE_READ_TOKEN` | token *fine-grained* com **leitura** de Contents do `clinica-site` |

**clinica-site** → Settings → Secrets → Actions:

| Secret | Valor |
|---|---|
| `CLINICA_DISPATCH_TOKEN` | token *fine-grained* com **Actions: write** (ou Contents: write) no `Clinica`, só para o dispatch |

Apague a chave privada de `/root` depois de colar no GitHub.

## Se der errado

- **Passo "Conferir" vermelho:** nada foi publicado. Corrija na `main`.
- **Passo "Publicar" vermelho:** o atualizador já voltou ao release anterior. O resumo do workflow e
  `/var/backups/clinica-tablet-continuidade-*` dizem o que houve.
- **Voltar uma versão:** o workflow sempre publica a `main`. Reverta o commit na `main` (o merge do
  revert publica) ou, no console, aponte `current` para o release anterior e reinicie o serviço.

## UX · DX · CX

- 🧑‍💻 **UX:** quem usa o portal recebe a melhoria minutos depois do merge. Uma atualização com
  defeito que derrube a saúde volta sozinha. O risco que sobra é o defeito que **não** derruba a
  saúde, já que ninguém aprova antes.
- 🛠️ **DX:** acabou o roteiro de console a cada publicação. As peças são o workflow, o comando
  `clinica-deploy` e o `atualizar-gestao.py`. O atualizador do posto continua o mesmo, com a dispensa
  de homologação registrada no relatório como **dispensa**, nunca como aprovação.
- 🤝 **CX:** sem custo de máquina, só minutos do Actions em runner Linux (barato). O merge passa a
  ser a decisão de publicar, então PR mal revisado vira produção. Os segredos ficam no Environment
  restrito à `main`, e a chave da VPS só executa um comando.
