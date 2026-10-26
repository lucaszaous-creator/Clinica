# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## O que é o projeto

Sistema de **faturamento** (não recebíveis — não há campos de dinheiro) para clínica médica de acupuntura,
em .NET 8, desktop WPF. O coração do produto é impedir que o **2º código/guia** (obtido +24h depois do
atendimento) seja esquecido, via dashboard de pendências com semáforo. Também cobre o ciclo TISS completo:
lote → envio → retorno → glosa → recurso, com XML TISS 4.01 e guia em PDF no leiaute ANS.

Todo o código, comentários, commits e UI são em **português (pt-BR)** — mantenha esse padrão.

## Comandos

A máquina de trabalho é **Windows 11** (onde a solução inteira compila, WPF incluído); o CI e a
web de leitura também rodam em Linux. Use `python` no Windows e `python3` no Linux. Em Linux, o
`Clinica.Desktop` e a suíte multi-exe (`net8.0-windows`) NÃO compilam — o SDK de lá não traz o
`Microsoft.NET.Sdk.WindowsDesktop`; é para isso que existe a `compilar-sombra`. SDK ausente no Linux:
`apt-get install -y dotnet-sdk-8.0` (o instalador da Microsoft está bloqueado pelo proxy).

```bash
# Compilar TUDO (os cinco apps, módulos, telas WPF e Blazor) — só no Windows
dotnet build Clinica.sln

# Testes (multiplataforma). A suíte completa leva ~15 min: quem a roda é o CI do PR (abaixo)
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj

# Um teste ou uma família — é o que se roda localmente
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj --filter "FullyQualifiedName~RegrasFaturamentoTests"

# A MESMA suíte contra um Postgres de verdade (o job `testes-postgres` saiu do CI em out/2026).
# Windows: o serviço `postgresql-x64-18` já roda nesta máquina (pede a senha do usuário postgres).
# Linux: apt-get install postgresql-16 e `service postgresql start`. As migrations são aplicadas
# UMA vez num banco-modelo e cada teste ganha uma cópia (tests/Clinica.Tests/BancoDosTestes.cs). ~10 min.
CLINICA_TESTES_POSTGRES="Host=localhost;Port=5432;Username=postgres;Password=...;Database=postgres" \
  dotnet test tests/Clinica.Tests/Clinica.Tests.csproj

# Rodar o faturamento (apenas Windows — WPF); os apps da suíte: src/Clinica.Recepcao, .Financeiro, .Gerente, .Clinico
dotnet run --project src/Clinica.Desktop

# A WEB DE LEITURA (set/2026) — `net8.0` puro, roda em Linux. Somente leitura: o dia, o mês e a
# ficha. Ver docs/web-de-leitura.md, inclusive ONDE ela pode ser publicada (não é "na internet").
ConnectionStrings__Clinica="Host=...;Database=...;Username=...;Password=..." \
  dotnet run --project src/Clinica.Web

# A PRÉVIA das telas Blazor com dados fictícios (tools/PreviaBlazor, Sementes/ = uma por tela).
# A página da aula (/aula/{app}/{chave}, onde a moldura e o chat aparecem) pede CLINICA_NAVEGACAO
# apontando para o navegacao.json; sem ele, só /tela/{Nome}.
dotnet run --project tools/PreviaBlazor --urls http://127.0.0.1:5199

# Verificação estática da suíte multi-exe (XAML, pack URIs, tokens, migration destrutiva)
python tools/verificar-suite.py

# Compilação do C# das telas WPF EM LINUX (no Windows, `dotnet build Clinica.sln` já cobre)
python3 tools/compilar-sombra.py

# Migrations — o `dotnet ef` não precisa de banco vivo (ClinicaDbContextFactory); CLINICA_DB é opcional
dotnet ef migrations add NomeDaMigration -p src/Clinica.Infrastructure -s src/Clinica.Infrastructure
```

⚠️ **Confira o carimbo de hora da migration recém-gerada.** Várias migrations deste repositório
foram escritas à mão com horas FUTURAS (`20260731230000`, `20260801000000`), porque não havia
`dotnet ef` no ambiente. O `dotnet ef` carimba com a hora REAL do relógio, então uma migration
gerada hoje pode nascer ordenando **antes** delas — e o EF aplica na ordem do ID, o que faria a
migration nova tentar alterar uma tabela que ainda não existe. Se o ID novo não for o maior de
todos, renomeie os dois arquivos e o `[Migration("...")]` do Designer. `ls Migrations/ | sed
's/_.*//' | sort -u` mostra a ordem real. Migration ainda NÃO publicada pode ser refeita (apague os
dois arquivos, volte o `ModelSnapshot` ao da `main` e gere de novo) — mas reponha o SQL escrito à
mão que ela tinha (gatilhos, índices de expressão) e confira que o snapshot só mudou no que é seu.

### ⛔ Onde os testes rodam: no CI do GitHub (decisão do dono, out/2026)

**Build e suíte completa rodam no CI do PR, não na máquina de trabalho.** A suíte leva ~15 min,
trava a DLL dos testes e o CI já faz o trabalho certo: num `pull_request` o GitHub compila
`refs/pull/N/merge`, a árvore EXATA que a `main` vai ter.

- **CI** (`.github/workflows/build-exe.yml`, runner Windows, só no PR para a `main`): `build`,
  `testes` (6 lotes, inclui `TelasBlazorAbremTests`: toda tela Blazor e cada botão), `conferencias`
  e `publicar`. O push da `main` depois do merge NÃO roda de novo (o runner Windows é faturado 2×).
  Os jobs `gravar`/`montar`/`inventario` são das videoaulas (`Videoaulas (Treinamento)`), não do build.
- **Localmente**, só o que é rápido e focado: `dotnet build` do projeto mexido, `--filter` na família
  de testes da mudança (segundos), `verificar-suite`. Nada de rodar a suíte inteira "para garantir".
- **O que o CI NÃO cobre** desde que o `verificar.yml` saiu (out/2026, para economizar Actions): a
  suíte contra o **Postgres**, os testes do **tablet** (`deploy/tablet` em Python e
  `Clinica.Assinaturas.Tests`) e a **web de leitura**. Mexeu em migration/coluna/consulta, tablet ou
  web? Rode a rede correspondente localmente (ou diga no PR que ela não rodou):

