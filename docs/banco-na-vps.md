# O banco numa VPS própria — conexão direta, porta trancada por certificado

**A decisão**: migrar o PostgreSQL da Neon para uma VPS Linux de preço fixo (Locaweb,
Hostinger ou equivalente, **datacenter no Brasil**), com o acesso protegido por
**mTLS** — TLS mútuo: além de o servidor provar quem é (o `VerifyFull` que o app já
exige), o servidor passa a **exigir que cada máquina cliente prove quem é**, com um
certificado emitido pela própria clínica. Quem não tem o certificado morre no aperto
de mão do TLS, antes de existir uma senha para tentar.

## Por que este desenho, e não os outros

Os requisitos que a decisão precisa atender, todos de uma vez:

| Requisito | Como o mTLS atende |
|---|---|
| **Custo fixo** | só a VPS. Nenhuma mensalidade além dela, nenhuma cobrança por uso ou por usuário. |
| **Conexão direta, sem instalar nada** | o "cliente TLS" é o próprio Npgsql — a mesma biblioteca que fala com a Neon hoje, já em produção. Nas máquinas ficam **três arquivos parados numa pasta**; não há app, serviço nem túnel para "parar de funcionar". |
| **Usuário remoto, IP e MAC dinâmicos** | a identidade é o certificado, não o endereço. Trocar de rede não muda nada. |
| **Seguro (LGPD)** | duas fechaduras independentes: certificado **e** senha SCRAM. Força bruta não existe — sem o arquivo do certificado, a conexão é recusada no handshake. E o dado passa a residir no Brasil, o que **remove** a transferência internacional do art. 33 (ver `docs/conformidade-lgpd.md`, item 10). |
| **Fácil de configurar como a Neon** | por máquina: copiar uma pasta + colar a connection string na tela de Setup de sempre. Zero mudança de código no sistema. |

O que se compara com o modelo atual: **a Neon é um Postgres aberto na internet,
protegido por TLS + senha.** Este desenho é o modelo da Neon **mais** uma fechadura
que a Neon não oferece. A porta responde a um scanner (isso é inevitável em conexão
direta sem VPN), mas responder não é ceder: toda tentativa sem certificado é
encerrada pelo Postgres no handshake, e a superfície que sobra exposta é a pilha
TLS do PostgreSQL/OpenSSL — a mesma que protege a Neon, o RDS e afins. A obrigação
contínua que isso cria é **uma**: manter o sistema atualizado (passo 2).

As alternativas descartadas, e por quê, para a próxima pessoa não reabrir o debate:

- **VPN (WireGuard/Tailscale)** — mais fechada ainda (porta nenhuma responde), mas
  exige um agente rodando em cada máquina, e a direção não quis peça nova que possa
  parar. Decisão de agosto/2026.
- **API HTTPS no meio** — é o desenho *mais* exposto (porta 443 respondendo a
  qualquer IP, com código nosso atrás) e custaria meses: o `IClinicaRepositorio`
  tem 234 métodos, 45 serviços gravam por change tracking do EF (179 `SalvarAsync`)
  e a concorrência por `xmin` não atravessa HTTP. Onde a indústria controla as duas
  pontas — banco↔banco no SPB/RSFN — ela usa rede privada ou mTLS, não API pública.
- **Firewall por IP de origem** — a clínica tem IP dinâmico; no dia em que trocar,
  o sistema inteiro para de abrir.

## O que muda de responsabilidade (leia antes de comprar)

Na Neon, backup e disponibilidade são problema de outro. Na VPS, **são nossos**:

1. **Backup diário para FORA da VPS é inegociável** — a guarda legal do prontuário é
   de 20 anos, e o `PoliticaBackupService` do Gerente passa a ser complemento, não
   única linha (passo 7).
2. Se a VPS cair, ninguém trabalha até ela voltar. O plano B honesto: o dump da
   véspera **sobe na Neon de novo em meia hora** (criar projeto grátis → `pg_restore`
   → trocar a connection string nas máquinas). Ter o plano escrito é o que o torna
   de meia hora.
