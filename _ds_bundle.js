/* @ds-bundle: {"format":4,"namespace":"ClNicaFaturamentoDesignSystem_bd26af","components":[{"name":"Button","sourcePath":"components/core/Button.jsx"},{"name":"Card","sourcePath":"components/core/Card.jsx"},{"name":"Checkbox","sourcePath":"components/core/Checkbox.jsx"},{"name":"DatePicker","sourcePath":"components/core/DatePicker.jsx"},{"name":"Heading","sourcePath":"components/core/Heading.jsx"},{"name":"Icon","sourcePath":"components/core/Icon.jsx"},{"name":"Input","sourcePath":"components/core/Input.jsx"},{"name":"Label","sourcePath":"components/core/Label.jsx"},{"name":"Select","sourcePath":"components/core/Select.jsx"},{"name":"DataTable","sourcePath":"components/data/DataTable.jsx"},{"name":"KpiCard","sourcePath":"components/data/KpiCard.jsx"},{"name":"UrgencyDot","sourcePath":"components/data/UrgencyDot.jsx"},{"name":"AlertBanner","sourcePath":"components/feedback/AlertBanner.jsx"},{"name":"Sidebar","sourcePath":"components/navigation/Sidebar.jsx"}],"sourceHashes":{"components/core/Button.jsx":"0147ca488bb5","components/core/Card.jsx":"4cdbba74362d","components/core/Checkbox.jsx":"63106875504b","components/core/DatePicker.jsx":"8bcb3b66c55d","components/core/Heading.jsx":"ee5f76e8ec87","components/core/Icon.jsx":"092c2b13da55","components/core/Input.jsx":"c07fd5fb72fa","components/core/Label.jsx":"4eef82637245","components/core/Select.jsx":"0ffa79c3a795","components/data/DataTable.jsx":"281fffe2e1af","components/data/KpiCard.jsx":"506a7406a08d","components/data/UrgencyDot.jsx":"7b2584b329e0","components/feedback/AlertBanner.jsx":"e4dce0f963d6","components/navigation/Sidebar.jsx":"32b914ef2053","ui_kits/faturamento/modals.jsx":"d3cd82d15e4e","ui_kits/faturamento/screens.jsx":"51056cd1036c"},"inlinedExternals":[],"unexposedExports":[]} */

(() => {

const __ds_ns = (window.ClNicaFaturamentoDesignSystem_bd26af = window.ClNicaFaturamentoDesignSystem_bd26af || {});

const __ds_scope = {};

(__ds_ns.__errors = __ds_ns.__errors || []);

// components/core/Card.jsx
try { (() => {
function Card({
  title,
  children,
  style
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'var(--surface-card)',
      border: '1px solid var(--border)',
      borderRadius: 'var(--radius-card)',
      padding: 'var(--space-card-pad)',
      ...style
    }
  }, title ? /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 'var(--text-h2-size)',
      fontWeight: 600,
      color: 'var(--text-title)',
      margin: '0 0 8px'
    }
  }, title) : null, children);
}
Object.assign(__ds_scope, { Card });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/core/Card.jsx", error: String((e && e.message) || e) }); }

// components/core/Checkbox.jsx
try { (() => {
function Checkbox({
  checked,
  onChange,
  children,
  style
}) {
  return /*#__PURE__*/React.createElement("label", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: '8px',
      fontFamily: 'var(--font-ui)',
      fontSize: '13px',
      color: 'var(--text-title)',
      cursor: 'pointer',
      ...style
    }
  }, /*#__PURE__*/React.createElement("input", {
    type: "checkbox",
    checked: !!checked,
    onChange: e => onChange && onChange(e.target.checked),
    style: {
      accentColor: 'var(--brand)',
      width: '14px',
      height: '14px',
      margin: 0
    }
  }), children);
}
Object.assign(__ds_scope, { Checkbox });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/core/Checkbox.jsx", error: String((e && e.message) || e) }); }

// components/core/DatePicker.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function DatePicker({
  value,
  onChange,
  style,
  ...rest
}) {
  return /*#__PURE__*/React.createElement("input", _extends({
    type: "date",
    value: value || '',
    onChange: e => onChange && onChange(e.target.value),
    style: {
      fontFamily: 'var(--font-ui)',
      fontSize: '13px',
      padding: '5px 8px',
      border: '1px solid var(--border)',
      borderRadius: '2px',
      color: 'var(--text-title)',
      background: '#fff',
      boxSizing: 'border-box',
      ...style
    }
  }, rest));
}
Object.assign(__ds_scope, { DatePicker });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/core/DatePicker.jsx", error: String((e && e.message) || e) }); }

