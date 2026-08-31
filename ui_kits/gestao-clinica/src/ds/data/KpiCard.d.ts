export interface KpiCardProps{
  label:string;
  /** Ícone lucide pequeno antes do rótulo. */
  icon?:string;
  value:React.ReactNode;
  suffix?:string;
  /** Variação % vs. período anterior; negativo fica vermelho. */
  delta?:number;
  deltaLabel?:string;
  /** 0–1: barra pontilhada de traços que apagam em cinza. */
  progress?:number;
  ticks?:number;
  tone?:'brand'|'apoio'|'info'|'neutro';
  /** Kebab ⋮ no canto. */
  menu?:boolean;
  style?:React.CSSProperties;
}