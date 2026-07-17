# Atalhos de teclado

| Atalho | Ação | Rota |
|---|---|---|
| `Ctrl+N` | Novo atendimento | `NavegarCommand(Secao.Atendimento)` |
| `Ctrl+F` | Focar pesquisa global | code-behind do MainWindow (foco é responsabilidade da View) |
| `Ctrl+S` | Salvar na tela ativa | `AtalhoSalvarCommand` → `IAtalhosDeTela.AtalhoSalvar` |
| `Ctrl+P` | Imprimir/exportar na tela ativa | `AtalhoImprimirCommand` → `IAtalhosDeTela.AtalhoImprimir` |
| `F5` | Atualizar a tela ativa | `AtalhoAtualizarCommand` → `IAtalhosDeTela.AtalhoAtualizar` |
| `Ctrl+B` | Recolher/expandir menu | `AlternarMenuCommand` |
| `Esc` | Cancelar/fechar diálogo | `IsCancel` no botão Cancelar/Fechar |
| `Enter` | Confirmar diálogo | `IsDefault` no botão principal |

## Como funciona

`MainWindow.InputBindings` dispara comandos do `MainViewModel`, que repassa à tela ativa via a interface opcional `ViewModels/IAtalhosDeTela.cs` (propriedades com implementação default `null` — cada VM expõe só o que faz sentido, ex.: `AtalhoSalvar => LancarCommand` no Novo atendimento). Comando nulo ou `CanExecute` falso = atalho sem efeito, sem erro.

Ao criar uma tela nova: implemente `IAtalhosDeTela` mapeando os comandos existentes; em diálogos, marque sempre `IsDefault`/`IsCancel`.
