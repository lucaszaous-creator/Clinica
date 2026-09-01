import React from 'react';
import { Icon } from '../core/Icon.jsx';
import { IconButton } from '../core/IconButton.jsx';
function KpiCard({
  label,
  icon,
  value,
  suffix,
  delta,
  deltaLabel = 'vs. período anterior',
  progress,
  ticks = 28,
  tone = 'brand',
  menu,
  style
}) {
  const cor = {
    brand: 'var(--brand)',
    apoio: 'var(--marca-medio-brush)',
    info: 'var(--info-color)',
    neutro: 'var(--cinza-500)'
  }[tone] || 'var(--brand)';
  const acesos = progress == null ? 0 : Math.round(ticks * Math.max(0, Math.min(1, progress)));
  const positivo = (delta || 0) >= 0;
  return /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'var(--surface-card)',
      border: '1px solid var(--border)',
      borderRadius: 'var(--radius-card)',
      padding: '16px',
      fontFamily: 'var(--font-ui)',
      boxSizing: 'border-box',
      ...style
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 8
    }
  }, icon ? /*#__PURE__*/React.createElement(Icon, {
    name: icon,
    size: 16,
    style: {
      color: cor
    }
  }) : null, /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      fontSize: 13,
      color: 'var(--text-muted)'
    }
  }, label), menu ? /*#__PURE__*/React.createElement(IconButton, {
    icon: "more-vertical",
    label: "Op\xE7\xF5es",
    size: 24
  }) : null), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 'var(--text-kpi-grande-size)',
      fontWeight: 500,
      color: 'var(--text-title)',
      letterSpacing: '-.02em',
      display: 'flex',
      alignItems: 'baseline',
      gap: 2,
      margin: '10px 0 12px'
    }
  }, value, suffix ? /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 20,
      fontWeight: 500
    }
  }, suffix) : null), progress != null ? /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 3,
      alignItems: 'flex-end',
      height: 14,
      marginBottom: 10
    }
  }, Array.from({
    length: ticks
  }).map((_, i) => /*#__PURE__*/React.createElement("span", {
    key: i,
    style: {
      flex: 1,
      height: i < acesos ? 14 : 8,
      borderRadius: 1,
      background: i < acesos ? cor : 'var(--cinza-200)'
    }
  }))) : null, delta != null ? /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 6,
      fontSize: 13
    }
  }, /*#__PURE__*/React.createElement(Icon, {
    name: positivo ? 'arrow-up-right' : 'arrow-down-right',
    size: 14,
    style: {
      color: positivo ? 'var(--success-text)' : 'var(--danger-text)'
    }
  }), /*#__PURE__*/React.createElement("b", {
    style: {
      color: positivo ? 'var(--success-text)' : 'var(--danger-text)',
      fontWeight: 600
    }
  }, positivo ? '+' : '', String(delta).replace('.', ','), "%"), /*#__PURE__*/React.createElement("span", {
    style: {
      color: 'var(--text-muted)'
    }
  }, deltaLabel)) : null);
}
export { KpiCard };
