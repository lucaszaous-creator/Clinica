# Layout e navegação

⚠️ **Há DOIS shells**, e este documento descrevia só um. O do app de faturamento
(`Clinica.Desktop/MainWindow.xaml`, congelado) e o da suíte
(`Clinica.Desktop.Shell/Shell/ShellWindow.xaml`, que serve Recepção, Financeiro e Gerente).
Até a parcela 7 a moldura rica existia só no congelado — sidebar recolhível, busca global e
breadcrumb —, e foi exatamente isso que o cliente notou ao comparar o sistema com os
mockups. As duas agora se equivalem; as diferenças que restam estão anotadas abaixo.

## Shell da SUÍTE (Recepção · Financeiro · Gerente) — parcela 7

```
┌──────────┬──────────────────────────────────────────┐
│ Sidebar  │ Topbar (56px): ☰ · seção › tela · busca  │
│ 248/56px │            · data · avatar + usuário     │
│ agrupada ├──────────────────────────────────────────┤
│ por TEMA │ Conteúdo da tela (margem 24px)           │
└──────────┴──────────────────────────────────────────┘
```

- **Grupos TEMÁTICOS, não por módulo.** `GrupoSidebar` (GESTÃO · PACIENTE · FINANCEIRO ·
  INTELIGÊNCIA) decide onde o item aparece; `ItemMenuModulo.ModuloNome` diz quem constrói a
  tela. São duas coisas que só pareciam uma — antes o cabeçalho era o nome do módulo, e o
  Gerente (que carrega os três) via "Recepção / Financeiro / Direção": uma sidebar que
  explica a arquitetura para quem só quer saber onde mexe no paciente.
- **Recolhível** 248↔56px (Ctrl+B, 150ms). Recolhe sozinha abaixo de 1100px e volta acima
  de 1200 — em 1366×768, o monitor do balcão, a sidebar aberta come a grade da agenda.
  Recolhida, o rótulo vira tooltip: ícone sozinho não diz o que é para quem chegou hoje.
- **Topbar**: botão de recolher, breadcrumb `SEÇÃO › Tela`, busca global (Ctrl+F) como
  paleta de seções, data de hoje e o `Avatar` com o nome de quem está logado.
- **A busca é só de SEÇÕES.** Buscar paciente daqui exigiria o shell saber qual tela de qual
  módulo abre uma ficha, e o shell não conhece tela nenhuma — quem busca paciente é o
  `SeletorPacienteViewModel`, dentro das telas.
- **Sub-abas** (`TabControl`, estilo de `Navegacao.xaml`) quando um item de menu da proposta
  cobre vários assuntos. É o caso de "Faturamento (TISS)" no Gerente: cinco abas sob UM item,
  porque a proposta tem um item ali e quebrar em cinco entradas encheria a sidebar da direção
  com o vocabulário do faturamento.

## Shell do FATURAMENTO (congelado)

```
┌──────────┬──────────────────────────────────────────┐
│ Sidebar  │ Topbar (56px): ☰ · pesquisa · 🔔 ⚙ 👤    │
│ 240/56px ├──────────────────────────────────────────┤
│ módulos  │ Breadcrumb: Módulo › Tela › Detalhe      │
│ agrupados├──────────────────────────────────────────┤
│          │ Conteúdo da tela (margem 24px)           │
│ badge    │                                          │
└──────────┴──────────────────────────────────────────┘
```

- **Sidebar** (`MainWindow.xaml`): branca com borda direita, recolhível 240↔56px (Ctrl+B ou ☰; animação 150ms). Item ativo: fundo azul-suave + barra 3px + texto azul. Recolhida: só ícones com tooltip. Itens vêm de `MainViewModel.Grupos` (coleção `ItemMenu {Secao, Rotulo, Glifo, Grupo}`) — para adicionar uma tela, inclua o item na lista e o caso no `Navegar(Secao)`.
- **Módulos**: Painel (Pendências) · Agenda · Atendimento (Novo atendimento, Consultas) · Faturamento (Consultar guias, Faturados, Glosas, Guias TISS) · Cadastros e ajustes (Pacientes, Relatórios, Parâmetros). Baixa e Ficha do paciente são telas de detalhe (aparecem só no breadcrumb).
- **Topbar**: pesquisa global (command palette de seções — digite, Enter navega no primeiro resultado), sino com `BadgeContador` de pendências, engrenagem → Parâmetros.
- **Breadcrumb**: `BreadcrumbModulo › BreadcrumbTela [› BreadcrumbDetalhe]`, atualizado por `DefinirSecao`/telas de detalhe.

