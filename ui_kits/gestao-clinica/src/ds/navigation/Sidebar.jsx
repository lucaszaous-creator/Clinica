import React from 'react';
import { Icon } from '../core/Icon.jsx';
import { Badge } from '../feedback/Badge.jsx';
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function MenuItem({
  icon,
  label,
  badge,
  active,
  onClick
}) {
  const [h, setH] = React.useState(false);
  return /*#__PURE__*/React.createElement("button", {
    onClick: onClick,
    onMouseEnter: () => setH(true),
    onMouseLeave: () => setH(false),
    style: {
      position: 'relative',
      display: 'flex',
      alignItems: 'center',
      gap: 10,
      height: 'var(--sidebar-item-height)',
      padding: '0 10px 0 12px',
      width: '100%',
      border: 'none',
      borderRadius: 'var(--radius-control)',
      cursor: 'pointer',
      textAlign: 'left',
      fontFamily: 'var(--font-ui)',
      fontSize: 14,
      fontWeight: active ? 600 : 400,
      color: active ? 'var(--sidebar-text-active)' : 'var(--sidebar-text)',
      background: active ? 'var(--sidebar-item-ativo)' : h ? 'var(--sidebar-item-hover)' : 'transparent',
      transition: 'background var(--duracao-rapida) linear'
    }
  }, active ? /*#__PURE__*/React.createElement("span", {
    style: {
      position: 'absolute',
      left: 0,
      top: 8,
      bottom: 8,
      width: 3,
      borderRadius: 2,
      background: 'var(--brand)'
    }
  }) : null, /*#__PURE__*/React.createElement(Icon, {
    name: icon,
    size: 17
  }), /*#__PURE__*/React.createElement("span", {
    style: {
      flex: 1,
      minWidth: 0,
      overflow: 'hidden',
      textOverflow: 'ellipsis',
      whiteSpace: 'nowrap'
    }
  }, label), badge ? /*#__PURE__*/React.createElement(Badge, {
    tone: "marca"
  }, badge) : null);
}
function Sidebar({
  groups = [],
  activeId,
  onSelect,
  logoSrc,
  productName,
  footer,
  style
}) {
  return /*#__PURE__*/React.createElement("nav", {
    style: {
      width: 'var(--sidebar-width)',
      flexShrink: 0,
      minHeight: '100%',
      background: 'var(--sidebar-bg)',
      borderRight: '1px solid var(--border)',
      padding: '16px 12px',
      boxSizing: 'border-box',
      display: 'flex',
      flexDirection: 'column',
      gap: 4,
      ...style
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 10,
      padding: '0 4px 8px'
    }
  }, logoSrc ? /*#__PURE__*/React.createElement("img", {
    src: logoSrc,
    alt: "Cl\xEDnica SemDor",
    style: {
      height: 26,
      width: 'auto'
    }
  }) : /*#__PURE__*/React.createElement("span", {
    style: {
      fontFamily: 'var(--font-ui)',
      fontSize: 16,
      fontWeight: 700,
      color: 'var(--text-title)'
    }
  }, "Cl\xEDnica SemDor")), productName ? /*#__PURE__*/React.createElement("div", {
    style: {
      fontFamily: 'var(--font-ui)',
      fontSize: 12,
      color: 'var(--text-muted)',
      padding: '0 4px 12px'
    }
  }, productName) : null, groups.map((g, gi) => /*#__PURE__*/React.createElement("div", {
    key: gi,
    style: {
      marginTop: gi ? 12 : 0
    }
  }, g.label ? /*#__PURE__*/React.createElement("div", {
    style: {
      fontFamily: 'var(--font-ui)',
      fontSize: 'var(--text-secao-size)',
      fontWeight: 700,
      letterSpacing: 'var(--text-secao-tracking)',
      textTransform: 'uppercase',
      color: 'var(--text-muted)',
      padding: '0 12px 6px'
    }
  }, g.label) : null, g.items.map(it => /*#__PURE__*/React.createElement(MenuItem, _extends({
    key: it.id
  }, it, {
    active: it.id === activeId,
    onClick: () => onSelect && onSelect(it.id)
  }))))), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1
    }
  }), footer);
}
export { Sidebar };
