# Arquitetura multi-exe — Recepção, Financeiro e Gerente Geral

> Como sair de **um** executável (Faturamento) para **quatro**, sem duplicar código
> e sem encostar no que já está em produção.

## O ponto de partida

Hoje existe um único app WPF (`Clinica.Desktop`) sobre três bibliotecas compartilhadas:

```
Clinica.Domain          entidades, enums, motor de regras por convênio
Clinica.Application     serviços de caso de uso (IClinicaRepositorio)
Clinica.Infrastructure  EF Core + Npgsql, ClinicaDbContext, migrations
Clinica.Desktop         WPF/MVVM — o .exe de faturamento
```

**Toda a lógica de negócio já é reaproveitável.** Um executável novo precisa apenas
referenciar as três bibliotecas e chamar `services.AddClinica(connectionString)`
(`src/Clinica.Infrastructure/DependencyInjection.cs:14`) para receber repositório,
regras e serviços prontos — sem alterar uma linha do faturamento.

## Como os exes se comunicam

Eles **não** conversam entre si. Conversam com o mesmo PostgreSQL.
Não há necessidade de IPC, fila ou API entre os apps.

Três consequências práticas:

| Assunto | Situação |
|---|---|
| Escrita simultânea | **Resolvido.** Concorrência otimista por `xmin`; `ClinicaRepositorio.SalvarAsync` traduz o conflito em mensagem amigável. |
| Atualização em tempo real | **Não temos.** Um app não vê a alteração do outro até atualizar a tela. |
| Migrations concorrentes | **Risco.** Detalhado abaixo. |

### Tempo real (quando incomodar)

Ordem de preferência, do mais simples ao mais sofisticado:

1. Recarregar ao focar a janela + timer de fundo — atende a clínica.
2. `LISTEN/NOTIFY` do PostgreSQL (suportado nativamente pelo Npgsql): quem grava
   emite `NOTIFY clinica_evento`, quem está aberto escuta e atualiza.

Começar pelo item 1. O item 2 só quando houver dor real.

### Migrations — o maior risco para o que já está pronto

Hoje todo app aplica `MigrateAsync` na abertura (`src/Clinica.Desktop/App.xaml.cs:78`).
Com quatro executáveis instalados isso cria dois problemas:

1. **Corrida na subida** — dois apps abrindo às 8h aplicam migration ao mesmo tempo.
   *Mitigação:* envolver o `MigrateAsync` num `pg_advisory_lock`, serializando a subida.
2. **Versões diferentes convivendo** — o Recepção v2 aplica uma migration que o
   Faturamento v1 instalado desconhece. Coluna nova o EF ignora sem problema;
   **renomear ou remover** algo que o faturamento usa derruba a clínica.
   *Regra:* enquanto houver versões diferentes em campo, **migration só aditiva**.

## Arquitetura alvo: o exe vira uma casca fina

O erro a evitar é fazer cada exe ser um projeto WPF completo — o Gerente Geral,
que "engloba tudo", viraria uma **cópia** das telas dos outros três, e toda
correção passaria a ser feita em dois lugares.

A solução é inverter: **o executável não é dono das telas, ele carrega módulos.**

```
Clinica.Desktop.Shell         (lib)  design system, sidebar, snackbar,
                                     ConexaoStore, janela genérica, bootstrap
Clinica.Modulo.Recepcao       (lib)  Views + ViewModels da recepção
Clinica.Modulo.Financeiro     (lib)
Clinica.Modulo.Gerente        (lib)  BI, campanhas, acessos e a leitura do faturamento
Clinica.Recepcao.exe    → Shell + Recepcao
Clinica.Financeiro.exe  → Shell + Financeiro
Clinica.Gerente.exe     → Shell + TODOS os módulos
Clinica.Desktop.exe     → o faturamento, intocado (nunca vira módulo — ver Fase 4)
```

Cada executável fica com poucas dezenas de linhas: registra os módulos desejados
e sobe o shell. **O Gerente Geral sai de graça** — é apenas a casca que referencia
todos os módulos. E os módulos continuam vendáveis separadamente.

Desde a Fase 3 é exatamente esse o desenho no disco: o `App.xaml.cs` de cada exe
tem uma lista de módulos e nada mais — a sequência da abertura (log → conexão →
host → migrations → janela) mora uma vez só, em `ShellBootstrap`/`SuiteApp`.

### O contrato de módulo

O que hoje impede essa composição é o `MainViewModel`: a lista de seções está
cravada no código (`src/Clinica.Desktop/ViewModels/MainViewModel.cs:66`) e a
navegação é um `switch` sobre o enum `Secao` (`:111`).