## Grid de página

- Margem externa 24px (`Margem.Pagina`); espaçamento entre cartões 16px; dentro de cartões, escala de 8.
- Formulário + lista: coluna fixa 340–380px à esquerda + `*` para a lista.
- **Painel de apoio** (lista de espera, filtros, resumo — o que acompanha a tela sem ser o
  formulário dela): **320px**, à direita. São duas famílias de largura de propósito: o
  formulário precisa caber rótulo + campo; o painel de apoio precisa tirar o mínimo da
  grade. Antes da auditoria de jul/2026 havia 300, 320, 330 e 360 espalhados pelos três
  módulos, sem critério — o que muda a moldura de lugar quando o usuário troca de tela.
- Telas de tabela: título (H1) → filtros em cartão → cartão com DataGrid ocupando `*`.

## Responsividade e DPI

- Janela: 1280×740 padrão, mínimo 960×560; nunca maior que a área útil do monitor (clamp no `MainWindow.xaml.cs`).

### O ajuste à tela vale para a SUÍTE também (jul/2026)

O clamp acima era só do faturamento. A suíte nasceu sem ele e o resultado estava
declarado no XAML: `ShellWindow` com **760** de altura num monitor de **768** — descontadas
a barra de tarefas e a barra de título, sobram ~696px, então a janela principal nascia com
o rodapé atrás da barra. Três diálogos estavam piores (800, 780 e 700), e onze usavam
`SizeToContent="Height"`, que **cresce com o conteúdo sem limite nenhum**.

O que passou a valer, e é conferido por `tools/verificar-suite.py` (checagens 10 e 11):

- **Altura declarada tem de caber em 1366×768.** Nada acima de ~696px de conteúdo.
  `ShellWindow` foi de 760 para **690**; os diálogos altos, para 660–680.
- **Janela alta ou que cresce com o conteúdo precisa de rolagem.** O padrão é
  `DockPanel` com título no `Top`, botões no `Bottom` e um `ScrollViewer` no miolo: o
  rodapé fica parado e o formulário rola. `ListBox`/`DataGrid` no miolo já contam como
  rolagem (o template deles traz um `ScrollViewer`).
- **Rede de segurança em runtime**: `AjusteJanela.Instalar()`, chamado uma vez no
  `SuiteApp`, registra um handler de CLASSE para `Window.Loaded` e encolhe/recentraliza
  qualquer janela que passe da área útil — inclusive as futuras. Handler de classe, e não
  uma linha no construtor de cada janela, porque **estilo implícito de `Window` não pega
  janela derivada** (o WPF procura o recurso pela chave do tipo concreto, e todas as
  nossas são subclasses) e porque pedir uma linha por janela é pedir que a próxima
  esqueça.
- ⚠️ **Nunca usar `MaxWidth`/`MaxHeight` para isso**: com eles definidos, MAXIMIZAR deixa
  a janela menor que o quadro do Windows e sobram faixas pretas em volta. A nota já
  existia no faturamento; agora está nos dois lugares.
- **Escala do Windows a 150%/175%** é o caso que mais estoura: um diálogo de 600px vira
  900px físicos. É por isso que a rolagem do miolo vale para todos, não só para os altos.
- 1366×768: recolher a sidebar (Ctrl+B) é o modo confortável; colunas `*` absorvem a diferença.
- 2560/4K: conteúdo cresce pelas colunas `*`; cartões de formulário mantêm largura fixa legível.
- Escala do Windows 100–175%: `UseLayoutRounding` + `SnapsToDevicePixels` no Window raiz mantêm bordas 1px nítidas.
