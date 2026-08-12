# Banco próprio numa VPS Linux (Locaweb) — o que muda, o que é preciso, como testar

> **A pergunta que originou este documento foi "conseguimos conectar ao banco da VPS via API?".**
> A resposta curta é que **não existe API entre o sistema e o banco, e não precisa existir**:
> os cinco aplicativos falam **direto** com o PostgreSQL pelo driver Npgsql (TCP 5432), e
> trocar a Neon por uma VPS é **trocar a connection string**. A parte longa — o que a VPS
> precisa ter para essa string funcionar com segurança — é o resto deste arquivo.

---

## 1. Por que não há API (e o que custaria haver)

O ponto único de acesso a dados é `IClinicaRepositorio`, implementado por
`ClinicaRepositorio` sobre EF Core + Npgsql. `DependencyInjection.AddClinica` recebe **uma
string** e registra o `DbContext` com ela; os ~60 serviços de aplicação conversam com o
repositório em memória, no mesmo processo.

Pôr uma API HTTP no meio significaria:

- escrever um projeto ASP.NET Core novo, hospedado na VPS;
- reimplementar `IClinicaRepositorio` inteiro sobre HTTP, em cada um dos cinco executáveis;
- reproduzir do outro lado o que o EF resolve hoje sozinho — projeção, paginação, o
  `SaveChanges` **atômico** que a auditoria exige (regra: a trilha grava no MESMO
  `SaveChanges` do ato), a concorrência otimista por `xmin` e o advisory lock das migrations.

Isso é a reescrita da camada de dados do produto, e **não resolve nenhum problema que a
clínica tenha hoje**. Uma API se justificaria para atender cliente web ou celular — não é o
caso: são cinco aplicativos WPF instalados em máquinas Windows da própria clínica.

**O que a VPS muda é o endereço do banco. Só isso.**

## 2. As especificações

| Item | Pedido | Veredito |
|---|---|---|
| 2 vCPUs | ✅ sobra | uma clínica com 5–10 postos não passa perto do limite |
| 40 GB SSD | ✅ serve por anos — **com uma ressalva**, abaixo | |
| Transferência ilimitada | ✅ irrelevante aqui | o tráfego é de consulta SQL, não de vídeo |
| **RAM** | ❓ **não foi informada — e é o número que decide** | **4 GB recomendado; 2 GB é o piso** |

**A RAM é a única especificação que aperta.** PostgreSQL vive de cache: com 2 GB ele roda,
com 4 GB ele roda folgado e os relatórios do Gerente (que varrem meses de lançamentos)
param de ir ao disco. Se a diferença de preço entre 2 GB e 4 GB for pequena, é onde o
dinheiro rende mais — mais do que em vCPU.

### A ressalva do disco: **este banco guarda arquivos binários**

Quatro tabelas gravam bytes dentro do banco, e é isso que faz o disco crescer:

| O quê | Onde | Tamanho típico |
|---|---|---|
| Miniatura da foto do paciente (~160 px) | `Paciente.FotoMiniatura` | ~8 KB por paciente |
| Foto do paciente (~640 px) | `PacientesFotos.Conteudo` | ~60–100 KB por paciente |
| **Anexos do prontuário** (laudo, exame) | `AnexoEvolucao.Conteudo` | **100 KB – 5 MB cada** |
| PDF assinado (receita, atestado) | `ArquivoAssinado.Conteudo` | ~100–300 KB cada |

Fotos e PDFs assinados são previsíveis: 5.000 pacientes ≈ 500 MB; 5.000 documentos
assinados por ano ≈ 1,5 GB/ano. **O que pode estourar 40 GB são os anexos** — se a clínica
passar a anexar PDF de ressonância no prontuário, a conta muda de ordem de grandeza.

Some a isso o **backup**: se as cópias ficarem no mesmo disco (e a rotação do
`PoliticaBackupService` guarda várias), cada cópia é uma base inteira a mais.

