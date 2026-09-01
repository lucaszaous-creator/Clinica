import React from 'react';
function Spinner({
  size = 16,
  color = 'var(--brand)',
  style
}) {
  return /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'inline-block',
      width: size,
      height: size,
      borderRadius: '50%',
      border: '2px solid var(--cinza-200)',
      borderTopColor: color,
      animation: 'spin 900ms linear infinite',
      ...style
    }
  });
}
export { Spinner };
