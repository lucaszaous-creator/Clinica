# Conector do web service TISS — o servidor que fala com a operadora

**Pedido da direção (nov/2026):** parar de digitar no portal da operadora o que o sistema já
sabe, começando pela Saúde Petrobras. A elegibilidade pelo web service já foi provada com o login
da clínica (`docs/tiss-webservice-petrobras.md`). Este documento cobre a peça que faltava para
isso sair da linha de comando e chegar às telas: **um serviço no servidor** que lê uma fila no
banco, fala com a operadora e grava o resultado.

Regra de fundo, decidida pela direção: **nada é enviado à operadora sem alguém, logado,
autorizar aquela guia específica.**

## Em uma tela

| Pergunta | Resposta |
|---|---|
| O que é? | `src/Clinica.Tiss.Conector`: um serviço Linux (systemd) na VPS 1, ao lado do banco. Não escuta em porta nenhuma; só faz conexões de saída (banco local e HTTPS da operadora). |
| Como a tela fala com ele? | Pela tabela `TransacoesTiss`. A tela chama `FilaTissService` (grava o pedido); o conector reserva, processa e grava o desfecho; a tela relê. |
| O que funciona hoje? | **Elegibilidade**, de ponta a ponta. |
| O que ainda NÃO funciona? | Envio de guia, consulta de protocolo e cancelamento: a fila, as chaves e as regras estão prontas e testadas, mas os três processadores respondem "ainda não foi ligado" até as mensagens deles (outra frente) serem ligadas. **Nenhuma guia sai por este serviço hoje.** |
| O que precisa estar ligado para uma guia sair? | **Duas chaves**, as duas desligadas por padrão: `TISS_ENVIO_HABILITADO=true` no servidor **e** o envio ligado para o convênio. |
| Uma guia pode ir duas vezes? | O sistema não reenvia sozinho, nunca. Ver "A regra que não se negocia". |
| Onde fica a senha da operadora? | Só o MD5 dela, cifrado, na configuração do banco. A chave que abre fica numa variável de ambiente no servidor, fora do banco. Ninguém lê a senha de volta. |
| Depende de quê para entrar em produção? | Da migration da tabela (gerada na integração), da instalação deste roteiro e da **decisão LGPD** (fim do documento). |

## UX · DX · CX

### As opções para "quem fala com a operadora"

| | 🧑‍💻 UX (quem fatura) | 🛠️ DX (quem mantém) | 🤝 CX (a clínica) |
|---|---|---|---|
| **(A) Conector no servidor + fila no banco** ✅ | A tela pede e continua livre; o resultado aparece em segundos. Se a operadora está fora do ar, a consulta tenta de novo sozinha e a tela mostra em que pé está. Fechar o app não perde o pedido. | Um projeto novo no molde da ponte do CRM; a regra de fila mora num serviço da Application com teste; a rede é trocada por uma de mentira nos testes. Um lugar só guarda a credencial. | A senha da operadora fica em UMA máquina, não em cada computador do balcão. Um serviço a mais para instalar e vigiar na VPS. Custo de máquina: desprezível (um processo parado a maior parte do tempo). |
| **(B) O app desktop chama a operadora direto** | Resposta na hora quando tudo vai bem. Quando a operadora demora, a tela espera; se o app fecha no meio de um envio, ninguém sabe se a guia chegou. | Sem serviço novo. Mas a credencial (ou a chave que a abre) teria de estar em todo computador da clínica, e a regra "não reenviar" dependeria de cada app estar na mesma versão. Cinco apps, um deles o faturamento em produção. | Menos uma peça no servidor. Mais risco: senha da operadora espalhada, IP de saída diferente por máquina (se a operadora um dia liberar por IP, quebra), e nenhum registro central do que foi dito à operadora. |
| **(C) Hub/intermediário pago** | Depende do fornecedor. | Menos código de mensagem; mais um contrato técnico para acompanhar. | Custo por guia ou mensalidade; os dados dos pacientes passam por mais uma empresa, o que exige contrato de tratamento de dados e a decisão do ponto 10 **antes** do código (`CLAUDE.md`, compromisso de conformidade). Nenhum hub confirmado cobre a Saúde Petrobras (a conectividade dela é da própria operadora). |

