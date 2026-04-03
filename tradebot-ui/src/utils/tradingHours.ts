const MARKET_TIMEZONE = 'America/New_York';
const MARKET_OPEN_HOUR = 9;
const MARKET_OPEN_MINUTE = 30;
const MARKET_CLOSE_HOUR = 16;
const MARKET_CLOSE_MINUTE = 0;

type EasternParts = {
  year: number;
  month: number;
  day: number;
  hour: number;
  minute: number;
  second: number;
  weekday: number;
};

export type MarketSessionState = 'pre-market' | 'open' | 'after-hours' | 'holiday' | 'weekend';

export type MarketSessionInfo = {
  state: MarketSessionState;
  isOpen: boolean;
  nextSessionStart: Date;
  nextSessionStartLabel: string;
  message: string;
};

const easternDateFormatter = new Intl.DateTimeFormat('en-US', {
  timeZone: MARKET_TIMEZONE,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  hour12: false,
  weekday: 'short',
});

const easternOffsetFormatter = new Intl.DateTimeFormat('en-US', {
  timeZone: MARKET_TIMEZONE,
  timeZoneName: 'shortOffset',
  hour: '2-digit',
});

function getWeekdayIndex(weekday: string): number {
  const map: Record<string, number> = {
    Sun: 0,
    Mon: 1,
    Tue: 2,
    Wed: 3,
    Thu: 4,
    Fri: 5,
    Sat: 6,
  };

  return map[weekday] ?? 0;
}

function getEasternParts(now = new Date()): EasternParts {
  const values = easternDateFormatter.formatToParts(now);
  const part = (type: Intl.DateTimeFormatPartTypes) => values.find((value) => value.type === type)?.value ?? '0';

  return {
    year: Number(part('year')),
    month: Number(part('month')),
    day: Number(part('day')),
    hour: Number(part('hour')),
    minute: Number(part('minute')),
    second: Number(part('second')),
    weekday: getWeekdayIndex(part('weekday')),
  };
}

function getEasternOffsetMinutes(now: Date): number {
  const timeZoneName = easternOffsetFormatter
    .formatToParts(now)
    .find((value) => value.type === 'timeZoneName')?.value;

  const match = timeZoneName?.match(/GMT([+-])(\d{1,2})(?::(\d{2}))?/);
  if (!match) {
    return 0;
  }

  const sign = match[1] === '-' ? -1 : 1;
  const hours = Number(match[2]);
  const minutes = Number(match[3] ?? '0');
  return sign * (hours * 60 + minutes);
}

function toUtcDateFromEastern(
  year: number,
  month: number,
  day: number,
  hour = 0,
  minute = 0,
  second = 0,
): Date {
  const utcGuess = new Date(Date.UTC(year, month - 1, day, hour, minute, second));
  const offsetMinutes = getEasternOffsetMinutes(utcGuess);
  return new Date(utcGuess.getTime() - offsetMinutes * 60_000);
}

function pad(value: number): string {
  return String(value).padStart(2, '0');
}

function toDateKey(year: number, month: number, day: number): string {
  return `${year}-${pad(month)}-${pad(day)}`;
}

function formatEasternDateTime(date: Date): string {
  return new Intl.DateTimeFormat('en-US', {
    timeZone: MARKET_TIMEZONE,
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
    timeZoneName: 'short',
  }).format(date);
}

function getObservedHoliday(year: number, month: number, day: number): string {
  const holiday = new Date(Date.UTC(year, month - 1, day));
  const weekday = holiday.getUTCDay();

  if (weekday === 6) {
    holiday.setUTCDate(holiday.getUTCDate() - 1);
  } else if (weekday === 0) {
    holiday.setUTCDate(holiday.getUTCDate() + 1);
  }

  return toDateKey(holiday.getUTCFullYear(), holiday.getUTCMonth() + 1, holiday.getUTCDate());
}

function getNthWeekdayOfMonth(year: number, month: number, weekday: number, occurrence: number): string {
  const firstDay = new Date(Date.UTC(year, month - 1, 1));
  const firstWeekday = firstDay.getUTCDay();
  const offset = (7 + weekday - firstWeekday) % 7;
  const day = 1 + offset + (occurrence - 1) * 7;
  return toDateKey(year, month, day);
}

function getLastWeekdayOfMonth(year: number, month: number, weekday: number): string {
  const lastDay = new Date(Date.UTC(year, month, 0));
  const offset = (7 + lastDay.getUTCDay() - weekday) % 7;
  return toDateKey(year, month, lastDay.getUTCDate() - offset);
}

function getEasterSunday(year: number): { month: number; day: number } {
  const a = year % 19;
  const b = Math.floor(year / 100);
  const c = year % 100;
  const d = Math.floor(b / 4);
  const e = b % 4;
  const f = Math.floor((b + 8) / 25);
  const g = Math.floor((b - f + 1) / 3);
  const h = (19 * a + b - d - g + 15) % 30;
  const i = Math.floor(c / 4);
  const k = c % 4;
  const l = (32 + 2 * e + 2 * i - h - k) % 7;
  const m = Math.floor((a + 11 * h + 22 * l) / 451);
  const month = Math.floor((h + l - 7 * m + 114) / 31);
  const day = ((h + l - 7 * m + 114) % 31) + 1;
  return { month, day };
}

function getGoodFriday(year: number): string {
  const easter = getEasterSunday(year);
  const easterDate = new Date(Date.UTC(year, easter.month - 1, easter.day));
  easterDate.setUTCDate(easterDate.getUTCDate() - 2);
  return toDateKey(easterDate.getUTCFullYear(), easterDate.getUTCMonth() + 1, easterDate.getUTCDate());
}