3. Atualizações de segurança são automáticas (`unattended-upgrades`), mas alguém
   precisa olhar a VPS de vez em quando. Está no passo 8.

**Dimensão da VPS**: 2 vCPU / 2 GB RAM / 40 GB SSD atende com folga. O que cresce
com os anos são os anexos de prontuário gravados no banco (`AnexoProntuario`,
`ArquivoAssinado`); 40 GB dão anos de margem, e disco de VPS se expande.

---

## Passo 1 — Primeiro acesso e usuário

Assumindo Ubuntu 24.04 LTS (ou 22.04). Entre com o root que o painel der e:

```bash
adduser clinica-admin
usermod -aG sudo clinica-admin

# Chave SSH: gere na SUA máquina (ssh-keygen -t ed25519) e copie:
mkdir -p /home/clinica-admin/.ssh
echo "ssh-ed25519 AAAA... seu-comentario" > /home/clinica-admin/.ssh/authorized_keys
chown -R clinica-admin:clinica-admin /home/clinica-admin/.ssh
chmod 700 /home/clinica-admin/.ssh && chmod 600 /home/clinica-admin/.ssh/authorized_keys
```

Desligue senha e root no SSH (`/etc/ssh/sshd_config`):

```
PermitRootLogin no
PasswordAuthentication no
```

```bash
systemctl restart ssh
```

⚠️ **Teste o login por chave numa segunda janela ANTES de fechar a primeira.**

## Passo 2 — Firewall, atualizações automáticas e swap

```bash
apt update && apt upgrade -y
apt install -y ufw unattended-upgrades fail2ban

ufw default deny incoming
ufw default allow outgoing
ufw allow 22/tcp        # SSH (só chave)
ufw allow 45432/tcp     # PostgreSQL em porta alta — menos ruído de robô que 5432
ufw enable

dpkg-reconfigure -plow unattended-upgrades   # confirme "Yes"

# Swap de 2 GB: com 2 GB de RAM ele é o que impede o OOM killer de matar o Postgres
fallocate -l 2G /swapfile && chmod 600 /swapfile
mkswap /swapfile && swapon /swapfile
echo '/swapfile none swap sw 0 0' >> /etc/fstab
```

A porta 45432 no lugar de 5432 não é segurança — é higiene de log: os robôs varrem a
padrão, e log limpo é log que alguém lê.

## Passo 3 — PostgreSQL 16

```bash
apt install -y postgresql-16
# gere a senha com: openssl rand -hex 20 — só letras e números DE PROPÓSITO:
# caractere especial em senha acaba quebrando a connection string em alguma
# máquina, e o erro (28P01) só aparece lá na ponta, no Testar conexão
sudo -u postgres psql -c "CREATE USER clinica WITH PASSWORD 'GERE-UMA-SENHA-LONGA-AQUI' NOSUPERUSER;"
sudo -u postgres psql -c "CREATE DATABASE clinica OWNER clinica;"
```

Trocar a senha depois (ela vazou, ou por rotina) é um comando — e exige
reconfigurar as máquinas: `ALTER USER clinica WITH PASSWORD '...';`

Tuning para 2 GB de RAM, em `/etc/postgresql/16/main/postgresql.conf`:

```
listen_addresses = '*'
port = 45432
max_connections = 60
shared_buffers = 512MB
effective_cache_size = 1GB
work_mem = 8MB
maintenance_work_mem = 128MB
```

`max_connections = 60` supõe o pool capado nas máquinas (passo 6). O padrão do
Npgsql é **100 por app**; sem capar, cinco apps estouram qualquer limite razoável.

## Passo 4 — Os certificados (a fechadura)

Uma **CA própria da clínica** assina o certificado do servidor e um certificado
**por máquina**. Rode uma vez, na VPS, num diretório de trabalho:

