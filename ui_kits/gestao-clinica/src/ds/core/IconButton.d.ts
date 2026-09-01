export interface IconButtonProps{
  icon:string;
  /** Obrigatório: tooltip + nome para leitor de tela. */
  label:string;
  size?:number;
  active?:boolean;
  onClick?:()=>void;
  style?:React.CSSProperties;
}