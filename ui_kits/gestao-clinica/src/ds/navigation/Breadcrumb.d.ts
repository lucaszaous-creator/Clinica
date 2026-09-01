export interface BreadcrumbProps{
  /** Strings ou {label}; o último é a tela atual. */
  items:(string|{label:string})[];
  onNavigate?:(item:any,index:number)=>void;
  style?:React.CSSProperties;
}