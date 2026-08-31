export interface UrgencyDotProps {
  /** Semáforo do domínio (UrgenciaParaCorConverter). */
  level?: 'verde' | 'amarelo' | 'vermelho';
  size?: number;
  /** Mostra o texto do nível ao lado do ponto. */
  label?: boolean;
  style?: React.CSSProperties;
}
export declare function UrgencyDot(props: UrgencyDotProps): JSX.Element;