**Recomendação: (A).** É a única em que a credencial fica num lugar só, o envio sobrevive ao
fechamento do app e a regra contra duplicidade é uma, no servidor. (B) fica como o que já existe
para diagnóstico: a ferramenta `tools/TissPetrobras`, que não grava nada.

### O que esta entrega faz com cada eixo

- 🧑‍💻 **UX** — Nenhuma tela muda nesta entrega: ela é o motor. O que a tela vai poder mostrar
  está em `TransacaoTiss.Resultado`, sempre uma frase curta e verdadeira: "Elegível. Validade da
  carteira: 31/12/2027.", "Não elegível: …", "O envio pelo web service está desligado para
  Petrobras. Nada foi enviado à operadora.", "Falhou: confira no portal da operadora antes de pedir
  de novo.". Três estados finais que não se confundem: **Concluída** (a operadora respondeu o que
  se perguntou), **Falhou** (alguém precisa olhar) e **Cancelada** (nada saiu).
- 🛠️ **DX** — Peças novas: 1 entidade (`TransacaoTiss`), 2 serviços (`FilaTissService`,
  `CredenciaisTissService`), 7 métodos de repositório, 1 projeto (`Clinica.Tiss.Conector`) e a
  pasta `deploy/tiss-conector/`. A ordem que importa (reservar → conferir as chaves → preparar →
  **carimbar o envio** → rede → gravar) mora num lugar só, o `ExecutorDeTransacoesTiss`; um
  processador novo só monta a mensagem e lê a resposta. 74 testes na família, sem rede real.
- 🤝 **CX** — Risco em produção hoje: baixo. O serviço não escuta em porta, o usuário dele no banco
  lê quatro tabelas e grava em duas, e o envio de guias está atrás de duas chaves desligadas. O
  que muda no dia de quem atende: nada, até uma tela usar a fila. O que a clínica passa a ter de
  cuidar: uma senha de operadora cadastrada no servidor (e trocada quando a do portal mudar) e a
  cópia da chave de cifra fora do servidor.

## Como funciona

```
 app (tela)                      banco (VPS 1)                    conector (VPS 1)            operadora
 ───────────                     ─────────────                    ────────────────            ─────────
 FilaTissService
   .Enfileirar…  ───────────►  TransacoesTiss: Pendente
                                + linha na Auditoria
                                                    ◄───────────  reserva (UPDATE condicional)
                               TransacoesTiss: EmEnvio
                                                                  confere as 2 chaves de envio
                                                                  monta a mensagem
                               EnvioIniciadoEm + XML sem senha ◄─ carimba ANTES da rede
                                                                  ─────────── HTTPS ──────────►
                                                                  ◄────────── resposta ────────
                               Concluida / Falhou / Pendente  ◄─  grava o desfecho
 FilaTissService
   .ObterAsync   ◄───────────  Resultado (frase para a tela)
```

### Os estados

| Estado | Quer dizer | Volta sozinho para a fila? |
|---|---|---|
| `Pendente` | Na fila. | — |
| `EmEnvio` | Um conector reservou. | Se a reserva vencer (5 min): elegibilidade e status, sim; envio e cancelamento cuja mensagem já saiu, **nunca** (viram `Falhou`). |
| `Concluida` | A operadora respondeu o que se pediu. | Não. |
| `Falhou` | Não deu certo; alguém precisa olhar. | Não. |
| `Cancelada` | Nada saiu: quem pediu desistiu, ou o conector recusou (envio desligado, sem credencial, sem endereço). | Não. |

### A regra que não se negocia: envio de guia nunca é reenviado sozinho

Quando a rede cai no meio de um envio, **ninguém sabe se a operadora recebeu**. Reenviar é
arriscar a mesma guia duas vezes (glosa 1308, "guia já apresentada"). Por isso:

