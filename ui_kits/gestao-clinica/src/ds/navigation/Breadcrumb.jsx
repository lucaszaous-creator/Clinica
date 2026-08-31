import React from 'react';
import { Icon } from '../core/Icon.jsx';
function Breadcrumb({
  items = [],
  onNavigate,
  style
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 6,
      fontFamily: 'var(--font-ui)',
      fontSize: 13,
      ...style
    }
  }, items.map((it, i) => /*#__PURE__*/React.createElement(React.Fragment, {
    key: i
  }, i ? /*#__PURE__*/React.createElement(Icon, {
    name: "chevron-right",
    size: 14,
    style: {
      color: 'var(--cinza-300)'
    }
  }) : null, /*#__PURE__*/React.createElement("span", {
    onClick: () => i < items.length - 1 && onNavigate && onNavigate(it, i),
    style: {
      color: i === items.length - 1 ? 'var(--text-title)' : 'var(--text-muted)',
      fontWeight: i === items.length - 1 ? 600 : 400,
      cursor: i < items.length - 1 && onNavigate ? 'pointer' : 'default'
    }
  }, it.label || it))));
}
export { Breadcrumb };
