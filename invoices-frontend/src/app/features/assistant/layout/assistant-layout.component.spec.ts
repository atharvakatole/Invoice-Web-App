import { describe, expect, it } from 'vitest';
import { AssistantLayoutComponent } from './assistant-layout.component';

describe('AssistantLayoutComponent mobile navigation', () => {
  function createComponent() {
    return new AssistantLayoutComponent({} as any, {} as any, {} as any);
  }

  it('starts closed, opens when toggled, and closes after navigation', () => {
    const component = createComponent();

    expect(component.sidebarOpen()).toBe(false);
    component.toggleSidebar();
    expect(component.sidebarOpen()).toBe(true);
    component.closeSidebar();
    expect(component.sidebarOpen()).toBe(false);
  });

  it('allows closing the drawer if the toggle is used a second time', () => {
    const component = createComponent();

    component.toggleSidebar();
    component.toggleSidebar();

    expect(component.sidebarOpen()).toBe(false);
  });
});