1. o conector **carimba** `EnvioIniciadoEm` e guarda o XML **antes** de ir à rede;
2. transação que escreve na operadora (envio, cancelamento) e já tem esse carimbo só termina:
   `Concluida` ou `Falhou`. Não importa o que o processador peça, a fila não a devolve;
3. se o conector morrer depois de carimbar, a transação é encerrada como `Falhou` com a frase
   "Confira no portal da operadora antes de pedir um novo envio" — e **não** é entregue a outro conector;
4. pedir de novo é ato de **gente**: um pedido novo, com operador e trilha. A fila recusa o
   pedido se a guia já tem envio em andamento ou já tem protocolo.

Elegibilidade e consulta de protocolo só perguntam: tentam de novo até 3 vezes (espera de 1 min,
depois 5 min) **quando a operadora não respondeu**. Quando ela respondeu "não" (login recusado,
mensagem fora do padrão), não há nova tentativa: repetir não muda a resposta, e login recusado
repetido pode bloquear o acesso da clínica.

### A credencial

- Guardada por convênio na configuração do banco: o login em claro e o **MD5 da senha** cifrado
  (AES-GCM, `ProtecaoSegredoGlobal`). A senha em claro não é guardada em lugar nenhum.
- A chave que abre (`CLINICA_CREDENCIAIS_CHAVE`) fica no arquivo de ambiente do serviço, fora do banco.
- Quem cadastra não lê de volta: a tela recebe só "login X, tem senha: sim/não".
- A senha não vai para log, exceção, auditoria nem XML guardado — há um teste para cada um.

⚠️ **Limite honesto:** a chave de cifra é a mesma que protege as outras credenciais da clínica
na mesma tabela (armazenamento, SafeID). O usuário do banco do conector lê a tabela de
configuração inteira, então **um conector comprometido alcançaria as outras credenciais cifradas**.
Separar isso pede uma chave própria para o TISS ou uma tabela própria — fica registrado como
melhoria, não está feito.

## O que entra na integração (para quem liga as telas)

Assinaturas públicas:

```csharp
// FilaTissService — a tela pede e acompanha
Task<TransacaoTiss>  EnfileirarElegibilidadeAsync(string convenioCodigo, string numeroCarteira, DateOnly? validadeCarteira, int? pacienteId, string operador, CancellationToken ct = default)
Task<TransacaoTiss>  EnfileirarEnvioGuiaAsync(string convenioCodigo, int codigoId, int? pacienteId, string operador, CancellationToken ct = default)
Task<TransacaoTiss>  EnfileirarStatusProtocoloAsync(string convenioCodigo, string protocolo, int? codigoId, string operador, CancellationToken ct = default)
Task<TransacaoTiss>  EnfileirarCancelamentoGuiaAsync(string convenioCodigo, int codigoId, string numeroGuiaPrestador, string? protocolo, string operador, CancellationToken ct = default)
Task<bool>           CancelarAsync(int id, string operador, CancellationToken ct = default)          // só enquanto Pendente
Task<TransacaoTiss?> ObterAsync(int id, CancellationToken ct = default)                              // sem os XML
Task<IReadOnlyList<TransacaoTiss>> DaGuiaAsync(int codigoId, CancellationToken ct = default)
Task<IReadOnlyList<TransacaoTiss>> RecentesAsync(string? convenioCodigo = null, int limite = 50, CancellationToken ct = default)

// FilaTissService — o conector executa
Task<TransacaoTiss?> ReservarProximaAsync(string consumidor, CancellationToken ct = default)
Task                 MarcarEnvioIniciadoAsync(int id, string? xmlEnviado, CancellationToken ct = default)
Task                 ConcluirAsync(int id, ConclusaoTransacaoTiss conclusao, CancellationToken ct = default)
Task<EstadoTransacaoTiss> FalharAsync(int id, string erro, bool podeTentarDeNovo, string? xmlRecebido = null, CancellationToken ct = default)
Task                 RecusarAsync(int id, string motivo, CancellationToken ct = default)

// CredenciaisTissService
Task SalvarAsync(string convenioCodigo, string login, string senha, string operador, CancellationToken ct = default)
Task RemoverAsync(string convenioCodigo, string operador, CancellationToken ct = default)
Task<CredencialTissNaTela> ParaATelaAsync(string convenioCodigo, CancellationToken ct = default)      // (Login, TemSenha)
Task<CredencialTiss?> AbrirParaEnvioAsync(string convenioCodigo, CancellationToken ct = default)      // só no servidor
Task<bool> EnvioLigadoAsync(string convenioCodigo, CancellationToken ct = default)
Task DefinirEnvioAsync(string convenioCodigo, bool ligado, string operador, CancellationToken ct = default)
Task<Uri?> EnderecoAsync(string convenioCodigo, TipoTransacaoTiss tipo, CancellationToken ct = default)
Task DefinirEnderecoAsync(string convenioCodigo, TipoTransacaoTiss tipo, string endereco, string operador, CancellationToken ct = default)

// IClinicaRepositorio
Task AdicionarTransacaoTissAsync(TransacaoTiss transacao, CancellationToken ct = default)
Task<TransacaoTiss?> ObterTransacaoTissAsync(int id, CancellationToken ct = default)
Task<IReadOnlyList<TransacaoTiss>> TransacoesTissReservaveisAsync(DateTime agora, DateTime reservaVencidaAntes, int limite, CancellationToken ct = default)
Task<bool> ReservarTransacaoTissAsync(int id, int versaoVista, string consumidor, DateTime agora, CancellationToken ct = default)
Task RecarregarTransacaoTissAsync(TransacaoTiss transacao, CancellationToken ct = default)
Task<IReadOnlyList<TransacaoTiss>> TransacoesTissDaGuiaAsync(int codigoId, CancellationToken ct = default)
Task<IReadOnlyList<TransacaoTiss>> TransacoesTissRecentesAsync(string? convenioCodigo, int limite, CancellationToken ct = default)
```

