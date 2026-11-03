import { describe, expect, it, vi } from 'vitest';
import { of } from 'rxjs';
import { CalendarComponent } from './calendar.component';
import { parseCalendarEventDate, serializeLocalCalendarDate } from './calendar-date.utils';
import { CalendarService } from '../../core/services/calendar.service';

describe('calendar date semantics', () => {
  it('keeps date-only and UTC-midnight schedule days on their written local calendar day', () => {
    for (const input of ['2025-01-01', '2025-01-01T00:00:00', '2025-01-01T00:00:00Z', '2025-01-01T00:00:00+00:00']) {
      const parsed = parseCalendarEventDate(input);
      expect(parsed).not.toBeNull();
      expect([parsed!.getFullYear(), parsed!.getMonth(), parsed!.getDate()]).toEqual([2025, 0, 1]);
    }
  });

  it('keeps non-midnight timestamps as instants and ignores invalid dates', () => {
    const value = '2025-01-01T23:30:00Z';
    const parsed = parseCalendarEventDate(value)!;
    const instant = new Date(value);
    expect([parsed.getFullYear(), parsed.getMonth(), parsed.getDate()])
      .toEqual([instant.getFullYear(), instant.getMonth(), instant.getDate()]);
    expect(parseCalendarEventDate('2025-02-30')).toBeNull();
    expect(parseCalendarEventDate('not-a-date')).toBeNull();
  });

  it('serializes valid selected dates as UTC midnight without timezone drift', () => {
    expect(serializeLocalCalendarDate('2025-01-01')).toBe('2025-01-01T00:00:00.000Z');
    expect(serializeLocalCalendarDate('2024-02-29')).toBe('2024-02-29T00:00:00.000Z');
    expect(serializeLocalCalendarDate('2025-02-29')).toBeNull();
    expect(serializeLocalCalendarDate('')).toBeNull();
  });

  it('matches UTC-midnight events to the same month cell and serializes the selected day on save', () => {
    const service = {
      getEvents: vi.fn(() => of([])),
      createEvent: vi.fn(() => of({})),
      deleteEvent: vi.fn(() => of({}))
    } as unknown as CalendarService;
    const toast = { error: vi.fn(), success: vi.fn() } as any;
    const component = new CalendarComponent(service, toast, { load: vi.fn() } as any);
    component.current.set(new Date(2025, 0, 1));
    component.events.set([{ date: '2025-01-01T00:00:00Z', type: 'project', title: 'Project day' }]);

    const firstWeekCell = component.weeks().flat().find(cell => cell.date?.getDate() === 1)!;
    expect(firstWeekCell.events.map(event => event.title)).toEqual(['Project day']);

    component.newTitle.set('Milestone');
    component.newDate.set('2025-01-01');
    component.saveEvent();
    expect(service.createEvent).toHaveBeenCalledWith('Milestone', '2025-01-01T00:00:00.000Z', undefined, undefined);
  });

  it('rejects an invalid selected date without entering a stuck saving state', () => {
    const service = {
      getEvents: vi.fn(() => of([])),
      createEvent: vi.fn(() => of({})),
      deleteEvent: vi.fn(() => of({}))
    } as unknown as CalendarService;
    const toast = { error: vi.fn(), success: vi.fn() } as any;
    const component = new CalendarComponent(service, toast, { load: vi.fn() } as any);
    component.newTitle.set('Milestone');
    component.newDate.set('2025-02-29');

    component.saveEvent();

    expect(service.createEvent).not.toHaveBeenCalled();
    expect(component.saving()).toBe(false);
    expect(toast.error).toHaveBeenCalledWith('Pick a valid date');
  });
});
