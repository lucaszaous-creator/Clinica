export interface AvatarProps {
  /** Nome do paciente/usuário — vira iniciais quando não há foto. */
  name?: string;
  /** URL do retrato (no app é o JPEG da webcam da recepção). */
  src?: string;
  /** Diâmetro em px. Padrão 32. */
  size?: number;
  style?: React.CSSProperties;
}
export declare function Avatar(props: AvatarProps): JSX.Element;