// components/core/Heading.jsx
try { (() => {
function Heading({
  level = 1,
  children,
  style
}) {
  const s = level === 1 ? {
    fontSize: 'var(--text-h1-size)',
    fontWeight: 700,
    margin: '0 0 16px'
  } : {
    fontSize: 'var(--text-h2-size)',
    fontWeight: 600,
    margin: '8px 0'
  };
  return /*#__PURE__*/React.createElement("div", {
    style: {
      fontFamily: 'var(--font-ui)',
      color: 'var(--text-title)',
      ...s,
      ...style
    }
  }, children);
}
Object.assign(__ds_scope, { Heading });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/core/Heading.jsx", error: String((e && e.message) || e) }); }

// components/core/Icon.jsx
try { (() => {
function Icon({
  name,
  size = 16,
  style
}) {
  const ref = React.useRef(null);
  React.useEffect(() => {
    if (!ref.current) return;
    ref.current.innerHTML = '';
    const el = document.createElement('i');
    el.setAttribute('data-lucide', name);
    ref.current.appendChild(el);
    if (window.lucide) window.lucide.createIcons({
      attrs: {
        width: size,
        height: size,
        'stroke-width': 2
      }
    });
  }, [name, size]);
  return /*#__PURE__*/React.createElement("span", {
    ref: ref,
    "aria-hidden": "true",
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      width: size,
      height: size,
      flexShrink: 0,
      ...style
    }
  });
}
Object.assign(__ds_scope, { Icon });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/core/Icon.jsx", error: String((e && e.message) || e) }); }

// components/core/Button.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function Button({
  variant = 'primary',
  size = 'md',
  icon,
  disabled,
  children,
  style,
  ...rest
}) {
  const [h, setH] = React.useState(false);
  const base = {
    fontFamily: 'var(--font-ui)',
    fontSize: size === 'sm' ? '12px' : '13px',
    fontWeight: 600,
    padding: size === 'sm' ? '3px 8px' : 'var(--space-btn-pad, 8px 14px)',
    borderRadius: 'var(--radius-control)',
    border: '1px solid transparent',
    cursor: disabled ? 'default' : 'pointer',
    opacity: disabled ? .5 : 1,
    display: 'inline-flex',
    alignItems: 'center',
    gap: '6px',
    lineHeight: '18px'
  };
  const v = {
    primary: {
      background: h && !disabled ? 'var(--brand-hover)' : 'var(--brand)',
      color: '#fff'
    },
    secondary: {
      background: h && !disabled ? 'var(--slate-100)' : '#fff',
      color: 'var(--text-body)',
      borderColor: 'var(--border)'
    },
    danger: {
      background: h && !disabled ? 'var(--danger-hover)' : 'var(--danger)',
      color: '#fff'
    }
  }[variant] || {};
  return /*#__PURE__*/React.createElement("button", _extends({
    disabled: disabled,
    onMouseEnter: () => setH(true),
    onMouseLeave: () => setH(false),
    style: {
      ...base,
      ...v,
      ...style
    }
  }, rest), icon ? /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: icon,
    size: size === 'sm' ? 13 : 15
  }) : null, children);
}
Object.assign(__ds_scope, { Button });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/core/Button.jsx", error: String((e && e.message) || e) }); }

// components/core/Input.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function Input({
  multiline,
  mono,
  value,
  onChange,
  placeholder,
  rows = 3,
  style,
  ...rest
}) {
  const s = {
    fontFamily: mono ? 'var(--font-mono)' : 'var(--font-ui)',
    fontSize: '13px',
    padding: 'var(--space-input-pad)',
    border: '1px solid var(--border)',
    borderRadius: '2px',
    color: 'var(--text-title)',
    background: '#fff',
    width: '100%',
    boxSizing: 'border-box',
    outline: 'none',
    resize: 'vertical',
    ...style
  };
  const p = {
    value,
    placeholder,
    onChange: e => onChange && onChange(e.target.value),
    style: s,
    ...rest
  };
  return multiline ? /*#__PURE__*/React.createElement("textarea", _extends({
    rows: rows
  }, p)) : /*#__PURE__*/React.createElement("input", p);
}
Object.assign(__ds_scope, { Input });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/core/Input.jsx", error: String((e && e.message) || e) }); }

// components/core/Label.jsx
try { (() => {
function Label({
  children,
  style
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      fontFamily: 'var(--font-ui)',
      fontSize: '13px',
      fontWeight: 600,
      color: 'var(--text-body)',
      margin: '8px 0 3px',
      ...style
    }
  }, children);
}
Object.assign(__ds_scope, { Label });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/core/Label.jsx", error: String((e && e.message) || e) }); }

// components/core/Select.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function Select({
  options = [],
  value,
  onChange,
  style,
  ...rest
}) {
  return /*#__PURE__*/React.createElement("select", _extends({
    value: value,
    onChange: e => onChange && onChange(e.target.value),
    style: {
      fontFamily: 'var(--font-ui)',
      fontSize: '13px',
      padding: '5px 8px',
      border: '1px solid var(--border)',
      borderRadius: '2px',
      color: 'var(--text-title)',
      background: '#fff',
      width: '100%',
      boxSizing: 'border-box',
      ...style
    }
  }, rest), options.map(o => typeof o === 'string' ? /*#__PURE__*/React.createElement("option", {
    key: o,
    value: o
  }, o) : /*#__PURE__*/React.createElement("option", {
    key: o.value,
    value: o.value
  }, o.label)));
}
Object.assign(__ds_scope, { Select });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/core/Select.jsx", error: String((e && e.message) || e) }); }

