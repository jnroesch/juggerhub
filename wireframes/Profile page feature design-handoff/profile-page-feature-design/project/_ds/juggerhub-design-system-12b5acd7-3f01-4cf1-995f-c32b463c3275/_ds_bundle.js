/* @ds-bundle: {"format":3,"namespace":"JuggerHubDesignSystem_12b5ac","components":[{"name":"EventCard","sourcePath":"components/community/EventCard.jsx"},{"name":"MatchResult","sourcePath":"components/community/MatchResult.jsx"},{"name":"PlayerCard","sourcePath":"components/community/PlayerCard.jsx"},{"name":"TeamCard","sourcePath":"components/community/TeamCard.jsx"},{"name":"Alert","sourcePath":"components/feedback/Alert.jsx"},{"name":"Avatar","sourcePath":"components/feedback/Avatar.jsx"},{"name":"AvatarStack","sourcePath":"components/feedback/AvatarStack.jsx"},{"name":"Badge","sourcePath":"components/feedback/Badge.jsx"},{"name":"EmptyState","sourcePath":"components/feedback/EmptyState.jsx"},{"name":"ProgressBar","sourcePath":"components/feedback/ProgressBar.jsx"},{"name":"Spinner","sourcePath":"components/feedback/Spinner.jsx"},{"name":"Tag","sourcePath":"components/feedback/Tag.jsx"},{"name":"Button","sourcePath":"components/forms/Button.jsx"},{"name":"Checkbox","sourcePath":"components/forms/Checkbox.jsx"},{"name":"FormField","sourcePath":"components/forms/FormField.jsx"},{"name":"IconButton","sourcePath":"components/forms/IconButton.jsx"},{"name":"Input","sourcePath":"components/forms/Input.jsx"},{"name":"Radio","sourcePath":"components/forms/Radio.jsx"},{"name":"Select","sourcePath":"components/forms/Select.jsx"},{"name":"Switch","sourcePath":"components/forms/Switch.jsx"},{"name":"Textarea","sourcePath":"components/forms/Textarea.jsx"},{"name":"Accordion","sourcePath":"components/layout/Accordion.jsx"},{"name":"Card","sourcePath":"components/layout/Card.jsx"},{"name":"Stat","sourcePath":"components/layout/Stat.jsx"},{"name":"Tabs","sourcePath":"components/layout/Tabs.jsx"},{"name":"Breadcrumbs","sourcePath":"components/navigation/Breadcrumbs.jsx"},{"name":"NavBar","sourcePath":"components/navigation/NavBar.jsx"},{"name":"Pagination","sourcePath":"components/navigation/Pagination.jsx"}],"sourceHashes":{"components/community/EventCard.jsx":"dfa70abeefcb","components/community/MatchResult.jsx":"c5c9ca6ea8b7","components/community/PlayerCard.jsx":"d5beb75b248d","components/community/TeamCard.jsx":"f50e6a552757","components/feedback/Alert.jsx":"40dbbf73e8f9","components/feedback/Avatar.jsx":"18d7bcff2824","components/feedback/AvatarStack.jsx":"5f960e7e47d3","components/feedback/Badge.jsx":"ef9d46d7e99e","components/feedback/EmptyState.jsx":"32b5394016cb","components/feedback/ProgressBar.jsx":"af76d41b55f4","components/feedback/Spinner.jsx":"bd8aa6ea13bf","components/feedback/Tag.jsx":"29ee9140efc5","components/forms/Button.jsx":"887382d86657","components/forms/Checkbox.jsx":"a1d28471e14f","components/forms/FormField.jsx":"73902b0d49e8","components/forms/IconButton.jsx":"80052ed56aae","components/forms/Input.jsx":"347f5fe8c82f","components/forms/Radio.jsx":"c28efe532387","components/forms/Select.jsx":"c3bc58261e14","components/forms/Switch.jsx":"5b9e4f3beb3f","components/forms/Textarea.jsx":"87d62dde1c23","components/layout/Accordion.jsx":"4e25d874f9bd","components/layout/Card.jsx":"e2ecdb69d4c1","components/layout/Stat.jsx":"0116f38225c0","components/layout/Tabs.jsx":"81111b4c6928","components/navigation/Breadcrumbs.jsx":"608159353561","components/navigation/NavBar.jsx":"6129238789ff","components/navigation/Pagination.jsx":"1fa58a356873","ui_kits/webapp/data.js":"d0d4f46815b9","ui_kits/webapp/screens.jsx":"7d183a9fb7f8","ui_kits/webapp/shell.jsx":"c9a2f06eb108"},"inlinedExternals":[],"unexposedExports":[]} */

(() => {

const __ds_ns = (window.JuggerHubDesignSystem_12b5ac = window.JuggerHubDesignSystem_12b5ac || {});

const __ds_scope = {};

(__ds_ns.__errors = __ds_ns.__errors || []);

// components/feedback/Alert.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const tones = {
  info: {
    bg: 'var(--info-bg)',
    border: 'var(--info-border)',
    fg: 'var(--info-fg)'
  },
  success: {
    bg: 'var(--success-bg)',
    border: 'var(--success-border)',
    fg: 'var(--success-fg)'
  },
  warning: {
    bg: 'var(--warning-bg)',
    border: 'var(--warning-border)',
    fg: 'var(--warning-fg)'
  },
  danger: {
    bg: 'var(--danger-bg)',
    border: 'var(--danger-border)',
    fg: 'var(--danger-fg)'
  }
};
function Alert({
  tone = 'info',
  title,
  children,
  icon,
  onDismiss,
  style,
  ...rest
}) {
  const t = tones[tone] || tones.info;
  return /*#__PURE__*/React.createElement("div", _extends({
    role: "status",
    style: {
      display: 'flex',
      gap: '12px',
      padding: '14px 16px',
      background: t.bg,
      border: `1px solid ${t.border}`,
      borderRadius: 'var(--radius-lg)',
      ...style
    }
  }, rest), icon && /*#__PURE__*/React.createElement("span", {
    style: {
      color: t.fg,
      flexShrink: 0,
      display: 'inline-flex',
      marginTop: '1px'
    }
  }, icon), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      minWidth: 0
    }
  }, title && /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 'var(--weight-bold)',
      color: 'var(--text-heading)',
      marginBottom: children ? '2px' : 0
    }
  }, title), children && /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 'var(--text-body-sm)',
      color: 'var(--text-body)'
    }
  }, children)), onDismiss && /*#__PURE__*/React.createElement("button", {
    type: "button",
    "aria-label": "Dismiss",
    onClick: onDismiss,
    style: {
      border: 'none',
      background: 'transparent',
      color: 'var(--text-muted)',
      cursor: 'pointer',
      fontSize: '18px',
      lineHeight: 1,
      padding: 0,
      flexShrink: 0
    }
  }, "\xD7"));
}
Object.assign(__ds_scope, { Alert });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/Alert.jsx", error: String((e && e.message) || e) }); }

// components/feedback/Avatar.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const sizes = {
  xs: 24,
  sm: 32,
  md: 40,
  lg: 56,
  xl: 80
};
// Warm, on-brand fallback backgrounds derived by name hash
const palette = [['var(--coral-1)', 'var(--coral-7)'], ['var(--teal-1)', 'var(--teal-7)'], ['var(--lemon-1)', 'var(--lemon-6)'], ['var(--blue-1)', 'var(--blue-6)'], ['var(--sand-3)', 'var(--sand-8)']];
function initials(name = '') {
  return name.trim().split(/\s+/).slice(0, 2).map(w => w[0] || '').join('').toUpperCase();
}
function hash(str = '') {
  let h = 0;
  for (let i = 0; i < str.length; i++) h = h * 31 + str.charCodeAt(i) >>> 0;
  return h;
}
function Avatar({
  src,
  name = '',
  size = 'md',
  square = false,
  ring = false,
  style,
  ...rest
}) {
  const px = typeof size === 'number' ? size : sizes[size] || sizes.md;
  const [bg, fg] = palette[hash(name) % palette.length];
  return /*#__PURE__*/React.createElement("span", _extends({
    title: name || undefined,
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      justifyContent: 'center',
      width: px,
      height: px,
      flexShrink: 0,
      overflow: 'hidden',
      borderRadius: square ? 'var(--radius-md)' : 'var(--radius-pill)',
      background: src ? 'var(--surface-muted)' : bg,
      color: fg,
      fontFamily: 'var(--font-body)',
      fontWeight: 'var(--weight-bold)',
      fontSize: Math.round(px * 0.4),
      lineHeight: 1,
      boxShadow: ring ? '0 0 0 2px var(--surface-card), 0 0 0 4px var(--brand-primary)' : undefined,
      ...style
    }
  }, rest), src ? /*#__PURE__*/React.createElement("img", {
    src: src,
    alt: name,
    style: {
      width: '100%',
      height: '100%',
      objectFit: 'cover'
    }
  }) : initials(name));
}
Object.assign(__ds_scope, { Avatar });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/Avatar.jsx", error: String((e && e.message) || e) }); }

// components/feedback/AvatarStack.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const sizes = {
  xs: 24,
  sm: 32,
  md: 40,
  lg: 56,
  xl: 80
};
function AvatarStack({
  people = [],
  size = 'md',
  max = 4,
  style,
  ...rest
}) {
  const px = typeof size === 'number' ? size : sizes[size] || sizes.md;
  const shown = people.slice(0, max);
  const extra = people.length - shown.length;
  const overlap = Math.round(px * 0.32);
  return /*#__PURE__*/React.createElement("div", _extends({
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      ...style
    }
  }, rest), shown.map((p, i) => /*#__PURE__*/React.createElement("span", {
    key: i,
    style: {
      marginLeft: i === 0 ? 0 : -overlap,
      borderRadius: 'var(--radius-pill)',
      boxShadow: '0 0 0 2px var(--surface-card)'
    }
  }, /*#__PURE__*/React.createElement(__ds_scope.Avatar, _extends({}, p, {
    size: size
  })))), extra > 0 && /*#__PURE__*/React.createElement("span", {
    style: {
      marginLeft: -overlap,
      width: px,
      height: px,
      borderRadius: 'var(--radius-pill)',
      display: 'inline-flex',
      alignItems: 'center',
      justifyContent: 'center',
      background: 'var(--surface-muted)',
      color: 'var(--text-body)',
      fontFamily: 'var(--font-body)',
      fontWeight: 'var(--weight-bold)',
      fontSize: Math.round(px * 0.36),
      boxShadow: '0 0 0 2px var(--surface-card)'
    }
  }, "+", extra));
}
Object.assign(__ds_scope, { AvatarStack });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/AvatarStack.jsx", error: String((e && e.message) || e) }); }

// components/feedback/Badge.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const tones = {
  neutral: {
    bg: 'var(--surface-muted)',
    fg: 'var(--text-body)'
  },
  primary: {
    bg: 'var(--coral-1)',
    fg: 'var(--coral-7)'
  },
  secondary: {
    bg: 'var(--teal-1)',
    fg: 'var(--teal-7)'
  },
  success: {
    bg: 'var(--success-bg)',
    fg: 'var(--success-fg)'
  },
  warning: {
    bg: 'var(--warning-bg)',
    fg: 'var(--warning-fg)'
  },
  danger: {
    bg: 'var(--danger-bg)',
    fg: 'var(--danger-fg)'
  },
  highlight: {
    bg: 'var(--lemon-1)',
    fg: 'var(--lemon-6)'
  }
};
function Badge({
  children,
  tone = 'neutral',
  size = 'md',
  dot = false,
  leadingVisual,
  style,
  ...rest
}) {
  const t = tones[tone] || tones.neutral;
  const pad = size === 'sm' ? '2px 8px' : '4px 12px';
  const fs = size === 'sm' ? 'var(--text-caption)' : 'var(--text-body-sm)';
  return /*#__PURE__*/React.createElement("span", _extends({
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      gap: '6px',
      padding: pad,
      background: t.bg,
      color: t.fg,
      fontFamily: 'var(--font-body)',
      fontSize: fs,
      fontWeight: 'var(--weight-semibold)',
      lineHeight: 1.4,
      borderRadius: 'var(--radius-pill)',
      whiteSpace: 'nowrap',
      ...style
    }
  }, rest), dot && /*#__PURE__*/React.createElement("span", {
    style: {
      width: '7px',
      height: '7px',
      borderRadius: '50%',
      background: t.fg,
      flexShrink: 0
    }
  }), leadingVisual, children);
}
Object.assign(__ds_scope, { Badge });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/Badge.jsx", error: String((e && e.message) || e) }); }

