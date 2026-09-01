export interface InputProps {
  value?: string;
  onChange?: (value: string) => void;
  placeholder?: string;
  /** Textarea em vez de input de uma linha. */
  multiline?: boolean;
  rows?: number;
  /** Fonte monoespaçada — campos técnicos (connection string). */
  mono?: boolean;
  /** Borda vermelha para erro de validação (a mensagem vai inline, perto da ação). */
  invalid?: boolean;
  style?: React.CSSProperties;
}
export declare function Input(props: InputProps): JSX.Element;