Pendências de quem integra:

1. **A migration.** Esta branch não tem migration, de propósito (uma só, na integração). Enquanto
   ela não existir, `MontagemDoSistemaTests` reprova por "modelo com mudança pendente": é esperado.
   A tabela é `TransacoesTiss`; o mapeamento está em `ClinicaDbContext`.
2. **Os três processadores.** `ProcessadoresAindaNaoLigados.cs` tem o `TODO(tiss-b-mensagens)` com
   o que trocar. Cada um vira: credencial + `DadosDoConvenio.LerAsync(…)` + `MontarEnvelope` no
   preparo, e `LerResposta` na leitura. O executor já faz o resto.
3. **O papel de banco** (`papel-tiss-conector.sql`) tem, no fim, as linhas comentadas de leitura
   da guia. O envio de guia precisa delas; concedê-las é decisão.
4. **Permissão de tela.** `FilaTissService` exige o operador, mas não confere permissão: quem
   decide quem pode pedir envio é a tela (as duas barreiras), como nas outras.

### Como eu ligaria a transação à baixa (não está feito aqui)

O conector **não** chama `DarBaixaAsync`: baixa é ato de quem está logado, e o conector não tem
sessão. O caminho que proponho:

1. no formulário de baixa da guia Petrobras, o botão "Enviar à operadora" chama
   `EnfileirarEnvioGuiaAsync` (é o ato que autoriza aquela guia) e a tela passa a acompanhar a
   transação com `ObterAsync`;
2. quando ela chega a `Concluida` com `Protocolo`, o formulário **preenche** o campo "Número da
   guia" com `NumeroGuiaOperadora` (quando a operadora o devolve) e mostra o protocolo; a pessoa
   confere e clica em "Confirmar baixa" — a mesma `DarBaixaAsync` de sempre, com o operador dela;
3. se a pessoa fechou o formulário antes, a guia continua pendente e o painel mostra "enviada,
   protocolo X: falta dar baixa" (`DaGuiaAsync` responde isso). É o aviso que pede ação;
4. `Falhou` depois de a mensagem sair mostra a frase que manda conferir no portal, e a baixa
   manual (número copiado do portal) continua funcionando como hoje.