// components/feedback/EmptyState.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function EmptyState({
  illustration,
  icon,
  title,
  description,
  action,
  style,
  ...rest
}) {
  return /*#__PURE__*/React.createElement("div", _extends({
    style: {
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      textAlign: 'center',
      padding: '40px 24px',
      gap: '6px',
      ...style
    }
  }, rest), illustration ? /*#__PURE__*/React.createElement("img", {
    src: illustration,
    alt: "",
    style: {
      width: '160px',
      height: 'auto',
      marginBottom: '8px'
    }
  }) : icon ? /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      justifyContent: 'center',
      width: '64px',
      height: '64px',
      marginBottom: '8px',
      borderRadius: 'var(--radius-pill)',
      background: 'var(--surface-accent-soft)',
      color: 'var(--brand-primary)'
    }
  }, icon) : null, title && /*#__PURE__*/React.createElement("h3", {
    style: {
      margin: 0,
      fontSize: 'var(--text-h4)',
      color: 'var(--text-heading)'
    }
  }, title), description && /*#__PURE__*/React.createElement("p", {
    style: {
      margin: 0,
      maxWidth: '380px',
      color: 'var(--text-muted)',
      fontSize: 'var(--text-body-md)'
    }
  }, description), action && /*#__PURE__*/React.createElement("div", {
    style: {
      marginTop: '14px'
    }
  }, action));
}
Object.assign(__ds_scope, { EmptyState });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/EmptyState.jsx", error: String((e && e.message) || e) }); }

// components/feedback/ProgressBar.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const tones = {
  primary: 'var(--brand-primary)',
  secondary: 'var(--brand-secondary)',
  highlight: 'var(--lemon-3)',
  success: 'var(--green-4)'
};
function ProgressBar({
  value = 0,
  max = 100,
  tone = 'primary',
  size = 'md',
  showLabel = false,
  label,
  style,
  ...rest
}) {
  const pct = Math.max(0, Math.min(100, value / max * 100));
  const h = size === 'sm' ? 6 : size === 'lg' ? 14 : 10;
  return /*#__PURE__*/React.createElement("div", _extends({
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: '6px',
      ...style
    }
  }, rest), (showLabel || label) && /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      justifyContent: 'space-between',
      fontSize: 'var(--text-body-sm)',
      color: 'var(--text-muted)'
    }
  }, /*#__PURE__*/React.createElement("span", null, label), showLabel && /*#__PURE__*/React.createElement("span", {
    style: {
      fontFamily: 'var(--font-mono)',
      color: 'var(--text-body)'
    }
  }, Math.round(pct), "%")), /*#__PURE__*/React.createElement("div", {
    role: "progressbar",
    "aria-valuenow": value,
    "aria-valuemax": max,
    style: {
      height: h,
      background: 'var(--surface-muted)',
      borderRadius: 'var(--radius-pill)',
      overflow: 'hidden'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: `${pct}%`,
      height: '100%',
      background: tones[tone] || tones.primary,
      borderRadius: 'var(--radius-pill)',
      transition: 'width var(--duration-slow) var(--ease-out)'
    }
  })));
}
Object.assign(__ds_scope, { ProgressBar });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/ProgressBar.jsx", error: String((e && e.message) || e) }); }

// components/feedback/Spinner.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function Spinner({
  size = 24,
  tone = 'primary',
  label = 'Loading',
  style,
  ...rest
}) {
  const color = tone === 'primary' ? 'var(--brand-primary)' : tone === 'secondary' ? 'var(--brand-secondary)' : 'var(--text-muted)';
  const id = React.useId().replace(/:/g, '');
  return /*#__PURE__*/React.createElement("span", _extends({
    role: "status",
    "aria-label": label,
    style: {
      display: 'inline-flex',
      ...style
    }
  }, rest), /*#__PURE__*/React.createElement("span", {
    style: {
      width: size,
      height: size,
      borderRadius: '50%',
      border: `${Math.max(2, Math.round(size / 10))}px solid var(--surface-muted)`,
      borderTopColor: color,
      display: 'inline-block',
      animation: `jh-spin-${id} 0.7s linear infinite`
    }
  }), /*#__PURE__*/React.createElement("style", null, `@keyframes jh-spin-${id}{to{transform:rotate(360deg)}}`));
}
Object.assign(__ds_scope, { Spinner });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/Spinner.jsx", error: String((e && e.message) || e) }); }

// components/feedback/Tag.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function Tag({
  children,
  onRemove,
  interactive = false,
  selected = false,
  leadingVisual,
  style,
  ...rest
}) {
  const [hover, setHover] = React.useState(false);
  const bg = selected ? 'var(--teal-1)' : hover && interactive ? 'var(--surface-muted)' : 'var(--surface-sunken)';
  const fg = selected ? 'var(--teal-7)' : 'var(--text-body)';
  const border = selected ? 'var(--teal-2)' : 'var(--border-muted)';
  return /*#__PURE__*/React.createElement("span", _extends({
    onMouseEnter: () => setHover(true),
    onMouseLeave: () => setHover(false),
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      gap: '6px',
      padding: '5px 12px',
      background: bg,
      color: fg,
      border: `1px solid ${border}`,
      fontFamily: 'var(--font-body)',
      fontSize: 'var(--text-body-sm)',
      fontWeight: 'var(--weight-medium)',
      borderRadius: 'var(--radius-pill)',
      cursor: interactive ? 'pointer' : 'default',
      transition: 'background var(--duration-fast), border-color var(--duration-fast)',
      ...style
    }
  }, rest), leadingVisual, children, onRemove && /*#__PURE__*/React.createElement("button", {
    type: "button",
    "aria-label": "Remove",
    onClick: e => {
      e.stopPropagation();
      onRemove(e);
    },
    style: {
      display: 'inline-flex',
      border: 'none',
      background: 'transparent',
      color: 'var(--text-muted)',
      cursor: 'pointer',
      padding: 0,
      fontSize: '14px',
      lineHeight: 1
    }
  }, "\xD7"));
}
Object.assign(__ds_scope, { Tag });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/Tag.jsx", error: String((e && e.message) || e) }); }

// components/forms/Button.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const sizeMap = {
  sm: {
    fontSize: 'var(--text-body-sm)',
    padding: '0 14px',
    height: '36px',
    gap: '6px',
    radius: 'var(--radius-sm)'
  },
  md: {
    fontSize: 'var(--text-body-md)',
    padding: '0 20px',
    height: '44px',
    gap: '8px',
    radius: 'var(--radius-md)'
  },
  lg: {
    fontSize: 'var(--text-body-lg)',
    padding: '0 28px',
    height: '52px',
    gap: '10px',
    radius: 'var(--radius-md)'
  }
};
const variantMap = {
  primary: {
    base: {
      background: 'var(--brand-primary)',
      color: 'var(--text-on-accent)',
      border: '1px solid transparent'
    },
    hover: {
      background: 'var(--brand-primary-hover)',
      boxShadow: 'var(--shadow-coral)'
    },
    active: {
      background: 'var(--brand-primary-active)'
    }
  },
  secondary: {
    base: {
      background: 'var(--brand-secondary)',
      color: 'var(--text-on-accent)',
      border: '1px solid transparent'
    },
    hover: {
      background: 'var(--brand-secondary-hover)',
      boxShadow: 'var(--shadow-teal)'
    },
    active: {
      background: 'var(--teal-6)'
    }
  },
  subtle: {
    base: {
      background: 'var(--surface-muted)',
      color: 'var(--text-heading)',
      border: '1px solid transparent'
    },
    hover: {
      background: 'var(--sand-3)'
    },
    active: {
      background: 'var(--sand-4)'
    }
  },
  outline: {
    base: {
      background: 'var(--surface-card)',
      color: 'var(--text-heading)',
      border: '1px solid var(--border-default)'
    },
    hover: {
      background: 'var(--surface-sunken)',
      borderColor: 'var(--border-strong)'
    },
    active: {
      background: 'var(--surface-muted)'
    }
  },
  ghost: {
    base: {
      background: 'transparent',
      color: 'var(--brand-primary-active)',
      border: '1px solid transparent'
    },
    hover: {
      background: 'var(--surface-accent-soft)'
    },
    active: {
      background: 'var(--coral-1)'
    }
  },
  danger: {
    base: {
      background: 'var(--danger-bg)',
      color: 'var(--danger-fg)',
      border: '1px solid var(--danger-border)'
    },
    hover: {
      background: 'var(--red-1)'
    },
    active: {
      background: 'var(--red-1)'
    }
  }
};
function Button({
  children,
  variant = 'primary',
  size = 'md',
  leadingVisual,
  trailingVisual,
  block = false,
  disabled = false,
  type = 'button',
  onClick,
  style,
  ...rest
}) {
  const [hover, setHover] = React.useState(false);
  const [active, setActive] = React.useState(false);
  const s = sizeMap[size] || sizeMap.md;
  const v = variantMap[variant] || variantMap.primary;
  const composed = {
    display: block ? 'flex' : 'inline-flex',
    width: block ? '100%' : undefined,
    alignItems: 'center',
    justifyContent: 'center',
    gap: s.gap,
    height: s.height,
    padding: s.padding,
    fontFamily: 'var(--font-body)',
    fontSize: s.fontSize,
    fontWeight: 'var(--weight-semibold)',
    lineHeight: 1,
    letterSpacing: '0.01em',
    borderRadius: s.radius,
    cursor: disabled ? 'not-allowed' : 'pointer',
    opacity: disabled ? 0.5 : 1,
    transition: 'background var(--duration-fast) var(--ease-standard), box-shadow var(--duration-base) var(--ease-standard), transform var(--duration-fast) var(--ease-standard)',
    transform: active && !disabled ? 'translateY(1px) scale(0.99)' : 'none',
    whiteSpace: 'nowrap',
    ...v.base,
    ...(!disabled && hover ? v.hover : null),
    ...(!disabled && active ? v.active : null),
    ...style
  };
  return /*#__PURE__*/React.createElement("button", _extends({
    type: type,
    disabled: disabled,
    onClick: onClick,
    onMouseEnter: () => setHover(true),
    onMouseLeave: () => {
      setHover(false);
      setActive(false);
    },
    onMouseDown: () => setActive(true),
    onMouseUp: () => setActive(false),
    style: composed
  }, rest), leadingVisual && /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'inline-flex',
      alignItems: 'center'
    }
  }, leadingVisual), children, trailingVisual && /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'inline-flex',
      alignItems: 'center'
    }
  }, trailingVisual));
}
Object.assign(__ds_scope, { Button });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/Button.jsx", error: String((e && e.message) || e) }); }

