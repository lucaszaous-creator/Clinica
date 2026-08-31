/**
 * @startingPoint section="Componentes" subtitle="Cartão branco, borda 1px, raio 12" viewport="700x220"
 */
export interface CardProps {
  /** Título 18/600 no topo do cartão. */
  title?: React.ReactNode;
  subtitle?: React.ReactNode;
  /** Seletor de período, kebab, etc., à direita do título. */
  actions?: React.ReactNode;
  /** false remove o padding (tabela encostando na borda). */
  padded?: boolean;
  children?: React.ReactNode;
  style?: React.CSSProperties;
  bodyStyle?: React.CSSProperties;
}
export declare function Card(props: CardProps): JSX.Element;
