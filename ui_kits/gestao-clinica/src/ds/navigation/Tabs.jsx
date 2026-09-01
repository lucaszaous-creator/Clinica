import React from 'react';
function Tabs({
  tabs = [],
  activeId,
  onSelect,
  style
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 20,
      borderBottom: '1px solid var(--border)',
      ...style
    }
  }, tabs.map(t => {
    const a = t.id === activeId;
    return /*#__PURE__*/React.createElement("button", {
      key: t.id,
      onClick: () => onSelect && onSelect(t.id),
      style: {
        border: 'none',
        background: 'transparent',
        padding: '8px 0',
        cursor: 'pointer',
        fontFamily: 'var(--font-ui)',
        fontSize: 14,
        fontWeight: a ? 600 : 400,
        color: a ? 'var(--brand)' : 'var(--text-body)',
        boxShadow: a ? 'inset 0 -2px 0 var(--brand)' : 'none'
      }
    }, t.label);
  }));
}
export { Tabs };
