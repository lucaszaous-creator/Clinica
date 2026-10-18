# O CRM em produção: duas VPS, um túnel e o SMS 24h (out/2026)


> **A instalação real (17–18/10/2026)** — endereços, acessos, o que está ligado e como
> publicar de novo — está em [`crm-em-producao-out2026.md`](crm-em-producao-out2026.md).

> Decisão da direção: o CRM fica **na web** (como está), numa **VPS separada no Brasil**. A
> VPS da clínica (o Postgres do prontuário) não ganha nenhum site aberto. O lembrete por SMS
> sai **com as máquinas da clínica desligadas**. O WhatsApp fica para outro momento.

Este documento é o roteiro de instalação, comando por comando. Os arquivos que ele usa estão
em dois lugares:

| Onde | Arquivos |
|---|---|
| Clinica: `deploy/ponte-crm/` (VPS 1) | `clinica-crm-bridge.service`, `bridge.env.example`, `wg0-vps1.conf.example`, `papel-ponte-crm.sql` |
| semdor-crm: `deploy/vps/` (VPS 2) | `semdor-crm.service`, `production.env.example`, `Caddyfile.example`, `wg0-vps2.conf.example`, backup diário (`semdor-crm-backup.*`); contas: `tools/crm-contas.py` |

## 1. O desenho

```
VPS 1 · Clínica (como está)                         VPS 2 · CRM (nova)
┌──────────────────────────────┐                    ┌──────────────────────────────┐
│ Postgres (mTLS, intocado)    │                    │ Caddy :443 ── crm.dominio    │◄─ secretárias
│   ▲ socket local             │                    │       └────── avisos.dominio │◄─ webhooks do SMS
│ Ponte do CRM 10.8.0.1:5190   │◄── WireGuard ──────┤ CRM (127.0.0.1:8080 e :8081) │
│   + lembrete de SMS 24h ─────┼──► provedor SMS    │                              │
│   + respostas do link ◄──────┼─── balde R2 ◄──────┼─ Worker /c (Cloudflare)       │◄─ paciente
└──────────────────────────────┘                    └──────────────────────────────┘
```

