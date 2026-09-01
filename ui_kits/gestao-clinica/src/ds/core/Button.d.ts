export interface ButtonProps{
  /** primário azul = a ação principal da tela (uma por tela). */
  variant?:'primary'|'secondary'|'ghost'|'danger';
  size?:'md'|'sm';
  /** Ícone lucide à esquerda. */
  icon?:string;
  iconRight?:string;
  disabled?:boolean;
  /** Mostra spinner e bloqueia o clique. */
  loading?:boolean;
  onClick?:()=>void;
  children?:React.ReactNode;
  style?:React.CSSProperties;
}