// components/forms/Checkbox.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function Checkbox({
  label,
  description,
  checked,
  defaultChecked,
  disabled = false,
  onChange,
  id,
  style,
  ...rest
}) {
  const inputId = id || React.useId();
  return /*#__PURE__*/React.createElement("label", {
    htmlFor: inputId,
    style: {
      display: 'flex',
      alignItems: description ? 'flex-start' : 'center',
      gap: '10px',
      cursor: disabled ? 'not-allowed' : 'pointer',
      opacity: disabled ? 0.6 : 1,
      ...style
    }
  }, /*#__PURE__*/React.createElement("input", _extends({
    id: inputId,
    type: "checkbox",
    checked: checked,
    defaultChecked: defaultChecked,
    disabled: disabled,
    onChange: onChange,
    style: {
      appearance: 'none',
      WebkitAppearance: 'none',
      flexShrink: 0,
      width: '20px',
      height: '20px',
      marginTop: description ? '2px' : 0,
      border: '2px solid var(--border-strong)',
      borderRadius: 'var(--radius-xs)',
      background: 'var(--surface-card)',
      cursor: 'inherit',
      display: 'grid',
      placeContent: 'center',
      transition: 'background var(--duration-fast), border-color var(--duration-fast)'
    }
  }, rest)), (label || description) && /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: '2px'
    }
  }, label && /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--text-body-md)',
      color: 'var(--text-heading)',
      fontWeight: 'var(--weight-medium)',
      lineHeight: 1.4
    }
  }, label), description && /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--text-body-sm)',
      color: 'var(--text-muted)'
    }
  }, description)), /*#__PURE__*/React.createElement("style", null, `
        #${CSS.escape(inputId)}:checked { background: var(--brand-primary); border-color: var(--brand-primary); }
        #${CSS.escape(inputId)}:checked::after { content: ''; width: 6px; height: 10px; border: solid var(--white); border-width: 0 2px 2px 0; transform: rotate(45deg) translate(-1px,-1px); }
        #${CSS.escape(inputId)}:focus-visible { box-shadow: var(--focus-ring); outline: none; }
      `));
}
Object.assign(__ds_scope, { Checkbox });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/Checkbox.jsx", error: String((e && e.message) || e) }); }

// components/forms/FormField.jsx
try { (() => {
function FormField({
  label,
  htmlFor,
  hint,
  error,
  required = false,
  children,
  style
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: '6px',
      ...style
    }
  }, label && /*#__PURE__*/React.createElement("label", {
    htmlFor: htmlFor,
    style: {
      fontSize: 'var(--text-body-sm)',
      fontWeight: 'var(--weight-semibold)',
      color: 'var(--text-heading)'
    }
  }, label, required && /*#__PURE__*/React.createElement("span", {
    style: {
      color: 'var(--brand-primary)',
      marginLeft: '3px'
    }
  }, "*")), children, error ? /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--text-body-sm)',
      color: 'var(--danger-fg)'
    }
  }, error) : hint ? /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--text-body-sm)',
      color: 'var(--text-muted)'
    }
  }, hint) : null);
}
Object.assign(__ds_scope, { FormField });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/FormField.jsx", error: String((e && e.message) || e) }); }

// components/forms/IconButton.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const sizeMap = {
  sm: {
    box: '36px',
    radius: 'var(--radius-sm)'
  },
  md: {
    box: '44px',
    radius: 'var(--radius-md)'
  },
  lg: {
    box: '52px',
    radius: 'var(--radius-md)'
  }
};
const variantMap = {
  primary: {
    base: {
      background: 'var(--brand-primary)',
      color: 'var(--text-on-accent)',
      border: '1px solid transparent'
    },
    hover: {
      background: 'var(--brand-primary-hover)'
    }
  },
  subtle: {
    base: {
      background: 'var(--surface-muted)',
      color: 'var(--text-heading)',
      border: '1px solid transparent'
    },
    hover: {
      background: 'var(--sand-3)'
    }
  },
  outline: {
    base: {
      background: 'var(--surface-card)',
      color: 'var(--text-heading)',
      border: '1px solid var(--border-default)'
    },
    hover: {
      background: 'var(--surface-sunken)'
    }
  },
  ghost: {
    base: {
      background: 'transparent',
      color: 'var(--text-muted)',
      border: '1px solid transparent'
    },
    hover: {
      background: 'var(--surface-sunken)',
      color: 'var(--text-heading)'
    }
  }
};
function IconButton({
  icon,
  label,
  variant = 'ghost',
  size = 'md',
  round = false,
  disabled = false,
  onClick,
  style,
  ...rest
}) {
  const [hover, setHover] = React.useState(false);
  const s = sizeMap[size] || sizeMap.md;
  const v = variantMap[variant] || variantMap.ghost;
  return /*#__PURE__*/React.createElement("button", _extends({
    type: "button",
    "aria-label": label,
    title: label,
    disabled: disabled,
    onClick: onClick,
    onMouseEnter: () => setHover(true),
    onMouseLeave: () => setHover(false),
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      justifyContent: 'center',
      width: s.box,
      height: s.box,
      borderRadius: round ? 'var(--radius-pill)' : s.radius,
      cursor: disabled ? 'not-allowed' : 'pointer',
      opacity: disabled ? 0.5 : 1,
      transition: 'background var(--duration-fast) var(--ease-standard), color var(--duration-fast) var(--ease-standard)',
      ...v.base,
      ...(!disabled && hover ? v.hover : null),
      ...style
    }
  }, rest), icon);
}
Object.assign(__ds_scope, { IconButton });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/IconButton.jsx", error: String((e && e.message) || e) }); }

// components/forms/Input.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const heights = {
  sm: '36px',
  md: '44px',
  lg: '52px'
};
function Input({
  size = 'md',
  invalid = false,
  leadingVisual,
  trailingVisual,
  disabled = false,
  style,
  ...rest
}) {
  const [focus, setFocus] = React.useState(false);
  const borderColor = invalid ? 'var(--danger-border)' : focus ? 'var(--border-focus)' : 'var(--border-default)';
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: '8px',
      height: heights[size] || heights.md,
      padding: '0 14px',
      background: disabled ? 'var(--surface-sunken)' : 'var(--surface-card)',
      border: `1px solid ${borderColor}`,
      borderRadius: 'var(--radius-md)',
      boxShadow: focus ? 'var(--focus-ring)' : 'none',
      transition: 'border-color var(--duration-fast) var(--ease-standard), box-shadow var(--duration-fast) var(--ease-standard)',
      opacity: disabled ? 0.6 : 1,
      ...style
    }
  }, leadingVisual && /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'inline-flex',
      color: 'var(--text-muted)',
      flexShrink: 0
    }
  }, leadingVisual), /*#__PURE__*/React.createElement("input", _extends({
    disabled: disabled,
    onFocus: () => setFocus(true),
    onBlur: () => setFocus(false),
    style: {
      flex: 1,
      minWidth: 0,
      border: 'none',
      outline: 'none',
      background: 'transparent',
      fontFamily: 'var(--font-body)',
      fontSize: 'var(--text-body-md)',
      color: 'var(--text-heading)'
    }
  }, rest)), trailingVisual && /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'inline-flex',
      color: 'var(--text-muted)',
      flexShrink: 0
    }
  }, trailingVisual));
}
Object.assign(__ds_scope, { Input });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/Input.jsx", error: String((e && e.message) || e) }); }

// components/forms/Radio.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function Radio({
  label,
  description,
  name,
  value,
  checked,
  defaultChecked,
  disabled = false,
  onChange,
  id,
  style,
  ...rest
}) {
  const inputId = id || React.useId();
  return /*#__PURE__*/React.createElement("label", {
    htmlFor: inputId,
    style: {
      display: 'flex',
      alignItems: description ? 'flex-start' : 'center',
      gap: '10px',
      cursor: disabled ? 'not-allowed' : 'pointer',
      opacity: disabled ? 0.6 : 1,
      ...style
    }
  }, /*#__PURE__*/React.createElement("input", _extends({
    id: inputId,
    type: "radio",
    name: name,
    value: value,
    checked: checked,
    defaultChecked: defaultChecked,
    disabled: disabled,
    onChange: onChange,
    style: {
      appearance: 'none',
      WebkitAppearance: 'none',
      flexShrink: 0,
      width: '20px',
      height: '20px',
      marginTop: description ? '2px' : 0,
      border: '2px solid var(--border-strong)',
      borderRadius: 'var(--radius-pill)',
      background: 'var(--surface-card)',
      cursor: 'inherit',
      display: 'grid',
      placeContent: 'center',
      transition: 'border-color var(--duration-fast)'
    }
  }, rest)), (label || description) && /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: '2px'
    }
  }, label && /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--text-body-md)',
      color: 'var(--text-heading)',
      fontWeight: 'var(--weight-medium)',
      lineHeight: 1.4
    }
  }, label), description && /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--text-body-sm)',
      color: 'var(--text-muted)'
    }
  }, description)), /*#__PURE__*/React.createElement("style", null, `
        #${CSS.escape(inputId)}:checked { border-color: var(--brand-primary); border-width: 6px; }
        #${CSS.escape(inputId)}:focus-visible { box-shadow: var(--focus-ring); outline: none; }
      `));
}
Object.assign(__ds_scope, { Radio });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/Radio.jsx", error: String((e && e.message) || e) }); }

// components/forms/Select.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const heights = {
  sm: '36px',
  md: '44px',
  lg: '52px'
};
function Select({
  size = 'md',
  invalid = false,
  disabled = false,
  children,
  style,
  ...rest
}) {
  const [focus, setFocus] = React.useState(false);
  const borderColor = invalid ? 'var(--danger-border)' : focus ? 'var(--border-focus)' : 'var(--border-default)';
  return /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'relative',
      display: 'inline-flex',
      width: style?.width || '100%'
    }
  }, /*#__PURE__*/React.createElement("select", _extends({
    disabled: disabled,
    onFocus: () => setFocus(true),
    onBlur: () => setFocus(false),
    style: {
      appearance: 'none',
      WebkitAppearance: 'none',
      width: '100%',
      height: heights[size] || heights.md,
      padding: '0 40px 0 14px',
      background: disabled ? 'var(--surface-sunken)' : 'var(--surface-card)',
      border: `1px solid ${borderColor}`,
      borderRadius: 'var(--radius-md)',
      boxShadow: focus ? 'var(--focus-ring)' : 'none',
      fontFamily: 'var(--font-body)',
      fontSize: 'var(--text-body-md)',
      color: 'var(--text-heading)',
      cursor: disabled ? 'not-allowed' : 'pointer',
      outline: 'none',
      transition: 'border-color var(--duration-fast) var(--ease-standard), box-shadow var(--duration-fast) var(--ease-standard)',
      opacity: disabled ? 0.6 : 1,
      ...style
    }
  }, rest), children), /*#__PURE__*/React.createElement("span", {
    "aria-hidden": "true",
    style: {
      position: 'absolute',
      right: '14px',
      top: '50%',
      transform: 'translateY(-50%)',
      pointerEvents: 'none',
      color: 'var(--text-muted)',
      fontSize: '12px',
      lineHeight: 1
    }
  }, "\u25BE"));
}
Object.assign(__ds_scope, { Select });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/Select.jsx", error: String((e && e.message) || e) }); }

