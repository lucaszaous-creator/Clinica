export interface RangeBarProps {
  /** Origem da receita, forma de pagamento, etc. */
  label: React.ReactNode;
  /** Valor já formatado ("R$ 82.400"). */
  value: React.ReactNode;
  /** 0–1 da barra preenchida. */
  fraction?: number;
  /** var(--serie-1..3). */
  color?: string;
  style?: React.CSSProperties;
}
export declare function RangeBar(props: RangeBarProps): JSX.Element;
