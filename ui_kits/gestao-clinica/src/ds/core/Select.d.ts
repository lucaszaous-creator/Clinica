export interface SelectProps {
  /** Strings simples ou {value,label}. */
  options?: Array<string | { value: string; label: string }>;
  value?: string;
  onChange?: (value: string) => void;
  /** Pílula compacta — seletor de período no canto do cartão ("Mensal", "2026"). */
  pill?: boolean;
  style?: React.CSSProperties;
}
export declare function Select(props: SelectProps): JSX.Element;
