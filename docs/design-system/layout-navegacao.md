# Layout e navegação

⚠️ **Há DOIS shells**, e este documento descrevia só um. O do app de faturamento
(`Clinica.Desktop/MainWindow.xaml`, congelado) e o da suíte
(`Clinica.Desktop.Shell/Shell/ShellWindow.xaml`, que serve Recepção, Financeiro e Gerente).
Até a parcela 7 a moldura rica existia só no congelado — sidebar recolhível, busca global e
breadcrumb —, e foi exatamente isso que o cliente notou ao comparar o sistema com os
mockups. As duas agora se equivalem; as diferenças que restam estão anotadas abaixo.

## Shell da SUÍTE (Recepção · Consultório · Financeiro · Gerente) — parcela 7, refeito na 55

```
┌────┬───────────┬─────────────────────────────────────┐
│Rail│ Painel da │ Topbar (56px): 📌 · seção › tela ·  │
│56px│ categoria │        busca · data · avatar        │
│    │  250px    ├─────────────────────────────────────┤
│4   │ (espia no │ Conteúdo da tela (margem 24px)      │
│cats│  hover,   │                                     │
│    │ fixa no   │                                     │
│    │  clique)  │                                     │
└────┴───────────┴─────────────────────────────────────┘
```

### Por que deixou de ser uma lista de 248px

A sidebar listava TUDO. No Gerente Geral, que carrega os quatro módulos, isso deu
**46 itens**: a 36px por item mais os cabeçalhos, **1.824px de menu para 610px de tela** —
um terço visível e o resto atrás de uma rolagem sem marca de onde se está. Recolher para
56px (o Ctrl+B de antes) não mostrava um item a mais; só trocava rótulo por ícone na mesma
lista rolante.

Duas mudanças, e nenhuma sozinha resolvia:

1. **Consolidação em sub-abas** — 46 itens viraram **24**. A regra já estava escrita aqui
   desde a parcela 7 e tinha sido aplicada uma vez só, no "Faturamento (TISS)".
2. **Rail + painel de categoria** — a sidebar deixou de listar itens e passou a listar as
   quatro seções; os itens vivem no painel da categoria escolhida.

Com as duas, o maior painel (FINANCEIRO, 8 itens) cabe inteiro numa tela de 768px.

### O rail

- **56px, sempre visível**, com os quatro `GrupoSidebar` (GESTÃO · PACIENTE · FINANCEIRO ·
  INTELIGÊNCIA), cada um com **ícone e nome curto**. O nome curto não é enfeite: o rail é
  o único lugar onde a categoria aparece, e ícone sozinho não diz o que é para quem chegou
  hoje — é a mesma razão que já punha tooltip na sidebar recolhida, só que tooltip exige
  parar o mouse e esperar.
- **Glifo único por categoria e por item visível**, por obrigação. Antes havia **8 glifos
  repetidos** entre os 46 itens (o mesmo desenho em "Resultado do mês", "Taxas e impostos"
  e "Meus números"); numa lista com rótulo isso passa, num rail não.
- **Barra de 3px** na categoria que contém a tela ativa. Sem ela o rail não responde "em
  que parte do sistema eu estou?" — a sidebar antiga respondia pelo item aceso, que agora
  vive dentro de um painel que fecha.

### O painel de categoria

- **Espiar**: passar o mouse abre o painel **flutuando** por cima do conteúdo, depois de
  **180ms**. O atraso existe para atravessar o rail não abrir as quatro categorias em
  sequência.
- **Fixar**: o clique (no ícone, no alfinete do painel ou no Ctrl+B) **ancora** o painel,
  que passa a ocupar coluna e empurrar o conteúdo.
- ⚠️ **O clique que fixa é a metade que torna o modelo utilizável.** Painel que só existe
  enquanto o mouse está em cima é um **alvo móvel**: para ir do ícone até o oitavo item o
  mouse atravessa a borda entre os dois, e num percurso diagonal ele sai da zona por um
  instante. Por isso há **320ms de folga** antes de fechar (o "corredor") — e por isso o
  clique fixa.
- Ancorado, ele **solta sozinho abaixo de 1100px**: 250px de painel em 1366 comem a grade
  da agenda.
- **Esc** fecha; o painel também fecha ao escolher uma tela, a menos que esteja fixado —
  quem fixou quer a lista à mão para ir à próxima.

### Itens compostos (sub-abas)