| rede | cobre | não cobre |
|---|---|---|
| CI do PR (`build-exe`) | os cinco apps compilados no Windows + a suíte inteira em SQLite | o Postgres; o SQLite não vê tamanho de coluna, `Kind` de data, `xmin` nem migration |
| `dotnet test` com `CLINICA_TESTES_POSTGRES` | a mesma suíte no Postgres: migrations de verdade, tetos de coluna, datas com fuso, tipos de parâmetro | nada das telas |
| `TraducaoNoNpgsqlTests` (roda no CI, sem banco) | se a consulta TRADUZ para o Postgres (`ToQueryString`) | se ela RODA no Postgres |
| `tools/verificar-suite.py` | XAML, pack URIs, chaves do design system, projetos na solução, **migration destrutiva** | semântica de C# |
| `tools/compilar-sombra.py` (Linux) | o C# dos 10 projetos WPF sem o SDK do Windows | XAML |

Depois de abrir o PR, quem acompanha o CI é o app (Auto-fix): não fique consultando `gh pr checks`
em laço. O Codex do GitHub comenta "revisão falhou" quando a cota dele acaba — não é achado de código.

**Testar sem publicar**: `build-exe.yml` tem `workflow_dispatch` e roda em qualquer branch,
gerando os cinco `.exe` PORTÁTEIS — sem `vpk pack`, então `mgr.IsInstalled` é falso e eles não se
auto-atualizam nem mexem no canal do app instalado. A armadilha não é o exe, é o BANCO: aponte
`ConnectionStrings__Clinica` para uma branch do Neon (a env var vence a config salva e **não grava**
em `%APPDATA%`) e **nunca** use a tela de Setup no build de teste, que grava. Roteiro completo em
`docs/testar-sem-publicar.md`.

### Publicar (release) — cada app tem a sua versão

`.github/workflows/release.yml` empacota UM app por execução com **Velopack** (um canal por app; o
faturamento fica no canal padrão `win` e **nunca muda**) e os apps instalados se auto-atualizam na
próxima abertura. Merge na `main` não publica nada; publicar é decisão do dono, a cada vez.

- **Tag por app**, no commit do merge: `v1.2.3` (faturamento), `recepcao-v…`, `financeiro-v…`,
  `gerente-v…`, `clinico-v…`. A próxima versão é a última tag DAQUELE app + 1
  (`git tag -l "recepcao-v*" --sort=-v:refname | head -1`). Ou Actions → "Release" → Run workflow
  (`gh workflow run "Release (instalador + auto-update)" --ref main -f app=recepcao -f version=1.2.65`).
- ⚠️ **Envie UMA tag por `git push`.** O GitHub não dispara workflow nenhum quando chegam mais de
  3 tags no mesmo push: as tags ficam lá e nenhum release roda (aconteceu em out/2026). Tag que
  ficou sem release se resolve com o Run workflow na mesma versão — o pré-voo só recusa versão que
  já tem RELEASE publicada, não tag.
- **Quais apps publicar** depende de QUEM carrega o código mexido: o Gerente Geral carrega todos
  os módulos (inclusive o do Consultório inteiro); a Recepção e o Financeiro carregam o módulo do
  Consultório só como `ModuloContextual` (sem o menu dele); a moldura Blazor (e o chat da equipe)
  é dos quatro apps da suíte, não do faturamento. Não republique o faturamento sem necessidade:
  ele está em produção, e cada release é download e reinício na clínica.
- O job `sincronizar-canais` de um release pode aparecer **cancelado** quando vários rodam juntos:
  a fila dele guarda só o último pendente, e a sincronização é global — basta o último ter passado.

## ⛔ UX · DX · CX — toda implementação e toda refatoração respondem às três (out/2026)

**Decisão da direção.** Nenhuma implementação ou refatoração — tela, serviço, migração,
ferramenta, CI — é proposta, documentada ou entregue sem dizer o que ela faz com:

- 🧑‍💻 **UX** — quem USA a tela: o que vê, quanto espera, o que pisca, quantos cliques, o
  que acontece quando falha.
- 🛠️ **DX** — quem MANTÉM o código: quantas peças novas, onde a regra mora, como se testa,
  o que a próxima pessoa precisa saber para não quebrar.
- 🤝 **CX** — a CLÍNICA como cliente: confiança no produto ("parece profissional?"),
  custo (máquina, Actions, suporte), risco em produção e o que muda no dia de quem atende.

**Todo `.md` de decisão ou de feature leva as três seções**, e toda proposta com mais de
um caminho traz a COMPARAÇÃO das opções nos três eixos antes da recomendação. E quando a
direção sugere um caminho e pede para ser refutada, a resposta é a comparação — com a
opção dela, a recomendada e as melhores que existirem —, nunca só "concordo, vou fazer".
A tela preta das telas Blazor (um navegador por tela) é o caso que originou a regra: a
escolha foi feita olhando só o DX, e o UX foi a clínica quem mediu, em vídeo.

## ⛔ Regras de tela para quem atende (out/2026 — decisão da direção, valem também no CRM)

O mesmo pedido da direção vale para os cinco apps e para o CRM (`semdor-crm/CLAUDE.md`), e
todo PR diz em **UX · DX · CX** como cada regra foi atendida:

1. **Uma porta por pergunta** — duas telas que levam ao mesmo lugar viram uma; antes de criar
   item, aba ou atalho, procure a tela que já responde.
2. **Função que saiu, sai inteira** — do menu, das telas, das configurações e dos textos.
3. **O que pede ação vira aviso** — resposta do paciente ao lembrete (confirmou, não vem, sem
   resposta) chega ao SINO da Recepção e ao painel do Gerente, não só a uma aba que alguém
   precisa lembrar de abrir. A regra do aviso mora num lugar só (Application, com teste).
4. **O telefone não é identidade** — a ponte devolve as fichas candidatas
   (`ClinicPatientBridge`) e quem confere é a recepção.
5. **Relacionamento do CRM é de quem trata** (BSV, acupuntura e os dois —
   `ClinicRelationshipBridge.Tratamento`).
6. **Responsivo e sem resquício** — tela que se adapta à largura de quem usa e nada de
   botão para função morta.

## ⛔ AUDITORIA DE LINHA — a conferência é NA ESCRITA, e não existe rodada depois

**Regra de processo, decidida em 23/08/2026 e reafirmada pela direção em 24/08/2026.**

⛔ **É PROIBIDO rodar auditoria adversarial — agentes, workflow, varredura de lentes — sobre
código que você mesmo acabou de escrever.** Não é opcional nem "por segurança". Rodada
posterior é o SINTOMA de que a conferência não foi feita na hora, e ela custa: a de 23/08
gastou **12,7 milhões de tokens e 98 minutos** para achar o que a lista abaixo pega em
segundos, no teclado — e a metade das refutações dela nem chegou a rodar, o que obrigou a
reconferir tudo à mão de qualquer forma. **Quem escreve confere enquanto escreve; quem
termina, termina certo.**

