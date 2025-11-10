# Theme System Guide

## Quick Start: Editing Your Theme

Edit **only these 3 colors** in `wwwroot/app.css`:

```css
--color-primary-base: #c52a5b;      /* Main brand color */
--color-secondary-base: #00c44b;    /* Neutral/accent */
--color-accent-base: #198754;       /* Success color */
```

Everything else automatically derives from these using `color-mix()`.

---

## What This Theme System Does

### 1. **Automatic Color Derivation**
All shades (darker, lighter) are calculated automatically using CSS `color-mix()`:
- **Darker shades**: Mix base color with black (e.g., 80% base + 20% black)
- **Lighter shades**: Mix base color with white (e.g., 60% base + 40% white)

Example:
```css
--color-primary-darker: color-mix(in srgb, var(--color-primary-base) 80%, black);
```

### 2. **Gradient Background**
The page background uses a subtle diagonal gradient mixing primary and secondary colors:
```css
background: linear-gradient(135deg, 
    color-mix(in srgb, var(--bs-primary) 5%, white) 0%, 
    color-mix(in srgb, var(--bs-secondary) 3%, white) 100%);
```

### 3. **Smart Text Contrast**
- **Dark backgrounds** (sidebar): Use `--color-text-on-dark` (white)
- **Light backgrounds** (main content): Use `--bs-body-color` (dark gray)
- **Primary color backgrounds** (active nav items): Use `--color-text-on-primary` (white)

### 4. **Form Elements**
- Input fields have white background for readability
- Borders are tinted with primary color
- Focus states use primary color with ring effect

### 5. **Component Coverage**

**What changes with theme:**
- ✅ All Bootstrap buttons (`.btn-primary`, `.btn-secondary`, etc.)
- ✅ Alerts (`.alert-success`, `.alert-danger`, etc.)
- ✅ Badges (`.badge`)
- ✅ Page gradient background
- ✅ Sidebar gradient (dark primary shades)
- ✅ Top navigation bar (subtle primary tint)
- ✅ Navigation links (active state uses primary)
- ✅ Form borders and focus states
- ✅ Headings (tinted with primary)
- ✅ Links (use primary color)
- ✅ Cards (subtle primary tint)

**What stays neutral:**
- ❌ Input field backgrounds (white for readability)
- ❌ Body text color (dark for contrast)

---

## How color-mix() Works

```css
color-mix(in srgb, color1 percentage%, color2)
```

- `in srgb`: Color space (standard RGB)
- `color1`: Base color
- `percentage%`: How much of color1 to use
- `color2`: Color to mix with

**Examples:**
```css
/* 80% primary + 20% black = darker primary */
color-mix(in srgb, var(--bs-primary) 80%, black)

/* 60% primary + 40% white = lighter primary */
color-mix(in srgb, var(--bs-primary) 60%, white)

/* 5% primary + 95% white = very subtle tint */
color-mix(in srgb, var(--bs-primary) 5%, white)
```

---

## Updating RGB Values

**Important:** When changing base colors, update the RGB values manually:

1. Convert your hex color to RGB (use online converter)
2. Update the corresponding RGB variable

```css
--bs-primary: #c52a5b;
--bs-primary-rgb: 197, 42, 91;  /* ← Must match hex above! */
```

Bootstrap uses RGB values for opacity/transparency effects.

---

## Browser Support

`color-mix()` requires:
- Chrome 111+ (February 2023)
- Firefox 113+ (May 2023)
- Safari 16.2+ (December 2022)
- Edge 111+ (March 2023)

**All modern browsers support this.** No fallback needed for 2024+ projects.

---

## Accessibility Considerations

### Contrast Ratios

When choosing colors, ensure sufficient contrast:
- **Normal text**: 4.5:1 minimum
- **Large text** (18pt+): 3:1 minimum
- **UI components**: 3:1 minimum

**Test your colors:**
1. Use browser DevTools or online contrast checker
2. Test with primary color on white background
3. Test white text on primary color background

