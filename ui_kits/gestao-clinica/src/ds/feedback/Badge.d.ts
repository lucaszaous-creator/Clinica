export interface BadgeProps {
  /** Pílula de estado: fundo suave + texto forte da mesma família. */
  tone?: 'neutro' | 'sucesso' | 'aviso' | 'erro' | 'info' | 'marca';
  children?: React.ReactNode;
  style?: React.CSSProperties;
}
export declare function Badge(props: BadgeProps): JSX.Element;