O que a lista substituiu: aquela auditoria devolveu 38 candidatos e 13 defeitos reais, quatro
deles bloqueadores, num código com 1778 testes verdes, três redes locais verdes e CI verde.
**Todos teriam sido baratos de evitar.** A lista abaixo é o resumo de cada um deles — ela
existe para a próxima rodada não precisar acontecer.

**Portanto: cada linha acrescentada, mudada ou removida passa pela lista abaixo NO MOMENTO em
que é escrita** — antes de salvar o arquivo, não depois de fechar a parcela. Quando um item
pegar algo, **escreva a lição aqui embaixo**; foi assim que esta lista nasceu, e é assim que
ela deixa de precisar de agente nenhum.

⚠️ **A pergunta que abre toda conferência é sempre a mesma:** *isto quebra alguma coisa, ou
apenas deixa de fazer o que promete?* Os quatro bloqueadores de 23/08 eram todos do segundo
tipo — nada falhava, e é exatamente por isso que nenhuma rede os pegava.

⚠️ **A única coisa que se roda DEPOIS é a seção 6 e o CI do PR** — as redes e os testes: são
determinísticos, custam minutos e não precisam de agente nenhum. Percorrida a lista e verdes
as redes, o trabalho está terminado. Defeito que ainda assim escapar vira **lição aqui** e,
quando o ruído medido permitir, **uma checagem nova no `verificar-suite`** — é assim que a
conferência fica automática, e não repetindo a rodada que esta regra existe para aposentar.

### 1. Campo novo de PRONTUÁRIO — os oito lugares

Nenhum deles quebra o build quando é esquecido:

1. a entidade + o mapeamento no `ClinicaDbContext` (`DateTime` **exige**
   `HasColumnType("timestamp without time zone")` — o Npgsql recusa `Kind=Local`);
2. migration **aditiva** (confira o `defaultValue` de coluna não-anulável: o EF põe o padrão
   da LINGUAGEM, e quase nunca é o que as linhas já gravadas valem);
3. **a cópia do serviço** — `SalvarAsync` copia campo a campo; o que não estiver na lista é
   apagado na primeira EDIÇÃO, e a CRIAÇÃO funciona, o que esconde o defeito;
4. **o versionamento** (`GuardarVersao` + a coluna na entidade de versão) — sem ele a
   correção apaga o anterior sem rastro, contra o art. 3º da Lei 13.787/2018;
5. **a validação de "registro vazio"** — senão o caso legítimo (a primeira consulta é
   história + achado + hipótese ANTES de haver conduta) é recusado nomeando campos que a
   pessoa preencheu;
6. **TODAS as portas de edição** — *quem não edita, PRESERVA*: a janela que não tem o campo na
   tela precisa carregá-lo e devolvê-lo intacto, senão ela o apaga;
7. **o PDF, a exportação, o art. 18 II e a guarda** (ponto 8 do compromisso de conformidade);
8. **a busca do prontuário** e o `CatalogoRegistroClinico`;
9. **os CAMPOS PERSONALIZADOS** (set/2026) — o `GuardarVersao` os guarda como TEXTO, e
   eles são a variante mais fácil de esquecer do lugar 4: não aparecem na entidade como
   propriedade, então nada quebra quando ficam de fora;
10. **os TRÊS leitores que só MOSTRAM a sessão** — o `ModeloEvolucao` (parcela 76), o
   painel da sessão anterior (`ResumoSessaoAnterior`, parcela 77) e a janela que ABRE a
   sessão (`SessaoDoProntuario`, set/2026). Os três já ficaram para trás uma vez cada, e
   pelo mesmo motivo: esquecer um não quebra build, não quebra teste e não acusa em rede
   nenhuma — o campo simplesmente não aparece para quem lê. O terceiro é o único com
   rede: `SessaoDoProntuarioTests.Todo_campo_escrito_aparece_em_algum_bloco` varre a
   entidade por REFLEXÃO, e o que não for conteúdo clínico tem de ser DECLARADO ali com
   a razão. Lista de campos escrita à mão é lista que a próxima parcela não alcança.

### 2. Método de repositório novo

- **Alergia pertence ao paciente, não ao atendimento ou termo.** Todas as portas de relato,
  inclusive o portal do tablet, usam a normalização e reutilização de `ProblemaPacienteService`
  dentro de `ExecutarRegistroAlergiaAtomicoAsync`. No atendimento, os nomes únicos aparecem
  em uma única entrada; observações/CIDs distintos ficam nos detalhes, sem apagar o histórico.

- **Formatação da evolução acompanha o texto em todas as portas** (out/2026): o texto
  simples continua canônico; `TextoEvolucaoFormatado` guarda apenas negrito/itálico e
  só é aceito quando seus trechos recompõem esse mesmo texto. Cliente antigo que omite
  a propriedade (`null`) preserva a formatação se o texto não mudou; o editor envia
  string vazia para removê-la explicitamente. Alterar o texto sem metadados descarta
  estilos antigos. Guardar também nas versões, reutilização da sessão, leitores,
  relatório/PDF e exportação. O teste nativo exercita salvar, reabrir, copiar e remover
  estilo sem mudar as palavras; somente testar texto simples não detecta essa perda.

- **Nasce com um teste que o EXECUTA**, mesmo trivial. Consulta LINQ só se prova executando:
  a tradução acontece em RUNTIME, e método sem chamador em teste é **código que ninguém rodou**.
- **Nunca use propriedade DERIVADA** (`=> X is not null`) dentro de `Where`/`OrderBy`/`Select`
  traduzido — use a COLUNA. O EF recusa em runtime.
- **Entra em `TraducaoNoNpgsqlTests`** se usar navegação, agregação ou função: os testes rodam
  em SQLite e a clínica roda Postgres, e o suporte a tradução DIFERE.
- **Meça o que a consulta TRAZ.** Ler o prontuário inteiro para extrair três frases é meio
  megabyte por troca de paciente.

### 3. Leitura composta

- **SEQUENCIAL, nunca `Task.WhenAll` sobre o mesmo `IClinicaRepositorio`** — é o mesmo
  `DbContext`, e ele não aceita duas operações ao mesmo tempo. **O SQLite dos testes esconde**:
  ele responde quase sincronamente e as consultas nunca se sobrepõem.
