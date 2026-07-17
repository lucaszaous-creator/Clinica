# Layout e navegação

## Shell

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
- Telas de tabela: título (H1) → filtros em cartão → cartão com DataGrid ocupando `*`.

## Responsividade e DPI

- Janela: 1280×740 padrão, mínimo 960×560; nunca maior que a área útil do monitor (clamp no `MainWindow.xaml.cs`).
- 1366×768: recolher a sidebar (Ctrl+B) é o modo confortável; colunas `*` absorvem a diferença.
- 2560/4K: conteúdo cresce pelas colunas `*`; cartões de formulário mantêm largura fixa legível.
- Escala do Windows 100–175%: `UseLayoutRounding` + `SnapsToDevicePixels` no Window raiz mantêm bordas 1px nítidas.