// components/data/DataTable.jsx
try { (() => {
function DataTable({
  columns = [],
  rows = [],
  maxHeight,
  style
}) {
  const th = {
    background: 'var(--table-header-bg)',
    color: 'var(--text-body)',
    fontWeight: 600,
    padding: 'var(--table-header-pad)',
    borderBottom: '1px solid var(--border)',
    textAlign: 'left',
    fontSize: '13px',
    whiteSpace: 'nowrap'
  };
  return /*#__PURE__*/React.createElement("div", {
    style: {
      border: '1px solid var(--border)',
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
      fontSize: '13px',
      color: 'var(--text-title)'
    }
  }, /*#__PURE__*/React.createElement("thead", null, /*#__PURE__*/React.createElement("tr", null, columns.map((c, i) => /*#__PURE__*/React.createElement("th", {
    key: i,
    style: {
      ...th,
      width: c.width
    }
  }, c.header)))), /*#__PURE__*/React.createElement("tbody", null, rows.map((r, ri) => /*#__PURE__*/React.createElement("tr", {
    key: ri,
    style: {
      background: ri % 2 ? 'var(--surface-row-alt)' : '#fff'
    }
  }, columns.map((c, ci) => /*#__PURE__*/React.createElement("td", {
    key: ci,
    style: {
      padding: 'var(--table-cell-pad)',
      borderBottom: '1px solid var(--border)',
      height: '18px'
    }
  }, c.render ? c.render(r, ri) : r[c.key])))))));
}
Object.assign(__ds_scope, { DataTable });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/data/DataTable.jsx", error: String((e && e.message) || e) }); }

// components/data/KpiCard.jsx
try { (() => {
function KpiCard({
  label,
  value,
  suffix,
  tone = 'neutral',
  style
}) {
  const t = {
    neutral: {
      bg: 'var(--slate-100)',
      label: '#555',
      value: 'var(--text-title)'
    },
    success: {
      bg: 'var(--success-tint)',
      label: 'var(--success-text)',
      value: 'var(--success-text)'
    },
    danger: {
      bg: 'var(--danger-tint)',
      label: 'var(--danger-text)',
      value: 'var(--danger-text)'
    },
    brand: {
      bg: 'var(--brand)',
      label: 'var(--verde-claro)',
      value: '#fff'
    }
  }[tone];
  return /*#__PURE__*/React.createElement("div", {
    style: {
      background: t.bg,
      borderRadius: 'var(--radius-card)',
      padding: '16px',
      fontFamily: 'var(--font-ui)',
      ...style
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: '13px',
      color: t.label
    }
  }, label), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 'var(--text-kpi-size)',
      fontWeight: 700,
      color: t.value,
      display: 'flex',
      alignItems: 'baseline'
    }
  }, value, suffix ? /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: '20px',
      fontWeight: 400,
      marginLeft: '2px'
    }
  }, suffix) : null));
}
Object.assign(__ds_scope, { KpiCard });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/data/KpiCard.jsx", error: String((e && e.message) || e) }); }

// components/data/UrgencyDot.jsx
try { (() => {
function UrgencyDot({
  level = 'verde',
  size = 16,
  style
}) {
  const c = {
    verde: 'var(--semaforo-verde)',
    amarelo: 'var(--semaforo-amarelo)',
    vermelho: 'var(--semaforo-vermelho)'
  }[level] || 'gray';
  return /*#__PURE__*/React.createElement("span", {
    title: level,
    style: {
      display: 'inline-block',
      width: size + 'px',
      height: size + 'px',
      borderRadius: '50%',
      background: c,
      verticalAlign: 'middle',
      ...style
    }
  });
}
Object.assign(__ds_scope, { UrgencyDot });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/data/UrgencyDot.jsx", error: String((e && e.message) || e) }); }

// components/feedback/AlertBanner.jsx
try { (() => {
function AlertBanner({
  tone = 'info',
  icon,
  title,
  children,
  style
}) {
  const t = {
    info: {
      bg: 'var(--slate-100)',
      fg: 'var(--text-body)',
      r: 'var(--radius-control)'
    },
    success: {
      bg: 'var(--success-tint)',
      fg: 'var(--success-text)',
      r: 'var(--radius-control)'
    },
    warning: {
      bg: 'var(--warning-tint)',
      fg: 'var(--text-title)',
      r: 'var(--radius-chip)'
    },
    danger: {
      bg: 'var(--danger)',
      fg: '#fff',
      r: 'var(--radius-control)'
    }
  }[tone];
  return /*#__PURE__*/React.createElement("div", {
    style: {
      background: t.bg,
      color: t.fg,
      borderRadius: t.r,
      padding: tone === 'warning' ? '6px 10px' : '10px 12px',
      fontFamily: 'var(--font-ui)',
      fontSize: tone === 'danger' ? '15px' : '13px',
      fontWeight: tone === 'danger' || title ? 700 : 400,
      display: 'flex',
      alignItems: 'center',
      gap: '10px',
      ...style
    }
  }, icon ? /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: icon,
    size: tone === 'danger' ? 20 : 16
  }) : null, /*#__PURE__*/React.createElement("div", null, title ? /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 700
    }
  }, title) : null, children));
}
Object.assign(__ds_scope, { AlertBanner });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/AlertBanner.jsx", error: String((e && e.message) || e) }); }

// components/navigation/Sidebar.jsx
try { (() => {
function MenuItem({
  icon,
  label,
  active,
  onClick
}) {
  const [h, setH] = React.useState(false);
  return /*#__PURE__*/React.createElement("button", {
    onClick: onClick,
    onMouseEnter: () => setH(true),
    onMouseLeave: () => setH(false),
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: '10px',
      height: 'var(--sidebar-item-height)',
      margin: '3px 0',
      padding: '0 12px',
      width: '100%',
      border: 'none',
      borderRadius: 'var(--radius-control)',
      cursor: 'pointer',
      textAlign: 'left',
      fontFamily: 'var(--font-ui)',
      fontSize: '14px',
      color: 'var(--sidebar-text)',
      background: active ? 'var(--sidebar-item-active)' : h ? 'var(--sidebar-item-hover)' : 'transparent'
    }
  }, /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: icon,
    size: 17
  }), label);
}
function Sidebar({
  items = [],
  activeId,
  onSelect,
  badge,
  style
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      width: 'var(--sidebar-width)',
      minHeight: '100%',
      background: 'var(--sidebar-bg)',
      padding: '16px',
      boxSizing: 'border-box',
      ...style
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontFamily: 'var(--font-ui)',
      color: '#fff',
      fontSize: '22px',
      fontWeight: 700
    }
  }, "CL\xCDNICA"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontFamily: 'var(--font-ui)',
      color: 'var(--sidebar-subtext)',
      fontSize: '13px',
      margin: '0 0 24px'
    }
  }, "Faturamento"), items.map(it => /*#__PURE__*/React.createElement(MenuItem, {
    key: it.id,
    icon: it.icon,
    label: it.label,
    active: it.id === activeId,
    onClick: () => onSelect && onSelect(it.id)
  })), badge != null ? /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'var(--danger)',
      borderRadius: 'var(--radius-control)',
      margin: '32px 0 0',
      padding: '10px 12px',
      color: '#fff',
      fontWeight: 700,
      fontFamily: 'var(--font-ui)',
      fontSize: '13px',
      display: 'flex',
      alignItems: 'center',
      gap: '8px'
    }
  }, /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: "bell",
    size: 15
  }), "Pend\xEAncias: ", badge) : null);
}
Object.assign(__ds_scope, { Sidebar });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/navigation/Sidebar.jsx", error: String((e && e.message) || e) }); }

