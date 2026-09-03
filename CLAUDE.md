# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## O que é o projeto

Sistema de **faturamento** (não recebíveis — não há campos de dinheiro) para clínica médica de acupuntura,
em .NET 8, desktop WPF. O coração do produto é impedir que o **2º código/guia** (obtido +24h depois do
atendimento) seja esquecido, via dashboard de pendências com semáforo. Também cobre o ciclo TISS completo:
lote → envio → retorno → glosa → recurso, com XML TISS 4.01 e guia em PDF no leiaute ANS.

Todo o código, comentários, commits e UI são em **português (pt-BR)** — mantenha esse padrão.

## Comandos

```bash
# Testes (multiplataforma — única parte que roda fora do Windows)
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj

# Um teste específico
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj --filter "FullyQualifiedName~RegrasFaturamentoTests"

# Rodar o app (apenas Windows — WPF)
dotnet run --project src/Clinica.Desktop

# Verificação estática da suíte multi-exe (roda em qualquer sistema) — RODE ANTES DE TODO PUSH
python3 tools/verificar-suite.py

# Compilação do C# das telas WPF em Linux — RODE ANTES DE TODO PUSH que mexa em ViewModel/janela
python3 tools/compilar-sombra.py

# Migrations (usa a env var CLINICA_DB como connection string)
CLINICA_DB="Host=...;Database=...;Username=...;Password=...;SSL Mode=Require" \
  dotnet ef migrations add NomeDaMigration -p src/Clinica.Infrastructure -s src/Clinica.Infrastructure
```

⚠️ **Confira o carimbo de hora da migration recém-gerada.** Várias migrations deste repositório
foram escritas à mão com horas FUTURAS (`20260731230000`, `20260801000000`), porque não havia
`dotnet ef` no ambiente. O `dotnet ef` carimba com a hora REAL do relógio, então uma migration
gerada hoje pode nascer ordenando **antes** delas — e o EF aplica na ordem do ID, o que faria a
migration nova tentar alterar uma tabela que ainda não existe. Se o ID novo não for o maior de
todos, renomeie os dois arquivos e o `[Migration("...")]` do Designer. `ls Migrations/ | sed
's/_.*//' | sort -u` mostra a ordem real.

⚠️ `Clinica.Desktop` e toda a suíte multi-exe **só compilam no Windows** (`net8.0-windows`) — o SDK
de Linux não traz o `Microsoft.NET.Sdk.WindowsDesktop`. **Isso não é desculpa para empurrar sem
compilar.** Neste ambiente há três redes, e as três rodam antes de todo push:

| ferramenta | cobre | não cobre |
|---|---|---|
| `dotnet build` + `dotnet test` | Domain, Application, Infrastructure e os 988 testes | nada das telas |
| `tools/compilar-sombra.py` | **o C# dos 10 projetos WPF, faturamento incluído** (nome, tipo, aridade, atributo) | XAML |
| `tools/verificar-suite.py` | XAML, pack URIs, chaves do design system, projetos na solução, **migration destrutiva** | semântica de C# |

Se o SDK não estiver instalado: `apt-get update && apt-get install -y dotnet-sdk-8.0` (o instalador
da Microsoft está bloqueado pelo proxy; o repositório do Ubuntu não). O CI
(`.github/workflows/build-exe.yml`, runner Windows) segue sendo o build oficial dos cinco apps, e
desde a parcela 90 ele dispara **só no PR para a `main`** — commit em branch de trabalho não gera
build sozinho, e o push da `main` depois do merge também não. ⚠️ **Não é economia às cegas**: num
evento `pull_request` o GitHub faz checkout de `refs/pull/N/merge`, então o que o PR compila é
EXATAMENTE a árvore que a `main` vai ter; e o runner Windows é faturado **2×**, de modo que rodar
os dois gatilhos pagava ~13 minutos de cota duas vezes pelo mesmo código. Pela mesma razão o
`verificar.yml` perdeu o gatilho de PR: o `build-exe` é um **superconjunto** dele (roda o espelho
de tokens, o `verificar-suite`, os testes E o compilador de marcação), e com os dois ligados cada
commit era conferido **três vezes**. O que sobrou: `verificar` em todo push (inclusive na `main`,
que é a rede caso a base ande entre o PR ficar verde e o merge acontecer) e `build-exe` no PR,
com `concurrency` nos dois para o push seguinte cancelar o anterior.

**Testar sem publicar**: `build-exe.yml` tem `workflow_dispatch` e roda em qualquer branch,
gerando os cinco `.exe` PORTÁTEIS — sem `vpk pack`, então `mgr.IsInstalled` é falso e eles não se
auto-atualizam nem mexem no canal do app instalado. A armadilha não é o exe, é o BANCO: aponte
`ConnectionStrings__Clinica` para uma branch do Neon (a env var vence a config salva e **não grava**
em `%APPDATA%`) e **nunca** use a tela de Setup no build de teste, que grava. Roteiro completo em
`docs/testar-sem-publicar.md`.

Release: tag `vX.Y.Z` (ou Actions → "Release") dispara `.github/workflows/release.yml`, que empacota
os cinco apps com **Velopack** (um canal por app; o faturamento fica no canal padrão `win` e **nunca
muda**) e publica na mesma release; os apps instalados se auto-atualizam.

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

⚠️ **A única coisa que se roda DEPOIS é a seção 6** — as três redes e os testes: são
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
8. **a busca do prontuário** e o `CatalogoRegistroClinico`.

### 2. Método de repositório novo

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

### 4. ViewModel e tela

- **Carga async** disparada por tecla/clique/timer/troca-de-paciente: **contador de geração**.
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

### 5. Perguntas que só o autor pode responder

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
python3 tools/compilar-sombra.py
python3 tools/verificar-suite.py
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj
```

⚠️ **Rode a suíte contra a árvore que VAI SER COMMITADA.** Em 23/08 ela ficou verde porque um
arquivo estava no COMMIT e tinha sido apagado do DISCO — e era justamente o que reprovava.

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
  a abertura padrão (`SuiteApp`) e o login (`LoginWindow` + `SessaoUsuario`); cada módulo é uma
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

### Regras de negócio que não são óbvias pelo código

- **Faturamento ≠ recebíveis**: "baixa" = a secretária efetivou a guia no sistema do convênio; nunca
  adicione campos de dinheiro/pagamento.
- **A AMARRA ENTRE O HORÁRIO E A SESSÃO, e a pergunta que ela produz** (parcela 93). A
  clínica não trabalha o check-in pela agenda: a recepcionista vai direto ao Novo
  atendimento. A parcela 91 fez o lançamento reconhecer o **horário do dia** e nascer
  pendurado nele (`AgendaService.LancarNoHorarioAsync`) — mas só quando a **data bate**, e a
  agenda importada do Smart Clinic está nas datas do sistema antigo. O que não bate vira
  encaixe, e o horário fica "Aguardando" para sempre: infla a ocupação, entra no "Meu dia"
  do médico e **captura a evolução importada** (distribuída pela ordem da hora marcada),
  deixando a sessão real em "Sessões sem evolução".
  `ConciliacaoAgendaService` é a LEITURA que transforma isso em fila de trabalho, e ela não
  decide nada: ausência de clique não é prova de falta.
  ⚠️ **A pergunta tem TRÊS respostas, e a do meio é a maior do backlog.** Faltou
  (`MarcarFaltaAsync`) · aconteceu e ninguém lançou (`LancarNoHorarioAsync`, que já data o
  atendimento pela data **do horário**, não pela de hoje) · **aconteceu e já foi lançada por
  fora** — e aqui lançar criaria um SEGUNDO jogo de guias para a mesma sessão. Por isso a
  linha carrega `HorarioParado.TemSessaoNoDia` e o botão de lançar fica **apagado**, com a
  frase ao lado (botão cinza sem explicação vira "o sistema travou", parcela 41).
  **Encerrar esse horário pede um `StatusAgendamento` NOVO** e é por isso que ficou de fora:
  `Cancelado` é contado por `IndicadoresService` e `RelacionamentoService` (uma sessão que
  ACONTECEU inflaria o indicador de cancelamento) e `Faltou` culparia o paciente. O enum é
  gravado como TEXTO, então acrescentar é seguro para as linhas salvas — o custo são os
  **107 usos em 23 arquivos**, e principalmente as comparações NEGATIVAS
  (`is not (Cancelado or Faltou)`), onde um valor novo cai do lado "ativo" sem ninguém
  perceber. É parcela própria.
  ⚠️ **`WithMany()` no mapeamento de `Agendamento.Atendimento` NÃO é descuido, e trocar por
  `WithOne()` NÃO é de graça**: o EF exige índice ÚNICO para o dependente 1‑1, e migration
  roda na ABERTURA do app — índice único que falha na criação é o faturamento não abrindo.
  Conte as duplicatas pela conciliação antes.
- **ESTORNAR UM ATENDIMENTO** (parcela 94). O `RemarcarAsync` recusava horário realizado
  dizendo "Estorne o atendimento antes" — e **não havia estorno nenhum**: a instrução
  mandava fazer o que não existe, e a saída que sobrava era o `Cancelar`, sem trava.
  O motivo de não existir é que o lançamento tem **cinco efeitos em três serviços**
  (atendimento+códigos · NCs reabertas · consulta renovada · carimbo do horário · pacote,
  insumo e caixa) e só um é a guia. `EstornoAtendimentoService` **pergunta item a item** —
  as guias saem sempre; caixa, pacote e insumo só se marcados. Recusa quando o fato já saiu
  da clínica (guia baixada, em lote TISS ou em NC), reusando a trava do
  `AjustarAoRemarcarAsync`. Bit `LancarAtendimento`: com aquela recusa, o que se desfaz é
  guia que ainda **não** saiu — corrigir o próprio erro na hora.
  ⚠️ **NUNCA apaga.** `OnDelete(SetNull)` transformaria um `Remove` em horário ÓRFÃO — o
  estado de 12/08/2026. O atendimento fica marcado (`EstornadoEm`) e os códigos vão para
  `NaoAplicavel` com a `MarcaEstorno`.
  ⚠️ **A ARMADILHA, e por que `EstornadoEm` existe.** O estorno SOLTA o horário (para a
  sessão poder ser relançada limpa — sem soltar, o `ConfirmarNucleoAsync` reaproveitaria o
  atendimento anulado e a sessão nova nasceria sem guia faturável, em silêncio). Só que
  `MarcarAtendimentosSemCarimboComoRealizadosAsync` carimba como realizado todo atendimento
  sem carimbo **que não tenha horário em outro estado apontando para ele** — e sem horário
  nenhum, o estornado seria RESSUSCITADO na próxima ativação da chave "guia no
  agendamento", voltando a contar em BI, retenção e origem. A coluna entra nos DOIS filtros
  do backfill; `EstornoDeAtendimentoTests.O_backfill_NAO_ressuscita_atendimento_estornado`
  é o que amarra isso.
  ⚠️ **NÃO desfaz a consulta renovada** (`StatusConsulta` não tem "cancelada"; desfazer
  exigiria ressuscitar a anterior, e receita emitida sob a nova ficaria inválida) **nem as
  NCs reabertas** (o paciente apareceu de verdade). As duas são decisão escrita, não
  esquecimento.
  ⚠️ **`Sum(x => (decimal?)x.Valor)` sobre sequência VAZIA devolve 0, não nulo** — foi assim
  que a prévia ofereceu "desfazer entrada de R$ 0,00" num atendimento sem caixa. Conte
  primeiro (`lista.Count > 0 ? lista.Sum(...) : null`). Um teste pegou; a lição fica.
- **A GUIA QUE VAI NASCER, DESENHADA** (set/2026, redesenho aprovado em mockup). A coluna
  direita do Novo atendimento resumia a prévia em TEXTO — "2 guias · a 2ª libera 09/08" —,
  e a guia, que é o que a clínica entrega, só virava objeto depois de gravada e noutro app.
  O número estava certo e mesmo assim passava o erro que mais custa: guia emitida com o
  campo errado, que volta glosada semanas depois. Agora a coluna desenha o **documento**,
  com os campos que vão nele — convênio, carteirinha, validade, beneficiário, executante —
  e os códigos embaixo; o campo que FALTA sai com o fundo do erro, porque no documento o
  buraco tem de se ver como buraco. **Nenhum campo é inventado**: o `EntradaModalidade` do
  catálogo tem `Codigo` interno, `Nome` e `Base` — **não há código TISS por modalidade**, e
  o mockup que os mostrava estava errado. O documento é PRÉVIA: não há número (ele nasce na
  baixa) e a tarja diz isso.
  As modalidades saíram dos cartões de 200×130 e viraram **linhas**: para comparar "quantas
  guias" entre cinco opções em grade o olho salta na diagonal, e em linha os números caem
  numa COLUNA — que é como se compara número. Cada linha ganhou o traço da FAMÍLIA à
  esquerda, com os mesmos tokens `Brush.Modalidade.*` do `CartaoDeHorario`: quem vê a agenda
  o dia inteiro já lê teal como BSV. Os gatilhos de `Escolhida` vêm DEPOIS dos de família,
  porque gatilho posterior vence e a linha marcada tem de se distinguir das outras quatro.
  A faixa do paciente passou a carregar **convênio e carteirinha em colunas rotuladas**, no
  lugar de dois badges do mesmo tamanho da categoria — são os dois campos que decidem se a
  guia nasce e se o convênio a aceita, e a carteirinha só aparecia quando VENCIA.
  ⚠️ **O botão continua habilitado sem convênio, e isso é a parcela 92, não um esquecimento**:
  o clique é que abre a janela de convênios (`VinculoDeConvenio`). Desabilitá-lo devolveria a
  recusa muda que a parcela 92 aposentou.
  ⚠️ `CornerRadius="8,8,0,0"` literal na tarja do documento: o WPF **não deriva raio parcial**
  de um `CornerRadius` do dicionário. Os 8 são `Raio.Medio` — se o token mudar, a tarja fica
  com uma lasca de fundo na quina.
  ⚠️ O `verificar-suite` **não resolve estilo local**: um `TextBlock` cujo `TextTrimming` vem
  de um `Style` declarado no `UserControl.Resources` é acusado do mesmo jeito. Repetir o
  atributo na tag é o preço, e ele deixa a intenção à vista onde ela é lida.
- **SEM CONVÊNIO ESCOLHIDO NÃO NASCE ATENDIMENTO** (parcela 92). A importação do Smart Clinic
  trouxe **2.021 das 2.238 fichas sem convênio**, no código `ConvenioCadastro.CodigoADefinir` —
  que não gera guia. A defesa era o alerta VERMELHO da elegibilidade, e ela não impede nada por
  contrato ("quem decide é a clínica"): a sessão era lançada por cima do vermelho, os códigos
  nasciam `NaoAplicavel`, a tela dizia "Atendimento registrado" e o faturamento não via guia
  nenhuma. Nada falhava — é o defeito do segundo tipo, o que **deixa de fazer o que promete**.
  A recusa (`ConvenioNaoDefinidoException`) mora em **`AtendimentoService.MontarAsync`**, a
  montagem por onde TODAS as portas passam: avulso do balcão, lançamento sobre o horário do dia,
  `ConfirmarPresencaAsync` da Fila e `AgendarAsync` com "guia no agendamento" ligado. Escrevê-la
  numa tela cobriria uma e deixaria três passando.
  **Recusar sem oferecer a saída seria trocar guia perdida por balcão travado**, então a metade
  visível é `VinculoDeConvenio` + `EscolhaDeConvenioWindow` (Shell): as telas perguntam ANTES,
  na hora, e a ficha é atualizada no mesmo clique — é o `ColetaDeTermo` de novo, e pela mesma
  razão (várias portas, uma montagem só). Quem pergunta: Novo atendimento (o aviso já traz o
  botão) e o Concluir da Fila. O faturamento (`Clinica.Desktop`) só mostra a mensagem — ele não
  referencia o shell, é o débito permanente da Fase 4.
  ⚠️ **Três assimetrias deliberadas.** (a) **PARTICULAR não é "sem convênio"**: é escolha
  registrada (`ConvenioCadastro.GeraGuia` desmarcado, parcela 60) e continua lançando.
  (b) **Marcar por telefone com a chave desligada continua passando**: aí a marcação não cria
  atendimento nenhum, e exigir o convênio ali travaria quem marca sem ter a resposta em mãos —
  a condição vale exatamente onde o atendimento nasce, e a UI espelha isso
  (`!MarcarParaDepois || GuiaNaMarcacao`). (c) **A carteirinha da janela é opcional e, em
  branco, PRESERVA a da ficha** — a janela responde uma pergunta, não regrava a ficha.
- **O número da guia tem FORMA, e ela é do convênio** (`RegraNumeroGuia`, `ConvenioCadastro.
  FormatoNumeroGuia`, parcela 45): a Unimed numera só com dígitos; Petrobras, Amil e Sul América
  misturam letra e número. O que se pega com isso é o erro que o olho não vê — o **"O" digitado no
  lugar do zero** —, que passava batido, dava a guia por baixada e só aparecia no retorno da
  operadora, semanas depois, com boa parte do prazo de recurso já corrida. A regra mora no
  **DOMÍNIO** e é aplicada em `FaturamentoService.DarBaixaAsync`, não na tela, porque a baixa tem
  **quatro portas** (tela de baixa, baixa em lote, rodada de pendências e fila do Gerente): validar
  na tela cobre uma e deixa três passando, que é o defeito recorrente do projeto vestido de
  validação. A tela usa a MESMA regra para avisar ANTES do clique — a dica ao lado do campo é a
  metade que explica; o serviço é a que impede.
  O formato é **dado, não código**: fica no `ConvenioCadastro`, editável em Configurações, porque
  "Sul América" não existe no enum `Convenio` (entrou como personalizada) e amarrar a forma à
  família obrigaria a publicar versão nova a cada operadora. A migration **semeia os embutidos**
  com o que a cliente informou — sem isso a regra nasceria desligada e só passaria a valer no dia
  em que alguém abrisse Configurações, que pode ser nunca. O que ela **não** faz é conferir
  tamanho, prefixo ou dígito verificador: cada operadora tem o seu, eles mudam sem aviso, e regra
  apertada demais recusa guia legítima — o que é **pior** do que aceitar uma errada, porque trava
  o faturamento do dia e o faturista não tem como contornar (ele não inventa o número). Três
  degraus na resolução, e o terceiro é o que separa "não configurado" de "não sei quem é":
  catálogo → padrão da família → **sem validação** para código desconhecido.
  **Número em branco passa em qualquer formato**: quem valida FORMA não decide obrigatoriedade.
- **Permissão granular no faturamento, e o login que a torna útil** (parcela 45): a cliente pediu
  "permissões granulares para a gerente auditar o que está sendo feito e liberar ou não". A metade
  das permissões não resolvia sozinha — **toda ação do faturamento gravava `Environment.UserName`
  na auditoria**, isto é, o login do WINDOWS: as duas pessoas que dividem o balcão assinavam com o
  mesmo nome, e a trilha da parcela 21, que existe para responder "quem fez isso?", respondia o
  nome da MÁQUINA. Por isso o app ganhou tela de login (`Clinica.Desktop/Acesso/LoginWindow`) e
  `SessaoUsuario` subiu para `Clinica.Domain`.
  Os bits novos cortam pelo **ATO, não pela tela** (`BaixarGuia`, `EstornarBaixa`,
  `RegistrarGlosa`, `GerenciarLotesTiss`, `LancarAtendimento`, `MarcarNaoConformidade`,
  `ConfigurarFaturamento`) — quebrar por tela daria uma lista que muda a cada leiaute novo.
  `VerFaturamento` virou **só a leitura**. Estornar é separado de baixar porque desfazer apaga o
  trabalho de outra pessoa; NC é separado de tudo porque é a única permissão que faz uma pendência
  **sumir do painel sem a guia ter sido faturada**.
  Três decisões que andam junto: (a) o padrão do perfil `Faturista` reproduz **exatamente** o que
  o app deixava fazer antes do login — a granularidade serve para a direção TIRAR, não para a
  atualização tirar sozinha; (b) as telas do Gerente que baixam/glosam/reabrem passaram a pedir os
  bits novos no mesmo commit, senão negar "Dar baixa" a alguém no Acessos não impediria nada e a
  permissão seria só uma caixinha na tela; (c) a rodada BLOQUEANTE **não abre** para quem não pode
  decidir nada — travar o sistema com uma tarefa que a pessoa não pode cumprir é o pior desfecho
  possível de uma permissão bem-intencionada.
  A entrada no app exige `VerFaturamento` **e diz isso na porta**: deixar entrar e mostrar sidebar
  vazia faz a pessoa ligar para o suporte em vez de falar com a direção. E "Trocar usuário"
  **reabre o app** em vez de repontar a sessão: as ViewModels leem a permissão quando são
  construídas, e metade delas já está viva — permissão que parece aplicada e não está é pior do
  que permissão nenhuma.
- **O atendimento saiu do faturamento e foi para o balcão** (parcela 46): "Novo
  atendimento" e "Consultas" viraram itens do `Clinica.Modulo.Recepcao`, e **saíram de vez**
  do `Clinica.Desktop`. Nenhum dos dois era feature nova — os dois existiam, no posto
  errado. Lançar atendimento AVULSO (quem chegou sem horário) e renovar a consulta do
  convênio são atos que se fazem **com o paciente na frente**, e moravam na máquina de quem
  não recebe ninguém: a recepção via o selo "consulta a renovar" no cartão da agenda desde a
  parcela 44 e **não tinha por onde renovar**. É a nona ocorrência do defeito recorrente do
  projeto, na variante "a porta está no módulo de quem não usa".
  **O circuito com o faturamento não mudou, e é isso que os testes fixam**: os dois caminhos
  do atendimento — este e a Fila → Finalizar (`FechamentoSessaoService` →
  `AgendaService.ConfirmarPresencaAsync`) — desembocam em `AtendimentoService.LancarAsync`,
  que é **ponto único** e grava `Atendimento` + `CodigoFaturamento` pelas regras do convênio.
  Não há atendimento que nasça sem guia. `AtendimentoNaRecepcaoTests` prova pelas consultas
  do PRÓPRIO faturamento (`PendenciaService`, `ConsultarCodigosAsync`), e não por uma
  consulta escrita para o teste: elo partido aqui não vira erro, vira **lista vazia**, que é
  indistinguível de um dia fraco.
  O que **não** foi junto foi a BAIXA. Ela é o ato do faturamento, tem as quatro portas de
  lá, e o perfil que usa a tela nova não tem o bit — botão que nasce apagado para quem usa a
  tela é o defeito da parcela 41. A lista de guias geradas ficou como **confirmação**: é onde
  o balcão vê, na hora, que a guia nasceu e quando ela libera. E `PerfilAcesso.Recepcao`
  ganhou `LancarAtendimento` no mesmo commit — sem o bit, as duas telas nasceriam invisíveis
  para quem passou a ser dono delas.
  O faturamento continua criando atendimento pela **agenda dele** (confirmar presença); o que
  saiu foi a tela de lançamento avulso.
- **Consultar guias filtra por modalidade e especialidade** (parcela 45): a pergunta da direção é
  "o que vem sendo feito", e a consulta só respondia por paciente, número, data, status e
  convênio. O filtro é pelo **enum**, não pelo código do catálogo, pela mesma razão do convênio ao
  lado: é por FAMÍLIA, e a variante cadastrada responde junto da embutida que ela deriva — filtrar
  por código obrigaria a escolher entre "Acupuntura" e "Acupuntura (domiciliar)" para responder
  quantas acupunturas foram feitas. A especialidade casa com a do **CÓDIGO** e cai para a do
  atendimento quando ele não a tem; sem esse caminho de baixo, guia de um atendimento com
  especialidade declarada ficaria de fora do filtro da própria especialidade. E o resumo **diz que
  está filtrado**: "12 guias" e "12 guias de acupuntura em psiquiatria" respondem perguntas
  diferentes, e quem volta à tela depois do café não lembra o que deixou marcado no combo.
- Cada convênio tem um fluxograma próprio (README lista os cinco modelados). O 2º código com data
  prevista +24h e a inversão de datas do BSV são requisitos do convênio, não bugs.
- Guia exportada num lote TISS não pode entrar em outro lote; glosa ganha data-limite de recurso
  (prazo configurável, padrão 30 dias) vigiada no dashboard.
- **Rodar as pendências** (`RodadaPendenciasService`): o prazo de decisão é **por guia** — cada
  pendência vence N dias (configurável, padrão 10) depois de **virar pendente**, ou seja da
  `DataPrevistaFaturamento` (`CodigoFaturamento.PrazoDecisaoVencido`). Não conta do atendimento: o 2º
  código só existe +24h depois, e contar do atendimento lhe daria um prazo real menor que o do 1º,
  cobrando decisão sobre uma guia que ninguém tinha como tirar. Passado o prazo sem baixa, o painel
  alarda (banner) e a abertura do app abre uma janela BLOQUEANTE com as guias vencidas: cada uma exige
  decisão — baixa ou **não conformidade** (`StatusCodigo.NaoConformidade` + justificativa) — e o
  sistema fica travado até a resolução. Há uma **carência de 1ª execução**
  (`ParametrosService.ChaveInicioRodadaPrazo`, ancorada por `GarantirInicioAsync`): guias que já
  estavam pendentes antes da ativação da versão só passam a contar o prazo a partir da ativação, para
  o backlog acumulado não bloquear tudo de uma vez. O valor da chave no banco continua
  `"InicioRodadaPorAtendimento"` de propósito — renomeá-lo perderia a âncora já gravada.
  O usuário também pode marcar NC proativamente (sem esperar o prazo) pelo botão **NC** na
  linha da pendência no painel (`NaoConformidadeWindow`). A não conformidade sai das
  pendências ativas (`EstaPendente`/`CodigosEmAbertoAsync` a ignoram) e vai para a aba própria **NC**
  (`NaoConformidadesViewModel` / `Secao.NaoConformidades`), que lista todas via
  `RodadaPendenciasService.NaoConformidadesAsync`, permite ler a justificativa e reabrir; também entra
  no relatório. Ela reativa (volta a pendência) de duas formas: manualmente (botão Reabrir na aba NC)
  ou automaticamente quando o **paciente volta** — `AtendimentoService.LancarAsync` reabre as NCs do
  paciente e avisa a secretária para cobrar a guia na hora. Toggles em Configurações estendem a rodada
  a consultas/carteirinhas.
- **Mapa corporal e protocolo** (`MapaCorporalService`, parcela 3): o mapa é **1:1 com a
  evolução** (é a mesma sessão vista de outro jeito) e some com ela. Aplicar um protocolo
  é **COPIAR** os pontos para a sessão, nunca apontar para ele — referência viva faria
  corrigir um ponto hoje reescrever o protocolo da clínica e a sessão da semana passada.
  As coordenadas são **normalizadas (0 a 1)** sobre a figura, nunca pixels: quem converte
  o clique é a tela (`PontoMapaItem.LarguraFigura`/`AlturaFigura`, os mesmos números do
  XAML). "Repetir a sessão anterior" e "aplicar protocolo" **não gravam** — trazem os
  pontos para a tela, e só o Salvar da sessão os efetiva.
- **Documento clínico é fato** (`DocumentoClinicoService`, parcela 3): os sete papéis da
  página 21 que saem da Recepção viram um `DocumentoClinico` numerado por ano
  (`2026/0001`) com código de conferência. Não se apaga nem se reescreve: **cancela-se
  com motivo** e emite-se outro (como a revogação de consentimento). O conteúdo é gravado
  na EMISSÃO e não remontado na reimpressão — a segunda via tem de sair idêntica à que o
  paciente levou. Quatro são escritos (receita, atestado, comparecimento, pedido de
  exame) e três montados do prontuário (relatório de evolução, termo de consentimento,
  anamnese). O **CID só sai impresso com autorização expressa do paciente**
  (`CidImpresso`); receita, atestado e pedido de exame **exigem** o profissional que
  assina — única exceção à regra de "avisa, mas não impede". Não há assinatura ICP-Brasil:
  o PDF traz carimbo, linha de assinatura e código de conferência, e chamar isso de
  assinatura digital seria mentir sobre o que a via garante.
- **Pacote é venda da clínica, cota é do convênio** (`PacoteService`, parcela 4): não
  confundir `PacotePaciente` com `AutorizacaoSessoes` — as duas contam sessões e não têm
  nada a ver uma com a outra (a cota evita glosa; o pacote evita atender de graça). A
  venda **copia** o catálogo (mudar o preço de tabela não reescreve o que já foi
  comprado); a **situação é calculada, não guardada** (`Situacao(hoje)`), porque um
  pacote gravado como "Ativo" viraria mentira à meia-noite do vencimento; consumo é fato
  datado e se desfaz **cancelando com motivo**. A baixa automática
  (`ConsumirPorAtendimentoAsync`) debita o pacote **que vence primeiro**, uma vez por
  atendimento, e é chamada pelo fluxo da **Recepção** — nunca pelo `AtendimentoService`,
  que é compartilhado com o faturamento congelado.
- **Fechamento da sessão** (`FechamentoSessaoService`, parcela 6): concluir o atendimento na
  Recepção são **quatro fatos do mesmo ato** — a guia nasce, o pacote debita, o insumo sai do
  estoque e o dinheiro entra no caixa. Por cinco parcelas só o primeiro acontecia: `PacoteService.
  ConsumirPorAtendimentoAsync` e `EstoqueService.BaixarAsync` estavam prontos, testados e **sem um
  único chamador em produção**, e a Recepção não conhecia o `FinanceiroService`. Este serviço é a
  ponte, e mora fora do `AtendimentoService` pelo mesmo motivo de sempre: aquele é compartilhado
  com o faturamento congelado. A tela (`FechamentoSessaoWindow`, aberta pelo Finalizar da Fila) é
  **proposta confirmada, nunca automática** — o sistema faz o trabalho (acha o pacote que vence
  primeiro, lembra o que a última sessão gastou, sugere o valor da última entrada do paciente
  **com a procedência**), e o clique continua sendo de quem está no balcão; valor errado gravado
  sozinho vira caixa que não bate e só aparece no fim do mês. Só a conclusão do atendimento
  derruba a operação: pacote, insumo e caixa que falham viram **aviso** (`ResultadoFechamento.
  Avisos`) e a janela fica aberta dizendo o quê — desfazer a guia porque o estoque de uma agulha
  não bateu deixaria a clínica sem a guia de uma sessão que o paciente recebeu. Cobrança **não é
  sugerida** quando há pacote (sessão comprada já foi paga) nem sem histórico de recebimento do
  paciente — marcar por padrão criaria receita fantasma para quem é do convênio.
- **Taxa de cartão e imposto** (`TaxaService`, parcela 9): o **valor do lançamento continua
  sendo o BRUTO** — o que o paciente pagou. Taxa e imposto são deduções ao lado, e o líquido
  é CALCULADO (`LancamentoFinanceiro.ValorLiquido`); gravar o líquido daria duas verdades
  sobre o mesmo dinheiro. `TaxaCartao` tem **vigência**, como a regra de repasse: a
  adquirente renegocia, e o que vale no recebimento de março é o percentual de março — por
  isso o valor da taxa é **copiado** na venda, nunca referenciado. A regra mais **específica**
  ganha (bandeira vence o adquirente genérico), senão a clínica cadastraria a exceção e
  continuaria vendo o número da regra geral. **Sem taxa cadastrada não se inventa desconto**:
  o lançamento fica só com o bruto, que é a verdade disponível. A alíquota de imposto nasce
  **zero** — cada clínica tem seu regime, e chutar erraria a base inteira — e é lida com
  ponto decimal invariante ("2,5" lido como 25 multiplicaria o imposto por dez).
- **Contas a pagar e a receber** (`ContasService`, parcela 12): `LancamentoFinanceiro` tem
  **três datas e elas não são a mesma coisa** — `Data` é a competência, `DataPagamento` é
  quando o dinheiro se moveu e `DataVencimento` é quando ele PRECISA se mover. Sem a
  terceira o módulo não respondia "o que vence esta semana". `LancamentoRecorrente` é um
  **molde, não um lançamento**: ele não entra em total nenhum, quem entra é a conta prevista
  que ele gera e que dali em diante tem vida própria (a luz nunca vem igual; se a
  recorrência fosse "o lançamento que se repete", corrigir março reescreveria janeiro, já
  pago e conciliado). A série sai sempre do **primeiro vencimento mais N períodos**, nunca
  da ocorrência anterior mais um — encadear faria o aluguel do dia 31 virar aluguel do dia
  28 para sempre por causa de fevereiro. A geração é **idempotente** por
  `OrigemRecorrencia` (`REC:{id}:{aaaa-MM-dd}`, índice único), roda por **clique** e nunca
  automaticamente na abertura, e **nada nasce pago**: tudo vem `Previsto`, porque o sistema
  sabe que a conta vence, não que ela foi quitada.
- **Fluxo de caixa** (`FluxoCaixaService`, parcela 13): **realizado e previsto nunca viram
  um número só** — somados, o mês que fechou e o que ainda vai vencer teriam a mesma cara.
  Cada lançamento entra no mês pela data que corresponde ao seu estado (`LancamentoDatado.
  DataDoFluxo`): realizado pelo pagamento, previsto pelo vencimento, competência como
  último recurso. O acumulado é **variação no período, não saldo em conta** — a clínica
  nunca cadastrou saldo inicial, e chamá-lo de saldo daria um número que não bate com o
  extrato. A fração da categoria é do total **do mesmo tipo**, e lançamento sem categoria
  aparece como "Sem categoria" em vez de sumir.
- **Fechamento de caixa** (`FechamentoCaixaService`, parcela 14): a conferência da gaveta
  conta **só espécie** — cartão e PIX caem na conta dias depois, e incluí-los faria a
  conferência nunca bater, o que treina a clínica a clicar "OK" sem olhar. `ValorSistema` e
  `SaidasEspecie` são **copiados no fechamento**, não recalculados: lançamento digitado
  amanhã com a data de ontem reescreveria a conferência de ontem e levaria junto a
  justificativa. **Divergência exige justificativa escrita** — é a única regra do serviço
  que impede em vez de avisar, e vale também para sobra (dinheiro a mais costuma ser venda
  não lançada). Reabrir **não apaga**: o fechamento anterior fica marcado com o motivo e o
  dia volta a pendente; `FechamentoCaixa.Data` não é único e o que vale é o de maior `Id`.
- **Regime tributário** (`TributoService`, parcela 15): substitui a alíquota única da
  parcela 9, que respondia "quanto saiu" e não "**de quê**" — a clínica no Presumido tem
  cinco guias — e não tinha vigência, então o reajuste do ISS mudava o mês já declarado.
  `Tributo.BasePercentual` existe porque nem todo tributo incide sobre a receita inteira:
  no Presumido o IRPJ de 15% incide sobre 32% e a **efetiva é 4,8%**; sem o campo a clínica
  digitaria 15% e triplicaria o imposto. Cada tributo é **arredondado ao centavo
  separadamente** (arredondar só no fim não bate com a soma do detalhe, e é o detalhe que
  vai ao contador). `Descricao` e `DetalheImposto` são formatados em **pt-BR fixo, não na
  cultura da máquina**: eles são GRAVADOS, e dois postos escreveriam "0,65%" e "0.65%" na
  mesma coluna — não contradiz a regra invariante do `ParametrosService`, que **lê** número
  que precisa voltar a ser número. A chave `AliquotaImpostoRetido` segue como **fallback**
  enquanto não houver tributo cadastrado: o valor já está na base do cliente, e zerá-lo
  faria um mês inteiro sair sem imposto sem ninguém perceber.
- **Recebíveis de cartão** (`RecebiveisService`, parcela 16): `PrevisaoRecebimento` era
  gravada desde a parcela 9 e **nenhuma tela a lia** — terceira ocorrência do mesmo defeito
  no projeto. A conciliação é por **depósito, não por venda** (a adquirente deposita o lote
  do dia; conferir venda a venda contra um extrato de um valor só nunca fecharia).
  `RecebimentoConfirmadoEm` fica **separada** da previsão: quando a adquirente atrasa as
  duas divergem, e sobrescrever a previsão apagaria a prova do atraso. A data do crédito é
  **informada**, nunca `Today` — conferir na segunda um depósito que caiu na sexta viraria
  três dias de atraso que não houve. O líquido do depósito é bruto menos **taxa, sem
  imposto**: o imposto é recolhido depois, por guia, e descontá-lo aqui faria o número não
  bater com o extrato, que é o único uso da tela.
- **Custo de transação** (`CustoTransacaoService`, parcela 17): a visão da direção. A
  leitura que só existe aqui é a **taxa efetiva contra a de tabela** — a tabela diz 3,1%, e
  se a clínica parcela mais do que imagina o efetivo é 3,9%; é a diferença que dá argumento
  na renegociação. A tabela vem como **faixa** (menor e maior vigente), porque uma
  adquirente tem várias taxas e um número só teria de escolher uma modalidade. A fração da
  barra é do custo de **maquininha**, não do faturamento: senão "(sem maquininha)" levaria
  a maior fatia sendo que custa zero.
- **Retenção na fonte por convênio** (parcela 18): a operadora que paga serviço de PJ
  **retém** IRRF, CSLL/PIS/COFINS e às vezes o ISS antes de depositar — a guia vale
  R$ 1.000 e caem R$ 943,50. `Tributo.ConvenioCodigo` preenchido significa retido na fonte
  por aquele convênio (reaproveita a entidade porque é o mesmo fato; só muda quem recolhe).
  A retenção **substitui** os tributos gerais naquele recebimento, nunca se soma: os dois
  são o mesmo imposto, e somá-los conta duas vezes — o erro clássico da planilha de
  convênio. Sem retenção cadastrada o convênio cai nos tributos gerais (mudar isso faria a
  receita de convênio parar de sofrer imposto da noite para o dia), e retenção de convênio
  **não vaza** para recebimento que não é dele. O detalhe copiado diz "(retido na fonte)"
  porque retido já saiu e recolhido ainda vai sair. `GuiaSemLancamento.ConvenioCodigo`
  existe porque `Convenio` é a família de REGRA: duas operadoras podem compartilhá-la e
  reter percentuais diferentes.
- **Tabela de preço por convênio** (`PrecoConvenioService`, parcela 20): cadastrada no
  **Gerente** e lida pela conciliação do **Financeiro** — quem negocia tabela é a direção,
  quem concilia guia é o balcão; mesmo banco, sem sincronização. A mais **específica** ganha
  (especialidade vence o genérico do tipo), tem **vigência** (reajuste é linha nova; a guia
  de março segue valendo o preço de março) e o valor é **copiado** no lançamento. **Sem
  preço cadastrado não se inventa valor** — o campo fica vazio para digitar, e chutar um
  valor de mercado daria receita errada com aparência de exata. É **proposta, não
  imposição**: a operadora pode ter pago menos (glosa parcial), e a linha mostra a
  PROCEDÊNCIA do número, porque campo que se preenche sozinho sem explicar faz a pessoa
  confirmar sem conferir. A proposta usa a data da **baixa da guia**, não a de hoje.
- **Rentabilidade por convênio** (`RentabilidadeConvenioService`, parcela 19): o encontro
  dos dois módulos. **Líquido por guia** é a leitura central — o único número comparável
  entre operadoras que pagam valores e volumes diferentes, e por isso a "menos rentável" é
  apurada por guia e não pelo total (que apontaria sempre a de menor volume). O **prazo
  médio** só conta o que JÁ foi pago: incluir o previsto mediria uma promessa, e quem
  atrasa teria o melhor número. O agrupamento é pelo **código**, não pelo nome exibido —
  duas operadoras da mesma família fora do catálogo resolvem para o mesmo nome padrão e
  seriam fundidas. O período é o do **atendimento**, não o do recebimento.
- **Tabela de preço por convênio** (`PrecoConvenioService`, parcela 20): o valor da guia é
  cadastrado no **Gerente** e refletido no Financeiro (a Conciliação já abre com o valor
  preenchido e a **procedência** escrita ao lado). A regra mais **específica** ganha —
  especialidade declarada vence a genérica, depois a vigência mais recente —, senão a
  clínica cadastraria a exceção e continuaria vendo o preço geral. Sem preço cadastrado o
  campo fica **vazio**, não zero: chutar um valor viraria receita inventada.
- **Auditoria** (`AuditoriaService`, parcela 21): `EventoAuditoria` era gravado por
  praticamente tudo o que mexe em dinheiro ou permissão e **nenhuma tela o lia** — quarta
  ocorrência do defeito "dado gravado sem leitor" (o pacote que debitava, o insumo que
  baixava, a previsão de recebimento) e a mais grave, porque esta é a resposta para "quem
  fez isso?". A trilha é **somente leitura, e isso é decisão**: registro de auditoria que
  se pode editar ou apagar não é auditoria, é rascunho — não há exclusão no serviço nem na
  tela. A ação casa por **prefixo** ("Conta" acha ContaCriada e ContaReagendada); o dia
  final entra **inteiro** (`TimeOnly.MaxValue`), senão um evento das 14h ficaria fora de um
  filtro que pede "até hoje"; o resumo conta sobre o **resultado do filtro**, não sobre a
  base; e **bater no limite é avisado**, porque "300 eventos" que são 300 de 4.000 faria a
  direção concluir que o período teve pouca movimentação. Fica sob `Permissao.VerAuditoria`,
  não sob `GerenciarUsuarios`: ler a trilha e mexer em permissão são coisas diferentes.
- **Painel da direção** (`PainelDirecaoService`, parcela 22): o Gerente carrega os três
  módulos e abria no primeiro item do primeiro deles — o painel da **Recepção**. Daí
  `ItemMenuModulo.Inicial` (reordenar os módulos desmontaria a sidebar, que já está na
  ordem do dia de trabalho) e `NavegacaoSuite`, pela qual uma tela pede ao shell para abrir
  outra por chave; `ChavesSuite` guarda só as chaves que **atravessam módulo** — nenhum
  módulo passa a conhecer o outro, o que se evita é repetir `"fechamento-caixa"` à mão do
  outro lado, onde renomear a seção compila e só falha na clínica. O painel **não calcula
  nada**: cada número vem do serviço dono dele (a pendência vem do
  `RodadaPendenciasService`, que já resolve prazo e carência — recontar daria outro número
  para a mesma guia). A comparação é com o **mesmo trecho do mês anterior**: no dia 5,
  cinco dias contra trinta apontariam queda de 80% todo começo de mês, e a direção
  aprenderia a ignorar a seta. **Cada bloco falha sozinho** (`NaoVerificados`), porque um
  painel que diz "nada vencido" por causa de uma consulta quebrada é pior do que um painel
  que não abre. Cada alerta **leva** ao assunto, e o botão fica desabilitado quando o
  destino não existe para quem está usando.
- **Inadimplência** (`InadimplenciaService`, parcela 23): a conta a receber vencida existia
  desde a parcela 12, mas dissolvida na lista de Contas — uma linha por lançamento,
  misturada com o que a clínica tem a **pagar** —, e `LancarContaAsync` **não tinha
  `pacienteId`**: a dívida não tinha dono. Só conta de **paciente** entra (a receber sem
  paciente é reembolso de convênio ou venda de produto, e cobrar quem não deve custa mais
  que a sessão em aberto). A situação é **calculada, nunca gravada** — campo "inadimplente"
  no cadastro continuaria lá depois de o paciente pagar, como a situação do pacote. A ordem
  padrão é o **mais antigo**, não o maior valor: a chance de receber cai com o tempo, e o
  caso de seis meses precisa de decisão, não de mais um lembrete. **Não há pagamento
  parcial** — receber passa pelo `FinanceiroService`, que é quem grava dinheiro. Todas as
  faixas de envelhecimento aparecem, **mesmo vazias** (aging sem a faixa de 90+ se lê como
  "não há dívida velha"). A mensagem de cobrança é **lembrete, não ameaça**, e não leva dado
  clínico — o telefone pode não ser só do paciente; e **cobrança não exige consentimento de
  marketing**, é transacional como a confirmação da própria sessão. No painel da direção,
  conta a pagar e paciente devendo são alertas **separados**: somá-los daria um número sem
  significado, porque um se resolve pagando e o outro cobrando.
- **Central de documentos** (`CentralDocumentosService`, parcela 24): as nove folhas do
  mockup existiam todas e **nenhuma estava no mesmo lugar** — quatro numa janela dentro da
  ficha do paciente, três no botão certo da aba certa dessa ficha, o recibo no Caixa, o
  orçamento só de dentro de um pacote vendido e o fechamento do período só no app
  congelado. Não faltava capacidade, faltava porta; e havia duas capacidades **sem porta
  nenhuma** (`DocumentoFinanceiroService.EmitirAsync`, que aceita linhas quaisquer desde a
  parcela 4, e `FechamentoPdfService`) — a variante mais discreta do defeito "dado gravado
  sem leitor": o CI fica verde e a clínica pega o bloquinho de papel. A tela **não
  reimplementa emissão nenhuma**: abre a janela que já existe ou chama o serviço dono da
  folha. `FolhaCatalogo.Exigencia` é o que faz cada cartão dizer **o que falta** em vez de
  deixar o botão aceso e avisar depois — descobrir o requisito errando é o que faz a pessoa
  desistir da tela. O **recibo continua nascendo no Caixa** (aponta para o lançamento; de
  outro lugar sairiam dois recibos do mesmo pagamento) e o cartão navega até lá. Cancelada
  **aparece marcada, nunca sumindo**; filtro de folha desconhecida devolve **nada, não
  tudo**; e o fechamento do período **não tem segunda via** — não é gravado, é conferência
  montada na hora, então a lista devolve vazio para ele de propósito. O rótulo segue o
  **mockup**, não o enum ("Receituário", não "Receita").
- **Repasse** (`RepasseService`, parcela 4): quem atendeu vem do **agendamento**
  (`Agendamento.ProfissionalId` + `AtendimentoId`), porque `Atendimento` é do faturamento
  e não guarda profissional. O percentual incide sobre a **receita que entrou**
  (lançamentos de entrada não cancelados), não sobre o faturado. `RepasseApurado` existe
  para travar o período: repasse pago duas vezes é dinheiro que não volta; cancelar a
  apuração cancela junto a saída no caixa. Regra tem vigência — mudar o percentual hoje
  não reescreve o mês já pago.
- **Estoque** (`EstoqueService`, parcela 4): o **saldo é a soma dos movimentos**, nunca um
  campo — total guardado é como o estoque para de bater. A **validade fica no movimento
  de entrada** (o lote), não no item, senão o lote que vence primeiro some. Saída maior
  que o saldo é recusada e perda exige motivo escrito.
- **Capacidade sem porta** (parcela 25): a quinta rodada do defeito recorrente do projeto,
  e a mais discreta — não é dado gravado sem leitor, é **serviço testado que nenhuma tela
  chama**. Seis capacidades estavam assim, todas com teste verde: o custo por sessão
  (`CustoDoAtendimentoAsync`, que a parcela 6 destravou e ninguém lia), devolver uma sessão
  ao pacote (`CancelarConsumoAsync`), desfazer a confirmação de depósito
  (`DesfazerConfirmacaoAsync`), sugerir quem chamar para o horário que vagou
  (`CandidatosParaAsync`), conferir o documento pelo código impresso (`PorCodigoAsync`) e
  apagar modelo/protocolo. **Antes de dar por pronta uma feature, procure o chamador em
  produção** — e, desde a parcela 36, procure também o leitor em OUTRO MÓDULO: dado que
  só o módulo que o grava consegue ler é a mesma falha vestida de arquitetura — `dotnet test` verde e CI verde não provam que a clínica alcança a função.
  Regras que a parcela fixou: no custo por sessão **só entra saída COM atendimento** (a
  baixa digitada à mão não é de sessão nenhuma, e rateá-la daria a cada uma um custo que
  não teve) e **média sem base é nula, nunca zero**; a lista de espera filtrada **diz que
  está filtrada** (título e texto do vazio mudam — "ninguém espera" e "ninguém serve para
  este horário" são respostas diferentes); a conferência por código é **o que este sistema
  oferece no lugar do ICP-Brasil**, então não podia continuar sem tela onde digitar; e
  **modelo e protocolo se apagam mesmo** — não são registro do que aconteceu, e as sessões
  e documentos feitos com eles não mudam porque aplicar **copia**, nunca referencia.
- **Meta e teto são a mesma ideia aplicada aos dois lados** (`MetaService` parcela 28,
  `OrcamentoService` parcela 31): antes das duas, o painel comparava tudo com o **mês
  anterior** — e variação responde "melhorou?", nunca "chegamos onde a gente disse que ia
  chegar?". Sem alvo, um mês 10% melhor que um mês ruim aparece como vitória, e gastar 10%
  menos que um mês caro aparece como economia. As duas entidades seguem as mesmas regras:
  **fato datado** (uma linha por mês; reajustar agosto não reescreve julho, como a vigência
  da taxa e do preço), **índice único** (dois alvos para a mesma pergunta dariam duas
  réguas e a tela escolheria uma sem dizer qual) e **ausência ≠ zero** — sem linha é "não
  decidimos", zero é "decidimos que é zero", e inventar zero faria todo mês não planejado
  aparecer como alvo batido. O realizado vem sempre do **serviço dono** do indicador, nunca
  recalculado. Duas assimetrias que parecem detalhe: **não se projeta mês com menos de 5
  dias** (projetar no dia 2 multiplica ruído por quinze) e **ocupação não se projeta por
  regra de três**, porque ela já é uma média e multiplicá-la pelos dias que faltam daria
  300%. No orçamento, o **comprometido** (conta prevista que ainda vai vencer) conta
  separado do gasto: não é dinheiro que saiu, mas é dinheiro já decidido — e o teto costuma
  estourar no que foi CONTRATADO, não no que foi pago.
- **Apuração por tributo e resultado do mês** (parcelas 28 e 31): `TributoService` separa
  ISS, PIS, COFINS, IRPJ e CSLL desde a parcela 15 e **toda tela consolidava num número
  só** — a clínica sabia quanto de imposto saiu e não sabia **de quê**, que é a pergunta do
  contador. Na apuração, a **retenção é resolvida por recebimento** com o convênio dele:
  apurar o mês com os tributos gerais somaria o que a clínica recolhe com o que a operadora
  já reteve. A **divergência** entre o apurado agora e o gravado na época **aparece** em vez
  de ser escondida — ela significa que uma alíquota mudou depois de o dinheiro entrar, e é a
  clínica que decide qual número vai para a guia. O `ResultadoMensalService` é **regime de
  CAIXA e a tela diz isso**: competência exigiria provisão e rateio que a clínica não
  mantém, e número aproximado apresentado como contábil é pior que número honesto
  apresentado como caixa. **Taxa e imposto são DEDUÇÃO, não despesa** (saem da receita antes
  de ela existir; listá-los junto do aluguel faria a clínica achar que pode cortá-los), e
  **margem sobre zero é nula** — 0% faria um mês sem faturamento parecer um mês que faturou
  e não sobrou nada.
- **O papel que sai da agenda** (`AgendaPdfService`, parcela 29): a folha do dia e o
  comprovante do paciente. Os dois saem **sem dado clínico** — a folha circula pela sala e
  pela recepção, e o comprovante é papel avulso; nome, horário, profissional e sala bastam.
  Cancelados e faltas entram na folha **marcados, nunca sumindo**: quem lê às 14h precisa
  saber que o horário das 15h vagou, e linha ausente se confunde com horário que nunca
  existiu.
- **Aniversário e padrão de falta** (`RelacionamentoService`, parcela 29): as duas perguntas
  do balcão que o sistema gravava e ninguém lia. A data de nascimento está no cadastro desde
  sempre — a ligação mais barata que a clínica faz era a que se perdia. A janela cobre a
  **semana** porque a clínica não abre todo dia. Do outro lado, a agenda registra `Faltou`
  desde a parcela 1 e os indicadores calculam a taxa da **clínica**; a do paciente nunca foi
  lida, e é ela que decide se vale dar a alguém o horário mais disputado da semana.
  **Cancelamento avisado conta separado da falta** (quem desmarcou deu chance de reocupar) e
  a reincidência **exige base mínima**: uma falta em duas sessões dá 50% e não diz nada.
- **Acerto de inventário** (`EstoqueService`, parcela 30): a clínica só podia lançar
  `Perda` quando a contagem não batia — e **perda é uma AFIRMAÇÃO**: alguém quebrou, venceu
  ou extraviou. Quando a contagem acha A MAIS, ou quando a diferença vem de erro de
  digitação antigo, chamar de perda mente sobre o que aconteceu, e é essa mentira que faz o
  custo médio do insumo parar de valer. O tipo novo não precisou de migration (o enum é
  gravado como texto); o que virou coluna foi a **direção** do acerto, e ela é campo
  separado em vez de quantidade negativa porque negativa espalharia `Math.Abs` por todo
  cálculo de saldo — um esquecido daria saldo negativo silencioso. O método recebe a
  quantidade **CONTADA**, nunca a diferença: quem está com a caixa na mão conta "tenho 37".
- **Quem parou de vir** (`RetencaoPacienteService`, parcela 32): o recall dispara mensagens
  por regra de tempo; esta é a **lista**, para a direção olhar caso a caso e decidir quem
  vale um telefonema de verdade — o paciente de tratamento longo que some vale dez recalls
  disparados no vazio. **Quem tem sessão futura marcada não sumiu**, por mais tempo que faça
  desde a última: ele já voltou, só ainda não veio. A base é o **atendimento**, não o
  agendamento (cancelado não é visita). Pacote em aberto é **destaque, não filtro**: o
  paciente pagou sessões que não usou, e essa ligação a clínica deve tanto a ela quanto a
  ele.
- **O consultório do profissional** (`ConsultorioService`, `AvaliacaoClinicaService`,
  parcela 36): o quinto app (`Clinica.Clinico.exe`, "Consultório"), na máquina do médico e
  do fisioterapeuta. Ele responde uma pergunta que nenhuma tela respondia: **o que eu
  atendi e ainda não escrevi.** É a mesma família do defeito que dá nome ao produto — a
  guia obtida +24h depois que ninguém lembra —, só que do lado clínico: a sessão acontece,
  o paciente vai embora e a evolução fica "para depois", que é o dia em que já não se
  lembra o que foi feito. A pendência olha **só dias anteriores**: a sessão das 14h não tem
  evolução às 14h05 porque o paciente ainda está na sala. O casamento sessão × evolução é
  pelo `Evolucao.AgendamentoId` e cai para paciente + data quando ele é nulo — sem esse
  caminho de baixo, a evolução escrita direto no prontuário faria o consultório cobrar para
  sempre um registro que já existe. **Sem `Profissional` vinculado ao login o app mostra o
  dia da clínica inteira e DIZ que está fazendo isso**: tela vazia se lê como defeito, não
  como cadastro faltando. O `PacienteEmFoco` é singleton porque no consultório o paciente é
  **contexto**, não parâmetro de tela — quem atende escolhe uma vez e passa vinte minutos
  entre quatro telas sobre a mesma pessoa. E o módulo não declara `Inicial`: o exe carrega
  um módulo só, então o primeiro item já é a abertura, e marcá-lo faria o Consultório
  vencer o painel da direção dentro do Gerente (o defeito que a parcela 22 corrigiu).
- **O Consultório não é uma ilha** (parcela 36): a primeira versão do módulo gravava
  `AvaliacaoClinica` e evolução, e **só ele as lia** — a sexta ocorrência do defeito
  recorrente do projeto, cometida por quem já o tinha documentado cinco vezes. O que
  fecha o circuito, nos dois sentidos:
  **Consultório → Gerente**: `PainelDirecaoService` ganhou `AssuntoDirecao.ProntuarioEmAberto`
  — a direção é a única que enxerga a clínica inteira, e o número vem do
  `ConsultorioService`, dono da leitura. Guia já faturada sem evolução é alerta
  **separado e mais grave**: a sessão sem registro é prontuário incompleto, a guia sem
  registro é cobrança sem o documento que a sustenta. No BI,
  `ProdutividadeProfissional.CompletudeProntuario` divide dois números que estavam lado a
  lado desde a parcela 5 sem ninguém os dividir — e é **nula, nunca 0%**, sem sessão
  atendida, senão acusaria de negligência quem tirou férias.
  **Consultório → Recepção/paciente**: as escalas entram no **relatório de evolução** e no
  prontuário da Recepção. O relatório é o papel que o paciente leva ao convênio, e sem
  isso ele descreveria metade do tratamento; a faixa sai como foi **gravada na aplicação**,
  não recalculada pela definição de hoje.
  **Recepção/Financeiro/Faturamento → Consultório**: o `ElegibilidadeService` aparece na
  tela de atendimento. Ele foi feito para o balcão — o lugar onde o paciente está de corpo
  presente —, e o consultório é o segundo: a urgência viaja **com cada alerta**, porque
  carteirinha vencida (vermelho) e dívida (amarelo) chegam juntas e pintar as duas da cor
  da pior faria a segunda parecer impedimento.
  **O que NÃO foi feito, e por quê**: o `PrevencaoGlosaService` seria o lugar natural do
  aviso "guia sem prontuário", e ele é chamado **só pelo app congelado**. Acrescentar o
  alerta mudaria o comportamento do faturamento em produção e dispararia para TODA guia
  numa clínica que ainda não documenta — alerta que dispara para todo mundo é alerta que
  ninguém lê. A leitura foi para a direção, onde é nova.
- **A folha de infusão e a checagem de enfermagem** (`PrescricaoInternaService`,
  `ChecagemPrescricaoService`, parcela 42): a clínica faz infusão, e a folha que ela usa
  não é a receita — é prescrição de vários itens "destinada ao próprio consultório, o
  paciente não vai apresentar lá fora". Entidade NOVA, e não um valor a mais em
  `TipoDocumentoClinico`: `DocumentoClinico` é fato IMUTÁVEL emitido (é essa regra que
  garante a segunda via idêntica), e esta tem ciclo de vida — rascunho → assinada →
  executada → encerrada. Enfiar estado vivo lá quebraria as outras sete impressões.
  **Checar não é preencher um campo**: é afirmar "foi prescrito assim e foi realizado
  assim" e responder por isso — o peso da baixa da guia, do lado clínico. O ✓ com o
  horário é o realizado; a **"rodela"** (horário circulado) é o não realizado, e ela
  **exige justificativa** — terceira recusa do projeto, junto da divergência do
  fechamento de caixa e do descarte de problema.
  As regras: **a hora é INFORMADA, nunca `DateTime.Now`** (a técnica administra às 14h e
  registra às 14h20; o relógio vai em `RegistradoEm` AO LADO, e a diferença entre os dois
  é o que uma auditoria de enfermagem procura); **hora futura é recusada**, porque
  pré-checagem é o hábito que faz um item aparecer como feito num paciente que saiu antes
  de recebê-lo — é a única regra do projeto com relógio injetado, e é assim porque regra
  de segurança que não dá para testar apodrece sem ninguém notar; **checagem não se apaga,
  RETIFICA-SE** (linha nova apontando a anterior, com motivo — apagar e regravar é
  exatamente o gesto que a auditoria procura, e a folha impressa mostra as duas);
  **item já checado não se edita nem se suspende**, senão a folha desdiz uma execução já
  assinada; **quem checa é quem fez LOGIN** (daí o perfil `Enfermagem` e a permissão
  `ChecarPrescricao`, separada de `Prescrever` — é serem duas pessoas que dá valor à
  conferência); e **o "se necessário" não conta como pendência**, senão toda folha com SOS
  ficaria eternamente aguardando e o contador da sala apontaria para nada.
  **O circuito de volta**: o exemplo que a clínica deu foi "o paciente apresentou reação
  alérgica e não quis fazer a dipirona" — e essa frase morreria num campo de texto. Agora
  a não realização por reação grava a **alergia na lista de problemas no mesmo
  SaveChanges**, e a conferência da parcela 40 acende na próxima prescrição.
- **A certificação digital, e por que NÃO se escaneia o carimbo** (`AssinaturaDigitalService`,
  `CertificadoIcpBrasil`, `AssinaturaDePrescricaoService`, parcela 42): o pedido original
  era escanear o carimbo e colar no PDF. Isso é **pior do que não assinar**: imagem não
  prova autoria (qualquer um copia para outro documento), e a partir do dia em que está no
  banco existe uma assinatura da médica reutilizável por quem tiver acesso ao sistema — o
  pior é que ela PARECE garantia, que é o que `DocumentosClinicosPdfService` se recusa a
  fazer desde a parcela 3.
  O que se faz é **assinatura qualificada ICP-Brasil**: PKCS#7 destacado SHA-256 no PDF
  (PdfSharp + `SignedCms`), carimbo do tempo RFC 3161 **opcional** (Configurações →
  Operação). O que NÃO se faz é LTV/PAdES-LT — anunciá-lo sem implementá-lo seria a mesma
  mentira do carimbo escaneado.
  **O CPF sai de DENTRO do certificado** (OID `2.16.76.1.3.1`, no *subjectAltName* como
  `otherName`; o .NET não expõe `otherName`, então o ASN.1 é lido à mão). Não é
  preciosismo: é a metade que faz a assinatura valer. Sem comparar esse CPF com
  `Profissional.Cpf`, o sistema provaria só que ALGUÉM com ALGUM token assinou, e o e-CPF
  da recepcionista assinaria a prescrição da médica sem um alerta. Certificado sem CPF,
  vencido, ou de outra pessoa é **recusado**; profissional sem CPF cadastrado também, e
  isso é decisão — aceitar em silêncio faria a conferência existir só para quem já tinha o
  campo preenchido.
  **UMA assinatura eletrônica por folha, e é a de quem prescreve** (decisão da clínica,
  ago/2026 — a primeira versão exigia duas). Quem confere e assina a EXECUÇÃO é a
  enfermeira, **na via impressa**, depois que a folha sai da impressora: por isso a
  Prescrição sai com as **colunas de checagem em branco** ("Feito às", "Visto") e três
  linhas para o motivo do não realizado. A prescritora assinar um PDF com esses campos
  vazios é correto — eles são pré-impressos do formulário, como no talão de papel; ela
  atesta o que MANDOU fazer, e o que foi feito entra por cima, à mão. Isso dispensou um
  segundo e-CPF (um por técnica) para produzir, com muita cerimônia, a garantia que a
  caneta dela já dava — e de quebra resolveu a restrição de o PDF não se assinar
  incrementalmente. O **Registro de execução** continua existindo como espelho eletrônico
  do que foi checado (prontuário e conferência do fim do dia), é montado NA HORA porque
  muda a cada item, e o rodapé dele diz que a autoria está no papel.
  **A reimpressão devolve os BYTES GUARDADOS**, nunca um PDF novo: a assinatura cobre uma
  faixa de bytes do arquivo, e um documento "igual" regerado agora abriria como inválido —
  por isso `ArquivoAssinado` é tabela e não cache. E **a folha nunca promete mais do que
  garante**: o rodapé escreve o nível da assinatura e, sem ACT contratada, diz que a data é
  declarada pelo relógio de quem assinou.
- **O documento que SAI da clínica, e a lei que manda nele** (`ConformidadeDocumentoClinico`,
  `AssinaturaDeDocumentoClinicoService`, parcela 43): a parcela 42 pôs assinatura
  qualificada na folha de infusão — que é o **único documento que a lei dispensa** (art. 13,
  parágrafo único, da Lei 14.063/2020: "não se aplica aos atos internos do ambiente
  hospitalar") — e deixou sem assinatura os quatro que a lei realmente disciplina. O motor
  estava pronto e testado; **faltava a porta**, na variante mais cara do defeito recorrente.
  O mapa completo está em `docs/prescricao-eletronica-conformidade.md`. As regras:
  **atestado em ARQUIVO só vale com assinatura qualificada** (art. 13) — não é documento com
  defeito, é arquivo sem valor —, enquanto **em papel, assinado à caneta, ele sempre valeu**;
  por isso a conferência muda de resposta conforme a forma de entrega, e o aviso SOME quando
  a folha vai ser assinada. Receita, pedido de exame e declaração caem no art. 14 (avançada
  **ou** qualificada) e assinamos com a qualificada mesmo assim, porque é a que a farmácia
  confere sozinha, num validador público, sem cadastro em plataforma nenhuma.
  O **conteúdo** é a metade que ninguém lembra: o art. 35 da Lei 5.991/1973 exige **endereço
  residencial do paciente** e **modo de usar** para a receita ser AVIADA, e o sistema
  imprimia receita desde a parcela 3 **sem ter onde guardar o endereço** — a clínica
  descobria na farmácia, com o paciente na fila. O endereço sai **só na receita** (num
  atestado iria para o RH sem exigência que o justifique — a economia do CID).
  A emissão **avisa**; a **assinatura RECUSA**, e é a quarta recusa do projeto: assinar sela
  os bytes, corrigir depois exige cancelar e emitir outro, e um PDF criptograficamente
  impecável de uma receita que a farmácia não pode aviar é a **garantia aparente** que este
  projeto se recusa a produzir desde a parcela 3.
  **Não precisamos de portal de validação**: `assinaturadigital.iti.gov.br` — o validador
  de documentos de SAÚDE do ITI, com apoio do CFM e do CFF — é público e gratuito, e é o
  que os CRFs mandam o farmacêutico usar. Não confundir com o genérico
  (`validar.iti.gov.br`): só o de saúde responde **"quem assinou é prescritor com registro
  ATIVO?"**, que é a pergunta que decide a dispensação, e é onde ela é registrada. O
  documento assinado sai com o endereço por extenso, um **QR** que leva à página do
  farmacêutico e um bloco **"PARA O FARMACÊUTICO"** com o passo a passo — PDF assinado que
  chega sem uma palavra sobre como conferir é recusado por precaução, e com razão: pela
  orientação dos CRFs, farmácia que não consegue verificar não é obrigada a dispensar. O QR
  leva ao validador e **não carrega o documento** (quem hospeda receita é plataforma —
  Memed, Mevo, CFM); prometer que ele "abre a receita" seria mentir sobre o que ele faz. E **a via impressa de
  um documento assinado é CÓPIA** (a assinatura vive nos bytes, não na tinta), então assinar
  **salva e abre o arquivo**, nunca manda para a impressora. A reimpressão devolve os
  **bytes guardados**, e a regra mora dentro do `DocumentosClinicosPdfService.GerarAsync` de
  propósito: são seis telas chamando, e uma que esquecesse produziria segunda via inválida
  sem nenhum sinal. O seletor de certificado subiu para o **shell** pelo argumento de
  sempre — capacidade que existe numa porta só é o defeito de novo, aqui com o agravante de
  ser a assinatura que dá valor jurídico ao arquivo.
- **Prescrever o alérgeno que a própria clínica anotou** (`PrescricaoService`, parcela 40):
  desde a parcela 37 o sistema GUARDA as alergias (`NaturezaProblema.Alergia`, com a regra
  de alertar mesmo dadas por resolvidas) e a emissão de receita **nunca as consultou** — a
  base sabia que a paciente é alérgica a dipirona, o profissional escrevia "Dipirona
  500mg" e o papel saía sem uma palavra. É o mesmo defeito recorrente do projeto (dado
  gravado sem leitor), só que aqui ele não custa uma guia.
  **O que o serviço NÃO é**: checador de interação medicamentosa. Isso exige base
  farmacológica licenciada e atualizada, e uma checagem caseira erraria nos dois sentidos
  e — pior — passaria a impressão de estar cobrindo o assunto. Ele compara o que está
  sendo prescrito com **o que a própria clínica anotou sobre este paciente**.
  A comparação é **textual** (a receita é texto livre por desenho) e por isso tem dois
  cuidados que valem tanto quanto o alerta: **palavra INTEIRA, nunca trecho** ("sal" dentro
  de "salbutamol" é coincidência de letras) e **piso de 4 caracteres** com lista de
  dispensáveis — sem eles, uma alergia anotada como "alergia a X" acenderia em toda receita,
  e **alerta que dispara à toa é alerta que se fecha sem ler**, o que produz o falso
  negativo na semana seguinte. Os testes cobrem as DUAS direções com o mesmo peso.
  **Avisa e exige confirmação — não impede**: o registro pode estar errado, pode haver
  dessensibilização, e quem decide é quem assina; mas não pode acontecer *sem alguém
  perceber*. É o segundo caso do projeto em que a tela cobra confirmação explícita (o
  primeiro é a divergência do fechamento de caixa). A conferência mora no **shell**
  (`DocumentoEdicaoViewModel`), não na tela do Consultório: é o único lugar por onde toda
  receita passa, nas duas portas — **checagem de segurança que só existe em uma delas é o
  defeito de novo, com a agravante de dar a impressão de estar coberto**. Ela reconfere
  no clique de emitir, porque a da abertura viu uma receita em branco; e **falha da
  conferência não bloqueia a emissão** (banco lento não pode impedir um atestado), mas
  também não passa calada — vira aviso de que a checagem não rodou.
  **Medicação contínua entra como CONTEXTO, sem casar com item**: o valor dela é o que
  ainda não foi escrito, e casá-la produziria "você prescreveu o que ele já toma", que é o
  caso normal da renovação de receita.
- **Botão aceso que não faz NADA** (parcela 41, 2ª rodada — regressão que o cliente pegou):
  os quatro botões de emitir da tela de Prescrições não abriam janela nenhuma. O comando
  começava com `if (_pacienteId == 0) return;` e voltava **calado**, enquanto o `IsEnabled`
  só olhava a permissão — e a tela abre pela sidebar **sem paciente em foco**. Quem clica e
  não vê nada acontecer conclui que o sistema quebrou; não tem como adivinhar que faltava
  escolher alguém. O mesmo defeito estava na tela de Prescrições da RECEPÇÃO.
  A regra do projeto já dizia isto para PERMISSÃO ("duas barreiras: `IsEnabled` explica,
  `Exigir` impede"); ela vale para **toda pré-condição**: o botão diz que não dá, e a
  guarda diz por quê. **Guarda que volta em silêncio é botão que não faz nada.**
  A **checagem 21** cobra as duas metades juntas, e o cuidado está nas EXCEÇÕES — sem elas
  a checagem viraria ruído e alguém a desligaria: guarda sobre **parâmetro** (`if (linha is
  null)`) nunca dispara vindo de botão de linha; guarda de **reentrância**
  (`if (Carregando)`) é "já estou fazendo", não "não dá para fazer", e ali o botão deve
  mesmo ficar aceso; e guarda sobre **variável local** (`var caminho = Escolher(); if
  (caminho is null)`) é diálogo cancelado, onde sair calado é o certo. O casamento
  tela↔comando é pela convenção de nome (`FooView.xaml` ↔ `FooViewModel`), nunca pelo nome
  do comando solto — `EditarCommand` existe em meia dúzia de telas que não se conhecem.
  Ela também **ignora comentários** antes de procurar a guarda: um bloco explicativo de três
  linhas empurrava a guarda para fora da janela de busca, e foi assim que a checagem deixou
  de ver o defeito na primeira tentativa.
- **O nome do enum vazando para a tela** (`RotulosEnum`, parcela 41): o CLIENTE achou em
  produção — o seletor de tipo da tela de documento clínico oferecia **"PedidoExame"**. A
  causa é de uma linha: `ComboBox` amarrado a uma lista de enum sem `DisplayMemberPath`
  nem `ItemTemplate`, e o WPF, sem nada melhor, chama `ToString()`. A varredura mostrou
  que eram **10 enums em 16 telas** ("RelatorioEvolucao", "CartaoCredito",
  "PercentualDaReceita", "CreditoAVista"). O rótulo certo do documento já existia
  (`TipoDocumentoInfo.Rotular`, parcela 3) e a tela não o usava — **a variante mais barata
  do defeito recorrente do projeto**: o build passa, o teste passa, e só quem abre a tela
  vê. `RotulosEnum.De` é o ponto único que resolve por tipo e reaproveita os rotuladores
  que já existiam; `ItemRotuloEnum` é a porta de XAML. Enum sem rótulo declarado cai no
  **humanizador** ("Pedido exame"), que é pior que o rótulo à mão e melhor que o
  identificador — e de propósito ele não some: a frase estranha é o que faz alguém vir
  escrever o rótulo. A **checagem 20** casa o `ItemsSource` com o TIPO declarado no VM e só
  reclama quando é enum (lista de string não precisa de rótulo); no **faturamento
  congelado** ela vira AVISO, porque o defeito está lá e não se corrige por decreto de
  leiaute — esconder seria fingir que a suíte está limpa quando a tela do cliente não está.
- **Botão ao lado de campo usa a ALTURA do campo** (parcela 41): campos têm `MinHeight`
  36 e `BotaoPequeno` (base de todos os `BotaoAcaoGrid*`) tem 26. Numa `Grid`, o botão
  baixo é esticado até a altura da linha e sai com o padding errado — é o **"fora do
  esquadro"** que o cliente viu na tela de documento clínico, em cinco linhas de uma vez.
  `BotaoPequeno` é para linha DENSA de tabela; barra de formulário usa `BotaoSecundario`
  com `VerticalAlignment="Center"`. E **destrutivo repetido não é sólido**: um "Remover"
  vermelho cheio por linha de receita faz o formulário inteiro parecer tela de erro e gasta
  a cor mais forte da paleta na ação menos frequente — daí o `BotaoSecundarioPerigo`
  (contorno). O sólido fica para o destrutivo que é o ASSUNTO da tela.
- **A sidebar do Consultório tinha TRÊS itens** (parcela 39): e não porque o app do médico
  seja simples — porque as portas estavam no módulo errado. A auditoria achou três lacunas
  da mesma família, e é a **sétima** ocorrência do defeito recorrente do projeto, na
  variante mais discreta de todas: não é dado sem leitor nem serviço sem chamador, é
  **capacidade inteira e testada cuja única porta está no módulo de quem não a usa**.
  (a) **Prescrições** — receita, atestado, comparecimento e pedido de exame só se emitiam
  pela RECEPÇÃO. Quem prescreve é quem atende, e o app instalado na sala do médico não
  tinha por onde; a parcela 36 já tinha subido a emissão para o shell (`DocumentoWindow`)
  exatamente por isto, e ninguém construiu a porta. A tela do consultório **abre no
  paciente em foco** e oferece os quatro tipos como BOTÕES, porque ali a decisão vem antes
  do clique: ninguém pensa "vou emitir um documento", pensa "vou dar um atestado".
  (b) **Minha semana** — "Meu dia" responde o que acontece hoje e não responde o que se
  pergunta com o paciente ainda na frente ("quando eu tenho espaço?", "quinta está
  cheia?"). A recepção tem visão de semana desde a parcela 26. Sete dias numa consulta só,
  não sete — o banco é remoto; a semana começa na **segunda** (o domingo é o SÉTIMO dia, e
  tratá-lo como primeiro devolveria a semana que só começa amanhã); **dia sem horário
  aparece vazio**, porque semana com cinco colunas faria procurar a quarta que sumiu.
  (c) **Meus números** — `ProdutividadeProfissional` existe desde a parcela 5 e
  `CompletudeProntuario` desde a 36, e o único leitor de ambos era o BI do GERENTE: o
  sistema media quem atende e a pessoa medida não via o próprio número. **Indicador que só
  o chefe enxerga não corrige comportamento nenhum** — ele só produz a conversa
  desagradável no fim do mês. A tela mostra **só quem está logado e não compara colegas**
  (ranking é decisão de gestão; no app de cada um viraria placar), e a **dívida de
  prontuário é de HOJE, não do período**: ela é fila de trabalho, e recortá-la pelo filtro
  faria escolher "mês passado" esconder o que está em aberto agora.
  A lição para a próxima auditoria: **ao procurar chamador em produção, conte também
  quantos ITENS DE MENU o módulo tem.** Sidebar curta demais para o que o app faz é
  sintoma, não simplicidade.
- **A chamada do próximo paciente** (parcela 38): "Meu dia" do Consultório era uma **lista
  corrida** — ela responde "quem vem hoje" e não responde a pergunta que quem atende faz
  vinte vezes por dia, "**quem já está aí e quem eu posso chamar agora**". Virou kanban de
  cinco colunas, as MESMAS da fila do balcão (que é kanban desde a parcela 26). E ganhou o
  recado de volta: quem chama pelo nome na sala de espera é a **recepção** — o profissional
  está na sala, com a porta fechada —, então o botão "Chamar próximo" carimba
  `Agendamento.ChamadoEm` e a fila do balcão anuncia a pessoa. **A sincronização é o
  BANCO**, como todo o resto da suíte: nem fila de mensagens, nem evento, nem um módulo
  conhecendo o outro; os dois leem a mesma linha, e é isso que faz os dois quadros nunca
  divergirem. O estágio `Chamado` **não virou coluna no banco** — é derivado dos carimbos
  de hora, como os outros quatro (`Agendamento.Etapa`); o que precisou de migration foi só
  o fato novo, e ela é aditiva (o faturamento congelado só lê `StatusAgendamento`).
  As regras: **só se chama quem já fez check-in** (anunciar quem não chegou tiraria da fila
  a única informação que a torna confiável, a de quem está no prédio); **chamar de novo não
  reinicia o cronômetro** (`??=`) porque quem insiste precisa ver há QUANTO tempo chamou, e
  o segundo clique esconderia justamente o caso demorado; **chamar existe dos dois lados**
  (em metade das clínicas o profissional avisa pela porta, e exigir o clique da sala faria
  a coluna nascer sempre vazia num fluxo que funciona há anos — carimba quem clicar
  primeiro); **entrar carimba a chamada junto**, porque linha do tempo com entrada e sem
  chamada não existe; e a **espera para na CHAMADA, não na entrada** — o que se mede é
  quanto tempo o paciente ficou sem notícia, e contar até ele levantar da cadeira somaria o
  tempo de atravessar a sala. A **releitura periódica** (1 min, ligada/desligada pelo
  Loaded/Unloaded da View) é o que faz o recado CHEGAR: até aqui as duas telas só reliam
  por clique, o que bastava porque tudo o que mexia no quadro era clicado nelas mesmas. Ela
  é **silenciosa** — não acende "Carregando" nem escreve erro, porque quem está no balcão
  com um paciente à frente não pode ver a fila piscar em branco a cada minuto — e só relê
  **hoje**. A faixa acima do quadro **nomeia quem chamar e para qual sala**: a coluna
  sozinha não bastaria, porque cartão que muda de coluna em silêncio é cartão que ninguém
  vê, e "1 paciente chamado" mandaria a recepcionista procurar antes de abrir a boca.
- **O que o profissional precisa ver e não alcança** (parcela 37): a auditoria do módulo
  do Consultório achou três lacunas da mesma família, e nenhuma pedia capacidade nova —
  pedia **porta no módulo certo**, que é a variante mais discreta do defeito recorrente
  do projeto (o CI fica verde e a Recepção usa todo dia). (a) **Anexos**: `AnexarAsync`
  existe desde a parcela 2 e só a Recepção o chamava — o Consultório EMITIA pedido de
  exame e não tinha onde ler o laudo de volta; ele pedia e não recebia. (b) **Busca no
  prontuário**: existe desde a parcela 28, dentro do módulo da Recepção, e o comentário
  que a acompanha lá diz "a pergunta que o profissional faz antes de atender é sempre a
  mesma" — a feature foi justificada pelo profissional e entregue na tela de quem não
  atende; a de Atendimento mostra três sessões, e num tratamento de quarenta a sessão 12
  era inalcançável. (c) **Contagem de anexos** era uma consulta POR SESSÃO
  (`ContagemDeAnexosAsync` a fez virar uma só). Ao procurar chamador em produção,
  procure também **em qual módulo está a tela** que lê o que este grava.
- **Medidas seriadas** (`MedidaClinicaService`, `CatalogoMedidas`, parcela 37): a parcela
  36 deu número às cinco especialidades pelas escalas e deixou de fora o mais básico. O
  argumento que fechou a lacuna foi o FINDRISC — ele **pergunta** IMC e circunferência de
  cintura, o paciente responde, o escore é gravado e os dados que o produziram evaporavam.
  O catálogo (peso, altura, cintura, PA, glicemia, HbA1c) mora em **CÓDIGO**, pelo desenho
  das escalas: o corte do IMC é definição publicada, não configuração da clínica.
  **Tudo o que descreve o tipo é COPIADO na colheita** (nome, unidade, faixa,
  interpretação). **O IMC não se digita** — é derivado do peso com a altura vigente, e a
  DATA dessa altura vai junto: um IMC calculado com altura de três anos atrás continua
  sendo a melhor leitura disponível, desde que quem lê saiba disso; gravá-lo criaria um
  terceiro número livre para contradizer os dois que o originam. Sem altura ANTES do peso
  cai-se para a primeira registrada, porque o adulto é pesado toda consulta e medido uma
  vez, muitas vezes depois — devolver curva vazia a quem tem os dois dados seria pior. A
  **única recusa é a implausibilidade** (2500 kg é dedo no teclado; 210 kg é anormal e
  possível, e recusá-lo esconderia quem precisa de atenção). **Faixa ausente ≠ faixa
  normal**: o peso isolado não tem leitura publicada e o selo SOME. **Meia pressão
  arterial não existe** (tipo com par exige os dois, e diastólica maior que a sistólica é
  campo trocado). Os cortes da cintura são os do FINDRISC e seguem o sexo — usar outro
  faria a mesma clínica dar duas leituras da mesma fita métrica. **Uma colheita só não é
  variação nenhuma**: `Variacao` é nula, nunca zero.
- **Lista de problemas** (`ProblemaPacienteService`, parcela 37): até aqui o CID morava só
  dentro de `DocumentoClinico` — um campo por papel emitido, nunca o diagnóstico da
  pessoa. O profissional redigitava "M54.5" a cada atestado, ninguém respondia "o que este
  paciente tem?" sem ler o prontuário inteiro, e a alergia ficava enterrada em texto livre.
  A lista é do **PACIENTE, não da sessão** (a evolução de origem é procedência opcional).
  **Não se apaga: muda-se a situação** — resolvido e descartado continuam na base, como a
  NC do faturamento. **Descartar exige motivo escrito**, única recusa do serviço, pela
  razão da justificativa do fechamento de caixa. **Alergia alerta mesmo dada por
  resolvida** — "resolvida" numa alergia é quase sempre "não reagiu da última vez", e o
  dia em que reagir é o dia em que o aviso teria valido; só o descarte a cala. **O CID é
  opcional**: exigi-lo faria o fisioterapeuta e o acupunturista pararem de usar a lista, e
  lista pela metade é pior que nenhuma porque seria lida como completa. Na tela de
  atendimento os alertas clínicos ficam em lista **separada** da administrativa: as duas
  são "avisos" e é só isso que têm em comum — carteirinha vencida se resolve no balcão
  depois, alergia se resolve ANTES de prescrever.
- **Tela que abre vazia é tela inacabada** (parcela 37, rodada de UI): as telas clínicas
  nasciam com uma caixa de busca vazia, uma coluna de 300 px em branco da altura da janela
  e um miolo de mil pixels com uma frase cinza flutuando no meio. Funcionava — e obrigava
  o profissional a DIGITAR o nome de alguém que o sistema já sabia que ele ia atender às
  9h. O `SeletorClinicoViewModel` + `PainelPacienteClinico` abrem com a **fila do dia** e a
  **carteira** (as duas já existiam, cada uma servindo a uma tela só) e deixam a busca como
  atalho para quem está fora do dia; a linha da fila leva o `AgendamentoId`, que é o que
  faz a evolução nascer ligada ao horário. Três armadilhas que a implementação encontrou e
  que valem para qualquer lista da suíte: (a) **uma ListBox por bloco não funciona** — duas
  amarradas ao mesmo `SelectedItem` se limpam mutuamente, porque a que não contém o item
  escolhido devolve `null` ao binding; use `CollectionViewSource` + `GroupStyle` sobre UMA
  coleção; (b) **remonte por busca CONCLUÍDA** (`SeletorPacienteViewModel.Atualizou`), não
  por `CollectionChanged`, que dispara uma vez por linha inserida; (c) **vazio antes da
  resposta não é resposta** — enquanto a busca do termo digitado não voltou, o painel não
  diz "nenhum paciente encontrado". A `FaixaPaciente` (avatar, nome, contexto, ações) fica
  no topo do conteúdo porque num app onde se passa vinte minutos entre quatro abas sobre a
  MESMA pessoa a identidade é âncora, não rótulo — e as ações da tela viajam com ela em vez
  de ficarem acesas no cabeçalho da página sobre uma tela sem paciente nenhum.
- **Chave de navegação sem item de menu = botão que não faz NADA** (parcela 37, 4ª
  rodada — a regressão que foi para produção). A navegação da suíte é por STRING:
  `NavegacaoSuite.Ir(chave)` faz o shell procurar a chave em `ShellViewModel.Itens`, que é
  a lista da SIDEBAR, e sem achar ele **retorna false em silêncio** — sem erro, sem log,
  sem exceção. Ao tirar as cinco telas clínicas do menu, tirei-as junto da lista: "Atender"
  na fila do dia, os atalhos da carteira e o painel da direção pararam de abrir qualquer
  coisa de uma vez. O `compilar-sombra` passou (é string), o `verificar-suite` passou (era
  C#, não XAML) e os 1023 testes passaram (nenhum monta a sidebar). A correção é
  `ItemMenuModulo.Oculto` — **navegável sem ocupar linha no menu**; `Itens` guarda tudo o
  que é destino, `Grupos` filtra o que aparece. A **checagem 19** do `verificar-suite`
  passou a exigir que toda `NavegacaoSuite.Ir(ModuloX.ChaveY)` tenha o item declarado, e é
  autotestada contra esta regressão.
- **Lista → tela do item. Não enfie mestre-detalhe numa tela só** (parcela 37, 4ª
  rodada — a correção mais cara da parcela, e a que o cliente reprovou em voz alta). As
  cinco telas clínicas tinham, CADA UMA, uma coluna de 300 px com a lista de pacientes
  grudada à esquerda. Somadas, eram seis cópias da mesma lista, metade da largura útil
  gasta com ela, e o nome do paciente repetido em toda tela — inclusive vinte minutos
  depois de ele ter sido escolhido. O desenho certo tem dois passos: **telas de LISTA com
  a largura inteira** (Meu dia, Meus pacientes) e a **tela do item** atrás de um clique
  (`PacienteWorkspaceView`: identidade no topo, uma vez, e as seções em ABAS). A regra que
  decide: **seção que só existe COM um item escolhido não é item de menu** — como item ela
  abre em branco pedindo que você vá primeiro a outro lugar, e isso ensina o usuário a
  errar; como aba, ela diz sozinha a quem pertence. As chaves de navegação antigas
  continuam valendo e caem cada uma na sua aba (`ModuloClinico.AbaDe`), porque renomear
  contrato de navegação para arrumar leiaute quebra o que funciona noutro módulo.
- **Ferramenta de uso pontual mora em BOTÃO, não em painel aberto** (parcela 37, 3ª
  rodada): o mapa corporal ocupava uma aba de 530 px permanente na tela de atendimento e o
  formulário de colher medida ocupava um terço da tela de Medidas — os dois para atos que
  acontecem uma vez por consulta. O resultado era o previsível: o mapa não cabia (as
  figuras são Canvas de 220×460 que NÃO esticam, então sobrava rolagem e os botões do
  rodapé saíam cortados pela borda da janela) e a série de medidas — que é o que se OLHA
  naquela tela — era empurrada para baixo da dobra por um formulário que ninguém estava
  preenchendo. Os dois viraram janela (`MapaCorporalWindow`, `RegistrarMedidaWindow`), o
  que de quebra deu ao mapa os 960 px de mínimo que a Recepção já lhe dava. A pergunta que
  decide: **isto é o que a pessoa VÊ nesta tela, ou o que ela FAZ de vez em quando?** O
  segundo caso é botão.
- **A ficha do paciente é LISTA → TELA DO ITEM, e as abas têm estilo** (parcela 47): a
  tela de Pacientes da Recepção era o defeito do README inteiro numa tela só — coluna de
  360 px com a lista grudada à esquerda para sempre, e a ficha à direita como pilha de
  **oito `Card` com moldura**. Virou o que a regra manda: lista de largura inteira
  (`ItemPacienteLinha`, com telefone, que só existia dentro da ficha) e a ficha atrás de um
  clique, com cabeçalho da pessoa uma vez e cinco **abas** — o MESMO desenho do
  `PacienteWorkspaceView` do Consultório. Dois desenhos para "ficha do paciente" no mesmo
  sistema é o que faz a recepcionista achar que abriu outro programa.
  O que destravou isso foi o `Styles/Componentes/Abas.xaml`: a suíte usava `TabControl`
  desde a parcela 37 e **nunca teve estilo para ele**, então o WPF desenhava o tema
  clássico — abas em forma de pasta, gradiente cinza, moldura 3D em volta do conteúdo.
  Visual de Windows XP no topo da tela, sempre visível, dentro de um app que no resto é
  plano; é a peça que mais denuncia idade. O estilo é **implícito, sem `x:Key`**, de
  propósito: as abas que já existiam no Consultório e no Gerente ficaram modernas sem
  tocar numa linha delas. O sublinhado ocupa o lugar mesmo apagado (`Transparent`, não
  `Collapsed`), senão o rótulo sobe 2 px ao ser escolhido e a régua treme a cada troca.
  No **Novo atendimento**, os quatro `Border` empilhados de aviso (carteirinha, consulta,
  cota, pendências) viraram **uma superfície com uma linha por aviso**, cor no traço de
  3 px à esquerda. Eles somavam até 280 px acima do formulário, e pioravam justamente no
  caso ruim: o paciente com quatro problemas era o que empurrava a Modalidade para fora da
  vista. A região inteira **some** quando não há aviso — em vez de quatro caixas vazias
  ocupando o lugar para dizer que não há nada.
- **O lançamento avulso é TELA CHEIA — nem faixa lateral, nem pop-up** (parcela 47,
  2ª e 3ª rodadas — a 6ª e a 7ª reprovações do cliente): a consolidação dos avisos foi
  cosmética e a tela continuava sendo o que o `README.md` proíbe — uma **faixa lateral**
  de 420 px com o formulário grudado à esquerda, permanente, para um ato que acontece
  algumas vezes por dia. A primeira correção tirou o formulário da faixa e o pôs numa
  **janela modal**; o cliente recusou também, e com razão: *"não precisa abrir uma nova
  tela, pode ser esse leiaute aí só que na mesma tela e com janela cheia"*.
  A lição corrige pela metade a regra do `README.md` — "o que a pessoa FAZ de vez em
  quando é botão ou janela" vale para o ato que acontece **DENTRO de outra tela** (o mapa
  corporal na evolução, o formulário de medida na tela de medidas). Quando o ato **É** a
  tela — o item de menu se chama "Novo atendimento" e existe só para isso —, o clique na
  sidebar já é a abertura, e exigir um segundo clique para abrir uma janela por cima é
  cerimônia sem função. **A pergunta é "isto acontece dentro de outra coisa?", não só "com
  que frequência?"**
  O desenho que ficou é o da janela com a largura da tela: cabeçalho, **barra de ação
  ancorada declarada ANTES do miolo** no `DockPanel` (senão o miolo rolante come a barra, e
  a barra é onde está o botão), miolo rolante e três passos numerados — QUEM, O QUE FOI
  FEITO, O QUE VAI SAIR. Ao fim, **LANÇADOS HOJE**: a conferência do dia, que não existia
  em lugar nenhum — quem lançava não tinha como revisar sem abrir o app de faturamento,
  que é de outra pessoa.
  Três defeitos de alinhamento que a rodada corrigiu e que valem para qualquer tela:
  (a) **barra de botões precisa de `VerticalAlignment="Center"`** — sem ele, uma mensagem
  de erro que quebra em duas linhas estica a linha da `Grid` e o WPF estica os BOTÕES
  junto; (b) **cartão em `WrapPanel` usa `Height`, não `MinHeight`** — cada filho fica com
  a altura que pede, e um nome que quebra em duas linhas deixa a fileira com a base
  serrilhada; (c) **`Button` com `Button.Template` próprio precisa declarar `Foreground`**,
  porque o estilo implícito de `Button` na suíte é o PRIMÁRIO (texto invertido) e
  `Foreground` é propriedade HERDADA — o rótulo saía branco sobre a superfície branca do
  cartão, e isso não aparece em build nenhum: só para quem abre a tela.
  O que a tela nova destravou não é leiaute, é **capacidade**: `AtendimentoService.PreverAsync`
  / `PreverModalidadesAsync`. O motor de regras sempre foi **puro** (`IRegraConvenio.Gerar`
  não grava nada), e ninguém tinha usado isso — dava para MOSTRAR o que a regra vai gerar
  antes de gerar. Cada cartão de modalidade escreve a própria consequência ("2 guias · a 2ª
  libera 09/08"), então a escolha deixa de ser às cegas: o 2º código, que é o assunto do
  produto, passa a ser anunciado no instante da decisão em vez de virar pendência amanhã.
  As regras da prévia: ela **não persiste nada** — nem atendimento, nem código, nem linha
  na agenda —, **não reabre NC**, **não renova consulta** e **não toca `paciente.Categoria`**
  (a entidade está rastreada, e a categoria calculada vazaria no próximo `SaveChanges` de
  quem quer que fosse). São N simulações em memória sobre **uma** leitura de banco, porque
  a recepcionista compara três modalidades antes de decidir e o banco é remoto. E o teste
  central é `Previa_promete_exatamente_o_que_o_lancamento_entrega`: **prévia que não bate
  com o lançamento é pior do que prévia nenhuma** — ela promete duas guias, a pessoa
  confirma, e sai uma.
- **Tela da suíte que precisa buscar ao abrir declara `ICarregarAoAbrir`** (parcela 47,
  3ª rodada): o shell monta a tela em `IModuloApp.CriarTela` e só resolve o `DataContext`
  — ele não chama método nenhum. A maioria das ViewModels dispara a busca no próprio
  construtor e funciona; as duas que vieram do FATURAMENTO na parcela 46 (Novo atendimento
  e Consultas) não faziam assim, porque lá quem chamava o `CarregarAsync` era a navegação
  do `MainViewModel` — que não existe aqui. As duas chegaram à suíte abrindo com o
  catálogo de modalidades VAZIO e a lista de consultas em branco, e **tela vazia se lê
  como sistema quebrado, não como "ninguém chamou o carregar"**. Nada disso quebra build:
  a porta existe, o serviço existe, o teste passa. O contrato resolve no **ponto único**
  por onde toda tela da suíte passa (`ShellViewModel.Navegar`), em vez de repetir a linha
  em cada construtor e depender de alguém lembrar dela na próxima porta.
- **A Recepção estava completa em FEATURE e furada em PORTA** (parcela 48): a auditoria do
  módulo achou quatro lacunas, todas da mesma família — a décima ocorrência do defeito
  recorrente do projeto — e nenhuma pedia capacidade nova.
  (a) **A cota do convênio: o balcão LIA e não ESCREVIA.** O `ElegibilidadeService` avisa
  desde a parcela 26 que a cota vai estourar (*"a próxima sessão vira glosa 2006"*), e a
  única porta para registrar a senha nova estava no app de FATURAMENTO. Quem recebe a
  senha da operadora é quem atende o telefone. É a variante que a parcela 46 já tinha
  corrigido para a consulta a renovar, e que ninguém procurou ao lado.
  (b) **O pacote só aparecia no Finalizar** — o ÚLTIMO passo, quando a sessão já
  aconteceu. Marcar a 11ª sessão de um pacote de 10 era descoberto tarde demais. A
  correção foi no **ponto único** (`ElegibilidadeService`), não na tela: de lá o alerta
  chega sozinho ao agendamento, ao check-in, à ficha e ao Consultório. **Cota e pacote são
  DOIS alertas, nunca um** — as duas contam sessões e não têm nada a ver uma com a outra:
  a cota evita GLOSA, o pacote evita ATENDER DE GRAÇA, e quem lê no balcão precisa saber
  se fala com o convênio ou com o paciente. Só avisa quem TEM ou TEVE pacote, pela regra
  de sempre: metade da clínica é de convênio e nunca comprou nada.
  (c) **O recall só rodava no Gerente**, e quem telefona é o balcão — o mesmo argumento
  que trouxe a rodada de confirmação na parcela 26, aplicado à outra ponta. A lista traz
  **todo candidato, inclusive quem a rodada não pôde gerar** (sem telefone, sem
  consentimento), com o motivo escrito: quem some da lista some da cabeça, e "12
  pacientes" que eram 30 faz a clínica concluir que quase ninguém está sumindo.
  (d) **`PerfilAcesso.Enfermagem` existia desde a parcela 42 e não levava a lugar nenhum**:
  a sala de infusão só estava no `ModuloClinico`, carregado pelo exe do MÉDICO. A tela não
  foi copiada — **subiu para o shell**, como o mapa corporal na parcela 36 —, e os dois
  módulos publicam a **MESMA chave**. Foi isso que exigiu a dedupe por chave no
  `ShellViewModel`: no Gerente, que carrega todos, a linha apareceria duas vezes, e item
  repetido faz a pessoa clicar nos dois para descobrir que são a mesma tela.
  A lição para a próxima auditoria, somada à da parcela 39 ("conte os itens de menu"):
  **procure o AVISO que a tela dá e pergunte se a porta para resolvê-lo está no mesmo
  app.** Alerta sem porta é pior que alerta nenhum — ele ensina a pessoa a ignorá-lo.
- **Permissão granular que não distingue o que a clínica distingue não é granular**
  (parcela 49): a direção apontou que "não adianta ter permissão granular se todo perfil
  nasce podendo tudo". Os padrões não davam literalmente tudo — dois davam demais, e a
  causa não era escolha, era o **bit sobrecarregado**: `VerProntuario` significava "abrir
  a ficha E ler a evolução" e `EditarProntuario` significava "cadastrar paciente E
  escrever no prontuário". Não havia como conceder um sem o outro, então a recepcionista,
  que precisa do cadastro para marcar horário, lia a evolução inteira de todo mundo. A
  granularidade existia na TELA e não no domínio.
  O corte novo (`VerFichaPaciente`/`EditarPaciente` × `VerProntuario`/`EditarProntuario`)
  é o da **LGPD**: dado de contato de um lado, dado sensível (art. 5º, II) do outro. As
  três perguntas que decidiram cada linha estão em `docs/permissoes-por-perfil.md`, com a
  tabela completa: (1) **a pessoa precisa disto para trabalhar?** — não "pode dar sem
  risco"; bit que ninguém usa vira bit que ninguém revisa; (2) **o ato apaga o trabalho de
  outra pessoa ou some com uma cobrança do sistema?** — daí saírem `EstornarBaixa` e
  `MarcarNaoConformidade` do Faturista; (3) **é dado de saúde?**
  ⚠️ **Isto TIRA capacidade de quem já a usava, e é de propósito.** A regra 3 do bloco do
  faturamento ("não tire função de quem a tinha ontem") vale para efeito COLATERAL de
  atualização; aqui a remoção É o pedido. O que a regra continua exigindo é que a
  devolução seja barata: cada bit se concede de volta a uma pessoa específica em Acessos,
  num clique, sem mexer no perfil dos outros.
  E a tela passou a **mostrar a decisão**: agrupada por assunto, com a CONSEQUÊNCIA ao
  lado de cada caixinha (`PerfisAcesso.Explicar`) e a PROCEDÊNCIA de cada uma (padrão da
  função, liberada à mão, tirada à mão). Vinte e quatro caixinhas numa lista corrida não
  são uma decisão, são um formulário — e é lendo por bloco que se percebe o bit solto que
  ninguém queria ter concedido.
- **Cadastro grudado ao lado da operação é faixa lateral** (parcela 49): as últimas cinco
  faixas da suíte estavam no Financeiro, e todas eram a mesma coisa — o CADASTRO (raro)
  ocupando 320–400 px permanentes ao lado da OPERAÇÃO (diária). Catálogo de pacotes,
  contas fixas, regras de repasse, validades do estoque e a alíquota única. Viraram botão
  + janela, e a janela recebe **o MESMO ViewModel** da tela de trás: catálogo e vendidos
  saem da mesma leitura, e dois VMs dariam duas verdades sobre a mesma tabela.
  A alíquota única do Taxas ganhou o tratamento que faltava: ela **só é aplicada enquanto
  não há tributo cadastrado**, e agora **some** quando há. Campo que não faz nada é pior
  que campo nenhum — alguém o preenche e conclui que mexeu no imposto.
  Duas armadilhas da varredura: coluna de 300–400 px numa tabela de largura inteira é a
  coluna de **Ações**, não faixa (dois falsos positivos), e trocar `BotaoPequeno` por
  `BotaoSecundario` em bloco atinge o botão de LINHA da lista, onde `BotaoPequeno` é o
  certo — e o resultado é `VerticalAlignment` duplicado, que o XML nem parseia.
- **Dica ESTÁTICA não é aviso: o campo precisa criticar a cada tecla** (parcela 51 — o
  cliente digitou oito letras num convênio "só números" e perguntou por que conseguia).
  Deixar digitar é por desenho: quem recusa é `FaturamentoService.DarBaixaAsync`, porque a
  baixa tem QUATRO portas e validar na tela cobriria uma. O que faltava era a outra metade
  da regra já escrita aqui — "a tela usa a MESMA regra para avisar ANTES do clique". Ela
  existia como **dica fixa** ("aceita somente números"), resolvida uma vez ao carregar a
  guia, e **nada acontecia enquanto se digitava**: oito letras sem uma palavra do sistema
  se leem como "aceitou", e a recusa só aparecia no Confirmar.
  Agora a crítica é derivada do que está no campo (`CriticaNumeroGuia`, a mesma
  `RegraNumeroGuia.Criticar` — sem cópia) e o botão fica apagado enquanto o número não
  serve. E as outras **duas portas com campo de número não tinham nem a dica**: a baixa em
  lote e a rodada passaram a criticar **linha a linha ANTES de processar**, cada uma com o
  formato do convênio DAQUELA linha. Não é preciosismo: o serviço recusa guia a guia, então
  uma linha ruim no meio de dez deixava as anteriores baixadas — e, na rodada BLOQUEANTE,
  o sistema travado com a pessoa sem saber qual linha corrigir.
  De quebra, a rodada escrevia `{i.Convenio}` na situação da linha — o enum de novo,
  "UnimedIntercambio · atrasada há 3 dias".
- **A rede que não cobre o app em PRODUÇÃO** (parcela 51): o `compilar-sombra` nasceu com
  nove projetos e o faturamento **de fora**, com o motivo escrito ao lado — "está
  congelado, ninguém o edita". Ele saiu do congelamento na parcela 45, e a exclusão
  sobreviveu ao motivo dela: por seis parcelas, cada linha de C# escrita no app que fatura
  a clínica só era compilada no runner Windows. Ao portar a tela de Acessos para lá, a
  inclusão pegou um `CS1061` **na primeira execução** (o `IDialogoService` do faturamento
  não tinha `PerguntarTexto`).
  O mesmo valia para o `verificar-suite`, que só varre a suíte. Ampliá-lo por inteiro
  inundaria com dívida antiga de `FontSize` numérico — e checagem que grita trinta vezes é
  checagem que alguém desliga —, então o que passou a alcançar os dois foi o grupo que pega
  **erro de runtime** (checagens 25, 26 e 27), via uma segunda lista de árvores. A regra:
  **quando uma rede exclui um projeto, o motivo tem prazo de validade — releia-o.**
- **Acessos no faturamento, e por que a trava é o BIT e não o perfil** (parcela 51): a
  direção pediu o cadastro de usuários e permissões dentro do faturamento, "só para quem é
  gerente". A trava é `Permissao.GerenciarUsuarios`, que **só o perfil Gerente recebe por
  padrão** — amarrar ao PERFIL contradiria o modelo da parcela 45 e tiraria justamente o
  que a direção pediu na 49: poder conceder a uma pessoa específica sem promovê-la.
  A tela é **porte, não compartilhamento**: o faturamento não pode referenciar
  `Clinica.Desktop.Shell` (os dois declaram tipos em `Clinica.Desktop.Controls`), e as
  ViewModels usam `CollectionViewSource`, então também não sobem para `Application`. É o
  débito permanente da Fase 4 cancelada, agora com mais 600 linhas — e o cadastro é ÚNICO,
  porque o que liga os dois apps é o BANCO, como todo o resto da suíte.
  De quebra, a tela original **tinha só uma barreira**: o item da sidebar. Nenhum comando
  chamava `Exigir`, o que contraria a regra do próprio projeto — e nesta tela mais que em
  qualquer outra, porque **quem mexe em permissão pode conceder permissão a si mesmo**. Os
  dois lados ganharam a segunda barreira, e ela **diz por que recusou** em vez de voltar
  calada (a lição da parcela 41).
- **Valor de propriedade que o WPF valida em RUNTIME passa pelas quatro redes** (parcela 50,
  4ª rodada — a tela de Pacientes abriu com "a propriedade
  'DefinitionBase.SharedSizeGroup' iniciou uma exceção", e o cliente mandou a foto).
  `SharedSizeGroup` exige um IDENTIFICADOR — letra ou sublinhado, depois letras, dígitos e
  sublinhado. Escrevi `"PacLinha.Avatar"` porque parece um nome qualificado e todo o resto
  do XAML aceita ponto; o WPF valida no `set` e LANÇA.
  A novidade não é o erro, é ONDE ele passou: **as três redes locais ficaram verdes e o
  compilador de marcação também** — o XAML é bem-formado, a propriedade existe e o tipo é
  string. Até aqui, "só o CI pega" era o pior caso; este é a categoria seguinte, **só a
  tela montada pega**, e o estrago é a tela inteira, não um desalinhamento. A lição
  generaliza: **propriedade cujo valor é validado em runtime precisa de checagem textual**,
  porque não existe compilador que a cubra. Virou a **checagem 27**, autotestada contra o
  nome que quebrou.
- **Estilo de PARÁGRAFO não serve de CÉLULA de tabela** (parcela 50, 3ª rodada — o cliente
  mandou a foto de "6R$ 0,00" e "R$ 0,00R$ 0,00" nos relatórios do Gerente). O `TextoSuave`
  fixa `HorizontalAlignment="Left"` desde a parcela 37, e por um bom motivo — sem ele o
  subtítulo da página nasce flutuando no meio da tela. O efeito colateral é que o TextBlock
  passa a ter a largura do TEXTO, e não a da célula: o `TextAlignment="Right"` escrito ao
  lado **não alinha nada**, porque não sobra espaço dentro do bloco onde alinhar. O número
  desgruda da borda direita da coluna e vai colar no valor da coluna anterior; o
  `Margin="0,4,0,0"` do mesmo estilo ainda o desce 4 px, e a linha sai com as células em
  alturas diferentes.
  Daí o `CelulaSuave`, nos DOIS design systems: mesma cor e mesmo tamanho, sem os três
  ajustes de parágrafo, e com **reticências** (regra de tabela — a coluna tem largura fixa,
  e quebrar em duas linhas desalinharia a altura da linha inteira). A **checagem 26** cobra
  a CONTRADIÇÃO declarada (`TextoSuave` + `TextAlignment` no mesmo elemento), e não
  "TextoSuave em tabela": adivinhar o que é tabela daria falso positivo em legenda
  centralizada sob uma foto — **três** das doze conversões automáticas eram exatamente
  isso, e foram revertidas à mão.
- **O nome do convênio é da OPERADORA; o enum é da REGRA** (parcela 50, 3ª rodada): o
  crachá do paciente escrevia "UnimedIntercambio" e "Personalizado". São dois ângulos do
  mesmo erro — a tela perguntou a FAMÍLIA quando queria a operadora. `{Binding Convenio}`
  faz o WPF chamar `ToString()` no enum (o defeito da parcela 41, que a checagem 20 não
  pega porque só olha `ComboBox`); e `CatalogoConvenios.Nome(p.Convenio)` devolve
  "Personalizado" para **toda** operadora que a clínica cadastrou, porque
  `Convenio.Personalizado` é a regra que todas compartilham — a clínica cadastra "Sul
  América" em Configurações, o nome fica no banco e a tela não o alcança.
  O ponto único é `CatalogoConvenios.Nome(codigo, familia)` (o código vence; a família é o
  caminho de baixo) e, para quem tem a entidade, `Paciente.ConvenioNome`. O par
  código+família teve de ser levado a três records de serviço que só carregavam a família
  — aditivo, `init` com padrão nulo, porque `PendenciaService` é compartilhado com o
  faturamento em produção.
- **Sobreposição posta como IRMÃ desaba a tela inteira** (parcela 50, 2ª rodada — o cliente
  mandou a foto da Conciliação com título, abas e texto desenhados uns por cima dos
  outros). O `EstadoDaTela` (carregando · falhou · vazio) é uma SOBREPOSIÇÃO, e por isso
  precisa de um pai que empilhe os filhos no mesmo lugar — um `Grid` —, sendo o ÚLTIMO
  filho dele (o WPF desenha na ordem do XAML). Ele foi escrito como irmão, dentro de um
  `DockPanel`, com o conteúdo embrulhado num `Grid` intermediário. Daí saem **dois**
  estragos, e o segundo é o que se vê: o `EstadoDaTela` deixa de sobrepor e passa a OCUPAR
  espaço; e todo `DockPanel.Dock` do conteúdo vira **no-op**, porque o pai dele passou a ser
  o `Grid` — então a tela inteira desaba numa célula só.
  A correção é uma troca de papéis entre os dois elementos, não um remendo: `Grid` por fora
  (hospeda a sobreposição), `DockPanel` por dentro (devolve o empilhamento vertical). Eram
  **cinco telas do Financeiro**, todas assim desde a parcela em que o estado vazio foi
  acrescentado. Nenhuma rede pegava: o XML é bem-formado, o `compilar-sombra` não lê o
  corpo do XAML e o compilador de marcação não tem o que reclamar — **o defeito só existe
  na tela montada**. Virou a **checagem 25**, que cobra as duas metades (painel linear
  nunca sobrepõe; dentro de `Grid`, ou é o último filho ou traz `Panel.ZIndex`) e é
  autotestada contra o caso real.
- **Login sem saída faz a auditoria assinar o nome errado** (parcela 50, 2ª rodada): os
  quatro apps da SUÍTE pediam login e não tinham "Trocar usuário" — só o faturamento tinha,
  desde a parcela 45. Parece conforto e não é: é a nona ocorrência do defeito recorrente do
  projeto (capacidade com porta num app só), com o agravante de **desfazer em silêncio a
  razão de o login existir**. No balcão duas pessoas dividem a máquina; sem saída, a segunda
  segue trabalhando com o login da primeira, e a trilha da parcela 21 volta a responder o
  nome errado para "quem fez isso?" — exatamente o que `SessaoUsuario` substituiu quando
  tirou o `Environment.UserName` de lá.
  Trocar de usuário **reabre o app** na suíte pela mesma razão do faturamento: as ViewModels
  leem a permissão quando são CONSTRUÍDAS, metade delas já está viva, e repontar a sessão
  deixaria a tela da colega anterior aberta com os botões dela. Uma diferença: aqui, se a
  reabertura FALHA, o app **não fecha** — derrubar o sistema sem ter conseguido abrir o
  outro deixaria o balcão sem nada.
- **Resposta de banco que chega fora de ordem é "não atualiza"** (parcela 50, dois bugs
  que o cliente achou em produção): a prévia do Novo atendimento mostrava "1 guia · tudo
  hoje" para uma modalidade que gera duas, e não mudava ao trocar qual código o convênio
  libera primeiro. O motor estava certo — quem errava era a TELA.
  Duas causas somadas: (a) toda entrada disparava `_ = PreverAsync()` sem ordenar, e
  trocar de modalidade largava **três** leituras concorrentes no ar (o `Clear()` da combo
  de "qual código primeiro" zera a seleção e devolve `null` pelo binding, depois vem o
  valor novo, depois a da própria modalidade); num banco remoto a mais VELHA podia
  responder por último e sobrescrever. A guarda que existia comparava paciente e
  modalidade, e não pegava duas leituras do MESMO paciente e da MESMA modalidade. Agora é
  **contador de geração** — quem começou primeiro perde —, que é a mesma solução que o
  `SeletorPacienteViewModel` já usava para busca fora de ordem. (b) `MontarCartoes()`
  recria os objetos do zero, e ao trocar de paciente ele corria junto com a prévia: quem
  chegasse por último decidia se a tela tinha número. A última leitura ficou **guardada**
  para a remontagem reaproveitar — e é **jogada fora ao trocar de paciente**, porque
  mostrar o número do paciente anterior é pior do que mostrar "calculando…".
  A lição geral: **toda tela que dispara leitura a cada tecla ou clique precisa de
  descarte de resposta fora de ordem.** Não é otimização; sem ele a tela mente, e mente de
  um jeito que não reproduz na máquina de quem programa (banco local responde em ordem).
- **`TabPanel` ENCOLHE as abas; `WrapPanel` não** (parcela 50): o cliente viu "Convê",
  "Prontu", "Documer", "Relacioname" e "LG" na ficha do paciente, com quase mil pixels de
  largura sobrando. Não era falta de espaço — é o painel padrão do `TabControl` decidindo
  espremer os itens quando julga que a régua não cabe. O `WrapPanel` dá a cada filho a
  largura que ele PEDE e, se um dia não couber, quebra para a linha de baixo: aba na
  segunda linha se lê, "Documer" não.
- **Cartão de altura FIXA precisa de teto no texto** (parcela 50): a mesma altura fixa que
  acerta a base da fileira (parcela 47) corta o conteúdo quando o nome quebra em duas
  linhas — foi o que apareceu no cartão escolhido do Novo atendimento. Altura fixa e
  `MaxHeight` no rótulo andam juntos.
- **Texto que estoura é dado do BANCO sem quebra nem reticências** (parcela 50): o
  cliente reclamou de "muitos textos estourando" no Gerente, e a família é sempre a mesma
  — um `TextBlock` amarrado a `{Binding}` dentro de uma célula de largura fixa. O WPF não
  corta nada por conta própria: o texto sai por cima do vizinho. A regra de decisão é
  curta: **célula de tabela leva `TextTrimming="CharacterEllipsis"`; texto de cartão leva
  `TextWrapping="Wrap"`**. Texto LITERAL o programador mede ao escrever; dado do banco é o
  que tem tamanho imprevisível (nome de 12 ou 60 caracteres, valor de três ou nove
  dígitos), e é por isso que a **checagem 24** só olha o amarrado.
  Dois casos foram resolvidos no ESTILO, que é onde valem para a suíte inteira:
  `TextoSuave` já quebrava desde a parcela 37, e `Rotulo` passou a quebrar agora — ele
  mora acima de campo em coluna de formulário ("Certificado em nuvem (SafeID) — opcional"
  em 290 px) e como cabeçalho de coluna de tabela ("Queda média da dor" em 110 px), e as
  duas estouravam. O **alinhamento não veio junto**: rótulo de tabela às vezes é
  centralizado sobre a coluna, e forçar `Left` desalinharia a régua das tabelas — é a
  meia-regra do `TextoSuave` pelo avesso.
  E a causa RAIZ dos KPIs era o padrão que o `README.md` já proibia: **`UniformGrid` (ou
  coluna estrela) de largura inteira**. Ele divide a largura em partes iguais e obriga o
  valor a caber no que sobrou — num monitor de 1366 px, cinco colunas dão ~250 px, e
  "R$ 1.234.567,89" em 28 px negrito não cabe. Virou `WrapPanel` com piso de largura: cada
  cartão pede o que precisa e a fileira quebra quando não couber.
  A checagem 24 cobre hoje os **SEIS módulos** — a dívida foi zerada de uma vez (338
  ocorrências) e `LIMPOS` os lista todos. O mecanismo de aviso por módulo continua ali
  porque é o caminho de volta: módulo novo entra fora da lista, aparece como aviso com a
  contagem, e passa a ser erro quando alguém o limpar.
  ⚠️ **A lista de estilos que "já resolvem" é LIDA dos dicionários, não escrita à mão.**
  São DOIS design systems (o da suíte e o do faturamento, que não se referenciam — o
  débito permanente da parcela 7), e a lista fixa só conhecia um: os seis `FichaValor` do
  faturamento apareciam como dívida sem serem, porque aquele estilo corta desde sempre.
  ⚠️ **A primeira versão da checagem tinha um ponto cego, e a pergunta do cliente ("você
  verificou TODAS?") foi o que o revelou**: ela só lia a TAG DE ABERTURA, e a suíte monta
  frase com pedaço variável no meio usando `<Run Text="{Binding X}" />` como FILHO —
  "Sai hoje de cada recebimento: R$ 1.234". Nenhum desses aparecia na contagem. Achar o
  ponto cego mudou os números: Financeiro foi de 122 para 130 e o faturamento de 76 para
  81. **Contagem de checagem só vale depois de alguém perguntar o que ela NÃO vê.**
- **O prontuário NÃO SE APAGA, e "alterar" sem guardar o anterior é apagar devagar**
  (parcela 52 — auditoria de fornecedor feita pela própria cliente, com dez pontos por
  escrito; o mapa completo está em `docs/conformidade-lgpd.md`). Dois deles o sistema
  não atendia, e o pior era o que ele fazia ATIVAMENTE: havia `Remove()` de verdade em
  **quatro** caminhos clínicos (evolução — levando os anexos junto —, anexo, avaliação e
  medida), e a evolução era sobrescrita no lugar, com a trilha gravando
  `"EvolucaoAlterada — sessão de 12/03"` enquanto **o texto anterior sumia**. Isso
  contradiz a Lei 13.787/2018 (art. 3º: integridade, autenticidade e rastreabilidade da
  retificação) e inviabiliza a guarda de 20 anos do art. 6º — não há como garantir
  retenção com um botão que destrói o registro.
  O mais revelador é que **a regra já estava escrita neste arquivo** e aplicada em toda
  parte: documento clínico cancela com motivo, NC não some, checagem de enfermagem
  retifica com linha nova, problema descarta com motivo. Ela só não tinha sido aplicada
  no prontuário — o lugar onde mais importa e o único com respaldo legal explícito. A
  lição generaliza: **quando uma regra do projeto vale "em toda parte", procure o lugar
  onde ela é mais óbvia e confira se está lá.** É justamente onde ninguém olha.
  As decisões: (a) **os métodos de exclusão saíram do `IClinicaRepositorio`**, e há teste
  que falha se voltarem — enquanto existirem, alguma tela futura os chama; (b) o
  versionamento é **tabela de versões** (`VersaoEvolucao`), e não o padrão de retificação
  da checagem, porque a evolução é salva várias vezes na MESMA sessão e uma linha nova
  por Salvar faria o prontuário mentir sobre quantas vezes o paciente veio; (c) o motivo
  da correção é **opcional** e o do cancelamento é **obrigatório** — exigir justificativa
  a cada Salvar produziria trinta "ajuste" por dia, que é rastro com aparência de
  controle e nenhum conteúdo, enquanto cancelar sem motivo é apagar com uma etapa a mais;
  (d) o prazo de guarda conta do **ÚLTIMO registro de qualquer natureza**, nunca do
  primeiro, e é `const` e não configuração — é prazo LEGAL, e editável numa tela alguém o
  baixa para 5 anos sem ninguém perceber; (e) **o sistema não elimina nada** ao vencer o
  prazo: o prontuário fica ELEGÍVEL, e a decisão é da clínica com a comissão do art. 7º —
  eliminar sozinho seria ler um PISO de guarda como agendamento de destruição.
- **Controle de acesso responde "quem PODE"; só a trilha responde "quem FEZ"** (parcela
  52): a auditoria da parcela 21 gravava 55 tipos de ação e **todas eram escrita**. A
  cliente pediu três coisas — *"quem acessou, quando acessou e o que realizou"* — e o
  sistema respondia só a terceira: abrir o prontuário de alguém e ler tudo não deixava
  rastro nenhum. E o acesso indevido clássico numa clínica é **LEITURA** (a funcionária
  que abre o prontuário da vizinha, do ex, de um conhecido), que é exatamente o caso que
  a permissão granular da parcela 49 **não** cobre: numa clínica pequena quase todo mundo
  tem permissão legítima sobre quase todo mundo.
  `AcessoProntuarioService` registra quem abriu, quando e **por qual porta** (ficha,
  prontuário clínico, atendimento, documento, exportação) — a porta entra porque "leu o
  telefone no balcão" e "abriu a evolução inteira" são acessos de natureza diferente ao
  mesmo paciente. Três regras: **janela de silêncio de 30 min** (um atendimento entre
  quatro abas não são quatro acessos, e trilha que ninguém consegue ler é trilha que
  ninguém lê), mas **curta de propósito** — quem abre o mesmo prontuário de manhã e à
  tarde fez DOIS acessos, e fundi-los esconderia o padrão que uma investigação procura;
  a comparação de operador dentro da janela é **exata**, porque o filtro casa por trecho
  e "ana" dentro de "mariana" faria o acesso da segunda pessoa desaparecer; e **falhar
  não derruba a tela** (banco lento não pode impedir alguém de ler o prontuário do
  paciente que está na frente), mas também não passa calado — vai para o log, senão a
  clínica acredita estar coberta e não está.
  Na tela, o registro é disparado na **troca de paciente**, nunca em todo `CarregarAsync`:
  as telas recarregam a cada tecla da busca, e a janela de silêncio cobriria a duplicata
  só DEPOIS de ir ao banco perguntar.
- **Ferramenta não é política** (parcela 52): o `BackupService` existia desde a parcela
  34, era bom (base inteira, manifesto conferível, restauração que recusa gravar por
  cima) e era **um botão**. A auditoria pediu *"política de backup, redundância e
  recuperação"* — e a clínica tinha a ferramenta, não a política. Backup que depende de
  alguém lembrar de clicar toda semana existe no manual e não no disco. É primo do
  defeito recorrente do projeto: ali é capacidade sem porta; aqui é **capacidade com
  porta que ninguém atravessa na hora certa**.
  `PoliticaBackupService` põe prazo, destino e **rotação de várias cópias** — guardar só
  a última é o erro clássico, porque a corrupção que ninguém viu na sexta é copiada por
  cima da única cópia boa no sábado. Roda na **abertura do Gerente**, e não num
  agendador: o sistema é desktop, não tem serviço residente, e inventar um daria mais uma
  peça para quebrar em silêncio. É o único lugar desta parcela em que **apagar é certo** —
  cópia velha é redundância, não registro clínico.
  O que o código **não** consegue garantir vai escrito na tela e no documento, não numa
  promessa: nenhum caminho de arquivo diz onde ele fisicamente está, então "grave fora da
  máquina" é orientação à clínica.
- **O QR da receita e a escolha do provedor não podem estar no mesmo caminho crítico**
  (`PublicacaoDocumento`, `ArmazenamentoS3`, parcela 53): a receita assinada é conferida no
  ITI pelo **envio do arquivo**, o que obriga o paciente a mandar o PDF para a farmácia — e
  é onde o balcão trava. Publicado, o farmacêutico escaneia o QR e abre. O motor saiu antes
  do provedor estar escolhido, e a lição é essa: **uma implementação para todo S3-compatível**
  (Magalu, R2, AWS, MinIO), com endpoint em campo de tela. Escolher fornecedor virou
  preencher um campo, não publicar versão.
  O que decidiu usar `AWSSDK.S3` em vez de assinar SigV4 à mão foi o **path-style
  addressing** (`ForcePathStyle`): o padrão do SDK é `bucket.host/objeto` e quase todo
  S3-compatível exige `host/bucket/objeto` — uma linha contra um caso de borda que só
  apareceria no provedor do cliente.
  **DOMÍNIO e ENDPOINT são dois campos porque são duas coisas.** O domínio é o endereço
  público que vai **selado dentro do QR do PDF assinado**; o endpoint é para onde o sistema
  escreve. Trocar de provedor tem de ser mexer no CNAME — se o QR apontasse para o
  endereço do provedor, mudar de fornecedor mataria **toda receita que os pacientes já têm
  na mão**, e elas não podem ser regeradas (a assinatura sela os bytes). É a mesma razão
  pela qual o token é **estável na renovação**.
  A **janela de dias é configurável e o prazo de guarda não**: publicação é política da
  clínica (30 dias para receita simples, 180 para uso contínuo), guarda é prazo LEGAL — daí
  `GuardaProntuario.AnosDeGuarda` continuar `const`. Configuração corrompida cai no padrão,
  porque valor inválido não pode deixar dado de saúde no ar para sempre.
  O **"Testar conexão" grava e apaga de verdade**, em vez de listar o balde: um teste que
  só lê passaria com credencial de leitura, com balde inexistente e — o caso que importa —
  com provedor que recusa a ACL de leitura pública, que é exatamente o que a publicação
  usa. Teste que não exercita o mesmo caminho atesta uma coisa e a receita falha por outra.
  E gravar-sem-apagar tem **frase própria**: a publicação funcionaria e a expiração não.
- **Credencial de serviço externo mora no BANCO, com o ambiente podendo sobrepor**
  (parcela 53 — correção de um comentário meu da 52). Eu havia escrito que as credenciais
  do armazenamento iriam por variável de ambiente, "porque segredo em tabela de
  configuração é segredo que sai no backup". Errado por duas vias, e as duas valem para a
  próxima integração:
  (a) **contradiz o que o projeto já decidiu**, com o motivo escrito no
  `ProvedorOpcoesSafeID` — variável de ambiente é **ritual de instalação**, e uma clínica
  não abre o Prompt de Comando em cada máquina. Aqui seria pior que no SafeID: quem assina
  documento é o Consultório **e** a Recepção, então a publicação funcionaria onde alguém
  digitou e falharia **calada** nas outras;
  (b) **descrevia como seguro um padrão que o sistema não segue** — o `client_secret` do
  SafeID já está gravado em claro nessa mesma tabela. Resolver o segredo-no-backup de
  raspão, numa integração nova, só o esconderia: ele é problema real, separado, e continua
  em aberto.
  A regra que fica: **quando for justificar uma decisão pelo "é mais seguro", confira se o
  resto do sistema faz assim.** Se não faz, ou você está corrigindo o sistema inteiro — e
  então corrija — ou está inventando uma exceção que ninguém vai manter.
- **A suíte chamava o snackbar 143 vezes e nunca teve onde mostrá-lo** (parcela 53 — o
  cliente clicou em "Testar conexão" e o sistema não respondeu nada). O teste tinha
  **funcionado**; o que não existia era a mensagem. O `SnackbarService` é registrado no
  `ShellBootstrap`, injetado em quase toda ViewModel da suíte e chamado 143 vezes — e o
  `ShellWindow.xaml` **nunca renderizou o host**. Ele só existe no `MainWindow` do
  FATURAMENTO, que é de onde o componente veio. Todo "salvo com sucesso" da Recepção, do
  Financeiro, do Gerente e do Consultório caiu no vazio desde que a suíte nasceu.
  É o defeito recorrente do projeto numa variante nova: não é dado gravado sem leitor nem
  capacidade sem porta — é **SAÍDA SEM TELA**. E é o mais discreto de todos porque **nada
  falha**: o serviço marshala pelo Dispatcher, atualiza o próprio estado, e ninguém observa
  esse estado. Build verde, 1374 testes verdes, e a única forma de notar é clicar em Salvar
  e reparar que a tela não respondeu. **Ao portar um componente entre os dois design
  systems, porte o HOST junto do serviço** — metade de um par não avisa que está sozinha.
- **"Consigo escrever?" e "dá para LER pela URL?" são testes diferentes** (parcela 53): o
  `TestarConexaoAsync` grava e apaga — prova credencial, balde e ACL. Ele **não** prova que
  o objeto abre pelo endereço público, e essa é a falha que chega ao balcão: o PUT passa, a
  ACL é aceita, a URL pública do balde está desligada, e o farmacêutico leva 403 com a
  receita na mão. Daí o `EnviarExemploAsync`, que sobe **um** PDF e **não apaga**,
  devolvendo o endereço para abrir no navegador. É PDF de verdade e não `.txt` porque
  metade do que se prova é que o celular **abre** em vez de baixar; usa token real para o
  endereço ter a forma exata do de uma receita; e o conteúdo é fixo, sem paciente — publicar
  documento real para conferir infraestrutura seria expor dado de saúde para testar
  endereço.
  **Validado ao vivo contra o Cloudflare R2** (ago/2026): gravação, ACL de leitura pública,
  exclusão e abertura do PDF pela URL. E a assinatura **também foi provada na clínica com
  e-CPF real pelo SafeID** (ago/2026) — ver a lição da prova de campo mais abaixo.
- **⛔ ANTES DE ESCREVER QUALQUER XAML, LEIA A REGRA DE LEIAUTE NO `README.md`** (topo do
  arquivo, seção "A REGRA DE LEIAUTE"). Ela é a consolidação de **seis** reprovações do
  cliente, todas pelo mesmo defeito: **tela picada em várias caixas empilhadas**. As três
  perguntas que decidem: (1) *isto é o que a pessoa VÊ nesta tela, ou o que ela FAZ de vez
  em quando?* — o segundo caso é botão/janela, nunca painel aberto; (2) *esta seção existe
  sem um item escolhido?* — se não, é aba, não item de menu; (3) *quantas perguntas esta
  tela responde?* — mais de uma, mais de uma tela. Na dúvida entre outra caixa e outra
  tela, **é outra tela**, e um botão que leva até ela.
- **A 5ª reprovação: faixas empilhadas comem a tela** (parcela 38, 2ª rodada). O "Meu dia"
  saiu com QUATRO faixas antes do quadro — o slab azul de "Chamar próximo" (que gastava
  70 px de largura inteira para escrever *"ninguém aguardando no balcão"*), o alerta de
  vínculo, a linha de resumo e a caixa de pendências com `MaxHeight="180"` cortando um nome
  de paciente ao meio. O quadro do dia começava na metade da tela, e num dia já terminado
  as quatro raias vazias viravam um buraco de mil pixels ao lado da única com conteúdo.
  As correções, todas generalizáveis: **contexto permanente é LINHA de texto, não faixa**
  (faixa permanente vira moldura); **ação é BOTÃO, não painel** — e um botão desabilitado
  já diz "não há ninguém" sem gastar a tela para dizê-lo, além de caber o nome de quem vai
  ser chamado, que a faixa não dizia; **lista longa merece TELA PRÓPRIA** com a largura
  inteira, e no lugar dela fica um botão com a contagem (foi assim que a dívida de
  prontuário virou `ChaveRegistrosPendentes`); **o número mora junto do que ele conta** —
  o resumo repetia as cinco contagens em sequência e obrigava o olho a casar cada uma com
  a sua coluna pela ordem, então elas foram para `CabecalhoRaia`; e **coluna vazia mostra
  um traço**, porque quatro vãos brancos sem marca nenhuma fazem o quadro deixar de se ler
  como quadro. As colunas do kanban deixaram de ser `Card` com borda e viraram **raias** —
  faixa de fundo levíssima, sem moldura, separadas por espaço: cinco cartões emoldurados
  lado a lado leem-se como cinco telas costuradas, que é literalmente a reclamação.
- **Cada retângulo a mais é uma costura a mais** (parcela 37, 3ª rodada): embrulhar todo
  bloco num `Card` com borda produz uma colcha de retalhos — a tela fica com aparência de
  esboço mesmo com o conteúdo certo. Uma superfície por REGIÃO, e a separação interna por
  espaço, rótulo e `Separator`. Três correções que vieram junto e valem para qualquer tela:
  **KPI não se espalha** (cinco números num `UniformGrid` de largura inteira viram cinco
  traços perdidos com meio metro de branco entre eles — em `StackPanel` horizontal à
  esquerda eles voltam a se ler como um conjunto); **gráfico sem dado SOME** em vez de
  desenhar 200 px de área vazia dizendo com o desenho o que a frase acima dele já diz com
  palavras; e **um estado vazio por pergunta** — a tela de Avaliações chegou a ter três
  respostas para "não há escala aplicada" (a frase, a área do gráfico e o `EstadoDaTela`
  da lista).
- **`TextoSuave` alinha à ESQUERDA, e isso anda junto do `MaxWidth`** (parcela 37): sem o
  `HorizontalAlignment`, o TextBlock recebe a fatia inteira do painel, o teto de 820 o
  encolhe e o WPF **centraliza** o que sobrou — num monitor de 1920 o subtítulo de toda
  página da suíte nascia flutuando no meio da tela, com o título alinhado à esquerda logo
  acima. Parecia leiaute quebrado porque era. Teto sem alinhamento é meia regra.
- **O mapa corporal e a emissão de documento moram no SHELL** (parcela 36): os dois
  nasceram dentro do módulo da Recepção, e o Consultório precisa dos mesmos — a acupuntura
  é a especialidade da casa, e quem prescreve é quem atende. Como **nenhum módulo conhece
  os outros**, as alternativas eram copiar (duas silhuetas divergindo na primeira
  correção) ou deixar o app do médico sem a ferramenta central dele. Subiram para
  `Clinica.Desktop.Shell/Componentes`, pelo mesmo argumento que já pôs o
  `SeletorPacienteViewModel` lá: **tela nova da suíte que marca ponto no corpo ou emite
  documento usa ESTES componentes; não reescreva.** O bloco foi movido INTEIRO, e não
  reescrito, para nenhuma função se perder na mudança — e, de quebra,
  `MapaCorporalViewModel.Observacoes`, gravado sempre nulo desde a parcela 3 porque
  nenhuma tela o mostrava, finalmente ganhou a caixa de texto.
- **Escalas clínicas por especialidade** (`Domain/Avaliacoes/`, parcela 36): a EVA responde
  "quanto dói" e serve à acupuntura e à clínica da dor. As outras quatro especialidades da
  casa — psiquiatria, geriatria, neurocirurgia e endocrinologia — **não tinham número
  nenhum**: escreviam "refere melhora do humor" na evolução, o que é verdade e não compara
  consulta com consulta, não desenha curva e não vai para o relatório. Cinco instrumentos
  (PHQ-9, GAD-7, Oswestry, Katz, FINDRISC) moram em CÓDIGO e não em tabela, pelo mesmo
  desenho do motor de regras de convênio: a pontuação de uma escala publicada não é
  configuração da clínica — deixar editar o peso de um item produziria um escore que
  continua se chamando PHQ-9 sem ser um. As regras:
  **tudo o que descreve o instrumento é COPIADO na aplicação** (nome, enunciado, rótulo da
  alternativa, faixa, interpretação), como o protocolo do mapa corporal e o preço por
  convênio — é o que permite corrigir uma redação sem reescrever o que o paciente respondeu
  no mês passado, e o que mantém legível a avaliação de uma escala já retirada do catálogo.
  **Escala incompleta é recusada** (item em branco vira zero numa soma, e zero é "não tenho
  esse sintoma"), salvo no Oswestry, cuja regra publicada manda calcular o percentual sobre
  as seções respondidas. **Peso fora das alternativas é recusado.** O **alerta de item não
  se dissolve no total** (`IInstrumentoAvaliacao.AlertaDeItem`): o item 9 do PHQ-9 vale 3
  dos 27 pontos, e um paciente pode marcá-lo zerando o resto — escore 3, faixa "sintomas
  mínimos", e a única resposta que exigia conduta imediata desapareceria dentro da média.
  O **Katz inverte** (`MelhorQuandoMenor = false`): 6 é o melhor resultado possível, e sem
  isso a curva chamaria a recuperação funcional de piora. **O sistema pontua e registra; ele
  não diagnostica** — a faixa é a leitura publicada da escala, e a tela diz isso o tempo
  todo. **Neurocirurgia NÃO entrou no enum `Especialidade`** — ela é um código do
  CATÁLOGO (`InstrumentoOswestry.CodigoNeurocirurgia`), e a diferença não é estilo: o
  enum é lido pelo faturamento CONGELADO, que na abertura chama
  `EspecialidadeCatalogoService.RecarregarCacheAsync` e garante as embutidas percorrendo
  `Enum.GetValues`. Um valor a mais ali viraria, sozinho, uma opção nova no seletor do
  lançamento de atendimento de um app em produção. A clínica cadastra a especialidade em
  Configurações quando quiser; até lá o Oswestry serve à acupuntura e à clínica da dor.
- **Como os módulos se falam** (parcela 27): eles compartilham o BANCO, não
  mensagens — não há fila, evento nem sincronização; o que um grava o outro lê, e a
  ligação é sempre uma CHAVE ESTRANGEIRA. O circuito completo:
  **Recepção → Faturamento**: `FechamentoSessaoService` → `AtendimentoService.LancarAsync`
  cria `Atendimento` + `CodigoFaturamento` pelas regras do convênio; o app congelado lê a
  mesma base. Volta pelo `PainelRecepcaoService` (guias pendentes só dos pacientes de
  HOJE) e pela NC que reabre quando o paciente volta.
  **Faturamento → Financeiro**: `FinanceiroService.GuiasSemLancamentoAsync` (guia baixada
  sem receita) alimenta a Conciliação; `LancarReceitaDaGuiaAsync` grava
  `LancamentoFinanceiro.CodigoFaturamentoId`, e é esse campo que fecha o elo — a guia sai
  da lista porque passou a ter receita, não porque alguém a marcou.
  **Financeiro → Gerente**: `RentabilidadeConvenioService`, `CustoTransacaoService` e o
  `PainelDirecaoService`, que não calcula nada — cada número vem do serviço dono dele.
  **Gerente → todos**: cada alerta do painel LEVA à tela dona, por `NavegacaoSuite` +
  `ChavesSuite`. A **dependência tem um sentido só**: o faturamento continua funcionando
  sem saber que o financeiro existe, e é por isso que as pontes (`FechamentoSessaoService`,
  `ReceitaGlosadaService`) moram FORA dos serviços compartilhados — dar efeito colateral
  novo ao `AtendimentoService` mudaria o comportamento de um app em produção.
- **A glosa que voltava para ninguém** (`ReceitaGlosadaService`, parcela 27): o elo que
  faltava era o de VOLTA. `GlosaService` mexia só no `CodigoFaturamento` e a palavra
  "Glosa" não aparecia em um único arquivo do módulo Financeiro: o convênio recusava a
  guia e o dinheiro **continuava no fluxo de caixa, no previsto e na rentabilidade**.
  **Receita fantasma é a pior espécie de número errado, porque tem cara de número exato.**
  As regras: a glosa **não apaga o lançamento** (fato datado — sai do total, não do
  histórico); **só o PREVISTO se cancela**, porque o REALIZADO é dinheiro que entrou na
  conta e cancelá-lo faria o caixa parar de bater com o extrato (estorno da operadora é
  uma SAÍDA, com a data dele); e a guia **volta sozinha para a conciliação** — cancelado o
  vínculo ela deixa de ter receita ativa e reaparece em `GuiasSemLancamentoAsync`, que é o
  caminho de volta funcionando sem uma linha de código para isso. Na conciliação a guia
  glosada **aparece marcada, nunca sumindo** (mesma regra da central de documentos), e
  lançar receita dela pede confirmação em vez de ser proibido: a clínica pode estar certa
  de que recupera no recurso — ela só precisava saber.
- **O que os outros módulos mandam ao balcão** (parcela 27): `ElegibilidadeService` alargou
  o contrato de propósito, e o critério é um só — **entra ali o que se resolve com o
  paciente presente e fica caro depois**. Por isso chegaram a **conta vencida**
  (Financeiro, via `InadimplenciaService`) e a **guia glosada em aberto** (Faturamento):
  as duas estavam gravadas havia parcelas e só eram lidas por quem não atende ninguém. O
  atraso só alarma a partir de `AtrasoMinimoParaAvisarDias` (5) porque a conta de ontem
  costuma estar em trânsito, e **alerta que dispara para todo mundo é alerta que ninguém
  lê**; a dívida é sempre **amarela** — vermelho neste serviço significa "a guia vai ser
  recusada", e dívida é assunto de conversa, não impedimento de atender.
- **A consulta a renovar chega ao horário** (`ConsultaService.SituacaoDeAsync` /
  `DoPacienteAsync`): a consulta renovável era lida em exatamente DOIS lugares — a aba
  Consultas e o painel de pendências —, e nenhum deles é onde a secretária está quando
  marca ou recebe o paciente. Oitava ocorrência do defeito recorrente do projeto, na
  variante "leitor existe, mas não onde a decisão acontece": renovar com a pessoa presente
  é uma assinatura, e descobrir a consulta vencida na hora de faturar é ligar para quem já
  foi embora com a guia recusada na mão. Agora aparece na **agenda** (selo no cartão +
  linha de contexto no cabeçalho), no **novo agendamento**, no **novo atendimento** e —
  pelo `ElegibilidadeService` — na Fila, na ficha e no Consultório da suíte.
  As regras: a agenda usa **`ARenovar`, não `PrecisaRenovar`** — quem NUNCA emitiu
  consulta fica de fora, porque o selo acenderia para toda a base de convênio no primeiro
  dia e alerta que dispara para todo mundo é alerta que ninguém lê (é a mesma escolha que
  `PendenciaService.ConsultasAVencerAsync` já fazia, e a aba Consultas continua mostrando
  a linha, que ali é o assunto da tela); a frase e o selo moram no **modelo**
  (`StatusConsultaPaciente.AvisoRenovacao`/`SeloRenovacao`), para o faturamento e a suíte
  não escreverem duas versões da mesma cobrança; a regra de "precisa renovar" tem **um só
  `Avaliar`**, senão a agenda passaria a discordar da aba Consultas sobre o mesmo paciente;
  a referência é a **data marcada, não hoje** (marcar para daqui a três semanas com uma
  consulta que vence em cinco dias é combinar a renovação de antemão); a leitura é **em
  lote pelos pacientes da grade** (`ConsultasDosPacientesAsync`), porque varrer a base
  inteira para responder sobre as vinte pessoas de hoje é caro num banco remoto e a agenda
  recarrega a cada navegação de dia; e **cancelado e falta não recebem selo** — não há
  ninguém para assinar nada. Falha da conferência vira **terceiro estado escrito** ("não
  foi possível conferir"), nunca agenda limpa.
- **A Recepção no balcão** (parcela 26): seis buracos do módulo que fatura o dia.
  **Elegibilidade ANTES** (`ElegibilidadeService` no check-in da Fila e no agendamento):
  carteirinha vencida e cota estourada só apareciam na hora de faturar, quando a sessão
  já aconteceu — é aviso, nunca impedimento, mas marcar dez sessões para quem está com a
  cota estourada é combinar dez glosas de antemão. A **rodada de confirmação** ganhou porta
  na Recepção (`ConfirmacoesWindow`): quem liga para o paciente é o balcão, e a campanha
  morava só no Gerente. **Bloqueio de agenda** (`BloqueioAgendaService`): férias, feriado e
  folga entram como fato que a agenda respeita — bloquear **não desmarca ninguém** (devolve
  quem já estava marcado, para a recepção remarcar; sessão que some sem avisar o paciente
  é pior que o choque), o **encaixe continua furando**, e bloqueio sem profissional e sem
  sala é da clínica inteira. **Agendamento em série** (`AgendarSerieAsync`): o pacote de
  dez marcado de uma vez, com a data saindo sempre da **primeira mais N períodos** — nunca
  da anterior mais um, senão uma sessão adiada empurraria todas as seguintes e o paciente
  perderia o horário fixo, que é o motivo de marcar em série; data com choque é **PULADA e
  dita**, e a janela fica aberta para a recepção resolver com o paciente ainda na frente
  dela. **Visão de semana** na agenda (o dia continua sendo o padrão): a semana começa na
  segunda e é para responder "quando cabe", que o dia não responde. E os **direitos do
  titular** (`TitularDadosService`): ver o item de LGPD abaixo.
- **LGPD além do consentimento** (`TitularDadosService`, parcela 26): colher e revogar
  consentimento a clínica sabia desde a parcela 2; faltavam os outros dois pedidos que o
  paciente pode fazer e que ela é **obrigada** a atender — acesso (art. 18, II) e eliminação
  (art. 18, VI). A regra que o produto não pode fingir que não existe: **prontuário não se
  apaga**. A guarda é obrigação legal do profissional de saúde (CFM 1.821/2007) e a própria
  LGPD a preserva (art. 16, II) — então o que se faz é **anonimizar**: nome, documento,
  telefone, carteirinha, nascimento e foto saem, o histórico fica sem dono identificável, e
  a tela **diz isso antes de fazer** (prometer apagar tudo e manter o prontuário seria mentir
  para o paciente por escrito). A exportação sai em **texto, não em PDF diagramado**: o
  direito é de receber dados legíveis e reutilizáveis, mesma razão do CSV dos relatórios. O
  **nome original fica na auditoria** — é o que liga o pedido ao registro, e a trilha é o
  único lugar onde esse vínculo pode existir sem expor o dado na operação do dia a dia.
  `Permissao.AnonimizarDados` nasce **separada de `EditarProntuario`**: evolução escrita se
  corrige, anonimização não tem volta — o balcão exporta (`VerProntuario`), a direção elimina.
- **Recibo e orçamento** (`DocumentoFinanceiroService`, parcela 4): mesmas regras do
  documento clínico — numerados por ano e por tipo (`REC 2026/0001`), não se apagam,
  cancelam-se com motivo, e os **valores ficam gravados na emissão**. O recibo aponta
  para o `LancamentoFinanceiro` que comprova; ele não substitui o caixa.
- **Foto do paciente**: capturada pela webcam da recepção (`Desktop/Servicos/CameraServico.cs`,
  DirectShow via AForge; `Retrato.cs` recorta em quadrado e gera os dois JPEGs). O armazenamento é
  deliberadamente partido em dois: `Paciente.FotoMiniatura` (~160px, na própria linha, alimenta os
  avatares da lista) e a tabela `PacientesFotos` (~640px, carregada só sob demanda) — assim a busca
  de pacientes não arrasta imagem. A gravação acontece no Salvar do cadastro, nunca na captura.
- Ações que alteram faturamento (baixa, estorno, glosa, lote) devem gravar um `EventoAuditoria`
  via `IClinicaRepositorio.RegistrarAuditoriaAsync` no MESMO SaveChanges da ação (atômico).
  O mesmo vale para ação administrativa de acesso (criar usuário, trocar senha, mudar permissão):
  permissão que muda sem rastro é pior do que não ter permissão.
- **Acesso (parcela 5)**: `UsuarioSistema` aponta para o `Profissional` em vez de duplicá-lo; a
  senha é PBKDF2 com sal por usuário (`Clinica.Domain.HashSenha`) e nunca é gravada em claro. A
  permissão efetiva é resolvida na LEITURA — `padrão do perfil + extras − negadas` —, então
  corrigir o padrão de um perfil alcança quem já está cadastrado; negada vence extra. Base sem
  usuário abre o **primeiro acesso** (nasce Gerente) em vez de trancar todo mundo do lado de fora,
  e o serviço recusa deixar a base sem ninguém que possa gerenciar acessos. **Os cinco apps pedem
  login** — o faturamento entrou na parcela 45 (ver a regra do formato da guia e das permissões
  granulares mais abaixo).
- **A permissão tem DUAS barreiras, e as duas são obrigatórias**: `IsEnabled` no botão (a metade
  visível, que explica) e `SessaoUsuario.Atual.Exigir(...)` no comando (a que impede). Só
  desabilitar é enfeite — atalho de teclado passa direto. Comando novo que grava nasce com as
  duas. `SessaoUsuario.Atual` é estática porque metade dos formulários é construída à mão pela
  tela e não passa pelo DI; **sem sessão autenticada, `Pode` libera** (tela vazia parece defeito,
  e no app real o login é obrigatório).
- **Quem assina a ação é quem fez login**, nunca o usuário do Windows: `SessaoUsuario.Atual.Operador`
  é o que vai para a auditoria e para o `EnviadoPor` da campanha. `Environment.UserName` só
  aparece como fallback dentro do próprio `Operador` — no balcão duas pessoas dividem a mesma
  máquina, e gravar o login do Windows apagaria a diferença entre elas.
- **Campanhas (parcela 5)**: confirmação de sessão, NPS e recall são UMA entidade
  (`ContatoCampanha`), porque o fato registrado é o mesmo. `Origem` (`AGD:123`, `ATD:987`,
  `REC:55:2026-07`) é a chave de idempotência, com índice único junto do tipo — rodar a campanha
  duas vezes não pode mandar a mesma mensagem duas vezes. **Confirmar a própria sessão é
  transacional** e não exige consentimento; **NPS e recall exigem** `ComunicacaoEMarketing`
  vigente, e quem não consentiu aparece CONTADO no resultado da rodada em vez de sumir. O envio é
  um clique por paciente de propósito: o número é o WhatsApp da clínica.
- **Indicadores (parcela 5)**: ocupação é medida contra os dias em que o profissional TEVE agenda
  × jornada configurável (`ParametrosService.ChaveJornadaDiariaMinutos`, padrão 480 min) — a
  clínica não cadastra jornada, e inventar dias úteis daria número mais bonito e menos verdadeiro.
  Cancelamento avisado não conta como falta. Toda métrica sem base de cálculo devolve `null` e a
  tela mostra "—": 0% e "não medido" são coisas diferentes.
- Concorrência otimista via `xmin` (só no Npgsql — testes rodam em SQLite e ficam de fora);
  `ClinicaRepositorio.SalvarAsync` traduz `DbUpdateConcurrencyException` em mensagem amigável.
- XML TISS gerado passa por `TissValidador.Validar` (estrutura + hash do epílogo); XSD oficial
  é opcional (pasta `%APPDATA%\ClinicaFaturamento\tiss\schemas`).
- `PrevencaoGlosaService` (radar de glosas) roda na exportação do lote: carteirinha vencida,
  duplicidade e taxa histórica por padrão (convênio+tipo). `TissRetornoImport.Ler` importa o
  demonstrativo XML da operadora e pré-preenche as decisões do retorno (casadas pelo nº real
  da guia); a leitura é tolerante ao nome local dos elementos (varia entre operadoras).
- **O lote TISS é POR OPERADORA, e a guarda de duplicidade era o que tornava o erro
  irreversível** (parcela 60 — revisão completa): o "lote do período" engolia as guias de
  TODAS as operadoras num XML endereçado a UMA (o registro ANS era um campo global), e as
  demais **nunca mais entravam em lote** — `LoteTissId` preenchido as escondia das
  candidatas para sempre, e a tela dizia "nenhuma guia nova", que se lê como dia fraco.
  Agora a exportação agrupa por operadora e gera **um lote e um XML por grupo** (uma
  operadora só = o fluxo de sempre, sem cerimônia); o registro ANS mora no
  `ConvenioCadastro` (dado, não código — em branco cai no global); `LoteTiss.ConvenioCodigo`
  grava de quem o lote é; e o **estorno da baixa solta o `LoteTissId`** — guia rebaixada
  com o número certo volta às candidatas, com o lote antigo registrado na observação. O
  agrupamento é pelo `ConvenioDaGuia` (código do catálogo, família como caminho de baixo),
  o MESMO do nome de exibição — outro critério fundiria operadoras que só compartilham a
  regra.
- **A cópia que ficou para trás é onde a permissão vaza** (parcela 60): a revisão achou
  cinco brechas críticas de permissão, TODAS no app de faturamento — sempre o mesmo
  formato: o módulo equivalente da suíte tinha as duas barreiras e a cópia do
  `Clinica.Desktop` não (estorno pela ficha, agenda, cadastro de paciente, autorizações,
  usuário). Os bits envolvidos eram exatamente os que as parcelas 49/58 tiraram dos perfis
  — cada tela esquecida era um caminho de volta. **Ao aplicar permissão nova, procure a
  MESMA ação nos dois lados** (suíte e faturamento); e a rodada fixou também: emitir na
  janela de documento confere o bit do tipo QUE ESTÁ NO COMBO (`AcessoParaEmitir`), não o
  da porta por onde se entrou — trocar o seletor para "Receita" exigia `Prescrever` só no
  papel.
- **Excluir paciente era a última porta que apagava prontuário** (parcela 60): as FKs
  clínicas apagam em CASCATA com a linha do paciente, e o botão Excluir do faturamento
  levava evolução, avaliação, medida, documento e prescrição juntos — com o
  `ConformidadeProntuarioTests` verde, porque ele só olhava os `Remover*Async` da
  interface. `PacienteService.RemoverAsync` agora RECUSA quando há registro clínico
  (explica a guarda de 20 anos e aponta a anonimização); ficha vazia continua removível. A
  lição de teste: **fixar a regra pela lista de métodos proibidos não cobre o método que
  apaga por arrasto** — o teste novo exercita a cascata de verdade.
- **O descarte de resposta fora de ordem virou padrão de TODA carga async** (parcela 60):
  a regra da parcela 50 valia em DUAS telas; a revisão achou mais de quarenta com o mesmo
  defeito, seis delas críticas (deduções GRAVADAS do valor antigo; CPF gravado no
  profissional errado; prontuário de um paciente sob o nome de outro). O padrão é um só —
  contador `_geracaoCarga`, guarda após cada await, catch e `Carregando` condicionais — e
  agora está em todos os ViewModels que leem banco por tecla, clique ou timer. Tela nova
  com carga async NASCE com o contador; guarda de reentrância (`if (Carregando) return`)
  no método de carga é incompatível com ele (descarta a carga NOVA) e foi removida onde
  conflitava.
- **A sala executa o que não alcançava imprimir** (parcela 61 — auditoria de prontidão do
  módulo clínico antes de produção; achou onze lacunas, todas do defeito recorrente). Todo
  o desenho da checagem da parcela 42 assume a via IMPRESSA (a enfermeira assina à caneta),
  e a única porta de impressão morava na tela de quem PRESCREVE, num exe que a máquina da
  enfermagem não instala. A impressão entrou na sala e na folha de execução; a folha também
  se acha pelo **código impresso** (`PorCodigoAsync`, que estava sem chamador) — a técnica
  está com o papel na mão, e a lista só mostra hoje. **Suspender item** ganhou porta na
  folha de execução sob o bit `Prescrever` (é ato de quem prescreve, não de quem executa —
  o botão fica fora do painel de `PodeMexer` de propósito). O contador de folhas aguardando
  (`PendentesDoDiaAsync`, também sem chamador) virou alerta do painel da direção
  (`AssuntoDirecao.InfusaoAguardando`), pelo argumento do prontuário em aberto: a sala vê a
  própria fila, a direção é quem vê a soma. E o `DoDiaAsync` duplicado do
  `PrescricaoInternaService` foi REMOVIDO: duas definições de "a fila da sala" divergem na
  primeira correção, e a cópia sem chamador é a que ninguém lembraria de ajustar.
- **Mover a fila é UM ato com UMA regra nos dois quadros** (`Permissao.MovimentarFila`,
  parcela 61): a Recepção exigia `EditarAgenda` para carimbar chegada/chamada/entrada, e o
  quadro do médico aceitava `VerAgenda` — a MESMA escrita na MESMA tabela com duas regras,
  e a de baixo deixava quem só lê a agenda carimbar chamada. O bit novo é o corte estreito
  de quem atende (o perfil `Profissional` o recebe por padrão, SEM ganhar `EditarAgenda` —
  marcar horário de terceiros continua sendo do balcão), e a autorização do ato é
  `EditarAgenda` OU `MovimentarFila` nos dois lados, via `SessaoUsuario.PodeAlgum` /
  `ExigirAlgum` (novos — `Pode` com bits combinados é um E, não um OU): quem movia a fila
  ontem pelo balcão continua movendo hoje. O Meu dia ganhou as transições que faltavam
  (Entrou / Voltar — a coluna EM ATENDIMENTO só enchia se o BALCÃO clicasse) e o arrasto da
  parcela 58; **Finalizar continua não existindo lá, e é decisão**: concluir são quatro
  fatos do mesmo ato (guia, pacote, insumo, caixa) e três são do balcão.
- **`BooleanToVisibilityConverter` sobre STRING é `Collapsed` para sempre** (parcela 61 —
  a mensagem da folha de execução, a justificativa da rodela e o nome do executante nunca
  apareceram desde a parcela 42). O conversor do WPF devolve `Collapsed` para qualquer
  valor que não seja `bool`: não é erro de compilação nem de binding — o elemento só nunca
  aparece, e as três redes ficam verdes. O faturamento tem `TextoParaVisibilidadeConverter`
  desde sempre; o shell não tinha e as telas ligavam `{Binding Mensagem}` no conversor de
  bool. Entrou `Clinica.Desktop.Controls.TextoParaVisibilidade` no shell. **Ao ligar
  `Visibility` a uma propriedade, confira o TIPO dela** — string pede o conversor de texto.
- **A trilha de leitura cobre a PORTA que abre o dado, não só a tela clássica** (parcela
  60): Prescrições, Prescrição de infusão, Anexos e a Folha de execução expunham dado de
  saúde sem registrar acesso — e `OrigemAcessoProntuario.Documento` existia no enum sem um
  único escritor. Todas registram agora (na TROCA de paciente ou uma vez por janela, nunca
  a cada `CarregarAsync`), o export CSV ganhou `Exigir(VerProntuario)` + origem própria
  (`ExportacaoClinica` — dado de saúde saindo para arquivo é o que uma investigação
  procura), e `AcessoProntuarioService.DoPacienteAsync` — a resposta a "quem abriu este
  prontuário", sem chamador desde a parcela 52 — ganhou porta na tela de Guarda do Gerente.
  As abas do workspace continuam cobertas pela janela de silêncio por ORIGEM: quatro abas
  do mesmo paciente no mesmo atendimento são UM acesso, e isso é desenho, não buraco.
- **O painel de pendências diz DE QUÊ elas são** (parcela 61 — a direção perguntou:
  *"12 pendências, mas 12 pendências de quê? 1º código? 2º? Acupuntura? Consulta?"*).
  A resposta tem duas formas, e a escolha entre elas não é estilo: **CHIP com contagem**
  para as perguntas de DUAS respostas que o faturista alterna o dia inteiro (1º/2º
  código, atrasada/no prazo — estilo `ChipFiltro`, pílula com a distribuição visível
  ANTES do clique, contada sempre sobre o TOTAL e nunca sobre o recorte); **combo
  rotulado** para as listas longas (tipo do código, modalidade, especialidade,
  convênio). Cada dupla de chips é exclusiva (marcar um desmarca o irmão; desmarcar os
  dois é "todas"). Para isso `PendenciaCodigo` ganhou `Modalidade` e `Especialidade` —
  aditivo, `init` nulo, o padrão de sempre para record compartilhado com produção — e a
  especialidade cai da do CÓDIGO para a do atendimento, o mesmo caminho de baixo da
  consulta de guias (parcela 45). O resumo escreve o recorte por extenso ("12 de 30 —
  2º código · acupuntura"), o Limpar só aparece quando há o que limpar, e o vazio
  distingue "não há pendência" de "nenhuma bate com o filtro" — um filtro esquecido
  respondendo "tudo em dia" faria a clínica dar o dia por resolvido com o painel inteiro
  pendente.
- **O circuito clínico é testado de ponta a ponta** (`CircuitoClinicoTests`, parcela 61):
  o `CircuitoCompletoTests` cobria agenda → faturamento → financeiro e nenhum elo clínico.
  Os quatro circuitos fixados: prescrição assinada → sala → checagem → encerramento (a
  folha SOME da fila e fica na conferência), sala → painel da direção (com a asserção de
  que o bloco não caiu em `NaoVerificados` — elo partido aqui vira ZERO com cara de dia
  tranquilo), reação na sala → alergia → PRÓXIMA prescrição recusada na assinatura, e
  acesso registrado → "quem abriu este prontuário". A chave da sala de infusão — a única
  publicada por DOIS módulos — saiu das duas strings à mão e virou `ChavesSuite.SalaInfusao`.

- **Nenhum defeito da Recepção QUEBRAVA nada — e é por isso que eles chegaram até aqui**
  (parcela 62, auditoria de prontidão do balcão antes de produção). Dez grupos, e o que os
  une é a ausência de sintoma: build verde, 1441 testes verdes, três redes locais verdes, e
  quem descobre é a recepcionista com o paciente na frente. São as variantes do defeito
  recorrente do projeto que nenhuma ferramenta alcança.
  **A porta que não abre, a venda que o dono da tela não pode fazer.** A Recepção publicava
  o item "Pacotes" e o `CriarTela` dela **não tinha o case** — o menu acendia e nada
  acontecia. Nenhuma rede viu porque a chave era uma string à mão dos dois lados; virou
  `ChavesSuite.Pacotes`, como a sala de infusão na 61. E a tela, que subiu para o shell
  vinda do Financeiro, perguntava por `EditarFinanceiro` em toda ação — bit que o perfil
  Recepção **não tem**. O balcão ganhou o item e não podia vender nada por ele.
  ⚠️ A regra que sai daí vale para toda tela que sobe para o shell: **ao mover uma tela
  entre módulos, releia as permissões que ela exige.** Elas foram escolhidas para o dono
  ANTIGO. `Pode(A | B)` é um **E**; a pergunta "o bit do balcão ou o do Financeiro" é
  `PodeAlgum` (parcela 61), e foi o mesmo conserto no passo do CAIXA do Finalizar da sessão
  — o balcão via o campo de dinheiro apagado justamente no ato de registrar o que o
  paciente acabou de pagar.
  **O aviso que morria entre duas camadas.** `FechamentoSessaoService.ConcluirAsync`
  recebia os avisos de `AtendimentoService.LancarAsync` e **abria uma lista nova vazia**.
  Pelo Finalizar da Fila — o caminho que a clínica usa todo dia — a NC reaberta ("o
  paciente voltou, cobre a guia AGORA") e o anúncio do 2º código, que é o assunto do
  produto, nunca chegaram ao balcão. `RecadosDoLancamento` fica **separado** de `Avisos`:
  aquele é o que FALHOU e segura a janela aberta, este é o que ACONTECEU — somá-los faria
  um fechamento perfeito com uma NC reaberta aparecer como fechamento com erro, e três dias
  depois ninguém mais lê a janela.
  **Mensagem de sucesso invisível em cinco telas.** O par `<Border AlertaPerigo
  Visibility="{Binding MensagemEhErro}">` mostra a caixa quando o booleano é verdadeiro, e
  a mensagem de ÊXITO (que zera `MensagemEhErro`) some junto com a de erro. Cinco janelas
  gravavam certo e não diziam nada. Agora a visibilidade segue a `Mensagem` (via
  `TextoParaVisibilidade`) e a COR segue `MensagemEhErro` num `DataTrigger`: **quem decide
  se aparece é o texto; quem decide a cor é a gravidade.**
  **Releitura de fundo na Agenda e no Painel.** Só a Fila tinha relógio. A agenda é a
  ÚNICA tela da suíte em que duas pessoas escrevem no mesmo dado ao mesmo tempo, e o vão
  vago é clicável desde a parcela 58: um horário marcado na outra máquina continuava
  aparecendo livre, e a recepcionista marcaria por cima. Três recusas na batida — só HOJE,
  só no modo DIA (a semana refaz sete colunas com um await no meio de cada, e piscaria) e
  nunca por cima de uma carga no ar. O Painel bate a cada DOIS minutos, não um: são
  contagens do dia, e cada batida custa três consultas ao banco remoto.
  **O descarte de resposta fora de ordem tem uma metade que a parcela 60 não escreveu.** O
  contador de geração impede a leitura VELHA de sobrescrever a nova; ele não impede duas
  leituras de se INTERCALAREM dentro da mesma coleção. `Equipe` e `Lançados hoje` faziam
  `Clear()` e depois `await` num laço — a segunda carga limpava o que a primeira ainda
  estava preenchendo, e a lista saía com linhas repetidas ou faltando. A regra completa é:
  **entre o `Clear()` e o último `Add` não pode haver `await`** — monte em lista local e só
  então publique.
  **E campo não é tela, nem na planilha.** A feature 02 marcava ✅ em "visão por
  profissional ou por sala" porque `Agendamento.SalaId` existe. A sala é gravada,
  respeitada no choque com a capacidade dela e bloqueável por período; **não há modo de
  grade que a use como coluna**. Voltou a 🟡 em `docs/features-por-modulo.md`, com o motivo
  escrito. É o defeito recorrente do projeto na versão mais barata de cometer e a mais cara
  de descobrir: em produção, quem o encontra é o cliente lendo a própria proposta.

- **A varredura de "capacidade sem porta" achou o contrário: código morto que DUPLICA a
  tela** (parcela 63). Varrer os 50+ serviços por método sem chamador em produção
  devolveu ~20 nomes, e quase nenhum era feature faltando — eram **segundas definições**
  do que a tela já faz por outro caminho (`PerderAsync` ao lado do movimento genérico de
  perda, `AtrasadosAsync` ao lado do atraso que a tela de Recebíveis já calcula,
  `ConferirConformidadeAsync` ao lado do `ConformidadeDocumentoClinico.Conferir` estático,
  os quatro do `MapaCorporalService` ao lado do que o ViewModel faz em memória). Oito com
  ZERO referência em `src` e em `tests` foram removidos. **Duas definições da mesma regra
  divergem na primeira correção, e a que ninguém lembra de ajustar é sempre a segunda.**
  ⚠️ E a varredura tem um resultado que só aparece depois de conferir caso a caso: dos
  seis "métodos órfãos" que eu tinha listado como buraco de feature, **um era falso** — a
  tela de Recebíveis já marca e conta os depósitos atrasados. Método sem chamador é
  SINTOMA, não diagnóstico: antes de construir a porta, procure se a tela não a tem por
  outro caminho.

- **O que a auditoria de features achou, e o padrão que se repete** (parcela 63): quatro
  capacidades prontas sem porta, duas features que nunca existiram, e uma linha de placar
  que estava errada nos DOIS sentidos.
  **A agenda ganhou as duas metades que faltavam da feature 02.** A visão por SALA nunca
  existiu — a sala era gravada, respeitada no choque com a capacidade dela e bloqueável
  por período, e nenhum modo de grade a usava como coluna. E o **vão fechado** era
  visualmente idêntico ao vão livre, que é clicável desde a parcela 58: a recepcionista
  escolhia o paciente, preenchia o formulário e levava a recusa do `AgendaService` no
  Salvar, com ele na frente dela. Os bloqueios são lidos por PERÍODO e cruzados em
  memória — havia um `BloqueioDoHorarioAsync` que respondia por um vão só e nunca teve
  chamador, e usá-lo daria ~150 idas ao banco para desenhar uma tela. "Sem sala" é coluna
  de primeira classe: metade dos horários não informa sala, e escondê-los faria a visão
  por sala mostrar um dia cheio como se estivesse vazio.
  ⚠️ **O bug mais grave da parcela não estava na lista: CANCELAR uma receita não a tirava
  do ar.** A documentação do `PublicacaoDocumentoService` afirmava, desde a parcela 53,
  que o cancelamento despublicava; a única chamada de `DespublicarAsync` era a da
  EXPIRAÇÃO. O papel dizia "CANCELADA" e o endereço público continuava entregando o PDF
  assinado por até 180 dias. A correção mora no SERVIÇO porque o cancelamento tem
  **quatro portas** (ficha do paciente, Prescrições e dois caminhos da central) — a mesma
  razão pela qual a crítica do número da guia mora no `FaturamentoService`. De quebra,
  `DespublicarAsync` passou a devolver `bool` e a receber o operador: engolir a falha do
  S3 fazia a tela dizer "saiu do ar" com o arquivo ainda acessível, que é falha exibida
  como sucesso.
  **O modelo de evolução** fechou a lacuna mais cara do dia a dia clínico: `ModeloDocumento`
  existe desde a parcela 3 e serve só aos papéis IMPRESSOS, enquanto a evolução — o texto
  mais escrito do sistema — era redigitada por inteiro a cada sessão. Vale a regra do
  protocolo do mapa corporal: **aplicar COPIA, nunca aponta**, e aqui ela não é desenho, é
  a Lei 13.787/2018 — referência viva faria corrigir uma palavra do roteiro hoje reescrever
  o prontuário da semana passada. É por copiar que o modelo é a única coisa "de prontuário"
  que **se apaga mesmo**. O índice de nome é POR DONO: um global faria o "Sessão padrão" de
  um profissional sobrescrever o de outro em silêncio.
  **O CID-10 virou atalho com conferência.** O campo era texto livre em `DocumentoClinico`
  e em `ProblemaPaciente`, e "M54.4" digitado no lugar de "M54.5" é tão plausível quanto
  ele — nada denunciava. O catálogo mora em CÓDIGO pelo desenho das escalas (classificação
  PUBLICADA, não configuração da clínica) e **não é a CID-10 inteira**: são os códigos
  desta clínica, o campo continua aceitando qualquer texto, e a tela diz isso. Recusar o
  que está fora da lista seria a regra apertada demais que o projeto já rejeitou no
  formato do número da guia. A metade que pega o erro é a DESCRIÇÃO ao lado do campo.
  **A conciliação bancária por OFX** fechou o último ponto do financeiro em que o mês
  dependia de alguém não se distrair. O leitor é à mão (o OFX 1.x tem tags que não fecham
  — não é XML), o valor é lido em cultura INVARIANTE (em pt-BR, "-2500.00" viraria
  -250000), e o sistema **propõe, a pessoa confirma**. A folga de datas é de três dias e
  não de trinta: folga grande casaria o aluguel de julho com o de agosto e a tela passaria
  a pedir desempate em tudo, que é como se ensina alguém a clicar sem olhar. A metade que
  ninguém pede é `SoNoSistema` — dado como recebido aqui e ausente do banco.
  ⚠️ **E a "dívida de leiaute" das seis telas já estava PAGA** desde a parcela 49; a
  planilha é que não tinha sido atualizada. Os 330 px do `AcessosView` e os 300 do
  `CampanhasView` são a coluna de **Ações** — o falso positivo que a parcela 54 já
  documentara. Somado ao ✅ falso da feature 02, a lição fecha nos dois sentidos: **linha
  de placar sem conferência no código é chute com aparência de registro**, e ela erra
  tanto a favor quanto contra.

- **Nome de propriedade errado em controle da CASA: só o compilador de MARCAÇÃO pega**
  (parcela 63, checagem 34 — o CI reprovou o PR). Três telas novas declararam
  `TextoVazio` no `EstadoDaTela`, que tem `TextoCarregando` e `TextoNaoVerificado` — o
  vazio se escreve com `Titulo` + `Descricao`. `MC3072`, e nenhuma rede local via: o XML é
  bem-formado, o `compilar-sombra` **não lê o corpo** do XAML e o C# compila. É a irmã da
  checagem 33, e o que a torna fácil de cometer é o nome plausível existir AO LADO do
  certo.
  A checagem casa cada atributo de `<ctrl:Tipo …>` com as propriedades declaradas no tipo
  e nas bases dele. As duas decisões que a mantêm utilizável: cadeia que termina numa base
  do WPF conhecida responde **"não tem"** (sem isso ela calaria para todo controle que
  herda de `Control`, que são todos); cadeia que termina em tipo de fora desconhecido
  responde **"não sei"** e cala.
  ⚠️ O autoteste pegou os DOIS erros dela antes de mim — a primeira versão respondia "não
  sei" para o caso real, e a segunda acusava o `Key` de `x:Key` por o *lookbehind* não
  excluir os dois-pontos. **Checagem nova sem autoteste do caso real e do caso legítimo é
  checagem que nasce cega ou barulhenta.**

- **A LINHA "Personalizado" DO CATÁLOGO É RENOMEÁVEL, e isso vazava para TODA operadora**
  (parcela 68 — a clínica mandou a foto: oito pacientes **Sul América** apareciam faturando
  como **"Porto Saúde"** na consulta de guias, e a lista de pacientes ao lado, na mesma
  janela, mostrava "SULAMERICA" para os mesmos oito).
  A causa não é o catálogo: é `ConvenioCatalogoService.ListarAsync` **garantir uma linha por
  família**, inclusive uma de código `"Personalizado"` — e essa linha é editável como
  qualquer outra. A clínica renomeou-a para a primeira operadora que cadastrou (em vez de
  clicar "Adicionar"), e a partir daí `CatalogoConvenios.Nome(Convenio.Personalizado)`
  passou a devolver o nome DELA para toda operadora personalizada.
  ⚠️ **Este é o defeito da parcela 50 na única variante que a correção dela não cobriu, e é
  a pior.** Lá, resolver pela família escrevia **"Personalizado"** — feio, óbvio, e alguém
  abre chamado. Aqui escreve **"Porto Saúde"**: o nome de uma operadora de verdade, na guia
  de outra operadora, com toda a cara de estar certo. É a **garantia aparente** de novo, e
  só foi descoberto porque o cliente comparou duas telas.
  A blindagem é `Nome(Convenio familia)` **não passar mais pelo catálogo quando a família é
  `Personalizado`** — ela devolve o rótulo neutro `"Convênio personalizado"`. Pior de ler e
  muito melhor de confiar: não responder qual operadora é **é a verdade**, porque a família
  não sabe. ⚠️ A assimetria com `Nome(codigo, familia)` é deliberada: **lá** o caminho de
  baixo continua indo ao catálogo, porque sem código a linha da família é o melhor palpite
  que existe — o paciente foi mesmo cadastrado nela, e blindar os dois lados apagaria o
  nome de quem estava certo.
  **O agrupamento por família era pior que o rótulo**, porque não é aparência, é número:
  `RelatorioService.PorConvenio` e a estatística do `PrevencaoGlosaService` agrupavam pela
  FAMÍLIA, então Sul América e Porto Saúde caíam numa linha só com o nome de uma delas. A
  direção lia "Porto Saúde: 143 guias" e eram as duas somadas — o número que ela usa para
  negociar tabela. Os dois passaram a agrupar por OPERADORA, que é o que o lote TISS já faz
  desde a parcela 60 (`ConvenioDaGuia`) e pela mesma razão.
  ⚠️ **O teste que existia para exatamente este assunto (`NomeDoConvenioTests`, parcela 50)
  passava.** Ele monta um catálogo SEM linha de código "Personalizado" e afirma que a
  resolução por família não devolve o nome da operadora **certa** — o que é verdade e deixa
  passar o caso real, que é devolver o nome de uma operadora **errada**. A asserção tinha de
  ser "não é o nome de operadora nenhuma". **Teste de "não é X" só vale quando X é o
  conjunto inteiro dos valores errados; contra UM valor errado, ele passa e o defeito
  também.**
  A **checagem 35** cobra o binding, e o que a mantém utilizável é resolver o **TIPO** e
  nunca o nome: `Convenio` é `string` já resolvida numa dúzia de records de ViewModel
  (Conciliação, Gerente, Recepção), e acusá-los seria o ruído que faz alguém desligar a
  ferramenta. Ela dispara quando o tipo declara `Convenio` como ENUM **e** oferece
  `ConvenioNome` ao lado — quem oferece o nome resolvido está dizendo que sabe qual é a
  operadora. Sem a segunda metade ela acusava a tabela "Regras por família" de
  Configurações, que é por família mesmo. O tipo do item de lista sai do ViewModel **da
  própria tela** (`FooView.xaml` ↔ `FooViewModel`), nunca da busca global da checagem 20:
  `Itens` e `PorConvenio` existem em telas diferentes com tipos diferentes, e a busca global
  respondia o tipo do vizinho — a resposta certa vinda do arquivo errado.

- **Tabela empilhada em `StackPanel` sem rolagem: o fim da tela é CORTADO, sem barra e sem
  como alcançar** (parcela 68, 2ª rodada — a cliente mandou a foto: em Relatórios, a seção
  "Não conformidades (guias justificadas na rodada)" aparecia só com o TÍTULO, decepada na
  borda de baixo da janela). A mecânica é de uma linha: a janela tem altura FINITA e o
  `StackPanel` vertical dá a cada filho a altura que ele PEDE, ignorando o disponível —
  três cards com tabela somam mais que a tela, e sem `ScrollViewer` o resto some.
  ⚠️ **Não é a mesma coisa que a tela cujo conteúdo elástico é UM `DataGrid` numa linha
  `*`** — aquele rola por dentro e a linha `*` absorve o resto. Cinco telas do faturamento
  têm essa forma e estão certas. O que distingue as duas não é a linha `*`, é o
  EMPILHAMENTO: no `StackPanel` nada absorve.
  O remédio é o padrão do `DashboardView`, e são **três** peças, não uma: `ScrollViewer` na
  raiz; **todas as linhas `Auto`** (dentro de um ScrollViewer a altura disponível é
  infinita, então `*` não distribui nada — ele mede igual a `Auto` e só engana quem lê); e
  **`MaxHeight` nas grades que crescem com o dado**, senão elas recebem altura infinita,
  desenham todas as linhas, perdem a virtualização e empurram as de baixo para longe. Grade
  de tamanho fixo (as 3 faixas de envelhecimento, os 6 meses) não leva teto — teto onde não
  precisa é o corte de volta.
  ⚠️ **A checagem 36 nasceu CEGA e o autoteste é que mostrou**: a primeira versão exigia
  que a pilha estivesse dentro de uma linha `*`, porque era assim no caso real — e ficou
  sem ver a variante PIOR, a mesma pilha com todas as linhas `Auto`, que corta igual. A
  linha `*` era coincidência do exemplo, não a causa. **Quando causa e sintoma aparecem
  juntos no primeiro caso, confira qual dos dois a checagem está olhando.** Autotestada nas
  duas formas quebradas e nas três legítimas, e ela chama a MESMA função da varredura, pela
  lição da parcela 67 (autoteste que reimplementa fica verde quando a checagem quebra).
  Nenhuma rede pegava, e é a categoria mais cara: o XAML é bem-formado, o
  `compilar-sombra` não lê o corpo, o compilador de marcação não reclama e nada lança. Só a
  tela montada mostra — e só em quem tem a janela mais baixa que o conteúdo, que **nunca é
  a máquina de quem escreveu**.

- **Duas caixas editando o MESMO campo é o que faz uma tela "não se entender"** (parcela
  69 — a cliente disse que a caixa de convênios das Configurações estava "horrível e não
  muito fácil de entender"). A aba era uma pilha de SEIS `Card` (o proibido nº 2 da regra
  de leiaute), espremida em `MaxWidth="860"` — e o pior não era o empilhamento: `Nome` e
  `Fatura como (família)` eram editáveis **na grade E no painel de baixo**, dois campos
  para o mesmo dado a 300 px um do outro. Quem lê conclui que são coisas diferentes e
  procura a diferença que não existe.
  O desenho é o que a regra manda: **lista de largura inteira → janela do item**. A lista
  passou a RESPONDER a pergunta da tela — família, gera guia ou é particular, forma do nº
  da guia e registro ANS numa linha só, o que antes exigia clicar convênio por convênio —,
  e configurar, que se faz de vez em quando, mora atrás de um clique. A janela recebe o
  **mesmo `ConvenioEdicao`** da lista (duas cópias dariam duas verdades) e por isso não tem
  "Cancelar": quem grava continua sendo o "Salvar configurações" da tela de trás, e o
  rodapé escreve isso em vez de deixar a pessoa supor.
  Três coisas que a rodada separou e que valem além desta tela: (a) **a tabela "por
  família" não é a lista de operadoras** — ela responde "quanto vale o número da regra que
  várias compartilham", e ficava embaixo da outra parecendo mais do mesmo; virou janela
  própria com o aviso de que o número vale para TODAS as operadoras daquela família;
  (b) **os três prazos globais** (alerta de consulta, recurso de glosa, rodada de
  pendências) não eram de convênio nenhum e moravam ali — foram para uma aba "Prazos",
  que é a 3ª pergunta da regra de leiaute aplicada literalmente; (c) o rótulo dessa aba é
  curto de propósito, porque o `TabPanel` padrão **encolhe** as abas e a janela mínima do
  faturamento (960) menos a sidebar (240) põe seis abas no limite — aba cortada no meio da
  palavra já foi reprovação do cliente na parcela 50.
  ⚠️ E **a linguagem é metade do "não se entende"**: "candidata a 2º código" e "Categoria
  (com app)" descrevem o modelo, não o trabalho. Viraram "A clínica atende com
  eletroacupuntura neste convênio" e "Semáforo quando o paciente TEM app", cada campo com
  a consequência escrita ao lado — o mesmo que a parcela 49 fez com as caixinhas de
  permissão. Nada foi tirado: todo campo editável antes continua editável, um clique
  adiante.

- **O CONSENTIMENTO ERA UMA CAIXINHA — e a única prova era a palavra de quem clicou**
  (parcela 89; o mapa completo está em `docs/termo-assinado-pelo-paciente.md` §8, e é lá
  que se atualiza). A direção pediu que **todo** documento que precisa da assinatura do
  paciente fosse pelo Worker, como o termo do BSV, e nomeou os quatro consentimentos LGPD
  da Recepção. Medido antes de escrever: **a coleta inteira já era genérica sobre
  `DocumentoClinico`** — a janela, a segunda tela, o traço, a evidência, o selo, o envio
  pelo WhatsApp e a volta pelo Worker olham `AguardaAssinaturaDoPaciente`, que responde por
  `TipoDocumentoInfo.AssinadoPeloPaciente`. **Faltava uma linha de portão.** É o defeito
  recorrente do projeto na variante mais barata de corrigir e a mais cara de deixar: a
  capacidade existia, testada e em produção, para um tipo só.
  ⚠️ **A inversão é a metade que importa.** O termo era o RECIBO do que o balcão marcara;
  passou a ser a FONTE. Com ele como recibo, o paciente podia responder "Não" ao marketing
  no celular e a clínica continuar mandando campanha, porque a caixinha seguia marcada —
  **duas verdades sobre o mesmo fato, e nada falha: a campanha simplesmente sai**. E o
  problema maior é jurídico: o art. 8º pede manifestação do TITULAR e o §2º põe o ônus da
  prova em quem trata o dado, e a nossa prova era um clique da recepcionista.
  Três decisões da direção: a **resposta assinada vence**; a caixinha **deixa de existir**
  (o termo é o único caminho — o que não trava quem não tem celular, porque a coleta no
  balcão continua sendo assinatura; o que acaba é o consentimento SEM assinatura nenhuma);
  e é **um termo com as quatro declarações**, não quatro papéis.
  ⚠️ **O vínculo é por CÓDIGO** (`ItemDocumento.Codigo`, migration aditiva), nunca por
  `Ordem` — seria o contrato de ÍNDICE que a parcela 41 trocou por nome: acrescentar uma
  finalidade no meio empurraria todas, e o "Sim" do uso de imagem viraria autorização para
  compartilhar com o convênio, **sem quebrar build nenhum**. Nem pelo RÓTULO, que a clínica
  reescreve. Código não reconhecível é **ignorado, nunca adivinhado**: deduzir pela posição
  gravaria a autorização errada, que é pior do que não gravar.
  ⚠️ **O PAPEL tinha de mudar junto, e essa foi a armadilha da parcela.** O desenho antigo
  (`ListaFinalidades`) marcava um X quando a resposta era a palavra `"Autorizado"` e
  escrevia "Pendente" no resto — com as respostas em "Sim"/"Não" ele imprimiria TODA
  finalidade como pendente, e um **"Não" sairia idêntico a uma pergunta não respondida**,
  no papel que o paciente leva justamente para provar o que recusou. O termo LGPD passou a
  usar o MESMO desenho do termo de procedimento. A regra que fica: **ao trocar o que um
  campo GUARDA, procure quem o IMPRIME** — o renderizador casa por VALOR e não quebra
  build ao deixar de reconhecê-lo.
  ⚠️ E **`RespostaDeclaracao` desceu da Application para o DOMÍNIO**: o termo LGPD precisa
  ler a mesma resposta e o Domínio não enxerga a Application. Uma segunda cópia de "isto é
  um sim?" divergiria na primeira correção — aqui, o paciente responder "Não" e o sistema
  gravar outra coisa. Pelo mesmo argumento, o par *linha + auditoria* do consentimento saiu
  para `ConsentimentoService.Montar`: são dois caminhos de escrita e um não pode chamar o
  outro (o termo grava no MESMO `SaveChanges` do ato; `RegistrarAsync` tem o `SalvarAsync`
  dele), e duas montagens divergiriam na AÇÃO de auditoria, que é o nome pelo qual uma
  investigação procura.
  ⚠️ **Reler RASTREADO na revogação**: `ConsentimentosDoPacienteAsync` é `AsNoTracking` —
  ela existe para LER —, e mutar o que ela devolve não grava nada. A revogação sumiria em
  silêncio, e a ficha mostraria uma autorização sem fim ao lado da recusa assinada.
  **REVOGAR continua sem assinatura, e a assimetria é da lei**: revogar é direito
  UNILATERAL do titular (art. 8º, §5º; art. 18), atendido de imediato, inclusive por
  telefone — exigir termo assinado dificultaria o lado que a LGPD manda facilitar.
  A **procedência** entrou na linha de cada finalidade ("Concedido em 12/03 · termo
  2026/0007"): é o número do termo que responde *"onde está a prova?"*, e sem ele a
  auditoria continuaria acreditando na palavra de quem clicou.
  ⚠️ E a releitura do próprio diff pegou o de sempre: `CarregarDocumentosAsync` limpava a
  lista **depois** do await, então uma falha de leitura deixaria as linhas do paciente
  ANTERIOR na tela com o botão "Colher assinatura…" apontando para o `TermoLgpdPendenteId`
  do outro — **um clique assinaria o termo de quem já saiu, em nome de quem está na
  frente**. É a lição da parcela 66, 2ª rodada, na tela vizinha à que a ensinou.

- **LIGAR UM PORTÃO NOVO FAZ O DADO ANTIGO SATISFAZER A CONDIÇÃO NOVA** (parcela 89, 2ª
  rodada — a clínica colheu o termo e o alerta *"Sem consentimento LGPD de tratamento de
  dados"* continuou aceso; o print do papel resolveu o diagnóstico em um olhar). O termo
  de consentimento passou a ser `AssinadoPeloPaciente`, e no MESMO instante todo termo LGPD
  já emitido pela versão anterior — quando ele era o **RECIBO** da caixinha do balcão —
  passou a satisfazer `AguardaAssinaturaDoPaciente`: não está cancelado, o paciente não
  assinou, não recusou. A ficha ofereceu um deles como "pendente", a coleta o
  **reaproveitou**, o paciente respondeu "Sim" nas quatro declarações, o documento saiu
  selado e completo — e **nenhum consentimento foi gravado**, porque os itens antigos não
  têm `Codigo` e `TermoConsentimento.Decisoes` não tinha por onde ler a resposta.
  ⚠️ **Nada falhou em lugar nenhum.** Build, 1938 testes, três redes locais e o CI verdes;
  o papel saiu perfeito; e o alerta continuou aceso do outro lado da tela. É a **garantia
  aparente** na forma mais discreta — e a mais cara, porque a clínica acredita ter colhido.
  **O papel é que denunciou**: a via saía com o RÓTULO da finalidade ("Tratamento de dados
  pessoais e de saúde") e o detalhe "Nunca perguntado", que são exatamente o que a emissão
  ANTIGA escrevia — conteúdo velho desenhado pelo renderizador novo. **Print de tela é
  evidência de primeira classe: leia o que está escrito, não o que devia estar.**
  A regra que fica, e ela vale para todo portão: **ao alargar a condição que um dado
  satisfaz, pergunte o que na BASE passa a satisfazê-la** — e se o que passa é a mesma
  coisa. Aqui não era: os dois papéis compartilham o `TipoDocumentoClinico` e são coisas
  diferentes; o discriminador é o `Codigo` do item, e ele não existia.
  As duas metades da correção, porque uma sem a outra não resolve: a porta **não OFERECE**
  o termo antigo (senão o paciente assina e só então leva a recusa) e `ColherAsync`
  **RECUSA** (senão a central, o link do WhatsApp ou uma tela futura reabrem o caminho).
  ⚠️ E a tela deixou de AFIRMAR "Assinado em 26/08" sobre um papel desses: header dizendo
  que o termo foi assinado com o alerta "sem consentimento" aceso no balcão são duas
  verdades sobre o mesmo fato — o defeito que esta parcela existe para acabar, cometido
  pela própria correção dele.
  ⚠️ **`Enum.TryParse` aceita NÚMERO, e `Enum.IsDefined` não salva**: `"1"` vira uma
  finalidade de verdade porque 1 É um valor definido. Quando o código guardado é o NOME, a
  conferência é de **ida e volta** (`finalidade.ToString() == codigo`) — senão um código
  numérico vindo de qualquer lugar grava a autorização de uma finalidade que ninguém
  escreveu.
  ⚠️ **A pergunta "quais termos carregam finalidade" virou consulta PRÓPRIA**, e não um
  `Include` na leitura dos documentos da ficha: aquela alimenta a lista inteira, e puxar os
  itens de todos arrastaria o `Desenho` dos relatórios de evolução — um mapa corporal por
  sessão — a cada abertura de ficha. E decidir pela navegação `documento.Itens` ali seria a
  lição da parcela 68 de novo: vazia em produção, cheia no teste pelo fixup do EF, com TODO
  termo — o novo inclusive — parecendo da versão anterior.

- **LISTA QUE ROLA POR DENTRO COME A RODA DO MOUSE, E A PÁGINA NÃO ROLA** (parcela 90 — o
  cliente: *"quando clicamos em lançar outro ele nos leva a tela de pacientes que está toda
  cortada junto a tela de lançados hoje"*, com a janela MAXIMIZADA). O `ScrollViewer` que o
  `ListBox` traz no template marca o evento da roda como TRATADO — sempre, inclusive no
  limite —, e ele nunca sobe para o `ScrollViewer` da página. Com a lista ocupando 300 px no
  meio da tela, ela é o **maior alvo do cursor**: a pessoa gira a roda em cima dela, a
  página não anda, e a leitura natural é que **a tela quebrou**.
  ⚠️ **O que me fez errar o primeiro palpite** foi tratar "não rola" como posição de
  rolagem. Não era: a rolagem existe e o `ScrollViewer` até desenha a barra — o que não
  existe é o EVENTO chegar nele. Perguntar *"rolar para cima resolve?"* foi o que separou as
  duas coisas, e a resposta **"não resolve"** matou a hipótese em uma pergunta. **Quando o
  sintoma é "não rola", separe POSIÇÃO de EVENTO antes de mexer em leiaute.**
  Por que só depois do "Lançar outro": `NovoLancamento` faz `Seletor.Termo = null`, o que
  dispara a busca e traz as 50 primeiras linhas. No primeiro open a lista também vem cheia
  — só que a pessoa **digita um nome imediatamente**, a lista encolhe para duas ou três
  linhas e nada passa da dobra. O estado "50 pacientes parados na tela + LANÇADOS HOJE" só
  acontece depois do reset, e é o único em que a página precisa rolar.
  ⚠️ **E ele não aparece na máquina de quem programa**: a coluna esquerda pede ~730 px, e
  isso só corta no monitor de 1366×768 do balcão ou com a escala do Windows em 125/150% —
  a mesma família da parcela 79.
  A correção é `Ajudantes.RodaDaPagina` no shell, e a **condição de borda é o que a separa
  de um remendo**: a roda vai para a página **só quando a lista já chegou ao fim naquela
  direção**. Sem isso, trocaríamos o defeito pelo oposto — a lista pararia de rolar.
  De quebra o `MinHeight="180"` da lista saiu: reservar 180 px de lista VAZIA empurrava a
  conferência do dia para fora da vista antes de haver o que mostrar.
  **Nenhuma rede pega**: o XAML é bem-formado, o binding é válido, nada lança, e as três
  redes locais e o CI ficam verdes. Só a tela montada — e só na altura errada.

- **LISTA SEM TETO CRESCE COM O DIA — e é a metade que só quebra DEPOIS de lançar**
  (parcela 90, 2ª rodada; a frase que a achou foi do cliente: *"esse erro acontece APÓS
  fazer um lançamento e clicar em 'lançar outro'"*). A roda do mouse explicava por que
  rolar não resolvia; **não explicava por que o corte só aparece depois de lançar**, e eu
  tinha dado a primeira metade por resposta inteira.
  A causa é `CarregarDoDiaAsync`, que roda **de novo a cada lançamento** e lista TODO
  agendamento de hoje com atendimento — não só o que saiu desta tela. Numa clínica que
  trabalhou o dia inteiro são 20 a 40 linhas, e o cartão LANÇADOS HOJE tinha `MinHeight` e
  **nenhum `MaxHeight`**: o `ItemsControl` desenha todas, sem virtualização, e a coluna
  esquerda passa de dois mil pixels — crescendo a cada guia lançada. Some-se a lista de
  pacientes que o reset reabre cheia, e o que a pessoa vê é a tela cortada logo depois de
  um lançamento bem-sucedido.
  ⚠️ **A regra que fica: `MinHeight` num cartão cujo conteúdo vem do BANCO é meia decisão.**
  O piso responde "não encolha quando estiver vazio"; ninguém respondeu "até onde pode
  crescer quando estiver cheio" — e a resposta implícita do WPF é *sem limite*. É a irmã da
  checagem 36 (dentro de um `ScrollViewer` a altura disponível é INFINITA, então nada
  distribui) e da parcela 79 (filho ancorado que não cabe é decepado): **quem cresce com o
  dado precisa de teto, e o teto vem com rolagem por dentro.**
  ⚠️ E o teto sozinho seria trocar o defeito de lugar: lista que rola por dentro come a
  roda (a lição acima), então o `ScrollViewer` novo nasceu com `RodaDaPagina`. **As duas
  metades andam juntas — teto sem devolução da roda é a mesma tela travada, numa altura
  menor.**
  A lição de método é a mais cara da rodada: **quando o cliente diz QUANDO o defeito
  acontece, o "quando" é parte do diagnóstico, não contexto.** Achei um mecanismo real,
  fechei a resposta com ele, e o gatilho que ele não explicava ficou de fora — foi preciso
  o cliente repetir a palavra "APÓS" para eu ir ler o que o `Lançar outro` faz de
  diferente. **Mecanismo que não explica o gatilho é meia causa.**

- **A MESMA RODA COMIDA EM MAIS DEZESSEIS LUGARES — e metade foi CRIADA por uma correção
  nossa** (parcela 90, 3ª rodada — a varredura que respondeu à pergunta *"ainda ficou
  erros?"*). `ScrollViewer.OnMouseWheel` marca o evento como TRATADO **sempre**, inclusive
  quando não há o que rolar; então todo controle cujo template traz um `ScrollViewer`
  (`DataGrid`, `ListBox`, `ListView`, `TreeView`, `RichTextBox`, e o próprio `ScrollViewer`
  aninhado) come a roda. A primeira varredura filtrou por "tem `MaxHeight`" e por isso
  deixou dois de fora: **teto não é a condição — a condição é ter `ScrollViewer` no
  template.**
  ⚠️ **Doze das dezesseis estavam no FATURAMENTO, e a checagem 36 é quem as pôs lá.** A
  correção da parcela 68 (seção decepada em Relatórios) manda pôr `ScrollViewer` na raiz e
  teto nas grades — e é exatamente isso que dá a cada grade um `ScrollViewer` próprio para
  comer a roda. **As duas metades andam juntas**, e uma correção que só aplica a primeira
  troca o corte por uma tela travada. Quatro delas ficam no **Dashboard**, que é a tela de
  ABERTURA do app que fatura a clínica.
  As outras quatro: a régua da EVA na janela de evolução (duas — `ListBox` horizontal, sem
  nada que role, comendo a roda vertical do formulário inteiro) e as duas listas da Guarda
  de prontuário do Gerente.
  ⚠️ **O que NÃO era defeito, e a regra que o separa**: os dez `ScrollViewer` das raias do
  kanban (Fila e Meu dia) estão dentro de uma página que rola **só na horizontal**
  (`VerticalScrollBarVisibility="Disabled"`) — ali comer a roda vertical é o
  comportamento certo, porque quem deve rolar na vertical é a raia. A busca sobe **pulando**
  essas páginas horizontais: se houver uma vertical mais acima, o defeito continua de pé.
  `Ajudantes.RodaDaPagina` teve de ser **portado para o design system do faturamento** — os
  dois não se referenciam, o débito permanente da Fase 4 —, como o `obrigatorio` do
  `PromptWindow` (parcela 75) e o `TextoParaVisibilidade` (parcela 61). **Correção de
  ajudante do shell se porta para o outro lado no mesmo commit**; a cópia que ficar para
  trás é onde a capacidade some.
  Virou a **checagem 43**, medida antes de ligar: **zero** ocorrências depois da correção —
  ela nasce sem uma linha de ruído. Autotestada contra o caso real (verificado removendo o
  atributo de uma grade do Dashboard: ela acusa a linha exata) e contra os três legítimos —
  com o atributo, kanban horizontal, e sem página rolante acima.

- **`CornerRadius` 999 no WPF desenha um OVO, não uma pílula** (parcela 91 — o cliente
  mandou a foto da busca global e dos chips ovais). O CSS trava raio maior que a metade da
  altura NA metade; o WPF não trava — os arcos saem por inteiro e as bordas de cima e de
  baixo ficam curvas. O token `Raio.Pilula` (999, espelho fiel do CSS do kit web) era
  referenciado cru em TREZE lugares dos dois design systems, e ninguém tinha visto porque a
  deformação cresce com a LARGURA: o badge estreito engana, a busca larga denuncia.
  Pílula de verdade é **`ctrl:Ajudantes.Pilula="True"`** (nos dois design systems), que
  mede a altura REAL do Border e aplica raio = metade do menor lado a cada mudança de
  tamanho; círculo de tamanho FIXO usa raio explícito. O token fica — é o espelho do CSS,
  onde 999 é correto — com o ⚠️ escrito; quem impede o uso cru é a **checagem 44**,
  autotestada nas duas formas do defeito (atributo e setter de estilo) e nos dois
  legítimos (a definição do token e o comentário). A lição que generaliza é a das
  parcelas 68/79: **valor que atravessa a fronteira entre dois sistemas de desenho (CSS ↔
  WPF, QuestPDF ↔ PDFsharp) tem a semântica MEDIDA no destino, nunca presumida da origem.**


- **AS TELAS PLANAS DE PRONTUÁRIOS E EXAMES, e a situação que só existe se houver FATO**
  (set/2026 — as duas telas do handoff que faltavam no Consultório). A tela de Exames
  pede "Aguardando resultado / Resultado disponível", e o domínio não tinha o elo que
  sustenta a resposta: o laudo entrava como anexo da sessão ou como `ResultadoExame`
  avulso, sem dizer DE QUAL pedido era. O elo é `ResultadoExame.PedidoDocumentoId`
  (migration aditiva, nullable — linha antiga fica com nulo, que é a verdade), a situação
  é DERIVADA da contagem de resultados vigentes amarrados, e o serviço RECUSA amarrar em
  pedido de OUTRO paciente ou em documento que não é pedido — o vínculo errado daria
  baixa na espera de outra pessoa. O "Agendado" do mockup ficou de FORA: não há fato de
  agendamento de exame no domínio, e situação sem fato é a garantia aparente de sempre.
  ⚠️ **A tela de Prontuários dá a cada linha a situação que ela TEM, não a que o mockup
  pinta**: anamnese é DOCUMENTO e tem assinatura de verdade ("A assinar" → o fluxo
  ICP-Brasil existente); evolução NÃO é assinável — o pendente real dela é a sessão sem
  registro ("A escrever", levando ao atendimento daquele horário). As linhas carregam
  `EvolucaoId` e `DocumentoId` em campos SEPARADOS (ids são por tabela — parcela 71), e
  quem pede AÇÃO sobe para o topo. Os montadores são PUROS na Application
  (`ListaDeProntuarios`, `PedidoDeExameLinha`) — o que a tela afirma mora onde o
  `dotnet test` alcança. As listas são PROJEÇÕES novas do repositório (sem os textos da
  evolução, sem a miniatura da foto — a lição da parcela 74), executadas em teste e na
  rede de tradução do Npgsql. O combo "responde ao pedido" mora na ÚNICA janela de
  registro de resultado, para as duas portas amarrarem pela mesma regra; e o "Novo pedido
  de exame" de uma tela SEM paciente em foco pergunta QUEM primeiro
  (`EscolherPacienteWindow`, no shell — capacidade em porta única é o defeito recorrente).
  Na lista da CLÍNICA inteira (posto sem vínculo/enfermagem) cada linha diz o
  profissional — booleano de estado sem leitor é só uma atribuição (parcela 76).

- **O LAUDO EM ARQUIVO, e por que ele não virou "anexo"** (set/2026 — a clínica recebe o
  PDF do laboratório por WhatsApp e precisa SUBIR). O caminho óbvio seria o
  `AnexoProntuario`, que já guarda arquivo clínico — e ele foi MEDIDO e recusado:
  `EvolucaoId` é obrigatório e `AnexosDoPacienteAsync` resolve o paciente ATRAVÉS da
  evolução (`a.Evolucao!.PacienteId`), então anexo sem sessão não existe. Aceitá-lo
  exigiria `AlterColumn` (migration não aditiva) mais uma coluna nova obrigatória com
  backfill, num app em produção — e o laudo que chega pelo WhatsApp não é de sessão
  nenhuma: forçá-lo a uma evolução seria inventar uma consulta que não houve.
  O laudo mora no `ResultadoExame`, que É o que ele registra. As decisões:
  **os BYTES ficam em tabela 1:1** (`ArquivosResultadoExame`) e só os METADADOS na
  linha — é o padrão do retrato do paciente (`FotoMiniatura` na linha, `PacientesFotos`
  à parte), e sem ele toda leitura da lista arrastaria os PDFs pela rede (a lição da
  parcela 74; a rede de tradução do Npgsql fixa que o SELECT não os traz);
  **valor OU laudo** — o PDF é registro completo por si, e exigir também o número faria
  a técnica inventar um para conseguir anexar; o que se recusa é o registro sem conteúdo
  nenhum; **o teto é o MESMO do anexo de prontuário** (`ProntuarioService.
  TamanhoMaximoAnexo`), porque dois limites divergem na primeira correção; e o arquivo é
  gravado no **MESMO `SaveChanges`** do resultado — laudo sem a linha que o descreve é
  arquivo órfão, e linha que promete arquivo sem ele é um "abrir laudo" que não abre.
  ⚠️ **Anexar aparece em TODO pedido vivo, não só no que aguarda**: um pedido de vários
  exames recebe vários laudos, e esconder o botão no primeiro resultado impediria o
  segundo. Só o pedido CANCELADO não recebe.
  ⚠️ E a mudança **desatualizou dois textos que ninguém teria relido**: o comentário do
  modal de resultado dizia que "o resultado estruturado não tem arquivo", e o subtítulo
  da seção Exames e anexos mandava anexar pela sessão no Prontuário. Os dois foram
  corrigidos no mesmo commit — **ao dar capacidade nova a um registro, procure o que
  AFIRMAVA que ela não existia.**

- **A IMPORTAÇÃO DA CARTEIRA DO SISTEMA ANTERIOR — e a auditoria que ficaria pendurada**
  (set/2026 — a clínica migrou do Smart Clinic e recebeu a exportação; roteiro em
  `docs/importar-pacientes.md`). `ImportacaoPacientesService` em dois passos separados de
  propósito: `PreverAsync` lê o CSV com o mapeamento e diz linha a linha o que VAI acontecer
  sem gravar; `ExecutarAsync` grava o que a prévia mostrou. As regras: **idempotente pela
  chave** (`Paciente.ChaveImportacao = IMPORT:{sistema}:{id}`, índice ÚNICO — a coluna nasce
  vazia, então o índice não tem como falhar na abertura), **quem existe é COMPLETADO e nunca
  sobrescrito** (CPF com ou sem máscara, ou nome + nascimento; convênio da ficha é mantido),
  **a criação passa pelo `PacienteService`** (a regra do CPF mora lá), e **convênio do
  arquivo é TEXTO que a direção aponta para um `ConvenioCadastro`** — sem palpite, porque
  "Unimed" pode ser Padrão ou Intercâmbio.
  ⚠️ **Auditoria acrescentada ANTES de um serviço que pode RECUSAR fica pendurada no
  contexto.** A primeira versão registrava o `EventoAuditoria` e só então chamava
  `SalvarNovoAsync`; quando ele recusava a ficha (CPF que entrou por outra porta), a linha
  "PacienteImportado" ficava rastreada e saía gravada junto da ficha SEGUINTE — trilha
  afirmando uma importação que não houve. Aqui a ordem é ato → trilha → Salvar, com o id
  da ficha; é uma exceção declarada à regra 7 do compromisso. **E entidade RASTREADA mexida
  antes de uma recusa sai no próximo Salvar de outra linha** — daí o retrato/restauração
  na ficha completada. Regra: **num laço que grava N linhas com um `DbContext` só, toda
  linha que falha tem de deixar o contexto como o encontrou.**
  ⚠️ O prontuário antigo **não é PDF**: a exportação real traz o prontuário em TEXTO por
  paciente (pós-operatório, S-O-A-P, prescrições, anamnese), e a direção decidiu **"não
  perder NADA"** — ver o bloco do PACOTE abaixo.
  ⚠️ **Sugestão de coluna se mede contra o arquivo REAL, não contra os sinônimos que o
  autor imagina.** A primeira versão do sugestor, rodada no `pacientes.csv` da clínica,
  mandou o convênio para `operadora` (a operadora do CELULAR — a prévia oferecia 80
  números de telefone como convênios a mapear), deixou a carteirinha sem coluna
  (`numero_convenio`) e escolheu `telefone` (78 preenchidos) em vez de `celular` (2.145).
  Nenhum teste sintético pegaria: os sinônimos eram os meus. A regra que ficou: a
  prioridade é do SINÔNIMO dentro de cada campo (o primeiro que existir no arquivo vence),
  nunca da ordem das colunas; e o cabeçalho real virou teste. E **aviso por linha que se
  repete em 3 de cada 4 linhas não é aviso** — o sexo em branco virou contagem no aviso
  geral, senão os 19 avisos que importavam ficavam enterrados em 1.712 iguais.

- **O PACOTE DO SMART CLINIC — "não perder NADA", e o que isso decidiu** (set/2026;
  `ImportacaoSmartClinicService`, mapa arquivo→destino em `docs/importar-pacientes.md`).
  O ZIP tem 14 CSVs, e cada um ganhou destino ESCRITO: a carteira vira ficha; os oito
  arquivos de prontuário viram `Evolucao` (HTML → texto, data de lá, autor como o sistema
  antigo gravou em `CriadoPor`, vínculo com a Equipe quando o nome casa); a agenda FUTURA
  vira `Agendamento`; a agenda PASSADA e toda coluna sem campo (e-mail, RG, nome da mãe…)
  vão para as OBSERVAÇÕES da ficha, com rótulo. Login e senha nunca entram.
  ⚠️ **Visita passada não vira sessão.** Marcar 9.000 horários antigos como `Realizado`
  sem evolução ligada inundaria "sessões sem evolução" e o alerta da direção; criar
  `Atendimento` inventaria guia. Preservar como texto legível na ficha é o que não mente.
  ⚠️ **Alargar coluna é a saída consciente da checagem 18, e é a certa aqui**: 88
  registros passavam de 4.000 caracteres, e cortar registro clínico na importação é
  perder o que a clínica pediu para não perder. Os quatro textos da `Evolucao` (e os da
  `VersaoEvolucao`, senão a primeira correção de um importado falharia ao guardar o
  anterior) viraram `text`, com a marca `MIGRATION-NAO-ADITIVA-CONSCIENTE(AlterColumn)`.
  ⚠️ **A regra do "registro vazio" virou UMA função** (`ProntuarioService.TemRegistro`):
  a importação grava pelo repositório em LOTES de 200 (4.287 registros um a um, com
  auditoria cada, seriam minutos num banco remoto — e milhares de linhas de trilha
  enterrariam a trilha), e precisava da mesma pergunta que o `SalvarAsync` faz. Duas
  definições divergiriam na primeira correção. A trilha é uma linha por LOTE; a
  procedência de cada registro está nele (chave e autor).
  ⚠️ **As três objeções da direção mudaram o desenho, e as três eram sobre DECISÃO FORA
  DO LUGAR.** (a) "Cadastrar a Equipe antes? Metade nem trabalha mais": o registro guarda
  o autor como texto, e a rodada seguinte REVINCULA quem foi cadastrado depois
  (`RevincularAsync`, UPDATE em lote pelo nome em `CriadoPor`/observação) — a ordem
  deixou de importar. (b) "Apontar convênio? Que o sistema acuse quando o paciente
  aparecer": nasceu `ConvenioCadastro.ADefinir` (não gera guia) e o alerta VERMELHO no
  `ElegibilidadeService` — a decisão vai para onde ela é possível, ficha a ficha, com a
  pessoa na frente, em vez de 2.021 vezes antes de importar. (c) "Importar duas vezes é
  a duplicação que não queremos": a duplicata do próprio arquivo passou a entrar FUNDIDA
  na mesma rodada (`LinhaPrevia.FundeNaLinha`, resolvida na execução por
  `ResultadoImportacao.IdPorLinha`), e o prontuário dos dois ids antigos cai na mesma
  ficha. A lição: **quando o roteiro pede à pessoa um passo antes ou depois da ferramenta,
  pergunte se a ferramenta não deveria dar esse passo sozinha** — três "faça isso antes"
  eram três defeitos de desenho.
  ⚠️ **O JSON do sistema antigo tem quebra de linha CRUA dentro das strings** — a norma
  proíbe e o `JsonDocument` recusa. `ComposicaoSmartClinic.Sanear` escapa só dentro das
  aspas; JSON que ainda assim não se lê vira registro VAZIO na prévia, nunca texto
  inventado. E `Enum`/HTML/JSON: **tudo o que decide o que o prontuário importado DIZ mora
  na Application e é puro** — os compositores recebem a linha e devolvem a evolução, e
  são eles que os testes exercitam.

- **O NOVO ATENDIMENTO RECONHECE O HORÁRIO DO DIA — lançar SOBRE ele, nunca ao lado**
  (set/2026; o mapa está em `docs/guia-no-agendamento.md` §3.7.1). A semana da migração
  expôs o buraco: 227 horários importados do Smart Clinic, todos "Consulta", e a
  secretária lançando as sessões pelo Novo atendimento — que SEMPRE criava um encaixe.
  A pergunta de duplicidade não disparava (ela olha ATENDIMENTO já registrado, e o
  horário importado não tem), e sobravam dois cartões da mesma sessão. ⚠️ **E o parado
  levava a evolução**: a evolução importada depois (sem `AgendamentoId`) é distribuída
  na ordem da hora marcada (`EvolucaoDoHorario`), o importado das 09h00 vinha antes do
  encaixe das 09h12 — a sessão de verdade ficava em "Sessões sem evolução".
  `AgendaService.LancarNoHorarioAsync` é o gesto atômico do avulso aplicado a um horário
  que JÁ EXISTE: modalidade da tela passa a valer para ele (trilha quando mudou),
  `ChegadaEm ??=`, e o atendimento nasce pendurado nele pelo MESMO `ConfirmarNucleoAsync`.
  Com a chave "guia no agendamento" ligada, a modalidade nova regera pelo MESMO
  `AjustarAoRemarcarAsync` do Remarcar — segunda regra de regeração divergiria.
  As decisões: **nulo mantém** (sem código de modalidade, sem profissional, sem sala: o
  horário fica com o que tem — a regra da parcela 68); **a observação do horário não se
  perde** ("Importado do Smart Clinic · Consulta" fica, a nota da tela entra abaixo);
  **recusa com frase que diz o que fazer** para realizado (a outra máquina concluiu) e
  cancelado/falta (reabra ou lance como encaixe); **a caixinha "criar um encaixe
  separado"** é a saída do caso legítimo e só existe no modo "lançar agora"; **o botão
  DIZ** "Lançar no horário das 09h00 e gerar 2 guias"; e **leitura que falha escreve o
  terceiro estado** ("não foi possível conferir — lance pela Fila") em vez de cair no
  encaixe em silêncio, que é justamente a duplicata que o aviso existe para impedir.
  A lição que generaliza: **quando uma tela cria um registro que outra tela também cria,
  pergunte se o registro já EXISTE antes de criar** — a duplicidade que a capa pegava era
  a de atendimento; a de HORÁRIO passou seis parcelas invisível porque nenhuma das duas
  telas perguntava à outra.

- **O ACERVO DE ARQUIVOS DO SISTEMA ANTERIOR, e por que ele não virou "anexo"** (set/2026
  — a clínica recebeu a segunda exportação: 756 PDFs de receitas de 113 pacientes, com um
  índice dizendo de quem é cada um). A receita pertence à PESSOA e não a uma sessão, e os
  dois lugares que já guardam arquivo clínico foram MEDIDOS e recusados: `AnexoProntuario`
  exige `EvolucaoId` (forçar o PDF a uma evolução inventaria uma consulta que não houve —
  o argumento do laudo em arquivo) e `ResultadoExame` afirma que aquilo É um resultado —
  gravar uma receita ali seria mentir sobre a natureza do registro. Nasceu
  `AnexoPaciente`: a **décima raiz clínica**, no `CatalogoRegistroClinico`, com os oito
  lugares percorridos no mesmo commit (guarda, exportação, art. 18 II, cascata do excluir,
  rede de tradução) — o `ConjuntoClinicoTests` é quem cobra.
  As decisões: bytes em tabela 1:1 (a lista de 756 arquivos não pode arrastar 756 PDFs);
  **o MESMO teto** do anexo de prontuário; **cancela-se com motivo**, nunca se apaga; a
  montagem é pública e estática (`AnexoPacienteService.Montar`) porque tem DOIS chamadores —
  a tela e a importação em lotes — e duas validações divergiriam.
  ⚠️ **A ficha é achada pelo id do sistema anterior; o NOME só resolve quando é ÚNICO.**
  Casar "Maria Silva" com uma de duas põe a receita na ficha errada sem falhar em lugar
  nenhum — a homônima fica de fora COM o motivo. Pela mesma razão o ZIP se importa DEPOIS
  do pacote de pacientes, e a prévia diz isso em vez de deixar 756 linhas caírem em "sem
  paciente" caladas.
  ⚠️ **"Fechou" na conferência não quer dizer "tudo entrou"**: quer dizer que o que não
  entrou está LISTADO com a razão. O teste da conferência nasceu esperando o contrário e
  a regra já escrita do pacote é que estava certa — duas semânticas de "fechou" no mesmo
  botão fariam a direção ler "conferido" com significados diferentes a cada ZIP.
  ⚠️ **A LINHA DO TEMPO É O LEITOR DA FICHA — e a natureza nova nasceu fora dela** (o
  cliente: *"importei a pasta zipada mas não encontrei os arquivos dentro de cada
  ficha"*). Os oito lugares foram percorridos, o `ConjuntoClinicoTests` ficou verde, e a
  ficha da Recepção não mostrava nenhuma das 756 receitas: `LinhaDoTempoClinica.Montar`
  recebe listas TIPADAS, uma por natureza, e o teste dele afirmava as quatro que existiam
  em vez de percorrer o catálogo — **asserção que enumera à mão é asserção que a próxima
  natureza não alcança**. A porta que eu tinha construído (a região no Consultório) era a
  do app do MÉDICO; quem confere a importação abre a ficha no Gerente/Recepção. Agora o
  arquivo entra no montador, e abrir/cancelar passam por UM ponto do shell
  (`ArquivosDaFicha`), porque são três portas e a cópia que ficasse para trás abriria o
  PDF sem registrar quem leu.
  ⚠️ **Snapshot escrito à mão: no 1:1, o bloco de relacionamento do lado DEPENDENTE (o
  que declara `WithOne("Arquivo")`) vem ANTES do principal que chama `Navigation("Arquivo")`**
  — o `BuildModel` roda os blocos em sequência, e a navegação só existe depois do
  `WithOne`. Invertido, o snapshot inteiro LANÇA ao ser carregado ("Navigation … was not
  found"), e o `Snapshot_do_EF_esta_em_dia` é quem pega; copie a ordem do par
  `ArquivoResultadoExame` → `ResultadoExame`, nos dois arquivos (snapshot e Designer).

### Convenções

- **⛔ TELA, BARRA OU BOX NOVO SEGUE O DESIGN SYSTEM — SEMPRE** (decisão da direção,
  ago/2026, ao aprovar o handoff dos painéis; a referência é `docs/design-system/` e o
  espelho de tokens conferido no CI). As cinco metades da regra:
  1. **Valor visual sai de TOKEN, nunca de número solto.** `FontSize` numérico novo, cor
     em hexadecimal nova e raio/espaçamento inventado são dívida no ato — o mockup e o
     kit foram gerados DOS tokens do repositório, e um valor fora deles é o primeiro
     ponto em que as duas coisas param de bater.
  2. **Antes de desenhar, procure o componente que já existe**: `CardKpi` (anatomia
     fixa — `CardKpi.Icone` à esquerda do rótulo NA MESMA LINHA, o `BotaoMenuKpi` ("⋯")
     à direita dela, `CardKpi.Rotulo` 13, `CardKpi.Valor` 28, `BarraDado` de 6 px só
     onde a fração é real, e `CardKpi.Delta` como ÚLTIMA linha onde há período; o
     ESTILO tem MinHeight 88 — a altura fixa é decisão POR TELA de painel, com a conta
     da pilha escrita ao lado do `Height`), `ItemBarraRotulada`, `GraficoLinha`,
     `Badge.*`, `ChipFiltro`, `EstadoDaTela`, `Card`. Desenho próprio para uma pergunta
     que um componente já responde é a segunda definição que diverge na primeira
     correção.
  3. **Iconografia é Segoe Fluent (`FonteIcones`), com glifo SEMÂNTICO consistente**: a
     mesma métrica leva o mesmo glifo em todo o sistema (E896 entrada · E898 saída ·
     E7BA vencido/falta · E73E baixado/atendido · E823 pendente/previsto · E7C3 guia —
     a tabela completa está em `docs/design-system/tokens.md` §Iconografia). E **a cor do
     ícone não é escolha do ícone: ela repete a que a tela deu ao NÚMERO** (Acento no
     neutro; semântica só onde o valor é pintado; Acento.Tint no cartão de acento).
  4. **Componente ou correção do shell é PORTADO ao design system do faturamento no
     MESMO commit** — os dois não se referenciam (o débito permanente da Fase 4), e a
     cópia que fica para trás é onde a capacidade some (parcelas 61, 75, 90).
  5. **A regra de leiaute do `README.md` continua sendo o portão** — o design system diz
     COMO cada peça se desenha; as três perguntas do README dizem SE a peça cabe ali.
- Ao adicionar um **instrumento de avaliação**: nova classe em `Domain/Avaliacoes/`
  implementando `IInstrumentoAvaliacao`, uma linha em `RegistroInstrumentos`, as
  especialidades declaradas e o fluxograma coberto em `InstrumentosAvaliacaoTests` — a
  mesma convenção do convênio, e pela mesma razão: peso errado num item produz escore que
  continua parecendo válido.
- Ao adicionar uma **medida clínica**: nova entrada em `CatalogoMedidas`, com unidade,
  piso/teto de PLAUSIBILIDADE (não de normalidade) e as faixas publicadas, e cobertura em
  `MedidasClinicasTests`. Não precisa de migration — o tipo é um código, e o que a colheita
  grava são as colunas que já existem. Faixa que depende do sexo vai em `FaixasMasculino` /
  `FaixasFeminino`; sem leitura publicada, deixe `Faixas` vazio em vez de inventar "normal".
- Ao adicionar um convênio fixo: nova classe em `Domain/Regras/`, registrar em `RegistroRegras`,
  adicionar ao enum `Convenio`, cobrir o fluxograma com testes em `RegrasFaturamentoTests`.
- Ao adicionar uma **permissão**: bit novo no fim do enum `Permissao` (nunca reaproveite um bit —
  ele é gravado como INTEIRO em bases de produção), rótulo em `PerfisAcesso.Rotular`, entrada nos
  perfis padrão que já faziam aquilo, e as **duas barreiras** em todo comando alcançado.
  `PermissoesFaturamentoTests` cobre bit único, rótulo em português e o padrão de cada perfil — é
  ele que impede uma atualização de tirar em silêncio o que a pessoa fazia ontem.
- Toda tela que escreve trata as exceções e nunca derruba o app (`DispatcherUnhandledException`
  como última rede). O **feedback tem dois canais, e a escolha não é livre**:
  - **Mensagem inline** (propriedade `Mensagem` + `MensagemEhErro` no VM, desenhada perto da
    ação) — é o padrão para formulário: validação, erro de gravação e confirmação que precisa
    ficar na tela enquanto o usuário corrige. Usado em 13 dos 18 VMs.
  - **Snackbar** (`ISnackbarService`) — só para confirmação passageira de ação que não tem
    lugar natural na tela (ex.: salvar em Configurações). Some em 4s, então nunca para erro
    que exija correção.
- **Degradação silenciosa tem que deixar rastro.** O projeto degrada de propósito em vez de
  derrubar o app (aviso que não carregou, foto que não abriu, update que falhou) — e isso é
  certo. Mas todo `catch` desses grava `LogErros.Registrar(contexto, ex)` (Desktop) ou
  `Diagnostico.Registrar` (Application/Infrastructure, com o sink ligado no `App.OnStartup`).
  O log é um `.txt` por mês em `<pasta da instalação>\logs` (fora da pasta versionada do
  Velopack, para sobreviver às atualizações), rotacionado em 2 MB e expurgado em 90 dias;
  Configurações → Clínica/prestador tem o caminho e o botão "Abrir pasta de logs".
  Só ficam mudos: o próprio logger, decodificação de quadro da webcam (30x/s) e cancelamento
  esperado. **Falha nunca pode ser exibida como sucesso** — se a checagem não rodou, a tela
  mostra um terceiro estado ("não verificado"), como o painel faz com a rodada de pendências.
- **A sidebar da suíte é agrupada por TEMA, não por módulo** (parcela 7). `ItemMenuModulo`
  tem duas coisas que só parecem uma: `Grupo` (`GrupoSidebar` — GESTÃO · PACIENTE ·
  FINANCEIRO · INTELIGÊNCIA) é **onde o item aparece**, e `ModuloNome` é **quem sabe
  construir a tela**. Antes o cabeçalho era o nome do módulo carregado, e o Gerente — que
  carrega os três — via "Recepção / Financeiro / Direção": uma sidebar que explica a
  arquitetura para quem só quer saber onde mexe no paciente. Item novo declara o `Grupo`.
  Quando um item da proposta cobre vários assuntos (é o caso de "Faturamento (TISS)"), use
  **sub-abas** dentro dele em vez de criar entradas novas — a proposta tem um item ali.
  A abertura do app é o item com `Inicial = true` (parcela 22), não o primeiro da lista;
  navegação entre módulos passa por `NavegacaoSuite` + `ChavesSuite` — chave que só um
  módulo usa continua sendo `const` do módulo dono, porque não é contrato de ninguém.
- **O circuito entre os módulos é testado de PONTA A PONTA** (`CircuitoCompletoTests`,
  parcela 33). O resto da suíte testa trechos — o fechamento sozinho, a conciliação
  sozinha, a glosa sozinha —, e cada um pode passar com o circuito partido: o que liga um
  módulo ao outro aqui **não é chamada de método, é chave estrangeira**. Se
  `LancamentoFinanceiro.CodigoFaturamentoId` deixasse de ser preenchido, nenhum teste de
  unidade falharia e a guia nunca sairia da conciliação. Os quatro circuitos cobertos são
  os da parcela 27: Recepção → Faturamento → Financeiro (a guia sai da lista por **ter
  receita**, não por alguém marcá-la), a **glosa que volta** (cancelada a receita, a guia
  reaparece sozinha), a **NC que reabre quando o paciente volta** e o **painel da direção**,
  montado com os onze serviços à mão — que é o que prova que o grafo do Gerente fecha sem
  o DI resolver nada por baixo. O painel é o teste certo para o fim do circuito porque
  ele **não calcula nada**: elo partido não vira erro, vira número ZERADO, e zero por
  defeito é indistinguível de zero porque o dia foi fraco.
- **Cobertura é medida, não estimada.** `dotnet test --collect:"XPlat Code Coverage"` (o
  `coverlet` já está no `.csproj`). A leitura útil é a da **camada de aplicação sem as
  migrations** — elas têm milhares de linhas e afundam o número global para ~21%. Hoje:
  **91,6%**, com 26 métodos sem execução (eram 39). A varredura por "método com zero
  execução" é o que achou `RemarcarEmLoteAsync`, `ApuracaoMensalAsync` e os dois PDFs do
  faturamento — 323 linhas de desenho aprovadas no CI que nunca haviam rodado uma vez.
  **Serviço testado não é serviço executado**, e é assim que se descobre a diferença.
- **Duas barreiras locais contra erro de compilação WPF, e elas se dividem por linguagem.**
  - `tools/compilar-sombra.py` compila **o C#** dos sete projetos WPF. Ele recompila os
    mesmos `.cs` num projeto `net8.0` comum que referencia as *reference assemblies* do
    WPF baixadas do NuGet, e substitui o compilador de marcação — a etapa que o SDK de
    Linux não tem — por um gerador próprio de `.g.cs`: para cada XAML com `x:Class`, emite
    a parte `partial` com `InitializeComponent()` e um campo por `x:Name`. **`x:Name`
    dentro de `Style`/`ControlTemplate`/`DataTemplate` NÃO vira campo** (o WPF resolve por
    `FindName` em runtime) — gerar campo ali inventaria erro que o CI não tem. Ele nasceu
    de quatro erros reais numa noite só (`CS0019` de `DayOfWeek % 7`, `CS0117` de membro
    inexistente, `CS1503` de argumento posicional caindo no parâmetro errado e `CS0579` de
    `[RelayCommand]` órfão), e é **autotestado contra os quatro**.
  - `tools/verificar-suite.py` cobre **o XAML e o que é textual**: chaves do design system,
    pack URIs, dicionário que usa token sem mesclá-lo (quebra em runtime, não no build),
    **aridade de `new XViewModel(...)` escrito à mão** (checagem 7), **membro `required`
    não inicializado** (checagem 8), **variável de padrão colidindo com outra declaração do
    mesmo método** (checagem 9) e **tipo público duplicado no mesmo projeto e namespace**
    (checagem 12, `CS0101`). A 9 tem uma assimetria que engana: `is { } f` entra no escopo
    do MÉTODO, enquanto `foreach (var f in ...)` entra num escopo próprio — por isso dois
    `foreach` com o mesmo nome são irmãos e legais, e a checagem parte só das variáveis de
    padrão.

  **Rode as duas antes de todo push.** O que sobra para o CI é o compilador de marcação de
  verdade (`MC*`) e o empacotamento — não invente heurística para isso.
- **Tamanho de tela tem TETO e PISO, e o piso é o que corta.** A checagem 15 olha o teto
  (janela que nasce maior que o monitor de 1366×768 do balcão); a **16** olha o piso, e é
  o lado que o usuário alcança sozinho: janela redimensionável sem `MinWidth` encolhe até
  o mínimo do WPF e não há leiaute que resista. A diferença é que o teto tem conserto —
  arrasta-se a borda — e o **piso não**: quem encolheu demais não sabe qual largura
  devolve a tela ao normal. A **17** cobre o texto: `StackPanel` horizontal dá a cada
  filho a largura que ele PEDE e nunca dobra a linha, e `WrapPanel` dobra ENTRE os filhos
  mas nunca DENTRO de um — então `{Binding Mensagem}` sem `TextWrapping` sai pela direita
  e **não se lê justamente o texto que explica por que a ação falhou**. As duas valem
  também no faturamento congelado, pela razão da 15 (nada a ver com arquitetura, o usuário
  sente todo dia) e por uma segunda: os três casos reais estavam lá, e checagem que não
  alcança o lugar onde o defeito estava é checagem que passa sozinha.
  Quando o conteúdo tem um piso REAL, sobe-se o mínimo em vez de espremer: as figuras do
  mapa corporal são `Canvas` de 220×460 casados com `PontoMapaItem.LarguraFigura`, a mesma
  const que posiciona o marcador e converte o clique de volta — encolher a figura
  espalharia os pontos em silêncio, então quem cede é a janela (`EvolucaoWindow`, mínimo
  960). Quando o piso é gratuito, ele some: largura fixa de campo dentro de barra vira
  coluna elástica (`RetornoLoteWindow`), que de quebra acaba com o vão morto ao lado dele
  quando a janela está maximizada.
- **Gráfico é desenhado com os tokens, sem biblioteca** (`Controls/Graficos.cs`,
  `Componentes/Graficos.xaml`). Os cinco apps se auto-atualizam por Velopack e uma
  dependência de UI nova é risco desproporcional. Duas regras do desenho: **valor nulo
  interrompe a linha** (um mês sem horário fechado desenhado como 0% inventaria um mês
  perfeito) e **o eixo ancora no zero** (escala que começa no menor valor transforma
  variação de 2% num despencar visual). A fração das barras vem **normalizada do
  ViewModel** — o DataTemplate não enxerga os irmãos da série.
- **CSV para o Excel em português** (`Componentes/ExportacaoCsv.cs`): separador **ponto e
  vírgula** e **BOM UTF-8**. Com vírgula o arquivo abre com tudo numa coluna só; sem BOM,
  "Ocupação" vira "OcupaÃ§Ã£o". Métrica sem base de cálculo exporta "—", igual à tela —
  trocar por 0 faria a planilha calcular média sobre um número que não existe.
- **Escolher paciente é um componente só**: `SeletorPacienteViewModel` (VM) +
  `ItemPacienteSeletor` (`Styles/Componentes/Pacientes.xaml`). Ele já resolve limite no SQL
  (`BuscarPacientesAsync(termo, limite)` — nunca `Take()` depois de materializar), agrupamento
  das teclas e descarte de resposta fora de ordem. Tela nova que escolhe paciente usa ele; não
  reescreva a busca. A **listagem** de Pacientes é outra coisa (linha com ações) e usa o mesmo
  VM com `limite: null` + `Refinar`.
- `docs/atualizacoes.md` documenta o mecanismo de auto-update; `docs/design-system/` documenta
  tokens, componentes, atalhos e acessibilidade da UI.
- **CPF do paciente não se repete, e a recusa mora na ESCRITA** (`PacienteService`,
  parcela 57): duas fichas da mesma pessoa partem o histórico em dois — metade dos
  atendimentos, das guias e do prontuário fica em cada uma. Não é fraude nem erro de
  digitação: é a mesma pessoa cadastrada de novo por quem não achou a ficha antiga, e por
  isso a mensagem **diz o nome de quem já tem aquele CPF** — é o que transforma um erro
  numa instrução ("é este aqui, abra a ficha dele").
  **Não é índice único**, e a razão é a mesma que já vale para o CPF do profissional
  (parcela 45): a migration roda no `MigrateAsync` da ABERTURA do app, inclusive do
  faturamento em produção, e a criação do índice falharia se a base já tivesse duplicata —
  quem não abriria seria o sistema que fatura. E ela TEM chance de ter: até aqui nada
  impedia.
  ⚠️ **A regra é simples de propósito: CPF de OUTRA ficha é recusado na criação e na
  edição.** Houve uma versão que abria exceção para a ficha antiga já duplicada, para não
  travar a correção do telefone dela; a direção dispensou — as duplicatas que já existem
  serão apagadas direto no banco (Neon), e daí em diante só precisa existir o impedimento.
  **Regra com exceção que ninguém vai exercer é código a mais para manter e mais uma
  resposta possível para a mesma pergunta.** O efeito colateral fica fixado em teste em vez
  de descoberto no balcão: enquanto a limpeza não acontece, a ficha duplicada não salva.
  Dois cuidados que o código não conta sozinho: a comparação **ignora máscara nos dois
  lados** (a coluna aceita 30 caracteres e guarda o que foi digitado; a base tem linhas
  anteriores à normalização, com "123.456.789-00", e comparar o texto cru deixaria passar
  justamente o duplicado que já existe) e a limpeza acontece **no banco**, com o `replace`
  do SQL, porque carregar a carteira inteira a cada Salvar seria uma varredura completa
  numa base remota. **CPF em branco continua sendo o caso normal** — criança, paciente
  cadastrado pela carteirinha, quem chegou sem documento —, e vazio vira NULO para dois
  documentos "" não serem iguais.

- **GUIA NÃO É ATENDIMENTO — e o sistema materializava a pendência como HORÁRIO**
  (parcela 58; a cliente mandou a foto de uma paciente sem agendamento ocupando a fila do
  dia e a agenda). `AgendaService.ConfirmarPresencaAsync` criava, ao confirmar a presença,
  um `Agendamento` de verdade (`OrigemAgendamento.RetornoSugerido`) na data prevista do 2º
  código, às 9h, "para não esquecer de obtê-lo".
  O 2º código é obtido +24h depois **pela SECRETÁRIA, no sistema do convênio** — o paciente
  não volta para nada. Materializá-lo como horário punha na fila do balcão e na agenda dos
  MÉDICOS uma pessoa que não tem hora marcada e não vai aparecer.
  ⚠️ **E não era ruído visual.** O cartão fantasma vinha com "Chegou / Entrou / Falta /
  Cancelar": um clique em Entrou → Finalizar lança um atendimento NOVO e gera guias NOVAS
  para uma sessão que nunca aconteceu — faturamento inventado a partir de uma pendência de
  faturamento. É a inversão exata do que o produto existe para fazer.
  **Não faltava lembrete, sobrava um no lugar errado**: o 2º código já nasce como
  `CodigoFaturamento` com `DataPrevistaFaturamento`, aparece no painel de pendências com
  semáforo, entra na rodada bloqueante ao vencer o prazo e é mostrado ao balcão pelo
  `PainelRecepcaoService` junto dos pacientes do dia.
  O valor do enum FICA (é gravado como texto e há linhas assim em produção; apagá-lo
  quebraria a leitura delas), e o selo "Retorno do 2º código" segue na tela justamente
  para a clínica reconhecer e limpar as que sobraram.
  A lição, que é a mais cara desta lista: **ao ligar dois módulos, pergunte se o que
  atravessa é o FATO ou só o lembrete dele.** Pendência de faturamento é fato do
  faturamento; ela se mostra onde se resolve, e não vira agenda de quem atende.

- **`EstadoDaTela` com `Visibility` AMARRADA: o binding morre e o vazio vaza pela tela**
  (parcela 58 — o cliente mandou a foto de "Nenhuma sessão registrada" escrito por cima da
  lista de pacientes CHEIA, no Prontuário e nas Prescrições da Recepção). O componente
  decide a própria `Visibility` em `Recalcular()`, atribuindo um valor **local** — e em WPF
  valor local atribuído por código **substitui o binding**. A tela ligava a visibilidade a
  "estou mostrando o prontuário"; na primeira mudança de `Itens`, `Carregando` ou
  `NaoVerificado` o `Recalcular` sobrescrevia e o binding deixava de existir. Daí em diante
  o vazio aparecia quando a LISTA estava vazia, e não quando a tela dele estava aberta.
  A saída é `Ativo`, que entra no CÁLCULO em vez de brigar com ele; e o componente volta
  para dentro da região a que pertence, como último filho do Grid dela — na raiz da tela
  ele cobre tudo, que é o defeito que a PR #113 já tinha corrigido noutras telas. Virou a
  **checagem 30**.
- **`SharedSizeGroup` sem `Grid.IsSharedSizeScope` não alinha NADA** (parcela 58): cada
  linha de uma lista é um Grid próprio, então sem o escopo as larguras são resolvidas POR
  LINHA — a linha que tem o selo "Carteirinha vencida" fica com a última coluna mais larga
  e empurra o telefone daquela linha para a esquerda. A lista deixa de ter colunas e vira
  uma pilha de linhas que por acaso se parecem. O `ItemPacienteLinha` trazia o aviso
  escrito no próprio comentário ("o escopo é declarado por quem monta a lista") e **três
  das quatro telas que o usam esqueceram**. Contrato que depende de alguém lembrar é o que
  a **checagem 31** existe para substituir.
  ⚠️ A checagem nasceu CEGA e o autoteste é que mostrou: ela procurava
  `IsSharedSizeScope` no texto do arquivo, e o **comentário que explica a regra** satisfazia
  a busca — a tela ficava desalinhada com a checagem verde. É o inverso da lição da
  checagem 19 (lá a prosa fazia disparar, aqui fazia silenciar), e o silêncio é pior:
  ninguém percebe uma checagem que passou. **Toda checagem que procura uma marca no texto
  tem de tirar os comentários antes.**

- **A AGENDA era uma pilha de cartões; agenda é LINHA DO TEMPO** (parcela 58 — *"o iClinic
  está dando um banho na gente nessa questão de fila/agenda"*). A da Recepção empilhava os
  horários numa coluna por profissional, contíguos: o das 9h colado no das 15h. Ela
  respondia **"quais horários existem"**, e a pergunta do balcão com o paciente na frente é
  outra — **"quando cabe?"**. Numa pilha, ela só se responde lendo cartão por cartão; numa
  grade, o **vazio tem tamanho**, e é isso que a torna legível de longe.
  As decisões:
  (a) **O vão livre é CLICÁVEL e abre o formulário já na hora e na coluna clicadas**
  (`ProfissionalPreferidoId` — um ID, não a entidade, porque a lista de profissionais é
  carregada do banco DEPOIS do construtor, e atribuir a entidade de fora abriria o combo em
  branco justamente na coluna que a pessoa apontou). Redigitar a hora que se acabou de
  apontar com o dedo é onde ela sai errada.
  (b) **A janela de horas é a padrão da clínica ESTICADA pelo que existe no dia** — nunca
  00:00–23:59, que daria 48 faixas vazias para rolar antes do primeiro paciente; e uma
  sessão às 6h30 puxa a grade para cima em vez de ficar de fora.
  (c) **A sessão de uma hora marca a faixa seguinte como CONTINUAÇÃO**, senão a meia hora
  de dentro dela apareceria vaga e a recepção marcaria por cima.
  (d) **Cancelado e falta ficam apagados, nunca sumindo** (a regra da folha do dia, parcela
  29) — e o vão deles volta a ser oferecido, porque o horário de fato vagou.
  (e) **Colunas de largura IGUAL** (`UniformGrid`), que é o que mantém cabeçalho e faixas no
  mesmo prumo sem uma única `SharedSizeGroup`; o cabeçalho rola JUNTO, porque congelá-lo
  exigiria dois `ScrollViewer` sincronizados à mão — mais peça para desalinhar do que valor.
  (f) **Os sete botões saíram do cartão** e foram para a janela do horário. Sete botões em
  cada um de quarenta cartões é uma tela em que o olho para de distinguir o frequente do
  raro — e não caberiam em 46 px. A janela **não executa nada**: devolve a INTENÇÃO, e quem
  age é a ViewModel, para as sete ações continuarem com um dono só (permissão,
  recarregamento e erro no mesmo lugar).
  (g) **A lista de espera saiu da faixa lateral de 320 px** e virou botão com a contagem no
  rótulo. Ela se consulta quando um horário VAGA; a agenda é o que se olha o tempo todo.
  E o "quem chamar para as 14h" ABRE a janela em vez de repintar um painel que ninguém
  estava olhando.
  ⚠️ **A janela de cima passou a ser dona do próximo modal** (`Dono()` = a janela ATIVA, não
  `MainWindow`): com a lista de espera aberta, o formulário nasceria ATRÁS dela, e a
  recepção concluiria que o clique não fez nada.

- **`WrapPanel` que nunca dobra a linha** (parcela 58, checagem 32): a barra da agenda tem
  nove controles. Num `Grid` de coluna `Width="Auto"` — ou docado num `DockPanel`, ou dentro
  de um `StackPanel` horizontal — o `WrapPanel` é medido com largura **infinita**: ele
  alinha tudo numa linha só e empurra o título para fora da tela. O `Auto` engana
  especialmente, porque a intenção declarada ("ocupa o que precisar") é exatamente o que
  impede a quebra. A saída é fazê-lo o filho que **PREENCHE** e encostá-lo com
  `HorizontalAlignment`. Nenhuma rede pegava — XAML bem-formado, nada lança —, e o defeito
  **não reproduz na máquina de quem escreve**: só na largura errada.

- **Kanban se ARRASTA — e o arrasto não pode ser o único caminho** (parcela 58): a fila
  ganhou arrastar-e-soltar entre as cinco raias, com realce da raia de destino (alvo de
  soltar sem realce é alvo que se erra, e errar aqui manda o paciente para a coluna do
  lado). Três regras:
  **as transições legais são EXATAMENTE as dos botões** — não uma segunda regra escrita no
  arrasto, porque duas definições de "para onde este cartão pode ir" divergem na primeira
  correção e a de baixo é a que ninguém lembra de ajustar; **para trás anda um passo por
  vez**, porque `VoltarEtapaAsync` apaga UM carimbo de hora e apagar três de uma vez para
  atender a um arrasto longo inventaria uma linha do tempo que não aconteceu; e
  **movimento impossível não é silêncio** — a tela diz por que não deu (a lição da parcela
  41 aplicada ao gesto).
  O handler de `PreviewMouseLeftButtonDown` **não marca o evento como tratado** e o arrasto
  só começa depois do limiar do sistema: sem isso o clique nunca chegaria aos botões do
  cartão, e o quadro perderia a metade que funciona para quem não arrasta nada.
  ⚠️ **O menu "⋯" e o arrasto moram em CÓDIGO, não em XAML**: um `ContextMenu` declarado
  vive num `Popup`, fora da árvore visual da tela, e os comandos precisariam de
  `PlacementTarget.Tag` para chegar ao ViewModel — binding que erra o caminho falha em
  RUNTIME, calado, que é a categoria que nenhuma rede local pega. Em código, o
  `compilar-sombra` pega.

- **Quem LANÇOU o atendimento e o agendamento** (parcela 58): a direção pediu para ver de
  quem é cada lançamento. A trilha de auditoria responde "quem fez isso?" desde a parcela
  21 — mas ela é outra tela, filtrada por período, e a pergunta que se faz olhando a agenda
  é sobre **aquela linha, agora**. `Agendamento.CriadoPor/CriadoEm` e
  `Atendimento.LancadoPor/LancadoEm` são migration **aditiva** (quatro `AddColumn`), e o
  operador chega da TELA (`SessaoUsuario.Atual.Operador`): o serviço não lê a sessão, pela
  razão de sempre — é o chamador que sabe quem está logado, e no balcão duas pessoas
  dividem a máquina.
  **Nulo é decisão**: linha anterior à parcela guarda `null`, nunca `""` nem o usuário do
  Windows, e a tela escreve *"marcado antes de o sistema passar a registrar quem lança"* —
  em branco não se distingue de "não carregou".
  A confirmação de presença leva **quem CONCLUIU**, não quem marcou o horário semanas
  atrás: são dois atos e duas pessoas, e o segundo é o que gera as GUIAS.

- **A Agenda saiu do perfil Faturista** (parcela 58): a direção pediu *"retire a Agenda do
  módulo faturamento ou permita granular que o faturista possa ou não abrir um
  agendamento"*, e a razão é a mesma que derrubou o agendamento fantasma do 2º código —
  horário aberto do lado do faturamento aparece na fila do balcão e na agenda de quem
  atende. A escolha foi **granular, não amputação**: `PerfilAcesso.Faturista` perdeu
  `EditarAgenda` e **manteve `VerAgenda`**, porque conferir o dia é parte de faturá-lo, e
  tirar as duas juntas seria tirar capacidade de quem a usava (a regra 3 do bloco do
  faturamento). Devolver é um clique em Acessos.
  As **duas barreiras** entraram no `AgendaViewModel` do faturamento no mesmo commit —
  `PodeAgendar` nos botões e `Exigir` em `AbrirCadastroAsync`, que é o ponto único por onde
  Novo, Remarcar e "agendar na faixa" passam. Bit sem guarda seria só uma caixinha na tela
  de Acessos.

- **DUAS PORTAS, UMA ESTEIRA — e a diferença entre elas não tinha sido decidida, tinha
  sido esquecida** (parcela 60). Havia dois caminhos para criar atendimento, e do lado do
  faturamento eles eram idênticos — a guia nascia certa —, o que é exatamente por que
  ninguém notou. Em volta dela, não: **pela agenda** (Fila → Finalizar) o
  `FechamentoSessaoService` fazia QUATRO coisas — a guia nasce, o pacote debita, o insumo
  sai do estoque, o dinheiro entra no caixa; **pelo avulso** acontecia UMA.
  O custo era invisível e diário: paciente com pacote de dez sessões lançado pelo avulso
  consumia sessão **sem debitar** (a clínica atendia de graça, e é o que o `PacoteService`
  existe para impedir), e o particular que pagou no balcão não aparecia no caixa — o mês
  fechava com uma diferença que não tinha nome. Não havia uma linha de comentário
  explicando a escolha porque **não houve escolha**.
  A saída não foi escolher uma das duas telas: foi ver que **quem chega sem horário está
  pedindo um ENCAIXE**, e encaixe a agenda já sabia fazer. "Novo atendimento" continua no
  menu (decisão da direção) e por dentro faz `AgendarAsync(hora real, encaixe: true)` →
  `RegistrarChegadaAsync` → **a MESMA `FechamentoSessaoWindow` da Fila**. Não há uma
  segunda tela de decisão: duas telas para a mesma pergunta divergem na primeira correção.
  ⚠️ **O efeito estrutural é a amarra**: `AtendimentoService.LancarAsync` ficou com **UM
  ÚNICO CHAMADOR** em todo o sistema (`AgendaService.ConfirmarPresencaAsync`). Ponto único
  deixou de ser documentação e virou o que o compilador mostra. `registrarNaAgenda` morreu
  — e com ele o **agendamento fantasma às 9h fixo**, sem profissional, que o avulso criava
  porque `Atendimento` só guarda `DateOnly` e não havia hora para copiar; ele aparecia na
  grade num horário em que ninguém foi atendido e CONTAVA na ocupação do dia. Não era o
  fantasma da parcela 58 (aquele nascia `Agendado` e tinha o botão "Entrou", que fabricava
  guia): era ruído na grade e um número de ocupação errado — a mesma família, sem o
  estrago.
  **O que a unificação custou, e por que valeu**: `Agendamento.PrimeiroCodigo` (migration
  aditiva). A escolha de qual código o convênio libera primeiro é da tela, e precisava
  atravessar o horário para chegar ao motor — sem a coluna, unificar teria custado a
  feature. De quebra ela ficou disponível também no agendamento normal.
  **Cancelar o fechamento não desfaz o encaixe**: o horário fica com o check-in carimbado,
  e o paciente aparece na Fila em "Na recepção". É a verdade — ele chegou —, e apagar o
  registro sumiria com o único sinal de que há alguém no balcão esperando.
  `Avulso_e_agendado_produzem_os_mesmos_fatos` é a amarra em teste: ele falha se alguém
  abrir uma terceira porta que pule algum dos fatos, que é como a segunda foi aberta.
- **ATENDIMENTO QUE ENTRA NO SISTEMA JÁ GERA GUIA — a unificação da parcela 60 pendurou a
  guia no clique errado** (parcela 65; o cliente achou em produção, no dia seguinte). Ao
  unificar a esteira, a guia deixou de nascer no lançamento e passou a nascer na
  **confirmação da `FechamentoSessaoWindow`**. Quem fechasse aquela janela ficava com o
  horário na agenda, o paciente marcado como presente e **nenhuma guia** — e, como o
  encaixe tinha sido criado, a tela parecia ter funcionado.
  Em **12/08/2026** a mesma paciente foi lançada **três vezes em 71 segundos** (encaixes
  161, 162 e 163, todos com `ChegadaEm` carimbado e `AtendimentoId` nulo): a recepcionista
  não via guia nenhuma e tentava de novo. Zero guias, três cartões na fila para uma sessão.
  A mensagem que explicava existia — inline, numa tela que a pessoa já dava por concluída.
  **Ninguém tenta três vezes em um minuto se a mensagem chegou.**
  A regra que a direção fixou é uma só e vale para as DUAS portas: **registrou o
  atendimento, a guia nasce e vai para o faturamento**. Pacote, insumo e caixa são o passo
  SEGUINTE e não podem condicionar nem desfazer a guia — é a MESMA hierarquia que o
  `ConcluirAsync` já aplicava entre os quatro fatos (só o atendimento derruba a operação;
  os outros três viram aviso), agora estendida ao **momento** em que cada um acontece.
  `FechamentoSessaoService.RegistrarAtendimentoAsync` é o ponto único, e é **idempotente
  por agendamento**: quem já tem `AtendimentoId` é reaproveitado, nunca reconfirmado —
  dois cliques no Finalizar não viram duas guias.
  ⚠️ **A recusa de "agendamento já realizado" teve de SAIR do `PrepararAsync`.** Com a guia
  antes da janela, `Realizado` virou o caso NORMAL da tela, e a recusa derrubaria
  justamente a janela que veio depois do lançamento correto. Quem impede a duplicidade é o
  reaproveitamento do atendimento e o `AtendimentoJaConsumiuPacoteAsync` — nunca uma
  exceção na cara de quem clicou duas vezes procurando a guia que ele não viu.
  **A janela só abre quando há o que decidir** (`RegistroAtendimento.TemDecisao`: pacote,
  dinheiro ou insumo). Para o paciente de convênio sem pacote não há uma única pergunta a
  responder ali, e pedir confirmação de uma tela vazia é como se ensina alguém a fechar
  janela sem ler — que é a causa raiz do dia 12.
  **E o segundo clique passou a custar caro**, então ganhou pergunta: antes de criar o
  encaixe, a tela confere se o paciente já tem atendimento naquele dia e pergunta. É
  PERGUNTA e não recusa — o paciente pode ter a sessão da manhã e a consulta da tarde, e
  recusar travaria o balcão num caso legítimo que a recepcionista não teria como contornar.
  A lição, que vale para a próxima unificação: **ao juntar dois fluxos, pergunte em que
  momento cada fato passa a existir — e não deixe o fato IRREVERSÍVEL depender do passo
  OPCIONAL.** A guia é o que a clínica não pode perder; o pacote e o caixa se resolvem
  depois, por outra tela. Inverter essa ordem não quebra teste nenhum: os 1512 continuavam
  verdes, porque cada um deles chamava `ConcluirAsync` — o caminho que ninguém questiona é
  o que os testes exercitam.
- **Mover XAML entre projetos quebra o `xmlns`, e só o CI acusa** (parcela 60, checagem
  33): `clr-namespace:X;assembly=Y` manda o WPF procurar o namespace X **dentro** do
  assembly Y. Quando Y é o próprio projeto do arquivo, o compilador de marcação não acha
  nada e recusa (`MC3074`, `MC3072`). É o que acontece ao mover uma tela de um projeto para
  outro: o `xmlns` continua nomeando o assembly de ORIGEM, que agora é o de DESTINO.
  Foi assim que a tela de Pacotes, ao subir do Financeiro para o shell, derrubou o build.
  ⚠️ **Nenhuma rede local pegava**: o XML é bem-formado, o `compilar-sombra` **não lê o
  corpo** do XAML (ele só gera o `.g.cs` a partir de `x:Class` e `x:Name`) e o C# compila.
  O defeito existe só para o compilador de MARCAÇÃO, que roda no Windows — sete minutos de
  CI por um `;assembly=` que sobra. Dentro do próprio projeto a forma certa é
  `clr-namespace:…` **sem** o sufixo.
- **A venda de pacote subiu para o shell** (parcela 60): a tela existe desde a parcela 4 e
  a única porta estava no app do FINANCEIRO — mas quem vende dez sessões ao paciente é a
  RECEPÇÃO, no balcão, com ele na frente. Décima primeira ocorrência do defeito recorrente,
  e ela bloqueava justamente o caso que motivou o Particular. A tela não foi copiada:
  **subiu** (`Componentes/PacotesView`), como a sala de infusão na parcela 48, e os dois
  módulos publicam a **MESMA chave** — a dedupe do `ShellViewModel` faz o Gerente, que
  carrega os dois, mostrar uma linha só.
  ⚠️ O item passou a exigir `Permissao.VenderPacote`, bit **próprio**, e não
  `VerFinanceiro`: dar o financeiro ao balcão abriria junto o caixa, a conciliação e as
  contas a pagar. Vender um pacote é combinar um preço com o paciente; ler o dinheiro da
  clínica é outra coisa — o mesmo corte que a parcela 49 fez entre ficha e prontuário.
- **O PARTICULAR não é um convênio: é a ausência de um** (`ConvenioCadastro.GeraGuia`,
  parcela 60). O enum `Convenio` não tem "sem convênio", então o paciente que vem sem plano
  não tinha onde ser cadastrado — e as duas saídas eram ruins de jeitos diferentes:
  (a) cadastrá-lo sob um convênio qualquer faz o motor gerar guia com data prevista, que
  entra no painel de pendências, vence o prazo e abre a **rodada BLOQUEANTE** — travando a
  tela de quem fatura por uma guia que nunca vai a operadora nenhuma, porque não há
  operadora; (b) não cadastrar o atendimento, e aí a sessão não existe em lugar nenhum:
  nem guia, nem prontuário, nem caixa.
  A saída é um sinalizador no CADASTRO, e **o código continua nascendo** — marcado
  `NaoAplicavel`. Não é preciosismo: `EstaPendente` já ignora esse status, então o
  particular sai das pendências e da rodada **sem uma linha de código nova**; o invariante
  "não há atendimento sem código" é o que prova que `AtendimentoService.LancarAsync`
  continua sendo ponto único; e o registro da sessão (modalidade, especialidade, data) é o
  que alimenta os indicadores — sumir com ele faria a clínica medir só o convênio.
  ⚠️ **O sinalizador mora no `ConvenioCadastro`, não em `ConfiguracaoRegraGenerica`**, pela
  mesma razão que pôs o formato do número da guia lá: aquela configuração só é lida pela
  família Personalizado, e o campo tem de valer para **qualquer** família. Dentro dela
  seria uma caixinha que não faz nada num convênio embutido — o campo morto que a parcela
  49 tirou da tela de Taxas.
  ⚠️ **E quem aplica é o `AtendimentoService`, não as regras.** São seis regras com vários
  ramos cada, e a modalidade Consulta delega para `RegraConsultaAvulsa`, que monta o código
  por fora: um ramo esquecido produziria guia pendente para um particular, e ela só
  apareceria **dez dias depois**, travando a rodada. Pós-processar no ponto por onde a
  PRÉVIA e o LANÇAMENTO passam é o único jeito que não tem como ser esquecido — e é o que
  garante que a tela não prometa "nenhuma guia" e o serviço grave uma.
  **Código fora do catálogo é presumido FATURÁVEL** (`CatalogoConvenios.GeraGuia` devolve
  `true`): o erro nesse sentido é uma guia a mais para conferir; no sentido contrário seria
  o sistema parar de gerar guia para um convênio de verdade, em silêncio, e só descoberto
  no fim do mês.
- **Migration de coluna `bool` não-anulável: o EF gera `defaultValue: false`, e quase nunca
  é isso que você quer** (parcela 60). O gerador não sabe o que a coluna significa — viu um
  `bool` e pôs o default da linguagem. Aplicada assim, a coluna `GeraGuia` teria desligado
  a guia de **todos os convênios já cadastrados** na primeira abertura depois da
  atualização: o app abriria, os atendimentos continuariam nascendo, e as guias
  simplesmente parariam de existir. **Confira o `defaultValue` de toda coluna nova
  não-anulável, e pergunte o que as linhas JÁ GRAVADAS valem** — é isso que o default
  precisa dizer, não o que o tipo devolve por omissão.
- **Porta e CONTEÚDO são duas permissões, e uma sem a outra não resolve** (parcela 59 — a
  direção viu a recepcionista abrindo os documentos e pediu a permissão granular). A
  central de documentos pedia `VerFichaPaciente`, que todo perfil de balcão tem: a
  recepção alcançava as dez folhas, inclusive relatório de evolução e anamnese.
  **A porta sozinha seria o defeito do bit sobrecarregado de novo** (parcela 49), agora
  numa tela: as dez folhas não são a mesma coisa — receituário, atestado, pedido de exame,
  relatório de evolução e anamnese carregam dado de saúde (art. 5º, II); **declaração de
  comparecimento e termo de consentimento não**, e os dois saem do balcão o dia inteiro.
  Um bit só obrigaria a direção a escolher entre a recepcionista lendo a evolução de todo
  mundo e a recepcionista sem o recibo que ela emite dez vezes por dia.
  Daí as duas metades: `Permissao.VerDocumentos` fecha a SEÇÃO, e cada folha declara o que
  exige (`FolhaCatalogo.PermissaoVer` / `PermissaoEmitir`).
  ⚠️ **O acesso NÃO segue a `NaturezaFolha`**, e a distinção é o ponto: a natureza diz de
  que lado da clínica a folha vem (é o que agrupa os cartões), e sete são "do
  atendimento". Amarrar o acesso a ela tiraria da recepção dois papéis que ela entrega
  todo dia para proteger um dado que eles não carregam.
  ⚠️ **A regra mora no CATÁLOGO porque são TRÊS portas**: a central, o Receituário da
  Recepção e a aba Documentos da ficha emitem os mesmos papéis. Corrigir só a que o
  cliente apontou deixaria a correção cosmética — bastaria clicar no item ao lado para ler
  as mesmas receitas. É o defeito recorrente do projeto na variante que mais engana: a que
  **parece** coberta.
  Três decisões menores que valem além desta tela: **cartão que a pessoa não alcança SOME,
  não fica apagado** — "sem permissão" ao lado de "Relatório de evolução" anuncia que
  existe um relatório daquele paciente, que é justamente o que não se quer contar; **a
  lista do que já saiu passa pelo mesmo filtro dos cartões**, senão a tela esconderia o
  botão de emitir receita e mostraria "Receituário 2026/0012 — Maria Silva" logo abaixo; e
  **folha sem acesso declarado nasce FECHADA** (`Permissao.Nenhuma` faz `Pode` LIBERAR, e
  um papel novo nasceria aberto para todo mundo sem ninguém notar até vazar).
  `SessaoUsuario.Efetivas` existe para filtrar LISTA por acesso sem repetir a regra do
  "sem sessão autenticada, libera" — lida como `Permissoes` cru, a central abriria sem um
  único cartão fora do login, e tela vazia se lê como defeito.
- **A rodada bloqueante é DIRIGIDA a quem fatura, e a dispensa é um BIT — não o contrário**
  (`Permissao.DispensarRodadaPendencias`, parcela 57): a trava de 10 dias abria para
  qualquer um que pudesse baixar OU marcar NC, e o Gerente Geral recebe `Todas` — então a
  direção entrava no faturamento para CONFERIR e caía numa fila de guias que ela não vai
  resolver. Travar quem entra para olhar faz a conferência simplesmente não acontecer.
  ⚠️ **O bit é uma DISPENSA, e essa inversão é a decisão.** `PerfilAcesso.Gerente =>
  Todas` percorre `Enum.GetValues`, então um bit com o sentido direto ("está sujeito à
  rodada") chegaria LIGADO à direção justamente por ela ter tudo — e um bit novo que
  precisasse ser subtraído de `Todas` transformaria "todas" em "todas menos", que é a
  porta para a próxima exceção. Como dispensa, o perfil que tem tudo é dispensado sozinho
  e o `Faturista`, que não tem, continua travado.
  **Dispensa não é cegueira**: quem tem o bit continua vendo o banner de rodada vencida no
  painel e o botão "Rodar pendências". Esconder o aviso junto faria a direção deixar de
  saber que há guia vencida — o oposto do que a rodada existe para garantir. Por isso a
  checagem mora na ABERTURA (`App.MostrarRodadaSeVencidaAsync`) e não dentro do
  `RodadaPendenciasFluxo`, que é compartilhado com o botão do painel: o que se desliga é a
  janela que TRANCA, nunca a capacidade de rodar.
  ⚠️ **Tensão que ficou de pé e é decisão da direção**: o `Faturista` não tem
  `MarcarNaoConformidade` (parcela 49), e a janela bloqueante exige uma decisão POR GUIA —
  baixa ou NC. Quem não tem o número da guia e não pode marcar NC fica sem saída para
  aquela linha. Antes isso se diluía porque a direção também era travada e resolvia; agora
  a trava é só dele. O conserto de um clique existe e é o que a granularidade serve:
  conceder `MarcarNaoConformidade` ao Faturista em Acessos.

- **Coluna de formulário feita de `StackPanel` irmão desalinha quando o rótulo quebra**
  (parcela 57 — a cliente reprovou o "fora de esquadro" da tela de novo paciente). Duas
  colunas lado a lado, cada uma um `StackPanel` com rótulo em cima e campo embaixo, ficam
  alinhadas **enquanto os dois rótulos couberem numa linha**. Basta a janela estreitar ou
  o rótulo crescer — "Validade da carteirinha" ao lado de "Nº da carteirinha" — para um
  deles quebrar em duas linhas, empurrar o campo daquela coluna 17px para baixo e deixar a
  linha inteira torta. O defeito depende do texto E da largura, que é o que o torna difícil
  de reproduzir e fácil de reintroduzir.
  A correção é estrutural: rótulo e campo em **LINHAS de `Grid`**, com a linha do rótulo
  em `SharedSizeGroup` — as duas colunas reservam a altura do MAIOR rótulo e os campos
  começam sempre na mesma altura. ⚠️ O nome do grupo é um **identificador** (sem ponto,
  sem espaço): ele é validado em RUNTIME e derruba a tela inteira — a lição da parcela 50,
  hoje cobrada pela checagem 27.
  Na mesma tela, dois vizinhos do mesmo tipo: o **`DatePicker` sem `Template` no design
  system do FATURAMENTO** (o mesmo defeito da parcela 56 no da suíte, portado agora — dois
  design systems que não se referenciam significam corrigir duas vezes), e **botão ancorado
  num `DockPanel` sem `VerticalAlignment="Center"`**, que estica junto quando a mensagem ao
  lado quebra em três linhas. A recusa de CPF repetido é exatamente uma mensagem dessas: a
  regra nova tornou visível um defeito de leiaute que já estava lá.

- **Clicar no dado COPIA o dado** (`Copiavel`, `CelulaCopiavel`, parcela 57): a clínica
  não vive só neste sistema — a guia é efetivada no PORTAL da operadora, e para isso a
  secretária retipa nome, telefone, carteirinha e número da guia do outro lado. Retipar é
  onde nasce o erro que o produto inteiro combate: o **"O" no lugar do zero** que a
  `RegraNumeroGuia` existe para pegar, o dígito a menos no telefone. Copiar não erra.
  O telefone **já estava no modelo de pendência** desde que o botão do WhatsApp existe —
  o sistema o TINHA e não o MOSTRAVA, e quem precisava do número para o portal abria a
  ficha do paciente noutra tela. É a variante do defeito recorrente do projeto em que o
  dado tem leitor, mas só um, e não o da tarefa.
  As decisões: **nome e telefone na MESMA célula**, e não em duas colunas — as pendências
  já disputam a largura, e foi a coluna de paciente espremida entre vizinhas mais largas
  (1,2* e 1,4* contra 1*) que cortava o nome; **o estilo carrega a afordância** (mão e
  sublinhado no hover), porque recurso que não se anuncia ninguém descobre; **a dica
  mostra o valor inteiro**, que é o que devolve o nome longo a quem só vê o começo dele;
  **célula vazia não se anuncia como copiável**, senão a pessoa clica num traço e conclui
  que quebrou; e o **`TextBlock` ganha fundo transparente ao ser ligado**, porque sem
  pintura o WPF só aceita clique em cima das LETRAS — sem isso o clique ao lado do texto
  não copiaria nada e o recurso pareceria funcionar "às vezes".
  A cópia **tenta três vezes**: a área de transferência é recurso ÚNICO da máquina e fica
  bloqueada enquanto outro programa a segura (o navegador e o próprio portal fazem isso o
  tempo todo). E **falha nunca aparece como sucesso** — dizer "copiado" e a pessoa colar o
  conteúdo anterior no portal é pior do que não ter o recurso, porque ela cola sem
  conferir. A confirmação aparece NO LUGAR DO CLIQUE, não numa barra no rodapé: quem copia
  a terceira célula da linha está olhando para ela.
- **A rede não varria o faturamento na checagem de CHAVE** (parcela 57): chave inexistente
  é `ResourceReferenceKeyNotFoundException` na montagem da tela — erro de runtime puro, que
  é exatamente o grupo que o `arvores_com_faturamento` existe para alcançar desde a parcela
  51. Ficar de fora deixou passar quatro `CellTemplate="{StaticResource …}"` apontando para
  uma chave ainda não declarada: XAML bem-formado, `compilar-sombra` verde,
  `verificar-suite` verde, e a coluna sairia **vazia** na tela de quem fatura. Só a metade
  das CHAVES foi estendida — não a de `FontSize` numérico e cor em hexadecimal, que é a
  dívida antiga que faria a checagem gritar trinta vezes. Medido antes: **zero** chaves
  pendentes no faturamento, então a extensão não custou nada.
  ⚠️ De quebra, a lista de "estilos que já resolvem" da checagem 24 **não seguia o
  `BasedOn`**: estilo que herda o corte do pai aparecia como dívida sem ser. O ponto cego é
  traiçoeiro porque a reclamação é PLAUSÍVEL — quem a lê acrescenta o `TextTrimming`
  repetido na tela e segue, e a checagem continua cega para o próximo caso.

- **O WPF não formata na cultura da máquina — `StringFormat` é en-US por padrão**
  (parcela 56; o cliente viu **"August/2026"** no cabeçalho da Conciliação). O que engana
  é que só METADE da tela erra: o que a ViewModel formata em C# (`valor.ToString("C")`)
  sai em pt-BR pela cultura da máquina, e o que o XAML formata sai em inglês, porque
  binding usa `FrameworkElement.Language` — que nasce `en-US` e ignora a thread. Daí
  "August/2026" logo acima de uma coluna de "R$ 0,00". A correção é um `OverrideMetadata`
  no `SuiteApp`, e não uma linha por binding: são ~30 `StringFormat` de data e moeda
  espalhados, e o próximo que alguém escrever também precisa nascer certo.
- **Componente sem `Template` no design system não fica neutro: fica com o tema do
  SISTEMA OPERACIONAL** (parcela 56 — os campos "De"/"Até" da Auditoria destoavam dos
  vizinhos). O `DatePicker` tinha estilo desde a parcela 7, com só uns `Setter` e um
  comentário que dizia "usa o TextBox estilizado internamente". **Não usa**: o campo de
  texto de um `DatePicker` é um `DatePickerTextBox`, tipo próprio que o estilo implícito
  de `TextBox` não alcança. Sem `Template`, o WPF desenhava a moldura 3D, os cantos retos
  e o botão de calendário do Aero, ao lado dos campos planos da suíte. É a MESMA história
  do `TabControl` na parcela 55 — controle usado em 30 telas e nunca estilizado. O
  **calendário do pop-up ficou de fora de propósito**: retemplá-lo exige `CalendarItem`,
  `CalendarButton` e `CalendarDayButton`, cujos gatilhos só falham em RUNTIME e nenhuma
  rede local compila XAML; o campo, que é o que fica na tela, está resolvido.
- **`EstadoDaTela` sem `Itens` nem `Vazio` liga a sobreposição PARA SEMPRE** (parcela 56 —
  o cliente mandou a foto do "Nada por aqui" escrito por cima do paciente que a tela tinha
  acabado de achar). O componente resolve `(Vazio ?? Vazia(Itens))`, e `Vazia(null)` é
  **verdadeiro**: quem o declara só com `Carregando` e `NaoVerificado` ganha o estado vazio
  permanente por cima de uma tela que funciona por baixo. As duas saídas legítimas são
  `Itens` (o caminho normal, quando há lista) e `Vazio` (tela composta, ou tela que não é
  lista nenhuma e portanto nunca está vazia). Nenhuma rede pegava — XAML bem-formado,
  propriedades existentes, binding válido, nada lança —, e virou a **checagem 29**.
- **Filtro na Conciliação, e por que ele é por OPERADORA e não por família** (parcela 56):
  a tela abria com 53 guias numa lista corrida e nada para estreitá-la. Quem concilia não
  lê a lista — tem o **demonstrativo de uma operadora** na mão e precisa achar as guias
  que estão nele, uma a uma. O filtro de convênio casa pelo **nome resolvido**
  (`CatalogoConvenios.Nome(codigo, familia)`), ao contrário da consulta de guias do
  faturamento, que filtra por família: lá a pergunta é "o que vem sendo feito", aqui é
  "onde estão as guias deste papel", e filtrar por família juntaria "Sul América" e
  "Unimed Costa do Sol" — as duas respondem a `Convenio.Personalizado` — devolvendo as
  guias de quem não está no demonstrativo. A lista de convênios sai **do mês carregado**,
  não do catálogo: oferecer opção que não tem guia daria filtro que só leva a resultado
  vazio. Três regras que o código não conta sozinho: (a) o filtro **reaproveita as
  instâncias** de `LinhaConciliacao`, porque a linha guarda o VALOR DIGITADO e recriá-la
  apagaria o que a pessoa acabou de teclar em cinco guias ao ela estreitar a lista para
  achar a sexta; (b) o resumo e o estado vazio **dizem que está filtrado** — "12 de 53
  guia(s)" e "nenhuma bate com o filtro" existem porque um filtro esquecido que responda
  "nenhuma guia esperando receita" faz a clínica dar o mês por conciliado com 53
  pendentes (a lição da lista de espera da parcela 25); (c) `Convenios.Clear()` faz o
  `ComboBox` devolver **nulo** pelo binding — a mesma armadilha da prévia do Novo
  atendimento na parcela 50 —, então a remontagem da lista roda sob guarda.

- **A sidebar não estava desorganizada: estava CHEIA** (`ItemMenuModulo.Abas`,
  `TelaComAbas`, rail + painel, parcela 55). A direção reclamou de "muitas abas dentro das
  categorias" no Gerente, e a medição explicou por quê: o `Clinica.Gerente.exe` carrega os
  quatro módulos e a dedupe do `ShellViewModel` casa por CHAVE, então a sidebar tinha
  **46 itens** — a 36px por item mais os cabeçalhos, **1.824px de menu para 610px de
  tela**. Um terço visível, o resto atrás de rolagem sem marca de onde se está. O
  `MenuRecolhido` (248↔56px) não ajudava: recolher não mostra um item a mais, só troca
  rótulo por ícone na mesma lista rolante.
  A contagem achou mais três coisas: **dois itens "Prescrições"** lado a lado em PACIENTE
  (`prescricoes` e `consultorio-prescricoes` — chaves diferentes, então a dedupe não
  pegava), **oito glifos repetidos** entre os itens, e o **"Painel da direção" em 9º** no
  primeiro grupo, sendo a tela de abertura.
  O modelo escolhido pelo cliente foi **rail de 56px + painel de categoria**, e ele só é
  viável junto da **consolidação em sub-abas** — 46 itens viraram 24, e o maior painel
  (FINANCEIRO, 8) cabe inteiro em 768px. O rail sozinho deixaria um flyout de 16 linhas por
  cima do conteúdo; a consolidação sozinha ainda pediria 996px. **Nenhuma das duas metades
  bastava, e é isso que a medida mostra.**
  As decisões, e a razão de cada uma:
  (a) **A aba carrega a CHAVE, não a tela** (`AbaMenu(Rotulo, Chave)`), e quem resolve é o
  shell. É a indireção que permite um item compor telas de **módulos diferentes** —
  "Relatórios / BI" é publicado pela Direção e inclui duas telas do Financeiro e uma do
  Consultório — sem que nenhum módulo passe a conhecer o outro, que é a regra da suíte.
  (b) ⚠️ **A tela que vira aba CONTINUA sendo um item.** `NavegacaoSuite.Ir(chave)` procura
  na lista de itens e **devolve false em silêncio** quando não acha; o shell passou a
  resolver a chave de uma sub-tela abrindo **o item pai já na aba certa**, então toda
  navegação entre módulos continua valendo. É literalmente a regressão da 4ª rodada da
  parcela 37, que passou pelas três redes — agora com a **checagem 28** cobrando que a
  chave de cada `AbaMenu` seja item declarado de algum módulo.
  (c) ⚠️ **Quem esconde a sub-tela é o PAI, e só onde o pai existe** — não é uma marca
  nela. Um item composto é declarado por UM módulo, mas compõe telas de vários: no
  `Clinica.Financeiro.exe`, que não carrega a Direção, "Relatórios / BI" não existe, e
  "Resultado do mês" e "Produção" voltam a ser itens de menu comuns. Esconder por decreto
  teria feito duas telas sumirem do único app onde alguém as usa todo dia — o defeito
  recorrente do projeto cometido pela própria correção dele. `Oculto` continua sendo outra
  coisa: a tela que nunca aparece sozinha (as cinco clínicas, que só existem com paciente).
  (d) **O clique FIXA o painel, e é a metade que torna o hover utilizável.** Painel que só
  existe enquanto o mouse está em cima é **alvo móvel**: para ir do ícone até o oitavo item
  o mouse atravessa a borda entre os dois e, num percurso diagonal, sai da zona por um
  instante. Daí os 180ms de intenção para abrir, os **320ms de folga do "corredor"** antes
  de fechar, e o alfinete dentro do próprio painel — mandar a pessoa de volta ao ícone para
  prender a lista é o movimento que este modelo cobra caro.
  (e) **Glifo único por item visível**, agora por obrigação: numa lista com rótulo, oito
  desenhos repetidos passam; num rail, o ícone é a única identificação.
  (f) **A busca indexa o rótulo das ABAS** e diz o caminho ("Fechamento de caixa — em
  Caixa"). Consolidação que tira telas do Ctrl+F troca um problema de rolagem por um pior.
  (g) **A abertura vem primeira no grupo dela** — a ordem dentro do grupo é a de
  carregamento dos módulos, e o dono da abertura é a Direção, que carrega por último: sem a
  exceção, o "Painel" abria o app e aparecia no FIM de GESTÃO. É a desordem que a parcela
  22 corrigiu, um nível abaixo.
  (h) **Uma aba não é aba, é a tela**: sobrando uma só, o shell mostra a tela direto.
  E duas que NÃO foram feitas: "Faturamento (TISS)" **não virou aba de nada** (ele já é uma
  tela de cinco abas por dentro, e pendurá-lo sob outra régua daria abas dentro de abas, o
  que é pior do que os dois itens que se economizaria); e o `Clinica.Desktop` **não foi
  tocado** — ele tem o shell dele, e a Fase 4 segue cancelada.
  ⚠️ A lição de rede: a **checagem 19 disparou no COMENTÁRIO** que explicava a regra, porque
  ela casava `Ir("literal")` sem tirar comentário. Checagem que reclama de prosa é checagem
  que alguém desliga — e aí ela para de pegar o defeito de verdade. Virou `_sem_comentarios`
  (que PULA as strings, para `"https://…"` não virar comentário).

- **Varredura do Gerente: o enum vazava onde a checagem não olha, e a permissão tinha
  UMA barreira** (parcela 54). A checagem 20 só examina `ComboBox`, e por isso não via o
  caminho mais comum do defeito da parcela 41 — **interpolação em `$"..."` dentro do
  ViewModel**. Sete pontos escreviam o identificador na tela: `"ConsultaEspecialidade"` na
  linha da guia em Pendências, NC, Glosas e Tabela de preço; `"ClinicaDaDor"` na
  especialidade; e o **código** do convênio na frase que a direção lê em Rentabilidade
  ("rende mais: UnimedIntercambio" — o defeito da parcela 50 de novo, agora em texto
  montado). `RotulosEnum.De` e `CatalogoConvenios.Nome` já resolviam tudo; as telas não os
  usavam. A lição: **ao procurar enum vazando, procure a INTERPOLAÇÃO, não só o binding** —
  `{p.Tipo}` dentro de uma string é invisível para qualquer checagem de XAML.
  `Especialidade` ganhou rótulo porque é o único cujo humanizador não salva: ele devolve
  "Clinica da dor" sem acento, e isso sai impresso em relatório que vai para fora.
  Do lado da permissão, **`UsuarioEdicaoViewModel` criava usuário, definia senha e alterava
  permissões sem um único `Exigir`** — e é a tela onde a segunda barreira vale mais, porque
  quem mexe em permissão **pode conceder permissão a si mesmo**. A parcela 51 derrubou essa
  suposição no `AcessosViewModel` ("só se chega por ali") e ela ficou de pé na janela que
  ele abre. Campanhas tinha o mesmo buraco em quatro caminhos de escrita.
  **O que a varredura conferiu e NÃO era defeito**, para a próxima não refazer: coluna de
  300–330 px é a coluna de **Ações** em tabela de largura inteira, não faixa lateral (o
  falso positivo da parcela 49); `StackPanel` horizontal sem `VerticalAlignment` só estica
  botão quando o pai é linha de `Grid` — dentro de painel vertical não há defeito; e
  `LotesGerencialViewModel` ser somente leitura é **decisão** (o número do lote é sequência,
  e dois apps gerando em paralelo produzem lotes duplicados que a operadora recusa semanas
  depois).

- **O MESMO ATO com DUAS regras: a metade sem regra é a que ninguém confere** (parcela 64,
  auditoria de prontidão do módulo do GERENTE). A tela "Quem parou de vir" abre o WhatsApp
  com um convite para retomar as sessões. É **recall** — comunicação ativa da clínica —, e
  o projeto decidiu na parcela 5 que recall só sai com `ComunicacaoEMarketing` vigente:
  `CampanhaService.GerarRecallAsync` recusa quem não consentiu e ainda **conta** quantos
  ficaram de fora. A tela mandava a mesma mensagem para as mesmas pessoas **sem perguntar
  nada**, porque a lista vinha de outro serviço (`RetencaoPacienteService`, que responde
  "quem sumiu" e não tem por que saber de consentimento).
  Não é a mesma coisa que capacidade sem porta: aqui há DUAS portas para o mesmo ato, uma
  com regra e outra sem — e a sem regra é justamente a que ninguém lembra de conferir,
  porque a existência da outra dá a sensação de que o assunto está coberto. É primo do que
  a parcela 61 corrigiu na fila (`EditarAgenda` de um lado, `VerAgenda` do outro), com o
  agravante de o lado frouxo aqui não ter regra NENHUMA, e de a garantia ser a que a
  cliente está auditando.
  As decisões: a leitura de consentimento é **em lote** (`PacientesComConsentimentoVigente
  Async`, a MESMA da campanha — nunca uma segunda definição), e quem não consentiu
  **continua na lista, contado, com o motivo escrito na linha** e o botão apagado. Sumir
  com ele seria pior: some da lista, some da cabeça, e a tarefa que destrava o telefonema
  (colher o consentimento no balcão) deixaria de existir para a direção.
  ⚠️ E o teste não podia morar no ViewModel (WPF não compila no projeto de teste): o que
  `RetencaoConsentimentoTests` fixa é que os **dois lados perguntam a mesma coisa e recebem
  a mesma resposta**, revogação incluída. **Ao achar o mesmo ato em duas telas, escreva o
  teste que compara as duas** — não o que prova que a que você acabou de arrumar funciona.

- **Trocar de paciente rápido mistura os dados de DOIS pacientes na tela de conformidade**
  (parcela 64): `GuardaProntuarioViewModel` fazia duas idas ao banco em sequência (a guarda
  e a trilha de leitura) disparadas pelo `Selecionado` do seletor, **sem contador de
  geração** — e o seletor é uma BUSCA, então trocar de pessoa várias vezes é o uso normal
  de quem investiga um acesso indevido. Num banco remoto a leitura velha responde depois da
  nova: a guarda de um paciente sob o nome de outro, ou a trilha da Maria listada na ficha
  do João. A regra da parcela 60 já valia para "toda tela que dispara leitura a cada tecla
  ou clique"; o que esta parcela acrescenta é **onde doer mais**: numa tela que existe para
  responder auditoria, a resposta errada tem exatamente a mesma cara da certa. A vizinha
  (`AuditoriaViewModel`), com o mesmo seletor, já tinha o contador — **duas telas com o
  mesmo componente e só uma com a guarda é o sinal de que a varredura da 60 passou por
  alto**.

- **A mensagem de ÊXITO invisível não é uma tela: é o padrão do arquivo ao lado** (parcela
  64). A parcela 62 achou o defeito em cinco janelas da Recepção — `<Border AlertaPerigo
  Visibility="{Binding MensagemEhErro}">` esconde junto a mensagem que zera o booleano — e
  o corrigiu lá. As **20 telas do Gerente** estavam todas no padrão antigo, com oito
  mensagens que nunca apareceram: o estado vazio das Campanhas ("Gere uma rodada acima para
  começar"), "Nenhum usuário cadastrado", "Nenhum horário na agenda neste período" e — a
  pior — **"Exportação gravada em {destino}"**, na tela que exporta o prontuário da clínica
  inteira: a direção clicava, esperava, e a tela não dizia nada nem onde havia gravado.
  A correção é a mesma frase da 62 — **quem decide se aparece é o texto; quem decide a cor
  é a gravidade** —, e a lição é sobre o ALCANCE: quando um defeito de padrão é corrigido
  numa tela, **procure o mesmo par de linhas nos outros módulos antes de dar a parcela por
  fechada**. Quatro destas telas já usavam `AlertaAviso` em vez de `AlertaPerigo`, o que
  mostra que alguém percebeu que a mensagem era informativa e não percebeu que ela nunca ia
  aparecer.

- **A checagem que existe para pegar o defeito passava por cima dele** (parcela 64,
  checagem 20). A tela de preço por convênio oferecia **"ClinicaDaDor"** no seletor de
  especialidade — o defeito da parcela 41, na tela do Gerente, com a checagem verde. A
  causa é um caractere: a coleção é `IReadOnlyList<Especialidade?>` (anulável porque o nulo
  é a opção "todas"), e a expressão da checagem só casava `<Especialidade>`. O WPF chama
  `ToString()` igual nos dois casos.
  Alargá-la para `<Tipo?>` custou **zero ruído** — a varredura achou UMA ocorrência em toda
  a suíte, que era o próprio defeito. A lição: **checagem cega é pior que checagem ausente,
  porque ela responde "está limpo"**; e a hora de medir o ruído de um alargamento é ANTES
  de decidir não fazê-lo. Autotestada nos dois sentidos (dispara com o `ItemTemplate`
  removido, cala com ele posto), pela regra da checagem 34.

- **Varredura de permissão: conte os IRMÃOS, não os comandos** (parcela 64). `Dispensar
  Async` era o único dos quatro comandos de escrita de `CampanhasViewModel` sem `Exigir` —
  e é o que faz o contato **sumir da fila sem que ninguém tenha falado com o paciente**, a
  mesma família de `MarcarNaoConformidade` no faturamento. Nas exportações CSV faltavam as
  duas: a trilha de auditoria (que leva nome de paciente desde a parcela 52) e a lista de
  sumidos (nome e TELEFONE) saíam para arquivo sem a segunda barreira, num módulo onde
  todas as outras escritas a tinham. **Quando três comandos vizinhos têm a guarda e um não,
  o que está errado é o um** — e o CSV conta como saída de dado, que foi a lição da parcela
  60 aplicada ao export clínico.

- **Ver o número e DECIDIR sobre ele são bits diferentes** (`Permissao.DefinirMetas`,
  parcela 64): a tela de Metas exigia `VerIndicadores` — o bit de LER o BI — para criar e
  para APAGAR o alvo do mês. É o bit sobrecarregado da parcela 49 de novo, num par que
  parece o mesmo assunto e não é: o realizado é FATO, e a meta é a DECISÃO da direção
  sobre o fato. Enquanto os dois moraram no mesmo bit, dar acesso de leitura aos números a
  alguém ("o financeiro pode ver") entregava junto o poder de apagar as metas do ano — e
  meta apagada não deixa buraco visível: o painel volta a comparar com o mês anterior, que
  responde "melhorou?" e nunca "chegamos onde a gente disse que ia chegar?".
  ⚠️ Diferente da parcela 49, **esta separação não tira nada de ninguém**: nenhum perfil
  padrão além do Gerente tinha `VerIndicadores`, e o Gerente recebe `Todas` — o bit novo
  chega ligado a quem já definia meta ontem. O que ele acrescenta é a possibilidade de
  conceder o BI sem conceder o alvo, que é o pedido da direção na 49 aplicado ao lugar
  onde ainda não estava. `Ver_indicadores_nao_da_o_direito_de_definir_meta` falha se
  alguém os juntar de volta, inclusive pelo caminho discreto (acrescentar o bit ao padrão
  de um perfil que só deveria ler).

- **Teto posto no elemento errado não encolhe o conteúdo: ele o DECEPA** (parcela 64 — o
  cliente mandou a foto do mapa corporal com "Repetir a anterior" e "Limpar" meio visíveis,
  encavalados no resumo). O `MaxHeight="260"` estava no `DockPanel` do painel inteiro, e
  não no `ScrollViewer` da lista de pontos, que é o único filho que cresce sem limite.
  Protocolo, campo de observações e a linha de ações somam mais de 260 px sozinhos — então
  o `StackPanel` do topo era cortado, e o que ficava fora do corte eram justamente os dois
  botões e a frase "Nenhum ponto marcado". A pergunta que decide onde o teto vai: **qual
  filho cresce com o DADO?** É nele. Os de altura conhecida não podem ser cortados.
- **Três respostas para a mesma pergunta se leem como sobreposição** (parcela 64, tela
  "Quem me deve"): o resumo ao lado do combo dizia "Nenhuma conta de paciente vencida", uma
  faixa verde `AlertaSucesso` repetia a MESMA frase, e o `EstadoDaTela` dizia "Ninguém
  devendo" com desenho. O cliente descreveu como faixa sobreposta no lugar errado, e era
  isso mesmo: o `EstadoDaTela` estava na RAIZ da tela, cobrindo KPIs, filtro e a coluna de
  envelhecimento, e caía por cima da faixa verde. Duas correções, e as duas são regras
  velhas: **"um estado vazio por pergunta"** (parcela 37) e **a sobreposição pertence à
  REGIÃO cujo vazio ela explica, nunca à página** (parcela 58, que já a tinha corrigido
  noutras telas e não nesta).
- **Coluna elástica ao lado de coluna FIXA dá o excesso todo para a elástica** (parcela 64,
  Conciliação): `Paciente` era `*` e `Convênio` 170 px fixos, então numa tela larga o nome
  do paciente ganhava meio palmo de branco enquanto "Unimed Costa do Sol Intercâmbio" saía
  truncado ao lado — e o número da guia colava nele. Quando DUAS colunas têm conteúdo de
  tamanho imprevisível, as duas são estrela e o que se escolhe é a PROPORÇÃO (`2*` e
  `1.3*`, com piso); fixa fica só para o que tem tamanho conhecido — data, número, campo de
  digitar. E célula de tabela precisa de respiro: sem margem, "Unimed Costa do
  Sol Inte…37034962" se lê como uma coisa só.
- **A checagem 20 tinha um SEGUNDO ponto cego: o enum da camada de APLICAÇÃO** (parcela 64
  — o cliente viu "MaisAntigo" e "MaiorValor" no seletor de "Quem me deve"). A função que
  monta a lista de enums varria só `src/Clinica.Domain`, e `OrdemInadimplencia` mora em
  `Clinica.Application/Servicos` — o WPF chama `ToString()` sem se importar com a camada em
  que o enum nasceu. Custo de alargar, medido ANTES: uma ocorrência em toda a suíte, que
  era o próprio defeito. É o mesmo desfecho do ponto cego do enum anulável, na mesma
  parcela, e a lição se repete de propósito: **quando uma checagem responde "está limpo",
  pergunte primeiro o que ela não olha** — e meça o ruído antes de decidir não alargar.

- **O termo que o PACIENTE assina, e a pergunta que decidiu o desenho inteiro** (parcela
  66): a cliente pediu "algo como o SmartDocs" para o BSV — o consentimento do
  procedimento e a **declaração de jejum**. O pedido chegou como um item só e são **dois
  documentos de naturezas opostas**, e é essa separação que resolve tudo. O consentimento
  do procedimento ganha em ser lido em casa, com calma. A declaração de jejum afirma
  "**ESTOU** em jejum": assinada na véspera ela é uma declaração sobre o FUTURO, e o valor
  dela é ser sobre o presente. **Isso derruba o SmartDocs como resposta ao caso trazido** —
  o link no celular resolve o primeiro documento e é inadequado justamente para o segundo,
  que foi o que motivou o pedido. Daí a assinatura no BALCÃO, onde o paciente do BSV já
  está: `InkCanvas` é de fábrica no WPF, não exige e-CPF, não exige internet e não cria
  deployable novo. **A pergunta que decide não é "que tecnologia o concorrente usa", é
  "sobre QUANDO este documento afirma alguma coisa".**
  ⚠️ **O paciente NÃO assina com certificado, e isso não é limitação.** Termo de
  consentimento é documento ENTRE AS PARTES (MP 2.200-2/2001, art. 10, §2º) — a Lei
  14.063/2020 chama isso de assinatura SIMPLES. Exigir e-CPF do paciente seria inviável e
  desnecessário. **O que dá valor a ela é EVIDÊNCIA**: quem, quando, diante de quem, com
  que documento conferido, e o SHA-256 do que ele tinha na frente — declarações respondidas
  incluídas, senão o selo deixaria de fora justamente a parte que se contesta ("eu nunca
  disse que estava em jejum"). E o rodapé escreve "assinatura eletrônica simples", jamais
  "digital": é a regra do carimbo escaneado, da parcela 3.
  **A ordem das duas assinaturas é amarra técnica, não preferência**: o PDF não se assina
  incrementalmente (a restrição que a parcela 42 já encontrou), então o traço do paciente
  tem de estar DENTRO dos bytes ANTES do selo ICP-Brasil do profissional. Colher depois
  produziria, em silêncio, um arquivo cujo selo não fecha — a garantia aparente que este
  projeto recusa. `ColherAsync` RECUSA documento já assinado eletronicamente.
  **"Não estou em jejum" NÃO impede, e é decisão**: o termo é emitido do mesmo jeito, com a
  resposta escrita, e acende alerta VERMELHO pelo `ElegibilidadeService`. Bloquear
  produziria o desfecho pior — ninguém emite o termo, o procedimento acontece assim mesmo e
  não sobra registro nenhum. Quem decide adiar é quem faz o procedimento.
  **Validade POR SESSÃO, sem campo de prazo** (decisão da clínica): a chave de "já
  assinou?" é a DATA. Um "vale por N dias" existiria para nunca ser usado, e regra com
  exceção que ninguém exerce é código a mais e mais uma resposta possível para a mesma
  pergunta (a lição do CPF duplicado, parcela 57).
  **O TEXTO é da clínica, não nosso**: `ModeloDocumento` com as declarações como
  `ItemModelo`, escrito em Configurações → "Escrever termos…". Não há termo de fábrica —
  um texto de consentimento embutido seria o sistema opinando sobre risco clínico. E
  aplicar **COPIA**, como o protocolo do mapa corporal; aqui não é desenho, é a Lei
  13.787/2018: referência viva faria corrigir uma palavra hoje reescrever o que um
  paciente assinou no mês passado. Por isso `DocumentoClinico.ModeloOrigemId` é
  PROCEDÊNCIA — e casar por MODELO, não por tipo, é o que permite dois procedimentos no
  mesmo dia exigirem dois termos sem um cobrir o outro.
  ⚠️ **`TipoDocumentoClinico.TermoProcedimento` não reaproveita o `Consentimento`**, que é o
  termo **LGPD** montado das finalidades: seria o bit sobrecarregado da parcela 49 num
  papel — sem como conceder um sem o outro, e a segunda via de um sairia com o texto do
  outro. E a folha nova nasce **fechada** em `FolhaCatalogo` (a regra da parcela 59).
  **A leitura tem DOIS caminhos e UMA definição**: a fila lê 30 cartões em 3 consultas
  (`DoDiaAsync`) e a ficha lê um (`SituacaoDoDiaAsync`), e os dois resolvem pela MESMA
  função privada. Duas definições de "falta assinar" divergiriam na primeira correção, e a
  que ninguém lembraria de ajustar é a do quadro — onde o erro aparece como **cartão
  limpo**, indistinguível de termo em dia. `Leitura_em_lote_da_fila_concorda_com_a_leitura_
  por_paciente` é a amarra.
  **A porta fica na fila do balcão**, no "⋯" do cartão: o alerta do check-in diz que falta
  assinar, e alerta sem porta no mesmo app é pior que alerta nenhum (parcela 48). A janela
  mora no **shell**, como o mapa corporal — ela é apresentada em três lugares e copiá-la
  daria três telas divergindo na primeira correção.
  **Lição de teste**: `Os_sete_documentos_geram_PDF` cobrava `TipoDocumentoInfo.Todos` e
  falhou ao nascer o oitavo tipo — foi a rede que impediu um documento sem PDF de passar no
  build e só quebrar na frente de quem fosse imprimi-lo. **Asserção contra a lista COMPLETA
  de um enum é o que faz o tipo novo cobrar a própria cobertura.**

- **A porta do termo na FICHA, e as dezesseis coisas que uma revisão adversarial achou
  numa parcela que já estava verde** (parcela 66, 2ª rodada). O cliente pediu para colher o
  termo dentro da ficha do paciente — a porta nasceu na fila, e a ficha é onde a
  recepcionista já está com a pessoa na frente. Virou uma seção no TOPO da aba Documentos
  (não aba nova: a ficha já responde seis perguntas), e a decisão que a governa é a mesma
  da entidade: **ela só mostra o dia de hoje**. Sem procedimento marcado, a seção troca de
  frase em vez de sumir, e explica que o termo é colhido no dia — colher "para amanhã"
  produziria um papel que o balcão não veria como cumprido amanhã, que é pior do que não
  ter porta, porque a pessoa acreditaria ter resolvido.
  ⚠️ **A lição maior não é a porta: é que a parcela estava com 1531 testes verdes, três
  redes locais verdes, e tinha DEZESSEIS defeitos reais.** Nenhum deles quebrava build ou
  teste. Vale a pena listar as famílias, porque elas se repetem:
  (a) **Dado de saúde sem barreira na tela nova** — a seção da ficha era lida por
  Financeiro e Faturista, que têm `VerFichaPaciente` e não têm `VerProntuario`. A lista de
  documentos ao lado já filtrava por acesso; a seção nova, não. **Ao acrescentar região a
  uma tela existente, copie a barreira da região vizinha.**
  (b) **Carga que falha deixa a tela do paciente ANTERIOR** — o `catch` logava e saía, e
  as linhas de quem já tinha saído continuavam ali com o botão aceso apontando para o
  `DocumentoId` dele. Um clique assinaria o termo de outra pessoa. **Limpe ANTES do await,
  não depois**, e ponha terceiro estado.
  (c) **O selo que regera o arquivo sem a parte selada** — `AssinaturaDeDocumentoClinico
  Service` gerava o PDF para assinar SEM passar o traço do paciente, e como a reimpressão
  devolve os bytes guardados, o termo selado perderia a assinatura do paciente PARA SEMPRE.
  É a inversão exata da regra que a própria parcela documentou.
  (d) **Formulário incompleto gravado como cumprido** — declaração sem resposta virava
  "Assinado hoje" sem alerta nenhum. Recusar em branco não contradiz o "avisa, mas não
  impede": aquilo vale para o CONTEÚDO da resposta ("não estou em jejum" é registrado e o
  procedimento segue sendo decisão de quem o faz); o que se impede é o campo vazio.
  (e) **Rota que cai no `default:`** — a folha nova na central caía na janela genérica de
  documento, que não conhece o tipo: papel numerado sem modelo de origem e sem declarações.
  A saída foi a forma que o RECIBO já usava (`ExigenciaFolha.ProcedimentoDoDia`: o cartão
  LEVA até onde se colhe, e o rótulo diz "Abrir a ficha" em vez de "Emitir"), mais uma
  recusa no construtor da janela genérica — **a segunda barreira para a próxima porta**.
  (f) **Mensagem de erro que manda fazer o que a tela não faz** — "troque o modelo da
  exigência que existe" e não havia por onde. `ExigirAsync` passou a TROCAR. É seguro
  porque aplicar COPIA: o termo assinado guarda o texto lido e o `ModeloOrigemId` antigo.
  (g) **`NULL` não é único no PostgreSQL** — o índice `(Modalidade, ModalidadeCodigo)`
  ficava inerte justamente no caso NORMAL (código nulo = família inteira), e dois cliques
  concorrentes criariam duas exigências, fazendo o paciente assinar o mesmo papel duas
  vezes. Família passou a gravar **string vazia**.
  (h) **Hash gravado que ninguém recalcula é um número** — `ConteudoIntacto` existia sem
  chamador em produção, e o rodapé afirmava o selo. Agora o PDF **recalcula na impressão** e
  a segunda via de um termo alterado sai dizendo que não prova o que foi assinado. A
  montagem do selo desceu para a ENTIDADE, porque quem precisa dela são dois (o serviço que
  grava e o PDF que confere) e duas montagens divergiriam.
  (i) **Alerta em data futura é alerta impossível de atender** — a conferência do termo
  roda também no formulário de agendamento, que pergunta pela data MARCADA. Marcar um BSV
  para o mês que vem acendia vermelho sem ter o que fazer. Restrito a hoje.
  (j) **Dado calculado que nenhuma tela mostra** — `CartaoFila.TemTermoPendente` era
  computado para todo cartão e só aparecia dentro do menu "⋯". Virou selo.
  (k) **`IsEnabled` de bloco apagando a permissão de outro ato** — o "⋯" seguia
  `PodeEditarAgenda`, e a técnica de enfermagem, que tem `ColherAssinaturaPaciente` e não
  tem o da agenda, não alcançava o único caminho para colher. O bloco perdeu o `IsEnabled`;
  quem decide item a item é o menu, e o botão de PASSO ficou com a permissão que é dele.
  (l) **Alerta sem porta no app de quem o lê** — o Consultório recebia "falta o termo" com
  o paciente na sala e não tinha botão. Ganhou a faixa com a MESMA janela do shell.
  ⚠️ E a **lição de método**: o script da revisão tinha um bug meu (passei promessas a
  `parallel`, que espera thunks), então a fase de verificação morreu e o workflow devolveu
  `{confirmados: [], descartados: []}` — **vazio por defeito, indistinguível de "nada
  encontrado"**. É o defeito recorrente do projeto cometido na própria ferramenta de achar
  defeitos. Os achados estavam no `journal.jsonl` o tempo todo. **Resultado vazio de
  workflow é para ser investigado no journal, nunca lido como aprovação.**

- **Checagem que cobre UM sentido de um erro simétrico está metade cega** (parcela 66, 3ª
  rodada — o CI reprovou a PR). A `ModelosTermoWindow` do Gerente declarou
  `xmlns:ctrl="clr-namespace:Clinica.Desktop.Controls"` **sem** `;assembly=`, e o tipo mora
  no shell: `MC3074`. A **checagem 33** existe exatamente para esta família e não viu,
  porque ela nasceu na parcela 60 pegando o `;assembly=` que **SOBRA** (tela movida entre
  projetos) — e este é o mesmo erro pelo avesso, o `;assembly=` que **FALTA**.
  Nenhuma rede local pegava, pela razão de sempre: o XML é bem-formado, o
  `compilar-sombra` não lê o corpo do XAML e o C# compila. Virou a **checagem 33-B**, que
  casa cada `clr-namespace:X` sem sufixo contra os namespaces que o projeto DECLARA nos
  `.cs` dele — projeto sem `.cs` lido responde "não sei" e cala, como a 34. Autotestada
  contra o caso real e contra os dois legítimos (shell e faturamento, que declaram
  `Clinica.Desktop.Controls` cada um no seu — o débito permanente da parcela 7).
  A regra que fica, e que vale para toda checagem futura: **ao escrever uma rede para um
  erro que tem dois sentidos, cubra os dois no mesmo commit.** O sentido que você deixar de
  fora é o que a próxima pessoa vai cometer — aqui foram seis parcelas até alguém tentar o
  outro lado.

- **"Regra com exceção que ninguém vai exercer" tem prazo de validade — e ele acabou antes
  de a feature chegar à clínica** (parcela 66, 3ª rodada). A validade do termo nasceu POR
  SESSÃO, sem campo de prazo, com o argumento (bom, e escrito) de que um "vale por N dias"
  existiria para nunca ser usado. A cliente exerceu a exceção **antes do primeiro uso**: ela
  quer colher a assinatura quando o paciente aparece — inclusive na consulta em que ele vem
  tirar dúvidas, semanas antes — e emitir pela central de documentos, sem esperar o dia.
  E ela tem razão pelo argumento que o próprio desenho já continha: **o dia do procedimento
  é justamente quando ninguém tem tempo de ler o termo.**
  O que a distinção original acertou, e que sobreviveu: as DECLARAÇÕES moram dentro do termo,
  e nem toda declaração sobrevive à antecedência — "estou em jejum" assinado na semana
  passada é afirmação sobre o futuro. Por isso a validade virou **escolha por procedimento**
  (`ExigenciaTermoProcedimento.SoValeNoDiaDoProcedimento`, desmarcada por padrão) em vez de
  desaparecer: a clínica escreve o consentimento longo sem prazo e, se quiser, um termo
  curto só com o jejum marcado como "a cada sessão". Os dois convivem porque a exigência é
  por **MODELO**, não por tipo.
  ⚠️ **Seja qual for a validade, RECUSA e papel pendente contam só no DIA.** Recusa é decisão
  de um momento, não estado permanente: herdá-la faria um "não" de três semanas atrás calar
  o pedido no dia do procedimento — e o paciente pode ter mudado de ideia, tanto que veio
  fazer. E o papel emitido e nunca assinado carrega a DATA da emissão: reusá-lo faria a
  assinatura de hoje nascer com data velha, que numa exigência "só no dia" não contaria
  nunca.
  De quebra, as portas viraram QUATRO (ficha, fila, Consultório, central) e passaram a
  entrar por um ponto único — `ColetaDeTermo.Abrir`, no shell. Cada uma montava a janela por
  conta própria (escopo, ViewModel, dono, recarga), e quatro montagens divergem na primeira
  correção; o que elas colhem é a prova de que o paciente consentiu. Quando o modelo não vem
  decidido por um procedimento marcado, o ponto único PERGUNTA qual termo é — que é o
  caminho da coleta avulsa.
  A lição, e ela é sobre método: **"não construa a exceção que ninguém vai exercer" é uma
  boa regra para código especulativo, e não para uma decisão que o cliente ainda não tomou.**
  Quando a exceção depende de como a clínica trabalha, pergunte antes de fechá-la.

- **Tela do paciente: o modelo da MAQUININHA, e por que ela não é um tablet** (parcela 66,
  5ª rodada — *"há um dispositivo para assinar via touchscreen … quero que o dispositivo
  ligue na hora aparecendo o lugar certo para o paciente assinar"*). A cliente escolheu
  **duas telas: ela controla, ele só assina**, e é o desenho da maquininha do cartão — não
  um espelho da janela da recepcionista. São dois conteúdos diferentes: aqui as declarações
  com Sim/Não, o documento conferido e o Confirmar; lá o texto GRANDE, as respostas como
  ela as marcou e a área de assinar.
  ⚠️ **A área de assinar SOME desta janela quando há a do paciente.** Duas áreas ativas
  permitiriam a recepcionista assinar pelo paciente sem querer — e o termo diria que ele
  assinou. No lugar dela fica a linha que explica a ausência: campo que some sem explicação
  se lê como defeito.
  ⚠️ **A resposta marcada aqui muda a tela dele NO MESMO INSTANTE**, e isso não é conforto:
  o selo do termo é o SHA-256 do que o paciente tinha na frente, declarações incluídas. Uma
  tela atrasada faria a evidência afirmar algo que não é verdade.
  **O que se grava é o NOME do dispositivo (`\\.\DISPLAY2`), nunca o índice na lista.** O
  índice muda quando alguém desliga um cabo ou o Windows reordena as telas depois de um
  reinício — e a tela do paciente viraria a da recepcionista, com o termo em tela cheia por
  cima do trabalho dela, sem ninguém ter mexido em nada. Tela gravada e ausente responde
  **"não há segunda tela"**, jamais "use a primeira que aparecer", e a configuração escreve
  o terceiro estado: silêncio faria a clínica concluir que a feature quebrou quando o que
  houve foi um cabo solto.
  **Sem segunda tela, TUDO continua numa janela só, e isso não é modo degradado**: é o modo
  de quem tem um monitor. A clínica pode ficar meses sem comprar o touch, e a feature não
  pode esperar por isso — foi o que decidiu o `ParametrosService` devolver **nulo** para a
  chave vazia, em vez de uma string que o sistema procuraria como nome de monitor.
  **O botão "Testar" é obrigatório, não conforto**: as telas se chamam `\\.\DISPLAY1` e
  `\\.\DISPLAY2` e ninguém sabe qual é qual pelo nome. Sem ele, o primeiro a descobrir que o
  termo abriu no monitor errado seria o PACIENTE, vendo o próprio nome e o procedimento dele
  numa tela virada para a sala de espera. O exemplo não leva dado de paciente nenhum, pela
  razão do arquivo de teste da publicação (parcela 53).
  **Por que MONITOR e não tablet**: um tablet é outro computador — exigiria servidor web,
  rede confiável no balcão e um caminho de volta para o traço, que é a Fase 2 inteira. Um
  monitor touch é só mais uma tela do Windows: o traço nasce dentro do mesmo processo que
  grava o termo, sem rede entre a caneta e o banco. A especificação do que comprar está em
  `docs/termo-assinado-pelo-paciente.md` §3.8.
  Detalhes que o código não conta sozinho: a janela dele é `Topmost`, sem barra de título e
  **dona desta** (painel órfão no balcão é convite para alguém assinar o termo de outra
  pessoa); o monitor é **impedido de dormir** enquanto o termo está no ar
  (`SetThreadExecutionState`), senão a proteção de tela apaga o documento no meio da leitura
  de quem lê devagar; e a enumeração de monitores é **P/Invoke puro** (`EnumDisplayMonitors`),
  sem WinForms, em pixels FÍSICOS — num arranjo de DPIs diferentes, posicionar em unidades
  do WPF põe a janela na tela errada.

- **Um procedimento exige VÁRIOS termos, e a chave antiga apagava o primeiro em silêncio**
  (parcela 67). A cliente pediu os termos do BSV prontos, e ao criá-los apareceu o defeito:
  `ExigenciaTermoProcedimento` tinha chave única `(Modalidade, ModalidadeCodigo)`, então
  amarrar o **segundo** papel TROCAVA o primeiro. O BSV precisa de dois, e a razão é a mesma
  que desenhou a parcela 66 inteira: o **consentimento** vale a partir da assinatura (lido
  com calma, dias antes) e a **declaração de jejum** não se herda (é sobre o dia). São
  validades OPOSTAS e não cabem no mesmo papel.
  O mais revelador é que **a leitura sempre soube devolver vários** — `Resolver` percorre as
  exigências e o balcão lê uma LISTA, com o comentário dizendo "é o que permite dois
  procedimentos no mesmo dia exigirem dois termos sem um cobrir o outro". Quem não deixava
  era a ESCRITA. **Quando um lado do sistema fala no plural e o outro no singular, o que
  está errado é quase sempre o singular** — e o erro não aparece: a exigência antiga
  simplesmente some da lista.
  A chave passou a incluir `ModeloDocumentoId`. Repetir a MESMA amarração continua não
  recusando (a lição da 2ª rodada da 66 — mensagem de erro que manda fazer o que a tela não
  faz), só que agora ela atualiza a VALIDADE; trocar de modelo é ligar o novo e desligar o
  antigo, dois cliques **visíveis** — e visível é o ponto, porque a troca automática
  acontecia sem a lista mostrar o que sumiu.
- **Rascunho revisável não é termo de fábrica** (parcela 67): a regra da parcela 66 é "o
  texto é da clínica, não nosso — texto embutido seria o sistema opinando sobre risco
  clínico", e ela continua valendo. O que a prática mostrou é que **a lista nascer vazia não
  fez ninguém escrever um termo do zero no meio do expediente — fez o BSV continuar
  acontecendo sem termo nenhum.** O que a regra proíbe é o texto que o sistema aplica
  SOZINHO; o que passou a existir é um rascunho que alguém **pede, lê e edita**: botão em
  Configurações (nada em migration, nada na abertura), nome com "(rascunho — revisar)" e
  primeira linha do corpo mandando o responsável técnico conferir e apagar o aviso ao
  aprovar. Criar o modelo **não exige nada de ninguém** enquanto a amarração não for feita, e
  recriar é recusado — sobrescrever apagaria a revisão que a clínica já fez, que é o trabalho
  que o botão existe para começar.
- **A saída CONSCIENTE da checagem 18** (parcela 67): alargar uma chave única é `DropIndex` +
  `CreateIndex`, e não perde dado — toda linha que passava na chave antiga passa na nova. Mas
  a regra não podia virar "`DropIndex` pode": o mesmo drop usado para ESTREITAR quebra a
  clínica no dia seguinte, e **a diferença entre os dois casos não está na operação, está na
  intenção de quem a escreveu**. Por isso a exceção é DECLARADA (`MIGRATION-NAO-ADITIVA-
  CONSCIENTE: <razão>` no arquivo) e o preço dela é escrever a razão; marca sem razão não
  vale, porque a razão É a exceção e não um interruptor. E ela **nunca fica silenciosa**:
  vira aviso em toda execução, inclusive no CI — exceção que some da saída é exceção que
  ninguém revisa, e a próxima pessoa a copiar a migration como modelo precisa ver que ali
  houve uma decisão, não uma permissão. Autotestada nos três cenários (sem marca, com marca
  e razão, marca vazia).

- **A dispensa da checagem 18 tem de ser por OPERAÇÃO, não por ARQUIVO** (parcela 67, 2ª
  rodada — o achado mais grave da revisão adversarial do próprio diff). A saída consciente
  nasceu certa na intenção e errada no alcance: a marca valia para o arquivo INTEIRO, então
  bastava um `DropIndex` inofensivo declarado para um `DropColumn` acrescentado DEPOIS, na
  mesma migration, passar junto — e a ferramenta ainda imprimia, como justificativa dele, a
  frase que falava do índice e afirmava "nenhuma linha se perde". **Garantia falsa no log do
  CI é pior do que checagem nenhuma**, e o caminho é o realista: a migration marcada é
  justamente a que a próxima pessoa copia como modelo. A marca passou a NOMEAR o que cobre
  (`MIGRATION-NAO-ADITIVA-CONSCIENTE(DropIndex): razão`); o que não estiver na lista continua
  erro. A regra geral: **exceção declarada delimita o que dispensa, senão ela dispensa o
  vizinho.**
- **`AlterColumn` nunca disparou a checagem 18 — o EF gera a forma GENÉRICA** (parcela 67,
  2ª rodada). A busca era `.AlterColumn(` e o EF emite `migrationBuilder.AlterColumn<string>(`.
  A operação **mais** destrutiva da lista (encolher `maxLength`, tornar coluna NOT NULL) era
  letra morta desde que a checagem nasceu, e ninguém notou porque ela só cala — não erra.
  Casar `Op(` **ou** `Op<`. A lição: **quando uma checagem procura uma chamada de método,
  confira como a ferramenta que gera o código a escreve de verdade**, não como você a
  escreveria.
- **Autoteste que REIMPLEMENTA a lógica não testa nada** (parcela 67, 2ª rodada): o da saída
  consciente repetia a leitura da marca linha a linha em vez de chamar a função da checagem.
  Ele ficaria verde exatamente quando a checagem quebrasse, porque a cópia dentro dele não
  quebra junto — é a variante mais discreta do defeito recorrente do projeto, aplicada à
  rede que existe para pegá-lo. **Autoteste chama o código que roda.**
- **Idempotência ancorada em campo EDITÁVEL não é idempotência** (parcela 67, 2ª rodada): o
  botão dos termos do BSV guardava-se contra o segundo clique comparando o NOME do modelo —
  e o nome é justamente o que o desenho pede que mude, porque a marca "(rascunho — revisar)"
  mora nele para ser apagada na aprovação. Renomeado, o segundo clique criava outro par e
  ligava mais quatro exigências, que a chave alargada da mesma parcela deixa CONVIVER: o BSV
  passaria a cobrar quatro papéis, dois deles o rascunho não revisado, e assinar um par não
  zeraria o outro. **A pergunta certa não era "este texto já existe?", era "o BSV já está
  configurado?"** — a guarda olha a EXIGÊNCIA. E, de quebra, isso a tornou RESUMÍVEL: são
  seis gravações contra banco remoto sem transação, e o segundo clique agora completa o que
  faltou em vez de recusar tudo por causa da metade que ficou.
- **Declaração de termo é redigida para que "Não" seja um SINAL** (parcela 67, 2ª rodada):
  responder "Não" acende alerta VERMELHO no balcão e no consultório, então (a) declaração
  cujo "Não" é NORMAL dilui o alerta — havia um "estou acompanhado, se a clínica exigir",
  que faria metade dos pacientes acender vermelho e treinaria todo mundo a ignorar o do
  jejum; e (b) declaração NEGATIVA ("não tive febre") torna a resposta ambígua, porque "Não"
  vira dupla negação. Todas afirmativas e incondicionais. E o **`Detalhe` sai IMPRESSO na via
  do paciente**: instrução para a equipe ali ("confira com o paciente quantas horas") produz
  um documento que fala do leitor na terceira pessoa.
- **Valor NOVO de enum é a única coisa que o app velho não consegue ler do banco
  compartilhado** (parcela 67, 3ª rodada — o cliente mandou a foto da tela de Prescrições
  morta, com *"Cannot convert string value 'TermoProcedimento' from the database to any
  value in the mapped 'TipoDocumentoClinico' enum"* no lugar da lista). Os cinco apps se
  auto-atualizam por Velopack, **um canal por app**, e dividem UM banco: a janela em que o
  Consultório já atualizou e a Recepção não é o DESENHO, não o acidente.
  Coluna nova e tabela nova atravessam essa janela sem incidente — o EF só pede as colunas
  que conhece. **Valor de enum, não**: o `HasConversion<string>()` chama `Enum.TryParse` e,
  ao não achar o nome, LANÇA. E não falha a LINHA, falha a **CONSULTA** — a clínica perde a
  tela inteira por causa de um registro, com uma frase em inglês que não diz a ninguém que
  o que falta é atualizar o programa. São 76 `HasConversion<string>()` no contexto; cada um
  é a mesma mina.
  `ConversorEnumTolerante<TEnum>` tem **duas metades, e uma sem a outra é pior que nada**:
  **ler é tolerante** (o nome desconhecido vira o sentinela, a linha aparece e a tela abre)
  e **escrever é RECUSADO**. Sem a segunda, o app velho leria "não sei o que é isto" e no
  primeiro Salvar gravaria o sentinela **por cima do tipo verdadeiro** — apagando em
  silêncio o que a versão nova registrou. Registro clínico não se apaga (Lei 13.787/2018),
  e apagar por conversão é a forma mais discreta de fazê-lo. A recusa mora no CONVERSOR
  porque a escrita tem muitas portas (emitir, cancelar, salvar modelo) e uma que esquecesse
  bastaria — a razão de sempre.
  ⚠️ **Cair no `default` do enum seria pior do que estourar.** Um termo de procedimento
  apareceria como "Receita": mentir sobre um registro de prontuário, em silêncio, é o
  desfecho que este projeto recusa desde a parcela 3. O sentinela `Desconhecido` diz o que
  há **e o que fazer** ("Tipo não reconhecido — atualize o sistema"), fica **fora de
  `TipoDocumentoInfo.Todos`** (não é papel nenhum) e **não imprime** sem os bytes guardados
  — a folha sairia com cabeçalho, número e assinatura e sem o miolo, que é a garantia
  aparente de novo.
  O conversor foi aplicado a `TipoDocumentoClinico` — o enum que quebrou, que CRESCE (sete
  tipos até a 65, oito na 66) e que os cinco apps leem. Enum que ganhar valor novo e for
  lido por outro app entra nele; enum estável não precisa, e sentinela espalhado por todos
  teria a blast radius conhecida do `Enum.GetValues` dos catálogos.
- **Quem diz onde o DER termina é o CABEÇALHO dele, nunca o enchimento** (parcela 67, 4ª
  rodada — a clínica levou *"A assinatura foi produzida mas não conferiu: … (ASN1 corrupted
  data)"* na primeira assinatura em nuvem que passou do decode). O `/Contents` do PDF é
  dimensionado para o pior caso ANTES de assinar (32 KB) e completado com zeros à direita;
  na volta, `LerConteudoAssinatura` cortava o enchimento com `TrimEnd('0')` — que tira
  **caracteres '0', não bytes zero**. Uma assinatura terminada em `0x00` perdia o último
  byte: os dois caracteres sumiam, o comprimento continuava PAR e o remendo de nibble ímpar
  (`if (hexa.Length % 2 == 1) hexa += "0"`) não repunha nada.
  ⚠️ **O comentário ao lado do defeito já dizia a solução** — "que é DER e sabe onde
  termina" — e o código cortava por enchimento assim mesmo. Agora `RecortarDer` lê tag e
  comprimento e fatia exatamente.
  A aritmética é o que explica por que isto chegou à clínica: o último byte de um CMS é o
  último byte da assinatura RSA, ou seja é **sorteado** — uma folha a cada 256. Raro o
  bastante para nunca cair num teste (o `Assinatura_confere_no_arquivo_intacto` passava
  255 vezes em 256, e ninguém viu a 256ª), frequente o bastante para acontecer em produção.
  **Teste que depende de um byte aleatório não é teste, é sorteio** — o novo fixa o caso
  com um DER construído à mão que termina em `0x00`.
  E nada disso é do SafeID: pega token e nuvem igual. Apareceu ali porque a assinatura
  ICP-Brasil **nunca tinha rodado fora dos testes** (a parcela 53 já dizia isso por
  escrito), e a nuvem foi o primeiro caminho a chegar ao cliente.
- **Decodificar prova que veio base64; não prova que veio uma ASSINATURA** (parcela 67, 4ª
  rodada). A 3ª rodada tornou o `raw_signature` tolerante a base64url, enchimento e
  armadura PEM — e o que sai dali continuava indo **calado** para o `/Contents` sem ninguém
  perguntar se era um PKCS#7. Quando não era, o erro só aparecia na conferência, como "ASN1
  corrupted data": uma frase que não distingue **três** causas diferentes — o PSC devolveu
  outro formato, o arquivo foi adulterado, ou nós lemos errado de volta. Foram duas rodadas
  de diagnóstico às cegas por isso.
  `AssinadorSafeID.ExigirPkcs7` decodifica com o MESMO `SignedCms` que a conferência vai
  usar depois (é isso que faz a checagem valer: o que passa aqui passa lá) e a recusa diz o
  **começo dos bytes em hexa** — um CMS abre em `30 82`, e o que vier diferente nomeia a
  causa de imediato. Ela mora no ASSINADOR e não no `ClienteSafeID` porque aquele é
  transporte: entrega o que o PSC mandou. Aqui é onde a assinatura vira PDF, e é o único
  caminho pelo qual uma assinatura em nuvem chega a um documento da clínica.
  A lição de método, que vale além desta integração: **quando uma correção torna uma
  entrada mais TOLERANTE, ela precisa vir junto da conferência do que a entrada deveria
  ser.** Tolerância sem conferência só empurra a falha para mais longe da causa — e o
  segundo erro é sempre mais caro de diagnosticar que o primeiro.
- **A assinatura em nuvem assinava o hash de NADA — e o defeito morava no vão entre dois
  testes que passavam** (parcela 67, 5ª rodada; três mensagens de erro diferentes em três
  dias até alguém REPRODUZIR o caminho em vez de deduzi-lo). O `RangedStream` que o
  PDFsharp entrega ao `IDigitalSigner` chega **sem posição**: `Position` nem getter
  utilizável tem (lança `NullReferenceException`), `CanSeek` **lança** em vez de devolver
  false — o que derruba `CopyTo` —, e **ler antes de posicionar devolve ZERO bytes, calado**.
  `AssinadorSafeID` fazia `SHA256.HashDataAsync(conteudoCoberto)` direto, então o que subia
  para o PSC era `e3b0c442…b7852b855` — o SHA-256 da cadeia vazia, **a mesma constante em
  toda folha da clínica**. O PSC assinava esse hash corretamente e devolvia um PKCS#7
  impecável: nenhum erro em lugar nenhum até a conferência dizer que o documento "foi
  alterado", porque a assinatura de fato cobre outra coisa. **Assinatura válida sobre
  conteúdo nenhum é a garantia aparente na forma mais perigosa que este projeto já
  produziu** — e a única que sai da clínica com valor jurídico afirmado no rodapé.
  ⚠️ **Nenhum teste podia pegar, e é isso que a lição tem de mudar.** Havia teste para a
  montagem da requisição, para a leitura da resposta, para o recorte do PKCS#7 e para a
  conferência com certificado LOCAL. Não havia um só que assinasse um PDF **pelo assinador
  de nuvem** e depois o CONFERISSE — o circuito inteiro. Cada peça verde, o produto delas
  quebrado. É o `CircuitoCompletoTests` da parcela 33 aplicado a outro assunto: **quando um
  caminho é montado de peças testadas, o teste que falta é sempre o do CAMINHO.**
  A rede que ficou junto da correção: assinar o hash da cadeia vazia é **recusado**. Não é
  paranoia — é a única forma de a próxima versão do PDFsharp (ou outro caminho de
  salvamento) não devolver o defeito em silêncio.
  ⚠️ E a lição de MÉTODO, que custou três rodadas ao cliente: **eu diagnostiquei por
  inferência três vezes seguidas.** Cada correção era um defeito real e provado — o
  `raw_signature` que não era base64 padrão, o `TrimEnd('0')` que comia o byte `0x00` — e
  nenhuma era ESTA. Ler o código e deduzir a causa a partir da mensagem de erro é o que
  parece diligência e é o que faz o cliente testar de novo. **Quando a mensagem muda a cada
  rodada, pare de ler e REPRODUZA o caminho.** O experimento que resolveu tem trinta linhas
  e devia ter sido a primeira coisa.
- **O CMS não vem em DER: vem em BER com comprimento INDEFINIDO** (parcela 67, 6ª rodada —
  e foi a mensagem-com-evidência da rodada anterior que resolveu em UMA tentativa). A tela
  trouxe `o cabeçalho DER não é legível (começa em 308006092A864886, 32768 bytes
  disponíveis)`, e o hexa diz tudo: `30` SEQUENCE, **`80` comprimento indefinido**, `06 09`
  OID de nove bytes, `2A 86 48 86 F7 0D 01 07 02` = `1.2.840.113549.1.7.2` = signedData.
  Um PKCS#7 perfeito — só que em **BER**, que é sobre o que o CMS é definido (RFC 5652),
  e não em DER.
  O `RecortarDer` da 4ª rodada lia o cabeçalho À MÃO e **recusava explicitamente** o `0x80`,
  com um comentário meu dizendo que "existe em BER, não em DER" — a premissa certa e a
  conclusão errada: eu tratei como defeito o formato que o fornecedor usa. Agora quem conta
  os bytes é o `AsnDecoder` do próprio .NET em modo BER, que percorre a estrutura e acha o
  `00 00` do fim. **Parser de ASN.1 escrito à mão é onde se erra justamente o caso do
  fornecedor** — e o `SignedCms.Decode` sempre aceitou os dois (foi por isso que o
  `ExigirPkcs7` passou e a mensagem dizia "produzida").
  ⚠️ **A lição de método, e é a que fecha a série**: as rodadas 3, 4 e 5 foram diagnóstico
  por inferência e custaram três testes ao cliente. A rodada 5 acrescentou UMA coisa — a
  frase passou a nomear a causa e a imprimir os primeiros bytes — e a 6ª foi resolvida no
  primeiro relato, sem hipótese nenhuma. **Mensagem de erro que carrega a evidência não é
  conforto: é o que substitui a próxima rodada de adivinhação.** Quando um caminho novo
  encosta em formato de terceiro, a evidência entra JUNTO com o código, não depois de ele
  falhar.
- **Aceitar o formato do fornecedor não é o mesmo que EMITIR o formato da norma** (parcela
  67, 7ª rodada — achado ao responder "temos 100% de certeza?", e a resposta honesta era
  não). A rodada anterior fez o nosso `Conferir` aceitar o CMS em BER indefinido que o
  SafeID devolve. Isso resolve metade: **PDF assinado exige DER** (ISO 32000-1 e
  PAdES/ETSI EN 319 142). Embutir o BER produziria um arquivo que o NOSSO validador aprova
  e que o Adobe e o validador do ITI podem recusar — e quem descobriria é o farmacêutico,
  com a receita na mão. É a **garantia aparente virada do avesso**: em vez de o sistema
  mentir para a clínica, ele diria a verdade para a clínica e produziria um arquivo que o
  mundo lá fora não lê.
  `AssinadorSafeID` passou a **normalizar para DER** (`SignedCms.Decode` + `Encode`) antes
  de devolver os bytes ao PDFsharp. Medido antes de decidir: o reencode devolve **o DER
  byte a byte idêntico** ao que o PSC teria produzido, e `CheckSignature` continua
  passando — a assinatura cobre os atributos assinados, não a codificação de fora.
  O `RecortarAsn1` continua aceitando BER de propósito: é o que mantém legível qualquer
  folha assinada ANTES desta correção.
  ⚠️ A pergunta que produziu isto vale mais que a correção: **"e se o nosso lado estiver
  certo e o de fora não?"** Toda integração que aceita o formato de um terceiro tem essa
  segunda metade, e ela não aparece em teste nenhum da casa — por definição, o teste da
  casa usa o leitor da casa.
- **BER aninhado, medido em vez de suposto** (parcela 67, 8ª rodada — a cliente perguntou
  se havia 100% de certeza, e cada tentativa de assinatura é COBRADA pelo PSC, então a
  resposta tinha de vir de experimento e não de leitura). Um HSM emite o CMS com
  comprimento indefinido nos ENVELOPES de fora e DER no miolo assinado. Medido em
  profundidade crescente: **1 a 4 passam inteiras** — o recorte acha o fim exato dentro do
  enchimento de 32 KB, o `SignedCms` decodifica, `CheckSignature` confere e a normalização
  devolve o DER **byte a byte idêntico**. A profundidade 4 já entra na lista de
  certificados, além do que qualquer PSC produz.
  Só o caso patológico (indefinido até o certificado embutido, que nenhum PSC reescreve)
  derruba o **reencode** — e a leitura continua correta mesmo ali.
  ⚠️ **O experimento achou um defeito meu na MENSAGEM, não no formato.** Decode e encode
  estavam no mesmo `try`, então a falha do reencode sairia com a frase "não são um PKCS#7"
  sobre bytes que SÃO um PKCS#7 válido e conferido. É a mesma família de erro que custou
  seis rodadas a esta integração: **mensagem plausível e errada manda procurar o defeito no
  lugar errado**, e é pior que mensagem nenhuma. Os dois passos ganharam `catch` separados.
  A recusa, nesse caso, é deliberada: o CMS é válido mas está em BER, e embuti-lo produziria
  um arquivo que a NOSSA conferência aprova e o Adobe/ITI podem recusar. A frase diz isso e
  lembra que a via em papel continua valendo.
- **"See the inner exception for details" é instrução para o PROGRAMADOR impressa na cara
  do usuário** (parcela 67, 9ª rodada — a clínica assinou um documento com sucesso e levou,
  no lugar da confirmação, *"An error occurred while saving the entity changes. See the
  inner exception for details."*). `ClinicaRepositorio.SalvarAsync` traduz o que reconhece
  (duplicidade, vínculo quebrado, sem conexão) e devolvia `null` para o resto — e `null`
  faz a exceção subir **como o EF a escreveu**. A frase não diz o que houve, não diz o que
  fazer, e esconde justamente a linha que resolve: a do banco, com a coluna, a restrição e
  o valor.
  Agora o caso não classificado leva `ex.GetBaseException().Message` junto ("O banco
  respondeu: …"). Não é elegante e é muito melhor que uma tela que não informa nada — e a
  causa já ia para o log desde sempre, o que significa que a informação existia e só não
  chegava a quem precisava dela.
  É a MESMA lição que a assinatura em nuvem custou seis rodadas para ensinar, agora noutra
  camada: **mensagem de erro que carrega a evidência substitui a próxima rodada de
  adivinhação.** Quando uma tradução de erro tem um caminho "não sei o que é isto", esse
  caminho é o que vai aparecer na clínica — e é o que precisa dizer mais, não menos.
- **Data COM fuso: o defeito que o SQLite não pode pegar** (parcela 67, 9ª rodada — a
  clínica assinou um documento com SUCESSO e levou *"An error occurred while saving the
  entity changes"* na publicação). O sistema grava hora de PAREDE (`DateTime.Now`,
  `Kind=Local`), e o Npgsql **RECUSA** isso numa coluna `timestamp with time zone` ("only
  UTC is supported"). O padrão do provedor para `DateTime` é justamente **com** fuso — então
  esquecer o `HasColumnType("timestamp without time zone")` numa propriedade nova **não
  quebra nada** até alguém tentar gravá-la em produção.
  Eram **SEIS** colunas, e a publicação era só a que apareceu: as outras cinco são da
  parcela 52 — cancelar evolução, anexo, avaliação e medida, e versionar evolução. Ou seja,
  **as garantias de prontuário imutável que a auditoria da cliente exigiu estavam todas
  quebradas no Postgres desde que nasceram**, e a clínica só não descobriu porque ninguém
  tinha cancelado um registro clínico ainda.
  ⚠️ **Os 1580 testes não podiam pegar, e isso é estrutural**: eles rodam em SQLite, que
  guarda data como texto e não liga para o `Kind`. É a mesma família do `xmin` (concorrência
  otimista, "só no Npgsql — testes rodam em SQLite e ficam de fora"), só que aqui o buraco
  não estava documentado. `DatasSemFusoTests` fecha por outro caminho: lê o **MODELO** do EF
  e cobra `timestamp without time zone` de toda propriedade `DateTime`. Ela falha no commit
  em que alguém esquecer, que é meses antes de a clínica esbarrar.
  **Ao acrescentar propriedade `DateTime`, declare o tipo da coluna.** O provedor não erra
  a favor.
- **Comentário que promete o que o código não faz é pior que comentário nenhum** (parcela
  67, 9ª rodada, o segundo defeito do mesmo log). `AssinaturaDeDocumentoClinicoService`
  dizia, por escrito, *"publica DEPOIS de gravar … falhar aqui não desfaz nada — vira frase
  no resultado"* — e **não havia `try`**. A exceção da publicação subia por cima de tudo, e
  a tela dizia que o documento NÃO tinha sido assinado, quando ele tinha e estava gravado.
  É o pior desfecho possível: quem lê "não foi assinado" **emite de novo**, e ficam dois
  documentos do mesmo ato — exatamente o que o comentário de `DocumentoEdicaoViewModel`
  avisa duas telas acima. `ResultadoPublicacao.Falhou` já existia; faltava alguém chamá-lo.
  A lição de leitura: **quando um comentário descreve um comportamento de degradação,
  confira se existe o `catch` que o realiza.** Intenção escrita não é intenção executada, e
  o comentário faz o revisor seguinte parar de procurar.
- **A 2ª assinatura: a enfermagem sela o REGISTRO DE EXECUÇÃO, nunca a mesma folha**
  (parcela 68 — a direção reverteu, para quem pedir, a decisão de ago/2026 de que a
  execução se assina só à caneta). O pedido era "um campo de 2ª assinatura onde, quando
  marcado, a enfermeira também possa assinar digitalmente" — e o desenho inteiro sai de
  duas restrições que já estavam escritas: em PDF **não se assina incrementalmente**
  (dois documentos encadeados, um por signatário — o caminho que o comentário da
  `AssinaturaDocumento` já apontava), e o **motor congelado não se toca** (o
  `AssinaturaDigitalService` e o SafeID são reusados tal e qual; o que nasceu foi
  orquestração por cima: `AssinarExecucaoAsync` no orquestrador e no serviço de domínio).
  As decisões: **o campo é por FOLHA, de quem prescreve** (`ExigeAssinaturaEletronicaDa
  Execucao` — regra global travaria a sala inteira no dia em que o certificado de uma
  técnica vencesse); **a assinatura é no ENCERRAMENTO**, porque o registro de execução
  muda a cada item checado e assinar antes selaria um arquivo que ainda ia mudar — a
  razão de a prescritora não assinar rascunho, aplicada ao outro papel; **o CPF do
  certificado confere contra QUEM ESTÁ ASSINANDO** (a enfermeira logada, via
  `UsuarioSistema → Profissional`), nunca contra o prescritor — sem isso o e-CPF da
  médica assinaria a execução da técnica; e **assinada, a segunda via da execução passa a
  devolver os bytes GUARDADOS** (até aqui ela era sempre montada na hora, e continua
  sendo nas folhas do regime do papel — que não mudou em nada para quem não marcou o
  campo, e há teste fixando as duas coisas).
  O bit da ação é `ChecarPrescricao`, não um novo: assinar a execução é responder pelo
  que se checou — o mesmo trabalho, selado. E o botão "Assinar execução" só EXISTE na
  folha encerrada que aguarda a assinatura: botão apagado permanente para o caso que não
  existe seria a caixinha morta da parcela 49.

- **Auditoria Recepção → Consultório: nenhum dos achados QUEBRAVA nada** (parcela 68). O
  fluxo entre os dois módulos foi varrido inteiro — lógica, erros e leiaute — e o que a
  varredura devolveu tem a assinatura de sempre: build verde, 1601 testes verdes, três
  redes locais verdes, e quem descobre é a recepcionista ou o médico com o paciente na
  frente. As famílias, e o que cada uma ensina:
  **O vínculo da evolução com o horário se perdia nas DUAS pontas, e o resultado era o
  mesmo — duas evoluções do mesmo atendimento.** Na ESCRITA: a janela de evolução da
  Recepção nunca carregou `AgendamentoId`/`AtendimentoId`, e o `ProntuarioService` os
  copiava **sem condição** — editar pelo balcão uma sessão escrita no consultório
  DESLIGAVA a evolução do horário, e o consultório voltava a cobrar o registro. Na
  LEITURA: o cartão do Meu dia casa por `AgendamentoId` **e cai para paciente + data**,
  e a tela de Atendimento só olhava o `AgendamentoId` — o cartão dizia "Ver registro", o
  formulário abria EM BRANCO e o Salvar criava a segunda.
  As correções são as duas metades da mesma regra: **nulo quer dizer "o chamador não
  sabe", nunca "desligue"** (o serviço passou a preservar o vínculo com `?? destino.…`,
  onde vale para toda porta), e o casamento sessão × evolução virou **uma definição só**
  (`ConsultorioService.EvolucaoDoHorario`, pública e estática). Para a segunda, o dia do
  horário teve de viajar até o posto (`PacienteEmFoco.DataDoHorario`): presumir hoje daria
  a resposta errada justamente onde a pergunta importa — a dívida de prontuário e a Minha
  semana abrem horário de OUTROS dias.
  ⚠️ **A lição de método**: as duas telas foram escritas com meses de distância, e a que
  ficou para trás é sempre a que ninguém releria. **Quando duas telas respondem à mesma
  pergunta sobre o mesmo dado, o teste que falta é o que compara as DUAS** — é a mesma
  lição da parcela 64 (recall com regra num lado e sem regra no outro), aplicada a uma
  leitura em vez de a uma permissão.
  **O menu "⋯" da fila prometia por escrito o que não fazia.** O XAML diz "o bloco não
  segue `PodeEditarAgenda`; quem decide item a item é o próprio menu" — e o construtor do
  menu montava os itens **só pelo estado do cartão**, sem um `Pode` sequer. Os três atos
  pedem bits diferentes (`ColherAssinaturaPaciente`, `EditarAgenda | MovimentarFila`,
  `EditarAgenda` estrito), então o item aparecia aceso e a recusa só chegava depois do
  clique. **Comentário que descreve uma barreira é lugar para conferir se ela existe** —
  a mesma leitura que achou o `catch` ausente da publicação na parcela 67.
  **A cor mentia sobre o desfecho.** A barra do Novo atendimento pintava a mensagem de
  vermelho FIXO, e é o mesmo campo que recebe "Atendimento registrado — 2 guia(s) no
  faturamento": a cor dizia "falhou" enquanto o texto dizia "registrado", e quem lê a cor
  primeiro lança de novo — o gesto que produziu os três encaixes em 71 segundos da parcela
  65. Três estados, porque o meio existe de verdade: a guia nasceu e o pacote/caixa não
  **não é erro** (só o atendimento derruba a operação, parcela 6) **nem é sucesso limpo**.
  E a confirmação "Consulta de X renovada" era escrita ANTES do `RecarregarAsync`, que
  começa zerando a mensagem: **só o sucesso sumia**, porque o erro volta antes de recarregar.
  **A regra do dono da janela existia como método privado SEM CHAMADOR, enquanto 49
  lugares repetiam `MainWindow` à mão.** A parcela 58 documentou que o dono é a janela
  ATIVA — com um modal aberto, a próxima nasce ATRÁS dele e quem clicou conclui que o
  botão não fez nada — e a regra ficou numa ViewModel só, sem uso. Virou `JanelaDona.Atual()`
  no shell. **Método sem chamador ao lado de vizinhos que repetem a linha à mão é sinal de
  regra que foi escrita e não foi aplicada** — não de código morto a remover.
  **E o `EstadoDaTela` na RAIZ da página, de novo.** Em Equipe ele cobria as três colunas;
  em Documentos, os cartões e o filtro — nas duas com um estado vazio inline repetindo a
  mesma frase logo abaixo. É a parcela 58 e a 64 pela terceira vez, e a regra não muda:
  **a sobreposição pertence à REGIÃO cujo vazio ela explica, e há UM estado vazio por
  pergunta.**

- **A premissa que ficou seis parcelas de pé porque ninguém mediu a CONCLUSÃO dela**
  (parcela 68, 2ª rodada — a cliente descreveu o fluxo real: *"o médico prescreve e assina,
  a infusão vai pra enfermagem e ela também assina a prescrição que já foi assinada pelo
  médico"*). Desde a parcela 42 o projeto afirmava, por escrito e em quatro lugares, que
  **"duas assinaturas no mesmo PDF não existem, porque o PDFsharp reescreve o arquivo ao
  salvar"** — e foi essa frase que desenhou a 1ª rodada desta parcela, em que a enfermagem
  selava OUTRO documento (o registro de execução).
  Medido antes de mexer: a primeira metade está **certa** — assinar por cima do assinado
  devolve um arquivo cujo prefixo mudou, e a assinatura de quem assinou primeiro deixa de
  fechar. A **conclusão** é que estava errada: a limitação é da BIBLIOTECA, não do formato.
  O PDF prevê múltiplas assinaturas exatamente para este caso, por **atualização
  incremental** — a revisão nova é anexada ao fim e os bytes já assinados não se tocam. A
  assinatura da médica cobre 0..N; a da enfermeira cobre 0..M, com M > N; as duas fecham
  porque nenhuma teve um byte alterado.
  ⚠️ **A lição não é sobre PDF.** É que **quando uma limitação de ferramenta vira decisão
  de desenho, é preciso escrever qual das duas metades foi medida** — a restrição ou a
  conclusão que se tirou dela. Esta ficou seis parcelas sem ninguém tentar o caminho que o
  formato já oferecia, e o desenho errado tinha chegado a ter teste verde e PR aberto.
  As decisões do `RevisaoIncrementalPdf`: ele **não calcula assinatura nenhuma** — quem
  produz o PKCS#7 continua sendo o mesmo `IDigitalSigner` do token e do SafeID, sem uma
  linha de diferença, e é isso que permitiu mexer aqui sem encostar no motor congelado;
  a aparência usa **Helvetica**, uma das 14 fontes-padrão do PDF, para não depender do que
  a outra ferramenta embutiu; e a forma do arquivo de entrada é **conferida, não
  adivinhada** — xref em fluxo, `/Annots` por referência indireta ou ausência de
  `/AcroForm` **recusam com a frase escrita**, porque um meio-parser que "dá um jeito"
  produziria o arquivo que a NOSSA conferência aprova e o Adobe recusa (a garantia
  aparente virada do avesso, a lição da 7ª rodada da parcela 67).
  **E o `Conferir` passou a valer pela PIOR das assinaturas.** Ele lia o primeiro
  `/ByteRange` e parava ali: num documento de duas, responderia "íntegro" a um arquivo cuja
  segunda não fecha — falha exibida como sucesso, no papel que prova quem mandou e quem
  executou.
  ⚠️ **O teste que decide não é o nosso.** Cada tentativa de assinatura no SafeID é
  COBRADA, então a prova tinha de fechar de graça e **fora do nosso código**: o arquivo foi
  validado no **pyhanko**, que devolveu `intact=True valid=True` nas duas, com a da médica
  em `coverage=ENTIRE_REVISION mod=FORM_FILLING` e a da enfermeira em `ENTIRE_FILE
  mod=NONE` — o retrato exato de um PDF corretamente assinado duas vezes. Os testes gravam
  o arquivo quando `CLINICA_DUMP_PDF` aponta uma pasta, justamente para essa conferência
  externa continuar barata.
  ⚠️ E um defeito que só apareceu por rodar num PDF de outro produtor: **o espaço entre
  `/Type` e o nome é OPCIONAL** — o QuestPDF escreve `/Type /Page` e o PDFsharp escreve
  `/Type/Page`. Casar o texto cru funcionava por sorte, e a falha teria sido "o PDF não tem
  páginas legíveis" num arquivo cheio delas.

- **A navegação que só o TESTE preenche** (parcela 68, 3ª rodada — o bloqueador que uma
  auditoria adversarial achou no código recém-enviado, com 1608 testes verdes).
  `AssinarExecucaoAsync` lia os bytes da médica por `AssinaturaDoPrescritor?.Arquivo?
  .Conteudo`. `ObterPrescricaoInternaAsync` faz `.Include(p => p.Assinaturas)` e **não** faz
  `.ThenInclude(a => a.Arquivo)`, e o projeto não usa lazy loading — em produção, onde cada
  operação abre escopo próprio, a navegação chega **nula** e a 2ª assinatura falhava
  **sempre**, na primeira tentativa da enfermeira, que nesta área é COBRADA pelo PSC.
  ⚠️ **Os testes não podiam pegar, e a razão é a que mais se repete neste projeto**: eles
  compartilham UM `DbContext` entre gravar o `ArquivoAssinado` e reler a prescrição, e o
  **relationship fixup** do EF preenche a navegação a partir do change tracker mesmo sem
  `ThenInclude`. Ou seja, o teste montava um mundo que produção nunca vê. É o
  `CircuitoCompletoTests` da parcela 33 outra vez: cada peça verde, o produto quebrado.
  A regra que fica: **bytes de arquivo assinado se buscam por
  `ObterArquivoAssinadoAsync(arquivoId)`, nunca pela navegação** — é o que todo leitor
  legítimo já fazia, e a linha nova foi a única a confiar no grafo. E, mais geral: **teste
  de serviço que lê navegação precisa de um `DbContext` NOVO**, senão ele prova o fixup do
  EF em vez do `Include` do repositório. `Assina_com_ESCOPO_SEPARADO_como_em_producao` fixa
  isso, e foi verificado que ele FALHA no código antigo.

- **A folha só fica assinável no instante em que ela SOME da lista** (parcela 68, 4ª
  rodada — as quatro pendências que a auditoria adversarial confirmou). A 2ª assinatura da
  enfermagem só é possível depois de ENCERRAR a folha, e encerrar era exatamente o que a
  tirava da fila da Sala de infusão. Somadas, as quatro faziam a pendência desaparecer:
  1. a fila é travada em **`p.Data == hoje`** — folha de ontem não aparecia de jeito nenhum;
  2. **"Mostrar encerradas" nasce DESMARCADO** — encerrada some da lista padrão no mesmo dia;
  3. **não existia consulta, contador nem selo** de "aguardando a 2ª assinatura":
     `AguardaAssinaturaDaExecucao` era lido só DENTRO da janela de uma folha já aberta, então
     descobrir qual ficou pendente exigia abrir uma por uma;
  4. no dia seguinte a única volta era **digitar o código impresso** — que só existe se
     alguém imprimiu o papel.
  É o **"alerta sem porta" na pior variante** (parcela 48): o que a pessoa precisa
  reencontrar é justamente o que a lista esconde. E a mensagem ainda prometia que "o botão
  Assinar execução fica nesta folha" sem dizer como voltar a ela — comentário/《promessa》
  que o código não cumpre, a armadilha da parcela 67.
  A correção é uma consulta **sem filtro de data** (`PrescricoesInternasAguardandoAssinatura
  Async`: encerrada + exige assinatura + sem a do Executante), unida à fila de hoje e
  publicada de uma vez em lista local — **entre o `Clear()` e o último `Add` não pode haver
  await**, e aqui são DUAS leituras (a lição da parcela 62). O selo vermelho diz o que
  falta, a data aparece quando a folha não é de hoje, e o contador da assinatura é
  **separado** do de itens aguardando: um se resolve administrando, o outro com o
  certificado — somá-los daria um número que não diz o que fazer.
  ⚠️ E a pendência **SAI da lista assim que ela assina**: pendência que não some é
  pendência que ensina a ignorar a lista. O regime do papel (campo desmarcado) nunca entra,
  senão toda folha encerrada da história viraria cobrança eterna.

- **Varredura do módulo CLÍNICO: a barreira que falta é sempre na tela que mais custa**
  (parcela 68, 5ª rodada). Três achados, e os três da família "duas barreiras":
  (a) **A prescrição de infusão tinha só a metade VISÍVEL** — `SalvarRascunho` e `Assinar`
  se protegiam por `IsEnabled`/`PodePrescrever` e não chamavam `Exigir` em lugar nenhum.
  Atalho de teclado e corrida de carregamento passam direto, e o ato é gerar e selar uma
  prescrição médica. É o mesmo defeito que a parcela 51 corrigiu no Acessos e a 54 no
  cadastro de usuário: a tela onde ele mais custa é a última em que alguém olha.
  (b) **Enviar documento clínico ao paciente não tinha guarda** — emitir, assinar e
  cancelar tinham; enviar, não. É dado de saúde SAINDO, que é o que a parcela 60 passou a
  cobrar no export. **Três comandos vizinhos guardados e um não: o errado é o um** (a
  lição da parcela 64, aplicada de novo).
  (c) **A direção não via a pendência que a sala passou a ver.**
  `AssuntoDirecao.InfusaoAguardando` conta só folhas `Assinada` com item sem checagem, e a
  folha ENCERRADA aguardando a 2ª assinatura fica de fora por construção — o alerta novo
  (`InfusaoSemAssinatura`) contradizia, por omissão, o argumento que criou o bloco na
  parcela 61: *a sala vê a própria fila, a direção é quem vê a soma*. Os dois alertas são
  **separados**, nunca somados: um se resolve administrando o que falta, o outro com o
  certificado de quem executou.
  ⚠️ E a varredura teve um **falso positivo meu que vale registrar**: contar `Exigir(` por
  arquivo acusou `MeuDiaViewModel` de ter 11 comandos e nenhuma guarda. Ele usa
  `ExigirAlgum(` — o par de `PodeAlgum` que a parcela 61 criou —, e as quatro escritas
  estão guardadas. **Varredura textual de barreira tem de conhecer TODAS as formas da
  barreira**, senão ela acusa o arquivo que está certo e ninguém confia mais nela.

- **O QR da folha e o "Ler QR Code" do validador são dois PROTOCOLOS — o mesmo texto,
  lido por dois contratos** (parcela 68, 6ª rodada — a clínica mandou o print: o validador
  LEU o QR e pediu "Insira o código"). O nosso QR carrega a URL do PDF publicado. A câmera
  do celular trata isso como "abra no navegador" e o arquivo abre; o botão "Colar URL" do
  validador trata como endereço de arquivo, baixa e valida. Já o botão **"Ler QR Code" do
  próprio validador** trata o conteúdo como endereço da API de uma plataforma CADASTRADA:
  ele chama a URL pedindo `application/validador-iti+json` com um `_secretCode`, e o guia
  do desenvolvedor diz que esse fluxo **não aceita documento sem código/senha**. Um bucket
  S3 não fala esse dialeto e não tem código: **nenhum código digitado ali funciona, por
  desenho** — não é o QR, não é o tamanho, não é o link.
  A evidência estava no print desde o início: **ele LEU o QR ("QR Code escaneado") e ainda
  pediu código — logo nunca tentou baixar o arquivo.** Quando o cliente pergunta "por que
  A funciona e B não com o MESMO dado", a resposta é B tratar o dado por outro CONTRATO.
  ⚠️ E a lição de método custou três rodadas: a primeira hipótese (código de plataforma
  cadastrada) estava CERTA — e eu a recuei por não ter fonte, troquei pela hipótese do
  tamanho do QR (medível, e errada) e só fechei quando li o guia oficial. **Hipótese certa
  sem fonte continua sendo hipótese; a resposta estava em procurar o CONTRATO do outro
  lado (guia do desenvolvedor), não em medir o nosso.**
  O que era nosso no defeito: a folha dizia "Escaneie para baixar · validar em …" sem
  dizer COM O QUÊ escanear nem POR QUAL botão validar — a dois centímetros do NOSSO código
  de conferência, numa tela que pede um código. O rodapé agora nomeia o caminho que
  funciona (câmera → colar link ou enviar o arquivo) e desarma o errado ("o Ler QR Code de
  lá pede um código que esta folha não tem"); o código de conferência diz que é interno; e
  a folha de infusão parou de afirmar que o código "confere integridade" — ele LOCALIZA a
  folha; quem prova integridade é a assinatura (a regra da parcela 3, que quase escapou por
  uma palavra).

- **O "Ler QR Code" do validador gov.br é um CONTRATO aberto — e entrar nele não exige
  cadastro** (parcela 68, 7ª rodada — a cliente colou o Guia de Orientações aos
  Desenvolvedores do VALIDAR, que a rede deste ambiente bloqueava). O que o guia fecha:
  depois de ler o QR, o validador chama a URL com
  `?_format=application/validador-iti+json&_secretCode=<código>` e espera um JSON com a
  URL do PDF (`{"version":"1.0.0","prescription":{"signatureFiles":[{"url":"…"}]}}`);
  401 para código errado, 404 para inexistente. O `_secretCode` é definido como **"0 a 64
  caracteres"** — vazio é contrato válido. Qualquer URL que responda isso entra no fluxo;
  não há credenciamento.
  O desenho que isso destrava: um **Worker no MESMO domínio do QR** (o CNAME da parcela
  53, que existe exatamente para pôr coisas na frente do balde sem trocar a URL selada nos
  PDFs) entrega o PDF a navegador e o JSON ao validador, conferindo o `_secretCode` contra
  o **código de conferência impresso na folha** — que a publicação passou a gravar como
  METADADO do objeto (`codigo`), para a borda responder sem banco. Como a URL não muda,
  **as receitas já assinadas e impressas entram no fluxo também**. O Worker pronto está em
  `tools/worker-validar-iti.js`; o roteiro, em `docs/validar-pelo-qr-code.md`; o rodapé só
  muda DEPOIS de o Worker existir — mudar antes imprimiria promessa que o endereço não
  cumpre.
  ⚠️ **E o Capítulo VI do guia toca no nosso arquivo de duas assinaturas**: o VALIDAR
  recomenda a 1ª assinatura declarar `DocMDP` (P=2 permite formulário e novas assinaturas)
  e avisa que, sem isso, PDF com atualização incremental "poderá" sair como "Assinatura
  Indeterminada". Afeta só a prescrição de infusão (interna, art. 13 — não vai à
  farmácia); declarar DocMDP mexe em como o PDFsharp escreve a 1ª assinatura, que é área
  congelada. Registrado como decisão futura, não tomada de raspão.

- **AUDITORIA DA AGENDA ANTES DE PRODUÇÃO — dois módulos, e nenhum dos achados quebrava
  nada** (parcela 69). A direção pediu o estado da Agenda na Recepção e no Consultório
  antes de subir. O ponto de partida era o de sempre: **build verde, 1612 testes verdes,
  as três redes locais verdes** — e a varredura devolveu um bloqueador que impede a
  recepcionista de fechar um feriado e um buraco que faz o paciente do balcão sumir do
  app do médico. As famílias, e o que cada uma ensina:

  ⚠️ **DUAS PORTAS PARA O MESMO ATO, DOIS BITS DIFERENTES — e a de fora era a mais larga.**
  "Fechar agenda…" na barra da Agenda pedia `EditarAgenda` (o comentário ao lado diz, com
  todas as letras, "o MESMO bit do Novo horário"), e o Salvar da janela pedia
  `GerenciarEquipe`. `PerfilAcesso.Recepcao` tem o primeiro e não tem o segundo: a
  recepcionista atravessava a porta, escolhia o 25/12, escrevia "Natal" e levava *"Seu
  acesso não permite fechar a agenda"* no clique final. Feriado e férias simplesmente não
  entravam no sistema pelo balcão.
  Não é a parcela 41 ("guarda que volta em silêncio") nem a 51 ("tela com uma barreira
  só"): as duas barreiras existiam e **discordavam sobre que ato era aquele**. A regra
  que fecha o assunto: **quando uma janela é aberta por mais de uma tela, a guarda do
  Salvar é a UNIÃO dos bits das portas** (`ExigirAlgum`), nunca o de uma delas — senão a
  porta que ficou de fora vira um corredor sem saída, e quem a atravessa descobre isso
  depois de fazer o trabalho todo.

  ⚠️ **O ATENDIMENTO AVULSO NASCIA SEM PROFISSIONAL, e é assim que o paciente some do app
  do médico.** "Novo atendimento" — a porta de quem chega sem hora marcada — chamava
  `AgendarAsync` sem `profissionalId`, e a tela nem perguntava quem ia atender. O
  encaixe nascia órfão, e órfão não aparece em lugar nenhum do Consultório: "Meu dia" e
  "Minha semana" filtram por `ProfissionalId`, e o **repasse também** — ele lê quem
  atendeu do AGENDAMENTO, porque `Atendimento` não guarda profissional. O médico atendia
  alguém que o app dele nunca mostrou, e não era pago por aquela sessão.
  Nada falhava: a guia nascia certa, a tela dizia "Atendimento registrado — 2 guia(s) no
  faturamento", e o balcão via o paciente na Fila (que não filtra por profissional). É o
  **elo partido da parcela 33 na forma mais cara**: não vira erro, vira ausência — e
  ausência é indistinguível de "hoje não teve".
  A lição para a próxima porta: **ao criar um caminho novo que grava um agendamento,
  liste quem LÊ agendamento e confira se o registro novo satisfaz o filtro de cada um.**
  São três leitores com o mesmo filtro (quadro do dia, semana do profissional, repasse) e
  um sem filtro (a fila do balcão) — e é justamente o sem filtro que faz o defeito passar
  despercebido no teste de mesa.
  ⚠️ O seletor **não impede**: sem profissional escolhido a sessão é lançada assim mesmo,
  porque a guia é o que a clínica não pode perder (a hierarquia da parcela 65). O que ele
  faz é dizer, ao lado do campo, o que a lacuna custa — e é isso que transforma um buraco
  invisível numa decisão de quem está no balcão.

  ⚠️ **"EMPURRAR AS SESSÕES" APAGAVA A ESPECIALIDADE E A VARIANTE DA MODALIDADE.**
  `RemarcarEmLoteAsync` (o botão que desloca as trinta sessões de umas férias) chama
  `RemarcarAsync` **só com a data nova**, porque é só isso que ele muda. Do outro lado, a
  atribuição era incondicional: `ag.ModalidadeCodigo = modalidadeCodigo ?? modalidade.
  ToString()` trocava "Acupuntura (domiciliar)" por "Acupuntura", e a especialidade da
  consulta ia a nulo. O empurrão respondia *"30 sessão(ões) empurradas"* e o estrago
  aparecia semanas depois, **uma paciente por vez**, na guia que nascia errada.
  É a regra da parcela 68 — **nulo quer dizer "o chamador não sabe", nunca "desligue"** —
  no lugar onde ela ainda não tinha sido aplicada. Quem MUDA a modalidade é quem informa
  o código dela, e só nesse caso a especialidade que vem junto é autoridade (inclusive
  para limpar). A correção mora no SERVIÇO e não no chamador, pela razão de sempre: são
  quatro portas para remarcar, e a próxima também não vai saber os códigos.

  ⚠️ **REMARCAR LEVAVA JUNTO OS CARIMBOS DA FILA.** A etapa do kanban é DERIVADA de
  `ChegadaEm`/`ChamadoEm`/`InicioAtendimentoEm`, não é coluna no banco — e `RemarcarAsync`
  nunca os tocou. O paciente que fez check-in às 9h e pediu para remarcar aparecia, na
  quinta-feira, já na raia "Na recepção" **antes de sair de casa**, com a espera contada
  desde terça (a espera vai da chegada até agora). Se ele tinha entrado na sala antes de a
  sessão ser interrompida, aparecia em "Em atendimento".
  Só se limpa quando a DATA muda: remarcar mexendo em sala, duração ou observação é ajuste
  do horário de hoje, e apagar o check-in de quem já está sentado no balcão seria destruir
  o fato pelo caminho errado. **Estado DERIVADO de carimbo de hora tem de ser revisto em
  toda escrita que muda o que o carimbo significa** — e "mudou de dia" é a maior delas.

  ⚠️ **`EstadoDaTela` CONGELAVA A RESPOSTA NO INSTANTE DO BINDING — e quase toda tela
  escapou por acidente.** O componente decide o estado num callback de
  *DependencyProperty*, e as telas amarram `Itens` a uma `ObservableCollection` que **nunca
  é reatribuída** (elas fazem `Clear()` e `Add()` na mesma instância). Isso dispara
  `CollectionChanged`, que o componente não ouvia. `Recalcular` rodava uma vez, com a lista
  ainda vazia, e a resposta ficava ali para sempre.
  O que salvou a maioria foi um acidente: elas também amarram `Carregando`, que vira true e
  depois false a cada leitura — e é esse callback vizinho que reavaliava a lista. As três
  que amarraram **só** `Itens` ficaram com "não há nada aqui" escrito por cima do conteúdo
  cheio: a **lista de espera da agenda**, as **autorizações do convênio** na ficha do
  paciente e a **busca de CID**. Nenhuma rede pega — XAML válido, binding válido, nada
  lança.
  A lição é maior que o componente: **contrato que só funciona porque uma propriedade IRMÃ
  costuma mudar junto é contrato que ninguém consegue lembrar.** A assinatura de
  `INotifyCollectionChanged` resolve no ponto único por onde toda tela passa, e as telas
  seguintes nascem certas sem saber que a regra existe.

  ⚠️ **CANCELADO E FALTA FICAVAM NA RAIA "AGUARDANDO" DO MÉDICO SEM UMA MARCA.** Ficarem na
  coluna é decisão certa e documentada (a regra da folha do dia: quem lê às 14h precisa
  saber que as 15h vagaram). O que faltava era o selo — e ele **existia calculado**
  (`LinhaSessao.Situacao`, via `Rotular`) e o XAML nunca o leu. Dado calculado sem leitor,
  na variante em que o estrago não é *não ler* e sim **ler errado**: o médico contava cinco
  pessoas por vir e duas tinham desmarcado.
  E a tela IRMÃ, no mesmo módulo, já marcava: "Minha semana" tem `ForaDaFila` e escreve
  "Não aconteceu". É a lição das parcelas 64 e 68 pela terceira vez — **quando duas telas
  respondem à mesma pergunta sobre o mesmo dado, a que está errada é a que ninguém releu**,
  e o teste que falta é o que compara as duas.

  ⚠️ **O BIT QUE NOMEIA O ATO NÃO GUARDAVA A PORTA QUE O EXECUTA.** `LancarAtendimento`
  existe para a direção poder tirar de alguém a criação de atendimento — e a caixinha dele
  em Acessos diz "criar o atendimento — e, com ele, as guias". O **Concluir da Fila**, que
  é onde a guia de fato nasce desde a parcela 65, pedia só `EditarAgenda`. Desmarcar
  aquela caixinha fechava a tela de Novo atendimento e não fechava **nada**: a mesma pessoa
  seguia gerando guia pela porta que a clínica usa o dia inteiro.
  A causa é a parcela 65 vista do lado do acesso: **quando um ATO muda de lugar, a
  permissão dele não vai junto sozinha.** Ao mover o momento em que um fato passa a
  existir, releia quem guarda o novo momento. Nenhum perfil padrão perdeu nada aqui — só
  `Recepcao` tem os dois bits, e o Gerente tem todos: o que mudou foi o bit passar a valer.

  ⚠️ **O APP CONGELADO FAZIA CERTO E O MÓDULO ATIVO FAZIA ERRADO.** A visão de semana da
  Recepção montava as sete colunas chamando `DoDiaAsync` **em laço** — sete idas em fila
  indiana a um banco REMOTO —, mais uma oitava desperdiçada (o dia escolhido era buscado
  no começo da carga e o ramo da semana nunca o abria). `AgendaService.NoPeriodoAsync`
  existe para isso desde sempre, e o único que a usava era a agenda do **faturamento
  congelado**; `ConsultorioService.DaSemanaAsync` já tinha o comentário dizendo por quê.
  A lição: **antes de escrever um laço de leitura, procure o método que faz o período de
  uma vez — e olhe no app que ninguém edita.** Código congelado não é código errado; muitas
  vezes é onde a decisão certa foi tomada primeiro e ficou.

  ⚠️ **O SEGUNDO BLOQUEADOR, e é o mais grave dos dois: A FILA E O PAINEL DO BALCÃO NUNCA
  VIAM O QUE A OUTRA MÁQUINA GRAVOU.** Serviço registrado como `AddScoped` — e o
  `DbContext` junto — pedido ao provedor **RAIZ** vive no ESCOPO RAIZ, isto é, pela vida
  inteira do aplicativo. E o shell resolve toda tela da raiz: `SuiteApp` passa
  `host.Services` ao `ShellViewModel`, que o entrega a `IModuloApp.CriarTela`. O
  `FilaViewModel` e o `PainelViewModel` recebiam `AgendaService`,
  `PainelRecepcaoService`, `TermoProcedimentoService` e `RelacionamentoService` **por
  construtor** — logo, o mesmo `DbContext` da abertura do app até o fim do expediente.
  A consulta da agenda é RASTREADA, e o EF **não sobrescreve valores de entidade já
  rastreada**: reler o dia no mesmo contexto devolve o `ChamadoEm` que ele já tinha — nulo.
  Ou seja: **o médico clica em "Chamar próximo" e o balcão não vê**. Justamente a
  sincronização que a parcela 38 existe para garantir, e que este arquivo descreve como "os
  dois leem a mesma linha, e é isso que faz os dois quadros nunca divergirem". A releitura
  de um minuto — construída na parcela 62 para fazer o recado CHEGAR — relia pelo contexto
  que já tinha a resposta velha. Falta marcada na outra máquina, remarcação e as cinco
  contagens do painel ficavam congeladas no número da abertura, a manhã inteira. **Só o que
  era clicado NESTA máquina aparecia — o que faz o quadro parecer perfeito para quem está
  usando.**
  Agravante: `DbContext` não aceita duas operações ao mesmo tempo. A batida do relógio
  caindo em cima de um clique vira *"A second operation was started on this context
  instance"* — erro em inglês, no balcão, com o paciente na frente.
  ⚠️ **`ValidateScopes` não pega**: `Host.CreateDefaultBuilder()` só o liga no ambiente
  **Development**; em produção a resolução passa calada. Por isso a rede virou a
  **checagem 37**, que casa o construtor de todo ViewModel resolvido em `CriarTela` contra
  os tipos registrados como `AddScoped`/`AddDbContext`.
  A regra: **tela de vida longa abre ESCOPO por operação, e não recebe serviço Scoped no
  construtor.** `AgendaViewModel` e `MeuDiaViewModel` já faziam assim — e é exatamente por
  isso que a grade e o quadro do médico atualizavam e a fila não. **Quando uma tela
  atualiza e a irmã não, a diferença não está na tela: está em como ela pede os serviços.**
  ⚠️ E eram **NOVE** ViewModels, não dois — os outros sete no Financeiro e no Gerente
  (Caixa, Conciliação, Estoque, Pacotes, Plano de contas, Produção, Repasses). **Todos os
  nove foram corrigidos**, e a lista de dívida da checagem 37 está VAZIA: o conjunto
  continua no código como caminho de volta, para tela nova nascer cobrada sem ninguém
  precisar afrouxar nada.
  O Caixa era o que doía mais: um recebimento lançado na outra máquina não aparecia, e o
  fechamento do dia conferia a gaveta contra um número velho.
  Duas coisas que a correção dos sete ensinou, e que a próxima vai reencontrar:
  **(a) o serviço não vai sozinho — o sub-VM vai junto.** Cada tela grande abre uma janela
  de edição (`CategoriaEdicaoViewModel`, `ItemEstoqueEdicaoViewModel`,
  `MovimentoEstoqueViewModel`, `RegraRepasseViewModel`, `PacoteCatalogoEdicaoViewModel`,
  `PacoteVendaViewModel`, `ConsumosPacoteViewModel`, `LancamentoEdicaoViewModel`,
  `CobrancaPixViewModel`) e passava o serviço já resolvido adiante. Corrigir só a tela de
  fora deixaria a janela com o contexto da abertura do app — e é NELA que se grava.
  **(b) antes de trocar, pergunte se alguma ENTIDADE atravessa o escopo.** Carregar num
  escopo e salvar noutro deixa a entidade destacada, e o EF a trataria como nova. Aqui não
  havia nenhum caso — todas as escritas passam ID ou objeto NOVO (`SalvarItemAsync(new
  ItemEstoque { Id = … })`), e `GuiaSemLancamento` é record, não entidade —, mas isso foi
  CONFERIDO caso a caso, não presumido. É a pergunta que decide se o refactor é mecânico
  ou se precisa de desenho.
  De quebra, três `Clear()` com `await` no meio apareceram no caminho (parcela 62) e foram
  corrigidos junto: `RegraRepasseViewModel`, `PacotesViewModel` e `PacoteVendaViewModel`.

  ⚠️ **A CONSULTA CHAMADA DA LISTA DE ESPERA NASCIA SEM ESPECIALIDADE.** O formulário do
  balcão EXIGE a especialidade ("Consulta precisa de especialidade") — e
  `ListaEsperaService.ChamarAsync` **não tinha o parâmetro**. A recepcionista escolhia
  "Consulta / Psiquiatria", salvava sem erro nenhum, e o horário nascia sem ela; como o
  atendimento herda a especialidade do AGENDAMENTO na confirmação da presença, a guia saía
  sem a informação que a operadora cobra. **Campo que a TELA exige e o SERVIÇO não recebe é
  um campo que só existe para o usuário** — e a distância entre a validação e o descarte é
  de duas telas, então ninguém as lê juntas.

  ⚠️ **FALTA MARCADA POR ENGANO NÃO TINHA VOLTA NO BALCÃO.** "Marcar falta" e "Cancelar
  horário" são dois botões vermelhos lado a lado, e o clique errado some com o cartão do
  quadro na hora — porque o bloco inteiro de ações da janela do horário estava sob
  `Visibility={Binding EmAberto}`. A única porta para reabrir ficava no app de
  **FATURAMENTO**: outro programa, de outra pessoa. `RemarcarAsync` já traz o horário de
  volta (`Status = Agendado`); o que faltava era a porta — a variante da parcela 48 ("alerta
  sem porta") aplicada a um ERRO em vez de a um alerta, que é pior, porque o erro é de quem
  está olhando a tela.
  Na mesma janela, os seis botões apareciam **acesos** para quem só LÊ a agenda — enfermagem,
  financeiro e faturista têm `VerAgenda` — e a recusa chegava depois do clique. O vizinho já
  fazia certo desde sempre (o vão livre da grade e os botões da barra têm `IsEnabled`); era
  esta janela que não tinha. E o `IsEnabled` é **por botão, não no bloco**: "Confirmar pelo
  WhatsApp" e "Comprovante" são leitura, e apagá-los tiraria de quem só lê uma coisa que ele
  fazia ontem.

  ⚠️ **A RELEITURA DE FUNDO DO CONSULTÓRIO COBRIA O QUADRO CHEIO — e as três telas irmãs do
  balcão já faziam certo.** O `catch` do "Meu dia" acendia `NaoVerificado` **sem olhar se a
  carga era silenciosa**, e ele liga a sobreposição do `EstadoDaTela`: uma engasgada do banco
  na batida de um minuto escrevia "vazia por falha de leitura" por cima de um quadro com
  pacientes, sem ninguém ter clicado em nada. O comentário logo abaixo prometia o contrário
  ("a tela segue com o quadro do minuto anterior") havia parcelas — comentário que descreve
  degradação sem o código que a realiza é o defeito da parcela 67.
  Agenda, Fila e Painel do balcão saem do `catch` sem tocar em nada quando a carga é
  silenciosa. **Quando três irmãs fazem igual e uma faz diferente, a diferente é a que
  ninguém releu** — e o reset de `NaoVerificado` na ENTRADA tem de ser guardado junto, senão
  a batida silenciosa que também falha limpa o aviso da carga anterior.

  ⚠️ **E O RECADO ERA APAGADO NO MESMO INSTANTE EM QUE ERA ESCRITO.** "Fulano foi chamado — a
  recepção já está vendo o aviso" é a única confirmação de que o recado atravessou os dois
  módulos, e as quatro ações do quadro escreviam a mensagem e chamavam `CarregarAsync()` — a
  carga que a PESSOA pede, que começa zerando `Mensagem`. O comentário da própria carga já
  dizia que a recarga de fundo não apaga o recado da última ação; o que faltava era **as
  chamadas usarem a sobrecarga silenciosa**. Regra: **ação que escreve um recado e recarrega
  em seguida recarrega em SILÊNCIO** — senão a tela desfaz o que a ação acabou de dizer.

  ⚠️ **"MEUS PACIENTES" FAZIA 1 + 200 CONSULTAS A UM BANCO REMOTO PARA CALCULAR DOIS
  NÚMEROS.** A carteira do consultório lia o prontuário INTEIRO de cada paciente, um por um,
  para extrair a primeira e a última EVA — texto da evolução, conduta e orientações vinham
  junto, e eram descartados. É uma das duas portas do módulo, e ficava dezenas de segundos em
  "Montando a sua carteira…", repetindo a espera a cada volta à tela.
  Virou `ParesDeEvaDosPacientesAsync`: uma consulta, três colunas. **Antes de escrever um
  laço com `await` dentro, pergunte quantas linhas ele pode ter no pior caso** — 200 é o
  limite padrão desta tela, e estava no próprio parâmetro do método.

  ⚠️ **O QUE NÃO FOI CORRIGIDO, e por que a decisão é essa.** `ConfirmarPresencaAsync`
  grava em DOIS `SaveChanges`: o primeiro (dentro de `LancarAsync`) persiste o atendimento
  e as guias, o segundo carimba `Status`/`AtendimentoId` no agendamento. Falhando o
  segundo — conflito de `xmin` entre as duas máquinas do balcão, queda de conexão —, **as
  guias existem e o agendamento não sabe**: a recepcionista não vê guia nenhuma, o cartão
  continua com "Concluir" aceso, e o segundo clique gera outro jogo de guias. É o incidente
  de 12/08 (parcela 65) uma camada abaixo, e a idempotência daquela parcela não alcança,
  porque a chave dela é justamente o `AtendimentoId` que não chegou a ser gravado.
  Não corrigi, e isso é decisão: juntar os quatro `SaveChanges` numa transação mexe no
  caminho que **gera as guias** e que o faturamento em produção também percorre, e este
  ambiente não tem Postgres para reproduzir o conflito (os testes rodam em SQLite, onde
  `xmin` não existe — o mesmo buraco que a parcela 67 documentou para as datas com fuso).
  A regra do projeto é anterior a mim: **não corrija por dedução, reproduza antes.** Fica
  escrito aqui como o primeiro item da próxima parcela, com o roteiro: transação única em
  `ConfirmarPresencaAsync` e teste contra Postgres de verdade, não contra SQLite.

  **A fila restante, medida e priorizada** (confirmada na refutação, não corrigida nesta
  parcela — cada uma com o motivo de ter ficado):
  1. **Horário sem profissional some do quadro do médico** — o seletor novo do avulso fecha a
     porta que criava órfãos em série, mas o FORMULÁRIO de agendamento continua aceitando
     salvar sem profissional, e a coluna da visão de SEMANA não pré-seleciona ninguém. Falta
     decidir com a clínica se o campo passa a ser obrigatório (impede) ou se avisa — e a
     escolha é dela, não minha.
  2. **O "+" não volta no vão de um horário cancelado** (`CelulaAgenda.Livre` conta os
     cartões sem olhar `ForaDoDia`). A correção esbarra numa escolha de leiaute: o cartão
     cinza é desenhado por cima do botão, então liberar o vão exige ou pôr o "+" na frente
     (e perder o clique que abre o cancelado) ou mudar o desenho da célula. O caminho de
     volta agora existe pela janela ("Reabrir este horário"); a decisão de leiaute é do
     cliente.
  3. ~~A espera média do painel conta quem chegou e virou FALTA~~ — **pago na validação
     da parcela 86**: a falta ficou de fora da média como o cancelado.
  4. ~~Bloqueio de parte do dia não enxerga a sessão que COMEÇA antes dele~~ — **pago na
     parcela 86**: a consulta-base do `MarcadosDentroAsync` abre no começo do DIA e quem
     decide é o `ColideCom` (o filtro certo estava sobre a consulta errada).
  5. ~~`ConfirmarPresencaAsync` não deixa linha na trilha~~ — **pago na parcela 70**: a
     trilha `PresencaConfirmada` entra no MESMO commit atômico do `ConfirmarNucleoAsync`.
  6. **Trocar a DATA no formulário não reconfere elegibilidade** (carteirinha, cota,
     consulta a renovar) — **metade paga**: o Novo atendimento reconfere tudo ao trocar a
     data (parcela 70); o FORMULÁRIO DA AGENDA (o fallback, e o caminho da lista de
     espera) só reconfere conflitos e a consulta — a elegibilidade completa continua
     presa ao `AoTrocarPaciente`.
  7. **Horário de profissional (ou sala) DESATIVADO some da grade** — a coluna só é montada
      para os ATIVOS, e o horário com dono inativo não cai em "Sem profissional": não existe
      coluna para ele. O resumo continua contando sobre a lista inteira, então o cabeçalho
      diz "12 horário(s)" e a grade desenha 11; o vão fica clicável e a recepção marca outra
      pessoa por cima, enquanto o paciente segue na Fila e na folha impressa do dia. A caixa
      da tela de equipe diz "Ativo (aparece na agenda)", prometendo o contrário do que
      acontece. É diferente do item 1: aqui o `ProfissionalId` existe — quem sumiu foi a
      coluna.
  (O item que estava aqui — os sete ViewModels restantes com serviço Scoped no construtor —
  saiu da fila: foram corrigidos na mesma parcela, e a checagem 37 fecha o assunto. Quatro
  outros saíram na rodada "todas as notas em 8" do Consultório, mais abaixo: a autoria da
  fila, as observações que não chegavam, a evolução avulsa cobrindo duas sessões e o
  Atender sem conferir `VerProntuario`. O 6 saiu na parcela 70, junto da unificação. Os
  itens **3, 4 e 7** foram pagos na parcela 85 — ver a lição abaixo; restam o **1** e o
  **5** (a transação única + trilha do `ConfirmarPresencaAsync`, que exigem Postgres de
  verdade) e o **2**, que espera a decisão de leiaute do cliente.)

- **A VARREDURA DA RECEPÇÃO DEPOIS DA AGENDA — três achados, nenhum bloqueador, e todos
  da mesma família** (parcela 69, continuação). Com a Agenda fechada, a pergunta virou
  "o que mais no balcão promete e não cumpre?". A varredura foi por eixo (mensagem de
  êxito invisível, EstadoDaTela na raiz, método órfão, propriedade sem leitor, texto de
  tela que promete mecanismo), e o resultado tem as duas metades: o que achou e o que
  CONFERIU E ESTAVA CERTO — sem a segunda, a próxima varredura refaz esta.

  ⚠️ **A ORIGEM DO PACIENTE ERA PERGUNTADA A TODO CADASTRO E NINGUÉM SOMAVA.** O balcão
  pergunta "como conheceu a clínica?" desde que o campo existe, e a resposta era lida em
  UM lugar: a própria ficha, uma pessoa por vez ("Indicação de Maria"). Nenhum serviço,
  relatório ou tela agregava — a direção não tinha como responder "quantos vieram por
  indicação neste ano?", que é a única razão de a pergunta ser feita. O detalhe que dói:
  o comentário do rótulo em `RotulosEnum` já dizia *"é um dos poucos campos que a direção
  lê agrupado num relatório"* — **o rótulo foi preparado para um relatório que nunca
  existiu**. Virou `OrigemPacientesService` + a tela "De onde vêm os pacientes" no grupo
  Marketing/Recall do Gerente. As decisões:
  (a) **"Estreou no período" = o PRIMEIRO atendimento caiu no período** — `Paciente` não
  tem data de cadastro, e a estreia é o fato mais honesto disponível: cadastro sem
  atendimento é intenção, atendimento é a clínica trabalhando. Quem já vinha e continuou
  vindo não estreou. A tela ESCREVE a definição: número cuja definição só existe no
  código é um número que cada leitor interpreta de um jeito.
  (b) **"Não perguntado" é linha de primeira classe, ordenada junto das outras** — quando
  ela encabeça a tabela, o achado do relatório é o balcão ter parado de perguntar, e o
  selo "colher no cadastro" transforma o número em tarefa. Escondê-la no rodapé faria a
  direção decidir anúncio sobre uma amostra que ninguém está colhendo.
  (c) **Quem indica agrupa por nome NORMALIZADO** (trim + maiúsculas fora): "maria silva"
  e "Maria Silva " são a mesma pessoa digitada por duas recepcionistas, e separá-las
  esconderia justamente a maior indicadora da clínica.
  A lição que generaliza: **quando um campo é PERGUNTADO no balcão, procure quem RESPONDE
  com ele.** Campo que só a ficha individual lê é pergunta feita de graça — e rótulo
  preparado "para o relatório" é promessa que se confere como as outras.

  ⚠️ **"CANCELAR O RESTO DA SÉRIE" CONFIRMAVA ÀS CEGAS — com a prévia pronta e sem
  chamador.** O diálogo dizia "cancelar TODAS as sessões ainda marcadas?" sem dizer
  QUANTAS nem QUAIS; a contagem só aparecia no snackbar, DEPOIS do estrago. E
  `AgendaService.DaSerieAsync` — "sessões de uma série, na ordem em que acontecem" —
  existia sem um único chamador em produção: a leitura que responde a pergunta do diálogo
  estava pronta desde que a série nasceu. Agora a prévia vem antes (quantas e as datas,
  até dez), série sem sessão em aberto ganha aviso em vez de silêncio, e falha na leitura
  IMPEDE a pergunta — confirmar "todas" sem saber quantas é exatamente o que a correção
  existe para acabar. **Ação destrutiva em LOTE diz o tamanho do lote ANTES do clique**;
  "todas" não é número.

  ⚠️ **NÃO EXISTIA TROCAR A PRÓPRIA SENHA.** `AcessoService.TrocarSenhaAsync` — o método
  que CONFERE a senha atual antes de aceitar a nova — estava órfão. O único caminho de
  troca era o forçado (a direção emite provisória com "deve trocar", o login cobra a
  definitiva): quem desconfiasse que alguém viu a sua senha precisava pedir à direção,
  que escolhia a nova e a ENTREGAVA — **trocando um segredo comprometido por um que já
  nasce compartilhado**. Entrou `TrocaSenhaWindow` ao lado do "Trocar usuário" — nos DOIS
  apps, porque é o débito permanente da Fase 4 (a lição da parcela 60: a mesma ação nos
  dois lados, e a cópia que faltar é onde a capacidade some). Sem permissão a exigir: a
  prova de posse é a senha atual, e é o serviço que a confere — validar na tela seria a
  segunda definição da mesma regra. A lição: **fluxo de segurança com só a metade
  administrativa não está completo** — o reset pela direção existia desde a parcela 5, e
  a metade voluntária ficou 60+ parcelas sem porta porque o fluxo forçado dava a
  impressão de assunto coberto.

  **CONFERIDO E LIMPO — para a próxima varredura não refazer**: os 13 candidatos a
  "mensagem de êxito invisível" eram falsos positivos (todos só escrevem ERRO no campo; a
  parcela 62 já tinha limpado); nenhum `EstadoDaTela` na raiz de página; a tela de
  Retorno confere `TemConsentimento` pela MESMA leitura da campanha e escreve o motivo na
  linha; a ficha→CRM lê `ContatoCampanha` de verdade, com contador de geração; o circuito
  autorização→aviso de cota fecha. E `TracoAsync` era código morto (terceira definição de
  uma leitura que dois serviços já fazem direto no repositório) — método órfão continua
  sendo SINTOMA: dos 15 órfãos reais, três eram feature faltando e o resto era duplicata.

- **A VARREDURA DO FINANCEIRO — mais limpo que a Recepção estava, e o achado grave era o
  mais antigo de todos** (parcela 69, continuação). Os mesmos eixos da varredura do
  balcão, aplicados ao módulo do dinheiro. O que achou, o que ensina, e o que estava
  CERTO — na ordem de sempre.

  ⚠️ **O ESTOQUE NÃO TINHA EXTRATO — e o motivo escrito da perda não tinha leitor.** "O
  saldo é a SOMA dos movimentos" é regra desde a parcela 4, a perda EXIGE motivo escrito
  (única recusa do serviço) e o acerto de inventário grava direção, observação e
  `CriadoPor` (parcela 30) — e `MovimentosAsync`, o extrato de um item, ficou órfão desde
  que nasceu: tudo gravado, nada lido. A clínica exigia justificativa da funcionária e a
  guardava num lugar que nem a direção alcançava — a trilha da parcela 21 de novo, na
  versão do estoque. E no dia em que o saldo não bate, a única pergunta útil ("QUAIS
  movimentos produziram este número?") não tinha tela. Virou o botão "Extrato" na linha
  do item: todos os movimentos com o saldo APÓS cada um, o motivo da perda em destaque,
  a direção do acerto escrita e quem fez.
  Três decisões que vieram junto, e as duas primeiras são maiores que a tela:
  (a) **A recusa da perda sem motivo subiu para `MovimentarAsync`** — ela morava SÓ no
  wrapper `PerderAsync`, que nenhuma tela chama; a janela genérica entra pelo método
  genérico, e a única barreira era a validação da TELA. É o defeito recorrente vestido de
  validação (a regra do número da guia): quem valida na tela cobre uma porta. O teste
  novo FALHA no código antigo.
  (b) **O sinal do movimento desceu para o DOMÍNIO** (`MovimentoEstoque.Delta`/`DeltaDe`),
  porque agora há DOIS somadores — o saldo do repositório e o extrato — e duas cópias da
  conta divergiriam exatamente no AJUSTE, o movimento raro que ninguém testa de cabeça.
  De quebra, isso removeu o par `Sinal`/`QuantidadeComSinal`: dizia "−1 para tudo que não
  é entrada", erraria o ajuste PARA CIMA, e nunca doeu por nunca ter tido um chamador —
  **propriedade de domínio errada sem chamador é uma mina, não um detalhe: o primeiro uso
  a detona.** `O_saldo_oficial_e_a_soma_dos_Deltas_sao_o_mesmo_numero` fixa que o extrato
  não pode desmentir a tela que ele existe para explicar.
  (c) O extrato acumula NA ORDEM CRONOLÓGICA e exibe do mais recente — a última linha TEM
  de bater com o saldo da lista de trás.

  ⚠️ **A FAIXA DA ALÍQUOTA ÚNICA SUMIA PELA CONDIÇÃO ERRADA — e a frase ao lado mentia
  junto.** O serviço cai no fallback quando não há tributo VIGENTE NO DIA
  (`ApurarAsync`); a tela escondia a faixa quando havia tributo CADASTRADO
  (`Tributos.Count` no DataTrigger). Na janela entre as duas condições — tributos
  cadastrados com vigência futura, ou todos expirados — a alíquota única continuava
  valendo no cálculo com o campo dela INVISÍVEL, e a frase `OrigemDaCarga` dizia "Nenhum
  tributo cadastrado" com a lista cheia. O detalhe que dói: o MESMO ViewModel calculava a
  frase com a condição certa (`ValendoAgora`) e a visibilidade com a errada, a dez linhas
  de distância. **Campo invisível que faz efeito é pior que o campo morto que a parcela
  49 tirou desta mesma tela** — e a lição é: quando a tela mostra/esconde algo POR CAUSA
  de uma regra de serviço, a condição da visibilidade é A MESMA do serviço, nunca uma
  aproximação ("cadastrado" não é "vigente").

  ⚠️ **O CSV DA INADIMPLÊNCIA SAÍA COM NOME + DÍVIDA DO PACIENTE SEM A SEGUNDA BARREIRA.**
  O `Receber` da mesma tela tinha o `Exigir`; o `Exportar` não — a mesma lacuna que a
  parcela 64 fechou nos exports do Gerente, sobrevivendo no módulo vizinho. **Quando um
  defeito de padrão é corrigido num módulo, os exports dos OUTROS módulos entram na mesma
  varredura** (é a lição da 64 sobre alcance, cobrada de novo). O FluxoCaixa ganhou o
  mesmo tratamento (agregados, sem dado pessoal — mas a regra "CSV é saída de dado" não
  tem exceção por conteúdo).

  ⚠️ **"CÓDIGO COPIADO" DO PIX NUNCA APARECIA.** A janela nasceu DEPOIS das limpezas das
  parcelas 62/64 e repetiu o padrão que elas mataram: o sucesso da cópia escrevia
  `MensagemEhErro = false` e a única superfície de mensagem era
  `Visibility="{Binding MensagemEhErro}"`. Quem copiava não via confirmação — e clicava
  de novo, ou copiava à mão sem precisar. Correção pelo padrão canônico (quem decide se
  aparece é o TEXTO; quem decide a cor é a GRAVIDADE). A lição é sobre recorrência:
  **padrão corrigido em N telas volta na tela N+1 se só as telas foram corrigidas** — o
  par `AlertaPerigo`+`MensagemEhErro` continua sendo o que a mão escreve primeiro.

  **Menores da mesma rodada**: cinco `Clear()` com await no meio (Contas 2×, Resultado,
  Taxas 2×) — cargas de abertura, janela pequena, corrigidos pelo padrão da 62; e
  `Resolvida` no ExtratoBanco, calculada e nunca lida, removida.

  **CONFERIDO E LIMPO — para a próxima varredura não refazer**: menu × `CriarTela` fecha
  (os 3 "sem caso" são grupos com abas, resolvidos pelo shell); nenhum `EstadoDaTela` na
  raiz nem sem gatilho; dos 21 arquivos com o padrão de `Border` de mensagem, só o Pix
  escrevia êxito; "Sem categoria aparece, nunca some" cumprida (Fluxo e Resultado); a
  reabertura do caixa fica marcada COM o motivo lido na tela; `DesfazerConfirmacaoAsync`
  tem porta; "Só no banco" e "Só no sistema" ambos no extrato OFX. **E dois falsos
  positivos meus que valem regra de varredura**: `ValorRotulo` parecia órfão porque o
  leitor é um template COMPARTILHADO no shell (`ItemBarraRotulada`) — scan de leitor que
  só olha o XAML do módulo não enxerga template compartilhado; e `EntrarAsync`/
  `PerderAsync`/`CustoDoAtendimentoAsync` pareciam duplicatas e são WRAPPERS de uma
  definição só (delegam a `MovimentarAsync`/`Precificar`) — **wrapper fino não é segunda
  definição; a pergunta é onde mora a REGRA, não quantas assinaturas existem.**

- **A VARREDURA DO GERENTE E DO CONSULTÓRIO — nenhum grave, e o melhor achado estava no
  vizinho** (parcela 69, fim da rodada). Os dois módulos mais auditados do projeto
  (Gerente: 54/64; Consultório: 61/68 e a auditoria da Agenda) saíram os mais limpos —
  as varreduras anteriores pagaram. Os quatro achados, e o que cada um ensina:

  ⚠️ **DUAS DEFINIÇÕES DO TOTAL DO BADGE — e o teste fixava a que ninguém executa.**
  `PendenciaService.TotalPendenciasAsync` dizia, no próprio comentário, ser "o total para
  o badge do topo" — e estava ÓRFÃO: o badge real era uma segunda conta, escrita à mão no
  `DashboardViewModel`, com um comentário jurando usar "o mesmo critério" e CITANDO o
  método que não chamava. As duas contas eram iguais hoje; a primeira mudança em qualquer
  uma divergiria em silêncio — e `PendenciasFaturamentoTests` fixava o lado morto. O
  critério virou UMA função (`TotalParaBadge`, estática) que os dois lados chamam, e o
  teste passou a exercitar o que a tela usa. A lição: **comentário que cita um método como
  "o critério" é promessa que se confere com grep — se o método está órfão, o comentário
  é a segunda definição se anunciando.**

  ⚠️ **A TELA MANDAVA COMPRAR TABLET — e a decisão documentada é MONITOR.** A parcela 66
  gastou uma seção inteira em "por que MONITOR e não tablet" (tablet é outro computador;
  monitor touch é só mais uma tela do Windows), a especificação de compra está em
  `docs/termo-assinado-pelo-paciente.md` §3.8 — e TRÊS textos de tela diziam "tablet":
  a configuração do termo no Gerente, a instrução que o PACIENTE lê na janela de
  assinatura e a descrição da folha na central de documentos. Texto de tela que contradiz
  a decisão de arquitetura induz a clínica a comprar o hardware errado — e o texto novo da
  janela de assinatura lembra o modo de UMA tela ("ou com o mouse"), que é o modo real de
  quem ainda não comprou o touch. **Quando uma decisão renomeia o mundo (tablet→monitor),
  os textos de tela entram no mesmo grep da decisão.**

  **Menores**: `MinhaSemana` e `AnexosSessao` limpavam a coleção antes do await (o item
  que a fila da parcela 69 já listava — pago); `TemAssinatura` na linha da lista de
  infusão, calculada e nunca lida (a `Situacao` textual já diz "assinada"), removida.

  **CONFERIDO E LIMPO — para a próxima varredura não refazer**: menu × `CriarTela` fecha
  nos DOIS (as cinco chaves "sem caso" do Consultório caem no `or` múltiplo do
  workspace — padrão que o scan de regex simples não vê); nenhum `EstadoDaTela` raso ou
  sem gatilho; TODAS as mensagens de êxito dos dois módulos aparecem (o Gerente foi limpo
  na 64, e as janelas novas do Consultório já nasceram no padrão certo — a primeira
  geração de telas que nasceu imune ao defeito); `PoliticaBackupService` roda na abertura
  do Gerente; o `AlertaDeItem` do PHQ-9 chega à tela de Avaliações; a fila de pendências
  do Gerente critica o número da guia linha a linha com a `RegraNumeroGuia` (parcela 51
  cumprida); "quem não consentiu aparece CONTADO" cumprida nas Campanhas. **Falsos
  positivos com lição**: `EhVerde`/`Urgencia`/`FormatoGuia` pareciam órfãos e alimentam
  DataTriggers e o fluxo de baixa (o verde é o DEFAULT do estilo — propriedade que define
  o caso-padrão não aparece em trigger nenhum); `Enunciado` alimenta `Rotulo`;
  `Correcoes` alimenta `Retificada`/`CorrecoesTexto` — **scan de leitor precisa seguir
  UMA derivação interna antes de acusar.**

- **A RODADA "TODAS AS NOTAS EM 8" DO CONSULTÓRIO** (parcela 69, encerramento — o
  diagnóstico pessimista deu notas de 4 a 8 por dimensão, e a direção mandou: *"Faça
  todas as notas baterem 8!"*). Tudo o que segurava cada nota foi pago de uma vez; as
  lições, na ordem do que ensinam:

  ⚠️ **A EVOLUÇÃO AVULSA CASA COM NO MÁXIMO UMA SESSÃO DO DIA.** Com duas sessões do
  mesmo paciente no mesmo dia (manhã e tarde — quase sempre especialidades diferentes),
  uma evolução sem `AgendamentoId` dava as DUAS por escritas: a segunda sumia da
  cobrança, e abrir qualquer uma na tela de Atendimento CONTINUAVA o mesmo texto,
  fundindo duas sessões num registro só. `EvolucaoDoHorario` agora recebe as sessões
  IRMÃS do dia (de TODOS os profissionais, antes do filtro — sem conhecê-las não há como
  saber a vez de cada uma na fila da avulsa) e distribui cronologicamente: avulsas na
  ordem em que foram escritas, sessões na ordem em que aconteceram; cancelada não
  disputa; quem tem evolução própria não disputa. É escolha determinística sobre um dado
  que não diz de quem é — e **a que erra, erra para o lado de COBRAR, nunca de calar**.
  A tela de Atendimento busca as irmãs (`SessoesDoPacienteNoDiaAsync`) porque ela conhece
  o horário chamado e não as vizinhas dele.

  ⚠️ **A FILA GANHOU AUTORIA — e o movimento que APAGA escreve o que apagou.** Os cinco
  movimentos (chegada, chamada, desfazer, entrada, voltar) recebem `operador`
  OBRIGATÓRIO (parâmetro com default deixaria todo chamador antigo compilando sem
  autoria — o compilador é quem acha os call sites) e gravam `EventoAuditoria` no mesmo
  `SaveChanges`. Duas regras que não são detalhe: **movimento idempotente que não mudou
  nada não grava linha** (trilha com duplicata a cada clique é trilha que ninguém lê), e
  **`VoltarEtapaAsync`/`DesfazerChamadaAsync` escrevem NA TRILHA o carimbo que apagaram,
  com o valor** — depois do apagamento ele não existe em mais lugar nenhum, e "quem
  desfez e o que dizia" é a pergunta de qualquer conferência.

  ⚠️ **A MINHA SEMANA VIROU A GRADE DO BALCÃO — e o montador mora na APPLICATION.** A
  pilha de cartões por dia era o desenho que a parcela 58 condenou com a frase que
  decide ("numa grade, o vazio TEM tamanho"), sobrevivendo na tela irmã da mesma
  pergunta. As regras da grade são AS MESMAS do balcão de propósito (janela padrão
  esticada, continuação, cancelado marcado sem cobrir, bloqueio escrito na célula) —
  duas grades da mesma agenda com regras diferentes divergiriam sobre o mesmo horário; a
  janela padrão (7h–20h) subiu para o DOMÍNIO (`Agendamento.AberturaPadraoGrade`) pela
  mesma razão. E `GradeSemana.Montar` é função PURA (recebe o relógio) na Application,
  porque a camada de tela do WPF não compila nos testes: **tudo o que decide um desenho
  precisa morar onde o `dotnet test` alcança** — a grade nasceu com sete testes, coisa
  que nenhuma tela do projeto teve. A diferença deliberada: a célula livre NÃO tem
  clique de marcar — quem marca é o balcão; aqui o vão é informação.

  ⚠️ **"CHAMAR PRÓXIMO" NO MODO SEM VÍNCULO CHAMAVA O PACIENTE DE OUTRO PROFISSIONAL.**
  Sem `Profissional` ligado ao login a tela mostra a clínica inteira — e "o primeiro da
  recepção" podia ser de qualquer colega: o clique cego anunciava um nome para a sala
  errada. O botão fica DESLIGADO nesse modo, com a explicação na tela e a segunda
  barreira no comando; **chamar pelo CARTÃO continua liberado**, porque ali a escolha é
  de quem leu o nome antes de clicar. E **a fila corre só HOJE**: os botões de movimento
  somem num dia passado/futuro e o arrasto recusa DIZENDO — chamar alguém de ontem
  carimbaria hora num horário morto e a tela afirmaria "a recepção já está vendo o
  aviso" sobre uma fila que só relê o dia corrente (afirmação falsa com cara de
  confirmação).

  **O resto da rodada, em uma linha cada**: as OBSERVAÇÕES do horário chegaram ao cartão
  do Meu dia (`SessaoDoDia.Observacoes` — onze campos e nenhum era o recado do balcão);
  férias/feriado apareceram (célula da semana + linha "Agenda fechada neste dia" no Meu
  dia — dia fechado aparecia como dia VAZIO, que se lê como "ninguém marcou"); a batida
  de 1 min parou de reler 30 dias de pendências (a recarga silenciosa relê só o quadro —
  quem escreve evolução está NESTA máquina); "Atender", a dívida e o "Ver a lista" dos
  Meus números ganharam as duas barreiras de `VerProntuario` (`NavegacaoSuite.Ir`
  devolve false em silêncio para quem não tem o bit do destino); o botão "Excluir" de
  medidas/avaliações virou "Cancelar…" (**o ato sempre foi cancelar com motivo — o
  RÓTULO é que mentia sobre o que o registro clínico não faz**); a frase da tendência da
  dor parou de sair duplicada (quando ela preocupa, só o alerta a diz — um estado por
  pergunta); a lista de problemas virou LINHA DENSA (cada problema tinha cinco linhas e
  QUATRO botões empilhados ≈ 140 px — três problemas empurravam o histórico, que é o
  assunto da aba, para fora da vista; detalhe foi para a dica, ação virou fileira, nada
  foi tirado); o "N sem evolução" da semana diz "NESTA semana" (o badge do Meu dia conta
  a fila de trabalho de 30 dias — dois números com a mesma frase se leem como o mesmo
  número errado); e o cabeçalho do Atendimento diz a DATA do horário quando não é hoje
  (a dívida e a semana abrem dias passados, e "da agenda de hoje" mentia sobre a data a
  que o registro ia ficar ligado).

  **O que ficou de fora, e é decisão**: a trilha do `ConfirmarPresencaAsync` (item 5 da
  fila) — mexer nele encosta no caminho que gera as guias, e vai junto da transação única
  com teste em Postgres de verdade, que este ambiente não reproduz.

- **A GUIA NASCE QUANDO O ATENDIMENTO ENTRA NO SISTEMA — e a duplicidade morreu de
  desenho, não de aviso** (parcela 70; o mapa completo está em
  `docs/guia-no-agendamento.md`, e é lá que se atualiza). O pedido da direção: *"o
  confirmar presença não pode gerar guia! A guia precisa nascer no momento em que a
  secretaria coloca o atendimento no sistema, seja avulso ou agenda"* — e depois:
  *"unificar tudo em um lugar só"*. As decisões que não são óbvias pelo código:
  **O clique virou UM SaveChanges.** `LancarAsync` se dividiu em CRIAÇÃO
  (`MontarAsync`, não grava) × PRESENÇA (`PrepararPresencaAsync` encena NCs no mesmo
  commit; `ConcluirPresencaAsync` renova consulta depois, falha vira aviso), e
  `ConfirmarNucleoAsync` pendura o atendimento no agendamento pela NAVEGAÇÃO — o EF
  grava horário + atendimento + guias + carimbo + trilha numa transação. Ou existe
  tudo, ou nada: o incidente de 12/08 (três encaixes em 71s) morava nos vãos entre
  cinco SaveChanges. `AtomicidadeDoLancamentoTests` prova com um DbContext que falha no
  N-ésimo save e afirma ESTADO, não mensagem — dá para provar atomicidade no SQLite.
  **O regime novo mora atrás de uma chave** (`ChaveGuiaNoAgendamento`, nasce DESLIGADA):
  os cinco apps se atualizam por canais separados e dividem um banco — um binário velho
  confirmando presença de um horário que o novo já marcou COM guia duplicaria; a chave
  se liga depois de todos atualizarem, e ligar dispara o backfill de `RealizadoEm`.
  **`Atendimento.RealizadoEm` é o que separa "marcado" de "aconteceu"**: leitor que
  quer dizer "sessão realizada" filtra por ele (retenção, origem, estreia); a COTA
  conta os ATIVOS (realizado OU guia aberta/baixada) — é assim que a 11ª sessão de uma
  autorização de 10 avisa NA MARCAÇÃO, não na glosa. Cancelar/faltar suspende as guias
  abertas (`NaoAplicavel` + marca `MarcaSuspensao` na observação — NUNCA NC, que a
  volta do paciente reabriria, e NUNCA valor novo de enum, a mina da parcela 67);
  reabrir devolve SÓ as marcadas; baixada não se toca — vira aviso. Remarcar de data
  desloca as previstas; de modalidade, regera (a velha fica "Substituída") ou RECUSA se
  algo já saiu da clínica (baixa, lote, NC).
  **A tela pergunta QUANDO** (o §3.7): "o paciente está aqui — lançar agora" × "marcar
  dia e horário", no MESMO formulário — modalidade, prévia, elegibilidade e capa servem
  aos dois modos. A agenda MOSTRA e mexe no existente; criar mora no Novo atendimento, e
  o clique no vão da grade chega lá pré-preenchido (`PreenchimentoNovoAtendimento`,
  singleton de UM pedido que consumir LIMPA — pedido órfão pré-preencheria a abertura
  de amanhã com o clique de ontem). **Sem profissional não é sorteio**: marca, avisa o
  custo (fora do Meu dia e do repasse) e nunca escolhe sozinho — repasse é dinheiro.
  **A pergunta de duplicidade ficou INFORMADA** (`CapasDoDiaAsync`, ponto único): número
  do atendimento, modalidade, quem lançou e o placar das baixas, no aviso ao escolher o
  paciente E na pergunta do clique — nas DUAS portas de criação (Novo atendimento e o
  formulário da agenda, que sobrevive para lista de espera/remarcar/fallback).
  **Permissão acompanhou o momento do fato** (a lição da parcela 69 aplicada de novo):
  com a chave ligada, marcar CRIA guias — o Salvar exige `EditarAgenda` E
  `LancarAtendimento`; e quem tem só `EditarAgenda` não perdeu nada, porque o
  redirecionamento da agenda cai no formulário antigo quando `NavegacaoSuite.Ir`
  devolve false (regra 3 do faturamento: a unificação não tira capacidade).
  ⚠️ **A auditoria do próprio diff achou o leitor que o inventário da Fase 3 pulou — e
  era o de DINHEIRO.** O alimentador do REPASSE (`AgendamentosComAtendimentoAsync`)
  filtrava só "tem atendimento": com a chave ligada, a sessão MARCADA — e a cancelada,
  que mantém o `AtendimentoId` com as guias suspensas — entraria na regra "valor por
  atendimento", pagando sessão que ninguém deu. O filtro virou `Status == Realizado`
  (no regime antigo não muda nada: `AtendimentoId` só nascia junto do `Realizado`), com
  teste que confirma a presença e vê o valor aparecer. Os outros dois achados da mesma
  auditoria: o formulário de agendamento do FATURAMENTO sem nenhuma `Exigir` (a cópia
  que ficou para trás, parcela 60 — ganhou as duas guardas, inclusive a do duplo bit
  com a chave ligada) e a tela QUANDO reconferindo cota/consulta/elegibilidade ao
  trocar a DATA (o item 6 da fila da parcela 69, que a unificação tornou cotidiano) —
  com a elegibilidade completa (dívida, glosa, pacote, termo) que o formulário
  substituído já mostrava: **porta unificada não pode mostrar MENOS que a porta que
  aposentou.**
  ⚠️ **A auditoria do GERENTE sob o regime novo — conferido e limpo, para a próxima
  varredura não refazer** (parcela 70, depois do achado do repasse): todo leitor do
  Gerente que responde "quantas sessões" filtra certo — `IndicadoresService` (Atendidos/
  produtividade/ocupação/série mensal) e `RelacionamentoService` (padrão de falta) por
  `Status == Realizado`; `CompletudeProntuario` divide evoluções por Atendidos
  (Realizado); retenção e origem/estreia por `RealizadoEm` (Fase 3); metas/orçamento/
  painel por dinheiro ou pelo serviço dono; a fila de pendências do Gerente pelo
  `EstaPendente`, que exige a data prevista alcançada. E os relatórios por CÓDIGO
  (`RelatorioService.PorConvenio`, rentabilidade) **contam a guia marcada quando o
  período pedido inclui dias futuros — e isso é decisão, não defeito**: a guia existe e
  pode ser baixada antecipadamente; filtrar pela data esconderia justamente a baixada
  antecipada, que é falha exibida como sucesso. Período já encerrado sai exato (a
  cancelada vira `NaoAplicavel` e o `CodigosNoPeriodoAsync` já a exclui). A chave em
  Configurações do Gerente tem as duas barreiras (`GerenciarUsuarios` no item e no
  `ExecutarAsync`), grava só quando MUDOU (ligar dispara o backfill, e repeti-lo a cada
  Salvar escreveria a tabela inteira à toa) e o aviso na tela diz a ordem: atualizar os
  cinco apps ANTES de ligar.
- **O carimbo da assinatura saía no ALTO da folha: o eixo Y do `/Rect` é medido do PÉ da
  página** (parcela 68, 8ª rodada — a clínica mandou o print: o carimbo do prescritor por
  cima do título "PRESCRIÇÃO DE EXECUÇÃO INTERNA", e o rodapé com o espaço reservado
  vazio logo abaixo). A fórmula era `AlturaPagina - MargemPagina - AlturaRodape + 2`, ou
  seja a distância a partir do TOPO — e o retângulo vai para o `/Rect` da anotação, que no
  PDF tem origem no canto INFERIOR esquerdo. **Medido antes de corrigir**: assinei um PDF
  pedindo `Y=640` e o arquivo saiu com `/Rect [40 640 280 686]` — o PDFsharp grava o que
  recebe, sem converter. A conta certa é a do próprio rodapé:
  `MargemPagina + AlturaRodape - AlturaFaixaAssinatura + 2`.
  ⚠️ **Saiu assim em TODO documento assinado até 20/08/2026** — receita, atestado,
  prescrição —, nos DOIS serviços de PDF, desde a parcela 42. Nenhuma rede pegava e nenhum
  teste olhava: os testes de assinatura provavam que o arquivo **fecha**, nunca onde o
  carimbo **cai**. `CarimboNoRodapeTests` fixa o invariante (o retângulo cabe dentro da
  faixa do rodapé, medida do pé) e **inclui a prova de que a fórmula antiga falharia** —
  teste de posição que não reprova a posição errada não prova nada.
  Duas consequências que o Y errado escondia e que a correção destapou: o carimbo passa a
  cair **em cima da linha "Assinatura e carimbo"** (por isso `GerarPrescricaoAsync` ganhou
  `paraAssinaturaEletronica`, como o de documentos clínicos já tinha — folha que vai ser
  selada não desenha a linha de caneta), e o **2º carimbo cairia sobre o nome do
  prescritor** (o rodapé passou a reservar DOIS espaços quando a folha pede a assinatura
  da execução; cada carimbo estreita para 240, porque 250+10+250 dá 510 contra 510,24
  úteis e o QuestPDF recusa a linha sem folga — a largura sai de `AreaDaAssinatura` e do
  rodapé pelo MESMO par de constantes).
  A lição geral: **quando um valor atravessa a fronteira para uma biblioteca de terceiro,
  o sistema de coordenadas dela é premissa a MEDIR, não a deduzir do nome do campo.**

- **O carimbo do prescritor NUNCA apareceu — e eram DOIS defeitos somados, cada um
  bastando sozinho** (parcela 68, 9ª rodada — a clínica assinou as duas vias e disse que
  "as DUAS assinaturas digitais não saíram na folha conforme tem que ser"). Reproduzido
  antes de corrigir: o PDF gerado pelo teste foi RENDERIZADO, e a folha saía com **um
  carimbo só**, o da enfermagem.
  1. **A aparência do widget era recortada.** O PDFsharp entrega ao desenhador o retângulo
     **na PÁGINA** (o `/Rect`), e o `XGraphics` desenha dentro de um form XObject cuja
     **BBox é `[0 0 largura altura]`** — coordenada LOCAL. Desenhar em `area.X`/`area.Y`
     punha o traço inteiro fora da BBox, e o form **recorta pela BBox**: o carimbo
     simplesmente não existia no arquivo. Medido no objeto: `BBox [0 0 240 46]` com o
     conteúdo em `40 -640 240 46 re`.
  2. **Anotação sem o bit Print não é IMPRESSA.** O PDFsharp não escreve o `/F` do widget e
     o padrão é 0. Medido: o mesmo arquivo achatado para impressão sai sem o bloco. Mesmo
     com a coordenada certa, o carimbo apareceria no leitor de quem assinou e sumiria na
     folha que a enfermagem leva para a sala. E **não há onde ligar o bit**: o campo só
     existe durante o salvamento, DEPOIS de a aparência ter sido desenhada (sondado — no
     momento do desenho não há `/AcroForm`, não há anotação na página e não há objeto
     `/FT /Sig` no documento).
  A saída não foi remendar nenhum dos dois: foi tirar o bloco visível da ANOTAÇÃO e pô-lo
  no **CONTEÚDO DA PÁGINA**, que não tem nenhuma das duas armadilhas — aparece no leitor,
  sai na impressora e ainda é **coberto pela assinatura**, porque entra antes dela. O campo
  de assinatura fica **invisível** (`/Rect` de área zero): deixá-lo com o mesmo retângulo
  faria o leitor desenhar o carimbo duas vezes sobre si mesmo, e texto cinza de 6,5 pt
  desenhado duas vezes sai mais grosso.
  ⚠️ Só a **2ª** assinatura continua sendo anotação, e não é escolha: a página já está
  assinada e não se toca num byte dela. Lá o `/F 4` é escrito à mão — e por isso ela era a
  única que a clínica via.
  ⚠️ **Saiu assim da parcela 42 até 20/08/2026, em TODO documento assinado** — receita,
  atestado, pedido de exame, prescrição. A assinatura criptográfica estava perfeita por
  baixo o tempo todo; o que faltava era a folha dizer quem assinou. É a irmã mais discreta
  do defeito recorrente do projeto: não é dado sem leitor nem capacidade sem porta, é
  **desenho sem tela** — e nada falha, em lugar nenhum.
  As duas lições de método, e elas são as mesmas de sempre nesta área:
  **(a) teste que prova que o arquivo FECHA não prova que a folha MOSTRA.** Havia teste de
  integridade, de recorte do PKCS#7, de posição do retângulo (parcela 68, 8ª rodada) e de
  circuito ponta a ponta — e nenhum abria o conteúdo da página para procurar o carimbo.
  `CarimboVisivelTests` faz isso, e **três dos seus quatro testes falham no código de
  antes** (o quarto é o bit de imprimir, que já estava certo): carimbo visível que não
  reprova o carimbo invisível não prova nada.
  **(b) renderizar é de graça, e é o único jeito de ver o que só a folha montada mostra.**
  `poppler-utils` desenha o PDF para tela (`pdftoppm`) e para impressão (`pdftocairo -pdf`,
  que é o que revelou o bit Print), e o `pyhanko` confere as duas assinaturas sem custo
  nenhum. Cada tentativa no SafeID é COBRADA — conferir aqui, antes, é o que evita a
  próxima rodada paga. Os testes gravam os arquivos quando `CLINICA_DUMP_PDF` aponta uma
  pasta, inclusive a **folha REAL** (`SegundaAssinaturaExecucaoTests`), que é a única que
  mostra os dois carimbos no rodapé de verdade.
  De quebra, os dois carimbos passaram a ser desenhados **linha por linha iguais** — mesma
  ordem, mesmo tamanho, mesma cor e a moldura recuada meia caneta nos dois. Eles ficam LADO
  A LADO na folha, e qualquer diferença ali se lê como se um deles fosse de outro sistema;
  o travessão do título exigiu traduzir U+2014 para `0x97`, porque a fonte do carimbo
  incremental é **WinAnsiEncoding** e o arquivo é escrito em Latin-1 — as duas tabelas só
  divergem na faixa 0x80–0x9F, onde mora a tipografia (acento não precisa de nada: "é" é
  0xE9 nas duas).

- **Carimbo visível é dado do BANCO numa caixa de largura FIXA — logo, precisa de corte**
  (parcela 68, 9ª rodada, continuação). Assim que o bloco da assinatura passou a aparecer
  de verdade, ele virou o caso clássico da parcela 50: `DrawString` com `TopLeft` num
  `XRect` do PDFsharp **não quebra linha nem corta**, e o operador `Tj` do PDF também não —
  o que não cabe sai por cima do que estiver ao lado. As quatro linhas levam nome do
  profissional, registro, CPF e o **emissor do certificado**, e nenhum deles tem tamanho
  máximo.
  **Medido antes de decidir**, com o emissor ICP-Brasil mais longo que existe ("Autoridade
  Certificadora do SERPRO Final v5") e um nome composto: sobravam **28 pontos** no carimbo
  da prescrição, **20** no da enfermagem e **8,4 na RECEITA** — o papel que vai à farmácia,
  onde a caixa tem 220 e o QR fica logo ao lado. Oito pontos são dois caracteres. Não
  estourava, e não havia guarda nenhuma.
  ⚠️ **O corte é EXATO nos dois, e a razão de não ser o mesmo mecanismo importa**: o carimbo
  do prescritor é desenhado pelo PDFsharp, então mede com `MeasureString`; o da enfermagem
  é escrito à mão em **Helvetica**, e o resolvedor de fontes do projeto só conhece a Segoe
  WP, que é **~9% mais estreita** — medir com ela deixaria estourar justamente no caso
  apertado. Por isso entrou a tabela de larguras da base-14, que foi **medida** (215,319
  pontos para a linha do emissor a 6,5) e é **conferida em teste** contra o número que o
  poppler devolve no arquivo gerado: um dígito errado ali só apareceria como texto por cima
  do carimbo vizinho, na clínica.
  A lição de método é a que esta área cobra sempre: **quando um desenho deixa de ser
  recortado e passa a ser visível, tudo o que o recorte escondia vira defeito de uma vez.**
  E a de ferramenta: `pdftotext -bbox-layout` dá o limite real de cada glifo em pontos —
  dá para medir folga de caixa sem abrir o PDF, de graça, o que nesta área vale por uma
  tentativa paga no PSC.

- **Conteúdo de página HERDA o estado gráfico de quem desenhou antes — e quem equilibra a
  pilha do QuestPDF é o PDFsharp** (parcela 68, 9ª rodada, 3ª parte — achado de uma revisão
  adversarial do próprio diff, com o CI verde). Ao pôr o carimbo no conteúdo da página em
  vez de na aparência da anotação, ele passou a depender de uma coisa que a aparência não
  dependia: os fluxos de conteúdo de uma página são **CONCATENADOS**, e o `q` que o
  `XGraphics` emite **salva** o estado, não o reinicia.
  Medido no arquivo real: o fluxo do QuestPDF abre com `q .25 0 0 -.25 0 842 cm` (a escala
  do Skia) e **não fecha esse `q`**; quem fecha é um ` Q` que o **PDFsharp** acrescenta no
  fim. Ou seja, a posição do carimbo é o resultado de uma combinação entre o que uma
  biblioteca EMITE e o que a outra CONSERTA — as duas se atualizam sozinhas.
  Se essa combinação mudar, o carimbo sai com a escala herdada: **um oitavo do tamanho, no
  meio da folha, e criptograficamente válido**, com build, testes e as três redes verdes.
  ⚠️ A rede que faltava não é sobre texto, é sobre ESTADO:
  `A_pilha_grafica_chega_equilibrada_no_carimbo` percorre os fluxos anteriores ao do
  carimbo e cobra profundidade `q`/`Q` **zero** e nenhum `cm` na raiz. Os outros testes
  verificam que o carimbo está no fluxo, nunca ONDE ele cai, e o `CarimboNoRodapeTests`
  mede o retângulo **PEDIDO**, não o desenhado — três testes sobre o mesmo assunto e um
  buraco no meio deles.
  A lição: **ao trocar um desenho isolado (form XObject) por um desenho no fluxo comum,
  o que você ganhou em visibilidade você pagou em ACOPLAMENTO** — e o preço só aparece
  quando a outra biblioteca muda de versão.
- **Resolvedor de fonte que descarta o `bold` faz o título nascer sem hierarquia** (mesma
  rodada): `FonteDoCarimbo.ResolveTypeface` devolvia sempre `SegoeWP#`, ignorando o
  parâmetro, e o `PdfSharp.WPFonts` traz `SegoeWPBold` ao lado. As quatro linhas do carimbo
  saíam com o mesmo peso. Enquanto o bloco era invisível ninguém via; agora ele é a única
  linha de cabeçalho, no papel que a clínica entrega. Conferido no arquivo: antes, um
  recurso de fonte só (`/F0` nas duas medidas); agora `/F0: Segoe WP,Bold` e `/F1: Segoe
  WP`, e há teste cobrando que título e corpo resolvam para faces **diferentes**.

- **`/Size` do trailer calculado À MÃO desencontra na primeira linha nova — e quem recusa é
  o leitor ESTRITO, não o nosso** (parcela 68, 9ª rodada, 4ª parte). Para igualar os dois
  carimbos, o incremental ganhou a face **Helvetica-Bold** do título — um objeto PDF a
  mais. O `/Size` era escrito como `nSig + 4`, certo enquanto o último objeto novo fosse a
  fonte: a tabela xref passou a declarar um objeto que o trailer dizia não existir.
  ⚠️ **Os 1675 testes ficaram verdes, o nosso `Conferir` aprovou e o arquivo abriu**, porque
  o leitor da casa é tolerante. Quem recusou o arquivo INTEIRO foi o **pyhanko**: *"Xref
  table size mismatch: table allocated object with id 47, but according to the trailer 46 is
  the maximal allowed object id"*. Em produção, quem recusaria seria o validador do
  farmacêutico.
  A correção não foi ajustar a conta: foi **derivá-la dos objetos realmente escritos**
  (`objetos.Max(Numero) + 1`), para não poder desencontrar de novo. E entrou
  `O_trailer_declara_um_tamanho_maior_que_o_maior_objeto`, que é a régua estrita dentro de
  casa — ele reprova com a conta errada.
  É a 7ª rodada da parcela 67 pela terceira vez, agora do lado da ESTRUTURA em vez do
  formato do CMS: **aceitar o próprio arquivo não é o mesmo que EMITIR o arquivo que o
  mundo lá fora lê.** E a lição operacional: **validador de fora roda de graça — passe TODO
  arquivo assinado por ele antes de dar a rodada por fechada**, porque nesta área a
  alternativa custa uma tentativa paga no PSC.

- **A folha selada pela MÉDICA não pode mostrar a execução — e a que mostra tem de ser
  selada também** (parcela 68, 10ª rodada — a clínica relatou que, com item não realizado ou
  suspenso, "está encerrando a infusão sem assinar a dela"). O relato apontava para o ciclo
  de vida e o defeito estava no PAPEL.
  **O que NÃO era**: `AguardaAssinaturaDaExecucao` sempre esteve certo. Cinco casos medidos
  (tudo realizado, um não realizado, um suspenso, nada realizado, tudo suspenso) e em todos
  a folha encerrada continua pedindo a assinatura — `NaoRealizadoESuspensoTests` fixa isso.
  Os testes anteriores só exercitavam a folha TODA realizada, e **é sempre o caminho não
  exercitado que a clínica encontra**.
  ⚠️ **O que era**: o REGISTRO DE EXECUÇÃO — a única folha que mostra o ✓, a rodela, o
  suspenso e as justificativas — é montado NA HORA e **nunca foi assinado**. Pior: depois de
  a enfermeira assinar a prescrição, o rodapé dele passava a escrever *"Assinado
  digitalmente com certificado ICP-Brasil (assinatura qualificada) · titular Joana
  Técnica"* — num PDF sem `/ByteRange`, sem PKCS#7 e sem carimbo. E, ao afirmar isso, ele
  **engolia a linha de caneta**: a folha saía sem carimbo digital e sem onde assinar à mão,
  isto é, **sem prova nenhuma de autoria**. Da cadeira dela, isso é exatamente "encerrou sem
  a minha assinatura". É a garantia aparente da parcela 3, e um comentário do próprio código
  a escondia — ele supunha que "na reimpressão nunca se chega aqui, os bytes selados são
  devolvidos guardados", e o registro **nunca foi guardado**.
  ⚠️ **A via que parecia óbvia foi MEDIDA e está fechada**: acrescentar a página do registro
  ao arquivo que a médica assinou, por revisão incremental, faz o pyhanko devolver
  `mod=OTHER · ILLEGAL_MODIFICATIONS` para a assinatura **dela**. Nosso lado aprovaria; o
  Adobe e o ITI acusariam. É a lição da 7ª rodada da parcela 67 pelo avesso, e ela custou
  trinta linhas de experimento em vez de um importador de PDF inteiro.
  **O desenho que ficou** (escolha da direção, com o custo na mesa): a enfermeira escolhe o
  certificado UMA vez e o sistema sela **DOIS documentos** — a prescrição (revisão
  incremental, o carimbo dela ao lado do da médica) e o registro (assinatura própria, um
  carimbo, mostrando tudo). São duas cobranças do SafeID por folha, e a direção aceitou.
  Três regras que o código não conta sozinho: **o registro é GERADO antes de a assinatura
  ser registrada** (depois dela, o rodapé escreveria "este arquivo NÃO é assinado", e selar
  esse texto produziria um arquivo assinado dizendo que não é assinado); **falhar ao selar o
  registro NÃO desfaz a assinatura da prescrição** (a hierarquia da parcela 65: o ato
  irreversível não depende do passo que veio depois), e a degradação é honesta — sem selo, o
  registro volta a ser espelho e DIZ que não é assinado, apontando a folha que é; e **cada
  folha devolve o SEU arquivo selado** (`ArquivoId` × `ArquivoRegistroId`), porque trocar um
  pelo outro entregaria a segunda via de um documento no lugar do outro.
  ⚠️ **E a causa do relato era ainda mais simples: a caixinha não estava marcada.** Os
  prints de produção fecham o diagnóstico — o rodapé do registro da folha 0009 traz a LINHA
  DE CANETA e o nome à direita, leiaute que só sai quando `ExigeAssinaturaEletronicaDaExecucao`
  é **falso**. A folha 0007, com dois carimbos, tinha a caixinha marcada. A clínica leu a
  coincidência ("as duas que ficaram sem assinatura tinham item não realizado") como causa,
  e ela não é — mas **o que ela via é o que vale**. Por isso o campo passou a **nascer
  marcado**: garantia que depende de alguém lembrar não é garantia. Desmarcar continua
  existindo, e continua sendo de quem prescreve; o que mudou é o lado para o qual o
  esquecimento cai.
  ⚠️ Teste que depende do PADRÃO passa a medir a escolha errada quando o padrão vira — o da
  trilha do encerramento quebrou junto, e a correção foi **declarar o regime no próprio
  teste** em vez de ajustar a asserção.
  A lição de teste: **a decisão do rodapé foi tirada de dentro do desenho do PDF**
  (`AvisoDoRegistroDeExecucao`, público). Lá dentro nenhum teste a alcançava — o QuestPDF
  embute a fonte em subconjunto e escreve o texto como IDs de glifo, então nem o texto do
  arquivo se lê de volta. **O que decide o que o papel AFIRMA precisa morar onde o
  `dotnet test` alcança.**

- **O ESCOPO da autorização em nuvem é escolhido pelo ATO, e errá-lo só aparece na SEGUNDA
  assinatura** (parcela 68, 11ª rodada — achado na revisão da própria mudança, antes de
  chegar à clínica). O encerramento passou a selar DOIS documentos com o mesmo certificado,
  e o seletor autorizava com o padrão do serviço: `EscopoSafeID.AssinaturaUnica`. O próprio
  arquivo já dizia o que isso significa — *"um hash só; **o token morre no uso**"*.
  Ou seja: a primeira selagem passaria e a **segunda seria recusada SEMPRE**, em toda folha
  da clínica, e a tela cairia no caminho degradado todo dia.
  ⚠️ **Nenhum teste pegaria**, e a razão é estrutural: o assinador de teste é local e não
  tem token que morra. Em produção, cada tentativa é COBRADA — então o defeito seria
  descoberto pagando. Foi achado LENDO os escopos, não medindo, e é o tipo de coisa que só
  se acha perguntando "o que muda quando eu chamo isto DUAS vezes?".
  A correção mora em `EscopoSafeID.ParaAto(assinaturas)` — na APPLICATION, não na ViewModel
  do WPF que escolhe o certificado, pela razão de sempre: **decisão dentro de ViewModel é
  decisão que o `dotnet test` não alcança**. E a sessão é pedida **encurtada** (5 min): o
  padrão do PSC para pessoa física vai a **sete dias**, e autorizar uma semana para selar
  duas folhas abre muito mais do que o ato pede — daí `AutorizarAsync` ter ganhado
  `duracaoSegundos`, que só existe para ENCURTAR.
  A lição que generaliza: **ao passar a chamar um serviço externo duas vezes onde antes era
  uma, leia o que a AUTORIZAÇÃO permite — não só o que a chamada faz.** Escopo, cota e
  idempotência são propriedades do primeiro uso, e nenhuma delas aparece no teste local.

- **A EVOLUÇÃO DE ENFERMAGEM, e por que ela não é a `Evolucao` com outro autor** (parcela
  71 — a cliente: *"todo paciente precisa passar pela enfermagem, por isso precisamos que
  também tenha um registro de evolução para enfermagem"*). São dois registros de naturezas
  e responsabilidades diferentes: a evolução responde *"o que eu concluí e o que decidi
  fazer"* e é de quem ATENDEU; esta responde *"o que eu observei no paciente, e a que
  horas"* e é de quem EXECUTOU, com o registro no conselho ao lado. A evolução é UMA por
  sessão, salva em várias passadas até ficar certa; esta são VÁRIAS por passagem — 14h20,
  14h50, 15h10 —, cada uma um fato pontual que não se reescreve.
  ⚠️ **Reusar a `Evolucao` com um campo de tipo foi medido e recusado**, e o argumento não
  é conceitual: QUATRO leitores quebrariam **em silêncio**. `ConsultorioService.
  EvolucaoDoHorario` cai para paciente + data, então a anotação da técnica cobriria a
  sessão do médico e ela sumiria de "sessões sem evolução" — elo partido não vira erro,
  vira LISTA VAZIA; `EvolucoesNoPeriodoAsync` filtra `ProfissionalId == null`, e a anotação
  apareceria no "Meu dia" de TODOS; `CompletudeProntuario` passaria a medir o trabalho de
  outra pessoa; e o relatório que o paciente leva ao CONVÊNIO imprime o autor por sessão.
  É o mesmo argumento com que a parcela 42 recusou enfiar a folha de infusão em
  `TipoDocumentoClinico`.
  **O paciente é o DONO; a infusão é procedência.** `PrescricaoInternaId` é anulável, e não
  por conveniência: FK obrigatória força `Cascade`, e apagar uma folha levaria junto
  registro clínico — a cascata que a parcela 60 achou no excluir paciente. Sem isso ficariam
  sem lugar o curativo, a sala de observação, a triagem e **a reação que aparece meia hora
  depois de a folha ter sido encerrada**, que é justamente a que mais importa.
  **Os SINAIS VITAIS moram na evolução, e não em `MedidaClinica`**: aquela não tem HORA (só
  `DateOnly`), então três aferições na mesma infusão ficariam indistinguíveis — e é a
  sequência dentro da sessão que faz a leitura de enfermagem ("PA 120x80 na admissão, 90x60
  aos vinte minutos"). Faltam-lhe também temperatura, FC, FR e SpO₂. A divisão fica escrita
  na tela: **aqui é o ponto no tempo; a CURVA continua sendo a tela de Medidas.** E a dor
  aferida **não entra na curva de dor do prontuário** (`Evolucao.EvaAntes/Depois`): aquela
  mede o efeito do TRATAMENTO entre sessões e a direção já a lê — misturá-las mudaria em
  silêncio um número existente.
  **A porta é a LINHA DA FILA da sala, não uma aba dentro da folha**, e a primeira razão
  sozinha decide: `PodeMexer => PodeChecar && EmExecucao` — **folha encerrada apaga a janela
  dela inteira**, e o botão nasceria morto exatamente no caso que justifica a feature. Some
  a isso que a folha é MODAL (registrar uma reação na cadeira 3 com a folha da cadeira 1
  aberta custaria dez gestos, com o paciente reagindo) e que ela já tem um campo de hora com
  OUTRO significado, num registro cuja razão de existir é a hora ser a certa.
  **Bit próprio** (`RegistrarEvolucaoEnfermagem`), e a alternativa de custo zero foi
  recusada de propósito: reusar `ChecarPrescricao` não custaria uma linha, mas **checar é
  afirmar que aquilo entrou no paciente; evoluir é descrever o que se observou nele** — dois
  atos de peso diferente, e o corte pelo ATO é a regra desde a parcela 45.
  **No prontuário, lista SEPARADA da médica — e não é estética, é bug evitado:**
  `LinhaEvolucao.EvolucaoId` é a chave das ações destrutivas da lista de sessões, e ids são
  POR TABELA. A evolução de enfermagem nº 42 na mesma lista cancelaria a `Evolucao` nº 42 —
  de outro paciente, com o motivo escrito para outra coisa. Não estoura, não avisa.
  ⚠️ **E a aba "Prontuário" da ficha não tinha barreira NENHUMA.** O item Pacientes pede só
  `VerFichaPaciente`, o `TabItem` não tinha `Visibility` e `CarregarProntuarioAsync` não
  tinha um único `Pode` — **Recepção, Financeiro e Faturista liam a evolução inteira de
  qualquer paciente**, que é literalmente o que o corte da parcela 49 existe para impedir. A
  contraprova estava 270 linhas abaixo no MESMO arquivo: `CarregarTermosAsync` tem a
  barreira e o comentário nomeando esses perfis. Achado ao construir esta parcela, e
  corrigido antes dela — **entregar dado clínico novo por uma porta aberta pioraria a
  conformidade que a feature existe para melhorar.**
  ⚠️ **E as duas primeiras portas não bastavam — foi preciso a TELA.** A evolução nasceu
  alcançável pela fila da sala de infusão e pela ficha do paciente, e as duas resolvem o
  caso da INFUSÃO. A clínica devolveu a frase que muda o desenho: *"todo paciente precisa
  passar pela enfermagem"*. A maioria dessas passagens não tem folha nenhuma — curativo,
  triagem, observação, pós-consulta —, e a enfermeira não tinha de onde alcançá-las: a sala
  só mostra as folhas do DIA, e a ficha exige saber o nome e passar pelo módulo da recepção.
  A tela `Enfermagem` é a **terceira tela do shell publicada por dois módulos** (a sala e os
  pacotes são as outras), e é SEPARADA da sala de propósito: a sala responde *"o que
  executar agora"*, esta responde *"quem eu atendi e o que escrevi"*. Terceira pergunta,
  terceira tela. A lista traz **todos os pacientes cadastrados** (`limite: null`, como a
  listagem do balcão) e a evolução mora atrás de UM clique — lista de largura inteira → tela
  do paciente, nunca a faixa lateral que o README proíbe.
  A lição: **porta no fluxo não é porta na rotina.** Pendurar o registro nas telas do
  processo que o motivou (a infusão) cobre quem está DENTRO daquele processo; quem faz o
  mesmo trabalho fora dele fica sem lugar — e é o defeito recorrente do projeto na variante
  "a porta existe, mas só para metade dos casos".
  De quebra: `SessaoUsuario.RegistroConselho` nasceu porque a folha gravava `Conselho: null`
  **literal** desde a parcela 42 — a coluna existia, o PDF tinha o ramo que a imprime e a
  exportação tinha a coluna, e nenhuma checagem de produção saía identificada. **Registro de
  enfermagem sem o número do conselho não é registro de enfermagem.**

- **X · Y · XY — a LEITURA que os dois compartilham, a ESCRITA que os separa** (parcela 72;
  o mapa completo está em `docs/atendimento-medico-e-enfermagem.md`, e é lá que se
  atualiza). A cliente pediu: *"o médico enxerga X itens de atendimento, a enfermagem
  enxerga Y, e caso X e Y se completem entreguem XY para ambos"*. A medição do domínio
  respondeu antes do desenho: `PerfilAcesso.Profissional` e `PerfilAcesso.Enfermagem` **já
  compartilhavam** `VerAgenda | VerFichaPaciente | VerProntuario | ColherAssinaturaPaciente`.
  ⚠️ **Nenhuma permissão nova era necessária para entregar o XY — faltava PORTA**, o defeito
  recorrente do projeto pela décima segunda vez. E havia um argumento duro contra inventar
  bit: `Permissao` é `[Flags]` de **`int`** gravada como INTEIRO em produção, e
  `RegistrarEvolucaoEnfermagem = 1 << 30` — **sobra UM bit** antes de o enum precisar virar
  `long`, o que muda o tipo da coluna numa base viva.
  **A frase que decide tudo: XY é a LEITURA; X e Y são as ESCRITAS.** O que separa é
  `EditarProntuario | Prescrever` (X) contra `ChecarPrescricao | RegistrarEvolucaoEnfermagem`
  (Y), e cada lado escreve com o conselho dele — médico escrevendo evolução de enfermagem é
  registro assinado com o conselho errado, pior que a lacuna que resolveria.
  **A linha do tempo clínica** (`LinhaDoTempoClinicaView`, no shell) é o componente das TRÊS
  portas: a ficha da Recepção, o Consultório e a tela da Enfermagem. Chips de seção
  **contados**, um marcado por vez — e os desmarcados MOSTRAM a contagem, porque é o número
  visível que faz a enfermeira descobrir que há 12 sessões médicas para ler.
  ⚠️ **Ela NÃO funde as listas, e as duas razões decidem sozinhas:** (a) os ids são POR
  TABELA — a evolução de enfermagem nº 42 e a `Evolucao` nº 42 são de pacientes diferentes,
  e um comando destrutivo sobre lista fundida cancelaria o registro errado **sem estourar e
  sem avisar** (o bug que `PacientesView.xaml` documentava desde a 71); (b) `Evolucao.Data`
  é `DateOnly` — ordenar a médica às 00:00 a poria antes de todas as aferições do dia,
  inclusive da reação que a motivou, e **ordenar um prontuário por uma hora que não existe é
  fabricar sequência de eventos num documento que responde em auditoria**. O item carrega
  `Natureza` + `Id`, e é isso que permitirá fundir quando `Evolucao` ganhar hora.
  ⚠️ **A lista rica de sessões do Consultório NÃO foi substituída** pela genérica: ela tem
  busca no texto, contagem de anexos, marca de correção e os botões da linha. Trocá-la
  tiraria capacidade de quem a usa todo dia — a regra 3 do bloco do faturamento aplicada a
  uma tela clínica. O componente entra ali só para o que faltava.
  **Antes de acrescentar informação ao Atendimento foi preciso corrigir o leiaute**: a tela
  exigia 520 + 16 + 400 = 936 px e a janela mínima da suíte deixa 856 (606 com o painel de
  categoria fixado). O WPF honra o `MinWidth` e **corta o excesso à direita** — não há
  rolagem horizontal ali, então acrescentar era cortar o que já existe.

- **Entidade clínica nova entra em UMA lista, e quatro leitores a consomem**
  (`CatalogoRegistroClinico`, parcela 72). O defeito já tinha sido cometido DUAS vezes neste
  exato assunto, e os comentários confessam: a folha de infusão ficou de fora da primeira
  versão da GUARDA e a lista de problemas — onde moram as ALERGIAS — ficou de fora da
  primeira versão da EXPORTAÇÃO. Uma terceira estava de pé: `SituacaoGuarda` contava CINCO
  naturezas enquanto o prazo de 20 anos era calculado sobre SETE, e o documento do art. 18,
  II cobria TRÊS de nove. A tela que responde ao auditor *"o que vocês guardam por 20 anos"*
  mostrava "0 sessão · 0 medida · 0 prescrição" com uma data vinda de lugar nenhum visível,
  para a ficha cujo único registro era enfermagem — **número errado com cara de exato, na
  tela de conformidade**.
  As nove naturezas moram no DOMÍNIO com rótulo e permissão de leitura; leem a lista a linha
  do tempo, a guarda (contagem e frase), a exportação do fornecedor e o direito do titular —
  que ganhou as SEIS seções que faltavam, execução de infusão item a item inclusive.
  ⚠️ **`ConjuntoClinicoTests` é COMPORTAMENTAL, não declarativo**: ele grava um registro de
  cada natureza e exige que ele APAREÇA na contagem, no CSV e no texto. Declaração conferida
  contra declaração ficaria verde com as duas erradas do mesmo jeito. Ele falha no commit em
  que a próxima entidade clínica nascer, que é meses antes de a clínica esbarrar.

- **A alergia é conferida na ADMINISTRAÇÃO, não só na assinatura** (parcela 72 — a quinta
  recusa do projeto, e a única que impede dano ao PACIENTE). `AssinarAsync` confere a folha
  contra as alergias e recusa sem confirmação escrita desde a parcela 42; a EXECUÇÃO não
  conferia **nada** — as únicas guardas de `ChecarAsync` eram "item já checado" e hora
  futura. O caminho de dano é inteiro e mora dentro do que a parcela 71 construiu: folha
  assinada de manhã sem alergia registrada → o item 2 causa reação → **a própria técnica**
  grava a alergia pelo campo "Reação a registrar como alergia" → itens 3, 4 e 5 seguem
  pendentes com a folha na sala → **ninguém reconfere**. O sistema tinha o dado, gravado por
  quem seria a vítima do silêncio, e não o usava.
  Três decisões: **só na administração** (não administrar é o desfecho seguro, e cobrar
  confirmação para a rodela treinaria a equipe a confirmar sem ler); **confere o ITEM, não a
  folha** (repetir a resposta da folha inteira acenderia na linha do soro por causa da
  dipirona da linha de baixo); e **avisa e exige confirmação — não impede**. Vale igual na
  RETIFICAÇÃO: retificar de "não realizado" para "realizado" é administrar, e deixar a
  conferência só na checagem normal seria a cópia que fica para trás.
  Junto veio o **COREN obrigatório** em checar, registrar e retificar. O comentário da
  entidade já dizia que "evolução sem registro no conselho não é evolução de enfermagem" e
  **não havia guarda em lugar nenhum**: bastava um login sem `Profissional` vinculado para
  todo registro daquela máquina entrar no prontuário sem COREN **para sempre** — e sem
  conserto barato, porque o campo é COPIADO no ato. A frase nomeia o conserto, pelo
  precedente do "Meu dia".

- **A curva de PRESSÃO vem de DUAS fontes, e cada ponto diz de onde veio** (parcela 72): a
  PA está no `CatalogoMedidas` com faixas publicadas desde a parcela 37 e a série **nascia
  vazia e continuava vazia** — a única porta de ESCRITA de `MedidaClinica` está no app do
  MÉDICO, enquanto a pressão de verdade é aferida na ENFERMAGEM, toda sessão, e vai para
  `EvolucaoEnfermagem`. **Curva vazia se lê como "este paciente nunca teve a pressão
  aferida"**, que é falso.
  A ponte é de **LEITURA**, e isso é decisão: destravar a colheita de `MedidaClinica` pela
  enfermagem daria DOIS lugares para gravar a mesma aferição, sem nada na tela dizendo qual.
  As regras: **procedência escrita em cada ponto** (dois pontos do mesmo dia sem dizer que um
  é de antes da consulta e o outro de meia hora depois da bomba escondem justamente a leitura
  clínica), **faixa da MESMA definição do catálogo** (a evolução grava os números crus, e uma
  segunda leitura de "pressão normal" daria duas respostas para a mesma aferição), **cancelada
  e retificada não entram** e **dentro do dia a HORA desempata**.

- **Comentário que promete o que o código não faz, terceira ocorrência** (parcela 72): a
  entidade `EvolucaoEnfermagem` afirmava que a marca de intercorrência *"viaja para a tela de
  atendimento do médico"* — e `grep EvolucaoEnfermagemService src/Clinica.Modulo.Clinico`
  devolvia **zero**. Aqui o estrago não era erro, era **AUSÊNCIA**, indistinguível de "não
  houve intercorrência". Agora ela entra na lista de `AlertasClinicos` que já existe (zero
  pixel novo), com **janela de 48 horas** — e a janela decide a utilidade da lista: alergia é
  ESTADO, intercorrência é EVENTO DATADO, e a marca é `bool`, então **não há como
  descartá-la**. Sem janela, seis meses depois o paciente crônico teria uma náusea de março e
  um extravasamento de abril acima da alergia real, e é assim que se ensina alguém a fechar o
  alerta sem ler. A janela é `const` no domínio, ao lado da regra.
  O mesmo commit corrigiu a promessa das *"quatro portas — … e o do Consultório"* da janela de
  escrita (a do Consultório não existe, e é decisão) e a frase divergente do cabeçalho do
  workspace, que dizia "da agenda de HOJE" para qualquer horário enquanto a tela de dentro já
  dizia a DATA desde a parcela 69 — a lição das parcelas 64 e 68 pela sétima vez.

- **Propriedade `init` NÃO existe ainda dentro do construtor** (parcela 72, revisão do
  próprio diff — o defeito mais caro da rodada, e o mais silencioso). O componente da linha
  do tempo fazia `Secao = SecaoInicial` no corpo do construtor, e `SecaoInicial` é `init`:
  o *object initializer* roda **depois** do construtor, então a leitura devolve sempre o
  DEFAULT. A tela da **Enfermagem** — cuja razão de existir é a evolução de enfermagem —
  abria marcada em "Médica", listando as sessões do médico; e o comentário do XAML afirmava
  por escrito que ela "abre no chip Enfermagem".
  ⚠️ O que fez isso passar foi um **socorro acidental**: a rotina que monta os chips troca a
  seção quando a atual não está visível, e as duas portas que restringem a lista de seções
  funcionavam por tabela. Só a porta que mostra TUDO ficava errada — a que mais importava.
  **Valor que depende de `init` resolve-se na primeira carga, nunca no construtor.**

- **`TextoParaVisibilidade` sobre qualquer coisa que não seja `string` é `Collapsed` para
  sempre** (parcela 72): o conversor faz `value as string`, e um `TimeOnly?` encaixotado
  devolve `null`. A HORA da evolução de enfermagem — o dado que a parcela 71 existe para
  registrar, e cuja leitura clínica é a sequência dentro da sessão (14h20 · 14h50 · 15h10) —
  **nunca apareceu**, nas três portas. É o defeito do `BooleanToVisibilityConverter` sobre
  string (parcela 61) pelo AVESSO, e nada falha: XAML bem-formado, binding válido, nenhuma
  exceção. **Amarre `Text` e `Visibility` na MESMA propriedade, e que ela seja string** —
  foi por isso que `RegistroClinicoPaciente.HoraTexto` nasceu.

- **Ao substituir uma lista por um componente genérico, liste o que a antiga MOSTRAVA**
  (parcela 72). A troca perdeu quatro coisas sem que nada quebrasse: o **selo
  INTERCORRÊNCIA** (que virou texto suave dentro do detalhe, com o mesmo peso do resumo da
  queixa — e aceso também no registro CANCELADO, que é registro desdito), o **realce do não
  vigente**, o **`RegistradoEm`** e — o pior — o **autor da sessão**, que passou de
  `Profissional?.Rotulo` (quem ATENDEU) para `CriadoPor` (quem DIGITOU, nulo em toda sessão
  anterior ao dia em que o sistema passou a gravá-lo). **A linha continuou existindo e
  passou a responder outra pergunta**, que é o jeito mais discreto de perder informação.

- **Portão de acesso por NATUREZA tem de ser o PISO quando a natureza tem regra por ITEM**
  (parcela 72): o catálogo declarava `VerProntuario` para "documento clínico", e o montador
  aplicava esse portão ANTES do filtro por folha. Isso engolia as duas folhas que a parcela
  59 decidiu não serem dado de saúde — declaração de comparecimento e termo de consentimento
  LGPD, ambas `VerFichaPaciente`, que saem do balcão o dia inteiro. **Onde a natureza tem
  regra por item, quem decide é o item; o portão de fora é só "pode abrir este paciente".**

- **Comando novo que faz DADO DE SAÚDE SAIR nasce com as duas barreiras** (parcela 72): o
  "Enviar documento pelo WhatsApp" foi portado para a ficha **sem um único `Exigir`**, e a
  metade visível era estado puro (`Assinado && !Cancelado`). A enfermeira, que tem
  `VerProntuario` e não tem `Prescrever`, via a receita na lista e o botão ACESO. E o
  "Assinar" ao lado tinha as duas barreiras **discordando sobre que ato é aquele**: o botão
  acendia pelo bit do TIPO do papel e o comando exigia `Prescrever` fixo — o corredor sem
  saída da parcela 69 em quatro dos oito tipos, com a fuga oposta de brinde (quem tem
  `Prescrever` e não tem o bit do tipo passava direto). **Confira o bit do TIPO que está na
  linha, não o da porta por onde se entrou** — e procure a mesma ação nos DOIS lados: a
  cópia da Recepção era a atrasada, e foi dela que o defeito veio.

- **Recusa nova de serviço precisa da metade VISÍVEL, senão ela chega depois do trabalho
  feito** (parcela 72): o COREN passou a ser obrigatório para checar — regra certa, porque o
  número é COPIADO no ato e corrigir depois exigiria retificar registro a registro. Mas os
  botões continuavam acesos: a técnica sem ficha de profissional vinculada administrava o
  soro, vinha marcar e levava um erro que **ela não pode consertar sozinha** (o conserto é em
  Equipe e em Acessos, bits que o perfil dela não tem). Agora a folha avisa na ABERTURA e o
  botão nasce apagado. **Toda recusa de serviço tem um botão que precisa saber dela.**

- **Resultado vazio de workflow é para ser investigado, nunca lido como aprovação** (parcela
  72 — a lição da parcela 66 cobrada de novo, agora na minha própria ferramenta). A
  auditoria adversarial devolveu `sobreviventes: []` porque **26 dos 28 agentes falharam no
  limite semanal** — os achados foram para a lista de "derrubados" com a razão `"sem voto"`.
  A conta era minha: `sobreviveu = votos.Length > 0 && refutados == 0` põe o achado
  SEM VOTO no balde dos refutados. **Cético que não votou não refutou nada** — e o script
  que trata as duas coisas igual transforma falha de infraestrutura em aprovação silenciosa.

- **"Está muito CRU!" — a parcela 72 entregou QUEM VÊ e não encostou no ATO DE ATENDER**
  (parcela 73; o mapa completo está em `docs/registro-do-atendimento.md`, e é lá que se
  atualiza). Foi a oitava reprovação do cliente, e a mais precisa: a parcela anterior deu
  portas, linha do tempo e permissões, e os dois registros clínicos continuavam sendo **um
  campo de texto**. O médico não tinha onde escrever história da doença atual, exame físico
  nem hipótese diagnóstica — os três eixos sem os quais o prontuário registra o que foi feito
  e não registra **por quê**.
  ⚠️ **E a metade da enfermagem não era pobreza, era ILEGALIDADE.** A **COFEN 358/2009** torna
  o Processo de Enfermagem obrigatório e **registrado formalmente** em cinco etapas
  (histórico, diagnóstico, planejamento, prescrição de enfermagem, avaliação), e a **Lei
  7.498/1986, art. 11, I, "i"** faz da consulta de enfermagem ato **privativo do Enfermeiro**.
  Oferecer uma caixa de texto à enfermagem não é oferecer pouco: é **impedir que ela cumpra a
  Resolução do próprio conselho**.
  **ANOTAÇÃO × CONSULTA é derivado do CONTEÚDO, nunca um campo de tipo** (`EhConsulta`):
  campo de tipo teria de ser preenchido, e seria preenchido errado — a técnica marcaria
  "consulta" por engano e a folha sairia cobrando cinco etapas de uma observação de sinais
  vitais. Derivado, ele não mente: quem escreveu diagnóstico fez consulta, quem escreveu uma
  linha fez anotação. E a **consulta incompleta AVISA e não impede** (`EtapasEmFalta`): a
  etapa 5 só existe depois de o cuidado ter sido prestado, e recusar salvar antes dela faria
  a enfermeira digitar tudo de novo no fim do turno — **registro clínico que não se consegue
  salvar é registro que não acontece**. A recusa seria a garantia aparente pelo avesso; o que
  se recusa é imprimir a consulta pela metade **sem dizer que está pela metade**, e por isso
  o aviso sai na tela E no papel.
  **O catálogo é a lista DESTA clínica, e NANDA-I não foi importada** — é licenciada. O que
  se reusa é a redação em três partes (problema, *relacionado a*, *evidenciado por*), que é
  método e não texto protegido; sem a terceira parte ninguém consegue avaliar depois se o
  diagnóstico foi resolvido, e a etapa 5 vira opinião. O `Codigo` é **anulável de propósito**:
  lista fechada faria a enfermeira parar de diagnosticar o que não está nela, e diagnóstico
  não escrito é cuidado não prestado. Aplicar **COPIA** — aqui não é desenho, é a Lei
  13.787/2018.
  ⚠️ **O item que quase ficou de fora é o PAPEL.** A fiscalização do COREN se faz no
  PRONTUÁRIO, que numa clínica é a via impressa e arquivada: **consulta de enfermagem que só
  existe na tela é consulta que a fiscalização não enxerga** — o defeito recorrente do
  projeto na variante que custa multa. As cinco etapas entraram em
  `PrescricaoInternaPdfService`, com o que falta escrito ao pé.
  **Só o registro que TEM processo ganha o bloco**: carimbar as cinco etapas em toda
  observação encheria a folha de rótulos vazios, e rótulo vazio é o que faz alguém parar de
  ler a folha inteira. Pela mesma razão o detalhe do médico abre num `Expander` FECHADO: a
  sessão de seguimento — a maioria absoluta do movimento — se registra em queixa, EVA e
  conduta, e quatro campos vazios em toda sessão viram paisagem antes do dia em que a
  anamnese importa.
  ⚠️ **O CID da hipótese NÃO sai no relatório de evolução** — a economia do CID da parcela 3,
  com mais razão: o relatório circula fora da clínica, o código é o que se lê num campo de
  formulário sem ninguém ler a frase ao lado, e este documento não passa pela autorização
  expressa que a receita e o atestado pedem. E a **hipótese não é a lista de problemas**:
  `ProblemaPaciente` é o que o paciente TEM, `HipoteseDiagnostica` é o que se pensou NAQUELA
  sessão — a de terça pode estar errada, e a lista não deve carregá-la.

- **"Estamos anos luz atrás" — o atendimento não era um ESTADO, era um formulário**
  (parcela 74; o mapa completo está em `docs/registro-do-atendimento.md`, §8 em diante).
  A cliente mandou o print do prontuário do iClinic. Medidas as duas telas lado a lado, os
  dois buracos que importam não eram cosméticos, e o primeiro é conceitual: **num prontuário
  eletrônico o profissional ENTRA no atendimento, o relógio corre e ele FINALIZA.** O nosso
  era um formulário com "Salvar sessão" no rodapé.
  ⚠️ **O carimbo do INÍCIO existe desde a parcela 38** (`Agendamento.InicioAtendimentoEm`,
  para o kanban do balcão) **e nenhuma tela do Consultório o lia.** O defeito recorrente do
  projeto na variante mais discreta de todas — nada falha, só que o médico não sabe que está
  há 40 minutos com o mesmo paciente e o balcão não sabe que ele terminou.
  **Finalizar NÃO conclui, e é a decisão da parcela 61**: concluir são QUATRO fatos do mesmo
  ato (a guia nasce, o pacote debita, o insumo sai, o dinheiro entra) e três são do balcão.
  Marcar `Realizado` daqui pularia os três **em silêncio**: o dia fecharia com o caixa sem a
  sessão, e nada falharia. Por isso `EncerrarAtendimentoAsync` carimba `FimAtendimentoEm` e
  deixa o `Status` em `Agendado`, e há teste que falha se alguém "simplificar" isso.
  ⚠️ **A ORDEM entre gravar e carimbar é a hierarquia da parcela 65, não estilo**: grava a
  sessão PRIMEIRO. Falhando a gravação, o carimbo não acontece — mandar o recado de que o
  médico terminou enquanto a evolução não existe em lugar nenhum é falha exibida como
  sucesso. O inverso também vale: gravada a sessão, falhar o carimbo vira AVISO e nunca
  desfaz o prontuário. É o que fez `SalvarAsync` virar `TentarSalvarAsync` devolvendo `bool`.
  **E o recado CHEGA**: o cartão da fila do balcão ganha o selo "Encerrado às 14h32" e sobe
  para a frente da raia. É o par do `ChamadoEm`, que atravessa no sentido contrário — até
  aqui a recepcionista descobria que o médico tinha terminado quando o paciente aparecia na
  frente dela, e o cartão ficava em "Em atendimento" meia hora depois de a sala estar vazia,
  o que faz o quadro do dia mentir sobre quem está ocupado. ⚠️ **É SELO e não raia nova**:
  uma coluna permanente para um estado que dura minutos é a faixa vazia comendo a tela que o
  README condena desde a parcela 38 — o que a recepcionista precisa saber não é que existe
  uma coluna nova, é QUAL cartão está pronto para fechar.
  **Encerrar com a sessão em branco PERGUNTA, não impede** (registro que não se consegue
  salvar é registro que não acontece), e **encerrar de novo não reescreve a hora** (`??=`,
  a razão do "chamar de novo": quem clica duas vezes precisa continuar vendo a hora em que
  terminou, e o segundo clique esconderia justamente o atendimento demorado).
- **Três seções do prontuário estavam FORA do paciente** (parcela 74): prescrever exigia
  **sair do paciente**, ir a um item de menu, escolher a pessoa de novo e voltar. É a porta
  no lugar errado — o defeito que o projeto já corrigiu doze vezes ENTRE módulos — cometido
  agora dentro de um app só, e era o que mais separava a tela de um prontuário eletrônico de
  verdade. Viraram sete seções num **RAIL VERTICAL**, e a escolha não é gosto: o `TabPanel`
  do WPF **espreme** as abas (o defeito da parcela 50 — "Convê", "Prontu", "Documer") e sete
  rótulos quebrariam a régua em duas linhas mesmo com `WrapPanel`. `AbaAtual` continua sendo
  o contrato — as chaves de navegação de outros módulos caem cada uma na sua seção
  (`ModuloClinico.AbaDe`), e trocar leiaute não pode quebrar navegação (a regressão da
  parcela 37, 4ª rodada). A tela que vira seção ganha `MostrarCabecalho = false`: dentro do
  workspace o nome já está no crachá, e o **seletor de busca dela trocaria o
  `PacienteEmFoco` por baixo das outras seis seções**, que continuariam mostrando o paciente
  anterior.
  **"Exames e anexos" muda o EIXO da leitura, e é a lição que generaliza**: os anexos só se
  alcançavam sessão a sessão, o que responde *"o que tem nesta consulta"*. A pergunta de
  quem atende é outra — *"eu pedi a ressonância; ela chegou?"* — e ela não se responde
  abrindo quarenta sessões uma por uma. **Dado com leitor pode estar com a CHAVE errada**, e
  isso não aparece em teste nenhum.
- **A alergia é ATRIBUTO DA PESSOA, não alerta do dia** (`CabecalhoClinicoPaciente`, parcela
  74). O cabeçalho do consultório mostrava iniciais, nome e uma linha de contexto; idade,
  convênio, desde quando a pessoa se trata ali e as ALERGIAS estavam no banco e não no olho
  de quem atende. Alerta é faixa que se lê uma vez e se ignora nas quarenta sessões
  seguintes — é a razão pela qual este projeto recusa alerta que dispara para todo mundo
  desde a parcela 26. No crachá ela fica ao lado do nome enquanto o prontuário estiver
  aberto, e **entra mesmo dada por RESOLVIDA** (a regra da parcela 37: "resolvida" numa
  alergia é quase sempre "não reagiu da última vez"); só o DESCARTE, que exige motivo
  escrito, a cala.
  O crachá é também o **primeiro leitor da `HipoteseDiagnostica`** que a parcela 73 criou —
  sem ele seria mais um campo gravado sem leitor, o defeito recorrente cometido na parcela
  seguinte à que criou o dado. As últimas hipóteses saem **distintas e no máximo três**:
  repetir "lombalgia" nas oito últimas sessões gastaria a linha inteira dizendo uma coisa só.
  A **linha de identificação é montada no MODELO**, não no XAML, porque ela precisa PULAR o
  que não existe: paciente sem data de nascimento não pode produzir "· anos ·" com um vão no
  meio, e cadastro novo não tem "desde". Frase feita de bindings concatenados não sabe pular.
  E o **total de sessões conta só o que ACONTECEU** (`RealizadoEm`): desde a parcela 70 a
  guia nasce no agendamento, então contar linhas de `Atendimento` somaria as sessões da
  semana que vem — o crachá diria "24 sessões" a quem teve 18.
- **`BooleanToVisibilityConverter` sobre um `BitmapImage` é `Collapsed` para sempre**
  (parcela 74 — defeito meu, pego na revisão do próprio diff antes de sair). É a lição da
  parcela 61 na terceira variante: o conversor do WPF devolve `Collapsed` para qualquer
  valor que não seja `bool`, e a foto do paciente é um objeto. **A foto nunca apareceria**,
  com XAML bem-formado, binding válido, nada lançando e as três redes verdes. Entrou
  `ObjetoParaVisibilidade` no shell, e ele **não substitui** o `TextoParaVisibilidade`:
  string vazia não é nula, e usá-lo numa mensagem deixaria a caixa de alerta aberta e vazia
  depois de a mensagem ser limpa. **A pergunta que decide é "o que significa 'não tem'?"** —
  para texto é o branco, para objeto é o nulo.
- **`--` é ilegal DENTRO de comentário XML** (parcela 74): a linha de sublinhado de um
  cabeçalho de comentário (`----------------`) quebra o XAML inteiro com "not well-formed
  (invalid token)". Nos `.cs` ela é livre; no XAML, não. O `verificar-suite` pega na hora —
  mas vale saber antes de escrever, porque o estilo de comentário deste projeto usa
  sublinhado em toda seção.

- **Método de repositório SEM chamador em teste é código que ninguém executou** (parcela 74,
  2ª rodada — auditoria do próprio diff, e os dois primeiros achados derrubariam a tela em
  produção com 1778 testes, três redes locais e o CI verdes).
  ⚠️ **Consulta LINQ só se prova EXECUTANDO**, porque a tradução para SQL acontece em
  runtime. `AnexosDoPacienteAsync` filtrava por `!a.Evolucao.Cancelada`, e `Cancelada` é
  propriedade DERIVADA (`=> CanceladaEm is not null`), não mapeada: o EF recusa com
  *"Translation of member 'Cancelada' on entity type 'Evolucao' failed"*. A seção "Exames e
  anexos" derrubaria a tela no primeiro clique. A regra: **em `Where`/`OrderBy`/`Select`
  traduzido, use a COLUNA (`CanceladaEm == null`), nunca a derivada** — e todo método novo
  de repositório nasce com um teste que o EXECUTA, mesmo que trivial.
- **`Task.WhenAll` sobre o MESMO `IClinicaRepositorio` é um defeito, não uma otimização**
  (parcela 74, 2ª rodada). As leituras passam pelo mesmo `DbContext`, que **não aceita duas
  operações ao mesmo tempo**: `CabecalhoAsync` estouraria com *"a second operation was
  started on this context instance"* em toda troca de paciente.
  ⚠️ **Os treze testes do crachá passavam**, porque o SQLite em memória completa a consulta
  quase sincronamente e as quatro nunca chegavam a se sobrepor. É a mesma família do `xmin` e
  das datas com fuso — **o que só aparece com latência de rede real**. A rede que fechou o
  buraco é barata e generaliza: um `DbCommandInterceptor` que injeta `Task.Delay` no
  `ReaderExecutingAsync` (ver `CabecalhoClinicoTests.Cracha_monta_contra_um_banco_LENTO`).
  **Toda leitura composta nova deveria passar por ele.**
  ⚠️ E o agravante é de método: eu escrevi um comentário JUSTIFICANDO o paralelismo por
  performance. **Comentário que explica uma decisão errada a torna invisível para o próximo
  revisor** — foi por isso que ele sobreviveu à minha primeira leitura do diff.
- **Carimbo novo da fila entra no bloco que a REMARCAÇÃO limpa** (parcela 74, 2ª rodada). A
  parcela 69 documentou que "remarcar levava junto os carimbos da fila" e a 74 criou um
  carimbo novo (`FimAtendimentoEm`) **sem o pôr no bloco**. Um horário remarcado para outro
  dia nasceria com o selo verde "Encerrado às 09h40" de uma sessão que não aconteceu — e
  cartão em "Aguardando" dizendo que já terminou é lido pelo balcão como pronto para fechar.
  **Estado derivado de carimbo de hora tem de ser revisto em TODA escrita que muda o que o
  carimbo significa** — e "mudou de dia" continua sendo a maior delas.
- **`DispatcherTimer` ligado no ViewModel mantém viva cada tela já trocada** (parcela 74, 2ª
  rodada). Quem liga e desliga é a VIEW, no `Loaded`/`Unloaded` — o `MeuDiaView` faz assim
  desde a parcela 38 **com o motivo escrito no comentário**, e a parcela 74 o repetiu errado.
  Aqui o estrago era maior: cada workspace abandonado segurava a ViewModel dele **e as SETE
  sub-ViewModels**, batendo a cada 15 s; num turno de vinte pacientes, vinte deles.
  ⚠️ E não basta mover o `Start()`: a função que recalcula o estado religa o timer sozinha, e
  por isso ela precisa de uma guarda de "a tela está montada" (`_naTela`).
- **Antes de aceitar uma leitura, meça o que ela TRAZ** (parcela 74, 2ª rodada — a lição da
  parcela 69 pela segunda vez). O crachá lia `EvolucoesDoPacienteAsync` — o prontuário
  INTEIRO, com o texto de cada evolução, a conduta e as orientações — para extrair **três
  frases curtas**. Num tratamento de quarenta sessões é meio megabyte a cada troca de
  paciente, que no consultório é o gesto mais repetido do dia. Virou `HipotesesRecentesAsync`,
  uma projeção de uma coluna com teto no SQL.

- **Campo novo de prontuário entra em TRÊS lugares no mesmo commit — e nenhum quebra o
  build** (parcela 74, 2ª rodada; o defeito é da 73 e passou por tudo). Os quatro campos da
  sessão médica (`HistoriaDoencaAtual`, `ExameFisico`, `HipoteseDiagnostica`, `CidSessao`)
  entraram na entidade, na tela, no PDF, na exportação e no art. 18 II — e **não** entraram
  em `ProntuarioService.SalvarAsync`, que copia campo a campo para a entidade rastreada.
  ⚠️ **O efeito era o pior possível porque a CRIAÇÃO funcionava**: o objeto é novo, tudo era
  gravado, a tela mostrava certo. Só a primeira EDIÇÃO — acrescentar uma linha à evolução —
  apagava a anamnese, o exame físico, a hipótese e o CID. Sem erro, sem aviso. E
  `GuardarVersao` também não os copiava, então **a versão anterior não os tinha**: o dado
  sumia para sempre, contra o ponto 2 do compromisso de conformidade e o art. 3º da Lei
  13.787/2018 (rastreabilidade da retificação).
  Os três lugares são: **a cópia** do serviço, **o `GuardarVersao`** (com a coluna na
  `VersaoEvolucao` e a migration) e **a validação de "evolução vazia"** — sem a terceira, a
  primeira consulta, que é história + achado + hipótese ANTES de haver conduta, seria
  recusada nomeando campos que o médico preencheu.
  ⚠️ E há um QUARTO lugar quando existe mais de uma porta de edição: **quem não edita,
  PRESERVA.** A janela de evolução do BALCÃO não tem esses campos na tela e reenviava nulos —
  com a cópia corrigida, ela passaria a APAGAR o que o médico escreveu. Ela agora os carrega e
  os devolve intactos, e eles não viram propriedade pública de propósito: propriedade pública
  convida um XAML a mostrá-los, e dado clínico do médico não se edita no balcão. É a mesma
  armadilha que a parcela 68 achou no vínculo com o horário, com a diferença que decide o
  desenho: `AtendimentoId` nulo é "o chamador não sabe" porque **nenhuma tela oferece
  desligar**; texto nulo é ambíguo, porque a tela do Consultório oferece LIMPAR — e por isso a
  saída é preservar no CHAMADOR, não adivinhar no serviço.
- **Teste que reenvia a ENTIDADE RASTREADA não testa versionamento** (parcela 74, 2ª rodada —
  falso positivo meu, pego pelo próprio teste). Mutar o objeto que `SalvarAsync` devolveu e
  reenviá-lo faz `GuardarVersao` copiar o valor **já alterado**, e a versão anterior nasce
  igual à nova. As duas telas de produção constroem um `new Evolucao`, e o teste tem de fazer
  o mesmo — senão ele reprova o produto por um defeito que só existe dentro dele.

- **`ToQueryString()` contra o Npgsql é a rede que faltava para tradução de consulta**
  (`TraducaoNoNpgsqlTests`, parcela 74, 2ª rodada). A tradução de LINQ para SQL acontece em
  **RUNTIME** e é **específica do provedor** — o que o SQLite dos testes aceita pode não
  existir no Postgres da clínica. `ToQueryString()` COMPILA a consulta contra o provedor real
  e devolve o SQL **sem abrir conexão nenhuma**: a cadeia pode apontar para um banco que não
  existe. Se a expressão não for traduzível, lança ali, no `dotnet test`, meses antes de a
  clínica esbarrar. **Consulta nova que use navegação, agregação ou função entra nessa
  suíte** — e ela também confere que a projeção NÃO traz a coluna cara (foi assim que o
  `Conteudo` do anexo ficou fixado fora do SELECT).
  ⚠️ Ela tem **autoteste**, pela regra da checagem 34: um caso que escreve a consulta como
  estava ANTES da correção e afirma que a compilação REPROVA. Rede que não prova ter dentes
  nasce cega.
- **`git add -A` commita o que você não revisou — e a suíte local pode rodar contra uma
  ÁRVORE DIFERENTE do commit** (parcela 74, 2ª rodada; o CI reprovou por isso). Dois arquivos
  de prova temporários (`ZzProvaTemp`, `ZzSqlProva`) vazaram para commits meus. O primeiro
  derrubou o CI — e reprovava **pelo motivo certo**, porque era a única coisa no repositório
  que enxergava o defeito da anamnese.
  ⚠️ O detalhe que ensina é por que a rodada local ficou **verde**: o arquivo estava no
  COMMIT e tinha sido apagado do DISCO antes de eu rodar a suíte. 1787 testes verdes contra
  uma árvore que não era a que eu havia commitado. É uma variante nova de "verde não quer
  dizer funciona", e o hábito que a fecha é ler `git status --porcelain` **antes** do
  `add -A`, e olhar o conteúdo de todo arquivo de prova antes de descartá-lo — foi olhando
  o `ZzProvaTemp` que o defeito da anamnese apareceu, e olhando o `ZzSqlProva` que a rede de
  tradução nasceu.

- **Quem RETIFICA precisa receber tudo o que quem REGISTRA recebe** (parcela 74, 2ª rodada —
  achado da auditoria adversarial, e é bloqueador). `EvolucaoEnfermagemService.RetificarAsync`
  não tinha sequer o parâmetro do processo de enfermagem: **corrigir uma vírgula do texto
  descartava a consulta inteira** — histórico, exame físico, avaliação, diagnósticos e
  cuidados, as cinco etapas que a COFEN 358/2009 torna obrigatórias. A linha anterior ficava
  na base (o prontuário não se apaga) mas virava a SUBSTITUÍDA, e a que passa a valer nascia
  vazia. A tela dizia "Registrado".
  ⚠️ E a metade da TELA era pior: `Corrigir` copiava hora, texto e intercorrência **da LINHA
  da lista** — que é um resumo FORMATADO (os sinais vitais vêm como a frase "PA 160/100"). A
  correção nascia sem a pressão aferida, e o ponto sumia da curva do paciente. Agora ela
  CARREGA o registro do banco e devolve os números ao compositor.
  ⚠️ Terceira metade: a chamada passava `hoje` como data. **A correção mantém a DATA DO
  FATO** — a técnica que corrige na segunda um registro observado no sábado estaria movendo o
  fato de dia, e a folha do sábado perderia a linha.
- **O flag que decide a PERGUNTA não é o que decide a GRAVAÇÃO** (parcela 74, 2ª rodada).
  `SessaoEmBranco` olha só os campos de TEXTO, e com razão: ela existe para decidir se a tela
  pergunta *"encerrar sem escrever a evolução?"*, e EVA e mapa são MEDIDA, não o registro do
  que aconteceu. Usá-la também para decidir se GRAVA fazia o "Finalizar atendimento"
  **descartar em silêncio** a sessão de acupuntura mais comum da casa — EVA antes 8, depois 3,
  seis pontos no mapa, nenhuma linha de texto. O serviço aceita a sessão só com EVA desde
  sempre; era a tela que não a mandava. Entrou `TemAlgoParaGravar`, e a lição generaliza:
  **quando um booleano começa a ser usado numa segunda decisão, releia se ele responde à
  segunda pergunta** — o nome não avisa.
- **Voltar etapa que consome o clique sem mover o cartão é botão que não faz nada** (parcela
  74, 2ª rodada). `VoltarEtapaAsync` promete "volta o cartão UMA coluna", e `FimAtendimentoEm`
  **não é coluna** por decisão da própria parcela — então gastá-lo num passo próprio fazia o
  primeiro clique não mudar nada enquanto as duas telas afirmavam que o cartão tinha voltado.
  O encerramento passou a sair JUNTO com a entrada na sala (sair da sala é o fato: quem não
  está em atendimento não tem fim de atendimento), e desfazer SÓ o encerramento virou ato
  próprio — `ReabrirAtendimentoAsync`, com porta na barra —, do mesmo jeito que
  `DesfazerChamadaAsync` é separado desde a parcela 38.

- **Estilo local sem `BasedOn` apaga o controle inteiro, e o diálogo rotulado "desisti" era o
  que GRAVAVA** (parcela 75, 2ª rodada — a auditoria adversarial que ainda estava rodando
  quando a primeira foi dada por fechada; as 70 rodadas de refutação dela morreram por limite
  de sessão, então **nenhum achado chegou contestado** e todos tiveram de ser reconferidos à
  mão). Três defeitos, e os três da mesma família: **nada falha, nada avisa, e quem descobre é
  quem abre a tela.**
  ⚠️ **(a) `<Style TargetType="ctrl:EstadoDaTela">` local, sem `BasedOn`.** O `Template` do
  controle mora no estilo IMPLÍCITO do design system (`Feedback.xaml`) e **não existe
  `themes/generic.xaml` em projeto nenhum** — então o `DefaultStyleKeyProperty.OverrideMetadata`
  não tem tema de onde cair. Estilo explícito no elemento SUBSTITUI o implícito, e o que sobra
  é um controle vivo, com `Ativo`, `Titulo` e `Descricao` corretos, desenhando **nada**. Some o
  estado vazio E some o terceiro estado — a tela de uma leitura que FALHOU fica idêntica à de
  um paciente sem antecedentes, que é exatamente a confusão que o `NaoVerificado` existe para
  impedir. **As dez ocorrências equivalentes do repositório trazem o `BasedOn`; a minha era a
  única sem.** Padrão que dez arquivos seguem e um não é sinal de que o um está errado.
  ⚠️ **(b) `PromptWindow` só aceita texto OBRIGATÓRIO — e duas perguntas prometiam o
  contrário.** A janela recusa o vazio com um erro em vermelho ("Escreva o motivo para
  continuar"), e está certa: quase toda pergunta dela é o motivo de um cancelamento. O defeito
  nasce quando a PERGUNTA anuncia o campo como opcional — *"Se quiser, diga o que mudou
  (opcional)"* na revisão da anamnese e *"Deixe em branco para usar o nome do paciente"* no
  recibo do caixa. A pessoa clica Confirmar, leva o erro, e a **única saída que lhe resta é o
  Cancelar** — que o chamador lia como "siga". Nos dois casos isso GRAVAVA: uma revisão de
  prontuário com versão e auditoria (não se desfaz) e um recibo numerado por ano (desfeito só
  com cancelamento e motivo escrito). **A porta rotulada "desisti" era a que efetivava o ato.**
  A correção mora no COMPONENTE — `obrigatorio: true` por padrão, e com `false` a janela aceita
  o vazio e devolve `string.Empty`, o que separa "desisti" (`null`) de "siga, sem texto". Nos
  DOIS design systems, para as cópias não divergirem (o débito permanente da parcela 7); e a
  frase de erro deixou de dizer "motivo", porque a mesma janela pergunta nome do recibo, agente
  da alergia e quantidade contada.
  ⚠️ **(c) A checagem 39 nasceu acusando a correção que estava certa.** Ela cobra as duas
  metades juntas (a pergunta opcional passa `obrigatorio: false`; quem passa `false` distingue o
  `null`) — e disparou no código recém-corrigido, porque a janela de busca da guarda era medida
  em CARACTERES e `_sem_comentarios` **apaga o comentário com espaços, preservando as
  posições**: o bloco de cinco linhas que explicava a correção empurrou o `if (motivo is null)`
  para fora da janela. É literalmente o tropeço da parcela 41, cometido de novo dentro da rede
  que existe para não deixar rastro. O autoteste sintético não o pegava porque não tinha
  comentário no meio — **caso de teste sem o ruído do mundo real é caso de teste que aprova a
  checagem cega.** Agora o branco é colapsado antes de medir, e há autoteste com oito linhas de
  comentário entre a chamada e a guarda.

- **Relógio INJETADO com vazamento: o carimbo que a auditoria compara escapou dele** (parcela
  75, 2ª rodada — `Registro_atrasado_de_ontem_e_legitimo` falhou às 00h21, com 1816 outros
  testes verdes). `EvolucaoEnfermagemService` recebe `Func<DateTime> agora` e o fixture do teste
  o **congela ao meio-dia**, com o comentário dizendo por quê: *"senão o teste vira loteria
  perto da meia-noite"*. E `RegistradoEm = DateTime.Now` ignorava o relógio injetado — logo o
  atraso de um registro de "ontem às 16h" era medido contra a hora REAL da máquina: 20h quando
  a suíte roda de tarde, **8h quando ela roda depois da meia-noite**, e a asserção pedia mais de
  12h. O teste falha todo dia entre 00h e 04h e passou meses porque nenhuma rodada caiu nessa
  faixa. É a mesma aritmética do `TrimEnd('0')` da parcela 67: **teste que depende do relógio
  não é teste, é sorteio** — só que aqui o dado tem 24 faces e seis delas são ruins.
  ⚠️ **O mais revelador é onde o defeito NÃO está**: `ChecagemPrescricaoService` (parcela 42),
  que inventou o relógio injetado neste projeto, faz `RegistradoEm = _agora()` desde sempre e
  tem escrito ao lado quais usos de `DateTime.Now` ficaram de fora **de propósito**. A cópia
  posterior — a evolução de enfermagem, parcela 71 — herdou a ideia e perdeu a metade que a
  torna testável. **Ao copiar um mecanismo de um serviço para outro, copie a REGRA junto: o par
  `Momento` × `RegistradoEm` é o que a auditoria compara, e um lado medido por outro relógio não
  é um par.**

- **"Árvore limpa" não prova que a árvore é a que você empurrou** (parcela 75, 2ª rodada — o
  contêiner reiniciou duas vezes no meio da conferência). `git status` respondeu *"nothing to
  commit, working tree clean"* e *"up to date with origin/…"* sobre um clone recriado num commit
  **oito parcelas atrás**: a referência `origin/…` local também tinha voltado, então os dois
  lados concordavam — e concordavam sobre a coisa errada. Foi um `find` que não achou
  `AnamneseView.xaml` que denunciou. **Confira o SHA do `HEAD` contra o remoto (`git fetch` e
  comparar), nunca só o `git status`** — e desconfie de toda leitura de arquivo que responda
  "não existe" para algo que você acabou de escrever.

- **O roteiro preenchia MENOS DA METADE da consulta, e os sinais vitais paravam na
  enfermagem** (parcela 76 — a direção pediu enriquecimento do módulo clínico para os dois
  lados, médico e enfermagem). Os dois achados são o mesmo defeito recorrente do projeto em
  duas variantes, e nenhum quebrava nada.
  ⚠️ **(a) `ModeloEvolucao` nasceu na parcela 63 com QUATRO campos e a evolução passou a ter
  NOVE nas parcelas 73 e 75.** Quem montava "sessão de acupuntura — lombar" recebia quatro
  campos prontos e **redigitava os outros cinco toda sessão** — de modo que os campos novos,
  que existem para o registro ficar mais completo, viravam digitação a mais em vez de menos.
  A lição vai para a lista dos oito lugares como o **nono**: *campo novo de evolução entra no
  MODELO*. E o teste que a fixa é o do lugar 3 — `SalvarModeloAsync` **copia campo a campo**
  quando o nome já existe, então o que ficar de fora da lista some ao REGRAVAR enquanto a
  criação continua funcionando, que é o que esconde o defeito.
  ⚠️ **O modelo aplica o que a tela MOSTRA.** A janela de evolução da RECEPÇÃO edita quatro
  campos e PRESERVA os cinco da consulta sem exibi-los; aplicar ali os cinco gravaria no
  prontuário um texto que a pessoa não viu e não pode conferir — a garantia aparente que
  este projeto recusa desde a parcela 3. Então ela não os aplica **e DIZ isso** quando o
  modelo escolhido os traz: aplicar cinco de nove campos em silêncio é meio sucesso
  apresentado como sucesso.
  ⚠️ **(b) A clínica disse que TODO paciente passa pela enfermagem** — a PA, a FC e a
  temperatura são colhidas minutos antes da consulta —, `SinaisVitais` era gravado desde a
  parcela 71, e `AtendimentoViewModel` **não tinha uma única referência a ele**. Quem
  prescreve escrevia a sessão sem os números na frente, ou saía da tela para procurá-los.
  A tela mostra **LEITURA, nunca coleta**: colher ali daria dois lugares para gravar a mesma
  aferição. E são **três estados escritos** — aferido (com a procedência ao lado), não
  aferido naquele dia, e não foi possível conferir —, porque num campo de sinais vitais
  confundir "ninguém mediu" com "o banco não respondeu" é do tipo que muda conduta.
  A **procedência** ao lado do número (`às 09:12, por Joana (COREN-SP 999999)`) é o que
  separa "eu medi" de "a técnica mediu"; sem ela o número parece do próprio exame físico.
  ⚠️ **E a regra de QUAL REGISTRO VALE virou uma função só.** `EvolucaoEnfermagem.Vigentes`
  (canceladas fora, retificadas fora) já existia escrita à mão dentro do
  `MedidaClinicaService`, e a tela nova precisava da mesma resposta. Duas definições de "o
  registro que vale" divergem na primeira correção — e a que ninguém lembra de ajustar
  passa a responder com um número **DESDITO**, que muda conduta e é indistinguível do
  certo. É a asserção que carrega `SinaisVitaisNoAtendimentoTests`.
  A **data é a da SESSÃO, nunca hoje** (a dívida de prontuário e a Minha semana abrem
  horários de dias passados), e a aferição de **outro dia não entra**: ela já tem casa — a
  curva de PA da tela de Medidas, que junta as da enfermagem com a procedência em cada
  ponto. Trazê-la para o cabeçalho da consulta seria pôr um número de três semanas atrás
  onde se lê "os sinais deste paciente agora".

- **A enfermeira prescrevia o cuidado e nada registrava que ele foi FEITO** (parcela 76,
  segunda metade — a etapa 4 da COFEN 358/2009). A Resolução divide o Processo de
  Enfermagem em CINCO etapas, e o sistema cobria as três primeiras (histórico, diagnóstico,
  resultado esperado) e a quarta **só como texto**: escrevia-se "curativo a cada 12h" e a
  execução não existia em lugar nenhum. Implementação sem registro é intenção — e cuidado
  que não se registra é, para qualquer fiscalização, cuidado que não aconteceu.
  **Tudo o que o serviço faz já tinha sido pago caro na parcela 42** e foi reusado tal e
  qual: hora **INFORMADA** com o relógio ao lado, hora futura **recusada**, não realizado
  **exige justificativa**, nada se apaga (**retifica-se** em linha nova), e quem checa é
  quem fez **login**, com o COREN copiado no ato. O bit é o MESMO (`ChecarPrescricao`):
  checar a execução é o mesmo ato e a mesma responsabilidade, e um bit novo nasceria
  desligado justamente para quem já faz isso hoje.
  ⚠️ **A regra que NÃO se copia, e é a asserção que carrega a suíte**: na folha de infusão
  "item já checado não se edita" — o item é de administração ÚNICA. O cuidado tem
  FREQUÊNCIA e é executado de novo a cada turno; copiar aquela guarda impediria a segunda
  troca de curativo do dia, e a técnica registraria a primeira e desistiria da segunda.
  ⚠️ **`SeNecessario` é BOOLEANO, não adivinhado do texto da frequência.** Cuidado
  condicional sem registro não é trabalho atrasado — é a condição que não ocorreu —, e
  contá-lo deixaria todo plano com um SOS eternamente pendente, com o contador da sala
  apontando para nada (a sutileza que a folha de infusão já documentava). Ler "se dor > 5"
  com regex daria um palpite que erra nos dois sentidos sobre um campo que é texto livre
  por desenho. E o campo nasceu **com porta no mesmo commit** — caixinha na linha do
  cuidado —, senão a regra que ele sustenta nunca valeria.
  ⚠️ **Retificar a evolução tinha de devolver o `SeNecessario`.** A linha que recarrega os
  cuidados para a correção copia campo a campo, e sem ela a retificação DESLIGARIA o "se
  necessário" de todo cuidado condicional, em silêncio — a regra "quem não edita, PRESERVA"
  aplicada ao que EDITA.
  ⚠️ **E a regra 8 do compromisso foi cumprida no mesmo commit**: a execução entra na
  EXPORTAÇÃO (planilha própria, com a retificada marcada — exportar o cuidado prescrito sem
  o que foi executado dá um prontuário que diz o que se mandou fazer e cala sobre o que foi
  feito) e na GUARDA, onde ela **move o prazo** — um plano escrito em janeiro é executado
  por semanas, e contar só a prescrição daria o prazo calculado pelo registro errado.
  ⚠️ O nome do cuidado, na exportação, sai do que **já está em memória** e nunca de
  `x.Cuidado`: a leitura é `AsNoTracking` sem `Include`, a navegação viria NULA em produção
  e a coluna sairia "#123" — enquanto o teste passaria pelo relationship fixup do EF. É a
  lição da parcela 68, e ela foi pega **na escrita** desta vez, não numa auditoria depois.

- **A conferência na escrita pega muito, e a RELEITURA do próprio diff ainda pega três**
  (parcela 76, 3ª rodada — a direção perguntou se eu havia conferido durante a escrita). A
  resposta honesta: a lista pegou seis coisas enquanto eu escrevia (a cópia campo a campo do
  `SalvarModeloAsync`, o `x.Cuidado` que viria nulo em produção, o `SeNecessario` que a
  retificação desligaria, a checagem 24 no selo, o `defaultValue` da coluna nova, o
  `timestamp without time zone`) — **e reler o diff inteiro depois de verde ainda achou mais
  três, todos meus e todos da mesma família.**
  ⚠️ **`PlanoNaoVerificado` gravado e sem LEITOR.** Eu escrevi a propriedade, documentei-a
  como "o terceiro estado" e nenhum XAML a lia. O texto do resumo salvava a situação por
  acidente; o booleano era enfeite. **Booleano de estado que ninguém lê não é estado — é uma
  atribuição.**
  ⚠️ **Um comentário prometendo o que o código não fazia.** `RetificarAsync` diz "a folha
  mostra as duas", e o quadro do dia filtrava as corrigidas ANTES de chegar à tela: sumia da
  vista justamente o registro retificado, que é o que uma auditoria de enfermagem procura. É
  o defeito da parcela 67 na área onde ele custa mais. A separação certa é a que ficou:
  `Checagens` leva TODAS as do dia (a corrigida marcada) e `Vigentes` é quem decide o que
  VALE — filtrar na leitura misturava as duas perguntas numa lista só.
  ⚠️ **`HistoricoDoCuidadoAsync` com chamador só no TESTE** — capacidade sem porta nascida
  no mesmo commit que a documenta. Removido: a exportação já leva o histórico inteiro, e o
  teste passou a ler pelo repositório, que é o caminho que a produção usa. **Método que só o
  teste chama é método que o teste inventou.**
  A lição de método, e ela não contradiz a regra da AUDITORIA DE LINHA: **a lista se percorre
  ao escrever CADA trecho, e o diff inteiro se relê UMA vez antes do commit.** São conferências
  diferentes — a primeira olha a linha, a segunda olha o que sobrou. Nenhuma das duas é rodada
  de agentes, e as duas cabem em minutos.

- **As duas fichas eram UMA rolagem só, e a do médico escondia metade do registro atrás de
  um Expander recolhido** (parcela 77 — a direção pediu reorganização de abas e sub-abas nas
  fichas de atendimento). As duas viraram **sub-abas**, e a forma não foi inventada:
  **S-O-A-P** do lado do médico (o que a pessoa DIZ, o que se ACHA nela, o que isso É, o que
  se vai FAZER) e as **cinco etapas numeradas da COFEN 358/2009** do lado da enfermagem. Não
  por acaso são a mesma forma — e ver as duas fichas com a mesma estrutura é o que faz o
  prontuário parecer um só sistema.
  ⚠️ **Sub-aba ESCONDE campo, e campo escondido de prontuário é como se escreve menos sem
  perceber.** É a única objeção séria à reorganização, e ela precisa de resposta no desenho,
  não de boa vontade: cada aba do médico anuncia se TEM conteúdo, com um ponto no rótulo que
  se vê de qualquer aba. É ele que denuncia o "Subjetivo" vazio de quem foi direto ao Plano.
  Sem esse indicador, dividir em abas seria trocar leiaute por registro mais pobre.
  ⚠️ **E a ficha da enfermagem NÃO ganhou o ponto — de propósito.** Ali já existe a faixa
  `EtapasEmFalta`, que NOMEIA a etapa que falta. Um segundo indicador seria uma segunda
  resposta para a mesma pergunta: **um estado vazio por pergunta** (parcela 37). A regra é
  "resolva a objeção", não "aplique o mesmo widget nos dois lados".
  ⚠️ **A altura fixa do `TabControl` da enfermagem conserta um defeito, não é estética.**
  Marcar "Consulta de enfermagem" abria as cinco etapas dentro de um `StackPanel` sem teto,
  numa janela de altura FIXA: o compositor crescia e espremia a **linha do tempo** — que é
  justamente o que se lê ANTES de escrever — até ela sumir. Cada aba rola por dentro.
  ⚠️ **O Expander recolhido era pior que a rolagem.** Os cinco campos da parcela 73 nasciam
  escondidos atrás de um clique que ninguém dá — metade do registro clínico invisível por
  padrão. Aba vazia se vê; seção recolhida, não.

- **A checagem 24 acusava `Text` LITERAL por causa de um binding no vizinho** (parcela 77):
  ela procurava a palavra "Binding" **na tag inteira**, então um `<TextBlock Text="●"
  Visibility="{Binding ...}" />` — o indicador das abas da sessão — era reclamado como "texto
  do banco sem quebra". A regra dela é "texto do BANCO tem tamanho imprevisível"; um literal
  o programador mede ao escrever, tenha ele binding no `Visibility`, no `Foreground` ou em
  nada. Passou a olhar o atributo `Text`, com autoteste nos dois sentidos.
  A lição vale para toda checagem futura: **quando ela reclamar do que está certo, o defeito
  é dela.** Silenciar o aviso acrescentando o atributo pedido é o caminho fácil e é o que
  ensina a próxima pessoa a ignorá-la — e checagem que se ignora para de pegar o defeito de
  verdade.

- **Os itens novos das duas fichas, e o lugar por onde um campo de prontuário some sem
  quebrar nada** (parcela 77, 2ª parte). **Médico**: `RetornoSugeridoEm` + nota e
  `Encaminhamento`. **Enfermagem**: o `AcessoVenoso` (local, calibre, punção).
  ⚠️ **O retorno NÃO vira agendamento**, e a regra é a mesma que a parcela 58 arrancou do
  2º código: materializar a sugestão como `Agendamento` põe na fila do balcão e na agenda de
  quem atende um cartão fantasma com botões que fabricam guia. Aqui é AFIRMAÇÃO CLÍNICA —
  "reavaliar em 7 dias" —, e quem marca horário é a recepção, com o paciente na frente.
  ⚠️ **E ele NÃO entra no `ModeloEvolucao`**, com a exceção declarada na entidade: o nono
  lugar da lista (parcela 76) vale para o que é ROTEIRO, e retorno e encaminhamento são
  decisões DESTA consulta. Um modelo que trouxesse "encaminhar para psiquiatria"
  preencheria sozinho, em toda sessão, uma afirmação sobre um paciente que ninguém leu.
  ⚠️ **O acesso venoso é três campos na EVOLUÇÃO, e não uma entidade.** Ele é um ACHADO da
  passagem, como a PA: quem o descreve é quem o viu naquele momento. Uma entidade "acesso"
  com ciclo de vida próprio precisaria de retirada, troca e motivo — nada disso foi pedido,
  e seria construir a exceção que ninguém vai exercer. E **em branco quer dizer "não
  avaliado", nunca "não há acesso"**: gravar o vazio como afirmação de ausência é inventar
  um achado que ninguém fez.

- **A busca no prontuário existia DUAS vezes, e as duas já tinham divergido** (parcela 77).
  `Casa(Evolucao, termo)` estava copiada na tela do Consultório e na da Recepção — e o
  comentário da segunda dizia, **por escrito**, que a primeira tinha sido atualizada e ela
  "ficou para trás". Consertaram uma vez e a cópia ficou; acrescentar campo agora as faria
  divergir de novo. Virou `BuscaNoProntuario.Casa`, no domínio.
  ⚠️ O que torna esta duplicata pior que as outras: divergência aqui não vira erro, vira
  **lista incompleta** — indistinguível de "não há nada escrito sobre isso", que é a pior
  resposta possível para uma busca de prontuário.

- **Três derivadas nascidas sem leitor, no commit seguinte ao que documentou isso**
  (parcela 77): `AcessoResumo`, `TemAcesso` e `DiasDeAcesso` existiam calculadas e nenhuma
  tela as mostrava — estruturar o acesso serve exatamente para LER, e a releitura do diff
  as pegou antes do commit. Ganharam três leitores: a linha da passagem (é o achado que a
  próxima punção consulta), a trilha de auditoria e a exportação do titular (art. 18 II).
  **É a segunda vez em duas parcelas.** A conferência que pega isto não é a lista percorrida
  linha a linha — é a releitura do diff INTEIRO perguntando "o que aqui não tem quem leia?".

- **O painel das últimas sessões mostrava 4 dos 12 campos — e o que faltava era a razão da
  consulta de hoje** (parcela 77, 3ª rodada — a direção perguntou se, ao abrir o atendimento
  de um paciente que retorna, o profissional vê a sessão passada). **Vê**: as três últimas
  ficam ABERTAS ao lado do formulário desde que a tela nasceu, com a linha do tempo de
  enfermagem e infusões abaixo, e a aba "Histórico de sessões" com o prontuário inteiro. O
  desenho estava certo; o CONTEÚDO tinha envelhecido.
  ⚠️ `LinhaSessaoAnterior` nasceu com quatro campos e a sessão passou a ter doze (parcelas
  73, 75 e 77). Ficava cega para a hipótese, o CID, o plano, o encaminhamento e — o que mais
  dói — o **retorno sugerido**: o profissional escrevia "voltar em 7 dias para reavaliar a
  EVA", o paciente voltava, e a tela **não dizia**. E a `Evolucao`, que é o texto mais
  escrito do sistema, estava no modelo e **não estava no XAML** desde que o painel existe —
  dado calculado sem leitor no lugar onde ele mais importa.
  A lição, que é a mesma da parcela 76 num SEGUNDO leitor: **campo novo de evolução entra
  também em quem MOSTRA a sessão anterior.** Ela vai para a lista dos oito lugares junto com
  o modelo — os dois são leitores que não quebram nada quando são esquecidos.
  ⚠️ **E a composição mudou de casa**: saiu da ViewModel para a `Application`
  (`ResumoSessaoAnterior`). Ela tem decisões — a hipótese com o CID entre parênteses, o CID
  sozinho quando é só ele, a linha que SOME quando não há o que dizer — e decisão que mora
  em projeto WPF não é alcançada pelo `dotnet test`. É a lição da grade da semana (parcela
  69) aplicada de novo, e é o que permitiu os nove testes que agora a fixam.
  O **retorno é o único campo do painel que a tela NÃO corta**: os outros saem com
  reticências e a dica mostra o texto inteiro, porque a coluna tem ~350 px e três sessões, e
  o texto completo está a uma aba de distância. Ele não, porque é curto e é a resposta.

- **A FICHA DO ATENDIMENTO: o motor existia, faltava a PORTA — e faltava a ENFERMAGEM
  inteira** (parcela 78 — a direção pediu um PDF do atendimento, com a logo e no padrão dos
  papéis que já saem, para o médico e a enfermeira entregarem ao paciente). A resposta
  honesta era **temos o motor e falta a porta**: o relatório de evolução é um
  `DocumentoClinico` desde a parcela 3, com a marca da clínica, numeração por ano, código de
  conferência, PDF pelo `DocumentosClinicosPdfService` e assinatura ICP-Brasil — e os **dois
  únicos chamadores dele estavam na RECEPÇÃO** (`FichaPacienteViewModel` e
  `DocumentosViewModel`). Quem acabou de escrever a sessão pedia ao balcão para imprimi-la.
  É o defeito recorrente do projeto na variante "a porta está no módulo de quem não usa",
  agora com o agravante de o papel ser justamente o que se entrega **com o paciente ainda na
  sala**.
  As três metades que faltavam, e o que cada uma decide:
  (a) **O RECORTE.** Os dois chamadores emitem sem `inicio`/`fim`, isto é, o histórico
  INTEIRO — para entregar "o atendimento de hoje", um paciente de quarenta sessões recebia
  quarenta. A porta nova emite recortada **na data DESTA sessão** (a do médico) e em **hoje**
  (a da enfermagem). O relatório sem recorte continua existindo e é outro papel: é o do
  convênio.
  (b) **A ENFERMAGEM não tinha papel NENHUM.** A passagem só saía impressa dentro da folha
  de infusão, ou seja, quando havia folha: a técnica que colhe sinais vitais, troca o
  curativo e registra a consulta de enfermagem completa — as cinco etapas da COFEN 358/2009 —
  não tinha o que entregar a ninguém. Ela entra no **MESMO documento**, e não num tipo novo:
  o paciente veio uma vez, e o que aconteceu com ele é um fato só; dois papéis obrigariam a
  clínica a entregar dois e o convênio a casar duas numerações. Só as **vigentes** —
  cancelada ou já retificada é registro desdito, e ele fica no prontuário, não no papel que
  sai da clínica. Quem assina cada linha é quem a fez, **com o COREN**.
  ⚠️ (c) **A LINHA DO TEMPO era uma promessa não cumprida.** O comentário das escalas
  garante, desde a parcela 36, que elas entram "na MESMA linha do tempo das sessões" — e
  elas eram **anexadas depois de todas**: o papel saía com as sessões, depois os escores,
  depois a enfermagem. Quem lê comparava o escore de agosto com a sessão de junho. Agora
  cada item carrega a DATA e a lista é ordenada na emissão (`OrderBy` do LINQ é ESTÁVEL, e é
  isso que mantém sessão → enfermagem → escala na ordem em que o dia aconteceu). É o defeito
  da parcela 67 — comentário que descreve um comportamento e não o realiza — num arquivo que
  eu estava editando por outro motivo.
  ⚠️ **`evolucoes[0]` ESTOURAVA numa ficha só de enfermagem**, e essa ficha passou a existir
  nesta parcela: o período do documento saía da primeira evolução MÉDICA. Agora sai do que de
  fato entrou no papel (`Menor`/`Maior` das duas listas), e a frase da EVA **só existe quando
  há sessão médica** — numa ficha de enfermagem, "nenhuma sessão tem a EVA medida" seria uma
  afirmação sobre um registro que não se propôs a medi-la.
  ⚠️ **O DIAGNÓSTICO e os CUIDADOS quase ficaram de fora, e o comentário que eu mesmo
  escrevi foi quem denunciou.** A primeira versão levava ao papel o texto, os sinais vitais,
  o acesso venoso e as etapas 1 e 5 — com um comentário dizendo "as etapas do processo entram
  quando FORAM escritas". As etapas **2, 3 e 4** não entravam, e são elas que fazem a consulta
  de enfermagem ser uma consulta: sem elas o papel diz "consulta de enfermagem" e mostra um
  texto livre, que é exatamente o que a clínica tinha antes de a etapa existir. As duas
  listas já vinham carregadas da consulta do repositório — era dado gravado sem leitor no
  único papel que sai da clínica. A regra de leitura vale para o próprio diff: **quando
  terminar de escrever um comentário, confira se o código abaixo dele faz o que ele diz.**
  **Emitir é FATO, então a ficha sai depois de SALVAR.** O papel é numerado, fica na lista do
  paciente e não se apaga — cancela-se com motivo; imprimir o que ainda não está no prontuário
  entregaria ao paciente uma versão que o prontuário não tem. ⚠️ A pergunta da guarda é
  `TemAlgoParaGravar`, **nunca** `!SessaoEmBranco`: confundir as duas é o defeito da parcela
  74, e aqui ele faria a sessão de acupuntura mais comum da casa — EVA 8→3, seis pontos no
  mapa, nenhuma linha de texto — **sair impressa dizendo "EVA não medida"** com o 8→3 na tela
  de quem imprimiu. E o `IsEnabled` do botão cobre as **duas** pré-condições que a guarda
  impede (paciente em foco E `VerProntuario`), senão quem não tem o bit clica e leva a recusa
  depois.

- **A FICHA DESENHADA: o mapa corporal e a curva da dor no papel** (parcela 79 — a direção
  pediu a ficha "animada com mapas corporais e outros, estilizado mas sem fluflu"). O mapa
  existe desde a parcela 3 e **nunca saiu impresso**: a sessão de acupuntura — a
  especialidade da casa — era relatada por texto, e onde a agulha entrou só se via na tela
  de quem marcou. As decisões, e por que cada uma:
  ⚠️ **A silhueta SUBIU PARA O DOMÍNIO, e não foi desenhada de novo.** Ela era um
  `GeometryGroup` no XAML do `MapaCorporalControl` — e o comentário no topo daquele arquivo
  já dizia por que copiá-la é proibido: *"copiar a figura teria criado duas versões do mesmo
  desenho — e a segunda correção de silhueta já sairia divergente"*. Pôr o mapa no papel ia
  criar essa segunda cópia, agora **atravessando camadas**, que é onde ninguém as lê lado a
  lado. Divergir aqui não estoura nada: produz um papel em que a agulha está num lugar do
  corpo e a tela mostra outro. Agora `SilhuetaCorporal.Formas` é a lista, o WPF monta o
  `GeometryGroup` dela e o PDF monta um SVG — e o **220×460 também virou uma definição só**
  (a const da tela lê a do domínio, e o Canvas não tem mais os números no XAML): os três
  TÊM de concordar, porque é dividindo por eles que o clique vira fração, e divergir espalha
  as marcações **em silêncio**.
  ⚠️ **O desenho é COPIADO na emissão** (`ItemDocumento.Desenho`, migration aditiva), nunca
  lido do prontuário na hora de imprimir. É a regra mais antiga do `DocumentoClinico` — a
  segunda via tem de sair idêntica à que o paciente levou —, e a sessão pode ser corrigida
  depois. Aqui ela não é desenho, é a Lei 13.787/2018.
  ⚠️ **E foi um `new` que quase engoliu tudo:** `EmitirAsync` copia o item **campo a campo**,
  e o `Desenho` era montado certo pelo relatório e sumia ali. O **lugar 3 da auditoria de
  linha**, na emissão do documento — a ficha saía sem mapa nenhum, sem erro em lugar nenhum.
  Quem o pegou foi o teste que abre o PDF, não a leitura do código.
  **A curva da dor** (`GraficoDaDor`, na Application) não repete a frase do corpo: mostra o
  que a frase não cabe — se a queda foi contínua, se houve recaída, se o alívio de cada
  sessão **dura** até a seguinte (daí serem DUAS linhas, antes e depois; só a de depois
  diria que o paciente melhorou, só a de antes esconderia o efeito da sessão). **O eixo
  ancora em 0 e 10** porque é a escala publicada da EVA — ajustá-lo aos dados faria uma
  queda de 8 para 7 parecer um despencar. **Duas sessões no mínimo**: uma é linha de base, e
  um ponto solto num eixo prometeria uma evolução que o registro não tem.
  **Três armadilhas de leiaute do QuestPDF, e as três só a folha montada mostra:**
  (a) **`AspectRatio` é largura/altura**, e invertê-lo pediu uma curva de 1633 pt de altura
  — o QuestPDF então **RECUSA o documento inteiro**, não desenha torto;
  (b) **elemento com largura FIXA maior que a área útil derruba a folha**: os 520 pt do
  sistema de coordenadas do gráfico contra ~510 pt de A4 com as margens da casa. Quem tem
  tamanho próprio pede proporção, não pontos;
  (c) **`ShowEntire` sobre bloco que cresce com o dado é uma bomba**: envolvendo a legenda,
  uma sessão no teto do mapa (80 pontos) pediria mais que uma página e a ficha **deixaria de
  imprimir**. Ele cobre o título e as figuras — altura conhecida — e a legenda corre solta.
  E o título entra JUNTO das figuras: a data sozinha no pé da página, com o corpo na folha
  seguinte, é um rótulo que não diz de que sessão ele é.
  ⚠️ **A lição de método é a da parcela 68, e ela se pagou três vezes nesta rodada:
  RENDERIZAR É DE GRAÇA.** `pdftoppm` desenha a folha, e foi só olhando que apareceram o
  título órfão, a página e meia em branco (uma figura de 130 pt fazia caber UM mapa por
  página; 112 pt fazem caber dois) e o vão morto ao lado do corpo, que virou o **resumo por
  técnica** — a leitura que o `TecnicaPonto` existe para permitir desde a parcela 3 ("o que
  dá para contar depois sem ler prontuário") e que nenhuma folha mostrava. **Teste que prova
  que o arquivo FECHA não prova que a folha MOSTRA**: o teste que carrega esta parcela gera
  o MESMO documento com e sem o desenho gravado e compara os operadores de caminho dentro
  dos fluxos de página.
  Detalhes que o código não conta sozinho: o SVG é escrito em cultura **INVARIANTE**
  (`cx="110,5"` em pt-BR é atributo inválido — o círculo some, ou a figura inteira não
  desenha, e nada avisa); a legenda numera **1..n na ordem**, não pelo `Ordem` cru, que fica
  com buracos depois de uma remoção e faria o leitor procurar os pontos 3 e 4 que não
  existem; **ponto estragado descarta só ele** — uma ficha que se recusa a imprimir por um
  campo mal gravado é pior do que uma com um ponto a menos, com o paciente esperando; e
  **técnica desconhecida mantém a marcação** (vira "Outra técnica"), pela regra do conversor
  tolerante da parcela 67: o ato aconteceu, e sumir com ele apagaria do papel algo que foi
  praticado.

- **O NOME DA TABELA SAI DO `DbSet`, NUNCA DA CLASSE — e migration aqui é escrita à mão**
  (parcela 79 — a clínica mandou o print: *"Não foi possível conectar ao banco de dados:
  42P01: relation `UsuariosSistema` does not exist"*, ao abrir o Consultório). A FK da
  tabela nova da parcela 76 apontava `principalTable: "UsuariosSistema"`, o nome da CLASSE;
  a tabela chama-se **`Usuarios`**, porque quem a nomeia é o `DbSet<UsuarioSistema>
  Usuarios`. `dotnet ef` acertaria sozinho — e não há `dotnet ef` neste ambiente, então a
  migration é digitada, e este é o único lugar do repositório onde o nome de uma tabela é
  digitado à mão.
  ⚠️ **O estrago não é a tabela que falta: é o que o usuário lê.** O erro estoura no
  `MigrateAsync` da ABERTURA, e o `SuiteApp` o apresenta como *"não foi possível conectar ao
  banco — deseja informar outra conexão?"*, que manda a clínica caçar a connection string,
  trocar a que estava certa e (se disser Sim) apagar a configuração da suíte. **Erro de
  schema exibido como erro de conexão é diagnóstico errado impresso na cara de quem não tem
  como saber** — é a mesma família da mensagem "See the inner exception" da parcela 67, com
  o agravante de a frase sugerir uma ação que piora o estado.
  ⚠️ **NENHUMA das redes podia pegar, e a razão é estrutural**: o C# compila (é string), o
  `compilar-sombra` não lê migration, e **os testes não executam migration nenhuma** — o
  SQLite deles monta o schema pelo MODELO, com `EnsureCreated`. É a terceira vez que este
  buraco aparece com outra roupa (o `xmin` da concorrência otimista, as datas com fuso da
  parcela 67): **"só o Postgres pega" quer dizer "só a clínica pega".** Virou a **checagem
  41**, que cruza todo `principalTable:`/`table:` das migrations contra as tabelas que
  alguma migration CRIA ou RENOMEIA — medida antes de decidir: 68 tabelas, ~80 migrations,
  **uma** ocorrência, que era o defeito. Autotestada contra o caso real e contra cinco
  legítimos (inclusive a tabela criada no próprio arquivo e a renomeada).
  **O banco da clínica não ficou quebrado**, e isso é do Postgres, não nosso: DDL é
  transacional e o EF envolve cada migration numa transação — a `20260824020000` inteira
  voltou atrás, sem gravar a linha no `__EFMigrationsHistory`. As duas seguintes nem
  chegaram a rodar (o EF para na primeira que falha). Atualizado o app, a fila continua de
  onde parou. **Não peça à clínica para mexer no banco antes de conferir se o erro foi
  transacional** — o conserto costuma ser mais caro que a falha.

- **A RODADA DOS PRINTS DO CONSULTÓRIO: quatro defeitos de leiaute, e três eram regras já
  escritas que a tela nova não seguiu** (parcela 79 — o cliente mandou quatro prints e a
  pergunta "você não quer que eu apresente isso a cliente, ou quer?"). Nenhum quebrava
  build; todos se viam no primeiro olhar. O que cada um ensina:
  (a) **Cinco botões numa StackPanel horizontal estouram a linha — e dois deles eram o RAIL
  disfarçado de botão.** "Evolução da dor" e "Avaliações" existiam como botões da barra E
  como itens do rail ao lado: duas portas com o mesmo nome fazem a pessoa procurar a
  diferença que não existe, e foram os 240 px deles que empurraram o "Ficha do atendimento"
  para fora da tela. **Só AÇÃO mora na barra; navegação é do rail** — e os dois comandos
  órfãos foram removidos (código morto que duplica a navegação, parcela 63).
  (b) **A pilha de faixas voltou no app onde a regra nasceu.** Alertas clínicos,
  administrativos e a faixa do termo eram `Alerta*` empilhados com moldura própria, mais o
  botão avulso do termo flutuando sozinho — a pilha que o README proíbe. Viraram UMA
  superfície com uma linha por aviso (traço de 3 px, o padrão da parcela 47), o termo com a
  porta NA linha, e a região inteira some quando não há aviso. O peso segue a **gravidade**,
  não a categoria.
  (c) **A mensagem da tela morava numa faixa no TOPO e quem mais a escreve é o Salvar, no
  RODAPÉ.** A recusa da ficha aparecia como banner rosa de largura inteira, longe do clique
  (a lição da parcela 65). Ela desceu para o lugar da dica do rodapé — mesmo lugar, fundo
  opaco, cor pela gravidade.
  (d) ⚠️ **O `EstadoDaTela` do componente cobria o componente INTEIRO — e o RESUMO era a
  segunda resposta para o mesmo vazio.** Na `LinhaDoTempoClinicaView`, a sobreposição
  cobria chips e resumo, e o resumo em seção vazia escrevia "Sem registro de …" — as duas
  respostas saíam desenhadas UMA POR CIMA DA OUTRA (o texto atravessando o ícone, no
  print). As regras violadas eram três, todas deste arquivo: a sobreposição pertence à
  REGIÃO cujo vazio ela explica (58/64/68), um estado vazio por pergunta (37) — e vazio
  agora NÃO escreve resumo. `Ativo` desliga a sobreposição no caso SEM ACESSO, onde quem
  explica é o resumo.
  (e) **Três Cards empilhados na aba Histórico viraram uma superfície** (problemas ·
  enfermagem · sessões, separadas por rótulo + `Separator`); e em Medidas, **a régua
  PRIMEIRA/ATUAL/VARIAÇÃO some sem série** — três traços "—" sobre a frase "nenhuma
  colheita" eram duas respostas para a mesma pergunta, e régua vazia se lê como tela
  quebrada.
  A lição de método: **tela nova herda as regras deste arquivo por LEITURA, não por
  osmose** — as quatro violações eram de regras já escritas e pagas em outras telas. Ao
  criar uma tela, a lista de conferência inclui reler a REGRA DE LEIAUTE com a tela
  desenhada na frente, região por região: barra (só ações?), avisos (uma superfície?),
  mensagem (perto do clique?), vazios (um por pergunta, na região certa?).

- **FILHO ANCORADO QUE NÃO CABE É DECEPADO — e o que ficava fora da dobra era o botão de
  SALVAR** (parcela 79, 2ª rodada — o cliente mandou o print da consulta COFEN cortada na
  borda da janela de Evolução de enfermagem). A janela tinha 680 px fixos e o compositor
  era `DockPanel.Dock="Bottom"`, uma PILHA que, com a consulta aberta, pedia ~850 px
  (campos fixos ~300 + banner + `TabControl Height="320"` + mensagem + botões). O
  DockPanel **não encolhe filho ancorado**: ele arranja no que sobra e o excesso é
  decepado na borda — as abas pela metade E os botões Fechar/Registrar fora da tela. No
  monitor de 768 px do balcão, **a consulta de enfermagem completa não podia ser SALVA**.
  É a irmã da checagem 36 (lá a pilha era `StackPanel` numa página; aqui, filho ancorado
  numa janela fixa), e o `Height="320"` era um remendo da parcela 77 que devolvia a linha
  do tempo à tela — somado aos vizinhos, continuava passando da janela.
  As decisões da correção, e cada uma é uma regra velha aplicada:
  (a) **rodapé da JANELA, ancorado e declarado ANTES do miolo** (parcela 47) — mensagem ao
  lado dos botões que a escrevem;
  (b) **escrever à ESQUERDA, reler à DIREITA** — o MESMO desenho do Atendimento do médico;
  dois desenhos para a mesma pergunta no mesmo sistema é a reclamação da parcela 47, e a
  linha do tempo deixou de disputar ALTURA com o formulário: passou a disputar LARGURA;
  (c) **as abas são o filho que PREENCHE, com piso BAIXO** (`MinHeight="100"`): quem
  protege do corte é o preenchimento elástico — o DockPanel dá às abas exatamente o que
  sobra —, e um piso alto seria o `Height="320"` de volta, só que menor. Janela maior =
  abas maiores; janela mínima = abas apertadas com rolagem interna, e NUNCA corte;
  (d) **o teto do campo de texto é parte da conta** (`MaxHeight="72"` no "observado"): sem
  ele, o texto crescendo comeria a coluna e sobraria menos que o piso das abas.
  ⚠️ E a checagem 15 pegou a primeira tentativa em segundos — Height=720 + barra de título
  passa dos 728 úteis do balcão. **Quando a resposta a "não cabe" for "aumente a janela",
  a rede do teto é quem diz até onde**: 690 + barra = 721 ≤ 728.
  A regra que fica: **pilha dentro de filho ancorado de janela FIXA é a checagem 36 sem a
  checagem** — ao ancorar um bloco que cresce com um checkbox, o elemento que cresce vira o
  filho que PREENCHE (com piso baixo), nunca uma altura fixa somada a vizinhos.

- **A ALERGIA NO TERMO DO BSV: o destaque vermelho antes da assinatura, e por que ele NÃO
  é um campo do termo** (parcela 80 — pedido da cliente: *"antes da assinatura um ponto de
  destaque grande pra documentar alergia... em vermelho, que chame atenção, que a técnica
  possa relatar ali também... porque dali o paciente já tem que sair com uma pulseira de
  alergia"*). A janela de coleta (`AssinaturaPacienteWindow`, ponto único das quatro
  portas) ganhou uma região VERMELHA entre as declarações e a área de assinar — antes da
  assinatura porque a pergunta tem HORA: depois de assinado o paciente levanta, e ele tem
  de levantar COM a pulseira.
  As decisões, e cada uma é uma regra da casa aplicada:
  (a) **O que a técnica relata ali vai para a LISTA DE PROBLEMAS** (`NaturezaProblema.
  Alergia`, via `ProblemaPacienteService.SalvarAsync` — validação, auditoria e gravação
  atômica). A alergia tem morada ÚNICA (parcela 75): é a lista que acende os alertas nas
  quatro telas e RECUSA a assinatura de prescrição — um texto guardado no termo seria uma
  segunda verdade, e a que ninguém lembraria de atualizar é justamente a que o alerta lê.
  É também o que faz a pulseira valer além do papel: o alerta segue o paciente em toda
  prescrição futura.
  (b) **A região mostra as alergias JÁ registradas** (o filtro do `AlertasAsync`: alergia
  alerta MESMO dada por resolvida; só o descarte cala) e **a instrução operacional** em
  negrito vermelho quando há alguma: "ele sai daqui com a PULSEIRA DE ALERGIA no braço".
  Vazia e conferida, ela manda PERGUNTAR — a região existe para forçar a pergunta, não
  para decorar.
  (c) **Falha de leitura é terceiro estado**, nunca região vazia: "sem alergia" e "não deu
  para conferir" são respostas diferentes, e a segunda manda perguntar e registrar.
  (d) **Leitura sob `VerProntuario`** (dado de saúde, art. 5º, II — quem não pode ler não
  vê a região: anunciá-la contaria que existem alergias, a regra da parcela 59); **escrita
  sob `EditarProntuario` OU `RegistrarEvolucaoEnfermagem`** (`ExigirAlgum` — o precedente
  da evolução de enfermagem: a técnica escreve alergia pelo bit dela). O perfil Enfermagem
  tem os dois lados; a recepcionista não vê nem escreve.
  (e) **O registro não depende do desfecho do termo**: a alergia é verdadeira mesmo que o
  paciente recuse assinar — botão próprio, gravação imediata, confirmação que já manda
  colocar a pulseira.
  (f) **Nada entra no conteúdo SELADO nem na tela do paciente**: o selo cobre o que o
  paciente declarou e assinou; a alergia é registro clínico de quem a colheu, com a trilha
  própria da lista de problemas. Mexer no selo seria mexer na evidência por causa de um
  destaque de tela.

- **"PRESSÃO CAIU" SAIU COMO ALERGIA — e a causa era o RÓTULO de um campo** (parcela 80,
  2ª rodada — a médica viu a folha de execução listar "• Dipirona • Pressão caiu" sob
  ALERGIAS REGISTRADAS e mandou separar as intercorrências). A auditoria do dado achou a
  porta exata: a janela de Evolução de enfermagem tinha o campo **"Reação a registrar como
  alergia"** — que pergunta a REAÇÃO — e o serviço grava o texto como o ALÉRGENO na lista
  de problemas. A técnica escreveu "Pressão caiu" (a reação, exatamente o que o rótulo
  pediu) e o sistema passou a afirmar, em toda tela e papel, que o paciente é alérgico a
  "pressão caiu". A porta da RODELA sempre perguntou certo ("Alergia a quê? Escreva só o
  agente — ex.: Dipirona"), e foi por ela que a Dipirona entrou certa: **o mesmo ato com
  duas perguntas é a lição da parcela 64, na variante em que a diferença não é a regra, é
  a FRASE** — e a frase errada fabrica dado clínico errado com cara de certo.
  As correções:
  (a) **O rótulo pergunta o AGENTE** ("Alergia observada — a quê?", placeholder "só o
  agente (ex.: dipirona)"), e o tooltip manda a reação para onde ela mora: o texto da
  passagem com "Foi uma intercorrência" marcado. O placeholder do termo (parcela 80)
  ganhou o mesmo aperto — "o que o paciente relatou" convidava narrativa.
  (b) **A folha de execução ganhou a caixa INTERCORRÊNCIAS DESTA EXECUÇÃO, separada da de
  alergias.** São dois fatos de naturezas diferentes, cada um com a sua morada: alergia é
  atributo do PACIENTE (lista de problemas — vale para sempre); intercorrência é EVENTO
  desta execução (evolução de enfermagem com o selo). A leitura é pública e pura
  (`IntercorrenciasDaExecucao`): vigentes com o selo; cancelada e substituída são registro
  desdito; texto dramático sem o selo não conta. "Nenhuma registrada nesta execução" é
  AFIRMAÇÃO impressa, não linha omitida — quem audita precisa ler que não houve.
  (c) **O dado errado que já existe não se conserta por código**: "Pressão caiu" está na
  lista de problemas como alergia, e a porta desenhada para isso é o DESCARTE com motivo
  (o único que cala o alerta) — a clínica descarta com "registrada como alergia por
  engano; era intercorrência, e está no registro da passagem".
  A lição que fica: **campo que alimenta registro clínico se audita pelo RÓTULO, não só
  pelo código** — o serviço estava certo, o banco estava certo, e a frase na tela fabricou
  o dado errado. Ao criar campo que grava em lista de outra natureza, a pergunta do rótulo
  tem de ser a pergunta da LISTA ("a quê?"), nunca a do contexto ("o que houve?").

- **O TERMO PELO WHATSAPP: o paciente assina no PRÓPRIO celular, na sala de espera**
  (parcela 81 — o pedido da cliente: *"a gente envia, ele lê, assina, envia e já cai no
  nosso banco... o médico/enfermeiro/secretária abre o termo e envia via WhatsApp para o
  paciente assinar/ler enquanto espera"*. O mapa completo está em
  **`docs/termo-pelo-whatsapp.md`**, e é lá que se atualiza). O cenário derruba a objeção
  que a parcela 66 tinha ao link: o paciente está NA CLÍNICA, no dia — o jejum continua
  sendo sobre o presente, e a identidade se confere no check-in.
  O desenho em uma linha: o desktop publica um PEDIDO minimizado no balde
  (`t/{token}`, o token de 2^127 das receitas), abre o wa.me com o link, e fica LENDO o
  balde à espera da RESPOSTA que o Worker grava; a resposta volta à janela da técnica, que
  confere e conclui pelo MESMO `ColherAsync` do balcão. As decisões que não são óbvias:
  (a) **O Worker NUNCA toca o banco** — só um binding R2 no prefixo `t/`. Vazamento da
  borda expõe no máximo os pedidos em aberto, nunca credencial do Postgres. Foi a escolha
  estrutural contra "Worker consulta o Neon", que seria mais direto e poria a chave do
  banco da clínica na borda.
  (b) **O pedido é MINIMIZADO e há teste fixando** (nem o SOBRENOME sai): cada campo a
  mais ali é dado de saúde a mais no ar. A mensagem do WhatsApp também — notificação
  aparece em tela bloqueada.
  (c) **A resposta nunca sela sozinha**: quem conclui é a técnica, vendo o traço e as
  respostas, com o documento conferido obrigatório como sempre. O papel do fluxo é tirar o
  custo do pad, não a pessoa do circuito. **O selo não mudou** — mudá-lo invalidaria a
  conferência dos termos já assinados.
  (d) **Write-once na borda** (segunda gravação recusada) e, do lado de cá, traço inválido
  manda CANCELAR E REENVIAR — não há segunda chance no mesmo token, de propósito.
  (e) **Expira em 24h FIXAS** (o link é para a sala de espera); a limpeza é tripla —
  concluir apaga, a varredura do Gerente apaga, e o Worker recusa vencido pela data DE
  DENTRO do pedido. `EnviarAsync` é idempotente (o 2º clique reaproveita o token em
  aberto: o paciente pode já estar com o link na mão) e o vencido é cancelado COM registro
  — a linha `ColetasRemotasTermo` não se apaga nunca: ela é a evidência do canal
  (telefone da ficha, quem enviou, IP/aparelho/hora da resposta).
  As armadilhas da casa que a implementação REENCONTROU, e pagou na hora:
  ⚠️ **A Visibility da área local de assinatura tem UM dono** — o code-behind já a
  atribuía por código (modo duas telas), e o gatilho de estilo que escrevi primeiro
  morreria no primeiro valor local (parcela 58). Virou `AtualizarAreaLocal()` no
  code-behind, com as DUAS condições (painel do paciente OU traço remoto).
  ⚠️ **A vigia de 5s e o Confirmar compartilham o DbContext do escopo da janela** — sem
  porteiro, o clique no instante da batida estoura "a second operation was started on this
  context" (parcela 69). `SemaphoreSlim(1,1)` na frente de TODA operação de serviço da
  janela.
  ⚠️ **O teste verde não provava o `Include`**: o serviço lê `documento.Itens` e o teste
  passa por relationship fixup mesmo sem Include (parcela 68) — conferido no repositório
  que `ObterDocumentoAsync` traz os itens, senão a página sairia SEM AS PERGUNTAS, calada.
  ⚠️ **O STJ escapa acento por padrão** (`Simpático`): para artefato lido por gente e
  pelo nosso próprio Worker, `UnsafeRelaxedJsonEscaping` — e o teste confere por VALORES
  parseados, não por substring cega.
  O que fica dito e não prometido: envio automático (WhatsApp Business) não existe — é o
  wa.me de um clique; assinar em casa dias antes continua fora (o jejum nunca); e o Worker
  (`tools/worker-termo-whatsapp.js`, rota `dominio/t/*`) precisa ser publicado no
  Cloudflare para o botão fazer sentido — o botão só APARECE quando o endereço público da
  publicação está configurado.

- **NO `workers.dev` O HOSTNAME É O WORKER — e o desenho de "dois workers, duas rotas"
  morreu no primeiro teste da clínica** (parcela 82; o cliente mandou o print: o link do
  termo respondia "Documento não encontrado ou fora do ar", a frase do worker das
  RECEITAS). O link do termo é `{endereço público}/t/{token}`, e o endereço público
  configurado é o hostname `workers.dev` do worker das receitas: rota por caminho entre
  dois workers **só existe com domínio próprio** — no `workers.dev` a requisição vai
  inteira para o worker dono do hostname, e o segundo worker (criado certinho pelo
  cliente) nunca recebeu nada. O erro foi MEU desenho presumir o CNAME da parcela 53 numa
  clínica que publica pelo `workers.dev`.
  A correção segue a regra da casa: **um arquivo só** (`tools/worker-clinica.js`), com as
  duas funções despachadas pelo caminho (`/t/*` → termo; resto → receitas/validador),
  colado no worker cujo hostname está configurado, usando o binding `BUCKET` que já existe.
  Dois arquivos eram também DUAS CÓPIAS de infraestrutura para o mesmo domínio — a segunda
  definição que diverge na primeira correção. Os dois arquivos antigos foram REMOVIDOS.
  ⚠️ **Registrado, não resolvido**: o endereço selado nos QRs das receitas já impressas é
  o `workers.dev` — renomear aquele worker (ou a conta) mata os QRs no ar, que é
  exatamente o risco que a parcela 53 documentou ao exigir domínio próprio. Migrar para um
  CNAME da clínica é decisão para quando a direção quiser: o worker unificado atende os
  dois mundos sem mudar nada no app.
  A lição de método: **quando a instalação depende de infraestrutura do CLIENTE (domínio,
  DNS, conta), o roteiro tem de ser escrito para a infraestrutura que ele TEM, não para a
  que o desenho gostaria** — e a primeira pergunta do roteiro devia ter sido "o endereço
  público de vocês é um domínio próprio ou um workers.dev?".

- **O CSP ESCRITO À MÃO BLOQUEOU O PRÓPRIO POST DA PÁGINA — e o erro culpava a internet
  do paciente** (parcela 82, 2ª rodada — no teste da clínica, o Enviar da página do termo
  morria com "Sem conexão"; o console dizia a verdade: `violates Content Security Policy
  "default-src 'none'"... 'connect-src' was not explicitly set`). Eu escrevi
  `default-src 'none'` por rigor e esqueci que a página faz exatamente UMA chamada — o
  POST da assinatura para a própria origem. Sem `connect-src 'self'`, o navegador recusa
  o fetch ANTES de ele sair, o `catch` de rede o apanha, e a frase que eu escolhi mandava
  o paciente conferir o Wi-Fi por um bloqueio meu.
  Duas regras que ficam: **CSP restritivo lista as chamadas que a página FAZ** — escrever
  `'none'` e ir liberando é o caminho certo, mas o checklist é percorrer cada `fetch`,
  `img`, `style` e `script` da página, e esta tinha um fetch que o autor do CSP era eu
  mesmo; e **mensagem de erro de rede não pode afirmar a causa que não conferiu** — "sem
  conexão" era plausível e errada, a mesma família da mensagem que manda trocar a
  connection string por um 42P01 (parcela 79). A frase virou "o envio não saiu", que é o
  que de fato se sabe.

- **A CENTRAL DE DOCUMENTOS ENTERRAVA A PRÓPRIA LISTA — 700 px de cartões de emitir e "o
  que já saiu" abaixo da dobra** (parcela 82, 3ª rodada — o cliente: *"esse layout me
  irrita... não dá pra ver os itens em 'o que já saiu'"*). Os 11 cartões de emissão
  tinham **248×222 px fixos** cada (título + descrição + pendência + botão, com ar de
  sobra) e, somados à faixa lateral de 320 px com TRÊS caixas empilhadas
  (Paciente/Período/Conferir), a lista de emitidos — justamente a porta da 2ª via —
  começava fora da tela. A tela respondia três perguntas com o peso invertido: o que se
  FAZ às vezes (emitir, conferir) ocupava tudo; o que se CONSULTA (a lista) não aparecia.
  O redesenho, só XAML, zero mudança de comportamento:
  (a) **cartão virou FICHA compacta** (232×118): o que se lê no RELANCE fica — nome da
  folha e O QUE FALTA (a regra da parcela 24: o requisito se descobre antes de errar) —,
  a descrição longa foi para a DICA, e o botão ancora embaixo (fileira com botões na
  mesma altura se lê como conjunto). ~700 px viraram ~260;
  (b) **a faixa lateral ficou com UMA caixa** (Paciente — contexto real de sete folhas);
  (c) **Período foi para o cabeçalho da lista que ele recorta** (na lateral parecia
  configuração da página) e **Conferir pelo código virou uma linha ali** — caixa
  permanente de 200 px para ato ocasional é o proibido do README;
  (d) a barra do cabeçalho é **WrapPanel como filho que PREENCHE** (dock Top = largura
  finita → dobra em tela estreita; a checagem 32 continua de olho).
  Duas conferências que a mudança exigiu e valem de lição: **os textos que apontam lugar
  ("escolha o paciente AO LADO", "usa o período escolhido ABAIXO") foram conferidos contra
  o leiaute novo** — os dois continuavam verdadeiros, mas texto espacial é o primeiro a
  mentir numa reforma; e **"as NOVE folhas" do subtítulo eram onze** — número escrito em
  prosa apodrece a cada folha nova, e saiu.

- **A CENTRAL DE DOCUMENTOS EM DUAS ABAS — e a correção anterior tinha parado no meio**
  (parcela 82, 3ª rodada — o cliente: *"Ainda está totalmente amador! Por que o 'O que já
  saiu' não vira uma sub-aba dentro de Documentos?"*). A rodada anterior tinha encolhido
  os cartões e desmontado a faixa de três caixas — e DEIXOU a tela como duas caixas
  empilhadas com lateral permanente: mexeu no tamanho das peças sem responder a pergunta
  da regra de leiaute, **"quantas perguntas esta tela responde?"**. São DUAS ("emitir um
  papel novo" e "achar o que já saiu"), e pergunta a mais é ABA (parcela 55: item que
  cobre mais de um assunto usa sub-abas), não caixa menor.
  O que a separação destravou de graça: **a lista ganhou a largura inteira** — o seletor
  de paciente é contexto da EMISSÃO (sete das nove folhas são de alguém) e foi para a aba
  dela; a lista recorta por PERÍODO, não por paciente (conferido no ViewModel:
  `EmitidasAsync(inicio, fim, acessos)` — a lateral nunca a filtrou). E o H3 "O que já
  saiu" dentro da aba homônima saiu — rótulo da região é a própria aba; o que fica é o
  RESUMO, que diz o recorte.
  A lição, e ela é sobre o MEU processo: **quando o cliente reprova uma tela, a correção
  começa pelas três perguntas da regra de leiaute, não pelo tamanho das peças.** Encolher
  cartão, compactar barra e mover caixinha são refinamentos DA resposta errada quando a
  pergunta estrutural ("isto é uma tela ou duas?") ainda não foi feita — e foi preciso o
  cliente apontar a aba pelo nome para ela ser feita.

- **Quando o texto APROVADO chega, o botão do rascunho vira SUBSTITUIÇÃO — a recusa "já
  existe" viraria beco sem saída no exato momento em que o texto certo chegou** (parcela
  84 — o advogado da cliente mandou os dois termos do BSV por escrito, e ela pediu para o
  conteúdo entrar no lugar do rascunho da parcela 67). `ModelosTermoBsv` agora carrega o
  texto dela: o **TCLE** (assinado UMA vez, fica na ficha — `SoValeNoDiaDoProcedimento:
  false`) e o **Termo da sessão** (assinado a CADA sessão: "fui reavaliado NESTA data"
  não se herda — `: true`). A marca "(rascunho — revisar)" morreu COM o rascunho:
  mantê-la num texto aprovado mandaria o responsável técnico revisar o que ele já
  assinou embaixo. E a guarda "o BSV já exige termo" — certa quando o clique repetido só
  podia ser engano — deixaria a clínica SEM caminho para adotar o texto novo: virou
  `ConfirmarPerigo` que lista o que está amarrado, DESLIGA (`AlternarAsync`, nunca
  apaga — os termos assinados guardam o texto lido, porque aplicar COPIA) e amarra o par
  novo; só as exigências ATIVAS entram na pergunta, porque `ExigenciasAsync` devolve
  também as desligadas e desligada não está "em uso".
  A transcrição papel→sistema é adaptação de FORMA, não de conteúdo, e as regras: os
  blocos de IDENTIFICAÇÃO/ASSINATURAS com linhas em branco saem (a folha emitida imprime
  isso e a assinatura é colhida na tela); **lacuna impressa ("____", Diagnóstico, CID)
  vira remissão ao prontuário** — modelo é texto FIXO copiado na emissão, e lacuna
  sairia em branco para sempre; as listas de "☐" viram as declarações Sim/Não (que é o
  que elas são no papel), mantidas AFIRMATIVAS e incondicionais mesmo vindas do
  advogado — a redação das declarações é responsabilidade nossa de FORMA, porque o "Não"
  aceso no balcão precisa continuar significando problema; e **seção que é ato da EQUIPE
  (avaliação médica, dados da sessão, intercorrências, alta) não entra no papel que o
  PACIENTE assina** — cada uma já tem registro próprio com autoria e trilha, e o corpo
  do termo diz onde. O teste que fixava o rascunho virou o que fixa as ÂNCORAS do texto
  aprovado (o jejum "6 (seis) horas", os imunobiológicos da seção 7, a remissão ao
  TCLE) — para uma edição futura no código não devolver o texto genérico em silêncio.

- **Os três da fila da parcela 69 que não dependiam de ninguém: espera de falta, bloqueio
  invadido e a coluna do desativado** (parcela 85). Nenhum quebrava nada; cada um tem uma
  regra que generaliza:
  (a) **Espera ABERTA só corre para quem ainda está na FILA.** `EsperaMinutos` completava
  com `agora` sempre que faltava a chamada — então quem chegou e virou FALTA "esperava"
  até hoje, e a média do painel ia a milhares de minutos num dia passado. A regra que
  ficou no domínio: falta e cancelamento não CONCLUEM a espera, DESFAZEM a medida
  (ninguém carimbou quando a pessoa desistiu — medir até `agora` é ficção), e espera
  aberta não atravessa o dia da chegada. A já MEDIDA (chegada → chamada) sobrevive ao
  desfecho: o fato aconteceu. **Métrica com relógio em aberto precisa dizer QUANDO o
  relógio para de valer** — e "quando o caso sai da fila" é quase sempre a resposta.
  (b) **A folga da BUSCA tem de cobrir o que o filtro fino decide.** `MarcadosDentroAsync`
  já usava `ColideCom` (sobreposição correta) — mas a consulta do repositório filtrava
  por `DataHora >= inicio`, então a sessão das 13h30 que invade o bloqueio das 14h nunca
  CHEGAVA ao `ColideCom`. Filtro fino certo depois de busca grossa errada é o filtro
  certo rodando sobre a lista errada — e não aparece em teste nenhum que só marque
  sessões DENTRO do período. A folga é a meia-noite do dia (sessão não atravessa o dia).
  (c) **Coluna por recurso ATIVO esconde o horário do recurso DESATIVADO.** A grade do
  dia montava colunas só para profissionais/salas ativos; o horário de quem foi
  desativado sumia com o cabeçalho contando-o, o vão ficava clicável e a recepção marcava
  por cima. O dono inativo com horário ganha coluna própria "(inativo)" — nunca cai em
  "Sem profissional", porque o horário TEM dono e atribuí-lo a ninguém esconderia quem
  precisa ser remarcado. As caixas "Ativo (aparece na agenda)" da tela de equipe
  prometiam o contrário do que acontecia e foram reescritas: **quando um sinalizador
  ganha efeito novo, o texto da caixinha que o liga entra no mesmo commit.** E o resumo
  passou a contar COLUNAS, não salas ativas — número do cabeçalho que não bate com o
  desenho da grade foi exatamente o que denunciou o defeito.
- **A VALIDAÇÃO COMPLETA DA RECEPÇÃO — "tudo funcionando perfeitamente" pedido pela
  direção, e o que três auditorias adversariais acharam com 1670 testes verdes**
  (parcela 86 — numerada após as do prontuário clínico, 71-85, trabalhadas em paralelo). As três redes locais passaram ANTES da rodada; nenhum dos achados
  quebrava build ou teste — a assinatura de sempre. Os que ensinam regra nova:
  **String na posição de string: a troca de parâmetro que nenhuma rede vê.** O
  "Excluir" da aba Prontuário da FICHA chamava `CancelarAsync(id, Operador)` — o
  operador caía no parâmetro `motivo`, o motivo obrigatório da parcela 52 virava o
  login (nunca vazio, então o serviço não recusava) e `CanceladaPor` ficava nulo: a
  trilha respondia "?" para "quem cancelou este registro clínico". A tela irmã
  (Prontuário) fazia certo, com `PerguntarTexto`. É o `CS1503` da família que o
  compilar-sombra não alcança — string encaixa em string. **Ao chamar método com dois
  parâmetros do mesmo tipo, escreva os nomes** (`motivo:`, `operador:`); e quando duas
  telas fazem o mesmo ato, o teste que falta continua sendo o que compara as duas.
  **O backfill que só é seguro na PRIMEIRA vez.** Religar a chave `GuiaNoAgendamento`
  repetia o backfill de `RealizadoEm` sobre "todo atendimento sem carimbo" — e depois
  da primeira ativação existe atendimento SEM carimbo de propósito: a sessão marcada
  (e a cancelada). Desligar e religar — o ritual que a própria tela ensina — carimbava
  como visita sessão que nunca houve, corrompendo retenção/origem/estreia sem sintoma.
  O filtro exclui atendimento pendurado em agendamento não-`Realizado`. **Premissa de
  migração escrita como "neste momento não existe X" expira no momento em que X passa a
  existir — releia-a em toda operação que pode rodar DUAS vezes.**
  **Confirmar presença de horário CANCELADO produzia sessão Realizada com todas as
  guias suspensas.** A corrida é real (duas máquinas, fila que relê a cada minuto), a
  recusa faltava no SERVIÇO — as telas só guardam a metade visível. Agora recusa
  mandando reabrir pelo Remarcar, que é quem devolve as guias.
  **A especialidade da consulta é parte da GUIA, e só a modalidade disparava a
  regeneração.** Trocar "Consulta/Psiquiatria" por "Consulta/Geriatria" mantém o código
  "Consulta": o horário gravava a nova e a guia seguia com a antiga. `regerarGuias`
  agora olha as duas; e a SÉRIE ganhou o `primeiroCodigo` que o ramo único já passava
  (campo que a tela oferece e o serviço descarta, de novo).
  **O aviso de guia descartado nas portas irmãs.** `CancelarAsync`/`MarcarFaltaAsync`
  devolvem os avisos do regime novo — inclusive "guia JÁ BAIXADA no portal" — e a FILA
  os jogava fora (a Agenda os punha em snackbar de 4s; a cópia do faturamento também
  descartava). Aviso que exige ação vai em DIÁLOGO nas três portas. E o formulário de
  agendamento do FATURAMENTO ganhou a pergunta de duplicidade informada pela capa — era
  a única porta de criação sem ela.
  **A mensagem escrita antes do recarregar, pela terceira vez** (Retorno e
  Confirmações — a rodada dizia "N sem consentimento (LGPD)" e a carga zerava a frase
  antes de ela renderizar); **o catch da batida silenciosa que pinta a tela**, pela
  segunda (as sub-cargas do Painel limpavam as pendências do dia numa engasgada do
  banco de 2 em 2 min); **barreiras**: enviar documento clínico sem `Exigir` na cópia
  da Recepção (a 68 corrigiu só o Consultório), o Emitir do orçamento exigindo o bit de
  LEITURA (`VerFinanceiro`) onde as portas exigem escrita, o WhatsApp de confirmação
  exigindo `EditarAgenda` num ato de leitura (as duas metades discordavam), e a guarda
  do "marcar" decidindo pela chave em CACHE da tela (na dúvida, a guarda exige MAIS).
  **Menores pagos**: "Excluir"→"Cancelar…" nos dois botões de evolução; N+1 de anexos
  na ficha (o método de uma consulta existia desde a 37 e a ficha não o usava); falta
  fora da espera média; o menu "⋯" da Fila escondendo falta/cancelamento de quem a
  guarda autoriza; capa do dia dizendo "particular" para sessão cancelada.
  **Ficou documentado sem correção (decisão ou desenho)**: itens 1, 2 e 7 da fila da
  69 (decisões de cliente/leiaute); o formulário da agenda sem reconferir elegibilidade
  na troca de data (item 6, metade); a observação do faturista sobrescrita pela
  suspensão; a NC de sessão cancelada que ressuscita na volta do paciente; a
  `Categoria` do paciente atualizada na marcação sem reversão no cancelamento; os dois
  commits do `ListaEsperaService.ChamarAsync`; férias do profissional invisíveis na
  visão de SEMANA da Recepção.

- **O KANBAN NOVO DA FILA — o redesenho que a cliente pediu comparando com o Smart
  Clinic** (parcela 87; mockup aprovado em canvas antes de uma linha de WPF — a lição
  das seis reprovações aplicada de véspera). "A fila/agenda em kanban do smart clinic é
  muito melhor que a nossa, tanto visualmente quanto funcional" — e o inventário deu
  razão com fatos: a raia tinha a MESMA cor do fundo da tela (o quadro não se lia como
  quadro), o nome do paciente tinha o mesmo corpo da legenda (12px — hierarquia só de
  peso), e o cartão calava atraso, confirmação, convênio, término e pacote — tudo dado
  que o banco JÁ TINHA.
  As decisões:
  (a) **Cores de estado moram no CABEÇALHO da raia, não no cartão** — `CabecalhoRaia`
  ganhou `Ponto`/`PilulaFundo`/`PilulaTexto` (nulos = o visual de sempre; os padrões da
  pílula moram no ESTILO, porque o C# não alcança StaticResource). Cartão continua
  branco em toda coluna: cor por estado no cartão brigaria com os DOIS estados que já
  pintam ele inteiro (chamada demorada, atraso).
  (b) **`Brush.Raia` é token** (Cinza.100) — raia um degrau acima do fundo é o que
  devolve o quadro ao olho, e vale para os dois quadros pelo mesmo token.
  (c) **O atraso mora no DOMÍNIO** (`Agendamento.AtrasoMinutos`): só Agendado, sem
  check-in, hora estourada — quem chegou está ESPERANDO, não atrasado (os dois selos
  juntos se contradiriam), e dois quadros com duas contas de "atrasado" divergiriam.
  (d) **O selo de confirmação só afirma o que a rodada afirmou**: "Confirmou" =
  Respondido; "Não confirmou" = Enviado sem resposta; sem contato = SEM selo — acusar
  de "não confirmou" quem nunca foi avisado seria o selo mentindo. E só na coluna
  AGUARDANDO: depois do check-in é ruído. A leitura é em lote
  (`ConfirmacoesDosAgendamentosAsync`), uma consulta para o dia.
  (e) **A espera média virou definição ÚNICA** (`PainelRecepcaoService.EsperaMediaMinutos`,
  estático) — o painel e o resumo da fila leem a mesma conta; nula sem base, nunca zero.
  (f) **A faixa CHAMANDO nomeia o MAIS ANTIGO e carrega o "Entrou" DENTRO dela**, pelo
  MESMO comando (e a mesma guarda) do botão do cartão — é para a faixa que a
  recepcionista está olhando quando o paciente levanta; os demais chamados viram uma
  linha ("Também: …").
  (g) **O filtro por profissional remonta da memória** (`MontarQuadro` separado da
  carga; entre o Clear e o último Add não há await) e só aparece com DOIS ou mais
  profissionais no dia — chip único é ruído. O filtro vigente sobrevive à recarga só
  enquanto o profissional continua no dia.
  (h) **Paridade entre os dois quadros é regra, não coincidência**: Meu dia ganhou as
  mesmas cores, a mesma hierarquia (nome grande, hora–fim como contexto), e as duas
  divergências históricas caíram (FINALIZADOS→FINALIZADO; encaixe Badge.Info→Aviso) —
  o mesmo fato com duas cores nos dois quadros se lia como dois fatos.
  O que ficou POR DECISÃO da clínica (as notas do canvas): cancelado/falta dentro do
  quadro (o Meu dia já mostra; a fila segue fora), situação de pagamento no cartão, e
  o clique no cartão abrindo a ficha.

- **A ENFERMAGEM NÃO TEM AGENDA PRÓPRIA — e cadastrá-la CERTO esvaziava as cinco telas
  dela** (parcela 88 — a cliente: *"os enfermeiros podem ver todos os pacientes e clicar em
  atender, em vez de ver só os pacientes dele"*; o mapa completo está em
  `docs/atendimento-medico-e-enfermagem.md` §13, e é lá que se atualiza). Três fatos
  verdadeiros, e o defeito era a SOMA deles: (1) as cinco listas do Consultório filtram por
  `SessaoUsuario.Atual.ProfissionalId`; (2) a enfermeira PRECISA de um `Profissional`
  vinculado, porque é dele que sai o COREN copiado em cada registro (parcela 72) e
  `IdentificacaoExecutante.Exigir` recusa sem ele; (3) os horários pertencem a quem
  CONSULTA — ela passa por todos eles.
  ⚠️ **Nada falhava.** Build verde, 1908 testes verdes, três redes verdes — e o dia, a
  semana, a carteira, a dívida de prontuário e os números dela abriam VAZIOS, justamente
  por ela estar cadastrada certo. Tela vazia se lê como sistema quebrado, não como "esta
  lista não é para você".
  A decisão mora no **DOMÍNIO** (`PerfisAcesso.EscreveComoEnfermagem` /
  `ProfissionalDaListaDoPosto` / `MotivoDaListaDoPosto`), e `PostoClinico` é só o adaptador
  que lê a sessão: projeto WPF não compila no `dotnet test`, e regra que o teste não alcança
  apodrece sem ninguém notar.
  ⚠️ **Pelos BITS, nunca pelo texto do conselho.** `RegistroConselho` é campo livre
  ("COREN-SP 999999", "Coren SP 12345", "coren/sp 12345"): procurar "COREN" nele erraria no
  dia em que alguém digitasse diferente, e erraria em SILÊNCIO. O corte é o X · Y da parcela
  72 — escreve por `ChecarPrescricao | RegistrarEvolucaoEnfermagem` e **não** por
  `EditarProntuario | Prescrever`. E quem tem OS DOIS lados (o Gerente, que recebe `Todas`)
  responde **false**: ele TEM agenda própria, e devolver-lhe a clínica inteira esconderia
  justamente os pacientes dele. A regra é "escreve **SÓ** por Y".
  ⚠️ **A FRASE vale tanto quanto o filtro.** São DOIS motivos para a lista ser a da clínica,
  e a mensagem não pode ser uma só: *"peça à direção para ligar o seu usuário ao seu
  cadastro"* é verdade para quem não tem vínculo e MENTIRA para a enfermeira, que está
  vinculada. Instrução errada com cara de instrução certa manda o suporte procurar um
  defeito que não existe — é a irmã de "falha exibida como sucesso", e ela sobrevivia
  escrita à mão dentro da guarda do "Chamar próximo" depois de o texto da tela já ter sido
  corrigido. **Frase de guarda sai do PONTO ÚNICO.**

- **"Atender" é uma palavra só, e precisa levar à seção de escrita de QUEM clicou**
  (parcela 88). `PostoClinico.ChaveDoAtendimento()` bifurca: quem consulta cai no S-O-A-P,
  quem executa cai na passagem de enfermagem — as duas na MESMA tela do paciente (mesmo
  crachá, mesmo rail, mesmas seções de leitura), mudando só a seção que abre. Sem a
  bifurcação a técnica cairia no formulário do médico, onde o `Salvar` segue
  `PodeEditarProntuario` e ela não o tem: o **botão que não faz nada** da parcela 41, com
  uma tela inteira em volta.
  A seção nova (`Atendimento de enfermagem`, posição 1 do rail) repete o desenho do
  Atendimento do médico DE PROPÓSITO — escrever à esquerda, reler à direita, rodapé
  ancorado fora do scroll —, e a coluna da direita é a **imagem espelhada**: o médico relê a
  enfermagem e a infusão; a enfermagem relê a sessão médica e a infusão.
  ⚠️ **O compositor NÃO foi reescrito**: a seção declara `DataContext="{Binding Passagem}"`
  sobre o MESMO `EvolucaoEnfermagemViewModel` da janela da sala. As regras caras (hora
  informada, hora futura recusada, retificação que preserva a data do fato, alergia no mesmo
  `SaveChanges`) não podem existir em duas cópias.
  ⚠️ **E a janela CONTINUA** — ela não é dívida, é a porta que a seção não alcança: a folha
  de execução é MODAL e `PodeMexer => PodeChecar && EmExecucao`, então folha encerrada apaga
  o painel inteiro, e é ali que se registra a reação que aparece meia hora depois da última
  bomba. **A seção é a quinta porta, não a substituta.**
  ⚠️ **O caminho de volta é parte da feature.** A tela da Enfermagem é do SHELL e é publicada
  por DOIS módulos: no `Clinica.Recepcao.exe` o módulo Clínico não está carregado, e
  `NavegacaoSuite.Ir` devolveria `false` EM SILÊNCIO. Pergunta-se antes com `Existe`, e o
  painel da própria tela atende onde não há posto clínico.

- **A cópia campo a campo do serviço engoliu o "se necessário" por doze parcelas** (parcela
  88). `EvolucaoEnfermagemService.AplicarProcesso` copia o cuidado campo a campo — o **lugar
  3** da lista de conferência — e `SeNecessario` ficou de fora desde que o campo nasceu
  (parcela 76). A caixinha da tela gravava sempre `false`.
  ⚠️ **E o estrago não aparecia como falha, e sim como CONTAGEM errada**: `CuidadoDoDia.
  Pendente` é `!SeNecessario && Vigentes.Count == 0`, então TODO cuidado condicional ("se
  dor > 5") ficava eternamente aguardando registro e o contador da sala passava a apontar
  para nada — exatamente o que o comentário do próprio campo diz existir para impedir. A
  retificação sofria do mesmo (mesmo `AplicarProcesso`), então corrigir uma vírgula
  desligava o SOS de todo cuidado condicional.
  Os dois testes novos **falham no código anterior** — foi verificado, não presumido.

- **Dado gravado sem leitor, na variante que faz o COMENTÁRIO mentir** (parcela 88):
  `EvolucaoEnfermagem.AgendamentoId` era gravado desde a parcela 71, preservado na
  retificação, e **nenhuma consulta, tela ou papel o lia**. A seção nova ia escrever
  *"a passagem fica registrada nesta sessão"* — promessa que o código não cumpre, o defeito
  da parcela 67. Ganhou o primeiro leitor: o selo **DESTA SESSÃO** na lista de passagens,
  que responde a pergunta de quem está com o paciente na frente ("já registrei alguma coisa
  NESTA passagem, ou o que estou vendo é de outro dia?").
  A regra que fica: **antes de escrever um comentário que promete um vínculo, procure quem
  o LÊ.** Se ninguém lê, ou se constrói o leitor, ou a frase muda.

- **A MESMA lacuna do lado de quem CONSULTA — e a lista da clínica é o segundo clique, não
  o padrão** (parcela 88, 2ª rodada; a cliente corrigiu: *"a tarefa acima se refere a
  enfermeiros e médicos também, eu mencionei somente enfermeiros"*).
  `MeusPacientesAsync(profissionalId)` devolve só quem ELE já atendeu, então o paciente de
  **primeira consulta**, o do **colega** que ele cobre e o que o **balcão acabou de
  cadastrar** eram INALCANÇÁVEIS do Consultório — não havia segunda porta, e a busca da
  tela filtra em memória o que já veio. "Meus pacientes" ganhou os mesmos dois chips
  exclusivos da tela da Enfermagem.
  ⚠️ **A carteira dele continua sendo o PADRÃO**: "quem eu acompanho" é a pergunta que a
  tela responde todo dia, e trocá-la pela clínica inteira afogaria os pacientes dele no
  cadastro. O pedido era o segundo clique, não a troca do primeiro.
  ⚠️ **Busca sobre lista CORTADA tem de ir ao SQL.** No modo "todos" a lista vem com teto, e
  filtrar em memória o que veio cortado faz a busca responder *"não existe"* para todo
  paciente além dele. É a resposta errada mais cara que uma busca de paciente pode dar,
  porque leva a **cadastrar a pessoa de novo** — o CPF duplicado da parcela 57 pela porta
  de trás. O termo desceu para `MeusPacientesAsync(..., termo:)` (que casa nome OU CPF),
  com o agrupamento de teclas do `SeletorPacienteViewModel`, e o **teto é DITO** na frase
  do resumo: lista cortada que se anuncia como "todos os pacientes" é corte silencioso.
  ⚠️ **Filtro de memória sobre resultado de servidor DERRUBA o que o servidor casou a mais.**
  O SQL casa nome OU documento; o filtro em memória só conhece o nome. Refiltrar depois
  faria a pessoa achar o paciente pelo CPF e vê-lo SUMIR da lista.
  ⚠️ **Chip que não muda nada é pior que chip nenhum**: para quem não tem carteira própria
  (sem vínculo, ou a enfermagem) os dois modos mostram a MESMA lista, então eles SOMEM e
  quem explica é a linha de motivo. *(Os dois chips morreram na 3ª rodada, logo abaixo — a
  lição fica porque ela é geral.)*
  ⚠️ **E a porta nova mudou o que a porta VELHA precisava fazer.** Abrir pela carteira
  fixava só id e nome — aceitável enquanto a lista era só dele, porque o caminho normal é
  "Meu dia", que já traz o agendamento. Com a lista alcançando a clínica, ela virou o
  caminho de quem cobre o colega, e sem o vínculo a evolução nasce SOLTA: a sessão fica em
  "Sessões sem evolução" para sempre, mesmo depois de escrita.
  `EntregaDoPaciente.AoPostoAsync` (shell) virou o ponto único que a Enfermagem e a carteira
  compartilham — e as TRÊS portas da linha (Atender, Dor, Avaliações) passam por ele,
  porque as três caem na mesma tela e o rail troca de seção sem trocar de paciente: se só
  uma amarrasse, bastaria entrar por outra para gravar solto.
  A lição que generaliza: **ao ALARGAR o alcance de uma lista, releia o que o clique dela
  faz.** O que era suficiente para um recorte estreito costuma deixar de ser quando o
  recorte cresce — e o que falha não é a porta nova, é a velha.


- **"MEU PACIENTE" NÃO EXISTE — e a resposta certa não era um segundo clique, era apagar o
  recorte** (parcela 88, 3ª rodada; a clínica: *"não existe 'meu paciente', todos atendem
  todos"*). A rodada anterior tinha resolvido o alcance da lista do Consultório com **dois
  chips** (*Meus pacientes* × *Todos os pacientes*), a carteira como padrão. A frase da
  direção derrubou a premissa dos dois: **oferecer a escolha entre "meus" e "todos" numa
  clínica que não distingue as duas coisas é inventar uma decisão** — e a que abre por
  padrão seria justamente a lista MAIS ESTREITA, escondendo o paciente do colega de quem
  foi chamado para cobri-lo.
  O recorte saiu do repositório, não da chamada: `PacientesDoProfissionalAsync(id, …)` →
  `PacientesAtendidosAsync(limite)`, `ConsultorioService.MeusPacientesAsync(profissionalId,
  …)` → `PacientesAsync(termo, limite, comDor)`, record `PacienteDoProfissional` →
  `PacienteDaCarteira`. **Passar `null` no lugar do id não teria bastado**: aquele caminho
  fabricava `Sessoes = 0` e `UltimaSessao = null` para todo mundo, esvaziando as duas
  colunas que são o assunto da tela.
  ⚠️ **A pergunta que decide o que continua recortado não é conceitual, é sobre a natureza
  do dado: PACIENTE não tem dono; HORÁRIO tem.** Por isso "Meu dia", "Minha semana", "Meus
  números" e "Sessões sem evolução" seguem filtrados por profissional — a agenda é fato de
  marcação, e `PodeChamarProximo` depende disso (na lista da clínica inteira o primeiro da
  fila pode ser de outro profissional, e o clique cego anunciaria um nome para a sala do
  colega). A AUTORIA também não se perdeu: quem atendeu está no agendamento, quem escreveu
  assina a evolução, e "Meus números" continua medindo o trabalho de cada um.
  ⚠️ **Sem termo, a lista é quem a clínica JÁ ATENDEU — e isso não é o recorte de volta.**
  É a leitura que a tela existe para dar (sessões, última visita, queda da dor); o cadastro
  inteiro, quem nunca veio inclusive, chega pela BUSCA. E a busca precisou de
  `SessoesDosPacientesAsync` (uma consulta em lote, o MESMO critério do agrupamento da
  lista): sem ela, um paciente de vinte sessões apareceria como "sem sessão registrada" só
  por ter sido achado pela busca — **dado exibido como ausente é a irmã de falha exibida
  como sucesso**.
  ⚠️ **Ao apagar um recorte, releia os RÓTULOS que o nomeavam.** "Meus pacientes" virou
  "Pacientes" no menu e na tela — e isso colidiu com a aba "Todos" da Recepção, deixando
  duas abas irmãs quase homônimas. Elas passaram a dizer a PERGUNTA de cada uma
  ("Cadastro" × "Em tratamento"), que é a diferença que sempre existiu e que o rótulo antigo
  escondia atrás do dono. **A CHAVE não muda junto** (`consultorio-pacientes`): chave é
  contrato de navegação entre módulos, e renomeá-la para acompanhar um rótulo é a regressão
  da parcela 37, 4ª rodada.
  ⚠️ **E o vazio ganhou DUAS perguntas novas no mesmo movimento.** Enquanto a busca filtrava
  a tela, "não há ninguém aqui" bastava; quando ela passou a alcançar o cadastro inteiro,
  responder *"entra aqui quem já foi atendido"* a quem digitou um nome faz concluir que o
  paciente EXISTE e só não foi atendido — quando o sistema está dizendo que ele **não está
  cadastrado**. E é essa leitura errada que leva a cadastrar quem já tem ficha (o CPF
  duplicado da parcela 57). A terceira apareceu na releitura: **quem ESCONDEU responde
  primeiro** — com o "só quem sumiu" marcado sobre uma busca que TROUXE gente, "não achei
  ninguém no cadastro" mentiria sobre a causa e mandaria cadastrar de novo alguém que a tela
  acabou de achar. Um estado vazio por pergunta, como sempre.
  ⚠️ E as três frases foram para a **Application** (`ResumoDaCarteira.Montar`), não para a
  ViewModel: é a regra da `GradeSemana` (parcela 69) e do `ResumoSessaoAnterior` (77) — **o
  que decide o que a tela AFIRMA precisa morar onde o `dotnet test` alcança**. Foi só ao
  escrever o teste que apareceu o exagero da frase do recorte, que afirmava *"vieram nos
  últimos 45 dias"* sobre uma lista que pode conter quem **nunca veio** — a busca alcança a
  primeira consulta, e essa pessoa também não é "sumida". Afirmar uma sessão que não houve,
  numa frase de tela, é a mesma família de garantia aparente que este projeto recusa.


- **O CATÁLOGO ERA UMA CAIXINHA DE 240 px, e o que não cabia nele era o que ele sabe**
  (parcela 88, 4ª rodada — a clínica mandou o print da aba de Cuidados: *"preciso de um pop
  out para que a enfermeira consiga maximizar e ler tudo e todas as opções"*). Os dois
  catálogos da consulta de enfermagem (diagnósticos e cuidados) eram colunas de 240 px com
  teto de 180, **permanentes ao lado de uma lista que nasce VAZIA** — a aba abria com um
  quarto da largura gasto num atalho e o resto em branco, mostrando três itens por vez com o
  título quebrado em três linhas ao lado de um `+`.
  A regra de leiaute do projeto decide sozinha: **escolher do catálogo é o que a enfermeira
  faz uma ou duas vezes por consulta; a prescrição é o que ela VÊ o tempo todo** — botão e
  janela para o primeiro, a tela para o segundo (parcela 37, 3ª rodada).
  ⚠️ **Mas o pior não era o tamanho, era o que não cabia**: o catálogo guarda a FREQUÊNCIA
  SUGERIDA de cada cuidado e o RESULTADO ESPERADO de cada diagnóstico, e **nada disso
  aparecia**. É o defeito recorrente do projeto na variante mais discreta de todas — não é
  dado sem leitor nem capacidade sem porta, é **porta pequena demais para dizer o que ela
  sabe**. Ao encolher um painel para caber ao lado de outra coisa, pergunte o que o dado
  tem que a caixa não mostra.
  ⚠️ **"Já no plano" tem de ser VISÍVEL.** `AdicionarCuidado` recusa o repetido em silêncio,
  e na caixinha o segundo clique simplesmente não fazia nada (o defeito da parcela 41). Na
  janela o botão SOME e no lugar dele fica a marca; e a lista **não é remontada** depois de
  acrescentar (a rolagem voltaria ao topo a cada escolha), só o estado de cada linha é
  corrigido no lugar — todas, porque um diagnóstico traz os cuidados dele junto.
  ⚠️ **UMA janela para os DOIS catálogos**, com o `CatalogoDeEnfermagem` chegando por
  parâmetro: duas seriam duas definições de "escolher do catálogo". E ela recebe o MESMO
  objeto da tela de trás (a regra da parcela 49), então **não tem "Salvar"** — o rodapé diz
  isso em vez de deixar a pessoa supor.
- **O compositor COFEN existia DUAS vezes, e esta seria a primeira divergência** (parcela 88,
  4ª rodada). O bloco das cinco etapas tinha ~300 linhas na janela da sala de infusão e ~300
  na seção do Consultório, que eu escrevi espelhando a primeira na 1ª rodada desta parcela.
  Medido antes de mexer: **47 linhas de diferença em 300, todas cosméticas** (comentários
  requebrados, uma margem, uma largura). O catálogo em janela cairia numa das duas cópias, a
  outra ficaria com a caixinha antiga, e **nada falharia** — build, testes e as três redes
  verdes, com a enfermeira descobrindo pelo app que ela usa.
  Virou `ProcessoDeEnfermagemView`, no shell, pela regra do mapa corporal (parcela 36):
  **quando duas telas precisam do MESMO bloco, ele sobe INTEIRO e não se reescreve.** Sem
  ViewModel próprio — o `DataContext` é o de quem o hospeda —, senão as regras caras (hora
  informada, hora futura recusada, alergia no mesmo `SaveChanges`) existiriam em duas cópias.
  A lição de método: **duplicação que ninguém pagou só cobra na primeira correção de
  verdade** — e a hora de pagá-la é justamente essa, e não "depois", porque depois quer dizer
  com uma das duas já divergente.
\n

- **Tag de tipo da casa SEM PREFIXO: a terceira variante da família, e a que as duas
  checagens não viam** (parcela 88, 4ª rodada — o CI reprovou o PR). Ao extrair o compositor
  da consulta de enfermagem para o shell, escrevi `<ProcessoDeEnfermagemView … />` dentro de
  uma janela que só declarava o prefixo de `Clinica.Desktop.Controls`. Sem prefixo, o WPF
  procura a tag no namespace PADRÃO — o dele — e recusa com **MC3074**.
  As checagens 33 e 33-B cobrem o `xmlns` que EXISTE e está errado (o `;assembly=` que sobra
  e o que falta). Aqui ele **não foi declarado**, e nenhuma das duas olha a TAG.
  ⚠️ Nenhuma rede local pegava, pela razão de sempre nesta família: o XML é bem-formado, o
  `compilar-sombra` **não lê o corpo** do XAML e o C# compila.
  Virou a **checagem 42**, e o critério é estreito de propósito: só reclama de tag sem
  prefixo cujo nome é um tipo que algum `.cs` do repositório DECLARA — tipo do WPF (`Grid`,
  `TabControl`) não está nessa lista e passa. **Medido antes de ligar: ZERO ocorrências em
  todo o repositório**, então ela nasce sem uma linha de ruído. Autotestada contra o caso
  real (verificado revertendo a correção: ela acusa a linha exata) e contra os três
  legítimos — com prefixo, tipo do WPF e a tag citada dentro de um COMENTÁRIO.
  A lição que generaliza, e é a terceira vez que esta família a cobra: **ao escrever uma
  rede para um erro, liste as FORMAS que ele pode tomar, não só a que mordeu.** O `xmlns`
  errado e a tag sem prefixo produzem o MESMO `MC3074`, e eu tinha duas checagens para o
  primeiro e nenhuma para o segundo.
\n

- **"O pop out é da CONSULTA, não do catálogo" — o pedido que eu li um degrau abaixo**
  (parcela 88, 5ª rodada; a clínica: *"ao clicar em «Consulta de enfermagem» ainda não abre
  um pop out como janela, que foi o solicitado"*). O print da rodada anterior mostrava a aba
  de Cuidados, e eu tratei "essa tela" como o CATÁLOGO — que é um degrau abaixo do que ela
  precisa maximizar.
  E o processo nunca coube onde estava: as cinco etapas ficavam empilhadas dentro do
  compositor da passagem, disputando altura com a hora, o texto, os sinais vitais e a linha
  do tempo, numa janela de altura FIXA. Foi essa disputa que a parcela 79 corrigiu tirando o
  `Height="320"` — **o remédio certo para o sintoma errado**. A pergunta que eu não fiz lá:
  *isto cabe nesta tela, ou é outra tela?*
  ⚠️ **`Click`, e NÃO `Checked`, na caixinha que abre janela.** `Checked` dispara também
  quando o ViewModel muda a propriedade por código — e ele muda, ao carregar um registro
  para CORRIGIR: a janela abriria sozinha no meio de uma carga. `Click` só existe quando foi
  a PESSOA que clicou.
  ⚠️ **Ao tirar algo da tela, releia o que o gesto vizinho passou a custar EM SILÊNCIO.**
  Desmarcar "Consulta de enfermagem" sempre descartou as cinco etapas na gravação
  (`ColherProcesso` devolve nulo no modo anotação) — e enquanto as abas estavam na tela, elas
  sumiam na frente da pessoa. Com a consulta em janela, nada muda visualmente e o registro
  vai para o prontuário sem elas. Daí a confirmação, e ela só aparece quando **há o que
  perder**: cobrar confirmação sobre uma consulta em branco treinaria a equipe a confirmar
  sem ler (a causa raiz do incidente da parcela 65).
  ⚠️ **Confirmação em callback de propriedade precisa de guarda contra a carga por CÓDIGO.**
  São três caminhos programáticos aqui (a reversão da própria confirmação, a limpeza depois
  de gravar e o `Corrigir`), e sem o flag a pergunta apareceria no meio de uma carga que
  ninguém pediu — pergunta que aparece sem gesto é pergunta que se fecha sem ler.
  E o **caminho de volta é parte da feature**: fechar a janela deixaria a consulta
  inalcançável sem desmarcar e marcar de novo — e desmarcar é justamente o gesto que devolve
  o registro para anotação. Daí o botão "Abrir a consulta…", e o aviso `EtapasEmFalta`
  ficando na TELA, que é onde ela o lê antes de clicar em Registrar.
- **Checagem que não enxerga dentro de controle da casa acusa o que está certo** (parcela
  88, 5ª rodada). A checagem de rolagem (15/16) varria só `raiz.iter()` do XAML da janela, e
  uma janela cujo miolo é `<comp:ProcessoDeEnfermagemView />` — com um `ScrollViewer` por aba
  lá dentro — era acusada de não ter rolagem nenhuma. **Checagem que reclama do que está
  certo é checagem que alguém desliga**, e aí ela para de pegar o defeito de verdade.
  Ela passou a seguir **UM nível** de composição: o tipo da tag é casado com o `x:Class` dos
  XAML do repositório, e a árvore dele entra na conta. Um nível basta para a composição deste
  projeto; mais níveis dariam um grafo para percorrer sem ganho medido.
  ⚠️ **Alargar uma checagem é o gesto que a deixa cega, então ela ganhou autoteste nos DOIS
  sentidos** — controle da casa COM rolagem conta, controle SEM rolagem não conta —, e foi
  verificado que o autoteste REPROVA quando a varredura é "simplificada" de volta.


- **PROVA DE CAMPO — o que deixou de ser suposição em ago/2026.** Três coisas que este
  arquivo descrevia como "sem prova" passaram a rodar na clínica, e a lista existe para
  ninguém voltar a hedgeá-las:
  **(a) O banco saiu da Neon e está numa VPS da Locaweb, com datacenter no BRASIL**
  (`docs/banco-na-vps.md`, que deixou de ser plano). A consequência que mais pesa não é
  técnica: o ponto 10 do compromisso de conformidade — **transferência internacional do
  art. 33** — deixou de existir, porque o dado passou a residir no país. O suboperador
  agora é um fornecedor nacional, e o mTLS acrescenta uma fechadura que a Neon não
  oferecia (cada máquina cliente prova quem é, com certificado da própria clínica).
  **(b) A assinatura ICP-Brasil foi testada com e-CPF REAL pelo SafeID.** Era a maior
  incógnita do projeto: o `docs/safeid-congelado.md` listava "e-CPF real",
  "`exigirCadeiaConfiavel` com cadeia ICP-Brasil de verdade" e "publicação no S3 com
  documento real" como nunca exercitados, e as rodadas 3 a 8 da parcela 67 corrigiram o
  caminho de nuvem **às cegas**, uma mensagem de erro por vez. Continuam sem prova o
  **carimbo do tempo no caminho de nuvem** (a ACT configurada não é aplicada, e a
  configuração é ignorada em silêncio) e o **LTV/PAdES-LT**, que nunca existiu.
  **(c) Os Workers do Cloudflare estão publicados e funcionando** — o do validador do QR
  (parcela 68, 7ª rodada) e o da coleta remota do termo (parcela 81), que hoje são um
  arquivo só (`tools/worker-clinica.js`, parcela 82).
  ⚠️ **A lição de método é sobre o que fazer com isto, não sobre o que foi provado.** Este
  projeto documenta com cuidado o que ainda não rodou, e isso é certo — mas afirmação de
  "sem prova" tem prazo, como o motivo de uma exclusão de rede (parcela 51). **Quando um
  caminho passa a rodar em produção, o documento que o chamava de incógnita vira uma
  hedge FALSA** — e hedge falsa num documento que vai ao cliente custa a mesma confiança
  que a promessa exagerada. O `entrega-ao-cliente.md` afirmava, meses depois da parcela
  42, que "**não** há certificado ICP-Brasil".
