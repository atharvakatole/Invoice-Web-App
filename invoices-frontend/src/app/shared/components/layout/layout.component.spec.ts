import { describe, expect, it } from 'vitest';
import { LayoutComponent } from './layout.component';

describe('LayoutComponent responsive navigation state', () => {
  function createComponent() {
    return new LayoutComponent({} as any, {} as any, {} as any, {} as any, {} as any);
  }

  it('opens and closes the manager drawer across repeated toggles', () => {
    const component = createComponent();
    expect(component.sidebarOpen()).toBe(false);
    component.toggleSidebar();
    expect(component.sidebarOpen()).toBe(true);
    component.toggleSidebar();
    expect(component.sidebarOpen()).toBe(false);
    component.toggleSidebar();
    component.closeSidebar();
    expect(component.sidebarOpen()).toBe(false);
  });

  it('closes the theme menu when a theme is selected', () => {
    const theme = { apply: () => undefined } as any;
    const component = new LayoutComponent({} as any, {} as any, {} as any, theme, {} as any);
    component.themeMenuOpen.set(true);
    component.pickTheme('linen');
    expect(component.themeMenuOpen()).toBe(false);
  });
});
