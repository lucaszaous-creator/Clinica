import React from 'react';
import { Icon } from './Icon.jsx';
function IconButton({
  icon,
  label,
  size = 36,
  active,
  onClick,
  style
}) {
  const [h, setH] = React.useState(false);
  return /*#__PURE__*/React.createElement("button", {
    onClick: onClick,
    title: label,
    "aria-label": label,
    onMouseEnter: () => setH(true),
    onMouseLeave: () => setH(false),
    style: {
      width: size,
      height: size,
      display: 'inline-flex',
      alignItems: 'center',
      justifyContent: 'center',
      borderRadius: 'var(--radius-control)',
      border: '1px solid ' + (active ? 'var(--border)' : 'transparent'),
      background: active ? 'var(--brand-soft)' : h ? 'var(--surface-hover)' : 'transparent',
      color: active ? 'var(--brand)' : 'var(--text-body)',
      cursor: 'pointer',
      padding: 0,
      ...style
    }
  }, /*#__PURE__*/React.createElement(Icon, {
    name: icon,
    size: 17
  }));
}
export { IconButton };
