# Site da clínica com agendamento online — o desenho, antes de existir uma linha

> **Levantamento e desenho, ago/2026. Nada disto está implementado.** O documento existe para
> que quem for construir comece com os três achados que custam caro e **não são óbvios ao ler o
> código** — o bloqueador da grade de disponibilidade, o nome de paciente dentro de
> `ConflitoAgenda.Descricao`, e o valor de enum que derruba app desatualizado.
>
> Toda referência a arquivo e linha foi conferida contra o `src/` na data acima.

## A decisão, e o que ela NÃO é

A cliente quer um **site público onde o paciente escolhe dia e hora sozinho**, e que a marcação
caia no sistema.

Isto **não é o "Portal do paciente"**, que ela pôs fora de escopo em jul/2026
(`docs/features-por-modulo.md`, linha 1064). É o **Agendamento online**, que
`docs/concorrencia-smartclinic.md` registra como decisão em aberto e **distinta** do portal —
*"um link público da agenda para o paciente escolher dia e hora sozinho"*, e *"o único que reduz
trabalho do balcão sem depender do balcão"*. Confundir os dois é o erro que este parágrafo existe
para impedir: o portal é aplicação web com login e prontuário; isto é uma página que oferece vãos
livres.

O escopo desenhado, decidido com a direção:

1. **O paciente marca sozinho.** A equipe continua nos apps Windows — **não** há área web logada
   para a clínica. A "web de leitura" (agenda e painel no navegador, item 7 do documento de
   concorrência) fica de fora.
2. **O site nunca escreve na agenda clínica.** O horário fica **segurado** e vira um **pedido**
   que a Recepção confirma com um clique.
3. **Paciente novo e antigo** podem pedir horário.
4. **Roda na mesma VPS do banco** (`docs/banco-na-vps.md`), falando com o PostgreSQL por
   **loopback**.

## Por que isto não contradiz `docs/banco-na-vps.md`

Aquele documento descartou **"API HTTPS no meio"**, e a próxima pessoa a ler os dois vai achar
que este desenho reabre um debate encerrado. Não reabre, e a diferença é inteira:

O descarte era sobre **substituir o acesso a dados dos apps desktop** por HTTP. O custo era
estrutural — `IClinicaRepositorio` tem ~249 assinaturas, 45 serviços gravam por change tracking
do EF (179 `SalvarAsync`), e a concorrência otimista por `xmin` não atravessa HTTP.

**O site não é isso.** Ele é um **consumidor adicional das mesmas bibliotecas**
(`Clinica.Domain` + `Clinica.Application` + `Clinica.Infrastructure`, todas `net8.0` puras, sem
uma linha de WPF), rodando **no mesmo host** do Postgres e falando com ele por loopback. Nenhum
app desktop passa a falar HTTP. **Nenhuma porta nova do banco é aberta** — a 45432 continua atrás
do mTLS, para a frota de Windows.

O que cresce é a superfície do **host**: a 443 passa a responder com código nosso atrás, e um
processo a mais disputa 2 GB de RAM com o banco da clínica. Isso é verdade, é novo, e o preço
está no passo de deploy (`MemoryMax` no systemd, patch do runtime pelo `unattended-upgrades`, e
um certificado a mais com data de vencimento). Dizer isso é melhor do que descobrir depois.

---

## ⚠️ O bloqueador central: não existe grade de disponibilidade

**É a maior parte do trabalho, e não é o site.**

O sistema não sabe quando a clínica abre nem quando cada profissional atende:

- A grade da agenda usa constantes **fixas no ViewModel** — `AberturaPadrao = new(7, 0)` e
  `FechamentoPadrao = new(20, 0)`, com `PassoMinutos = Agendamento.DuracaoPadraoMinutos`
  (`src/Clinica.Modulo.Recepcao/ViewModels/AgendaViewModel.cs`, linhas 211–220) — e desenha
  **todos** os vãos como clicáveis, esticando a janela para caber o que houver fora dela.
