export interface DataTableColumn{
  header:React.ReactNode;
  /** Chave do valor em row; ou use render. */
  key?:string;
  width?:string;
  align?:'left'|'right'|'center';
  render?:(row:any)=>React.ReactNode;
}
export interface DataTableProps{
  columns:DataTableColumn[];
  rows:any[];
  maxHeight?:number;
  onRowClick?:(row:any)=>void;
  /** Conteúdo quando rows está vazio (EmptyState). */
  empty?:React.ReactNode;
  style?:React.CSSProperties;
}