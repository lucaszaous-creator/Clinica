export interface TabsProps{
  tabs:{id:string;label:string}[];
  activeId?:string;
  onSelect?:(id:string)=>void;
  style?:React.CSSProperties;
}