No shell isso vira um registro: cada módulo declara seus itens de menu e sabe
construir a tela de cada item.

```csharp
public interface IModuloApp
{
    string Nome { get; }                                   // grupo na sidebar
    IReadOnlyList<ItemMenuModulo> Itens { get; }           // itens do menu
    void Registrar(IServiceCollection servicos);           // DI do módulo
    object? CriarTela(string chave, IServiceProvider sp);  // item -> View
}
```

O shell monta a sidebar a partir de todos os módulos registrados, na ordem em que
foram informados. Nenhum módulo conhece os outros.

> Para a distribuição das features vendidas na proposta pelos quatro módulos — com o
> estado de cada uma e a ordem das parcelas —, veja
> [`features-por-modulo.md`](features-por-modulo.md) e
> [`entrega-ao-cliente.md`](entrega-ao-cliente.md).

## Ordem de trabalho

A sequência é deliberada: o faturamento é o **último** a mudar.

| Fase | Entrega | Toca o faturamento? | Situação |
|---|---|---|---|
| 1 | `Clinica.Desktop.Shell` + `Clinica.Recepcao.exe` com a fila do dia | Não | ✅ feita |
| 2 | `Clinica.Financeiro.exe` com Caixa, Conciliação e Produção | Não | ✅ feita |
| 3 | Módulos viram bibliotecas + `Clinica.Gerente.exe` + empacotamento dos quatro | Não | ✅ feita |
| 4 | Faturamento migra para módulo e passa a usar o shell | Sim, mecânico | ❌ **cancelada** (abaixo) |

Extrair a abstração **depois** de ter dois ou três consumidores reais produz um
shell muito melhor do que tentar adivinhá-lo agora. E a Fase 4 de fato não vai
acontecer: o sistema continua funcionando — apenas com duas cópias do design system.

### Débito assumido conscientemente

Na Fase 1 o shell nasceu com uma **cópia** do design system e dos controles
(`Styles/`, `Controls/`). Com a Fase 4 cancelada, essa cópia é **permanente**: são dois
lugares para mudar um token de cor, para sempre. O mesmo vale para `LogSuite` (shell) e
`LogErros` (faturamento): mesma política de log, duas implementações.

O que segura a divergência é o CI — `tokens/verificar-espelho.py` reprova cor fora de
sincronia entre o CSS da marca e o `Tokens.xaml`.

Para que a cópia do XAML não precise de edição, as classes de controle no shell
mantêm o namespace original `Clinica.Desktop.Controls` — é o que os dicionários
de estilo referenciam (`Botoes.xaml`, `Campos.xaml`, `Feedback.xaml`).

Dentro da suíte, porém, a cópia acabou: os módulos são bibliotecas e o design
system tem **uma** porta de entrada (`Styles/Suite.xaml`), que cada `App.xaml`
mescla numa linha. Acrescentar um dicionário de componente é uma edição, não três.

## Decisões operacionais

### Empacotamento e auto-update

O `release.yml` era fixo em um app só. Desde a Fase 3 são quatro pacotes, cada um
com seu `packId` e seu **canal**. **Detalhe crítico: não mudar o `packId` nem o
canal do faturamento** — as instalações existentes perdem o canal de auto-update
e param de atualizar. Por isso o faturamento continua no canal padrão (`win`) e
os apps novos ganharam canais próprios.

| App | packId | mainExe | canal |
|---|---|---|---|
| Faturamento | `Clinica.Faturamento` (não mudar) | `Clinica.Desktop.exe` | `win` (padrão, não mudar) |
| Recepção | `Clinica.Recepcao` | `Clinica.Recepcao.exe` | `recepcao` |
| Financeiro | `Clinica.Financeiro` | `Clinica.Financeiro.exe` | `financeiro` |
| Gerente Geral | `Clinica.Gerente` | `Clinica.Gerente.exe` | `gerente` |

Os quatro entram na **mesma release** (`vX.Y.Z`): o Velopack grava um
`releases.<canal>.json` por canal, e o app instalado só enxerga o do canal com que
foi empacotado — é o canal, não a release, que separa um app do outro. O
`release.yml` reflete isso em dois jobs: o do faturamento **cria** a release
(comandos inalterados), e o da suíte entra depois com `--merge`, um app por vez
(`max-parallel: 1`, pois os três escrevem na mesma release). Se o job da suíte
falhar, a release do faturamento já saiu inteira.

