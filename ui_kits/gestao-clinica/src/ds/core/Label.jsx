import React from 'react';
function Label({
  children,
  hint,
  style
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      fontFamily: 'var(--font-ui)',
      fontSize: '13px',
      fontWeight: 600,
      color: 'var(--text-body)',
      margin: '12px 0 4px',
      ...style
    }
  }, children, hint ? /*#__PURE__*/React.createElement("span", {
    style: {
      fontWeight: 400,
      color: 'var(--text-muted)'
    }
  }, " \xB7 ", hint) : null);
}
export { Label };
