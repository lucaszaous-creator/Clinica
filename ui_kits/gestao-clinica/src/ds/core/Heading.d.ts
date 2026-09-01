export interface HeadingProps{
  /** 1=título de página 24/700 · 2=seção 20/600 · 3=subseção 18/600. */
  level?:1|2|3;
  /** Linha cinza sob o título. */
  subtitle?:React.ReactNode;
  /** Botões à direita (Exportar, + Nova consulta). */
  actions?:React.ReactNode;
  children?:React.ReactNode;
  style?:React.CSSProperties;
}