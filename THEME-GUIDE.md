# Theme Management Guide — Konfucjusz

## Overview

The application now uses a **3-Color Palette System** with CSS custom properties (CSS variables). All colors derive from 3 base colors for easy theme management and consistent design.

## The 3 Base Colors

Located at the top of `wwwroot/app.css`:

```css
:root {
    /* ============= BASE COLORS (EDIT THESE) ============= */
    --color-primary-base: #1b6ec2;      /* Blue - your main brand color */
    --color-secondary-base: #6c757d;    /* Gray - neutral color */
    --color-accent-base: #198754;       /* Green - success/positive actions */
}
```

**To change your app's entire color scheme**: Just edit these 3 values!

## How It Works

### 1. Base Colors → Derived Shades

Each base color generates lighter/darker shades automatically referenced throughout the CSS:

- **Primary** (brand color): Used for links, buttons, focus states
  - `--color-primary-darker`: Button borders
  - `--color-primary-dark`: Hover states
  - `--color-primary`: Main brand color
  - `--color-primary-light`: Focus rings
  - `--color-primary-lighter`: Link color

- **Secondary** (neutral): Used for borders, disabled states
  - `--color-secondary-darker` through `--color-secondary-lighter`

- **Accent** (success/error): Used for validation, alerts
  - `--color-success-*`: Positive feedback
  - `--color-error-*`: Negative feedback, validation errors

### 2. Semantic Variables (Use These in Your Code)

Instead of using color values directly, use semantic names:

```css
/* Text Colors */
--color-text-primary: Main text color
--color-text-secondary: Secondary/muted text
--color-text-link: Link text
--color-text-error: Error messages

/* Background Colors */
--color-bg-body: Page background
--color-bg-error: Error boundary background

/* Border Colors */
--color-border-default: Default borders
--color-border-primary: Primary button borders
--color-border-success: Validation success
--color-border-error: Validation errors

/* Focus States */
--color-focus-ring: Focus indicator color
```

## Examples

### Changing Your Brand Color

Want a purple theme instead of blue?

```css
:root {
    --color-primary-base: #7c3aed;  /* Purple */
}
```

All buttons, links, and focus states will update automatically!

### Adding Custom Components

When creating new components, use semantic variables:

```css
.my-custom-component {
    color: var(--color-text-primary);
    background-color: var(--color-bg-body);
    border: 1px solid var(--color-border-default);
}

.my-custom-component:hover {
    border-color: var(--color-primary);
}
```

### Creating Dark Mode (Future)

You can override these variables in a media query:

```css
@media (prefers-color-scheme: dark) {
    :root {
        --color-primary-base: #60a5fa;
        --color-secondary-base: #9ca3af;
        --color-accent-base: #34d399;
        
        --color-text-primary: #f9fafb;
        --color-bg-body: #1f2937;
    }
}
```

## Current Color Mapping

### Where Each Color Is Used

| Element | CSS Variable | Default Value |
|---------|--------------|---------------|
| Links | `--color-text-link` | #006bb7 (blue) |
| Primary Button BG | `--color-primary` | #1b6ec2 (blue) |
| Primary Button Border | `--color-border-primary` | #1861ac (darker blue) |
| Focus Ring | `--color-focus-ring` | #258cfb (light blue) |
| Validation Success | `--color-border-success` | #26b050 (green) |
| Validation Error | `--color-text-error` | #e50000 (red) |
| Form Borders | `--color-border-default` | #929292 (gray) |

## File Structure

```
wwwroot/
├── app.css              ← Your theme variables (EDIT HERE)
└── bootstrap/
    └── bootstrap.min.css ← Bootstrap framework (DO NOT EDIT)
```

**Important**: Bootstrap has its own CSS variables (prefixed with `--bs-`). Our custom theme system complements Bootstrap but doesn't replace it.

## Best Practices

1. **Always use semantic variables** in new CSS:
   ```css
   ✅ color: var(--color-text-primary);
   ❌ color: #212529;
   ```

2. **Create new semantic variables** for new purposes:
   ```css
   :root {
       --color-card-shadow: rgba(0, 0, 0, 0.1);
   }
   ```

3. **Keep the 3 base colors in sync** with your brand:
   - Primary = Brand color (blue, purple, etc.)
   - Secondary = Neutral/grayscale
   - Accent = Success/positive actions (green, teal, etc.)

4. **Test your changes** by editing the 3 base colors and checking all pages:
   - Links should change color
   - Buttons should update
   - Focus states should match
   - Validation states should remain clear

## Maintenance

When adding new pages or components:

1. Use existing semantic variables whenever possible
2. If you need a new color purpose, add it to the semantic section in `app.css`
3. Derive it from one of the 3 base colors to maintain consistency

## Accessibility

Ensure sufficient contrast when changing colors:
- Text on background: at least 4.5:1 contrast ratio
- Links should be clearly distinguishable
- Focus states must be visible (use browser dev tools or online contrast checkers)

---

**Questions?** Check `wwwroot/app.css` for the complete variable list and current mappings.
