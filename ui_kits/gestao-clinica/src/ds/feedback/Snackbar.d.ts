export interface SnackbarProps{
  tone?:'sucesso'|'erro'|'info';
  children?:React.ReactNode;
  onClose?:()=>void;
  style?:React.CSSProperties;
}