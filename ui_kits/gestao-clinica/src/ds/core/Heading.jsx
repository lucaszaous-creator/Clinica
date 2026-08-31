import React from 'react';
function Heading({
  level = 1,
  subtitle,
  actions,
  children,
  style
}) {
  const s = level === 1 ? {
    fontSize: 'var(--text-h1-size)',
    fontWeight: 700
  } : level === 2 ? {
    fontSize: 'var(--text-h2-size)',
    fontWeight: 600
  } : {
    fontSize: 'var(--text-h3-size)',
    fontWeight: 600
  };
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'flex-start',
      gap: 16,
      margin: '0 0 16px',
      ...style
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      minWidth: 0
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontFamily: 'var(--font-ui)',
      color: 'var(--text-title)',
      ...s
    }
  }, children), subtitle ? /*#__PURE__*/React.createElement("div", {
    style: {
      fontFamily: 'var(--font-ui)',
      fontSize: 14,
      color: 'var(--text-muted)',
      marginTop: 4
    }
  }, subtitle) : null), actions ? /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 8,
      flexShrink: 0
    }
  }, actions) : null);
}
export { Heading };
