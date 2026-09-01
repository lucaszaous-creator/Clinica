export interface BarDatum {
  /** Barra acima do zero. */
  entrada?: number;
  /** Barra abaixo do zero (opcional). */
  saida?: number;
  /** Alias de entrada, para séries de valor único. */
  valor?: number;
}
export interface BarChartProps {
  data?: BarDatum[];
  labels?: Array<string | null>;
  height?: number;
  positiveColor?: string;
  negativeColor?: string;
  /** Índice destacado; os demais ficam esmaecidos. */
  marker?: number;
  tooltip?: React.ReactNode;
  style?: React.CSSProperties;
}
export declare function BarChart(props: BarChartProps): JSX.Element;
