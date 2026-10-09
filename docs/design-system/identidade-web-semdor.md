# Identidade web Clínica SemDor

Orientação obrigatória do proprietário em 08/10/2026. A composição aprovada permanece; esta revisão corrige as cores, sem alterar a logo ou os contratos de ações.

## Fontes inspecionadas

- `src/Clinica.Desktop.Shell/Assets/Marca/logo-cor.png`: logotipo original, preservado sem edição.
- `src/Clinica.Desktop.Shell/Styles/Tokens.xaml`: fonte existente para azul da marca e neutros.
- `docs/design-system/tokens.md`: semântica dos tokens legados.
- Estilos e componentes React dos dois frontends e imagens enviadas pelo proprietário.

## Tokens compartilhados

`src/Clinica.Desktop.Shell/Web/frontend/src/cores-semdor.css` é carregado por último por ambos os frontends. O tema Mantine usa somente degraus de azul existentes no XAML, sem interpolação.

| Papel | Valor existente | Origem |
|---|---|---|
| Marca / hover | #123A9E / #0A2E86 | Cor.Azul.600 / 700 |
| Títulos / texto principal / rótulos | #071F5C / #111827 / #374151 | Azul.900 / Cinza.900 / Cinza.700 |
| Seleção / borda ativa | #EEF3FC / #D8E3F7 | Cor.Azul.50 / 100 |
| Foco | #3F62C9 | Cor.Azul.500 |
| Fundo / superfície / filtros | #F8FAFC / #FFFFFF / #F1F5F9 | Cinza.50 / Superficie / Cinza.100 |
| Bordas / texto secundário | #E5E7EB / #6B7280 | Cinza.200 / 500 |
| Contorno de controles / hover | #6B7280 / #374151 | Cinza.500 / Cinza.700 |
| Sucesso | #15803D / #F0FDF4 | Verde.700 / 50 |
| Atenção | #875B16 / #FFF6DF | Âmbar já existente em status-celula.css |
| Erro | #B91C1C / #FEE2E2 | Vermelho.700 / 100 |
| Informação | Azul principal / seleção clara | Reutilização da marca, sem ciano |

Verde, vermelho e âmbar identificam somente estados funcionais. Seleção, agenda marcada e andamento usam azul. Entradas e saídas financeiras são diferenciadas por rótulo, sinal e curvas azul/cinza, sem cores decorativas. Sombras são neutras. Sem lavanda, roxo, ciano ou gradientes na apresentação web.

## Legibilidade de campos e ações

A cliente relatou que campos claros pareciam invisíveis. A revisão de 08/10/2026 preserva o fundo Cinza.100 e as superfícies brancas, mas usa contorno Cinza.500 em repouso, com contraste de 4,83:1 contra branco. Placeholders usam o mesmo cinza sem redução de opacidade; conteúdo usa Cinza.900 e rótulos usam Cinza.700. Selects incluem seta azul. Campos desabilitados continuam legíveis, com fundo cinza e borda tracejada; erros mantêm vermelho e foco mantém anel azul.

Confirmação, criação principal e salvar usam azul preenchido. Ações auxiliares nas barras de página, seção e diálogo usam texto e ícones azuis, alinhados sem cápsulas preenchidas; em listas, o contorno azul identifica o controle. O proprietário aprovou essa hierarquia no atendimento e pediu sua aplicação às demais telas. Não preencher todas as ações de azul com o mesmo peso. Filtros ativos usam azul sólido, texto branco e marca de seleção; linhas selecionadas têm marca lateral. KPIs usam números e rótulos escuros, preservando cores funcionais de situação. Buscas, editor rico e seletor de mês seguem o contraste dos campos compartilhados.

O proprietário rejeitou o destaque arredondado de “GESTÃO”: a navegação superior é plana, sem cápsula ou fundo pastel. A seção ativa usa texto azul e linha inferior reta de 2 px. Submenus mantêm marca lateral; foco de teclado continua visível. A cor acompanha rótulos e estados acessíveis, nunca os substitui.


## Busca e escolha de pacientes

Usar a composição compartilhada de busca ampla e resultados em linhas. Nome selecionado fica explícito, inclusive durante outra consulta; trocar requer escolher um resultado. Evitar dropdown comprimido e listas duplicadas. Preservar documentos e ações quando a tela já utiliza uma tabela de pacientes. Carregamento acompanha o estado confirmado pelo host. Detalhes e evidências em [buscas-pacientes-pr245.md](buscas-pacientes-pr245.md).