// components/forms/Switch.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function Switch({
  checked,
  defaultChecked = false,
  disabled = false,
  onChange,
  label,
  id,
  style,
  ...rest
}) {
  const isControlled = checked !== undefined;
  const [on, setOn] = React.useState(defaultChecked);
  const value = isControlled ? checked : on;
  const inputId = id || React.useId();
  const toggle = e => {
    if (disabled) return;
    if (!isControlled) setOn(v => !v);
    onChange && onChange(!value, e);
  };
  return /*#__PURE__*/React.createElement("label", {
    htmlFor: inputId,
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      gap: '10px',
      cursor: disabled ? 'not-allowed' : 'pointer',
      opacity: disabled ? 0.6 : 1,
      ...style
    }
  }, /*#__PURE__*/React.createElement("button", _extends({
    id: inputId,
    type: "button",
    role: "switch",
    "aria-checked": value,
    disabled: disabled,
    onClick: toggle,
    style: {
      position: 'relative',
      width: '44px',
      height: '26px',
      flexShrink: 0,
      borderRadius: 'var(--radius-pill)',
      border: 'none',
      cursor: 'inherit',
      padding: 0,
      background: value ? 'var(--brand-secondary)' : 'var(--sand-4)',
      transition: 'background var(--duration-base) var(--ease-standard)'
    }
  }, rest), /*#__PURE__*/React.createElement("span", {
    style: {
      position: 'absolute',
      top: '3px',
      left: value ? '21px' : '3px',
      width: '20px',
      height: '20px',
      background: 'var(--white)',
      borderRadius: 'var(--radius-pill)',
      boxShadow: 'var(--shadow-sm)',
      transition: 'left var(--duration-base) var(--ease-bounce)'
    }
  })), label && /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--text-body-md)',
      color: 'var(--text-heading)',
      fontWeight: 'var(--weight-medium)'
    }
  }, label));
}
Object.assign(__ds_scope, { Switch });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/Switch.jsx", error: String((e && e.message) || e) }); }

// components/forms/Textarea.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function Textarea({
  invalid = false,
  disabled = false,
  rows = 4,
  style,
  ...rest
}) {
  const [focus, setFocus] = React.useState(false);
  const borderColor = invalid ? 'var(--danger-border)' : focus ? 'var(--border-focus)' : 'var(--border-default)';
  return /*#__PURE__*/React.createElement("textarea", _extends({
    rows: rows,
    disabled: disabled,
    onFocus: () => setFocus(true),
    onBlur: () => setFocus(false),
    style: {
      display: 'block',
      width: '100%',
      padding: '12px 14px',
      resize: 'vertical',
      background: disabled ? 'var(--surface-sunken)' : 'var(--surface-card)',
      border: `1px solid ${borderColor}`,
      borderRadius: 'var(--radius-md)',
      boxShadow: focus ? 'var(--focus-ring)' : 'none',
      fontFamily: 'var(--font-body)',
      fontSize: 'var(--text-body-md)',
      lineHeight: 'var(--leading-normal)',
      color: 'var(--text-heading)',
      outline: 'none',
      transition: 'border-color var(--duration-fast) var(--ease-standard), box-shadow var(--duration-fast) var(--ease-standard)',
      opacity: disabled ? 0.6 : 1,
      ...style
    }
  }, rest));
}
Object.assign(__ds_scope, { Textarea });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/Textarea.jsx", error: String((e && e.message) || e) }); }

// components/layout/Accordion.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function Item({
  item,
  open,
  onToggle
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      borderBottom: '1px solid var(--border-muted)'
    }
  }, /*#__PURE__*/React.createElement("button", {
    type: "button",
    onClick: onToggle,
    "aria-expanded": open,
    style: {
      display: 'flex',
      width: '100%',
      alignItems: 'center',
      justifyContent: 'space-between',
      gap: '12px',
      padding: '18px 4px',
      border: 'none',
      background: 'transparent',
      cursor: 'pointer',
      textAlign: 'left',
      fontFamily: 'var(--font-body)',
      fontSize: 'var(--text-body-lg)',
      fontWeight: 'var(--weight-semibold)',
      color: 'var(--text-heading)'
    }
  }, /*#__PURE__*/React.createElement("span", null, item.question), /*#__PURE__*/React.createElement("span", {
    "aria-hidden": "true",
    style: {
      flexShrink: 0,
      color: 'var(--brand-primary)',
      fontSize: '20px',
      lineHeight: 1,
      transform: open ? 'rotate(45deg)' : 'none',
      transition: 'transform var(--duration-base) var(--ease-standard)'
    }
  }, "+")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateRows: open ? '1fr' : '0fr',
      transition: 'grid-template-rows var(--duration-base) var(--ease-standard)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      overflow: 'hidden'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '0 4px 18px',
      color: 'var(--text-body)',
      fontSize: 'var(--text-body-md)'
    }
  }, item.answer))));
}
function Accordion({
  items = [],
  allowMultiple = false,
  style,
  ...rest
}) {
  const [open, setOpen] = React.useState([]);
  const toggle = i => {
    setOpen(prev => prev.includes(i) ? prev.filter(x => x !== i) : allowMultiple ? [...prev, i] : [i]);
  };
  return /*#__PURE__*/React.createElement("div", _extends({
    style: {
      borderTop: '1px solid var(--border-muted)',
      ...style
    }
  }, rest), items.map((item, i) => /*#__PURE__*/React.createElement(Item, {
    key: i,
    item: item,
    open: open.includes(i),
    onToggle: () => toggle(i)
  })));
}
Object.assign(__ds_scope, { Accordion });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/layout/Accordion.jsx", error: String((e && e.message) || e) }); }

// components/layout/Card.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const pads = {
  none: '0',
  sm: '16px',
  md: '20px',
  lg: '28px'
};
function Card({
  children,
  interactive = false,
  as = 'div',
  padding = 'md',
  elevated = false,
  accent = false,
  style,
  ...rest
}) {
  const [hover, setHover] = React.useState(false);
  const Comp = as;
  return /*#__PURE__*/React.createElement(Comp, _extends({
    onMouseEnter: interactive ? () => setHover(true) : undefined,
    onMouseLeave: interactive ? () => setHover(false) : undefined,
    style: {
      display: 'block',
      background: 'var(--surface-card)',
      border: `1px solid ${accent ? 'var(--border-accent)' : 'var(--border-muted)'}`,
      borderRadius: 'var(--radius-lg)',
      padding: pads[padding] ?? pads.md,
      boxShadow: hover ? 'var(--shadow-lg)' : elevated ? 'var(--shadow-md)' : 'var(--shadow-sm)',
      transform: interactive && hover ? 'translateY(-3px)' : 'none',
      transition: 'box-shadow var(--duration-base) var(--ease-standard), transform var(--duration-base) var(--ease-out), border-color var(--duration-base)',
      cursor: interactive ? 'pointer' : 'default',
      textDecoration: 'none',
      color: 'inherit',
      overflow: 'hidden',
      ...style
    }
  }, rest), children);
}
Object.assign(__ds_scope, { Card });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/layout/Card.jsx", error: String((e && e.message) || e) }); }

// components/community/EventCard.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function EventCard({
  title,
  date,
  month,
  day,
  time,
  location,
  kind = 'Training',
  attendees = [],
  attendeeCount,
  spotsLeft,
  going = false,
  onRSVP,
  href,
  style,
  ...rest
}) {
  const count = attendeeCount ?? attendees.length;
  const kindTone = kind === 'Tournament' ? 'primary' : kind === 'Match' ? 'secondary' : 'highlight';
  return /*#__PURE__*/React.createElement(__ds_scope.Card, _extends({
    padding: "none",
    style: style
  }, rest), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: '16px',
      padding: '18px 20px'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      flexShrink: 0,
      width: '64px',
      textAlign: 'center',
      borderRadius: 'var(--radius-md)',
      overflow: 'hidden',
      border: '1px solid var(--border-muted)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'var(--coral-4)',
      color: 'var(--white)',
      fontSize: 'var(--text-caption)',
      fontWeight: 'var(--weight-bold)',
      letterSpacing: 'var(--tracking-eyebrow)',
      textTransform: 'uppercase',
      padding: '3px 0'
    }
  }, month), /*#__PURE__*/React.createElement("div", {
    style: {
      fontFamily: 'var(--font-mono)',
      fontSize: '26px',
      fontWeight: 'var(--weight-bold)',
      color: 'var(--text-heading)',
      padding: '6px 0',
      lineHeight: 1
    }
  }, day)), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      minWidth: 0
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: '8px',
      marginBottom: '4px'
    }
  }, /*#__PURE__*/React.createElement(__ds_scope.Badge, {
    tone: kindTone,
    size: "sm"
  }, kind), spotsLeft != null && spotsLeft <= 5 && /*#__PURE__*/React.createElement(__ds_scope.Badge, {
    tone: "warning",
    size: "sm"
  }, spotsLeft, " spots left")), /*#__PURE__*/React.createElement("h3", {
    style: {
      margin: 0,
      fontSize: 'var(--text-h4)',
      color: 'var(--text-heading)'
    }
  }, href ? /*#__PURE__*/React.createElement("a", {
    href: href,
    style: {
      color: 'inherit',
      textDecoration: 'none'
    }
  }, title) : title), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexWrap: 'wrap',
      gap: '4px 14px',
      marginTop: '6px',
      fontSize: 'var(--text-body-sm)',
      color: 'var(--text-muted)'
    }
  }, time && /*#__PURE__*/React.createElement("span", null, time), location && /*#__PURE__*/React.createElement("span", null, location)), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      gap: '12px',
      marginTop: '14px'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: '8px'
    }
  }, attendees.length > 0 && /*#__PURE__*/React.createElement(__ds_scope.AvatarStack, {
    people: attendees,
    size: "xs",
    max: 3
  }), /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--text-body-sm)',
      color: 'var(--text-muted)'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontFamily: 'var(--font-mono)',
      color: 'var(--text-heading)'
    }
  }, count), " going")), /*#__PURE__*/React.createElement(__ds_scope.Button, {
    size: "sm",
    variant: going ? 'subtle' : 'primary',
    onClick: onRSVP,
    leadingVisual: going ? '✓' : undefined
  }, going ? "You're going" : 'RSVP')))));
}
Object.assign(__ds_scope, { EventCard });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/community/EventCard.jsx", error: String((e && e.message) || e) }); }

// components/community/MatchResult.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function Side({
  team,
  score,
  winner,
  align
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: '10px',
      flex: 1,
      minWidth: 0,
      flexDirection: align === 'right' ? 'row-reverse' : 'row'
    }
  }, /*#__PURE__*/React.createElement(__ds_scope.Avatar, {
    name: team.name,
    src: team.crest,
    size: "md",
    square: true
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      minWidth: 0,
      textAlign: align === 'right' ? 'right' : 'left'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: winner ? 'var(--weight-bold)' : 'var(--weight-medium)',
      color: winner ? 'var(--text-heading)' : 'var(--text-muted)',
      fontSize: 'var(--text-body-md)',
      whiteSpace: 'nowrap',
      overflow: 'hidden',
      textOverflow: 'ellipsis'
    }
  }, team.name)));
}
function MatchResult({
  home,
  away,
  homeScore,
  awayScore,
  status = 'final',
  date,
  competition,
  style,
  ...rest
}) {
  const live = status === 'live';
  const upcoming = status === 'upcoming';
  const homeWin = homeScore > awayScore;
  const awayWin = awayScore > homeScore;
  return /*#__PURE__*/React.createElement(__ds_scope.Card, _extends({
    padding: "md",
    style: style
  }, rest), (competition || date) && /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      justifyContent: 'space-between',
      marginBottom: '12px',
      fontSize: 'var(--text-caption)',
      color: 'var(--text-muted)'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontWeight: 'var(--weight-semibold)',
      letterSpacing: 'var(--tracking-wide)',
      textTransform: 'uppercase'
    }
  }, competition), /*#__PURE__*/React.createElement("span", null, date)), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: '12px'
    }
  }, /*#__PURE__*/React.createElement(Side, {
    team: home,
    winner: !upcoming && homeWin,
    align: "left"
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      flexShrink: 0,
      textAlign: 'center',
      minWidth: '86px'
    }
  }, upcoming ? /*#__PURE__*/React.createElement("div", {
    style: {
      fontFamily: 'var(--font-mono)',
      fontSize: 'var(--text-body-md)',
      color: 'var(--text-muted)',
      fontWeight: 'var(--weight-semibold)'
    }
  }, "vs") : /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      gap: '8px',
      fontFamily: 'var(--font-mono)',
      fontWeight: 'var(--weight-bold)',
      fontSize: '26px',
      color: 'var(--text-heading)'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      opacity: homeWin ? 1 : 0.55
    }
  }, homeScore), /*#__PURE__*/React.createElement("span", {
    style: {
      color: 'var(--sand-4)',
      fontSize: '18px'
    }
  }, ":"), /*#__PURE__*/React.createElement("span", {
    style: {
      opacity: awayWin ? 1 : 0.55
    }
  }, awayScore)), /*#__PURE__*/React.createElement("div", {
    style: {
      marginTop: '4px'
    }
  }, live && /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      gap: '5px',
      fontSize: 'var(--text-caption)',
      fontWeight: 'var(--weight-bold)',
      color: 'var(--danger-fg)'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      width: '6px',
      height: '6px',
      borderRadius: '50%',
      background: 'var(--danger-fg)'
    }
  }), "LIVE"), status === 'final' && /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--text-caption)',
      color: 'var(--text-subtle)',
      fontWeight: 'var(--weight-semibold)',
      letterSpacing: 'var(--tracking-wide)'
    }
  }, "FINAL"))), /*#__PURE__*/React.createElement(Side, {
    team: away,
    winner: !upcoming && awayWin,
    align: "right"
  })));
}
Object.assign(__ds_scope, { MatchResult });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/community/MatchResult.jsx", error: String((e && e.message) || e) }); }

