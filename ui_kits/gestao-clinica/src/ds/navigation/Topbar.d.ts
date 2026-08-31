export interface TopbarProps {
  /** Normalmente um <Breadcrumb />. */
  left?: React.ReactNode;
  searchPlaceholder?: string;
  onSearch?: (value: string) => void;
  /** Contador no sino. */
  notifications?: number;
  /** Nome de quem está logado (vira iniciais no avatar). */
  user?: string;
  style?: React.CSSProperties;
}
export declare function Topbar(props: TopbarProps): JSX.Element;