- Leitura composta nova ganha um teste com o **interceptor de lentidão**
  (`CabecalhoClinicoTests.LentidaoDeRede`).
- **Leituras INDEPENDENTES correm juntas, cada uma no seu escopo** (`ILeituraParalela`,
  out/2026): com o banco a ~250 ms por ida, oito conferências em fila são 2 s; em paralelo, o
  tempo da mais lenta. Sem `ILeituraParalela` (testes) o serviço continua em fila. O resultado
  tem de ser o MESMO — `ElegibilidadeParalelaTests` compara as duas formas, ordem incluída.
- **A mesma pergunta duas vezes na mesma carga não vai duas vezes ao banco**: o repositório
  memoriza por poucos segundos as leituras de clínica inteira (códigos em aberto, consultas,
  carteirinhas) e a fotografia das configurações, e TUDO isso some em qualquer `SaveChanges`
  do escopo. Leitura de UM paciente se filtra no banco, nunca trazendo a clínica e filtrando
  em memória. Detalhes em `docs/desempenho-leituras-2026-10-15.md`.

### 4. ViewModel e tela

- **Leitura e cópia de sessão:** abrir ou copiar um registro anterior usa `SessaoDoProntuario`, incluindo os campos personalizados já carregados, em vez de concatenar o resumo da linha do tempo. Texto clínico para seleção usa `TextoSelecionavel` com binding `Mode=OneWay`; botões de cópia usam `Copiavel.Texto`, que trata também teclado. Copiar não altera o rascunho do atendimento.
- **Cabeçalho em tela estreita com zoom:** ações de largura fixa não podem comprimir nome e contexto clínico até consumir toda a altura. Em largura reduzida, as ações passam para outra linha. A verificação cobre explicitamente 1044×788 a 150%, com editor e Salvar acessíveis, independentemente do monitor da máquina de teste.

- **Escopo do executável:** registrar componentes compartilhados não autoriza publicar seus menus/rotas. O Faturamento usa `ModuloFaturamentoAplicativo`, conferido pela mesma composição na verificação Windows, para abrir apenas Financeiro/Faturamento de guias mesmo com acesso de gerente. Testar abertura, busca e navegação direta; `Oculto` sozinho não bloqueia uma rota.