// components/community/PlayerCard.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function PlayerCard({
  name,
  photo,
  position,
  team,
  since,
  isCaptain = false,
  positions = [],
  href,
  style,
  ...rest
}) {
  const roles = positions.length ? positions : position ? [position] : [];
  return /*#__PURE__*/React.createElement(__ds_scope.Card, _extends({
    interactive: !!href,
    as: href ? 'a' : 'div',
    href: href,
    padding: "lg",
    style: {
      textAlign: 'center',
      ...style
    }
  }, rest), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      justifyContent: 'center',
      marginBottom: '12px'
    }
  }, /*#__PURE__*/React.createElement(__ds_scope.Avatar, {
    name: name,
    src: photo,
    size: "xl",
    ring: isCaptain
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      gap: '6px'
    }
  }, /*#__PURE__*/React.createElement("h3", {
    style: {
      margin: 0,
      fontSize: 'var(--text-h4)',
      color: 'var(--text-heading)'
    }
  }, name), isCaptain && /*#__PURE__*/React.createElement(__ds_scope.Badge, {
    tone: "highlight",
    size: "sm"
  }, "Captain")), team && /*#__PURE__*/React.createElement("div", {
    style: {
      marginTop: '2px',
      fontSize: 'var(--text-body-sm)',
      color: 'var(--text-muted)'
    }
  }, team), roles.length > 0 && /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexWrap: 'wrap',
      justifyContent: 'center',
      gap: '6px',
      marginTop: '12px'
    }
  }, roles.map((r, i) => /*#__PURE__*/React.createElement("span", {
    key: i,
    style: {
      padding: '4px 12px',
      borderRadius: 'var(--radius-pill)',
      background: 'var(--surface-secondary-soft)',
      color: 'var(--teal-7)',
      fontSize: 'var(--text-caption)',
      fontWeight: 'var(--weight-semibold)'
    }
  }, r))), since && /*#__PURE__*/React.createElement("div", {
    style: {
      marginTop: '12px',
      fontSize: 'var(--text-caption)',
      color: 'var(--text-subtle)'
    }
  }, "Playing since ", since));
}
Object.assign(__ds_scope, { PlayerCard });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/community/PlayerCard.jsx", error: String((e && e.message) || e) }); }

// components/community/TeamCard.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function TeamCard({
  name,
  location,
  crest,
  members = [],
  memberCount,
  recruiting = false,
  tags = [],
  href,
  style,
  ...rest
}) {
  const count = memberCount ?? members.length;
  return /*#__PURE__*/React.createElement(__ds_scope.Card, _extends({
    interactive: !!href,
    as: href ? 'a' : 'div',
    href: href,
    padding: "none",
    style: style
  }, rest), /*#__PURE__*/React.createElement("div", {
    style: {
      height: '8px',
      background: 'var(--brand-gradient)'
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '20px'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: '14px',
      alignItems: 'flex-start'
    }
  }, /*#__PURE__*/React.createElement(__ds_scope.Avatar, {
    name: name,
    src: crest,
    size: "lg",
    square: true
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      minWidth: 0
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: '8px',
      flexWrap: 'wrap'
    }
  }, /*#__PURE__*/React.createElement("h3", {
    style: {
      margin: 0,
      fontSize: 'var(--text-h4)',
      color: 'var(--text-heading)'
    }
  }, name), recruiting && /*#__PURE__*/React.createElement(__ds_scope.Badge, {
    tone: "success",
    dot: true,
    size: "sm"
  }, "Recruiting")), location && /*#__PURE__*/React.createElement("div", {
    style: {
      marginTop: '3px',
      fontSize: 'var(--text-body-sm)',
      color: 'var(--text-muted)'
    }
  }, location))), tags.length > 0 && /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexWrap: 'wrap',
      gap: '6px',
      marginTop: '14px'
    }
  }, tags.map((t, i) => /*#__PURE__*/React.createElement("span", {
    key: i,
    style: {
      padding: '4px 10px',
      borderRadius: 'var(--radius-pill)',
      background: 'var(--surface-sunken)',
      border: '1px solid var(--border-muted)',
      fontSize: 'var(--text-caption)',
      fontWeight: 'var(--weight-medium)',
      color: 'var(--text-body)'
    }
  }, t))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      gap: '12px',
      marginTop: '16px',
      paddingTop: '14px',
      borderTop: '1px solid var(--border-muted)'
    }
  }, members.length > 0 ? /*#__PURE__*/React.createElement(__ds_scope.AvatarStack, {
    people: members,
    size: "sm",
    max: 4
  }) : /*#__PURE__*/React.createElement("span", null), /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--text-body-sm)',
      color: 'var(--text-muted)',
      fontWeight: 'var(--weight-medium)'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontFamily: 'var(--font-mono)',
      color: 'var(--text-heading)'
    }
  }, count), " members"))));
}
Object.assign(__ds_scope, { TeamCard });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/community/TeamCard.jsx", error: String((e && e.message) || e) }); }

// components/layout/Stat.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function Stat({
  value,
  label,
  icon,
  tone = 'default',
  trend,
  style,
  ...rest
}) {
  const valueColor = tone === 'primary' ? 'var(--brand-primary-active)' : tone === 'secondary' ? 'var(--teal-6)' : 'var(--text-heading)';
  return /*#__PURE__*/React.createElement("div", _extends({
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: '2px',
      ...style
    }
  }, rest), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: '8px'
    }
  }, icon && /*#__PURE__*/React.createElement("span", {
    style: {
      color: 'var(--brand-secondary)',
      display: 'inline-flex'
    }
  }, icon), /*#__PURE__*/React.createElement("span", {
    style: {
      fontFamily: 'var(--font-mono)',
      fontWeight: 'var(--weight-bold)',
      fontSize: 'var(--text-h2)',
      color: valueColor,
      lineHeight: 1
    }
  }, value), trend && /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--text-body-sm)',
      fontWeight: 'var(--weight-semibold)',
      color: trend.startsWith('-') ? 'var(--danger-fg)' : 'var(--success-fg)'
    }
  }, trend)), /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--text-body-sm)',
      color: 'var(--text-muted)'
    }
  }, label));
}
Object.assign(__ds_scope, { Stat });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/layout/Stat.jsx", error: String((e && e.message) || e) }); }

// components/layout/Tabs.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function Tabs({
  tabs = [],
  value,
  defaultValue,
  onChange,
  style,
  ...rest
}) {
  const isControlled = value !== undefined;
  const [internal, setInternal] = React.useState(defaultValue ?? (tabs[0] && tabs[0].id));
  const active = isControlled ? value : internal;
  const select = id => {
    if (!isControlled) setInternal(id);
    onChange && onChange(id);
  };
  return /*#__PURE__*/React.createElement("div", _extends({
    role: "tablist",
    style: {
      display: 'inline-flex',
      gap: '4px',
      padding: '4px',
      background: 'var(--surface-sunken)',
      borderRadius: 'var(--radius-pill)',
      ...style
    }
  }, rest), tabs.map(t => {
    const on = t.id === active;
    return /*#__PURE__*/React.createElement("button", {
      key: t.id,
      type: "button",
      role: "tab",
      "aria-selected": on,
      onClick: () => select(t.id),
      style: {
        display: 'inline-flex',
        alignItems: 'center',
        gap: '7px',
        padding: '8px 16px',
        border: 'none',
        cursor: 'pointer',
        borderRadius: 'var(--radius-pill)',
        background: on ? 'var(--surface-card)' : 'transparent',
        color: on ? 'var(--text-heading)' : 'var(--text-muted)',
        fontFamily: 'var(--font-body)',
        fontSize: 'var(--text-body-sm)',
        fontWeight: 'var(--weight-semibold)',
        boxShadow: on ? 'var(--shadow-xs)' : 'none',
        transition: 'background var(--duration-fast), color var(--duration-fast)'
      }
    }, t.icon, t.label, t.count != null && /*#__PURE__*/React.createElement("span", {
      style: {
        fontFamily: 'var(--font-mono)',
        fontSize: 'var(--text-caption)',
        padding: '1px 7px',
        borderRadius: 'var(--radius-pill)',
        background: on ? 'var(--surface-muted)' : 'var(--sand-2)',
        color: 'var(--text-body)'
      }
    }, t.count));
  }));
}
Object.assign(__ds_scope, { Tabs });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/layout/Tabs.jsx", error: String((e && e.message) || e) }); }

// components/navigation/Breadcrumbs.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function Breadcrumbs({
  items = [],
  style,
  ...rest
}) {
  return /*#__PURE__*/React.createElement("nav", _extends({
    "aria-label": "Breadcrumb",
    style: {
      ...style
    }
  }, rest), /*#__PURE__*/React.createElement("ol", {
    style: {
      display: 'flex',
      alignItems: 'center',
      flexWrap: 'wrap',
      gap: '6px',
      listStyle: 'none',
      margin: 0,
      padding: 0
    }
  }, items.map((it, i) => {
    const last = i === items.length - 1;
    return /*#__PURE__*/React.createElement("li", {
      key: i,
      style: {
        display: 'inline-flex',
        alignItems: 'center',
        gap: '6px'
      }
    }, last || !it.href ? /*#__PURE__*/React.createElement("span", {
      "aria-current": last ? 'page' : undefined,
      style: {
        fontSize: 'var(--text-body-sm)',
        fontWeight: last ? 'var(--weight-semibold)' : 'var(--weight-medium)',
        color: last ? 'var(--text-heading)' : 'var(--text-muted)'
      }
    }, it.label) : /*#__PURE__*/React.createElement("a", {
      href: it.href,
      style: {
        fontSize: 'var(--text-body-sm)',
        color: 'var(--text-muted)',
        textDecoration: 'none'
      }
    }, it.label), !last && /*#__PURE__*/React.createElement("span", {
      "aria-hidden": "true",
      style: {
        color: 'var(--text-subtle)',
        fontSize: 'var(--text-body-sm)'
      }
    }, "/"));
  })));
}
Object.assign(__ds_scope, { Breadcrumbs });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/navigation/Breadcrumbs.jsx", error: String((e && e.message) || e) }); }

