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
| Marinho para texto e títulos | #071F5C | Cor.Azul.900 |
| Seleção / borda ativa | #EEF3FC / #D8E3F7 | Cor.Azul.50 / 100 |
| Foco | #3F62C9 | Cor.Azul.500 |
| Fundo / superfície / filtros | #F8FAFC / #FFFFFF / #F1F5F9 | Cinza.50 / Superficie / Cinza.100 |
| Bordas / texto secundário | #E5E7EB / #6B7280 | Cinza.200 / 500 |
| Sucesso | #15803D / #F0FDF4 | Verde.700 / 50 |
| Atenção | #875B16 / #FFF6DF | Âmbar já existente em status-celula.css |
| Erro | #B91C1C / #FEE2E2 | Vermelho.700 / 100 |
| Informação | Azul principal / seleção clara | Reutilização da marca, sem ciano |

Verde, vermelho e âmbar identificam somente estados funcionais. Seleção, agenda marcada e andamento usam azul. Entradas e saídas financeiras são diferenciadas por rótulo, sinal e curvas azul/cinza, sem cores decorativas. Sombras são neutras. Sem lavanda, roxo, ciano ou gradientes na apresentação web.