- `ItemMenuModulo.Abas` é uma lista de `AbaMenu(Rotulo, Chave)`. A aba **não carrega a
  tela, carrega a CHAVE dela**, e quem resolve é o shell. É essa indireção que deixa um
  item compor telas de **módulos diferentes** — "Agenda" junta a agenda do balcão
  (Recepção) com "Minha semana" (Consultório) — sem que um módulo passe a conhecer o outro.
- O host é o `Componentes/TelaComAbas`, com **criação preguiçosa** (a aba só monta a tela
  quando é aberta pela primeira vez; o banco é remoto) e o mesmo estilo de `Abas.xaml`.
- ⚠️ **A tela que virou aba CONTINUA sendo um item de menu.** `NavegacaoSuite.Ir(chave)`
  procura na lista de itens e, sem achar, **devolve false em silêncio** — foi assim que a
  4ª rodada da parcela 37 deixou meia dúzia de botões sem abrir nada, com as três redes
  verdes. O shell resolve a chave de uma sub-tela abrindo **o item pai já na aba certa**,
  então toda navegação que existia continua valendo. A **checagem 28** cobra que a chave
  de cada `AbaMenu` seja item declarado de algum módulo.
- ⚠️ **Quem esconde a sub-tela do menu é o PAI, e só onde o pai existe** — não é uma marca
  nela. "Resultado do mês" e "Produção" são abas de "Relatórios / BI", que a **Direção**
  publica; no `Clinica.Financeiro.exe`, que não carrega a Direção, o pai não existe e as
  duas voltam a ser itens de menu comuns. Sem essa regra, consolidar teria feito telas
  desaparecerem do único app onde alguém as usa todo dia. (`Oculto` continua significando
  outra coisa: a tela que nunca deve aparecer sozinha, como as cinco telas clínicas que só
  existem com paciente escolhido.)
- **Uma aba não é aba, é a tela**: quando sobra uma só, o shell mostra a tela direto, sem
  desenhar uma régua de um rótulo.
- **A abertura vem primeiro no grupo dela.** A ordem dentro do grupo é a de carregamento
  dos módulos, e o dono da abertura é a Direção, que carrega por último — sem essa exceção
  o "Painel" abria o app e aparecia no FIM de GESTÃO.

### Topbar

- Alfinete (fixar/soltar, Ctrl+B), breadcrumb `SEÇÃO › Tela`, busca global (Ctrl+F), data
  de hoje e o `Avatar` com o nome de quem está logado, mais "Trocar usuário".
- **A busca é só de SEÇÕES** — buscar paciente daqui exigiria o shell saber qual tela de
  qual módulo abre uma ficha, e o shell não conhece tela nenhuma; quem busca paciente é o
  `SeletorPacienteViewModel`, dentro das telas.
- ⚠️ **A busca indexa o rótulo das ABAS**, e diz o caminho ("Fechamento de caixa — em
  Caixa"). Sem isso a consolidação trocaria um problema de rolagem por um pior: telas que
  se achavam pelo nome sumiriam da busca. Com o rail, ela é a rota direta de quem já sabe
  o nome da tela.

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
- **Cadastro é BOTÃO, não coluna** (jul/2026): tela de cadastro é uma LISTA com um botão
  ("Novo item", "Nova conta", "Novo preço"), e o formulário abre em janela — o mesmo
  desenho da tela de Pacientes do faturamento. Seis telas tinham o formulário grudado na
  lista (Estoque, Plano de contas, Pacotes, Contas, Taxas/Tributos e Preços por convênio)
  e ele ocupava 360px em TODAS as visitas por uma tarefa que acontece na implantação ou
  quando o contrato muda. Na janela o campo ainda ganha espaço para explicar sua regra —
  base de cálculo, vigência, especialidade —, que espremida na lateral virava rótulo seco.
- **A janela de cadastro segue um molde só**: `ScrollViewer` > `StackPanel Margin="24"`,
  título `DialogoTitulo`, campos, `AlertaPerigo` para a mensagem inline, e o par
  Cancelar (`IsCancel`) + ação primária (`IsDefault`) com
  `ctrl:Ajudantes.EstaCarregando="{Binding Salvando}"`. Quem fecha é o ViewModel, pelo
  evento `Concluido`; a janela não conhece serviço.
- Formulário + lista (quando a tela É o formulário — conferência da gaveta, simulador):
  coluna fixa 340–380px à esquerda + `*` para a lista.
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