// components/navigation/NavBar.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function NavBar({
  brand,
  links = [],
  activeId,
  actions,
  style,
  ...rest
}) {
  const [open, setOpen] = React.useState(false);
  return /*#__PURE__*/React.createElement("header", _extends({
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: '16px',
      flexWrap: 'wrap',
      padding: '12px 20px',
      background: 'var(--surface-card)',
      borderBottom: '1px solid var(--border-muted)',
      ...style
    }
  }, rest), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: '10px',
      marginRight: 'auto'
    }
  }, brand), /*#__PURE__*/React.createElement("nav", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: '2px'
    },
    className: "jh-nav-links",
    "data-open": open
  }, links.map(l => {
    const on = l.id === activeId;
    return /*#__PURE__*/React.createElement("a", {
      key: l.id,
      href: l.href || '#',
      "aria-current": on ? 'page' : undefined,
      style: {
        display: 'inline-flex',
        alignItems: 'center',
        gap: '7px',
        padding: '9px 14px',
        borderRadius: 'var(--radius-md)',
        textDecoration: 'none',
        fontFamily: 'var(--font-body)',
        fontSize: 'var(--text-body-md)',
        fontWeight: 'var(--weight-medium)',
        color: on ? 'var(--brand-primary-active)' : 'var(--text-body)',
        background: on ? 'var(--surface-accent-soft)' : 'transparent'
      },
      onMouseEnter: e => {
        if (!on) e.currentTarget.style.background = 'var(--surface-sunken)';
      },
      onMouseLeave: e => {
        if (!on) e.currentTarget.style.background = 'transparent';
      }
    }, l.icon, l.label);
  })), actions && /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: '8px'
    }
  }, actions));
}
Object.assign(__ds_scope, { NavBar });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/navigation/NavBar.jsx", error: String((e && e.message) || e) }); }

// components/navigation/Pagination.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function range(start, end) {
  return Array.from({
    length: end - start + 1
  }, (_, i) => start + i);
}
function pages(current, total) {
  if (total <= 7) return range(1, total);
  if (current <= 4) return [1, 2, 3, 4, 5, '…', total];
  if (current >= total - 3) return [1, '…', ...range(total - 4, total)];
  return [1, '…', current - 1, current, current + 1, '…', total];
}
function Pagination({
  page = 1,
  total = 1,
  onChange,
  style,
  ...rest
}) {
  const items = pages(page, total);
  const go = p => {
    if (p >= 1 && p <= total && p !== page) onChange && onChange(p);
  };
  const btn = active => ({
    minWidth: '38px',
    height: '38px',
    padding: '0 10px',
    display: 'inline-flex',
    alignItems: 'center',
    justifyContent: 'center',
    border: `1px solid ${active ? 'transparent' : 'var(--border-default)'}`,
    borderRadius: 'var(--radius-md)',
    background: active ? 'var(--brand-primary)' : 'var(--surface-card)',
    color: active ? 'var(--text-on-accent)' : 'var(--text-body)',
    fontFamily: 'var(--font-body)',
    fontSize: 'var(--text-body-sm)',
    fontWeight: 'var(--weight-semibold)',
    cursor: 'pointer'
  });
  return /*#__PURE__*/React.createElement("nav", _extends({
    "aria-label": "Pagination",
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: '6px',
      ...style
    }
  }, rest), /*#__PURE__*/React.createElement("button", {
    type: "button",
    onClick: () => go(page - 1),
    disabled: page === 1,
    "aria-label": "Previous",
    style: {
      ...btn(false),
      opacity: page === 1 ? 0.4 : 1,
      cursor: page === 1 ? 'not-allowed' : 'pointer'
    }
  }, "\u2039"), items.map((p, i) => p === '…' ? /*#__PURE__*/React.createElement("span", {
    key: `e${i}`,
    style: {
      padding: '0 4px',
      color: 'var(--text-subtle)'
    }
  }, "\u2026") : /*#__PURE__*/React.createElement("button", {
    key: p,
    type: "button",
    onClick: () => go(p),
    "aria-current": p === page ? 'page' : undefined,
    style: btn(p === page)
  }, p)), /*#__PURE__*/React.createElement("button", {
    type: "button",
    onClick: () => go(page + 1),
    disabled: page === total,
    "aria-label": "Next",
    style: {
      ...btn(false),
      opacity: page === total ? 0.4 : 1,
      cursor: page === total ? 'not-allowed' : 'pointer'
    }
  }, "\u203A"));
}
Object.assign(__ds_scope, { Pagination });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/navigation/Pagination.jsx", error: String((e && e.message) || e) }); }

// ui_kits/webapp/data.js
try { (() => {
// JuggerHub UI kit — mock data (no backend; illustrative only)
window.JH_DATA = {
  teams: [{
    id: 1,
    name: 'Berlin Bloodhounds',
    location: 'Berlin · Tempelhofer Feld',
    recruiting: true,
    tags: ['Mixed', 'Beginner-friendly'],
    memberCount: 14,
    members: [{
      name: 'Mira O.'
    }, {
      name: 'Jonas B.'
    }, {
      name: 'Ada L.'
    }, {
      name: 'Sam K.'
    }, {
      name: 'Rai P.'
    }],
    wins: 16,
    played: 24,
    founded: '2019'
  }, {
    id: 2,
    name: 'Kreuzberg Rooks',
    location: 'Berlin · Görlitzer Park',
    recruiting: false,
    tags: ['Competitive'],
    memberCount: 11,
    members: [{
      name: 'Lena F.'
    }, {
      name: 'Omar D.'
    }, {
      name: 'Kit W.'
    }],
    wins: 12,
    played: 22,
    founded: '2017'
  }, {
    id: 3,
    name: 'Prenzlberg Owls',
    location: 'Berlin · Mauerpark',
    recruiting: true,
    tags: ['Mixed', 'Social'],
    memberCount: 9,
    members: [{
      name: 'Nina S.'
    }, {
      name: 'Paul R.'
    }, {
      name: 'Yuki T.'
    }],
    wins: 8,
    played: 18,
    founded: '2021'
  }, {
    id: 4,
    name: 'Spandau Foxes',
    location: 'Berlin · Spandau',
    recruiting: true,
    tags: ['Beginner-friendly'],
    memberCount: 7,
    members: [{
      name: 'Bea M.'
    }, {
      name: 'Cal V.'
    }],
    wins: 5,
    played: 14,
    founded: '2022'
  }],
  events: [{
    id: 1,
    kind: 'Training',
    title: 'Open training — all levels',
    month: 'SAT',
    day: '12',
    time: '14:00',
    location: 'Tempelhofer Feld',
    spotsLeft: 4,
    attendeeCount: 18,
    attendees: [{
      name: 'Mira O.'
    }, {
      name: 'Sam K.'
    }, {
      name: 'Rai P.'
    }]
  }, {
    id: 2,
    kind: 'Match',
    title: 'Bloodhounds vs Rooks',
    month: 'SUN',
    day: '13',
    time: '11:00',
    location: 'Görlitzer Park',
    spotsLeft: 20,
    attendeeCount: 9,
    attendees: [{
      name: 'Lena F.'
    }, {
      name: 'Omar D.'
    }]
  }, {
    id: 3,
    kind: 'Tournament',
    title: 'Berlin Summer Jugger Cup',
    month: 'JUL',
    day: '26',
    time: '09:00',
    location: 'Volkspark Friedrichshain',
    spotsLeft: 2,
    attendeeCount: 64,
    attendees: [{
      name: 'Nina S.'
    }, {
      name: 'Paul R.'
    }, {
      name: 'Yuki T.'
    }]
  }, {
    id: 4,
    kind: 'Social',
    title: 'Post-training picnic',
    month: 'SAT',
    day: '12',
    time: '17:00',
    location: 'Tempelhofer Feld',
    spotsLeft: 30,
    attendeeCount: 22,
    attendees: [{
      name: 'Bea M.'
    }, {
      name: 'Cal V.'
    }]
  }],
  matches: [{
    competition: 'Berlin League · R4',
    date: 'Jul 5',
    home: {
      name: 'Bloodhounds'
    },
    away: {
      name: 'Rooks'
    },
    homeScore: 5,
    awayScore: 3,
    status: 'final'
  }, {
    competition: 'Berlin League · R4',
    date: 'Jun 28',
    home: {
      name: 'Bloodhounds'
    },
    away: {
      name: 'Owls'
    },
    homeScore: 6,
    awayScore: 2,
    status: 'final'
  }, {
    competition: 'Friendly',
    date: 'Jun 21',
    home: {
      name: 'Foxes'
    },
    away: {
      name: 'Bloodhounds'
    },
    homeScore: 1,
    awayScore: 4,
    status: 'final'
  }],
  roster: [{
    name: 'Mira Okonkwo',
    positions: ['Runner', 'Q-tip'],
    since: '2021',
    isCaptain: true
  }, {
    name: 'Jonas Berg',
    positions: ['Enforcer'],
    since: '2019'
  }, {
    name: 'Ada Lindqvist',
    positions: ['Chain'],
    since: '2020'
  }, {
    name: 'Sam Keller',
    positions: ['Runner'],
    since: '2022'
  }, {
    name: 'Rai Petrov',
    positions: ['Shield'],
    since: '2021'
  }, {
    name: 'Nova Adeyemi',
    positions: ['Q-tip', 'Runner'],
    since: '2023'
  }]
};
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/webapp/data.js", error: String((e && e.message) || e) }); }

// ui_kits/webapp/screens.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
/* JuggerHub UI kit — screens. Composes DS primitives + community cards. */
const DS = window.JuggerHubDesignSystem_12b5ac;
const {
  Button,
  Input,
  Select,
  Card,
  Badge,
  Tag,
  Tabs,
  Stat,
  TeamCard,
  EventCard,
  MatchResult,
  PlayerCard,
  EmptyState,
  Avatar,
  FormField,
  Textarea,
  Checkbox,
  ProgressBar,
  Alert
} = DS;

/* ---------- DISCOVER ---------- */
function Discover({
  onNav,
  onOpenTeam
}) {
  const {
    teams,
    events
  } = window.JH_DATA;
  return /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'linear-gradient(160deg, var(--coral-0), var(--surface-page) 55%, var(--teal-0))'
    }
  }, /*#__PURE__*/React.createElement(PageWrap, null, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: 'minmax(0,1.3fr) minmax(0,1fr)',
      gap: 40,
      alignItems: 'center',
      padding: '32px 0 40px'
    }
  }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("span", {
    className: "jh-eyebrow"
  }, "Find your team"), /*#__PURE__*/React.createElement("h1", {
    style: {
      fontSize: 'var(--text-display)',
      margin: '10px 0 14px',
      color: 'var(--text-heading)',
      lineHeight: 1.05
    }
  }, "Jugger is better", /*#__PURE__*/React.createElement("br", null), "with a crew."), /*#__PURE__*/React.createElement("p", {
    style: {
      fontSize: 'var(--text-lead)',
      color: 'var(--text-body)',
      maxWidth: 460,
      margin: '0 0 24px'
    }
  }, "Discover local teams, book training, and follow your matches \u2014 all in one warm, community-run home for the sport."), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 12,
      flexWrap: 'wrap'
    }
  }, /*#__PURE__*/React.createElement(Button, {
    size: "lg",
    onClick: () => onNav('teams'),
    leadingVisual: /*#__PURE__*/React.createElement(Icon, {
      name: "compass"
    })
  }, "Find a team near you"), /*#__PURE__*/React.createElement(Button, {
    size: "lg",
    variant: "outline",
    onClick: () => onNav('onboarding')
  }, "Start a team")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 28,
      marginTop: 30
    }
  }, /*#__PURE__*/React.createElement(Stat, {
    value: "120+",
    label: "Active teams",
    tone: "primary"
  }), /*#__PURE__*/React.createElement(Stat, {
    value: "30",
    label: "Cities",
    tone: "secondary"
  }), /*#__PURE__*/React.createElement(Stat, {
    value: "2.4k",
    label: "Players"
  }))), /*#__PURE__*/React.createElement(Card, {
    elevated: true,
    padding: "none",
    style: {
      overflow: 'hidden'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      height: 8,
      background: 'var(--brand-gradient)'
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      padding: 20
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 8,
      marginBottom: 14
    }
  }, /*#__PURE__*/React.createElement(Icon, {
    name: "calendar-heart",
    size: 18,
    color: "var(--brand-primary)"
  }), /*#__PURE__*/React.createElement("strong", {
    style: {
      color: 'var(--text-heading)'
    }
  }, "This weekend near you")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 12
    }
  }, events.slice(0, 2).map(e => /*#__PURE__*/React.createElement(EventCard, _extends({
    key: e.id
  }, e))))))))), /*#__PURE__*/React.createElement(PageWrap, null, /*#__PURE__*/React.createElement(SectionHead, {
    icon: "users",
    title: "Teams recruiting now",
    action: /*#__PURE__*/React.createElement(Button, {
      variant: "ghost",
      size: "sm",
      onClick: () => onNav('teams'),
      trailingVisual: /*#__PURE__*/React.createElement(Icon, {
        name: "arrow-right",
        size: 16
      })
    }, "See all")
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: 'repeat(auto-fill, minmax(260px, 1fr))',
      gap: 18
    }
  }, teams.filter(t => t.recruiting).map(t => /*#__PURE__*/React.createElement("div", {
    key: t.id,
    onClick: () => onOpenTeam(t),
    style: {
      cursor: 'pointer'
    }
  }, /*#__PURE__*/React.createElement(TeamCard, _extends({}, t, {
    href: "#"
  }))))), /*#__PURE__*/React.createElement("div", {
    style: {
      marginTop: 44
    }
  }, /*#__PURE__*/React.createElement(SectionHead, {
    icon: "sparkles",
    title: "New to Jugger? Start here"
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: 'repeat(auto-fit, minmax(220px,1fr))',
      gap: 16
    }
  }, [{
    i: 'search',
    t: 'Find a team',
    d: 'Browse teams by city and vibe — many welcome total beginners.'
  }, {
    i: 'calendar-check',
    t: 'Come to training',
    d: 'RSVP to an open session. Most teams lend you gear for free.'
  }, {
    i: 'trophy',
    t: 'Play matches',
    d: 'Join friendlies, leagues and tournaments as you find your feet.'
  }].map((s, i) => /*#__PURE__*/React.createElement(Card, {
    key: i,
    padding: "lg"
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 44,
      height: 44,
      borderRadius: 'var(--radius-md)',
      background: 'var(--surface-secondary-soft)',
      display: 'grid',
      placeItems: 'center',
      marginBottom: 12
    }
  }, /*#__PURE__*/React.createElement(Icon, {
    name: s.i,
    size: 22,
    color: "var(--teal-6)"
  })), /*#__PURE__*/React.createElement("h3", {
    style: {
      fontSize: 'var(--text-h4)',
      margin: '0 0 4px',
      color: 'var(--text-heading)'
    }
  }, s.t), /*#__PURE__*/React.createElement("p", {
    style: {
      margin: 0,
      color: 'var(--text-muted)',
      fontSize: 'var(--text-body-md)'
    }
  }, s.d)))))));
}
function SectionHead({
  icon,
  title,
  action
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      margin: '8px 0 18px'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 10
    }
  }, icon && /*#__PURE__*/React.createElement(Icon, {
    name: icon,
    size: 20,
    color: "var(--brand-primary)"
  }), /*#__PURE__*/React.createElement("h2", {
    style: {
      fontSize: 'var(--text-h2)',
      margin: 0,
      color: 'var(--text-heading)'
    }
  }, title)), action);
}