```bash
mkdir -p ~/certs && cd ~/certs
IP_DA_VPS="203.0.113.10"   # troque pelo IP público real

# 1) A CA da clínica (10 anos) — a chave ca.key é a joia da coroa: fica SÓ na VPS
openssl req -new -x509 -days 3650 -nodes -newkey rsa:4096 \
  -keyout ca.key -out ca.crt -subj "/CN=CA Clinica"

# 2) Certificado do SERVIDOR (5 anos), com o IP no SAN — é o que o VerifyFull confere
openssl req -new -nodes -newkey rsa:2048 -keyout server.key -out server.csr \
  -subj "/CN=banco-clinica"
openssl x509 -req -in server.csr -CA ca.crt -CAkey ca.key -CAcreateserial \
  -days 1825 -out server.crt -extfile <(echo "subjectAltName=IP:$IP_DA_VPS")

# 3) Um certificado de CLIENTE por máquina (5 anos). Repita por máquina, mudando o nome:
MAQUINA="recepcao-01"
openssl req -new -nodes -newkey rsa:2048 -keyout $MAQUINA.key -out $MAQUINA.csr \
  -subj "/CN=$MAQUINA"
openssl x509 -req -in $MAQUINA.csr -CA ca.crt -CAkey ca.key -CAcreateserial \
  -days 1825 -out $MAQUINA.crt

# 4) Empacote para o Windows (.pfx) — o formato que o Npgsql carrega sem fricção lá.
#    A senha do .pfx protege o arquivo em trânsito; anote-a, ela vai na connection string.
openssl pkcs12 -export -out $MAQUINA.pfx -inkey $MAQUINA.key -in $MAQUINA.crt
```

Instale os do servidor:

```bash
mkdir -p /etc/postgresql/certs
cp ca.crt server.crt server.key /etc/postgresql/certs/
chown postgres:postgres /etc/postgresql/certs/*
chmod 600 /etc/postgresql/certs/server.key
```

E em `postgresql.conf`:

```
ssl = on
ssl_cert_file = '/etc/postgresql/certs/server.crt'
ssl_key_file  = '/etc/postgresql/certs/server.key'
ssl_ca_file   = '/etc/postgresql/certs/ca.crt'
ssl_min_protocol_version = 'TLSv1.2'
```

(`TLSv1.2`, não 1.3: máquina com Windows 10 no balcão não fala 1.3 no Schannel.)

⚠️ **Anote as datas num lugar que alguém olhe** (o calendário da clínica serve):
certificado é a única peça deste desenho que "para de uma hora para outra" — no
vencimento. Emitidos em 2026 com os prazos acima: **clientes e servidor vencem em
2031, a CA em 2036**. Renovar é rodar os mesmos comandos e redistribuir os arquivos.

**Máquina roubada ou pessoa desligada**: revoga-se **só aquele** certificado —
gere uma CRL (`openssl ca -gencrl`) e aponte `ssl_crl_file` para ela. As outras
máquinas nem percebem. (Na Neon, o equivalente seria trocar a senha de todo mundo.)

## Passo 5 — `pg_hba.conf`: as duas fechaduras

Substitua as linhas de acesso remoto em `/etc/postgresql/16/main/pg_hba.conf` por:

```
# Local (administração na própria VPS)
local   all      postgres                 peer
local   all      clinica                  scram-sha-256

# Remoto: SÓ com TLS + certificado assinado pela nossa CA + senha. Nada além disso.
hostssl clinica  clinica  0.0.0.0/0       scram-sha-256  clientcert=verify-ca
hostssl clinica  clinica  ::/0            scram-sha-256  clientcert=verify-ca
```

O que esta configuração afirma: o superusuário `postgres` **não existe
remotamente**; o usuário `clinica` só entra no banco `clinica`, só por TLS, só
apresentando certificado da nossa CA **e** a senha. Não há linha `host` sem SSL.

```bash
systemctl restart postgresql
```

## Passo 6 — As máquinas da clínica

O certificado identifica a **MÁQUINA, não a pessoa**: as duas secretárias do
balcão usam o certificado do computador do balcão, e quem responde "quem fez
isso?" continua sendo o login do app (`SessaoUsuario`), como sempre. Usuário
novo no sistema não gera trabalho nenhum nesta camada.

