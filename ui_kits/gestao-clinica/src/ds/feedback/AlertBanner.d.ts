export interface AlertBannerProps{
  tone?:'info'|'success'|'warning'|'danger';
  icon?:string;
  title?:React.ReactNode;
  /** Botão/link à direita. */
  action?:React.ReactNode;
  children?:React.ReactNode;
  style?:React.CSSProperties;
}