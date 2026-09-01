export interface DatePickerProps {
  /** ISO yyyy-mm-dd; exiba sempre dd/mm/aaaa em texto. */
  value?: string;
  onChange?: (value: string) => void;
  style?: React.CSSProperties;
}
export declare function DatePicker(props: DatePickerProps): JSX.Element;
