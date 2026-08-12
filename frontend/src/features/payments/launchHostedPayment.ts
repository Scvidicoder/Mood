import type { PaymentLaunch } from "../../types/orders";

export function launchHostedPayment(launch: PaymentLaunch): void {
  if (launch.method === "GET") {
    window.location.assign(launch.actionUrl);
    return;
  }

  const form = document.createElement("form");
  form.method = launch.method;
  form.action = launch.actionUrl;
  form.hidden = true;

  for (const [name, value] of Object.entries(launch.formFields)) {
    const input = document.createElement("input");
    input.type = "hidden";
    input.name = name;
    input.value = value;
    form.append(input);
  }

  document.body.append(form);
  try {
    form.submit();
  } finally {
    form.remove();
  }
}