function getMarketHolidays(year: number): Set<string> {
  return new Set([
    getObservedHoliday(year, 1, 1),
    getNthWeekdayOfMonth(year, 1, 1, 3),
    getNthWeekdayOfMonth(year, 2, 1, 3),
    getGoodFriday(year),
    getLastWeekdayOfMonth(year, 5, 1),
    getObservedHoliday(year, 6, 19),
    getObservedHoliday(year, 7, 4),
    getNthWeekdayOfMonth(year, 9, 1, 1),
    getNthWeekdayOfMonth(year, 11, 4, 4),
    getObservedHoliday(year, 12, 25),
  ]);
}

function isTradingHolidayParts(parts: EasternParts): boolean {
  return getMarketHolidays(parts.year).has(toDateKey(parts.year, parts.month, parts.day));
}

function isTradingDayParts(parts: EasternParts): boolean {
  if (parts.weekday === 0 || parts.weekday === 6) {
    return false;
  }

  return !isTradingHolidayParts(parts);
}

function addEasternDays(parts: EasternParts, daysToAdd: number): EasternParts {
  const baseUtc = new Date(Date.UTC(parts.year, parts.month - 1, parts.day + daysToAdd, 12, 0, 0));
  const shifted = getEasternParts(baseUtc);
  return {
    ...shifted,
    hour: 0,
    minute: 0,
    second: 0,
  };
}

export function isUsTradingHoliday(now?: Date): boolean {
  return isTradingHolidayParts(getEasternParts(now));
}

export function isMarketOpen(now?: Date): boolean {
  const easternNow = getEasternParts(now);
  if (!isTradingDayParts(easternNow)) {
    return false;
  }

  const currentMinutes = easternNow.hour * 60 + easternNow.minute;
  const openMinutes = MARKET_OPEN_HOUR * 60 + MARKET_OPEN_MINUTE;
  const closeMinutes = MARKET_CLOSE_HOUR * 60 + MARKET_CLOSE_MINUTE;

  return currentMinutes >= openMinutes && currentMinutes < closeMinutes;
}

export function getNextMarketOpen(now?: Date): Date {
  const easternNow = getEasternParts(now);
  const currentMinutes = easternNow.hour * 60 + easternNow.minute;
  const openMinutes = MARKET_OPEN_HOUR * 60 + MARKET_OPEN_MINUTE;

  if (isTradingDayParts(easternNow) && currentMinutes < openMinutes) {
    return toUtcDateFromEastern(
      easternNow.year,
      easternNow.month,
      easternNow.day,
      MARKET_OPEN_HOUR,
      MARKET_OPEN_MINUTE,
    );
  }

  let nextDay = addEasternDays(easternNow, 1);

  while (!isTradingDayParts(nextDay)) {
    nextDay = addEasternDays(nextDay, 1);
  }

  return toUtcDateFromEastern(
    nextDay.year,
    nextDay.month,
    nextDay.day,
    MARKET_OPEN_HOUR,
    MARKET_OPEN_MINUTE,
  );
}

export function formatTimeUntilOpen(now?: Date): string {
  const referenceNow = now ?? new Date();
  const nextOpen = getNextMarketOpen(referenceNow);
  const diffMs = nextOpen.getTime() - referenceNow.getTime();

  if (diffMs <= 0) {
    return 'opening soon';
  }

  const totalMinutes = Math.floor(diffMs / 60_000);
  const totalDays = Math.floor(totalMinutes / (24 * 60));
  const hours = Math.floor((totalMinutes % (24 * 60)) / 60);
  const minutes = totalMinutes % 60;

  if (totalDays > 0) {
    return `in ${totalDays}d ${hours}h ${minutes}m`;
  }

  if (hours > 0) {
    return `in ${hours}h ${minutes}m`;
  }

  return `in ${minutes}m`;
}

export function getMarketSessionInfo(now?: Date): MarketSessionInfo {
  const referenceNow = now ?? new Date();
  const easternNow = getEasternParts(referenceNow);
  const currentMinutes = easternNow.hour * 60 + easternNow.minute;
  const openMinutes = MARKET_OPEN_HOUR * 60 + MARKET_OPEN_MINUTE;
  const closeMinutes = MARKET_CLOSE_HOUR * 60 + MARKET_CLOSE_MINUTE;
  const nextSessionStart = getNextMarketOpen(referenceNow);
  const nextSessionStartLabel = formatEasternDateTime(nextSessionStart);

  if (easternNow.weekday === 0 || easternNow.weekday === 6) {
    return {
      state: 'weekend',
      isOpen: false,
      nextSessionStart,
      nextSessionStartLabel,
      message: `Weekend — next session starts ${nextSessionStartLabel}`,
    };
  }

  if (isTradingHolidayParts(easternNow)) {
    return {
      state: 'holiday',
      isOpen: false,
      nextSessionStart,
      nextSessionStartLabel,
      message: `Holiday — next session starts ${nextSessionStartLabel}`,
    };
  }

  if (currentMinutes < openMinutes) {
    return {
      state: 'pre-market',
      isOpen: false,
      nextSessionStart,
      nextSessionStartLabel,
      message: `Pre-market — session starts ${nextSessionStartLabel}`,
    };
  }

  if (currentMinutes < closeMinutes) {
    return {
      state: 'open',
      isOpen: true,
      nextSessionStart,
      nextSessionStartLabel,
      message: 'Market Open',
    };
  }

  return {
    state: 'after-hours',
    isOpen: false,
    nextSessionStart,
    nextSessionStartLabel,
    message: `After-hours — next session starts ${nextSessionStartLabel}`,
  };
}

export function getMarketStatusLabel(now?: Date): string {
  return getMarketSessionInfo(now).message;
}