**Conclusão:** 40 GB serve, desde que o backup vá para **fora da VPS** — que é o que a
política de backup já manda fazer, e o que o item 8 da conformidade LGPD cobra.

## 3. O que a clínica ganha e o que passa a dever

### Ganha: a pendência de **transferência internacional** desaparece

Está escrito em `docs/conformidade-lgpd.md`, ponto 10, como uma das duas pendências que
**não são código**:

> O banco de dados fica **fora do Brasil**. Prontuário de paciente brasileiro hospedado no
> exterior é **transferência internacional de dados** (LGPD, art. 33) e exige base legal
> específica.

O documento já previa dois caminhos: cobrir contratualmente com as cláusulas-padrão da
ANPD, **ou migrar de região**. Uma VPS da Locaweb (datacenter no Brasil) **é o segundo
caminho**, e ele fecha o assunto sem contrato nenhum. Numa auditoria de fornecedor feita
pela própria cliente, isso não é detalhe: é um ✅ trocado por escrito.

⚠️ **Confirme o datacenter na contratação.** O argumento inteiro depende de o servidor estar
em território nacional — a Locaweb tem estrutura no Brasil, mas isso é o que precisa estar
no contrato, não uma suposição.

### Deve: **backup e disponibilidade passam a ser da clínica**

A Neon faz *point-in-time recovery* sozinha. **Uma VPS crua não faz nada.** Se o disco
corromper numa terça-feira e não houver cópia, o prontuário de 20 anos que a Lei 13.787/2018
manda guardar acabou — e não há suporte para ligar.

O sistema já tem a ferramenta e a política (`BackupService`, `PoliticaBackupService`), mas
elas rodam **na máquina do Gerente**, puxando a base pela rede. Numa VPS, o certo é somar a
isso um `pg_dump` no próprio servidor, agendado, com a cópia saindo do servidor (item 6
abaixo). Regra que já vale no projeto e vale em dobro aqui: **guardar só a última cópia é o
erro clássico** — a corrupção que ninguém viu na sexta é copiada por cima da única cópia boa
no sábado.

E não há mais quem reinicie a máquina de madrugada. Para uma clínica isso costuma ser
aceitável; só precisa ser uma decisão, não uma descoberta.

## 4. O que é preciso ter — a lista