// ui_kits/faturamento/modals.jsx
try { (() => {
const DSm = window.ClNicaFaturamentoDesignSystem_bd26af;
const {
  Button: BtnM,
  Input: InpM,
  Label: LblM,
  DatePicker: DateM,
  UrgencyDot: DotM,
  AlertBanner: BanM,
  Select: SelM,
  Icon: IcoM
} = DSm;
const CAPA_URL = '../../templates/capa-faturamento/CapaFaturamento.dc.html';
function Modal({
  title,
  icon,
  width = 460,
  children,
  onClose
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'fixed',
      inset: 0,
      background: 'rgba(15,23,42,.45)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      zIndex: 50
    },
    onClick: onClose
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      background: '#fff',
      borderRadius: 10,
      border: '1px solid var(--border)',
      width,
      maxWidth: '90vw',
      padding: 20,
      fontFamily: 'var(--font-ui)'
    },
    onClick: e => e.stopPropagation()
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      justifyContent: 'space-between',
      alignItems: 'center',
      marginBottom: 12
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 16,
      fontWeight: 600,
      color: 'var(--text-title)',
      display: 'flex',
      alignItems: 'center',
      gap: 8
    }
  }, icon ? /*#__PURE__*/React.createElement(IcoM, {
    name: icon,
    size: 18
  }) : null, title), /*#__PURE__*/React.createElement("button", {
    onClick: onClose,
    style: {
      border: 'none',
      background: 'transparent',
      cursor: 'pointer',
      color: 'var(--text-muted)',
      padding: 2
    }
  }, /*#__PURE__*/React.createElement(IcoM, {
    name: "x",
    size: 16
  }))), children));
}
function ModalBaixa({
  row,
  onConcluida,
  onClose
}) {
  const [g, setG] = React.useState(row.g === '—' ? '' : row.g);
  const [d, setD] = React.useState('2026-07-16');
  const segundo = row.tipo.indexOf('2º') >= 0;
  return /*#__PURE__*/React.createElement(Modal, {
    title: "Dar baixa — " + row.p,
    icon: "check-circle",
    onClose: onClose
  }, /*#__PURE__*/React.createElement(BanM, {
    tone: "warning",
    style: {
      marginBottom: 10
    }
  }, "Confirme a guia no sistema do conv\xEAnio antes de dar baixa."), /*#__PURE__*/React.createElement(LblM, null, "N\xFAmero da guia gerada (sistema do conv\xEAnio)"), /*#__PURE__*/React.createElement(InpM, {
    value: g,
    onChange: setG
  }), /*#__PURE__*/React.createElement(LblM, null, "Data da baixa"), /*#__PURE__*/React.createElement(DateM, {
    value: d,
    onChange: setD
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      marginTop: 16,
      display: 'flex',
      gap: 8,
      justifyContent: 'flex-end'
    }
  }, /*#__PURE__*/React.createElement(BtnM, {
    variant: "secondary",
    onClick: onClose
  }, "Cancelar"), /*#__PURE__*/React.createElement(BtnM, {
    onClick: () => {
      onClose();
      if (segundo) onConcluida(row);
    }
  }, "Confirmar baixa")));
}
function ModalGlosa({
  onClose
}) {
  const [m, setM] = React.useState('');
  return /*#__PURE__*/React.createElement(Modal, {
    title: "Registrar glosa",
    icon: "ban",
    onClose: onClose
  }, /*#__PURE__*/React.createElement(LblM, null, "Guia"), /*#__PURE__*/React.createElement(SelM, {
    options: ["88231 — Carlos Nunes (Amil)", "88240 — João Pereira (Amil)"]
  }), /*#__PURE__*/React.createElement(LblM, null, "Motivo da glosa"), /*#__PURE__*/React.createElement(InpM, {
    multiline: true,
    rows: 3,
    value: m,
    onChange: setM
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      marginTop: 16,
      display: 'flex',
      gap: 8,
      justifyContent: 'flex-end'
    }
  }, /*#__PURE__*/React.createElement(BtnM, {
    variant: "secondary",
    onClick: onClose
  }, "Cancelar"), /*#__PURE__*/React.createElement(BtnM, {
    variant: "danger",
    onClick: onClose
  }, "Registrar glosa")));
}
function ModalAviso({
  pend,
  onVer,
  onClose
}) {
  return /*#__PURE__*/React.createElement(Modal, {
    title: "Aviso de pend\xEAncias",
    icon: "bell",
    width: 520,
    onClose: onClose
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 13,
      color: 'var(--text-body)',
      marginBottom: 10
    }
  }, "Ao abrir o sistema foram encontradas ", /*#__PURE__*/React.createElement("b", null, pend.length, " pend\xEAncias"), ":"), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 6,
      marginBottom: 14
    }
  }, pend.slice(0, 4).map((r, i) => /*#__PURE__*/React.createElement("div", {
    key: i,
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 8,
      background: 'var(--warning-tint)',
      borderRadius: 6,
      padding: '6px 10px',
      fontSize: 13
    }
  }, /*#__PURE__*/React.createElement(DotM, {
    level: r.u,
    size: 14
  }), /*#__PURE__*/React.createElement("b", null, r.p), " — ", r.tipo, " \xB7 ", r.c))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 8,
      justifyContent: 'flex-end'
    }
  }, /*#__PURE__*/React.createElement(BtnM, {
    variant: "secondary",
    onClick: onClose
  }, "Fechar"), /*#__PURE__*/React.createElement(BtnM, {
    onClick: onVer
  }, "Ver painel de pend\xEAncias")));
}
function ModalPrimeiroAtendimento({
  dados,
  onClose
}) {
  return /*#__PURE__*/React.createElement(Modal, {
    title: "Atendimento lan\xE7ado — 1\xBA c\xF3digo gerado",
    icon: "file-check",
    width: 520,
    onClose: onClose
  }, /*#__PURE__*/React.createElement(BanM, {
    tone: "success",
    title: "Atendimento n\xBA 000124",
    style: {
      marginBottom: 10
    }
  }, dados.pac, " — ", dados.conv), /*#__PURE__*/React.createElement("div", {
    style: {
      border: '1px solid var(--border)',
      borderRadius: 8,
      padding: '10px 12px',
      fontSize: 13,
      color: 'var(--text-body)',
      marginBottom: 10
    }
  }, /*#__PURE__*/React.createElement("b", {
    style: {
      color: 'var(--text-title)'
    }
  }, "1\xBA c\xF3digo:"), " AUT-114612 \xB7 gerado em 16/07/2026"), /*#__PURE__*/React.createElement(BanM, {
    tone: "warning",
    style: {
      marginBottom: 14
    }
  }, "Este conv\xEAnio exige ", /*#__PURE__*/React.createElement("b", null, "2 c\xF3digos"), ". O 2\xBA c\xF3digo deve ser obtido em ", /*#__PURE__*/React.createElement("b", null, "24h"), " por liga\xE7\xE3o ao conv\xEAnio — ele j\xE1 entrou no painel de pend\xEAncias."), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 8,
      justifyContent: 'flex-end'
    }
  }, /*#__PURE__*/React.createElement(BtnM, {
    variant: "secondary",
    onClick: onClose
  }, "Fechar"), /*#__PURE__*/React.createElement(BtnM, {
    icon: "printer",
    onClick: () => window.open(CAPA_URL, '_blank')
  }, "Imprimir comprovante (parcial)")));
}
function ModalFaturaCompleta({
  row,
  onClose
}) {
  return /*#__PURE__*/React.createElement(Modal, {
    title: "Fatura conclu\xEDda",
    icon: "badge-check",
    width: 520,
    onClose: onClose
  }, /*#__PURE__*/React.createElement(BanM, {
    tone: "success",
    title: "Os 2 c\xF3digos foram baixados",
    style: {
      marginBottom: 10
    }
  }, row.p, " — ", row.c, ". A fatura est\xE1 completa e pronta para envio ao conv\xEAnio."), /*#__PURE__*/React.createElement("div", {
    style: {
      border: '1px solid var(--border)',
      borderRadius: 8,
      padding: '10px 12px',
      fontSize: 13,
      color: 'var(--text-body)',
      marginBottom: 14,
      display: 'flex',
      flexDirection: 'column',
      gap: 4
    }
  }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("b", {
    style: {
      color: 'var(--text-title)'
    }
  }, "1\xBA c\xF3digo:"), " AUT-114532 \xB7 baixado"), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("b", {
    style: {
      color: 'var(--text-title)'
    }
  }, "2\xBA c\xF3digo:"), " AUT-114570 \xB7 baixado")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 8,
      justifyContent: 'flex-end'
    }
  }, /*#__PURE__*/React.createElement(BtnM, {
    variant: "secondary",
    onClick: onClose
  }, "Fechar"), /*#__PURE__*/React.createElement(BtnM, {
    icon: "printer",
    onClick: () => window.open(CAPA_URL, '_blank')
  }, "Imprimir capa de faturamento")));
}
Object.assign(window, {
  Modal,
  ModalBaixa,
  ModalGlosa,
  ModalAviso,
  ModalPrimeiroAtendimento,
  ModalFaturaCompleta
});
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/faturamento/modals.jsx", error: String((e && e.message) || e) }); }