Baixa automática sem ninguém conferir fica para depois de a clínica ver o envio funcionar algumas
semanas — e seria decisão da direção, com o operador de quem pediu o envio.

## Instalação na VPS (passo a passo, para quem opera)

Pré-requisitos: a VPS 1 com o Postgres da clínica (`docs/banco-na-vps.md`); os apps já na versão
que cria a tabela `TransacoesTiss`, abertos ao menos uma vez (é o app que aplica a migration).
Nada aqui abre porta no firewall.

**1. Publicar** — no computador de quem instala (Linux ou WSL, com o SDK 8), nunca na VPS:

```bash
deploy/tiss-conector/publicar.sh        # gera tiss-conector-XXXXXXX/ e o .tar.gz
sha256sum tiss-conector-XXXXXXX.tar.gz  # anote, para conferir depois de copiar
```

**2. O usuário do banco** — na VPS 1, como root, com o `papel-tiss-conector.sql` que veio na pasta
publicada (`deploy/`):

```bash
sudo -u postgres psql -p 45432 -d clinica -v senha="'SENHA_FORTE'" -f papel-tiss-conector.sql
```

No `pg_hba.conf`, **antes** das linhas `hostssl`: `local clinica tiss_conector scram-sha-256`.
Depois, `systemctl reload postgresql`. O conector entra só pelo socket local, nunca por TCP.

**3. Os arquivos** — na VPS 1, como root:

```bash
useradd --system --home-dir /nonexistent --shell /usr/sbin/nologin --user-group clinica-tiss
mkdir -p /opt/clinica-tiss-conector/releases /etc/clinica-tiss-conector
chmod 700 /etc/clinica-tiss-conector
# copie o .tar.gz, confira o sha256sum e:
tar -xzf tiss-conector-XXXXXXX.tar.gz -C /opt/clinica-tiss-conector/releases
ln -sfn /opt/clinica-tiss-conector/releases/tiss-conector-XXXXXXX /opt/clinica-tiss-conector/current

cd /opt/clinica-tiss-conector/current/deploy
install -m 600 conector.env.example /etc/clinica-tiss-conector/conector.env
install -m 600 admin.env.example    /etc/clinica-tiss-conector/admin.env
```

Edite os dois arquivos em `/etc/clinica-tiss-conector/`:

- `conector.env`: a senha do usuário `tiss_conector` e a `CLINICA_CREDENCIAIS_CHAVE`. Se os apps
  e a ponte do CRM já usam essa chave, é **a mesma**. Se a clínica ainda não a tem, gere com
  `openssl rand -base64 32` e **guarde uma cópia fora do servidor**. Deixe `TISS_ENVIO_HABILITADO=false`.
- `admin.env`: a conexão do usuário do banco que os apps usam. O serviço não lê este arquivo.

**4. Cadastrar a credencial da operadora** — a senha é digitada na hora e não fica em arquivo
nem no histórico do terminal:

```bash
export TISS_WS_LOGIN='LOGIN_DA_CONECTIVIDADE'
/opt/clinica-tiss-conector/current/deploy/comando.sh credencial Petrobras --senha-na-entrada --operador 'Seu Nome'
# (digite a senha e Enter; ela não aparece no retorno)
unset TISS_WS_LOGIN
```

`Petrobras` é o código do convênio no cadastro; `--operador` é quem está fazendo, e vai para a trilha.

**5. Ligar o serviço:**

```bash
cp /opt/clinica-tiss-conector/current/deploy/clinica-tiss-conector.service /etc/systemd/system/
systemctl daemon-reload && systemctl enable --now clinica-tiss-conector
journalctl -u clinica-tiss-conector -n 20
```

O que deve aparecer: `Conector TISS no ar. Envio de guias neste servidor: desligado. Intervalo: 5s.`

**6. Conferir:**

```bash
/opt/clinica-tiss-conector/current/deploy/comando.sh situacao Petrobras
# Petrobras: login cadastrado, senha cadastrada, envio desligado.
```

Depois, peça uma elegibilidade por uma tela (quando houver) e veja a linha passar de `Pendente`
a `Concluida` no mesmo comando.