O cliente de update dos apps novos é o `AtualizadorSuite` (shell): checa na
abertura, baixa e reinicia já atualizado, com limite de 30s para rede lenta nunca
travar a abertura. O ciclo periódico de 2h com aviso no snackbar continua só no
faturamento, que tem onde mostrá-lo. Como a Fase 4 foi cancelada, seguem separados.

### Connection string única

Hoje a conexão fica criptografada (DPAPI) em `%APPDATA%\ClinicaFaturamento`.
Se cada app usar sua própria pasta, a clínica configura a conexão quatro vezes.

Solução sem tocar no faturamento: os apps novos gravam em
`%APPDATA%\ClinicaSemDor` **e leem a pasta do faturamento como fallback**.
Se o faturamento já está instalado na máquina, os demais simplesmente funcionam.

### Perfis dentro do Gerente Geral

Executáveis separados resolvem "quem instala o quê", não "quem pode ver o quê".
Como o Gerente Geral engloba tudo, ele é o candidato natural a receber perfis de
acesso quando o módulo de permissões existir. Fora do escopo das Fases 1–4.

## Fase 1 — o que foi construído

- **`src/Clinica.Desktop.Shell`** — biblioteca com o contrato de módulos
  (`IModuloApp`), a janela genérica (`ShellWindow`), a navegação (`ShellViewModel`),
  o bootstrap reutilizável (`ShellBootstrap`: conexão → host → migrations → janela)
  e a cópia do design system.
- **`src/Clinica.Recepcao`** — executável da recepção com a tela **Fila de hoje**:
  lista os agendamentos do dia e permite confirmar presença, cancelar e marcar falta.

A primeira tela foi escolhida de propósito: ela usa apenas APIs que já existem
(`AgendaService.DoDiaAsync`, `ConfirmarPresencaAsync`, `CancelarAsync`,
`MarcarFaltaAsync`). **Nenhuma migration, nenhuma mudança de domínio** — o modelo
multi-exe é provado de ponta a ponta com risco zero para o faturamento.

`ConfirmarPresencaAsync` já faz o check-in completo: gera o atendimento com os
códigos e, havendo 2º código, cria o retorno sugerido para não esquecê-lo.
A recepção alimenta o faturamento sem que ninguém redigite nada.

### Limitação conhecida da Fase 1

A Recepção ainda **não tem tela de setup própria**: ela obtém a conexão da variável
`ConnectionStrings__Clinica` ou da configuração já feita pelo Faturamento na mesma
máquina. Se não encontrar nenhuma, orienta a abrir o Faturamento e configurar uma vez.
A tela de setup própria entra quando a Recepção passar a ser instalada sozinha.

## Fase 2 — o que foi construído

- **`src/Clinica.Financeiro`** — segundo executável sobre o shell, com três telas:
  **Caixa** (entradas, saídas, saldo realizado e previsto do mês), **Conciliação**
  (guias efetivadas sem receita lançada) e **Produção** (volume de códigos por mês).

O `App.xaml.cs` do Financeiro é idêntico ao da Recepção a menos da lista de
módulos — que é exatamente o resultado esperado da arquitetura de casca fina.
(Na Fase 3 essa igualdade deixou de ser cópia: virou o `SuiteApp`.)

### Financeiro e faturamento conversando

A decisão tomada foi criar **entidades monetárias novas e separadas**, sem tocar nas
entidades de faturamento. O desenho que sustenta isso:

- O dinheiro vive **apenas** em `LancamentoFinanceiro`. `CodigoFaturamento` e
  `Atendimento` continuam sem nenhum campo de valor, como manda o `CLAUDE.md`.
- As chaves estrangeiras apontam **do financeiro para o faturamento**
  (`CodigoFaturamentoId`, `AtendimentoId`, `PacienteId`), todas opcionais — uma
  despesa de aluguel não tem guia nem paciente.
- A dependência tem **um sentido só**: o faturamento segue funcionando sem saber
  que o financeiro existe. Isso é o que permite evoluir o financeiro à vontade
  sem risco para o módulo que já está em produção.

A migration é **puramente aditiva** (cria `CategoriasFinanceiras` e `Lancamentos`;
não altera nenhuma tabela existente), respeitando a regra de convivência entre
versões diferentes instaladas.

#### A conciliação

É onde os dois módulos se encontram. `FinanceiroService.GuiasSemLancamentoAsync`
pergunta ao faturamento quais guias já foram efetivadas no convênio e cruza com os
lançamentos existentes; o que sobra é receita que ainda não entrou no caixa.
Ao lançar, o vínculo com a guia fica gravado e ela sai da lista.

