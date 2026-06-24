import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CalendarService, CalendarEvent } from '../../core/services/calendar.service';
import { ToastService } from '../../core/services/toast.service';

interface DayCell {
  date: Date | null;
  isToday: boolean;
  events: CalendarEvent[];
}

const MONTH_NAMES = ['January','February','March','April','May','June','July','August','September','October','November','December'];

@Component({
  selector: 'app-calendar',
  standalone: true,
  imports: [CommonModule, FormsModule, CurrencyPipe],
  templateUrl: './calendar.component.html',
  styleUrl: './calendar.component.scss'
})
export class CalendarComponent implements OnInit {
  loading = signal(true);
  current = signal(new Date(new Date().getFullYear(), new Date().getMonth(), 1));
  events = signal<CalendarEvent[]>([]);
  selectedDay = signal<DayCell | null>(null);

  showAddForm = signal(false);
  newTitle = signal('');
  newDate = signal('');
  newNotes = signal('');
  saving = signal(false);

  monthLabel = computed(() => {
    const d = this.current();
    return `${MONTH_NAMES[d.getMonth()]} ${d.getFullYear()}`;
  });

  weeks = computed(() => {
    const d = this.current();
    const year = d.getFullYear();
    const month = d.getMonth();
    const firstDay = new Date(year, month, 1);
    const startOffset = firstDay.getDay(); // 0=Sun
    const daysInMonth = new Date(year, month + 1, 0).getDate();
    const today = new Date();

    const cells: DayCell[] = [];
    for (let i = 0; i < startOffset; i++) cells.push({ date: null, isToday: false, events: [] });

    for (let day = 1; day <= daysInMonth; day++) {
      const date = new Date(year, month, day);
      const isToday = date.toDateString() === today.toDateString();
      const dayEvents = this.events().filter(e => {
        const ed = new Date(e.date);
        return ed.getFullYear() === year && ed.getMonth() === month && ed.getDate() === day;
      });
      cells.push({ date, isToday, events: dayEvents });
    }

    while (cells.length % 7 !== 0) cells.push({ date: null, isToday: false, events: [] });

    const weeks: DayCell[][] = [];
    for (let i = 0; i < cells.length; i += 7) weeks.push(cells.slice(i, i + 7));
    return weeks;
  });

  monthSummary = computed(() => {
    const evts = this.events();
    const projectDays = new Set(evts.filter(e => e.type === 'invoice-item').map(e => e.date.slice(0, 10))).size;
    const assistantDays = evts.filter(e => e.type === 'assistant').length;
    const earnings = evts.filter(e => e.type === 'invoice-item').reduce((s, e) => s + (e.amount || 0), 0);
    const assistantUnpaid = evts.filter(e => e.type === 'assistant' && !e.isPaid).reduce((s, e) => s + (e.amount || 0), 0);
    return { projectDays, assistantDays, earnings, assistantUnpaid };
  });

  constructor(private calendarService: CalendarService, private toast: ToastService) {}

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading.set(true);
    const d = this.current();
    this.calendarService.getEvents(d.getFullYear(), d.getMonth() + 1).subscribe({
      next: (evts) => { this.events.set(evts); this.loading.set(false); this.selectedDay.set(null); },
      error: () => { this.loading.set(false); this.toast.error('Could not load calendar events'); }
    });
  }

  prevMonth() {
    const d = this.current();
    this.current.set(new Date(d.getFullYear(), d.getMonth() - 1, 1));
    this.load();
  }

  nextMonth() {
    const d = this.current();
    this.current.set(new Date(d.getFullYear(), d.getMonth() + 1, 1));
    this.load();
  }

  goToday() {
    this.current.set(new Date(new Date().getFullYear(), new Date().getMonth(), 1));
    this.load();
  }

  selectDay(cell: DayCell) {
    if (!cell.date) return;
    this.selectedDay.set(cell);
  }

  // ===================== Add/remove events =====================

  openAddForm(presetDate?: Date) {
    this.newTitle.set('');
    this.newNotes.set('');
    const d = presetDate ?? this.selectedDay()?.date ?? new Date();
    this.newDate.set(this.toIsoDate(d));
    this.showAddForm.set(true);
  }

  closeAddForm() {
    this.showAddForm.set(false);
  }

  private toIsoDate(d: Date): string {
    const tzOffset = d.getTimezoneOffset() * 60000;
    return new Date(d.getTime() - tzOffset).toISOString().slice(0, 10);
  }

  saveEvent() {
    if (!this.newTitle().trim()) {
      this.toast.error('Enter a title for this event');
      return;
    }
    if (!this.newDate()) {
      this.toast.error('Pick a date');
      return;
    }

    this.saving.set(true);
    this.calendarService.createEvent(
      this.newTitle().trim(),
      new Date(this.newDate()).toISOString(),
      this.newNotes().trim() || undefined
    ).subscribe({
      next: () => {
        this.saving.set(false);
        this.showAddForm.set(false);
        this.toast.success('Added to your schedule');
        this.load();
      },
      error: (err) => {
        this.saving.set(false);
        this.toast.error(err?.error || 'Could not add event');
      }
    });
  }

  deleteEvent(e: CalendarEvent) {
    if (e.type !== 'project' || !e.relatedId) return;

    this.calendarService.deleteEvent(e.relatedId).subscribe({
      next: () => {
        this.events.update(list => list.filter(x => x !== e));
        this.toast.success('Removed from schedule');
        // refresh selected day view
        const day = this.selectedDay();
        if (day) {
          this.selectedDay.set({ ...day, events: day.events.filter(x => x !== e) });
        }
      },
      error: () => this.toast.error('Could not remove event')
    });
  }
}