// ui_kits/faturamento/screens.jsx
try { (() => {
const DS = window.ClNicaFaturamentoDesignSystem_bd26af;
const {
  Button,
  Card,
  Input,
  Select,
  Checkbox,
  DatePicker,
  Heading,
  Label,
  DataTable,
  UrgencyDot,
  KpiCard,
  AlertBanner,
  Icon
} = DS;
const CONVENIOS = ["Unimed Costa do Sol (Padrão)", "Unimed Intercâmbio", "Amil", "Petrobras"];
const PEND = [{
  u: 'vermelho',
  p: 'Maria da Silva',
  c: 'Unimed Intercâmbio',
  d: '14/07/2026',
  g: '—',
  tipo: '2º código (24h)'
}, {
  u: 'vermelho',
  p: 'Carlos Nunes',
  c: 'Amil',
  d: '14/07/2026',
  g: '88231',
  tipo: 'Baixa da guia'
}, {
  u: 'amarelo',
  p: 'João Pereira',
  c: 'Amil',
  d: '15/07/2026',
  g: '88240',
  tipo: 'Baixa da guia'
}, {
  u: 'amarelo',
  p: 'Rita Campos',
  c: 'Petrobras',
  d: '15/07/2026',
  g: '—',
  tipo: '2º código (24h)'
}, {
  u: 'verde',
  p: 'Ana Souza',
  c: 'Petrobras',
  d: '16/07/2026',
  g: '88255',
  tipo: 'Baixa da guia'
}];
function TelaPendencias({
  onBaixa
}) {
  return /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement(Heading, null, "Pend\xEAncias de faturamento"), /*#__PURE__*/React.createElement(AlertBanner, {
    tone: "danger",
    icon: "bell",
    style: {
      marginBottom: 14
    }
  }, "Existem ", PEND.length, " guias pendentes de baixa. D\xEA baixa assim que poss\xEDvel para n\xE3o perder o faturamento."), /*#__PURE__*/React.createElement(Card, {
    title: "Guias e c\xF3digos pendentes"
  }, /*#__PURE__*/React.createElement(DataTable, {
    columns: [{
      header: '',
      width: '30px',
      render: r => /*#__PURE__*/React.createElement(UrgencyDot, {
        level: r.u
      })
    }, {
      header: 'Paciente',
      key: 'p'
    }, {
      header: 'Convênio',
      key: 'c'
    }, {
      header: 'Atendimento',
      key: 'd'
    }, {
      header: 'Guia',
      key: 'g'
    }, {
      header: 'Pendência',
      key: 'tipo'
    }, {
      header: '',
      width: '110px',
      render: r => /*#__PURE__*/React.createElement(Button, {
        size: "sm",
        onClick: () => onBaixa(r)
      }, "Dar baixa")
    }],
    rows: PEND
  })));
}
function TelaPacientes() {
  const [nome, setNome] = React.useState('');
  const [cpf, setCpf] = React.useState('');
  const [conv, setConv] = React.useState(CONVENIOS[0]);
  const [sexo, setSexo] = React.useState('Feminino');
  const [lista, setLista] = React.useState([{
    nome: 'Maria da Silva',
    cpf: '123.456.789-00',
    conv: 'Unimed Intercâmbio'
  }, {
    nome: 'João Pereira',
    cpf: '987.654.321-00',
    conv: 'Amil'
  }, {
    nome: 'Ana Souza',
    cpf: '456.789.123-00',
    conv: 'Petrobras'
  }]);
  return /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement(Heading, null, "Pacientes"), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 16,
      alignItems: 'flex-start'
    }
  }, /*#__PURE__*/React.createElement(Card, {
    title: "Cadastrar paciente",
    style: {
      width: 360,
      flexShrink: 0
    }
  }, /*#__PURE__*/React.createElement(Label, null, "Nome completo"), /*#__PURE__*/React.createElement(Input, {
    value: nome,
    onChange: setNome
  }), /*#__PURE__*/React.createElement(Label, null, "CPF"), /*#__PURE__*/React.createElement(Input, {
    value: cpf,
    onChange: setCpf
  }), /*#__PURE__*/React.createElement(Label, null, "Sexo (usado pela Petrobras)"), /*#__PURE__*/React.createElement(Select, {
    options: ["Feminino", "Masculino"],
    value: sexo,
    onChange: setSexo
  }), /*#__PURE__*/React.createElement(Label, null, "Conv\xEAnio"), /*#__PURE__*/React.createElement(Select, {
    options: CONVENIOS,
    value: conv,
    onChange: setConv
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      marginTop: 12
    }
  }, /*#__PURE__*/React.createElement(Button, {
    onClick: () => {
      if (nome) {
        setLista([{
          nome,
          cpf,
          conv
        }, ...lista]);
        setNome('');
        setCpf('');
      }
    }
  }, "Salvar paciente"))), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1
    }
  }, /*#__PURE__*/React.createElement(Label, {
    style: {
      margin: '0 0 3px'
    }
  }, "Buscar paciente (nome ou CPF)"), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 8,
      marginBottom: 10
    }
  }, /*#__PURE__*/React.createElement(Input, {
    placeholder: "Digite para buscar…",
    style: {
      flex: 1
    }
  }), /*#__PURE__*/React.createElement(Button, {
    variant: "secondary",
    icon: "refresh-cw"
  }, "Atualizar")), /*#__PURE__*/React.createElement(DataTable, {
    columns: [{
      header: 'Nome',
      key: 'nome'
    }, {
      header: 'CPF',
      key: 'cpf'
    }, {
      header: 'Convênio',
      key: 'conv'
    }],
    rows: lista
  }))));
}
function TelaNovoAtendimento({
  onGerar
}) {
  const [pac, setPac] = React.useState('Maria da Silva');
  const [conv, setConv] = React.useState(CONVENIOS[1]);
  const [data, setData] = React.useState('2026-07-16');
  const [app, setApp] = React.useState(false);
  const [obs, setObs] = React.useState('');
  return /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement(Heading, null, "Novo atendimento"), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 16,
      alignItems: 'flex-start'
    }
  }, /*#__PURE__*/React.createElement(Card, {
    style: {
      width: 380,
      flexShrink: 0
    }
  }, /*#__PURE__*/React.createElement(Label, null, "Buscar paciente (nome ou CPF)"), /*#__PURE__*/React.createElement(Input, {
    value: pac,
    onChange: setPac
  }), /*#__PURE__*/React.createElement(Label, null, "Conv\xEAnio"), /*#__PURE__*/React.createElement(Select, {
    options: CONVENIOS,
    value: conv,
    onChange: setConv
  }), /*#__PURE__*/React.createElement(Label, null, "Data do atendimento"), /*#__PURE__*/React.createElement(DatePicker, {
    value: data,
    onChange: setData
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      margin: '10px 0'
    }
  }, /*#__PURE__*/React.createElement(Checkbox, {
    checked: app,
    onChange: setApp
  }, "Possui app da Unimed (gera QR Code)")), /*#__PURE__*/React.createElement(Label, null, "Observa\xE7\xF5es"), /*#__PURE__*/React.createElement(Input, {
    multiline: true,
    rows: 3,
    value: obs,
    onChange: setObs
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      marginTop: 12,
      display: 'flex',
      gap: 8
    }
  }, /*#__PURE__*/React.createElement(Button, {
    icon: "stethoscope",
    onClick: () => onGerar({
      pac,
      conv,
      data
    })
  }, "Lan\xE7ar e gerar c\xF3digos"), /*#__PURE__*/React.createElement(Button, {
    variant: "secondary"
  }, "Limpar"))), /*#__PURE__*/React.createElement(Card, {
    title: "Regras por conv\xEAnio",
    style: {
      flex: 1
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 13,
      color: 'var(--text-body)',
      lineHeight: '22px'
    }
  }, "• Unimed Padr\xE3o: c\xF3digo no app ou por liga\xE7\xE3o.", /*#__PURE__*/React.createElement("br", null), "• Unimed Interc\xE2mbio: ", /*#__PURE__*/React.createElement("b", null, "2 c\xF3digos"), " — o 2\xBA deve ser obtido em +24h por liga\xE7\xE3o.", /*#__PURE__*/React.createElement("br", null), "• Amil: guia gerada no portal; baixa ap\xF3s atendimento.", /*#__PURE__*/React.createElement("br", null), "• Petrobras: exige sexo do paciente no cadastro."))));
}
function TelaConsultarGuias() {
  const rows = [{
    g: '88231',
    p: 'Carlos Nunes',
    c: 'Amil',
    d: '14/07/2026',
    s: 'Pendente'
  }, {
    g: '88240',
    p: 'João Pereira',
    c: 'Amil',
    d: '15/07/2026',
    s: 'Pendente'
  }, {
    g: '88255',
    p: 'Ana Souza',
    c: 'Petrobras',
    d: '16/07/2026',
    s: 'Baixada'
  }, {
    g: '88198',
    p: 'Maria da Silva',
    c: 'Unimed Intercâmbio',
    d: '10/07/2026',
    s: 'Baixada'
  }];
  return /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement(Heading, null, "Consultar guias"), /*#__PURE__*/React.createElement(Card, null, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 8,
      marginBottom: 12,
      alignItems: 'flex-end'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1
    }
  }, /*#__PURE__*/React.createElement(Label, {
    style: {
      margin: '0 0 3px'
    }
  }, "Guia ou paciente"), /*#__PURE__*/React.createElement(Input, {
    placeholder: "N\xFAmero da guia ou nome…"
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      width: 220
    }
  }, /*#__PURE__*/React.createElement(Label, {
    style: {
      margin: '0 0 3px'
    }
  }, "Conv\xEAnio"), /*#__PURE__*/React.createElement(Select, {
    options: ['Todos'].concat(CONVENIOS)
  })), /*#__PURE__*/React.createElement(Button, {
    icon: "search"
  }, "Buscar")), /*#__PURE__*/React.createElement(DataTable, {
    columns: [{
      header: 'Guia',
      key: 'g',
      width: '80px'
    }, {
      header: 'Paciente',
      key: 'p'
    }, {
      header: 'Convênio',
      key: 'c'
    }, {
      header: 'Data',
      key: 'd'
    }, {
      header: 'Situação',
      render: r => /*#__PURE__*/React.createElement("span", {
        style: {
          background: r.s === 'Baixada' ? 'var(--success-tint)' : 'var(--warning-tint)',
          color: r.s === 'Baixada' ? 'var(--success-text)' : 'var(--text-title)',
          borderRadius: 6,
          padding: '2px 8px',
          fontSize: 12,
          fontWeight: 600
        }
      }, r.s)
    }],
    rows: rows
  })));
}
function TelaAgenda() {
  const [dia, setDia] = React.useState(16);
  const rows = [{
    h: '08:00',
    p: 'Maria da Silva',
    c: 'Unimed Intercâmbio',
    t: 'Acupuntura'
  }, {
    h: '09:00',
    p: 'João Pereira',
    c: 'Amil',
    t: 'Acupuntura'
  }, {
    h: '10:30',
    p: 'Rita Campos',
    c: 'Petrobras',
    t: 'BSV'
  }, {
    h: '14:00',
    p: 'Ana Souza',
    c: 'Petrobras',
    t: 'Acupuntura'
  }];
  return /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement(Heading, null, "Agenda"), /*#__PURE__*/React.createElement(Card, null, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 10,
      marginBottom: 12
    }
  }, /*#__PURE__*/React.createElement(Button, {
    variant: "secondary",
    size: "sm",
    icon: "chevron-left",
    onClick: () => setDia(dia - 1)
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 16,
      fontWeight: 600,
      color: 'var(--text-title)',
      minWidth: 190,
      textAlign: 'center'
    }
  }, String(dia).padStart(2, '0'), "/07/2026 — quinta-feira"), /*#__PURE__*/React.createElement(Button, {
    variant: "secondary",
    size: "sm",
    icon: "chevron-right",
    onClick: () => setDia(dia + 1)
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1
    }
  }), /*#__PURE__*/React.createElement(Button, {
    icon: "plus"
  }, "Novo agendamento")), /*#__PURE__*/React.createElement(DataTable, {
    columns: [{
      header: 'Hora',
      key: 'h',
      width: '70px'
    }, {
      header: 'Paciente',
      key: 'p'
    }, {
      header: 'Convênio',
      key: 'c'
    }, {
      header: 'Procedimento',
      key: 't'
    }, {
      header: '',
      width: '130px',
      render: () => /*#__PURE__*/React.createElement(Button, {
        size: "sm",
        variant: "secondary"
      }, "Iniciar atendimento")
    }],
    rows: rows
  })));
}
function TelaParametros() {
  const [cs, setCs] = React.useState('Data Source=clinica.db');
  return /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement(Heading, null, "Par\xE2metros"), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 16,
      alignItems: 'flex-start'
    }
  }, /*#__PURE__*/React.createElement(Card, {
    title: "Banco de dados",
    style: {
      width: 420,
      flexShrink: 0
    }
  }, /*#__PURE__*/React.createElement(Label, null, "Connection string"), /*#__PURE__*/React.createElement(Input, {
    mono: true,
    value: cs,
    onChange: setCs
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      marginTop: 12,
      display: 'flex',
      gap: 8
    }
  }, /*#__PURE__*/React.createElement(Button, null, "Salvar"), /*#__PURE__*/React.createElement(Button, {
    variant: "secondary",
    icon: "refresh-cw"
  }, "Testar conex\xE3o"))), /*#__PURE__*/React.createElement(Card, {
    title: "Alertas de pend\xEAncias",
    style: {
      flex: 1
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 8
    }
  }, /*#__PURE__*/React.createElement(Checkbox, {
    checked: true
  }, "Exibir aviso de pend\xEAncias ao abrir o sistema"), /*#__PURE__*/React.createElement(Checkbox, {
    checked: true
  }, "Alertar 2\xBA c\xF3digo (Interc\xE2mbio) ap\xF3s 24h"), /*#__PURE__*/React.createElement(Checkbox, null, "Enviar resumo di\xE1rio por e-mail")))));
}
function TelaRelatorios({
  onCapa
}) {
  return /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement(Heading, null, "Relat\xF3rios"), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: 'repeat(4,1fr)',
      gap: 12,
      marginBottom: 16
    }
  }, /*#__PURE__*/React.createElement(KpiCard, {
    label: "C\xF3digos gerados",
    value: 139
  }), /*#__PURE__*/React.createElement(KpiCard, {
    label: "Baixados",
    value: 36,
    tone: "success"
  }), /*#__PURE__*/React.createElement(KpiCard, {
    label: "Pendentes",
    value: 103,
    tone: "danger"
  }), /*#__PURE__*/React.createElement(KpiCard, {
    label: "Taxa de baixa",
    value: 26,
    suffix: "%",
    tone: "brand"
  })), /*#__PURE__*/React.createElement(Card, {
    title: "Faturamento por conv\xEAnio (m\xEAs atual)"
  }, /*#__PURE__*/React.createElement(DataTable, {
    columns: [{
      header: 'Convênio',
      key: 'c'
    }, {
      header: 'Atendimentos',
      key: 'a'
    }, {
      header: 'Baixados',
      key: 'b'
    }, {
      header: 'Pendentes',
      key: 'p'
    }, {
      header: '',
      width: '130px',
      render: () => /*#__PURE__*/React.createElement(Button, {
        size: "sm",
        icon: "printer",
        onClick: onCapa
      }, "Gerar capa")
    }],
    rows: [{
      c: 'Unimed Costa do Sol (Padrão)',
      a: 48,
      b: 14,
      p: 34
    }, {
      c: 'Unimed Intercâmbio',
      a: 37,
      b: 9,
      p: 28
    }, {
      c: 'Amil',
      a: 32,
      b: 8,
      p: 24
    }, {
      c: 'Petrobras',
      a: 22,
      b: 5,
      p: 17
    }]
  })));
}
function TelaNaoRecriada({
  nome
}) {
  return /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement(Heading, null, nome), /*#__PURE__*/React.createElement(Card, null, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 13,
      color: 'var(--text-muted)'
    }
  }, "Tela existente no app, n\xE3o recriada neste UI kit. Consulte src/Clinica.Desktop/Views/ no reposit\xF3rio.")));
}
Object.assign(window, {
  TelaPendencias,
  TelaPacientes,
  TelaNovoAtendimento,
  TelaConsultarGuias,
  TelaAgenda,
  TelaParametros,
  TelaRelatorios,
  TelaNaoRecriada,
  PEND,
  CONVENIOS
});
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/faturamento/screens.jsx", error: String((e && e.message) || e) }); }

__ds_ns.Button = __ds_scope.Button;

__ds_ns.Card = __ds_scope.Card;

__ds_ns.Checkbox = __ds_scope.Checkbox;

__ds_ns.DatePicker = __ds_scope.DatePicker;

__ds_ns.Heading = __ds_scope.Heading;

__ds_ns.Icon = __ds_scope.Icon;

__ds_ns.Input = __ds_scope.Input;

__ds_ns.Label = __ds_scope.Label;

__ds_ns.Select = __ds_scope.Select;

__ds_ns.DataTable = __ds_scope.DataTable;

__ds_ns.KpiCard = __ds_scope.KpiCard;

__ds_ns.UrgencyDot = __ds_scope.UrgencyDot;

__ds_ns.AlertBanner = __ds_scope.AlertBanner;

__ds_ns.Sidebar = __ds_scope.Sidebar;

})();