- `BloqueioAgenda` (`src/Clinica.Domain/Entities/Equipe.cs`) é **exceção**: férias, feriado,
  folga, sala em manutenção. Não é expediente recorrente.
- `ParametrosService.ChaveJornadaDiariaMinutos` (480) é só o **denominador da taxa de ocupação**
  no BI; não governa a agenda.
- `AgendaService.GarantirSemChoqueAsync` valida choque de recurso, **não** horário de
  funcionamento.
- Não existe `HorariosLivresAsync`/`DisponibilidadeAsync` em lugar nenhum: a disponibilidade é
  calculada por **negação, na camada de UI**.

Para a recepcionista isso sempre bastou — **ela sabe de cabeça quem trabalha quando**. Um site
público não sabe, e ofereceria 07:00 de domingo.

⚠️ E a grade depende de uma decisão **da clínica** que hoje não existe por escrito: qual o
horário de funcionamento e qual a jornada de cada profissional. **O atraso aqui não é técnico**, e
quem planejar prazo precisa saber disso antes de prometer data.

---

## O desenho

### `JanelaAtendimento` — o gêmeo POSITIVO de `BloqueioAgenda`

Entidade nova, copiando deliberadamente o molde de `BloqueioAgenda`, inclusive o "null = a
clínica inteira": `ProfissionalId?` (null = horário de funcionamento da clínica), `SalaId?`,
`DiaSemana`, `Inicio`/`Fim` (`TimeOnly`), `PassoMinutos?`, `VigenteDe`/`VigenteAte?`, `Ativa`,
`ModalidadesCodigos?` (vazio = todas), e **`AbertoAoPublico`**.

- **A vigência não é enfeite.** Reajustar a jornada é **linha nova**, como a taxa de cartão e o
  preço por convênio. Sobrescrever reescreveria a resposta sobre uma semana que já passou.
- **`AbertoAoPublico`** é o que permite "quarta à tarde existe na agenda interna e o site não a
  vê" — horário guardado para encaixe e retorno.
- ⚠️ **Não nasce um `Profissional.AceitaAgendamentoOnline`.** A janela já responde, com mais
  precisão, e duas noções de "este profissional está online" seriam duas fontes de verdade sobre
  a mesma pergunta. De quebra evita um `AddColumn` de `bool` não-anulável numa tabela com linhas
  — que é exatamente onde o `defaultValue: false` do EF morde.

### `DisponibilidadeService`

Mora em **`Clinica.Application/Servicos/`**, não no projeto web: é regra de domínio, a Recepção
vai consumi-la, e `MontagemDoSistemaTests` só cobre o que está ali. Duas definições de "está
livre?" divergiriam na primeira correção.

Devolve `VagaAgenda` — record novo com `Inicio`, `DuracaoMinutos`, `ProfissionalId?`,
`SalaId?`. **Invariante estrutural: não tem nenhum campo que possa carregar nome de paciente.** É
o tipo que atravessa para o site, e a impossibilidade é do **tipo**, não da disciplina de quem
escreve a página.

O cálculo é **quatro leituras e o resto em memória**: janelas vigentes ∩ janela-da-clínica, menos
agendamentos (`a.OcupaAgenda && a.ColideCom(...)`), menos bloqueios (`b.AlcancaRecurso(...) &&
b.ColideCom(...)`), menos reservas vivas — descartando o vão que extrapola o fim da janela, o que
viola a antecedência mínima, e o que não é `AbertoAoPublico`. Reusa os métodos do domínio; não
reimplementa colisão.

