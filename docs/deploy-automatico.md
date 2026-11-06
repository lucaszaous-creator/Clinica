# Deploy automático do portal e da gestão na VPS (nov/2026)

> Decisão da direção: **o merge na `main` publica** o portal (a API em `Clinica` + as telas em
> `clinica-site`) e a gestão web na VPS, sem aprovação manual.

## Como funciona

`.github/workflows/deploy-portal.yml` roda no merge na `main` do Clinica que mexe no portal ou na
gestão, **a cada 15 min** (para pegar o merge do `clinica-site`) e à mão (Run workflow, que
republica mesmo sem mudança).

1. **Há o que publicar?** Compara a `main` do `clinica-site` e o último commit do Clinica que toca
   o portal com o par já implantado. Par igual → para em segundos. Sem o secret `VPS_DEPLOY`, não
   publica nada.
2. **Conferir:** testes da API do portal, do tablet e da gestão web, o atualizador e o catálogo.
3. **Publicar:** empacota (o portal sem migration), entra pelo túnel da Cloudflare (Access, sem
   porta 22 aberta) e roda `sudo clinica-deploy portal|gestao`. Na VPS: confere o SHA-256, faz
   backup, troca o release, testa a saúde e **volta sozinho ao anterior se falhar**.

Um par que falhou é tentado de novo na janela seguinte de 6 h (ou já, com Run workflow). A
**migration** não é deste workflow: sai pelo release do app (o Gerente aplica ao abrir). Se o
portal precisar de tabela que ainda não existe, o atualizador recusa e nada muda. A gestão só é
atualizada depois de instalada uma vez à mão (`docs/gestao-web.md`); até lá, o deploy a pula.

## Configurar uma vez (só a VPS)

1. **Túnel:** em Zero Trust → Networks → Tunnels → o túnel da VPS → *Public hostname*, adicione
   `ssh.clinicasemdormacae.com.br` → `SSH` → `localhost:22`. Se o túnel é por `config.yml`, a regra
   equivalente vai antes da regra final.
2. **Access:** em Zero Trust → Access → *Service Auth*, crie um service token `github-deploy`. Em
   *Applications*, crie uma aplicação *Self-hosted* para `ssh.clinicasemdormacae.com.br`, com uma
   política **Service Auth** que inclua esse token.
3. **Na VPS, como root:**
   ```bash
   bash <(curl -fsSL https://raw.githubusercontent.com/lucaszaous-creator/Clinica/main/deploy/vps/preparar-vps.sh)
   ```
   O script pede o Client ID e o Client Secret do token e imprime **uma linha**.
4. **GitHub:** Clinica → Settings → Secrets and variables → Actions → *New repository secret*
   `VPS_DEPLOY` = a linha. Na próxima janela de 15 min, o deploy roda.

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

## UX · DX · CX

- 🧑‍💻 **UX:** a melhoria chega ao portal até cerca de 15 min depois do merge. Uma atualização que
  derrube a saúde volta sozinha. O risco que sobra é o defeito que **não** derruba a saúde, já que
  ninguém aprova antes.
- 🛠️ **DX:** acabou o roteiro de console a cada publicação. As peças são o workflow, o
  `clinica-deploy` e o `atualizar-gestao.py`. Não há token entre repositórios: o `clinica-site` não
  precisa avisar. A dispensa de homologação fica registrada no relatório como **dispensa**, nunca
  como aprovação.
- 🤝 **CX:** sem máquina nova. O job das 15 min para em segundos num runner Linux, então o custo é
  baixo. O merge passa a ser a decisão de publicar. Um secret só, e a chave da VPS só executa um
  comando, chegando pelo túnel.
