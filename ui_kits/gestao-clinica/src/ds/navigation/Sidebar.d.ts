export interface SidebarItem{id:string;icon:string;label:string;
  /** Badge pílula à direita (ex.: "R$ 24,8k"). */
  badge?:string|number;}
export interface SidebarGroup{
  /** Rótulo do grupo em caixa alta (PRINCIPAL, CLÍNICO, FINANCEIRO…). */
  label?:string;
  items:SidebarItem[];}
export interface SidebarProps{
  groups:SidebarGroup[];
  activeId?:string;
  onSelect?:(id:string)=>void;
  /** Logo no topo; sem ele, o nome "Clínica SemDor" em texto. */
  logoSrc?:string;
  /** Nome do produto sob o logo (ex.: "Gestão da clínica"). */
  productName?:string;
  footer?:React.ReactNode;
  style?:React.CSSProperties;
}