## Operação

### Girar a credencial (a senha do portal mudou, ou alguém saiu da clínica)

Repita o passo 4 com o login e a senha novos. Vale na próxima transação; não precisa reiniciar o
serviço. Para tirar a credencial: `comando.sh credencial Petrobras --remover --operador 'Seu Nome'`
(o conector passa a recusar as transações do convênio, com a frase na tela).

Trocar a **chave de cifra** (`CLINICA_CREDENCIAIS_CHAVE`) é outra coisa: ela protege também as
outras credenciais da clínica e é compartilhada com os apps e a ponte. Não troque só aqui.

### Ligar e desligar o envio de guias por convênio

São **duas chaves**, e uma guia só sai com as duas ligadas:

| Chave | Onde | Como |
|---|---|---|
| Do servidor | `/etc/clinica-tiss-conector/conector.env` | `TISS_ENVIO_HABILITADO=true` e `systemctl restart clinica-tiss-conector` |
| Do convênio | configuração no banco | `comando.sh envio Petrobras ligar --operador 'Seu Nome'` (ou `desligar`); vale na próxima transação, sem reiniciar |

Para o envio falar com a operadora também é preciso o endereço:
`comando.sh endereco Petrobras envio https://… --operador 'Seu Nome'` (idem `status` e
`cancelamento`; a elegibilidade da Saúde Petrobras já tem o endereço do manual dela).

**Desligar na pressa:** `comando.sh envio Petrobras desligar --operador 'Seu Nome'`. Transação de
envio que já estava na fila é recusada com a frase "O envio pelo web service está desligado para
Petrobras. Nada foi enviado à operadora." A que já estava no meio da ida termina normalmente.

⚠️ Hoje, mesmo com as duas chaves ligadas, o envio responde "ainda não foi ligado nesta versão do
conector" — ver "O que ainda não funciona".

### O que olhar no log

```bash
journalctl -u clinica-tiss-conector -f
```

| Linha | Quer dizer | O que fazer |
|---|---|---|
| `Transação TISS 12 (Elegibilidade, Petrobras) concluída.` | Tudo certo. | — |
| `… recusada antes do envio: …` | Nada saiu: envio desligado, sem credencial ou sem endereço. | Ler o motivo; ele também está na tela. |
| `… sem sucesso: sem resposta da operadora (…) Fica Pendente.` | A operadora não respondeu; o sistema tenta de novo. | Nada, se for esporádico. |
| `… sem sucesso: … LoginInvalido … Fica Falhou.` | A operadora recusou o login. | Girar a credencial. **Não** fique repetindo: pode bloquear o acesso. |
| `… Fica Falhou.` num envio | O envio não se completou. | Conferir **no portal da operadora** se a guia chegou, antes de pedir outro. |
| `Conector TISS: a volta falhou (NpgsqlException). Falhas seguidas: N.` | O banco não respondeu. | Ver o Postgres; o conector volta sozinho. |
| `42501` em qualquer lugar | Falta permissão no usuário do banco. | Rever `papel-tiss-conector.sql`. |

O log traz o número da transação, o tipo, o convênio e o desfecho. **Nunca** carteira, nome,
login ou senha — para saber de quem é a transação 12, olhe a tela, com o seu login.

### Rollback

- **Parar tudo:** `systemctl disable --now clinica-tiss-conector`. As telas continuam abrindo; os
  pedidos ficam `Pendente` na fila e nada vai à operadora. A baixa manual não depende do conector.
- **Voltar à versão anterior:** `ln -sfn /opt/clinica-tiss-conector/releases/tiss-conector-ANTERIOR
  /opt/clinica-tiss-conector/current && systemctl restart clinica-tiss-conector`.
- **Só o envio:** desligue uma das duas chaves (acima).
- **Antes de religar depois de uma parada longa:** olhe a fila (`comando.sh situacao`). Pedido de
  envio antigo que ficou `Pendente` **sai** quando o serviço voltar com as chaves ligadas. Se não
  for para sair, cancele-o pela tela antes de religar.
