import React from 'react';
function DataTable({
  columns = [],
  rows = [],
  maxHeight,
  onRowClick,
  empty,
  style
}) {
  const [hov, setHov] = React.useState(-1);
  const th = {
    background: 'var(--table-header-bg)',
    color: 'var(--text-body)',
    fontWeight: 600,
    height: 'var(--table-header-height)',
    padding: 'var(--table-cell-pad)',
    borderBottom: '1px solid var(--border)',
    textAlign: 'left',
    fontSize: 'var(--text-table-size)',
    whiteSpace: 'nowrap'
  };
  return /*#__PURE__*/React.createElement("div", {
    style: {
      border: '1px solid var(--border)',
      borderRadius: 'var(--radius-control)',
      background: '#fff',
      overflow: 'auto',
      maxHeight,
      ...style
    }
  }, /*#__PURE__*/React.createElement("table", {
    style: {
      borderCollapse: 'collapse',
      width: '100%',
      fontFamily: 'var(--font-ui)',
      fontSize: 'var(--text-table-size)',
      color: 'var(--text-title)'
    }
  }, /*#__PURE__*/React.createElement("thead", null, /*#__PURE__*/React.createElement("tr", null, columns.map((c, i) => /*#__PURE__*/React.createElement("th", {
    key: i,
    style: {
      ...th,
      width: c.width,
      textAlign: c.align || 'left'
    }
  }, c.header)))), /*#__PURE__*/React.createElement("tbody", null, rows.length === 0 ? /*#__PURE__*/React.createElement("tr", null, /*#__PURE__*/React.createElement("td", {
    colSpan: columns.length,
    style: {
      padding: 24,
      textAlign: 'center',
      color: 'var(--text-muted)'
    }
  }, empty || 'Nada por aqui.')) : null, rows.map((r, ri) => /*#__PURE__*/React.createElement("tr", {
    key: ri,
    onClick: () => onRowClick && onRowClick(r, ri),
    onMouseEnter: () => setHov(ri),
    onMouseLeave: () => setHov(-1),
    style: {
      background: hov === ri ? 'var(--surface-hover)' : ri % 2 ? 'var(--surface-row-alt)' : '#fff',
      cursor: onRowClick ? 'pointer' : 'default'
    }
  }, columns.map((c, ci) => /*#__PURE__*/React.createElement("td", {
    key: ci,
    style: {
      padding: 'var(--table-cell-pad)',
      borderBottom: '1px solid var(--border)',
      height: 'var(--table-row-height)',
      boxSizing: 'border-box',
      textAlign: c.align || 'left'
    }
  }, c.render ? c.render(r, ri) : r[c.key])))))));
}
export { DataTable };
