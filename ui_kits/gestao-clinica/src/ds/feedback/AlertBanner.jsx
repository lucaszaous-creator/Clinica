import React from 'react';
import { Icon } from '../core/Icon.jsx';
function AlertBanner({
  tone = 'info',
  icon,
  title,
  action,
  children,
  style
}) {
  const t = {
    info: {
      bg: 'var(--info-tint)',
      fg: 'var(--info-text)'
    },
    success: {
      bg: 'var(--success-tint)',
      fg: 'var(--success-text)'
    },
    warning: {
      bg: 'var(--warning-tint)',
      fg: 'var(--warning-text)'
    },
    danger: {
      bg: 'var(--danger-tint)',
      fg: 'var(--danger-text)'
    }
  }[tone];
  return /*#__PURE__*/React.createElement("div", {
    style: {
      background: t.bg,
      color: t.fg,
      borderRadius: 'var(--radius-control)',
      padding: '10px 12px',
      fontFamily: 'var(--font-ui)',
      fontSize: 13,
      display: 'flex',
      alignItems: 'center',
      gap: 10,
      ...style
    }
  }, icon ? /*#__PURE__*/React.createElement(Icon, {
    name: icon,
    size: 16
  }) : null, /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1
    }
  }, title ? /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 700
    }
  }, title) : null, children), action);
}
export { AlertBanner };