- **Remover de vez:** pare o serviço, apague `/etc/clinica-tiss-conector` (a credencial some com a
  chave) e rode `DROP ROLE tiss_conector` depois de `REVOKE`. A tabela `TransacoesTiss` fica: é o
  registro do que foi dito à operadora.

## LGPD — a decisão que ainda falta (ponto 10 do compromisso)

Dito sem enfeite:

- **O que sai do sistema.** Na elegibilidade: o número da carteira, a validade e o CNPJ da
  clínica vão para a operadora do próprio paciente. Volta o nome do beneficiário. No envio de
  guia (quando ligado), irão os dados da guia. São os mesmos dados que a secretária já digita no
  portal da mesma operadora; o que muda é o caminho.
- **Por onde passa.** O web service da Saúde Petrobras é operado por uma conectividade contratada
  pela operadora (o endereço é de terceiro). É a mesma empresa por trás do portal, pelo que o
  endereço indica — **não foi confirmado com a operadora**.
- **O que o compromisso exige.** O ponto 10 do `CLAUDE.md` diz que dado clínico novo em serviço
  externo é transferência a decidir, e que nenhuma integração nova manda dado para fora sem
  passar por essa decisão. **Essa decisão não está registrada.** A elegibilidade já foi feita uma
  vez em produção, por autorização direta da direção para o teste; isso não é a decisão do ponto 10.
- **O que eu recomendo registrar** em `docs/conformidade-lgpd.md` antes de qualquer tela usar a
  fila: (a) que o destinatário é a operadora do beneficiário, para faturamento do atendimento —
  a base legal provável é a de execução de serviço de saúde e de transações administrativas dele
  (LGPD, art. 11, II, "a" e "f", e §4º, II); (b) quem é o operador da conectividade e onde ficam
  os servidores dele (é a pergunta que decide se há transferência internacional); (c) que o
  canal é o oficial da operadora. **Isto é leitura técnica, não parecer jurídico**: quem valida é
  o encarregado de dados ou o jurídico da clínica.
- **O que passa a ficar guardado e não ficava.** Na tabela `TransacoesTiss`: a carteira consultada,
  o XML enviado (sem a senha) e a resposta da operadora, que traz o **nome do beneficiário**. É
  dado pessoal novo em repouso. Não há prazo de descarte definido nem rotina de limpeza — fica
  como pendência a decidir junto (guardar pelo prazo de recurso de glosa parece o mínimo útil).
- **Quem vê.** A frase da tela (`Resultado`) não leva o nome. Os XML só saem do banco por
  `ObterTransacaoTissAsync`, que nenhuma tela usa hoje; quando uma tela os mostrar, isso pede
  permissão própria.
- **O que não há:** contrato de tratamento de dados com a conectividade (a relação é com a
  operadora), e resposta a incidente específica para a credencial da operadora — se ela vazar, o
  passo é trocar a senha no portal e girar a credencial aqui.

## O que ainda não funciona e o que não foi verificado

- **Envio de guia, status de protocolo e cancelamento** não falam com a operadora (ver acima).
- **Nenhuma chamada real** foi feita por este código: todos os testes usam rede de mentira. A
  elegibilidade real foi provada pela ferramenta `tools/TissPetrobras`, que usa as mesmas mensagens,
  mas não este transporte.
- **O papel de banco não foi medido** contra um Postgres de verdade (não havia um na máquina de
  quem escreveu). A lista saiu da leitura do código.
- **A suíte contra o Postgres não rodou** (`CLINICA_TESTES_POSTGRES`), e a tabela ainda não tem
  migration. A reserva usa `ExecuteUpdate`, que os testes exercitam em SQLite; a consulta da fila
  tem teste de tradução para o Npgsql, a reserva não.
- **O serviço não foi instalado** em servidor nenhum: a unidade systemd, o `publicar.sh` e o
  `comando.sh` foram escritos e conferidos por sintaxe, não executados numa VPS.
- **Um conector por vez** é o desenho; dois ao mesmo tempo não pegam a mesma linha (testado), mas
  não há por que subir dois.
