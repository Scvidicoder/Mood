const dateFormatter = new Intl.DateTimeFormat("en-GB", {
  dateStyle: "medium",
  timeStyle: "short",
});

export function formatMoney(value: number, currency = "TJS"): string {
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency,
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(value);
}

export function formatMoneyMinor(value: number): string {
  return formatMoney(value / 100);
}

export function formatMoneyModifier(value: number): string {
  if (value === 0) {
    return "Included";
  }

  return `${value > 0 ? "+" : "−"}${formatMoney(Math.abs(value))}`;
}

export function formatDate(value: string): string {
  return dateFormatter.format(new Date(value));
}