| # | Item | Por quê |
|---|---|---|
| 1 | VPS Linux com **root** (Ubuntu 24.04 LTS) | precisamos instalar e configurar o Postgres |
| 2 | **Um nome de domínio** apontando para o IP da VPS | **crítico** — ver o item 5 |
| 3 | IP fixo (a Locaweb dá) | o domínio precisa apontar para algo estável |
| 4 | PostgreSQL 16 | é o que a Ubuntu 24.04 traz e o que o projeto usa |
| 5 | Certificado TLS válido (Let's Encrypt) | **crítico** — ver o item 5 |
| 6 | Firewall liberando 5432 (e 22, e 80 só para emitir o certificado) | |
| 7 | Rotina de backup **para fora da VPS** | item 3 acima |

### ⚠️ O item 2 é o que costuma ser esquecido: **precisa de um domínio, não basta o IP**

O sistema conecta com `SslMode = VerifyFull` (decisão da parcela 52, escrita em
`ConexaoStore.Normalizar`): ele **criptografa e confere o certificado do servidor**,
inclusive o nome do host. É o que impede alguém no meio do caminho se apresentar como sendo
o banco — e é prontuário que passa por esse cano.

Consequências práticas, as duas:

- **O Let's Encrypt não emite certificado para endereço IP.** Sem um domínio
  (`banco.suaclinica.com.br`, por exemplo), não há certificado válido, e com certificado
  autoassinado a conexão **falha** — dizendo o que é, que é bem melhor do que aceitar
  qualquer um em silêncio, mas falha.
- **A connection string tem de usar o domínio**, não o IP: o `VerifyFull` compara o nome
  que você digitou com o nome dentro do certificado.

Um subdomínio de um domínio que a clínica já tenha resolve, e custa zero.

## 5. Provisionamento — os comandos

> Tudo abaixo roda **na VPS**, por SSH, como root. Troque `banco.suaclinica.com.br` pelo
> domínio real e escolha uma senha forte para o usuário do banco.

### 5.1 Sistema e PostgreSQL

```bash
apt update && apt upgrade -y
apt install -y postgresql postgresql-contrib certbot ufw fail2ban
systemctl enable --now postgresql
psql --version   # deve dizer 16.x
```

### 5.2 Certificado TLS (Let's Encrypt)

O DNS do domínio já precisa apontar para o IP da VPS antes deste passo.

```bash
ufw allow 80/tcp                      # temporário, só para a emissão
certbot certonly --standalone -d banco.suaclinica.com.br --agree-tos -m voce@suaclinica.com.br -n
```

O Postgres precisa **ler** os arquivos, e ele não roda como root. O jeito que sobrevive à
renovação automática é um *deploy hook* que copia e ajusta a dona a cada renovação:

```bash
mkdir -p /etc/letsencrypt/renewal-hooks/deploy
cat > /etc/letsencrypt/renewal-hooks/deploy/postgres.sh <<'EOF'
#!/bin/bash
set -e
DOMINIO=banco.suaclinica.com.br
DESTINO=/var/lib/postgresql/tls
mkdir -p "$DESTINO"
cp "/etc/letsencrypt/live/$DOMINIO/fullchain.pem" "$DESTINO/server.crt"
cp "/etc/letsencrypt/live/$DOMINIO/privkey.pem"   "$DESTINO/server.key"
chown -R postgres:postgres "$DESTINO"
chmod 600 "$DESTINO/server.key"
systemctl reload postgresql
EOF
chmod +x /etc/letsencrypt/renewal-hooks/deploy/postgres.sh
/etc/letsencrypt/renewal-hooks/deploy/postgres.sh   # roda uma vez agora
ufw delete allow 80/tcp
```

⚠️ **Sem o hook, a conexão de todos os aplicativos para de funcionar em 90 dias**, que é a
validade do certificado — e para num dia qualquer, sem ninguém ter mexido em nada.

### 5.3 Configuração do Postgres

Em `/etc/postgresql/16/main/postgresql.conf`:

```conf
listen_addresses = '*'
password_encryption = scram-sha-256

ssl = on
ssl_cert_file = '/var/lib/postgresql/tls/server.crt'
ssl_key_file  = '/var/lib/postgresql/tls/server.key'
ssl_min_protocol_version = 'TLSv1.2'

# Ajuste para 4 GB de RAM. Com 2 GB, use 512MB / 1536MB / 8MB.
shared_buffers = 1GB
effective_cache_size = 3GB
work_mem = 16MB
maintenance_work_mem = 256MB
max_connections = 100

# Deixa rastro de quem conectou e do que demorou — sem isso, investigar é adivinhar.
log_connections = on
log_disconnections = on
log_min_duration_statement = 2000
```

Em `/etc/postgresql/16/main/pg_hba.conf`, **`hostssl` e nunca `host`** — `host` aceitaria
conexão sem criptografia, e é justamente o que não pode:

```conf
# TIPO      BANCO     USUÁRIO   ENDEREÇO      MÉTODO
local       all       postgres                peer
hostssl     clinica   clinica   0.0.0.0/0     scram-sha-256
```

> Se a clínica tiver **IP fixo**, troque `0.0.0.0/0` pelo IP dela — é a linha de defesa mais
> barata que existe. Com IP dinâmico (o caso comum), fica `0.0.0.0/0` e a proteção passa a
> ser a senha forte + TLS + fail2ban.

### 5.4 Banco e usuário

```bash
sudo -u postgres psql <<'EOF'
CREATE ROLE clinica LOGIN PASSWORD 'TROQUE-POR-UMA-SENHA-LONGA-E-ALEATORIA';
CREATE DATABASE clinica OWNER clinica;
EOF
systemctl restart postgresql
```

### 5.5 Firewall

```bash
ufw allow 22/tcp
ufw allow 5432/tcp
ufw --force enable
ufw status
```

## 6. Backup no servidor

`pg_dump` diário, comprimido, com rotação de 14 dias:

```bash
mkdir -p /var/backups/clinica
cat > /usr/local/bin/backup-clinica.sh <<'EOF'
#!/bin/bash
set -e
DESTINO=/var/backups/clinica
ARQUIVO="$DESTINO/clinica-$(date +%Y%m%d-%H%M).dump"
sudo -u postgres pg_dump -Fc clinica > "$ARQUIVO"
find "$DESTINO" -name 'clinica-*.dump' -mtime +14 -delete
EOF
chmod +x /usr/local/bin/backup-clinica.sh
echo "0 2 * * * root /usr/local/bin/backup-clinica.sh" > /etc/cron.d/backup-clinica
```

⚠️ **Isto ainda NÃO é backup.** Cópia no mesmo disco morre com o disco. O passo que falta é
mandar `/var/backups/clinica` para fora — `rclone` para o mesmo bucket S3 que a publicação
de receitas já usa, ou para qualquer nuvem. **Enquanto isso não estiver feito, a clínica
tem uma cópia, não um backup**, e é assim que se escreve para ela — a regra do projeto de
não prometer garantia que o código não dá.

E **backup que nunca foi restaurado não é backup**: restaure uma vez, num banco de teste,
antes de confiar.

## 7. Migrar os dados que já existem na Neon

Só se a clínica já estiver em produção na Neon. Base nova não precisa disto — **os
aplicativos criam o esquema sozinhos** na primeira abertura (`ShellBootstrap.PrepararBancoAsync`
→ `MigrateAsync`, sob advisory lock).

```bash
# Com todos os aplicativos FECHADOS, para não perder o que for gravado durante a cópia.
pg_dump -Fc "postgresql://USUARIO:SENHA@ep-xxx.neon.tech/clinica?sslmode=require" > neon.dump
pg_restore -d "postgresql://clinica:SENHA@banco.suaclinica.com.br/clinica?sslmode=verify-full" \
  --no-owner --no-privileges neon.dump
```

Confira as contagens dos dois lados antes de desligar a Neon — pacientes, atendimentos,
códigos de faturamento, evoluções:

```sql
SELECT 'Pacientes', count(*) FROM "Pacientes"
UNION ALL SELECT 'Atendimentos', count(*) FROM "Atendimentos"
UNION ALL SELECT 'CodigosFaturamento', count(*) FROM "CodigosFaturamento"
UNION ALL SELECT 'Evolucoes', count(*) FROM "Evolucoes";
```

## 8. Testar a conexão do sistema

### 8.1 Do próprio servidor (prova que o Postgres está de pé)

```bash
sudo -u postgres psql -c "SELECT version();"
```

### 8.2 De fora, antes de mexer em aplicativo nenhum

De qualquer máquina com `psql` — inclusive Linux:

```bash
psql "postgresql://clinica:SENHA@banco.suaclinica.com.br:5432/clinica?sslmode=verify-full" \
  -c "SELECT current_database(), inet_server_addr();"
```

**Este é o teste que importa**, porque `sslmode=verify-full` é exatamente o que o sistema
usa. Se ele passa, o aplicativo conecta.

O que cada falha quer dizer:

| Erro | Causa provável |
|---|---|
| `connection timed out` | firewall (ufw) ou `listen_addresses` |
| `no pg_hba.conf entry for host` | falta a linha `hostssl` — ou está `host` |
| `server does not support SSL` | `ssl = on` não subiu; confira os arquivos do certificado |
| `certificate verify failed` | certificado autoassinado, ou expirado |
| `server certificate for "X" does not match host name "Y"` | você conectou pelo **IP** em vez do domínio |
| `password authentication failed` | senha, ou `password_encryption` diferente de scram |

### 8.3 Pelo sistema

Duas formas, e a primeira **não grava nada** — é a que serve para testar sem mexer na
instalação de produção (mesmo roteiro de `docs/testar-sem-publicar.md`):

```powershell
# A variável de ambiente vence a configuração salva e NÃO escreve em %APPDATA%.
$env:ConnectionStrings__Clinica = "postgresql://clinica:SENHA@banco.suaclinica.com.br/clinica"
.\Clinica.Recepcao.exe
```

A segunda é a tela de **Setup**, que grava — use só quando a decisão de migrar estiver
tomada. Cole a URI `postgresql://…` e clique em **Testar** antes de salvar.

⚠️ **Cole no formato URI (`postgresql://…`), não no formato `Host=…;`.** É o
`ConexaoStore.Normalizar` que aplica o `SslMode = VerifyFull`, e ele **só faz isso quando a
entrada é uma URI** — uma string `Host=…` sem `SSL Mode` passa direto, e o padrão do Npgsql
8 (`Prefer`) aceita conexão **sem criptografia nenhuma** se o servidor não oferecer TLS.
Isso vale hoje; ver a pendência no fim deste documento.

### 8.4 Sugestão para a string de produção

```
postgresql://clinica:SENHA@banco.suaclinica.com.br:5432/clinica?sslmode=verify-full&Maximum%20Pool%20Size=20
```

O teto de pool existe porque o padrão do Npgsql é **100 conexões por processo**: cinco
máquinas com dois aplicativos abertos poderiam, no limite, pedir mais conexões do que o
`max_connections` do servidor. Vinte por processo é folga de sobra para o uso real e não
deixa uma máquina sozinha derrubar as outras.

## 9. Checklist de corte

- [ ] Datacenter no Brasil confirmado **no contrato** (é o que fecha o art. 33)
- [ ] Domínio apontando para o IP da VPS
- [ ] Certificado Let's Encrypt emitido **e o hook de renovação instalado**
- [ ] `hostssl` no `pg_hba.conf` (nunca `host`)
- [ ] `ufw` ligado, 5432 e 22 liberados, 80 fechado de novo
- [ ] `psql` externo com `sslmode=verify-full` conectando
- [ ] Dados da Neon migrados e **contagens conferidas dos dois lados**
- [ ] `pg_dump` agendado **e a cópia saindo da VPS**
- [ ] Uma restauração testada de verdade
- [ ] Os cinco aplicativos abrindo e gravando contra o banco novo
- [ ] `docs/conformidade-lgpd.md` atualizado: ponto 10 deixa de ser pendência

---

## Pendência de código que esta migração revelou

`ConexaoStore.Normalizar` aplica `SslMode.VerifyFull` **apenas** quando a entrada é uma URI
`postgresql://`. Uma string no formato `Host=…;Database=…;Username=…;Password=…` — que é
justamente o formato que se escreve à mão para um Postgres próprio — passa **verbatim**, e o
padrão do Npgsql 8 é `SslMode.Prefer`: criptografa se o servidor oferecer, **não valida
nada**, e **cai para texto puro em silêncio** se o servidor não oferecer TLS.

Enquanto o banco era a Neon, o caminho normal era colar a URI que ela fornece, e o risco não
aparecia. Com banco próprio, colar `Host=…` passa a ser o caminho **natural** — e a proteção
que a parcela 52 pôs no lugar deixa de valer sem nenhum sinal na tela.

O conserto é normalizar os dois formatos pelo `NpgsqlConnectionStringBuilder` e elevar o
`SslMode` quando ele vier ausente ou mais fraco que `VerifyFull`, nos **dois**
`ConexaoStore` (suíte e faturamento — os dois arquivos são cópias, o débito permanente da
Fase 4). Ainda não foi feito.