- **⛔ TELA NOVA É BLAZOR — o padrão da casa desde out/2026** 🎨 (decisão da direção, depois
  de ver o piloto do Fluxo de caixa: *"Simplesmente perfeito! … para que toda tela seja feita
  dessa forma"*). O guia completo, com o passo a passo, está em **`docs/telas-blazor.md`** —
  leia ANTES de escrever a primeira linha. Em uma frase: **a tela é HTML/CSS em Razor
  (`Clinica.Ui.Blazor`), o C# dela chama os MESMOS serviços da Application, e o WPF só a
  hospeda** (`TelaBlazor` é o descritor da tela; a janela do app é UM navegador — `JanelaDoApp` +
  `Palco`, ver o 🕰️ abaixo — e desde out/2026 a própria moldura, a entrada e o menu também são
  Razor, em `Clinica.Ui.Blazor/Moldura`). O que isso exige, sempre:
  - 🧱 **Componentes antes de HTML solto**: `CabecalhoPagina`, `Kpi`, `Modal` (o formulário),
    `Campo`, `Aviso`, `EstadoVazio`, `FalhaDeLeitura`, `Selo`, `SeletorMes`,
    `SeletorDePaciente`, `Icone`. Tela que redesenha um deles é a segunda cópia.
  - 🎨 **Cor, raio e espaço só de TOKEN** (`tokens/*.css`, o espelho do `Tokens.xaml` conferido
    no CI). Número solto no CSS é dívida no ato — a regra do design system vale igual aqui.
    Estilo próprio da tela mora em `Ativos/css/telas/<tela>.css`; o comum, em `clinica.css`.
  - 🔐 **As duas barreiras**: o botão nasce `disabled` sem o bit (`Pode`) e o clique chama
    `Exigir`/`ExigirAlgum` da `TelaBase`, que DEVOLVE FALSO e diz por quê — exceção solta num
    evento derruba o circuito da tela (a `Raiz` tem um `ErrorBoundary` como última rede, mas
    rede não é desenho).
  - 🔁 **Escopo por operação** (`using var escopo = Escopo();`) — nunca serviço Scoped da
    Application injetado na tela (checagem 37 pelo outro lado: a aba vive o dia inteiro).
  - 🧮 **Contador de geração** em toda carga, publicação DEPOIS do último `await`, e o
    **terceiro estado** (`FalhaDeLeitura`) quando a leitura falha — vazio por erro não tem a
    cara de vazio por não haver nada.
  - 💬 **Diálogo é HTML** (`Dialogos`: confirmar, perguntar texto, avisar) e **formulário é
    `Modal`** em `Formularios/` — nunca janela do sistema. Cancelar uma pergunta é `null`,
    NUNCA texto vazio (checagem 39). Feedback de formulário é INLINE (`Aviso`); a confirmação
    passageira de ação de linha vai para `Notificacoes`.
  - 💾 **Disco, área de transferência e navegação passam pelo `IHostDaTela`** (CSV, PDF,
    escolher arquivo, copiar, `NavegarAsync`) — o hospedeiro WPF reusa as rotinas do shell, e
    a prévia responde com uma frase. CSV é saída de dado: `Exigir(VerFinanceiro)` antes.
  - 💰 **Dinheiro e data em pt-BR FIXO** (`Formato`), número de SVG em invariante, e o que a
    pessoa digita lido pela `LeituraDeValores` da Application (a mesma do WPF).
  - 📸 **A razão de existir é VER a tela antes de ela chegar à clínica**: `tools/PreviaBlazor`
    roda a MESMA tela em Linux com dados fictícios (`Sementes/`, uma por tela, achadas por
    reflexão), e `tools/PreviaBlazor/foto.js` a fotografa. **Toda tela é fotografada em 1024
    e 1366, e todo formulário aberto**, antes do push. Foi assim que apareceram o valor cortado
    ("R$ 981.87…") dos cartões em 1024 e o selo "Previsto" inchado pela classe `.aviso` que
    ele herdava do alerta — nenhuma rede pegaria nenhum dos dois.
  - 🤖 **E o `dotnet test` ABRE cada tela e clica cada botão** (`TelasBlazorAbremTests`,
    out/2026): a MESMA `Raiz` do WPF, com bUnit, sobre os serviços do app e as MESMAS
    `Sementes/` da prévia, com a direção logada. Reprova com o cartão do `ErrorBoundary`, com
    `FalhaDeLeitura` na abertura e com exceção que não é recusa de regra no `Diagnostico` —
    um clique por RÓTULO, descendo dois níveis no que o clique faz aparecer (aba, formulário,
    diálogo). Tela nova entra sozinha (é achada por reflexão); **semente nova vale para os
    dois**. A foto continua sendo o que vê leiaute; o teste é o que vê a tela QUEBRAR. Ele
    nasceu provado: um `throw` posto no botão de um formulário reprovou «Nova categoria →
    Entrada», e a primeira rodada achou a tela de Estoque lendo materiais sem login.
  - ⚠️ **O que decide o que a tela AFIRMA continua morando na Application**, com teste — Razor
    não é lugar de regra de negócio. A tela formata e desenha; o serviço decide.
  - ⚠️ O pacote `Microsoft.AspNetCore.Components.WebView.Wpf` só tem `lib/net8.0-windows`; o
    `compilar-sombra` o troca pelas dependências `net8.0` + a DLL baixada
    (`PACOTES_SO_WINDOWS`). Pacote só-Windows novo entra nessa tabela, senão a sombra para.
  - 🕰️ **Histórico**: em out/2026 as telas em HTML do WebView2 (Financeiro, prescrições,
    infusão, agenda do médico, fila…) tinham voltado para WPF por causa da tela PRETA de 1–5 s
    a cada abertura. ⚠️ **E a primeira versão Blazor repetiu o flash** (~100 ms pretos a
    cada tela, a clínica mandou o vídeo): ela criava um `BlazorWebView` POR TELA, e a janela
    nativa do WebView2 nasce preta antes de o navegador existir dentro dela — pintar o fundo
    em três camadas (controle WPF, `WEBVIEW2_DEFAULT_BACKGROUND_COLOR`, `index.html`) não
    cobre esse instante. **O navegador é UM POR JANELA** (desde out/2026 a `JanelaDoApp`
    inteira: o conteúdo dela é UM `BlazorWebView`, e a entrada e a moldura trocam dentro dele),
    criado escondido e só mostrado depois da primeira renderização; as telas trocam DENTRO dele (`Palco` + `PalcoDeTelas`), ficam montadas
    ocultas e cada uma rola sozinha. **Nunca crie `BlazorWebView` por tela.** Os arquivos vão
    embutidos na DLL e o Razor chama o serviço direto. **Não reintroduza HTML escrito à mão
    conversando por mensagem**; tela HTML é Razor.
  - 🗂️ As telas WPF dos OUTROS módulos continuam valendo e se mantêm em XAML até serem
    migradas; tela NOVA não nasce em XAML. O Financeiro e o Faturamento de guias já são só
    Blazor (o Faturamento desde out/2026 — `docs/faturamento-blazor.md`).
- **Tela de leitura que volta do histórico** implementa `IAtualizarAoVoltar`: reaparece na hora
  com o que mostrava e relê em silêncio (sem "Carregando", sem limpar, falha só no log).
  Formulário e prontuário em edição NÃO implementam — voltar não pode reler por baixo de quem digita.
- **Carga async** disparada por tecla/clique/timer/troca-de-paciente: **contador de geração**.
- **Disponibilidade da agenda:** só libere vagas depois de concluir todas as leituras do período. Na visão por sala, combine o profissional selecionado com a sala de cada coluna; preserve o atalho de sobreposição e a trava. Regressões na PR #205: `tools/ValidarLayoutWindows` cobre ocupação cruzada, trava ligada/desligada e leitura pausada após os bloqueios.
- **Entre o `Clear()` e o último `Add` não pode haver `await`.**
- **Timer**: quem liga e desliga é a VIEW, no `Loaded`/`Unloaded`. No ViewModel ele mantém viva
  cada tela já trocada — e leva as sub-ViewModels junto.
- **Comando que grava**: as DUAS barreiras (`IsEnabled` explica, `Exigir`/`ExigirAlgum` impede).
- **Conversor**: confira o TIPO. `BooleanToVisibilityConverter` devolve `Collapsed` para tudo
  o que não é `bool`; texto pede `TextoParaVisibilidade`, objeto pede `ObjetoParaVisibilidade`.
- **Tela de vida longa não recebe serviço `Scoped` no construtor** — abre escopo por operação.
  ⚠️ A checagem 37 olha o CONSTRUTOR: `GetRequiredService<X>()` DENTRO dele escapa dela.
- **`<Style>` local num controle da casa vai SEMPRE com `BasedOn="{StaticResource {x:Type
  ctrl:X}}"`.** Não há `themes/generic.xaml` em projeto nenhum: o `Template` mora no estilo
  IMPLÍCITO do design system, e estilo local sem `BasedOn` o SUBSTITUI. O controle continua
  vivo, com as propriedades certas, desenhando **nada** — e nenhuma rede pega.
- **Diálogo de texto: Cancelar não é resposta em branco.** `PerguntarTexto` devolve `null`
  quando a pessoa DESISTE; pergunta que anuncia o campo como opcional passa
  `obrigatorio: false` e o chamador testa `is null`. Sem isso a única saída de quem não
  quer escrever é o Cancelar — e ele grava. (Checagem 39.)
- **`--` é ilegal dentro de comentário XML** (a linha de sublinhado do estilo da casa).
- **Contrato SÍNCRONO servido por diálogo Blazor: nunca dentro de um evento do Blazor**
  (out/2026, `DialogoService`). Com a moldura em Blazor, o `IDialogoService` desenha a pergunta
  em HTML e espera a resposta num laço de mensagens aninhado. Chamado no MEIO de um evento ou de
  uma renderização do próprio Blazor, isso trava a tela para sempre: o renderizador só desenha o
  diálogo quando o evento termina, e o evento está esperando o diálogo. Por isso o serviço cai na
  caixa nativa quando a pilha passa pelo `Renderer` (e quando a pergunta vem de dentro de uma
  janela WPF modal, que desabilita a principal). **Tela Blazor pergunta pelo `Dialogos`
  assíncrono, nunca pelo `IDialogoService`.**

### 5. Perguntas que só o autor pode responder

- **Última prescrição emitida:** confira os estados produzidos pelo fluxo atual. A continuidade usa `SituacaoPrescricao.Liberada`, diferente de `Assinada`, e precisa entrar junto de `Assinada` e `Encerrada` na consulta de cópia. Teste o estado `Liberada` explicitamente: apenas zerar `AssinadaEm` numa folha `Encerrada` não exercita esse caminho.
- **Carimbo de hora novo na fila?** Entra no bloco que a REMARCAÇÃO limpa, e decida se ele é
  COLUNA — se não for, não pode ser gasto como um passo do "voltar etapa".
- **Seção nova no MEIO de uma lista indexada?** Ela empurra todos os índices abaixo dela, e
  índice de navegação não quebra build: ele abre a seção ERRADA. A saída não é lembrar — é
  **trocar o contrato de ÍNDICE por NOME** e casar a lista do C# com os rótulos da tela
  posição por posição (`ModuloClinico.SecoesDoPaciente` + checagem 38).
- **RAIZ CLÍNICA nova entra em `PacienteTemRegistroClinicoAsync` no MESMO commit.** A FK dela
  é cascata; sem a linha, a ficha cujo único registro é aquela natureza continua REMOVÍVEL e a
  exclusão a leva por arrasto — a cascata da parcela 60, com o teste verde ao lado. A parcela
  75 acrescentou a oitava raiz e esqueceu a linha, com o comentário do próprio método
  descrevendo o cenário logo acima.
- **Checagem nova promete só o que ENTREGA.** A 38 nasceu com um cabeçalho de três casos e
  detectava dois — o mesmo defeito de "garantia aparente" que este projeto recusa desde a
  parcela 3, cometido numa ferramenta. Ou o cabeçalho encolhe, ou a checagem cresce; aqui ela
  cresceu, e o que a tornou possível foi mudar o contrato de índice para nome.
- **Booleano reusado numa segunda decisão?** Releia se ele responde à SEGUNDA pergunta. O nome
  não avisa. (`SessaoEmBranco` decide se a tela PERGUNTA; usá-lo para decidir se GRAVA
  descartava a sessão inteira.)
- **Escreveu um serviço que RETIFICA?** Ele recebe **tudo** o que o que REGISTRA recebe —
  senão a correção vira uma reescrita mutilada.
- **Escreveu um comentário justificando uma decisão?** Confira se o código FAZ o que ele diz.
  Comentário que explica uma decisão errada a torna invisível para o próximo revisor.
- **A tela nova mostra tudo o que o modelo carrega?** Campo calculado sem leitor é o defeito
  recorrente do projeto.

### 6. Antes de todo commit

```bash
git status --porcelain --untracked-files=all   # nenhum arquivo de prova pode entrar
python tools/verificar-suite.py                # segundos; pega migration destrutiva e XAML
dotnet build <projeto mexido> --no-incremental # e os testes da família mexida, com --filter
# A suíte completa e os cinco apps: no CI do PR (ver "Onde os testes rodam").
# Fora do CI, quando o assunto toca (ou diga no PR que não rodou):
#   migration/coluna/consulta → a suíte com CLINICA_TESTES_POSTGRES
#   tablet → python3 -m unittest discover -s deploy/tablet -p 'test_*.py' e Clinica.Assinaturas.Tests (-c Debug)
#   web de leitura → dotnet build src/Clinica.Web
```

⚠️ **`dotnet build` INCREMENTAL esconde warning de arquivo que ele não recompilou.** Ao
mudar a assinatura de um método público (set/2026: o retorno de `AnexoPacienteService.
Montar` virou anulável), o chamador que ficou intacto não é recompilado — e o build
responde "0 Warnings" sobre um `CS8604` que existe. `--no-incremental` é o que mostra a
verdade, e é o que o CI faz por partir de zero. É a mesma família do "a suíte ficou verde
contra uma árvore diferente do commit" (parcela 74): **a resposta verde vale pelo que foi
MEDIDO, não pelo que foi perguntado.**

⚠️ **O CI testa a árvore que FOI COMMITADA.** Em 23/08 a suíte local ficou verde porque um
arquivo estava no COMMIT e tinha sido apagado do DISCO — e era justamente o que reprovava. Mais
um motivo para a suíte que vale ser a do CI.

⚠️ **Arquivo de prova temporário: LEIA antes de descartar.** Os dois que vazaram naquele dia
renderam — um revelou o defeito da anamnese, o outro virou a rede de tradução.
## Arquitetura

Camadas clássicas, todas em `src/`:

- **Clinica.Domain** — entidades, enums (`Enums.cs` concentra quase todos) e o **motor de regras**:
  uma classe `Regra<Convenio>` por fluxograma de convênio, todas implementando `IRegraConvenio`
  (`Gerar(paciente, atendimento, contexto) → ResultadoFaturamento` com os códigos, datas previstas e
  categoria/semáforo). `RegistroRegras` resolve a regra pelo convênio; `RegraGenerica` +
  `ConfiguracaoRegraGenerica` atendem convênios personalizados criados em runtime (via
  `CatalogoConvenios`, alimentado do banco).
- **Clinica.Application** — serviços de caso de uso (`Servicos/`) orquestrando o repositório e as regras.
  Ponto único de acesso a dados: `Abstracoes/IClinicaRepositorio`. Os principais: `AtendimentoService`
  (gera os códigos ao lançar atendimento), `PendenciaService` (alimenta o dashboard: 2º códigos,
  consultas a renovar, glosas com prazo de recurso, carteirinhas), `LoteTissService`/`TissExportService`
  (lotes e XML TISS 4.01), `GlosaService`, `ParametrosService` (configuração global no banco, com snapshot).
- **Clinica.Infrastructure** — EF Core + Npgsql (PostgreSQL/Neon), `ClinicaDbContext`,
  `ClinicaRepositorio` (única implementação do repositório), migrations. Migrations são aplicadas
  **automaticamente na abertura do app** (`App.xaml.cs` → `MigrateAsync`).
- **Clinica.Desktop** — WPF/MVVM. `App.xaml.cs` é o bootstrap: auto-update Velopack → obtenção da
  connection string (env `ConnectionStrings__Clinica` → `ConexaoStore` criptografado via DPAPI em
  `%APPDATA%\ClinicaFaturamento` → tela `SetupWindow`) → host DI → migrations → `MainWindow`.
  ViewModels em `ViewModels/` (um por seção da sidebar, registrados em `App.ConstruirHost`), design
  system em `Styles/` (tokens + um ResourceDictionary por família de componente; documentado em
  `docs/design-system/`).
- **Suíte multi-exe** (`Clinica.Desktop.Shell`, `Clinica.Modulo.*`, `Clinica.Recepcao`,
  `Clinica.Financeiro`, `Clinica.Gerente`, `Clinica.Clinico`) — quatro executáveis NOVOS, ao lado do faturamento e sem
  encostar nele. O shell tem o design system, a janela genérica, o contrato de módulo (`IModuloApp`),
  a abertura padrão (`SuiteApp`) e a janela do app com a entrada (`JanelaDoApp` + a moldura Blazor
  em `Clinica.Ui.Blazor/Moldura` + `SessaoUsuario`); cada módulo é uma
  **biblioteca** com suas telas; cada `.exe` é uma casca que só escolhe a lista de módulos — o
  Gerente Geral carrega todos, incluindo o `Clinica.Modulo.Gerente` (BI, campanhas, acessos e a
  leitura do faturamento), que só ele carrega. O `Clinica.Clinico` (Consultório, parcela 36)
  carrega um módulo só: a máquina do médico não precisa da agenda do balcão nem do caixa. **Leia `docs/arquitetura-multi-exe.md` antes de
  mexer aqui**: fases, débito assumido (design system e log duplicados, agora permanentes) e
  canais de release.
- **tests/Clinica.Tests** — xUnit; os testes de regras validam cada fluxograma de convênio de ponta a
  ponta usando repositório fake em memória (sem banco).

⚠️ **O FATURAMENTO (`Clinica.Desktop`) SAIU DO CONGELAMENTO NA PARCELA 45 — e o cuidado que o
congelamento protegia continua valendo por inteiro.** A cliente pediu três coisas dentro dele
(formato do número da guia por convênio, login com permissões granulares e filtros na consulta de
guias), e feature pedida no app que fatura a clínica não se entrega noutro app. O que mudou foi a
proibição de **editar arquivo**; o que NÃO mudou é a razão dela: **ele fatura a clínica hoje, roda
em produção e não tem quem o teste antes do usuário.**

Na prática, o que continua valendo:

1. **Migration nele é SÓ ADITIVA** (checagem 18 do `verificar-suite.py`). Nada de renomear nem
   remover coluna que ele usa.
2. **Mudança nas camadas compartilhadas chega à tela dele sem ninguém abrir uma pasta sua.** Os
   três caminhos que já morderam: (a) **valor novo num enum embutido** — `Especialidade`,
   `ModalidadeAtendimento`, `Convenio`, `TipoCodigo` — vira opção nova no seletor do lançamento,
   porque os catálogos garantem as embutidas por `Enum.GetValues`; (b) **alerta ou recusa nova num
   serviço que ele chama** (foi por aí que a crítica do número da guia chegou às quatro portas de
   baixa de uma vez); (c) **migration destrutiva**. `FaturamentoCongeladoTests` fixa a superfície
   (a); a checagem 18 pega (c); (b) é julgamento, e a pergunta continua sendo **"isso vai aparecer
   na tela de quem fatura amanhã de manhã?"** — só que agora a resposta "vai" às vezes é o objetivo,
   e aí ela precisa vir com teste e com a lição escrita aqui.
3. **Não tire capacidade de quem já a usava.** Foi a regra que desenhou o padrão do perfil
   `Faturista`: ele nasceu com exatamente o que o app deixava fazer antes do login, inclusive
   marcar na agenda e cadastrar paciente. Versão que introduz permissão e, de quebra, tira uma
   função que a pessoa usava ontem vira chamado de suporte na segunda de manhã — e o pedido era o
   contrário, poder **liberar ou não**, caso a caso, na tela de Acessos.
4. **A Fase 4 continua cancelada**: o faturamento não vira módulo da suíte. Ele tem o design system
   dele, o log dele e agora a tela de login dele — e não pode referenciar `Clinica.Desktop.Shell`,
   porque os dois declaram tipos no namespace `Clinica.Desktop.Controls` e as referências ficariam
   ambíguas. Foi por isso que `SessaoUsuario` subiu para `Clinica.Domain`: compartilhar a DECISÃO
   sem compartilhar a janela.

Feature nova que não seja do faturamento continua indo para Recepção, Financeiro, Consultório ou
Gerente. O que cada módulo deve entregar, e em que ordem, está em `docs/features-por-modulo.md` e
`docs/entrega-ao-cliente.md`.

### ⛔ CONGELADO: SafeID e assinatura digital — decisão da direção, 14/08/2026

**Não encoste em nada relativo ao SafeID e à assinatura digital sem autorização expressa.**
O mapa completo — o que é núcleo, o que é fronteira, o que ficou de fora e o que fazer se
algo parecer errado — está em **`docs/safeid-congelado.md`**. Leia ANTES de tocar em
qualquer arquivo de `Assinatura/`, `SafeID/` ou `PublicacaoDocumentoService`.

As três razões, em uma linha cada:

1. **Cada tentativa de assinatura é COBRADA pelo PSC** — depurar aqui gasta dinheiro da
   clínica, e não existe "testar de novo" de graça.
2. **É a única parte do sistema com valor jurídico afirmado no rodapé** — erro aqui não
   produz tela feia, produz documento que parece oficial e não é.
3. **Verde não quer dizer funciona**: os testes usam certificado autoassinado em memória e
   SQLite; eles não enxergam e-CPF real, cadeia ICP-Brasil, `timestamp with time zone` nem
   o formato que a Safeweb de fato emite. Foram **sete rodadas** em produção até funcionar.

Se algo parecer errado: **peça o log** (`Diagnostico` grava a inner exception inteira),
**peça o commit do build**, **reproduza antes de corrigir** e **teste em homologação**.
Não corrija por dedução — foi assim que três correções erradas chegaram à clínica.

✅ **Autorização expressa de 19/10/2026 — SÓ A CAMADA DE TELA.** A direção autorizou converter
para Blazor as janelas desta área (certificado A1, coleta do termo, prescrição de infusão,
folha de execução, evolução de enfermagem da folha) e fará os testes de assinatura em
homologação. **O motor continua congelado**: `Assinatura/`, `SafeID/`, os serviços de
assinatura, `PublicacaoDocumentoService`, hash, PDF/PAdES, certificado e token. A tela nova
chama os mesmos serviços, com os mesmos argumentos, na mesma ordem — a ordem que era protocolo
mora em `Application/Modelos/AssinaturaNaTela.cs`, com teste. Mapa e roteiro de homologação:
`docs/safeid-congelado.md` e `docs/safeid-telas-blazor-out2026.md`.


### ⛔ COMPROMISSO DE CONFORMIDADE — leia antes de mexer em qualquer coisa clínica

A cliente enviou, por escrito, os **dez pontos** que ela verificaria ao contratar um
prontuário eletrônico (LGPD 13.709/2018 + Lei 13.787/2018). Eles deixaram de ser uma
avaliação e viraram **compromisso do produto**: o mapa completo, com o que atendemos e o
que não, está em `docs/conformidade-lgpd.md`, e é lá que se atualiza o placar.

Aqui ficam só as regras que a construção não pode violar. **Toda parcela nova passa por
esta lista**, e a pergunta é sempre a mesma: *isto tira, esconde ou enfraquece alguma
garantia que já demos?*

1. **Registro clínico NÃO SE APAGA.** Nunca acrescente `Remove()`, `ExecuteDelete` nem
   método `Remover*Async` para evolução, anexo, avaliação, medida, documento ou
   prescrição. Cancela-se com **motivo obrigatório**, e a linha fica. Vale para entidade
   clínica NOVA também — nascer com exclusão é nascer fora da lei da guarda de 20 anos.
   `ConformidadeProntuarioTests` falha se um desses métodos voltar à interface.
2. **Alterar registro clínico guarda o que ele dizia antes.** Sobrescrever no lugar é
   apagar devagar. Se a entidade nova é editável e é prontuário, ela precisa do
   equivalente a `VersaoEvolucao` — e de leitura para recuperá-la.
3. **Migration que mexe em tabela clínica é ADITIVA.** Coluna que guarda dado de saúde não
   se renomeia nem se remove: o dado tem de sobreviver 20 anos, e migration destrutiva é o
   caminho mais rápido de perdê-lo. (A checagem 18 já cobre o faturamento; aqui a regra é
   por decisão, não por ferramenta.)
4. **Tela nova que ABRE prontuário registra o acesso.** `AcessoProntuarioService.
   RegistrarAsync`, disparado na **troca de paciente** (nunca a cada `CarregarAsync` — as
   telas recarregam a cada tecla). Sem isso o ponto 3 tem buraco, e buraco em trilha de
   acesso só aparece quando alguém precisa investigar.
5. **Permissão nova separa dado SENSÍVEL de dado cadastral.** `VerFichaPaciente` /
   `EditarPaciente` são contato; `VerProntuario` / `EditarProntuario` são saúde (art. 5º,
   II). Bit que junte os dois desfaz a parcela 49 e devolve a evolução inteira a quem só
   precisa marcar horário.
6. **Quem assina a ação é quem fez LOGIN.** `SessaoUsuario.Atual.Operador`, jamais
   `Environment.UserName` — no balcão duas pessoas dividem a máquina, e a trilha
   responderia o nome da máquina para "quem fez isso?".
7. **Auditoria grava no MESMO `SaveChanges` do ato.** Ação que possa acontecer sem a linha
   correspondente é ação sem trilha.
8. **Entidade clínica nova entra na EXPORTAÇÃO e na GUARDA.** `ExportacaoProntuarioService`
   e `GuardaProntuarioService` precisam enxergá-la, senão a clínica exporta um prontuário
   incompleto e calcula o prazo pelo registro errado. O backup pega a tabela sozinho (ele
   lê o modelo do EF) — a exportação e a guarda, não.
9. **Não prometa garantia que o código não dá.** É a regra mais antiga do projeto (parcela
   3, o carimbo escaneado) e a que mais aparece aqui: sem LTV, o rodapé diz que a
   assinatura é PAdES-B; sem certificação SBIS/CFM, o sistema não substitui o papel; o
   backup não sabe se a pasta está fora da máquina, então isso é orientação na tela e não
   promessa. **Garantia aparente é pior que ausência de garantia.**
10. **Dado clínico novo em serviço externo é transferência internacional** (art. 33) até
    que se prove o contrário. Nenhuma integração nova manda prontuário para fora sem passar
    pela decisão do ponto 10 do documento.

⚠️ **Motor não é porta, e a auditoria olha a porta.** Vários pontos estão com o serviço
pronto e sem tela (guarda, exportação, trilha de acesso, configuração de backup) — a lista
viva está no placar de `docs/conformidade-lgpd.md`. Enquanto a porta não existe, **a
resposta honesta a quem pergunta é "o motor existe, a clínica ainda não alcança"**, e é
assim que se escreve no documento. Marcar ✅ porque o teste passa é a variante mais cara do
defeito recorrente do projeto: aqui ela vira promessa a um cliente que está auditando.

## Onde está o resto: regras de negócio e convenções

Este arquivo guarda só o que vale para TODA mudança. O conhecimento por área mora em dois
arquivos que você LÊ quando a tarefa toca a área (cada um abre com o índice dos títulos):

- **`docs/agentes/regras-de-negocio.md`** (187 tópicos) — o que o código não diz sozinho:
  faturamento (2º código, TISS, glosa, número da guia, permissões), o circuito do particular e o
  financeiro (pacotes, caixa, cartão, tributos, repasse, estoque), agenda e recepção (busca de
  paciente, lançamentos, confirmações, SMS), Consultório e prontuário (evolução, enfermagem,
  infusão, prescrição, documentos clínicos, certificação digital, LGPD), BI e direção.
- **`docs/agentes/convencoes.md`** (208 tópicos) — como se escreve aqui: design system,
  WPF/XAML (bindings, conversores, teto e piso de tela), Blazor, EF/Npgsql (tradução, `Task.WhenAll`,
  migrations), as checagens do `verificar-suite`, assinatura digital (DER/CMS, carimbo), impressão
  e PDF, ponte do CRM e cada rodada de UI com o que ela achou.

Antes de mexer numa área: `grep -n "palavra" docs/agentes/*.md` e leia os tópicos que aparecerem.
Lição nova vai para o arquivo da área; regra que vale para toda mudança, para este arquivo.

Documentação de produto e decisões: `docs/` (`docs/arquitetura-multi-exe.md`, `docs/telas-blazor.md`,
`docs/chat-da-equipe.md`, `docs/crm-em-producao-out2026.md`, `docs/desempenho-leituras-2026-10-15.md`).
O WhatsApp nativo na Recepção (decisão da direção, out/2026): a proposta e o mockup interativo em
`docs/whatsapp-nativo-na-recepcao.md` + `docs/mockups/whatsapp-nativo/`, e o roteiro de
implementação PR a PR em `docs/whatsapp-nativo-implementacao.md` (a metade do CRM:
`semdor-crm/docs/api-para-os-apps.md`). Para pôr no ar: `docs/whatsapp-nativo-roteiro-da-manha.md`
(publicar o CRM na VPS 2, depois os apps Recepção e Gerente, e conferir). Desde a fase 3 (out/2026)
não há mais moldura nem escolha em Configurações: com o Endereço do CRM salvo, a aba WhatsApp é a
tela nativa; voltar atrás é o `rollback` do CRM e o release anterior dos apps.