/* ---------- TEAMS ---------- */
function Teams({
  onOpenTeam
}) {
  const {
    teams
  } = window.JH_DATA;
  const [q, setQ] = React.useState('');
  const [filter, setFilter] = React.useState('all');
  const list = teams.filter(t => (filter === 'all' || filter === 'recruiting' && t.recruiting || t.tags.map(x => x.toLowerCase()).includes(filter)) && (q === '' || t.name.toLowerCase().includes(q.toLowerCase()) || t.location.toLowerCase().includes(q.toLowerCase())));
  const chips = [['all', 'All teams'], ['recruiting', 'Recruiting'], ['beginner-friendly', 'Beginner-friendly'], ['mixed', 'Mixed'], ['competitive', 'Competitive']];
  return /*#__PURE__*/React.createElement(PageWrap, null, /*#__PURE__*/React.createElement("div", {
    style: {
      marginBottom: 8
    }
  }, /*#__PURE__*/React.createElement("h1", {
    style: {
      fontSize: 'var(--text-h1)',
      margin: '0 0 6px',
      color: 'var(--text-heading)'
    }
  }, "Teams in Berlin"), /*#__PURE__*/React.createElement("p", {
    style: {
      color: 'var(--text-muted)',
      margin: 0
    }
  }, "Find a crew that fits how you want to play.")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 12,
      flexWrap: 'wrap',
      margin: '18px 0'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      flex: '1 1 260px'
    }
  }, /*#__PURE__*/React.createElement(Input, {
    placeholder: "Search teams or areas\u2026",
    leadingVisual: /*#__PURE__*/React.createElement(Icon, {
      name: "search",
      size: 16
    }),
    value: q,
    onChange: e => setQ(e.target.value)
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      width: 180
    }
  }, /*#__PURE__*/React.createElement(Select, {
    defaultValue: "berlin"
  }, /*#__PURE__*/React.createElement("option", {
    value: "berlin"
  }, "Berlin"), /*#__PURE__*/React.createElement("option", {
    value: "munich"
  }, "Munich"), /*#__PURE__*/React.createElement("option", {
    value: "hamburg"
  }, "Hamburg")))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 8,
      flexWrap: 'wrap',
      marginBottom: 22
    }
  }, chips.map(([id, label]) => /*#__PURE__*/React.createElement(Tag, {
    key: id,
    interactive: true,
    selected: filter === id,
    onClick: () => setFilter(id)
  }, label))), list.length ? /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: 'repeat(auto-fill, minmax(260px, 1fr))',
      gap: 18
    }
  }, list.map(t => /*#__PURE__*/React.createElement("div", {
    key: t.id,
    onClick: () => onOpenTeam(t),
    style: {
      cursor: 'pointer'
    }
  }, /*#__PURE__*/React.createElement(TeamCard, _extends({}, t, {
    href: "#"
  }))))) : /*#__PURE__*/React.createElement(Card, null, /*#__PURE__*/React.createElement(EmptyState, {
    icon: /*#__PURE__*/React.createElement(Icon, {
      name: "search-x",
      size: 26
    }),
    title: "No teams match that",
    description: "Try a different filter or start your own team here.",
    action: /*#__PURE__*/React.createElement(Button, {
      size: "sm"
    }, "Start a team")
  })));
}

/* ---------- TEAM DETAIL ---------- */
function TeamDetail({
  team,
  onBack
}) {
  const {
    roster,
    matches,
    events
  } = window.JH_DATA;
  const [tab, setTab] = React.useState('overview');
  const t = team || window.JH_DATA.teams[0];
  return /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'var(--brand-gradient)',
      height: 120
    }
  }), /*#__PURE__*/React.createElement(PageWrap, null, /*#__PURE__*/React.createElement("div", {
    style: {
      marginTop: -56,
      display: 'flex',
      gap: 18,
      alignItems: 'flex-end',
      flexWrap: 'wrap'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      boxShadow: '0 0 0 5px var(--surface-page)',
      borderRadius: 'var(--radius-lg)'
    }
  }, /*#__PURE__*/React.createElement(Avatar, {
    name: t.name,
    size: 96,
    square: true
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      minWidth: 0,
      paddingBottom: 4
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 10,
      flexWrap: 'wrap'
    }
  }, /*#__PURE__*/React.createElement("h1", {
    style: {
      fontSize: 'var(--text-h1)',
      margin: 0,
      color: 'var(--text-heading)'
    }
  }, t.name), t.recruiting && /*#__PURE__*/React.createElement(Badge, {
    tone: "success",
    dot: true
  }, "Recruiting")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 6,
      marginTop: 4,
      color: 'var(--text-muted)'
    }
  }, /*#__PURE__*/React.createElement(Icon, {
    name: "map-pin",
    size: 15
  }), " ", t.location, " \xB7 Founded ", t.founded)), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 10,
      paddingBottom: 4
    }
  }, /*#__PURE__*/React.createElement(Button, {
    variant: "outline",
    leadingVisual: /*#__PURE__*/React.createElement(Icon, {
      name: "share-2",
      size: 16
    })
  }, "Share"), /*#__PURE__*/React.createElement(Button, {
    leadingVisual: /*#__PURE__*/React.createElement(Icon, {
      name: "user-plus",
      size: 16
    })
  }, "Join team"))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 30,
      margin: '20px 0 22px'
    }
  }, /*#__PURE__*/React.createElement(Stat, {
    value: t.played,
    label: "Matches",
    tone: "default"
  }), /*#__PURE__*/React.createElement(Stat, {
    value: Math.round(t.wins / t.played * 100) + '%',
    label: "Win rate",
    tone: "secondary",
    trend: "+4"
  }), /*#__PURE__*/React.createElement(Stat, {
    value: t.memberCount,
    label: "Members",
    tone: "primary"
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      marginBottom: 20
    }
  }, /*#__PURE__*/React.createElement(Tabs, {
    value: tab,
    onChange: setTab,
    tabs: [{
      id: 'overview',
      label: 'Overview'
    }, {
      id: 'roster',
      label: 'Roster',
      count: roster.length
    }, {
      id: 'matches',
      label: 'Matches',
      count: matches.length
    }]
  })), tab === 'overview' && /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: 'minmax(0,2fr) minmax(0,1fr)',
      gap: 20,
      alignItems: 'start'
    }
  }, /*#__PURE__*/React.createElement(Card, {
    padding: "lg"
  }, /*#__PURE__*/React.createElement("h3", {
    style: {
      marginTop: 0,
      fontSize: 'var(--text-h3)',
      color: 'var(--text-heading)'
    }
  }, "About us"), /*#__PURE__*/React.createElement("p", {
    style: {
      color: 'var(--text-body)'
    }
  }, "We're a friendly mixed team training twice a week at Tempelhofer Feld. Beginners very welcome \u2014 we lend gear and coach the basics. Expect fast games, good snacks, and zero ego."), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 8,
      flexWrap: 'wrap',
      marginTop: 10
    }
  }, t.tags.map((x, i) => /*#__PURE__*/React.createElement(Tag, {
    key: i
  }, x)))), /*#__PURE__*/React.createElement(Card, {
    padding: "lg"
  }, /*#__PURE__*/React.createElement("h3", {
    style: {
      marginTop: 0,
      fontSize: 'var(--text-h4)',
      color: 'var(--text-heading)'
    }
  }, "Next sessions"), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 12
    }
  }, events.slice(0, 2).map(e => /*#__PURE__*/React.createElement(EventCard, _extends({
    key: e.id
  }, e)))))), tab === 'roster' && /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: 'repeat(auto-fill, minmax(200px,1fr))',
      gap: 16
    }
  }, roster.map((p, i) => /*#__PURE__*/React.createElement(PlayerCard, _extends({
    key: i
  }, p, {
    team: t.name
  })))), tab === 'matches' && /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 12,
      maxWidth: 640
    }
  }, matches.map((m, i) => /*#__PURE__*/React.createElement(MatchResult, _extends({
    key: i
  }, m))))));
}

