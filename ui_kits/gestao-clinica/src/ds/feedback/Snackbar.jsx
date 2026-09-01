import React from 'react';
import { Icon } from '../core/Icon.jsx';
function Snackbar({
  tone = 'sucesso',
  children,
  onClose,
  style
}) {
  const c = tone === 'erro' ? 'var(--snackbar-erro)' : tone === 'sucesso' ? 'var(--snackbar-sucesso)' : '#fff';
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      gap: 10,
      background: 'var(--snackbar-fundo)',
      color: '#fff',
      borderRadius: 'var(--radius-control)',
      padding: '10px 14px',
      fontFamily: 'var(--font-ui)',
      fontSize: 13,
      boxShadow: 'var(--sombra-popup)',
      ...style
    }
  }, /*#__PURE__*/React.createElement(Icon, {
    name: tone === 'erro' ? 'alert-circle' : tone === 'sucesso' ? 'check-circle' : 'info',
    size: 16,
    style: {
      color: c
    }
  }), /*#__PURE__*/React.createElement("span", {
    style: {
      flex: 1
    }
  }, children), onClose ? /*#__PURE__*/React.createElement("span", {
    onClick: onClose,
    style: {
      cursor: 'pointer',
      opacity: .7,
      display: 'inline-flex'
    }
  }, /*#__PURE__*/React.createElement(Icon, {
    name: "x",
    size: 14
  })) : null);
}
export { Snackbar };
