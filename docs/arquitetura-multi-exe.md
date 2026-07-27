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
Clinica.Modulo.Faturamento    (lib)  o que hoje vive em Clinica.Desktop (por último)

Clinica.Recepcao.exe    → Shell + Recepcao
Clinica.Financeiro.exe  → Shell + Financeiro
Clinica.Gerente.exe     → Shell + TODOS os módulos
Clinica.Desktop.exe     → intocado por enquanto
```

Cada executável fica com poucas dezenas de linhas: registra os módulos desejados
e sobe o shell. **O Gerente Geral sai de graça** — é apenas a casca que referencia
todos os módulos. E os módulos continuam vendáveis separadamente.

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

## Ordem de trabalho

A sequência é deliberada: o faturamento é o **último** a mudar.

| Fase | Entrega | Toca o faturamento? |
|---|---|---|
| 1 | `Clinica.Desktop.Shell` + `Clinica.Recepcao.exe` com a fila do dia | Não |
| 2 | `Clinica.Financeiro.exe` com a Produção do período | Não |
| 3 | `Clinica.Gerente.exe` (casca com todos os módulos) | Não |
| 4 | Faturamento migra para módulo e passa a usar o shell | Sim, mecânico |

Extrair a abstração **depois** de ter dois ou três consumidores reais produz um
shell muito melhor do que tentar adivinhá-lo agora. Se a Fase 4 nunca acontecer,
o sistema continua funcionando — apenas com duas cópias do design system.

### Débito assumido conscientemente

Na Fase 1 o shell nasce com uma **cópia** do design system e dos controles
(`Styles/`, `Controls/`). São dois lugares para mudar um token de cor até a Fase 4,
quando as cópias originais de `Clinica.Desktop` são removidas.

Para que a cópia do XAML não precise de edição, as classes de controle no shell
mantêm o namespace original `Clinica.Desktop.Controls` — é o que os dicionários
de estilo referenciam (`Botoes.xaml`, `Campos.xaml`, `Feedback.xaml`).

## Decisões operacionais

### Empacotamento e auto-update

O `release.yml` hoje é fixo em um app só:

```
--packId Clinica.Faturamento --mainExe Clinica.Desktop.exe
```

Vira uma matriz de quatro pacotes, cada um com seu `packId` e seu canal de
atualização. **Detalhe crítico: não mudar o `packId` do faturamento** — as
instalações existentes perdem o canal de auto-update e param de atualizar.

| App | packId | mainExe |
|---|---|---|
| Faturamento | `Clinica.Faturamento` (não mudar) | `Clinica.Desktop.exe` |
| Recepção | `Clinica.Recepcao` | `Clinica.Recepcao.exe` |
| Financeiro | `Clinica.Financeiro` | `Clinica.Financeiro.exe` |
| Gerente Geral | `Clinica.Gerente` | `Clinica.Gerente.exe` |

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

### Validação

`Clinica.Desktop.Shell` e `Clinica.Recepcao` são `net8.0-windows` com WPF —
**não compilam em Linux**. A verificação é o CI no runner Windows
(`.github/workflows/build-exe.yml`), que passou a compilar e publicar Recepção e
Financeiro além do faturamento.

Atenção: o workflow só dispara em `push` na `main` ou em pull request para a
`main` — commits na branch de trabalho não geram build sozinhos.

Antes de subir, os projetos novos passam por uma verificação estática local
(XAML bem-formado, chaves de `StaticResource` existentes no design system e
bindings casando com os membros gerados pelo MVVM Toolkit), que pega a maior
parte dos erros sem precisar do Windows.
