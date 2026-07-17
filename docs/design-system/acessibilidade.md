# Acessibilidade

## Contraste (WCAG AA)

Pares aprovados sobre branco/superfícies claras:

| Par | Razão | Uso |
|---|---|---|
| `#111827` sobre `#FFFFFF` | ~17:1 | Texto principal |
| `#6B7280` sobre `#FFFFFF` | ~4.6:1 | Texto secundário (≥12px) |
| `#2563EB` sobre `#FFFFFF` | ~4.5:1 | Links, texto azul, ícones |
| `#FFFFFF` sobre `#2563EB` | ~4.5:1 | Botão primário (14px semibold) |
| `#FFFFFF` sobre `#DC2626` | ~4.5:1 | Botão perigo, faixa de alerta |
| `#15803D` sobre `#DCFCE7` | ~4.6:1 | Badge de sucesso |

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
