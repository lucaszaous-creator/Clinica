import React from 'react';
import { Icon } from './Icon.jsx';
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function Button({
  variant = 'primary',
  size = 'md',
  icon,
  iconRight,
  disabled,
  loading,
  children,
  style,
  ...rest
}) {
  const [h, setH] = React.useState(false);
  const base = {
    fontFamily: 'var(--font-ui)',
    fontSize: size === 'sm' ? '12px' : '13px',
    fontWeight: 600,
    padding: size === 'sm' ? '4px 10px' : '8px 14px',
    borderRadius: 'var(--radius-control)',
    border: '1px solid transparent',
    cursor: disabled ? 'default' : 'pointer',
    opacity: disabled ? .5 : 1,
    display: 'inline-flex',
    alignItems: 'center',
    justifyContent: 'center',
    gap: '6px',
    lineHeight: '18px',
    transition: 'background var(--duracao-rapida) linear,border-color var(--duracao-rapida) linear'
  };
  const v = {
    primary: {
      background: h && !disabled ? 'var(--brand-hover)' : 'var(--brand)',
      color: '#fff'
    },
    secondary: {
      background: h && !disabled ? 'var(--surface-hover)' : '#fff',
      color: 'var(--text-body)',
      borderColor: h && !disabled ? 'var(--border-hover)' : 'var(--border)'
    },
    ghost: {
      background: h && !disabled ? 'var(--surface-hover)' : 'transparent',
      color: 'var(--text-body)'
    },
    danger: {
      background: h && !disabled ? 'var(--danger-hover)' : 'var(--danger)',
      color: '#fff'
    }
  }[variant] || {};
  return /*#__PURE__*/React.createElement("button", _extends({
    disabled: disabled || loading,
    onMouseEnter: () => setH(true),
    onMouseLeave: () => setH(false),
    style: {
      ...base,
      ...v,
      ...style
    }
  }, rest), loading ? /*#__PURE__*/React.createElement(Icon, {
    name: "loader-2",
    size: 14,
    style: {
      animation: 'spin 900ms linear infinite'
    }
  }) : icon ? /*#__PURE__*/React.createElement(Icon, {
    name: icon,
    size: size === 'sm' ? 13 : 15
  }) : null, children, iconRight ? /*#__PURE__*/React.createElement(Icon, {
    name: iconRight,
    size: size === 'sm' ? 13 : 15
  }) : null);
}
export { Button };