Lançamento cancelado **não** conta como conciliado — a guia volta a cobrar
lançamento, para que um estorno não esconda receita.

#### Modelo financeiro

| Entidade | Papel |
|---|---|
| `LancamentoFinanceiro` | Entrada ou saída, com status Previsto/Realizado/Cancelado |
| `CategoriaFinanceira` | Plano de contas editável pela clínica |

Regras já cobertas por teste: valor sempre positivo (o sinal vem do tipo), data de
pagamento automática ao realizar, cancelamento exigindo motivo e preservando
histórico, e auditoria no mesmo `SaveChanges` da ação.

#### Próximos passos do financeiro

- Formulário de novo lançamento e edição na UI (o serviço já suporta).
- Repasse por profissional — depende de uma entidade `Profissional`, que ainda não
  existe (hoje há um prestador único em `ParametrosService`).
- Contas a pagar/receber recorrentes e centro de custo.

## Fase 3 — o que foi construído

A fase que a arquitetura vinha prometendo: **o Gerente Geral sem uma tela própria.**

- **`src/Clinica.Modulo.Recepcao`** e **`src/Clinica.Modulo.Financeiro`** — os
  módulos saíram de dentro dos executáveis e viraram **bibliotecas**. Só o assembly
  mudou de nome; os namespaces continuam `Clinica.Recepcao.*` e `Clinica.Financeiro.*`.
- **`src/Clinica.Gerente`** — terceiro executável, com os dois módulos na lista.
  Sem View, sem ViewModel, sem cópia: ganha as telas dos outros dois de graça, e
  ganha as do faturamento por telas próprias de leitura (ver Fase 4 — cancelada).
- **`Clinica.Desktop.Shell/Shell/SuiteApp.cs`** — a abertura de um app da suíte
  (log → rede de segurança de exceções → conexão → host → migrations → janela),
  agora num lugar só. O `App.xaml.cs` de cada exe ficou com a lista de módulos e
  mais nada.
- **`Clinica.Desktop.Shell/Styles/Suite.xaml`** — porta única do design system.
  Cada `App.xaml` mescla um dicionário em vez de dez.
- **`Clinica.Desktop.Shell/Configuracao/LogSuite.cs`** — os apps da suíte passaram
  a deixar rastro em arquivo, como manda o `CLAUDE.md`: mesma política do
  faturamento (um `.txt` por mês na raiz da instalação, rotação em 2 MB, expurgo
  em 90 dias) e ligado ao `Diagnostico` para as camadas sem UI.
- **`Clinica.Desktop.Shell/Shell/AtualizadorSuite.cs`** + matriz no `release.yml` —
  os três apps novos agora são instaláveis e se atualizam sozinhos, cada um no seu
  canal (ver "Empacotamento e auto-update").

Por que extrair os módulos em vez de o Gerente referenciar os dois `.exe`: um exe
referenciando outro exe funciona, mas cristaliza a inversão que esta arquitetura
existe para evitar — o dono das telas passaria a ser o executável. Com biblioteca,
"quem instala o quê" volta a ser só a lista de `ProjectReference` de cada casca.

### O que a Fase 3 NÃO fez

O faturamento continua intocado, então o Gerente Geral ainda **não** tem as telas de
faturamento — ele reúne Recepção e Financeiro. Ganhá-las é trabalho da parcela 5
(`entrega-ao-cliente.md`), por telas próprias de leitura.

## Fase 4 — cancelada

**O faturamento está em produção e não se encosta nele.** Migrá-lo para módulo é, por
definição, encostar — mover todas as telas, trocar a navegação e reempacotar o app que
fatura a clínica hoje, sem nenhum ganho para quem opera.

A decisão foi tomada com o produto já vendido: o que importa agora é entregar as features
da proposta nos outros três módulos, não reorganizar o que funciona.

### O que isso custa, e por que está tudo bem

O débito assumido na Fase 1 vira **permanente**:

- duas cópias do design system (`Clinica.Desktop/Styles` e `Clinica.Desktop.Shell/Styles`);
- duas implementações da mesma política de log (`LogErros` e `LogSuite`).

Mudar um token de cor passa a ser duas edições, para sempre. É barato perto de mexer no app
que fatura — e o espelho de tokens (`tokens/verificar-espelho.py`) já trava divergência de
cor no CI.

### E o Gerente Geral, que "reflete tudo"?

