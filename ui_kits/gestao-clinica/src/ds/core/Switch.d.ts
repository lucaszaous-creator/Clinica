export interface SwitchProps {
  checked?: boolean;
  onChange?: (checked: boolean) => void;
  /** Texto à direita do trilho. */
  label?: React.ReactNode;
  disabled?: boolean;
  style?: React.CSSProperties;
}
export declare function Switch(props: SwitchProps): JSX.Element;
