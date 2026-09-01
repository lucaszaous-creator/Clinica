export interface LineSeries {
  name?: string;
  data: number[];
  /** Use var(--serie-1..3); cinza tracejado para a série de comparação. */
  color: string;
  /** Traço tracejado (comparação/benchmark). */
  dashed?: boolean;
  /** Preenchimento em degradê até transparente. */
  fill?: boolean;
}
export interface LineChartProps {
  series?: LineSeries[];
  /** Rótulos do eixo X (Jan…Dez). */
  labels?: string[];
  height?: number;
  /** Formatação do eixo Y. */
  yFormat?: (value: number) => string | number;
  /** Índice do ponto destacado (linha vertical tracejada). */
  marker?: number;
  /** Conteúdo da caixa flutuante sobre o marcador. */
  tooltip?: React.ReactNode;
  style?: React.CSSProperties;
}
export declare function LineChart(props: LineChartProps): JSX.Element;
