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
| `#DC2626` sobre `#FEE2E2` | 3.95:1 | Badge de erro | ⚠️ |
| `#EA580C` sobre `#FFEDD5` | 3.11:1 | Badge de aviso | ⚠️ |
| `#0EA5E9` sobre `#E0F2FE` | 2.42:1 | Badge de informação | ❌ |

> **Dívida conhecida.** Os três últimos pares não alcançam 4.5:1 no texto de 12px dos
> badges. O padrão "fundo suave + texto forte" só fecha AA quando o texto usa a variante
> **700** da família (é o caso do sucesso, `Verde.700`); erro, aviso e info usam a **600/500**
> e ficam abaixo. A correção é acrescentar `Vermelho.700` (já existe), `Laranja.700`
> (`#C2410C` → 4.52:1) e `Ciano.700` (`#0369A1` → 5.17:1) e apontar os badges para elas.
> Enquanto isso não é feito, nenhum badge carrega informação exclusiva: todos vêm
> acompanhados de rótulo textual.

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