- **O CRM nunca fala com o Postgres.** Ele pede à ponte ("agenda do dia", "sem vir há 15
  dias", "cancele este horário"), e a ponte executa com as regras do próprio Clinica
  (`AgendaService`, `ConsultaService`).
- **O túnel WireGuard** é uma rede privada cifrada só entre as duas VPS. A ponte escuta
  **apenas** em `10.8.0.1` e aceita **apenas** `10.8.0.2`. Endereço público é recusado na
  partida (`RedeDaPonte`). Cada chamada exige a chave da ponte.
- **O lembrete de SMS roda na ponte** (`LembreteSms__Ligado=true`): é o mesmo
  `LembreteSmsService` dos apps, a cada 5 min, 24h por dia. Os apps da Recepção e do Gerente
  continuam com o relógio deles, como caminho de baixo. A reserva de cada SMS é um índice
  único no banco, então os dois juntos **não mandam SMS duplicado**.
- **A resposta pelo link** ("Confirmo" / "Não poderei ir") continua passando pelo Worker e
  pelo balde que já estão no ar. A ponte a traz para a agenda a cada 5 min, também com a
  clínica fechada.
- **A resposta por SMS escrito** ("posso remarcar?") vai do provedor ao webhook do CRM e vira
  conversa na caixa de entrada.

## 2. O que contratar e separar antes

1. **VPS 2** no Brasil, Ubuntu 24.04 LTS: 2 vCPU, 2–4 GB de RAM, 40 GB de disco bastam.
2. **Dois subdomínios** apontando (registro A) para o IP da VPS 2: `crm.SEU-DOMINIO` (a tela)
   e `avisos.SEU-DOMINIO` (webhooks e mídia).
3. **Provedor de SMS** (Zenvia, Twilio ou API própria): token da API e remetente. Quem tem
   Twilio separa também o Account SID. **A mesma conta** vai em dois lugares: nas
   Configurações do Clinica (para o lembrete) e no CRM (para a conversa e o recall).
4. Do Clinica, já em uso: o **endereço público** e o **balde** da publicação (Configurações),
   e o **Worker** (`tools/worker-clinica.js`) com a rota `/c/*` publicada.

## 3. O túnel (as duas VPS)

Nas duas: `apt install -y wireguard`. Gere as chaves em cada uma:

```bash
umask 077; wg genkey | tee /etc/wireguard/privada | wg pubkey > /etc/wireguard/publica
```

Monte `/etc/wireguard/wg0.conf` a partir de `wg0-vps1.conf.example` (VPS 1) e
`wg0-vps2.conf.example` (VPS 2), trocando as chaves e o IP público da VPS 1. Depois, nas duas:

```bash
chmod 600 /etc/wireguard/wg0.conf
systemctl enable --now wg-quick@wg0
```

Firewall da **VPS 1** (o `ufw` do `banco-na-vps.md`): só a VPS 2 alcança o WireGuard, e só pelo
túnel se chega à ponte.

```bash
ufw allow from IP_PUBLICO_DA_VPS2 to any port 51820 proto udp
ufw allow in on wg0 from 10.8.0.2 to 10.8.0.1 port 5190 proto tcp
```

Teste na VPS 2: `ping -c 3 10.8.0.1`.

## 4. A ponte (VPS 1)

**4.1 O usuário do Postgres.** A ponte LÊ o banco inteiro, mas só GRAVA onde os fluxos dela
gravam: agenda, a guia que nasce ou é suspensa com ela, o contato da rodada, o SMS, o recall,
a categoria do paciente e a trilha (que só recebe INSERT). Usuários, prontuário e financeiro
ficam só para leitura: se a credencial vazar, o estrago não alcança o banco inteiro. Ela não
apaga nada, não cria tabela e não é dona de nada. A lista é conferida pelo
`PontePapelRestritoTests` (rode-o com `CLINICA_TESTES_POSTGRES` sempre que um fluxo da ponte
mudar). Rode o arquivo **depois** de o banco ter as tabelas (abra um dos apps uma vez) e
**de novo depois do script do posto** (`deploy/tablet`), que cria as do recall:

```bash
sudo -u postgres psql -p 45432 -d clinica -v senha="'SENHA_FORTE'" -f papel-ponte-crm.sql
```

No `pg_hba.conf`, **antes** das linhas `hostssl`: `local clinica ponte_crm scram-sha-256`. Depois,
`systemctl reload postgresql`. A ponte entra só pelo socket local, nunca por TCP.

**4.2 Publicar.** No computador de quem instala (Linux ou WSL, com o SDK 8):

```bash
dotnet publish src/Clinica.Crm.Bridge -c Release -r linux-x64 --self-contained -o ponte-$(git rev-parse --short HEAD)
```

Na VPS 1:

```bash
useradd --system --home-dir /nonexistent --shell /usr/sbin/nologin --user-group clinica-crm
mkdir -p /opt/clinica-crm-bridge/releases /etc/clinica-crm-bridge
# copie a pasta publicada para /opt/clinica-crm-bridge/releases/ponte-XXXXXXX e:
ln -sfn /opt/clinica-crm-bridge/releases/ponte-XXXXXXX /opt/clinica-crm-bridge/current
```

**4.3 Configurar.** `bridge.env.example` → `/etc/clinica-crm-bridge/bridge.env` (dono root,
`chmod 600`). Gere a chave com `openssl rand -base64 48`. Se a clínica ligou a proteção das
credenciais (`CLINICA_CREDENCIAIS_CRIPTOGRAFIA_HABILITADA`), acrescente as duas variáveis
`CLINICA_CREDENCIAIS_*`, com os mesmos valores dos apps. Sem elas, a ponte não lê o token do
SMS.

**4.4 Ligar.**

```bash
cp clinica-crm-bridge.service /etc/systemd/system/
systemctl daemon-reload && systemctl enable --now clinica-crm-bridge
journalctl -u clinica-crm-bridge -n 30
```

⚠️ **O serviço antigo** (`semdor-crm/deploy/crm/clinica-crm-bridge.service`) tinha
`IPAddressDeny=any` + `IPAddressAllow=localhost`. Com o lembrete ligado, isso bloquearia, sem
aviso, a saída para o provedor de SMS e para o balde, e também o túnel. O novo não tem as duas
linhas. A entrada fica trancada pelo endereço de escuta, pela lista de clientes, pela chave e
pelo ufw.

**4.5 O SMS no Clinica.** Em um app da Recepção: **Relacionamento → Canal de SMS →
Configuração**. Escolha o provedor, preencha, salve e clique em **Enviar SMS de teste** para
um celular da equipe. É essa configuração que a ponte usa: não há uma segunda.

**4.6 Conferir, da VPS 2:**

```bash
curl -s -H "Authorization: Bearer CHAVE" http://10.8.0.1:5190/crm/relationship | head -c 300
```

Sem a chave a resposta é 401. De qualquer outro IP, 403. Da internet a porta não responde.

## 5. O CRM (VPS 2)

**5.1 Base.**

```bash
apt install -y aspnetcore-runtime-8.0 caddy python3 ffmpeg ufw unattended-upgrades
ufw default deny incoming; ufw allow 22/tcp; ufw allow 80/tcp; ufw allow 443/tcp; ufw enable
useradd --system --home-dir /var/lib/semdor-crm --shell /usr/sbin/nologin --user-group semdor-crm
mkdir -p /opt/semdor-crm /var/lib/semdor-crm/data /var/lib/semdor-crm/keys /etc/semdor-crm /var/backups/semdor-crm
chown -R semdor-crm:semdor-crm /var/lib/semdor-crm /var/backups/semdor-crm
```

Copie para `/opt/semdor-crm/` os três scripts do repositório semdor-crm: `tools/crm-release.py`,
`tools/crm-backup.py` e `deploy/vps/semdor-crm-backup.sh`.

**5.2 O certificado das chaves de sessão** (obrigatório no Linux). **Guarde uma cópia fora da
VPS**: sem ele, todas as sessões caem e as chaves salvas ficam ilegíveis.

```bash
cd /etc/semdor-crm && umask 077
openssl req -x509 -newkey rsa:3072 -days 3650 -nodes -keyout k.pem -out c.pem -subj "/CN=semdor-crm-chaves"
openssl pkcs12 -export -out chaves.pfx -inkey k.pem -in c.pem -passout pass:SENHA_DO_PFX && rm k.pem c.pem
chgrp semdor-crm chaves.pfx && chmod 640 chaves.pfx
```

**5.3 As contas das secretárias.** A primeira conta é a administradora da equipe:

```bash
python3 /caminho/semdor-crm/tools/crm-contas.py /etc/semdor-crm/accounts.json ana "Ana • Recepção"
chgrp semdor-crm /etc/semdor-crm/accounts.json
```

**5.4 Configurar.** `production.env.example` → `/etc/semdor-crm/production.env` (dono root,
grupo `semdor-crm`, `chmod 640`). Use a **mesma** chave da ponte em `CRM_CLINIC_KEY` e deixe
`CRM_SEND_ENABLED=false`. Sem nenhuma variável `INFOBIP_*`, o WhatsApp fica desligado.
Configuração da Infobip pela metade é recusada na partida.

**5.5 Instalar a versão** (pacote verificado, como em `semdor-crm/docs/crm-implantacao-e-reversao.md`):

```bash
dotnet publish src/Clinica.Crm/Clinica.Crm.csproj -c Release -p:UseAppHost=false -p:IsTransformWebConfigDisabled=true -p:DebugType=None -p:DebugSymbols=false -o /tmp/crm-pub
python3 tools/crm-release.py pack --published /tmp/crm-pub --destination /tmp/crm-pacote --commit $(git rev-parse HEAD)
# na VPS 2, com o pacote copiado:
python3 /opt/semdor-crm/crm-release.py verify --package crm-pacote --manifest-sha256 HASH
python3 /opt/semdor-crm/crm-release.py stage --package crm-pacote --installation /opt/semdor-crm/installation --manifest-sha256 HASH
python3 /opt/semdor-crm/crm-release.py initialize --installation /opt/semdor-crm/installation --configuration /etc/semdor-crm/production.env --release-id ID
python3 /opt/semdor-crm/crm-release.py select --installation /opt/semdor-crm/installation --configuration /etc/semdor-crm/production.env --release-id ID --backup-destination /var/backups/semdor-crm/primeira-selecao
chown -R semdor-crm:semdor-crm /opt/semdor-crm/installation /var/lib/semdor-crm
```

**5.6 HTTPS e serviço.**

```bash
cp Caddyfile.example /etc/caddy/Caddyfile   # troque SEU-DOMINIO
systemctl reload caddy
cp semdor-crm.service semdor-crm-backup.service semdor-crm-backup.timer /etc/systemd/system/
systemctl daemon-reload && systemctl enable --now semdor-crm semdor-crm-backup.timer
curl -s https://crm.SEU-DOMINIO/health/ready
```

O Caddy tira o certificado sozinho. Para isso, os dois nomes têm de apontar para a VPS 2 e as
portas 80 e 443 precisam estar abertas.

**5.7 Webhooks no provedor de SMS:**
- respostas: `https://crm:SENHA_DO_WEBHOOK@avisos.SEU-DOMINIO/webhooks/sms/incoming`;
- entrega: `https://crm:SENHA_DO_WEBHOOK@avisos.SEU-DOMINIO/webhooks/sms/reports`.

O endereço `avisos.` só atende webhooks e mídia: a tela das secretárias não é alcançável por
ele.

## 6. Ligar por etapas

| Etapa | O que muda | Conferir antes de seguir |
|---|---|---|
| 1. Só leitura | nada envia | Relacionamento mostra "Atualizado com a clínica às…"; a agenda aparece |
| 2. Lembrete de SMS | SMS configurado no Clinica e o SMS de teste recebido; só então `LembreteSms__Ligado=true` na ponte (o exemplo nasce `false`) e `systemctl restart clinica-crm-bridge` | o SMS de teste chega; no dia seguinte, a coluna de confirmação da agenda mostra quem respondeu |
| 3. Envio pelo CRM | `CRM_SEND_ENABLED=true` no CRM | uma conversa por SMS com um celular da equipe, ida e volta |
| 4. Marcar pelo CRM | `CRM_BOOKING_ENABLED=true` nos **dois** lados | marcar um horário fictício e vê-lo na agenda do balcão |
| 5. Remarcar e cancelar | `CRM_SCHEDULING_ENABLED=true` nos **dois** lados | cancelar o fictício; a guia dele sai suspensa |

Cada mudança: editar o `.env` e `systemctl restart` do serviço daquele lado.

## 7. Atualizar e voltar atrás

- **Ponte:** publicar numa pasta nova em `releases/`, trocar o link `current` e reiniciar.
  Para voltar, aponte o `current` para a pasta anterior. O banco não muda: a ponte nunca roda
  migration.
- **CRM:** `stage` + `select` de uma versão nova e `rollback` para voltar
  (`crm-implantacao-e-reversao.md`). A reversão troca só o código.
- **A ponte acompanha a versão dos apps da clínica.** Ela é compilada com as mesmas
  bibliotecas, então republique-a sempre que sair uma release dos apps que mexa em agenda ou
  em SMS.

## 8. Backup

- **VPS 1:** o que já existe para o Postgres (`banco-na-vps.md`).
- **VPS 2:** o timer faz a cópia às 3h, confere e guarda as 14 mais recentes em
  `/var/backups/semdor-crm`. **Copiar para fora da VPS é tarefa à parte**, e o script não sabe
  se a pasta está em outro disco. O recomendado é sincronizar essa pasta e o `chaves.pfx`
  para um armazenamento da clínica.

## 9. Quando algo falha

| Sintoma | Onde olhar |
|---|---|
| Relacionamento diz "Não foi possível consultar o sistema da clínica" | `ping 10.8.0.1` na VPS 2; `systemctl status clinica-crm-bridge` e `journalctl` na VPS 1 |
| A ponte não sobe | a frase no `journalctl`: endereço não privado, chave curta, conexão ausente, WireGuard parado |
| O CRM recusa uma ação (409) | o **motivo** está no log da ponte ("Recusa em …") |
| O SMS não sai | `journalctl -u clinica-crm-bridge \| grep SMS`; o SMS de teste no app; o saldo no provedor |
| Ninguém consegue entrar no CRM | `accounts.json` legível pelo grupo `semdor-crm`; reinício depois de criar contas |

## 10. O que esta entrega corrigiu no caminho

- **Na VPS, a ponte devolvia 409 em confirmar, cancelar, remarcar, marcar e registrar recall.**
  O `AddClinica` liga o `EnableRetryOnFailure`, e o EF recusa transação aberta à mão sob
  estratégia que repete. Os testes da ponte não pegavam porque montam o `DbContext` sem
  estratégia. Agora os cinco caminhos passam por `TransacaoDaPonte`, e
  `PonteComRepeticaoTests` reprova sem a correção. Isso foi conferido rodando a ponte contra
  um Postgres com o usuário restrito acima.
- **A ponte engolia o motivo da recusa.** Agora ele vai para o log.
- **O serviço antigo bloquearia o SMS.** Ver 4.4.
- **Não havia como criar a conta de uma secretária** sem escrever o hash à mão. Agora existe
  `tools/crm-contas.py`, com teste provando que o login do CRM aceita o hash gerado.

## 11. UX · DX · CX

- 🧑‍💻 **UX** — a secretária abre `crm.SEU-DOMINIO` de qualquer computador da clínica, com o
  login dela. O paciente recebe o SMS na hora certa, inclusive no fim de semana, e a resposta
  dele aparece na agenda do balcão na segunda de manhã sem ninguém ter ligado nada.
- 🛠️ **DX** — nenhuma regra nova de agenda: a ponte chama os serviços do Clinica. O relógio do
  SMS é UMA classe (`RelogioDoSmsNoServidor`) usada pela ponte e pela web. Endereço e clientes
  da ponte são validados por `RedeDaPonte`, com teste. Cada passo do roteiro tem um comando de
  conferência.
- 🤝 **CX** — o prontuário continua numa máquina sem site aberto. O CRM, exposto à internet,
  não tem a senha do banco, e invadi-lo alcança só as poucas ações da ponte. O custo é a
  segunda VPS e o SMS por mensagem (uma parte por lembrete). Nada envia antes da etapa em que
  a clínica decide ligar.

## 12. O que NÃO foi feito, e por quê

- **WhatsApp:** decisão da direção. Ele entra depois, preenchendo as três variáveis `INFOBIP_*`
  ou trocando o transporte pela API da Meta, sem mudar o resto da instalação.
- **Monitor externo** (aviso quando `crm.SEU-DOMINIO/health/ready` cair): exige uma conta num
  serviço de monitoramento. É recomendado, mas não está configurado.
- **Cópia do backup para fora da VPS:** a ferramenta não decide para onde. Ver o §8.
- **Instalação automatizada por script:** os scripts antigos de `semdor-crm/deploy/crm/`
  ficaram presos a uma versão específica (hash e migration fixos). O roteiro acima é manual de
  propósito, com uma conferência por passo, até a primeira instalação dar certo.
