export interface RadioProps {
  options?: Array<string | { value: string; label: string }>;
  value?: string;
  onChange?: (value: string) => void;
  /** Nome do grupo no DOM. */
  name?: string;
  /** Dispõe as opções em linha. */
  row?: boolean;
  style?: React.CSSProperties;
}
export declare function Radio(props: RadioProps): JSX.Element;