### Text Color Strategy

The theme uses **industry-standard approach**:

1. **Light backgrounds** → Dark text (high contrast)
2. **Dark backgrounds** → Light text (high contrast)
3. **Colored backgrounds** → White or black text (depends on color brightness)

**Our implementation:**
```css
/* Light gradient body → Dark text */
--bs-body-color: #212529;

/* Dark sidebar → White text */
--color-text-on-dark: white;

/* Primary colored elements → White text */
--color-text-on-primary: white;

/* Headings → Tinted with primary but still dark */
--bs-heading-color: color-mix(in srgb, var(--bs-primary) 80%, black);
```

### Not Too Strict with 3 Colors

**You're right to question strict adherence!** Industry standard:

- **3 base colors** for brand identity
- **Neutral colors** (white, black, grays) for text and backgrounds
- **Semantic colors** (red for errors) stay independent

**This theme follows best practices:**
- ✅ Brand colors derive from your 3 base colors
- ✅ Text uses neutral black/white for readability
- ✅ Danger/error stays Bootstrap red (universal understanding)
- ✅ Backgrounds mix brand colors with white (subtle branding)

---

## Troubleshooting

### "Colors don't look different"
- Check that you saved `app.css`
- Hard refresh browser (Ctrl+Shift+R / Cmd+Shift+R)
- Verify RGB values match hex colors

### "Text is hard to read"
- Adjust `--bs-body-bg-solid` percentage (currently 4%)
- Lower percentage = more subtle gradient
- Ensure primary color isn't too light or too dark

### "Input fields blend into background"
- Input fields intentionally stay white for best readability
- This is standard practice (Google, Microsoft, Apple all do this)
- Borders are tinted with primary color for brand consistency

### "I want stronger theme presence"
- Increase gradient percentages in `--bs-body-bg` (currently 5% and 3%)
- Increase top-row background from 8% to 12%
- Add tint to card backgrounds (currently 5%)

---

## Architecture Notes

### Where Theme Is Applied

1. **wwwroot/app.css**: Global variables and body styles
2. **Components/Layout/MainLayout.razor.css**: Sidebar and top-row
3. **Components/Layout/NavMenu.razor.css**: Navigation links

### Component-Scoped CSS

Blazor uses scoped CSS (`.razor.css` files). These styles are isolated per component, so:
- Theme variables work across all scoped CSS
- Must explicitly use `var(--bs-primary)` instead of hardcoded colors
- Cannot be overridden from parent stylesheets

---

## Examples: Customizing Further

### Make Gradient Stronger
```css
--bs-body-bg: linear-gradient(135deg, 
    color-mix(in srgb, var(--bs-primary) 12%, white) 0%,  /* 5% → 12% */
    color-mix(in srgb, var(--bs-secondary) 8%, white) 100%); /* 3% → 8% */
```

### Make Sidebar Lighter
```css
background-image: linear-gradient(180deg, 
    color-mix(in srgb, var(--bs-primary) 70%, black) 0%,  /* 60% → 70% */
    color-mix(in srgb, var(--bs-primary) 40%, black) 70%); /* 30% → 40% */
```

### Add Tint to Input Fields
```css
--bs-body-bg-input: color-mix(in srgb, var(--bs-primary) 2%, white);
```

---

## Summary of Industry Standards Used

1. **3-Color Rule**: Base brand palette (primary, secondary, accent)
2. **Neutral Text**: Black/white for readability (not derived from brand)
3. **Semantic Colors**: Red for errors (universal convention)
4. **White Inputs**: Standard practice for form fields
5. **Automatic Contrast**: Light backgrounds → dark text, dark backgrounds → light text
6. **Subtle Gradients**: 3-8% brand color mixed with white for backgrounds
7. **Strong Accents**: 50-80% brand color for interactive elements

**You have a professional, accessible, and flexible theme system!** 🎨
