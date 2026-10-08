# Acessibilidade

## Contraste (WCAG AA)

Razões medidas (WCAG 2.1). AA exige **4.5:1** para texto normal e 3:1 para texto grande
(≥18.66px bold ou ≥24px) e para elementos gráficos.

| Par | Razão | Uso | AA |
|---|---|---|---|
| `#111827` sobre `#FFFFFF` | 17.4:1 | Texto principal | ✅ |
| `#6B7280` sobre `#FFFFFF` | 4.83:1 | Texto secundário | ✅ |
| `#123A9E` sobre `#FFFFFF` | 9.88:1 | Links, texto azul, ícones | ✅ |
| `#FFFFFF` sobre `#123A9E` | 9.88:1 | Botão primário | ✅ |
| `#FFFFFF` sobre `#DC2626` | 4.83:1 | Botão perigo, faixa de alerta | ✅ |
| `#15803D` sobre `#DCFCE7` | 4.57:1 | Badge de sucesso | ✅ |
| `#B91C1C` sobre `#FEE2E2` | 5.30:1 | Badge de erro (`Brush.Erro.Texto`) | ✅ |
| `#C2410C` sobre `#FFEDD5` | 4.52:1 | Badge de aviso (`Brush.Aviso.Texto`) | ✅ |
| `#0369A1` sobre `#E0F2FE` | 5.17:1 | Badge de informação (`Brush.Info.Texto`) | ✅ |

> **Por que existem `Brush.*.Texto`.** O padrão "fundo suave + texto forte" só fecha AA
> quando o texto usa a variante **700** da família. Com o tom 600/500 os badges ficavam em
> 3.95:1 (erro), 3.11:1 (aviso) e 2.42:1 (info) — abaixo dos 4.5:1 exigidos para os 12px do
> badge. Por isso a cor de **fundo/ícone** (`Brush.Aviso`, `Brush.Info`, `Brush.Erro`) é
> separada da cor de **texto sobre o tint** (`Brush.*.Texto`). Sucesso não precisou de token
> novo: `Brush.Sucesso.Forte` já era `Verde.700`.
>
> Regra prática: sobre fundo suave ou branco, texto semântico usa sempre a variante
> `.Texto`. `Brush.Aviso` (3.56:1) e `Brush.Info` (2.77:1) **não servem para texto** nem
> sobre branco — só para preenchimento, tarja e ícone, onde vale o mínimo de 3:1.

Regras:
- Texto pequeno sobre azul/vermelho: sempre branco.
- Cinza `#D1D5DB` e mais claros: nunca para texto — só bordas e superfícies.
- Cor nunca é o único sinal: semáforo acompanha texto/tooltip; badges têm rótulo.

## Teclado e foco

- Todos os controles têm foco visível (anel azul `Brush.Foco` de 2px, fora do controle).
- Navegação completa por Tab; atalhos globais em [atalhos.md](atalhos.md); `Esc`/`Enter` em todos os diálogos.
- Sidebar navegável por Tab (itens são Buttons focáveis).

## Leitores de tela

- Botões só-ícone exigem `AutomationProperties.Name` (e `ToolTip` para todos os usuários).
- Mensagens de erro/estado ficam em texto na tela (não apenas em cor ou snackbar).

## Rolagem sem roda de mouse

As barras dos cinco aplicativos usam setas visíveis com área de clique de 24 × 24 px.
Clique para avançar uma linha ou mantenha pressionado para continuar rolando. O trecho
livre da barra avança uma página; o indicador continua podendo ser arrastado e mantém
pelo menos 24 px em listas longas. O mesmo comportamento vale na horizontal.

As setas têm nomes para leitores de tela e dicas em português. Elas preservam o foco
no campo ou lista, mantendo a navegação de teclado do WPF (setas, Page Up/Page Down).
O estilo compartilhado pelos cinco aplicativos está em
`src/Clinica.Desktop.Shell/Styles/Componentes/Sobreposicao.xaml`.

Campos `TextBox` com `AcceptsReturn="True"` também usam barra vertical automática
quando o texto ultrapassa a altura disponível. O padrão do WPF escondia essa barra;
o gatilho fica no template compartilhado de `Campos.xaml`. Campos de uma linha,
datas e textos de leitura que crescem dentro de uma página mantêm seu comportamento.

Listas precisam ter uma área de rolagem com altura finita: somente mudar o desenho
da `ScrollBar` não resolve um `ItemsControl` medido sem limite. As autorizações do
paciente e a página de regras/apurações de repasse agora têm rolagem. Alertas da
prescrição e da folha de execução têm área rolável de até 160 px; resultados da
pesquisa global têm até 380 px, preservando o restante da tela.

### Verificação reproduzível

Execute no Windows, da raiz do repositório:

```powershell
dotnet run --project tools/validar-rolagem-suite/Qa.csproj
```

O teste não inicia os aplicativos nem acessa banco/configuração. Carrega os recursos
de cada um dos cinco `App.xaml` em WPF real e inventaria todos os XAML de `src`.
Verifica páginas, listas virtualizadas, tabelas, texto multilinha e texto rico:
setas, avanço/retorno, página, limite final, arrasto, tamanho mínimo, nomes acessíveis,
repetição configurada e ocultação automática quando o conteúdo cabe.

Também exercita as telas reais de autorizações e repasses com 85 itens e as regiões
XAML reais de alertas e pesquisa com 85 entradas. Produz `inventario.json` e
`resultado.txt` em `artifacts/qa-rolagem-suite`. A validação das três regiões isoladas
não equivale a navegar todas as combinações de dados de todas as telas; tampouco
simula fisicamente manter o touchpad pressionado (confere a configuração do
`RepeatButton`).
