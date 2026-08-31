import React from 'react';
function Skeleton({
  width = '100%',
  height = 12,
  radius = 'var(--radius-pequeno)',
  style
}) {
  return /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'block',
      width,
      height,
      borderRadius: radius,
      background: 'var(--cinza-100)',
      animation: 'pulse 1.4s ease-in-out infinite',
      ...style
    }
  });
}
export { Skeleton };