A string é a mesma para todas as máquinas, mudando só o nome do `.pfx`:

```
Host=IP-DA-VPS;Port=45432;Database=clinica;Username=clinica;Password=SENHA-DO-BANCO;SSL Mode=VerifyFull;Root Certificate=C:\ClinicaDB\ca.crt;SSL Certificate=C:\ClinicaDB\recepcao-01.pfx;SSL Password=SENHA-DO-PFX;Maximum Pool Size=10
```

String em formato Npgsql atravessa `ConexaoStore.Normalizar` intocada, então nada
disso exige mudança de código; o `VerifyFull` continua valendo (valida o `server.crt`
contra o `ca.crt` e confere o IP no SAN) e o `Maximum Pool Size=10` é o que faz as
contas do passo 3 fecharem.

### Rota A — poucas máquinas: a tela de Setup

1. Crie `C:\ClinicaDB\` e copie `ca.crt` + o `maquina.pfx` dela (restrinja a
   pasta ao usuário do Windows em propriedades → segurança);
2. Abra o app → tela de Setup → cole a string → **Testar conexão** → Salvar.

A string fica cifrada por DPAPI, **por usuário do Windows** — é a rota de maior
proteção, e o preço é repetir o Setup para cada usuário do Windows da máquina.

### Rota B — a frota inteira: `tools/vps/montar-kit.sh`, um comando

Para dezenas de máquinas, UM comando na VPS monta o kit completo, **sem nada a
preencher em lugar nenhum** (o script se instala com um colar no terminal):

```bash
cd ~/certs && ./montar-kit.sh 25
```

Ele gera a pilha de certificados anônimos (`maquina-01.pfx` …), **troca a senha
do usuário `clinica` do banco por uma nova aleatória** (pede a senha do sudo —
autenticação, não configuração) e escreve, dentro do próprio kit, o
`instalar-maquina.bat` com IP, porta e as duas senhas **já embutidas**. Gere
com folga: certificado sobrando é o da próxima máquina nova.

Depois: `scp -r clinica-admin@IP-DA-VPS:certs/kit C:\kit` → pendrive → em cada
máquina, **botão direito no .bat → Executar como administrador — e nada mais**.
O instalador pega o primeiro `.pfx` livre da pilha, copia os arquivos para
`C:\ClinicaDB`, grava a string na variável de ambiente da máquina
(`ConnectionStrings__Clinica`) — que o app lê antes de qualquer configuração
salva e **pula a tela de Setup** —, move o certificado para `usados\` e anota
no `registro.txt` qual computador ficou com qual certificado. **A planilha de
revogação se escreve sozinha.** Ele também testa o alcance TCP da porta antes
(rede de clínica às vezes bloqueia porta alta de saída — melhor saber na
instalação do que no primeiro atendimento) e se recusa a rodar duas vezes na
mesma máquina, para não gastar dois certificados da pilha.

⚠️ **Rodar o .bat É virar a máquina** — só rode depois da migração (passo 7).
⚠️ **Use a MESMA pasta de kit para todas** (o pendrive que viaja): é o
mover-para-`usados\` que impede duas máquinas de levarem o mesmo certificado;
cópias separadas do kit quebram essa garantia.
⚠️ **O montar-kit não roda duas vezes por cima de máquinas instaladas** — ele
vê o `registro.txt` e recusa, porque regenerar trocaria a senha do banco por
baixo delas. Para só engrossar a pilha depois, gere certificados avulsos com os
comandos do passo 4 usando a MESMA senha de pfx (ela está dentro do
`instalar-maquina.bat` do kit).

O custo da rota B, dito por inteiro: as senhas ficam legíveis dentro do `.bat`
do kit e na variável de ambiente das máquinas (a rota A guarda cifrado por
DPAPI). O `.pfx` já mora no mesmo disco de qualquer forma, então o degrau real
é pequeno — mas numa máquina de uso público, prefira a rota A. Trate o pendrive
do kit como chave da clínica e guarde o `registro.txt`: é ele que torna a
revogação de um notebook roubado um ato de um minuto em vez de uma
investigação.

## Passo 7 — Migração da Neon (a ordem importa)

⚠️ **Restaure os dados ANTES de qualquer app abrir apontando para a VPS.** As
migrations rodam no `MigrateAsync` da abertura: um app aberto antes da hora cria o
schema vazio, e o restore por cima de banco mexido é conflito na certa.

O `tools/vps/migrar-da-neon.sh` faz o ciclo completo na VPS (ela alcança a Neon
de SAÍDA; a porta trancada é a de entrada): pergunta a connection string da
Neon, tira a foto (`pg_dump -Fc` — a Neon é só lida, a clínica pode estar
usando), **apaga e recria o banco local** e restaura por cima, terminando com a
contagem de linhas por tabela para conferência. Por apagar e recriar, ele pode
rodar quantas vezes quiser — o ensaio e a virada são o MESMO comando, só muda a
hora: a última rodada é fora do expediente, porque o que se escreve na Neon
depois da foto não viaja junto.

Valide antes de virar a clínica: numa máquina só, com a env var (que vence a config
salva e **não grava** — o mecanismo de `docs/testar-sem-publicar.md`):

```powershell
$env:ConnectionStrings__Clinica = "Host=...;Port=45432;...igual ao passo 6..."
```

Abra o app, confira pacientes, agenda, uma pendência, um PDF. Só então configure as
demais máquinas pela tela de Setup. Mantenha a Neon viva (ou um dump dela guardado)
por 30 dias antes de encerrar a conta.

Depois da virada, atualize `docs/conformidade-lgpd.md`: o suboperador do item 10
deixa de ser a Neon, e a pendência de transferência internacional (art. 33) **sai
da lista** — o dado passou a residir no Brasil.

## Passo 8 — Backup: a parte que não é opcional

```bash
cat > /usr/local/bin/backup-clinica.sh <<'EOF'
#!/bin/bash
set -e
DIA=$(date +%F)
sudo -u postgres pg_dump -Fc clinica > /var/backups/clinica/clinica-$DIA.dump
# guarda 14 dias na VPS; a cópia de VERDADE é a que sai dela (linha do rclone)
find /var/backups/clinica -name '*.dump' -mtime +14 -delete
rclone copy /var/backups/clinica/clinica-$DIA.dump destino:backup-clinica/
EOF
chmod +x /usr/local/bin/backup-clinica.sh
mkdir -p /var/backups/clinica
echo '30 2 * * * root /usr/local/bin/backup-clinica.sh' > /etc/cron.d/backup-clinica
```

O `destino:` do rclone é qualquer storage S3-compatível — o projeto já validou o
Cloudflare R2 ao vivo (parcela 53), e os 10 GB gratuitos dele seguram anos de dumps.
Backup que mora só no disco da própria VPS morre junto com ela.

**Teste a restauração uma vez por trimestre** (`pg_restore` num banco `clinica_teste`
na própria VPS). Backup nunca testado é esperança, não backup. O `clinica_teste`
também substitui a branch da Neon para os testes de versão de
`docs/testar-sem-publicar.md`.

## Se algo parar — sintoma → causa → conserto

| Sintoma | Causa provável | Conserto |
|---|---|---|
| "certificate has expired" no Testar conexão | venceu um certificado (ver datas do passo 4) | reemitir com os mesmos comandos e redistribuir |
| "connection requires a valid client certificate" | máquina sem o `.pfx`, caminho errado na string, ou cert revogado | conferir `C:\ClinicaDB\` e a string |
| App não abre em nenhuma máquina | VPS fora do ar | painel/ticket do provedor; se demorar: plano B — restaurar o último dump num projeto Neon grátis e trocar a string |
| "too many connections" | pool sem `Maximum Pool Size` em alguma máquina | conferir a string das máquinas (passo 6) |
| Lentidão geral | swap exaurido / RAM | `free -h` na VPS; subir plano de RAM é upgrade de painel |