⚠️ **Dia sem janela da clínica é dia FECHADO**, nunca aberto por omissão. No primeiro dia isso é
um site que não oferece nada, e é de propósito: **vazio ruidoso** ("a clínica ainda não declarou
horário de funcionamento") é melhor que **oferta errada silenciosa**.

### `SolicitacaoAgendamento` — o pedido

Tabela **nova**, portanto aditiva e **invisível aos apps que ainda não atualizaram**. É o que
permite publicar o site sem esperar a frota inteira subir de versão.

Cinco blocos: identidade (token opaco = a URL do paciente, chave de idempotência); o vão; o
**pré-cadastro** (nome, telefone, CPF, nascimento, convênio); o ciclo (`Status`,
`ReservaExpiraEm`, decisão, `PacienteIdSugerido?`, `PacienteId?`, `AgendamentoId?`); e
LGPD/anti-abuso.

- O pré-cadastro vive **aqui**, não numa tabela própria: um pedido recusado **é** o pré-cadastro
  descartado, e uma tabela separada precisaria de ciclo de vida e dedupe próprios para guardar o
  que já é campo do pedido.
- **Segurar é por LEITURA, não por temporizador.** `Vigente(agora)` é calculado, e a
  disponibilidade subtrai as vigentes. Sem job de fundo — se tudo parar, os vãos **voltam
  sozinhos**. É o padrão de `PacotePaciente.Situacao(hoje)` e de `Agendamento.Etapa`. Marcar
  `Status = Expirada` é varredura preguiçosa da tela, só cosmética.
- **A corrida real é de INSERT** (dois visitantes no mesmo vão no mesmo segundo), onde o `xmin`
  não ajuda. A guarda é um índice único **filtrado** sobre as reservadas; o erro de unicidade
  vira **frase própria e genérica**, nunca a mensagem do EF.

### Do pedido ao agendamento

`ConfirmarAsync` carrega, afirma que está vigente, **carimba a decisão na entidade rastreada** e
só então chama **`AgendaService.AgendarAsync`** — o mesmo método do balcão, com o operador do
login. Toda a validação de choque (profissional, sala com capacidade, paciente, bloqueio) **vem
de graça**, e um vão ocupado por dentro entre o clique do paciente e o do balcão é recusado com a
frase que o serviço já produz.

⚠️ **A ordem carrega peso.** `AgendarAsync` faz o próprio `SalvarAsync` e o repositório é
*scoped*, então carimbar antes grava junto. Morrer entre as duas gravações deixa uma solicitação
confirmada sem `AgendamentoId` — visível, benigna, e o horário está na agenda. **A ordem inversa
deixaria o pedido ainda reservado com um `Agendamento` fantasma, e o segundo clique da recepção
agendaria de novo.**

Recusar exige **motivo escrito** — a doutrina de `BloqueioAgenda.Motivo` ("bloqueio sem motivo
vira mistério"), e libera o vão na hora.

---

## ⚠️ As três armadilhas que custam caro

### 1. `ConflitoAgenda.Descricao` contém nome de paciente

`src/Clinica.Application/Servicos/AgendaService.cs`, linhas 215–219, monta literalmente:

```csharp
$"{a.Profissional?.Rotulo ?? "O profissional"} já atende "
+ $"{a.Paciente?.Nome ?? "outro paciente"} às {a.DataHora:HH:mm}."
```

e o repositório faz `.Include(a => a.Paciente)`. **Essa string não pode chegar perto do projeto
web.** É a primeira razão pela qual `ConflitosAsync` **não entra no laço** da disponibilidade. A
segunda é custo: 14 dias × 6 profissionais × 26 vãos são ~2.200 idas a um Postgres remoto — a
mesma lição já escrita em `BloqueioAgendaService.NoPeriodoAsync` ("usá-lo na grade daria uma
consulta por célula").

### 2. `OrigemAgendamento` não pode ganhar valor novo

`Agendamento.Origem` é `HasConversion<string>()` **puro**
(`src/Clinica.Infrastructure/ClinicaDbContext.cs`, linha 259) — **sem** o
`ConversorEnumTolerante`. Gravar `"Site"` mataria a **consulta inteira** da agenda em todo app
ainda não atualizado — não a linha, a **consulta** —, com a frase em inglês do incidente de
14/08/2026 documentado em `ConversorEnumTolerante.cs`. Os cinco apps se auto-atualizam por
Velopack, **um canal por app**: a janela em que o Consultório já atualizou e a Recepção não é o
**desenho**, não o acidente.

**A saída é não marcar.** `SolicitacaoAgendamento.AgendamentoId` já responde "este horário veio do
site" por junção, e é ela que a tela usa para o selo. Zero risco, zero enum novo.

A blindagem para a próxima vez — passar `Origem` e `Status` para `ConversorEnumTolerante<T>` com
sentinelas — cabe na mesma fatia e não custa migration (o `HasMaxLength(20)` já comporta). Dito
com honestidade: **isso não protege os builds já instalados**, só os desta release em diante. É
uma catraca cujo retorno é uma parcela futura.

⚠️ Alguém vai querer acrescentar `Site` "para ficar bonito no relatório". Merece um aviso no
próprio enum, ao lado do que já existe para `RetornoSugerido`.

### 3. `Permissao` está no penúltimo bit

O último ocupado é `ColherAssinaturaPaciente = 1 << 29`
(`src/Clinica.Domain/Entities/Acesso.cs`). O enum é `[Flags]` sobre **`int`**, e
`PermissoesExtras`/`PermissoesNegadas` são `HasConversion<int>()`. A permissão desta feature gasta
`1 << 30`; sobra `1 << 31`, que é o **bit de sinal**.

**A permissão seguinte a essa exige promover o enum a `long` e um `AlterColumn` numa coluna de
produção** — que a checagem 18 do `verificar-suite.py` reprova sem marca consciente. Isso precisa
ficar escrito no enum, ao lado do bit.

---

## LGPD e segurança

**O calendário público mostra só vãos LIVRES.** Nunca nome, nunca inicial, nunca horário ocupado
— e nem contagem por profissional, porque "Dra. Ana tem 1 vaga" já conta quanto ela está cheia.

**A garantia mais forte é do BANCO, não do código.** A role do site recebe `SELECT` só nas tabelas
de que precisa e escrita só nas de solicitação e auditoria. ⚠️ **Sem `ALTER DEFAULT PRIVILEGES`**,
de propósito: com eles, uma migration futura que criasse uma tabela de prontuário a entregaria ao
site **em silêncio**. Sem eles, uma tabela nova que o site precise falha **alto** no ensaio.
Falha barulhenta no que precisamos vale mais que concessão silenciosa no que é sensível.

Isso deixa disponível a frase que uma auditoria quer ouvir: **o processo exposto à internet não
consegue nem `SELECT` na tabela de evoluções.**

Mais:

- **O site nunca diz se um CPF existe na base.** Acertar e errar produzem resposta **letra por
  letra idêntica**. Um site que diz "bem-vindo de volta!" é um oráculo de *"esta pessoa é paciente
  desta clínica"* — dado quase-sensível numa clínica. Com resposta idêntica, a enumeração é
  **inútil**.
- **O site nunca cria um `Paciente`.** A ficha nasce na Recepção, por `PacienteService`, e a regra
  da parcela 57 (CPF de outra ficha recusado, com a mensagem que **diz o nome** de quem já o tem)
  dispara ali, **inalterada**. Zero lógica de duplicata nova.
- **O site nunca renderiza mensagem de exceção da camada Application** — página genérica, detalhe
  no log.
- **Consentimento** colhido no site vira `ConsentimentoLgpd` na confirmação, **preservando o
  `RegistradoEm` do aceite**: a FK exige um `Paciente` que ainda não existia no momento em que a
  pessoa aceitou.
- **Auditoria** no mesmo `SalvarAsync` do ato, com `Operador = "site"` — resposta honesta a "quem
  fez isso?", que significa "o titular, sem login".
- ⚠️ **Retenção.** Um pedido recusado ou expirado guarda nome, telefone e CPF de alguém que
  **nunca virou paciente**. Sem purga, o site constrói em silêncio um **banco paralelo de
  não-pacientes**. É **minimização (art. 6º, III)** — a exigência **oposta** à guarda de 20 anos,
  e por isso precisa de linha própria, para ninguém "consertar" guardando para sempre.
- **Confirmar um pedido que a própria pessoa fez é execução do que ela pediu, não
  `ComunicacaoEMarketing`.** A distinção precisa estar no código e aqui: é o tipo de coisa que um
  auditor pergunta.

**Anti-abuso**: antecedência mínima e janela de dias visíveis limitam o alvo; teto de pedidos
vivos por contato; cota por impressão de origem (hash de IP + user-agent — **o IP nunca é
gravado**); rate limiting; anti-forgery, honeypot e tempo mínimo de preenchimento.
**Sem CAPTCHA de terceiro**: seria um **operador terceiro** num formulário público de saúde,
exigindo linha no aviso de privacidade e no item 10 do `conformidade-lgpd.md`, e o ganho não paga.
**Sem confirmação por código**: não há API de WhatsApp (o `wa.me` é um clique por pessoa) e SMS
custa provedor — dito por extenso em vez de prometido.

⚠️ **O backstop honesto**: isto para script, não humano determinado. O que torna o ataque barato
de absorver é que **recusar custa um clique**, o vão volta na hora, e toda reserva morre sozinha
em ≤24 h.

---

## O projeto web

ASP.NET Core `net8.0` (nunca `-windows`), **zero NuGet novo** — rate limiting, anti-forgery e
health checks estão no framework compartilhado.

**Razor Pages**, porque o site tem duas metades com exigências opostas e ele atende as duas sem um
segundo mecanismo: o institucional precisa de HTML no primeiro byte e URL estável (`@page` é
URL ↔ arquivo, 1:1), e o fluxo precisa de POST validado com anti-forgery. Descartados: **Blazor
Server** (um circuito SignalR **por visitante**, com estado no servidor, numa VPS de 2 GB que
também é o banco da clínica — o vetor de DoS mais barato que existe, e que cai no 4G do paciente
no meio do formulário); **Blazor WASM** (precisa de uma API atrás de qualquer jeito, e mata o SEO
da metade institucional); **MVC** (um controller para cada página estática). Vanilla JS, sem npm,
sem build step — o CI continua sendo `dotnet` + Python.

Três guardas no arranque:

1. ⚠️ **O site NUNCA chama `MigrateAsync`.** O schema é dos apps desktop, sob
   `pg_advisory_lock(727411)`. Um `publish` na VPS não pode aplicar DDL de madrugada sem ninguém
   olhando.
2. **Mas ele confere**: migration pendente → serve o institucional e **desliga o agendamento** com
   uma frase honesta. Sem isso, tentaria escrever numa tabela que ainda não existe.
3. ⚠️ **Recarrega os catálogos** no arranque e periodicamente. `CatalogoModalidades` é cache
   **estático**; sem recarregar, `Nome(codigo)` cai no literal de fallback e **todo horário do
   site apareceria com o nome errado**. O site não reinicia quando a clínica cadastra uma
   modalidade nova.

---

## Deploy na VPS

**Framework-dependent**, com o runtime do repositório da Microsoft, para o `unattended-upgrades`
patchear o ASP.NET como já patcheia o Postgres — a "única obrigação contínua" de
`banco-na-vps.md` continua sendo uma. Artefato **construído fora e copiado**: `dotnet build` na
própria VPS come RAM ao lado do banco.

`systemd` com usuário próprio sem shell, Kestrel **só em loopback**, `NoNewPrivileges`,
`ProtectSystem=strict` e **`MemoryMax`** — que é o que protege o Postgres do OOM killer. A
connection string vai num `EnvironmentFile` restrito: é o bootstrap, e por isso a única exceção
reconhecida à doutrina de "configuração mora no banco". Pool capado, para fechar a conta do
`max_connections` do passo 3 daquele documento.

⚠️ **A linha mais perigosa de todo o deploy** é a do `pg_hba.conf`. Ela **tem** de restringir o
usuário do site a `127.0.0.1/32`. Escrita como `0.0.0.0/0`, vira uma porta **sem certificado**
aberta na internet, desfazendo em uma linha a fechadura inteira do `banco-na-vps.md`. Merece
conferência no próprio script de instalação, que deve **recusar** se encontrar máscara diferente.

nginx + certbot na frente, com `UseForwardedHeaders` (sem ele o rate limiter particiona todo mundo
no IP do nginx), HSTS/CSP/`Referrer-Policy`, e corpo de requisição pequeno (o formulário não tem
upload).

⚠️ O certificado do Let's Encrypt vira o **segundo** item que "para de uma hora para outra", junto
dos certificados de 2031 do passo 4 — mesma anotação de calendário.

**Backup**: `backup-clinica.sh` faz `pg_dump` do banco inteiro, e as tabelas novas entram
sozinhas. **Nada a mudar** — e isso precisa estar escrito, porque é a primeira pergunta que
alguém faz.

---

## Ordem de entrega

| # | Fatia | Entrega |
|---|---|---|
| **F1** | Conversor tolerante, as duas entidades, mapeamento, migration aditiva, repositório, os três serviços, testes | **A maior fatia.** Nada muda para o usuário |
| **F2** | Cadastro da grade como **aba de `EquipeView`** (reusa `GerenciarEquipe`, sem bit novo); a agenda estica para cobrir as janelas | Sem ela, F5 oferece 07:00 de domingo. Exige o **dever de casa da clínica** |
| **F3** | Site **só institucional** + aviso de privacidade; agendamento atrás de interruptor (padrão desligado) | Site no ar, e o deploy exercitado **antes** de haver dado em jogo |
| **F4** | Fila na Recepção: permissão, item de menu, tela, contador no Painel, WhatsApp, expiração, purga | ⚠️ **ANTES do fluxo público** |
| **F5** | O fluxo no site: páginas, seletor de vãos, rate limiting, página do pedido, interruptor ligado | O site recusa ligar sem janela da clínica declarada |
| **F6** | Documentação e conformidade | Onde a decisão fica registrada |

⚠️ **F4 antes de F5 não é negociável**: um pedido que ninguém consegue confirmar é pior que não
ter site. Tela vazia é inofensiva; **fila sem tela é um paciente esperando resposta que não vem.**

O cadastro da grade entra como **aba de `EquipeView`** porque é cadastro que se mexe uma vez por
ano, e a REGRA DE LEIAUTE do `README.md` proíbe grudá-lo na operação.

⚠️ Sobre a agenda WPF: na F2 ela apenas **estica** a grade desenhada para cobrir as janelas — o
site passa a ser, por construção, um **subconjunto** do que ela desenha. **`AgendaService.
AgendarAsync` NÃO passa a validar contra a janela**: ele é chamado pelo faturamento congelado, que
marca sem profissional nem sala, e uma validação nova ali derrubaria um app em produção que
ninguém vai recompilar. Numa parcela futura o vão fora da janela pode ganhar a pintura do vão
bloqueado — e **continua clicável**, marcando encaixe, porque tirar a clicabilidade enquanto a
clínica ainda aprende a declarar janelas é chamado de suporte no pior momento.

---

## Testes a escrever

**Disponibilidade** — domingo sem janela da clínica → **zero vagas** (o teste que existe para o
bug de "07:00 de domingo"); janela do profissional recortada pela da clínica; agendamento e
bloqueio somem com o vão; sala com capacidade 2 mantém o segundo; reserva viva some e **reserva
expirada volta**; vão que não cabe no fim da janela não é oferecido; e **número CONSTANTE de idas
ao banco**, medido por interceptor — é o que impede alguém reintroduzir `ConflitosAsync` no laço.

**Solicitação** — duas reservas no mesmo vão: a segunda recusada **com frase genérica** (nada do
EF vaza); idempotência do duplo-submit; cotas; **resposta idêntica para CPF conhecido e
desconhecido** (o teste anti-enumeração); confirmação sobre vão ocupado lança a exceção **do
`AgendaService`**, provando que a regra não foi duplicada; consentimento com a data do **aceite**.

**Circuito ponta a ponta**, no espírito do `CircuitoCompletoTests`, afirmando os **elos** — que
aqui, como lá, são chave estrangeira e não chamada de método: janela cadastrada → vão oferecido →
reservado → **deixa de ser oferecido** → confirmado vira `Agendamento` → aparece na agenda e no
painel do balcão → `ConfirmarPresencaAsync` gera atendimento e códigos de faturamento → o
consentimento existe e está vigente.

É o teste que prova que o site não é um silo. **Elo partido aqui não vira erro, vira lista
vazia** — indistinguível de um dia fraco.

---

## Riscos, ditos por inteiro

| Risco | Leitura |
|---|---|
| **A maior parte do trabalho não é o site** | F1+F2 são metade da entrega, e são **domínio**, não web. Se o orçamento apertar, é o que corta pelo meio |
| **A grade depende da clínica** | Horário de funcionamento e jornadas não existem por escrito hoje. **Atraso não técnico** |
| **Duas noções de "aberto" na virada** | *"O site não deixou marcar às 8h e a agenda mostra 8h."* O site é subconjunto por construção — benigno e **confuso**, que é custo de suporte real |
| **A tentação do `OrigemAgendamento.Site`** | Alguém vai querer acrescentá-lo e derrubar a clínica |
| **O bit 30** | Gasta o penúltimo. A próxima permissão custa `long` + `AlterColumn` em produção |
| **A VPS com um inquilino a mais** | Se o site comer RAM, a clínica inteira para. `MemoryMax` + swap contêm; o teste é olhar `free -h` por uma semana |
| **Superfície na internet** | A 443 passa a responder com código nosso atrás. É verdade e é novo |
| **Anti-abuso sem CAPTCHA nem SMS** | Para script, não humano determinado. O backstop é operacional |

## O que fica de fora, por inteiro

Área logada do paciente (fora de escopo, jul/2026) · agenda e painel no navegador para a equipe ·
remarcar ou cancelar pelo site **depois** de confirmado (só "cancelar meu pedido" antes) ·
pagamento ou sinal online · disparo automático de WhatsApp (exige a API oficial da Meta — decisão
comercial, e o gap que `docs/concorrencia-smartclinic.md` já aponta) · sugerir horário alternativo
dentro do pedido · lista de espera pública · e **a grade WPF passar a IMPEDIR marcação fora da
janela** — ela passa a **avisar**, nunca a impedir.

---

## Arquivos que a implementação vai tocar

**Novos:** `Domain/Entities/Disponibilidade.cs`, `Domain/Entities/SolicitacaoAgendamento.cs`,
`Application/Servicos/{Disponibilidade,JanelaAtendimento,SolicitacaoAgendamento}Service.cs`,
`src/Clinica.Site/`, a tela de pedidos em `Clinica.Modulo.Recepcao`, os scripts em `tools/vps/`.

**Alterados:** `Acesso.cs` (bit 30 + perfil + rótulo), `Agendamento.cs` (sentinelas + o aviso),
`ClinicaDbContext.cs`, `Migrations/`, `IClinicaRepositorio.cs` + `ClinicaRepositorio.cs`,
`Application/Modelos/Agenda.cs` (`VagaAgenda`), `ModuloRecepcao.cs`, `AgendaViewModel.cs`,
`Whatsapp.cs`, `Clinica.sln`, `.github/workflows/verificar.yml`.

⚠️ Cuidados que valem para qualquer parcela deste repositório e mordem esta em particular: todo
`DateTime` precisa de `.HasColumnType("timestamp without time zone")` (cobrado por
`DatasSemFusoTests`); a migration tem de ser **puramente aditiva** e com carimbo **maior que o da
última**; e não há `dotnet ef` neste ambiente, então o `.Designer.cs` e o `ModelSnapshot` são
escritos **à mão**. O item de menu novo precisa dos **três** pontos casando — declaração,
`Registrar` e o `case` em `CriarTela` —, senão é menu aceso e tela parada, e **nenhuma rede vê**,
porque a chave é uma string à mão dos dois lados.