/* ---------- ONBOARDING ---------- */
function Onboarding({
  onDone
}) {
  const [step, setStep] = React.useState(1);
  const [name, setName] = React.useState('');
  const done = step > 2;
  return /*#__PURE__*/React.createElement(PageWrap, {
    width: 640
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      marginBottom: 18
    }
  }, /*#__PURE__*/React.createElement("span", {
    className: "jh-eyebrow"
  }, "Start a team"), /*#__PURE__*/React.createElement("h1", {
    style: {
      fontSize: 'var(--text-h1)',
      margin: '8px 0 6px',
      color: 'var(--text-heading)'
    }
  }, done ? "You're all set!" : 'Create your team'), !done && /*#__PURE__*/React.createElement(ProgressBar, {
    value: step,
    max: 2,
    tone: "secondary",
    style: {
      marginTop: 14
    }
  })), /*#__PURE__*/React.createElement(Card, {
    padding: "lg"
  }, step === 1 && /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 16
    }
  }, /*#__PURE__*/React.createElement(FormField, {
    label: "Team name",
    required: true,
    hint: "You can change this later."
  }, /*#__PURE__*/React.createElement(Input, {
    placeholder: "Berlin Bloodhounds",
    value: name,
    onChange: e => setName(e.target.value)
  })), /*#__PURE__*/React.createElement(FormField, {
    label: "Home city / area",
    required: true
  }, /*#__PURE__*/React.createElement(Input, {
    placeholder: "Berlin \xB7 Tempelhofer Feld",
    leadingVisual: /*#__PURE__*/React.createElement(Icon, {
      name: "map-pin",
      size: 16
    })
  })), /*#__PURE__*/React.createElement(FormField, {
    label: "Who's it for?"
  }, /*#__PURE__*/React.createElement(Select, {
    defaultValue: "mixed"
  }, /*#__PURE__*/React.createElement("option", {
    value: "mixed"
  }, "Mixed & beginner-friendly"), /*#__PURE__*/React.createElement("option", {
    value: "comp"
  }, "Competitive"), /*#__PURE__*/React.createElement("option", {
    value: "social"
  }, "Social / casual"))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      justifyContent: 'flex-end'
    }
  }, /*#__PURE__*/React.createElement(Button, {
    onClick: () => setStep(2),
    trailingVisual: /*#__PURE__*/React.createElement(Icon, {
      name: "arrow-right",
      size: 16
    })
  }, "Continue"))), step === 2 && /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 16
    }
  }, /*#__PURE__*/React.createElement(FormField, {
    label: "Short bio",
    hint: "Tell players what to expect at training."
  }, /*#__PURE__*/React.createElement(Textarea, {
    rows: 4,
    placeholder: "We train twice a week and lend gear to newcomers\u2026"
  })), /*#__PURE__*/React.createElement(Checkbox, {
    defaultChecked: true,
    label: "Show my team publicly",
    description: "Appears in Discover and search."
  }), /*#__PURE__*/React.createElement(Checkbox, {
    label: "Open to recruiting new players",
    defaultChecked: true
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      justifyContent: 'space-between'
    }
  }, /*#__PURE__*/React.createElement(Button, {
    variant: "ghost",
    onClick: () => setStep(1),
    leadingVisual: /*#__PURE__*/React.createElement(Icon, {
      name: "arrow-left",
      size: 16
    })
  }, "Back"), /*#__PURE__*/React.createElement(Button, {
    onClick: () => setStep(3),
    leadingVisual: /*#__PURE__*/React.createElement(Icon, {
      name: "check",
      size: 16
    })
  }, "Create team"))), done && /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 16
    }
  }, /*#__PURE__*/React.createElement(Alert, {
    tone: "success",
    title: `${name || 'Your team'} is live!`,
    icon: /*#__PURE__*/React.createElement(Icon, {
      name: "party-popper",
      size: 18
    })
  }, "Invite players and schedule your first open training."), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 10
    }
  }, /*#__PURE__*/React.createElement(Button, {
    variant: "outline",
    leadingVisual: /*#__PURE__*/React.createElement(Icon, {
      name: "user-plus",
      size: 16
    })
  }, "Invite players"), /*#__PURE__*/React.createElement(Button, {
    onClick: onDone,
    leadingVisual: /*#__PURE__*/React.createElement(Icon, {
      name: "calendar-plus",
      size: 16
    })
  }, "Schedule training")))));
}
Object.assign(window, {
  Discover,
  Teams,
  TeamDetail,
  Onboarding
});
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/webapp/screens.jsx", error: String((e && e.message) || e) }); }

// ui_kits/webapp/shell.jsx
try { (() => {
/* JuggerHub UI kit — shared shell (nav, footer, icon helper). Exports to window. */
const {
  NavBar,
  IconButton,
  Avatar,
  Button
} = window.JuggerHubDesignSystem_12b5ac;
const Icon = ({
  name,
  size = 18,
  color
}) => /*#__PURE__*/React.createElement("i", {
  "data-lucide": name,
  style: {
    width: size,
    height: size,
    color
  }
});
function refreshIcons() {
  requestAnimationFrame(() => window.lucide && window.lucide.createIcons());
}
function Logo({
  size = 30
}) {
  return /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 9
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      width: size,
      height: size,
      borderRadius: Math.round(size * 0.3),
      background: 'var(--brand-gradient)',
      display: 'grid',
      placeItems: 'center',
      position: 'relative',
      flexShrink: 0
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      color: '#fff',
      fontWeight: 900,
      fontSize: size * 0.5,
      lineHeight: 1,
      fontFamily: 'var(--font-display)'
    }
  }, "\u2715")), /*#__PURE__*/React.createElement("span", {
    style: {
      fontFamily: 'var(--font-display)',
      fontWeight: 800,
      fontSize: 19,
      letterSpacing: '-0.02em',
      color: 'var(--text-heading)'
    }
  }, "JuggerHub"));
}
function Shell({
  active,
  onNav,
  children,
  onSignup
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      minHeight: '100%',
      display: 'flex',
      flexDirection: 'column',
      background: 'var(--surface-page)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'sticky',
      top: 0,
      zIndex: 10
    }
  }, /*#__PURE__*/React.createElement(NavBar, {
    brand: /*#__PURE__*/React.createElement("a", {
      href: "#",
      onClick: e => {
        e.preventDefault();
        onNav('discover');
      },
      style: {
        textDecoration: 'none'
      }
    }, /*#__PURE__*/React.createElement(Logo, null)),
    activeId: active,
    links: [{
      id: 'discover',
      label: 'Discover',
      href: '#',
      icon: /*#__PURE__*/React.createElement(Icon, {
        name: "compass",
        size: 17
      })
    }, {
      id: 'teams',
      label: 'Teams',
      href: '#',
      icon: /*#__PURE__*/React.createElement(Icon, {
        name: "users",
        size: 17
      })
    }, {
      id: 'events',
      label: 'Events',
      href: '#',
      icon: /*#__PURE__*/React.createElement(Icon, {
        name: "calendar-days",
        size: 17
      })
    }].map(l => ({
      ...l,
      href: '#'
    })),
    actions: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement(IconButton, {
      icon: /*#__PURE__*/React.createElement(Icon, {
        name: "search"
      }),
      label: "Search",
      variant: "ghost"
    }), /*#__PURE__*/React.createElement(IconButton, {
      icon: /*#__PURE__*/React.createElement(Icon, {
        name: "bell"
      }),
      label: "Notifications",
      variant: "ghost"
    }), /*#__PURE__*/React.createElement(Button, {
      variant: "primary",
      size: "sm",
      onClick: onSignup,
      leadingVisual: /*#__PURE__*/React.createElement(Icon, {
        name: "plus",
        size: 16
      })
    }, "Create team"), /*#__PURE__*/React.createElement(Avatar, {
      name: "You",
      size: "sm",
      ring: true
    }))
  })), /*#__PURE__*/React.createElement("main", {
    style: {
      flex: 1
    },
    onClick: refreshIcons
  }, children), /*#__PURE__*/React.createElement("footer", {
    style: {
      borderTop: '1px solid var(--border-muted)',
      background: 'var(--surface-card)',
      marginTop: 48
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      maxWidth: 1100,
      margin: '0 auto',
      padding: '28px 24px',
      display: 'flex',
      justifyContent: 'space-between',
      alignItems: 'center',
      flexWrap: 'wrap',
      gap: 16
    }
  }, /*#__PURE__*/React.createElement(Logo, {
    size: 26
  }), /*#__PURE__*/React.createElement("span", {
    style: {
      color: 'var(--text-muted)',
      fontSize: 14
    }
  }, "Community-owned \xB7 Made by players, for players"), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 8
    }
  }, /*#__PURE__*/React.createElement(IconButton, {
    icon: /*#__PURE__*/React.createElement(Icon, {
      name: "instagram"
    }),
    label: "Instagram",
    variant: "subtle",
    round: true
  }), /*#__PURE__*/React.createElement(IconButton, {
    icon: /*#__PURE__*/React.createElement(Icon, {
      name: "youtube"
    }),
    label: "YouTube",
    variant: "subtle",
    round: true
  }), /*#__PURE__*/React.createElement(IconButton, {
    icon: /*#__PURE__*/React.createElement(Icon, {
      name: "mail"
    }),
    label: "Email",
    variant: "subtle",
    round: true
  })))));
}
const PageWrap = ({
  children,
  width = 1100
}) => /*#__PURE__*/React.createElement("div", {
  style: {
    maxWidth: width,
    margin: '0 auto',
    padding: '28px 24px'
  }
}, children);
Object.assign(window, {
  Icon,
  refreshIcons,
  Logo,
  Shell,
  PageWrap
});
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/webapp/shell.jsx", error: String((e && e.message) || e) }); }

__ds_ns.EventCard = __ds_scope.EventCard;

__ds_ns.MatchResult = __ds_scope.MatchResult;

__ds_ns.PlayerCard = __ds_scope.PlayerCard;

__ds_ns.TeamCard = __ds_scope.TeamCard;

__ds_ns.Alert = __ds_scope.Alert;

__ds_ns.Avatar = __ds_scope.Avatar;

__ds_ns.AvatarStack = __ds_scope.AvatarStack;

__ds_ns.Badge = __ds_scope.Badge;

__ds_ns.EmptyState = __ds_scope.EmptyState;

__ds_ns.ProgressBar = __ds_scope.ProgressBar;

__ds_ns.Spinner = __ds_scope.Spinner;

__ds_ns.Tag = __ds_scope.Tag;

__ds_ns.Button = __ds_scope.Button;

__ds_ns.Checkbox = __ds_scope.Checkbox;

__ds_ns.FormField = __ds_scope.FormField;

__ds_ns.IconButton = __ds_scope.IconButton;

__ds_ns.Input = __ds_scope.Input;

__ds_ns.Radio = __ds_scope.Radio;

__ds_ns.Select = __ds_scope.Select;

__ds_ns.Switch = __ds_scope.Switch;

__ds_ns.Textarea = __ds_scope.Textarea;

__ds_ns.Accordion = __ds_scope.Accordion;

__ds_ns.Card = __ds_scope.Card;

__ds_ns.Stat = __ds_scope.Stat;

__ds_ns.Tabs = __ds_scope.Tabs;

__ds_ns.Breadcrumbs = __ds_scope.Breadcrumbs;

__ds_ns.NavBar = __ds_scope.NavBar;

__ds_ns.Pagination = __ds_scope.Pagination;

})();