Sem a Fase 4 ele não herda as telas do faturamento. A saída é melhor que a original: o
Gerente ganha **telas próprias de leitura** construídas sobre os mesmos serviços
compartilhados que o faturamento usa — `PendenciaService`, `RelatorioService`,
`LoteTissService`. Ele enxerga tudo sem tocar em uma linha de `Clinica.Desktop`, e como é
só leitura, não há risco de escrita concorrente com o faturista.

Isso também mantém honesta a promessa da arquitetura: o executável não é dono das telas.
O Gerente continua sendo uma casca — só que com um módulo de leitura a mais.

**Na parcela 5 esse módulo foi construído**: `src/Clinica.Modulo.Gerente`, com Indicadores
(BI), Faturamento (leitura sobre `RelatorioService`/`PendenciaService`), Campanhas e
Acessos. É o único módulo carregado por um executável só — as telas da direção não têm
por que viajar dentro do instalador da Recepção.

### Login e permissão no shell (parcela 5)

O shell ganhou três peças pequenas e uma regra:

- `SessaoUsuario` — singleton por processo com a fotografia do usuário que entrou (id,
  nome, perfil, permissões efetivas). É dela que as telas tiram o "operador" gravado na
  auditoria, em vez de cada uma inventar um nome.
- `LoginWindow` — entra depois das migrations e antes da `ShellWindow`. Base sem usuário
  abre o **primeiro acesso** (nasce Gerente): a versão que introduz login não pode
  trancar a clínica do lado de fora.
- `ItemMenuModulo.Requer` — permissão exigida por item de menu, com padrão
  `Permissao.Nenhuma` (= sempre visível). Módulo que não declarar nada continua se
  comportando como antes; o `ShellViewModel` filtra os itens e some com o grupo que
  ficar vazio, para não sobrar cabeçalho órfão na sidebar.

O **faturamento não passa por nada disso** — continua sem login, como todo o resto dele.

> A distribuição das features da proposta pelos quatro módulos, com o estado de cada uma,
> está em [`features-por-modulo.md`](features-por-modulo.md); como o cliente recebe os
> apps, em [`entrega-ao-cliente.md`](entrega-ao-cliente.md).

### Validação

`Clinica.Desktop.Shell`, os módulos e os três executáveis são `net8.0-windows` com
WPF — **não compilam em Linux**. A verificação é o CI no runner Windows
(`.github/workflows/build-exe.yml`), que compila e publica Recepção, Financeiro e
Gerente Geral além do faturamento.

Atenção: o workflow só dispara em `push` na `main` ou em pull request para a
`main` — commits na branch de trabalho não geram build sozinhos.

Antes de subir, a suíte passa por uma verificação estática que roda em qualquer
sistema — **`python3 tools/verificar-suite.py`**, também no CI:

| Confere | Pega |
|---|---|
| XAML bem-formado | tag não fechada, atributo duplicado |
| `{StaticResource X}` com `x:Key="X"` no design system | token renomeado, dicionário esquecido no `Suite.xaml` |
| pack URI `...;component/Caminho.xaml` existente | arquivo movido de pasta |
| `x:Class` com code-behind declarando a `partial class` | View sem `.xaml.cs`, classe renomeada pela metade |
| `ProjectReference` existente e projeto no `Clinica.sln` | projeto novo que o CI não compilaria |
| propriedade como atributo **e** como elemento (`Style="…"` + `<X.Style>`) | MC3024 — ver `docs/design-system/armadilhas-xaml.md` |
| `x:Key` repetido no mesmo dicionário | recurso sobrescrevendo outro |
| evento (`Click="Foo"`) com método no code-behind | handler renomeado só de um lado |
| `Application` usado sem qualificar | a armadilha CS0118 abaixo |

O script cresceu a cada erro que o CI pegou: cada regra acima existe porque um build
no Windows falhou por ela. Ao encontrar um erro novo de compilação da suíte, o
reflexo certo é acrescentar a regra aqui, não só corrigir o arquivo.

**Armadilha `Application` (CS0118).** Dentro de qualquer namespace `Clinica.*`, o
nome `Application` resolve para o **namespace** `Clinica.Application` — nunca para o
tipo `System.Windows.Application`. Ou seja: `public partial class App : Application`,
que é como todo projeto WPF do mundo escreve, **não compila neste repositório**. Use
sempre `System.Windows.Application` (é o que o faturamento já fazia). O erro só
aparece no Windows, exatamente onde não dá para compilar antes de subir — por isso
virou uma regra do `verificar-suite.py`.

Não substitui o build no Windows — substitui a parte dele que dá para conferir sem
Windows, que é onde estava a maioria dos erros.
