import { afterEach, describe, expect, it, vi } from "vitest";
import { launchHostedPayment } from "../features/payments/launchHostedPayment";

describe("hosted payment launch", () => {
  afterEach(() => {
    document.body.innerHTML = "";
    vi.restoreAllMocks();
  });

  it("submits a temporary top-level POST form and removes it immediately", () => {
    const submit = vi
      .spyOn(HTMLFormElement.prototype, "submit")
      .mockImplementation(() => undefined);

    launchHostedPayment({
      paymentId: "payment-1",
      actionUrl: "https://test-web.alif.tj/",
      method: "POST",
      formFields: {
        key: "merchant-key",
        token: "payment-token",
        amount: "10.00",
      },
    });

    expect(submit).toHaveBeenCalledOnce();
    const submittedForm = submit.mock.instances[0] as HTMLFormElement;
    expect(submittedForm.method).toBe("post");
    expect(submittedForm.action).toBe("https://test-web.alif.tj/");
    expect(new FormData(submittedForm).get("token")).toBe("payment-token");
    expect(document.querySelector("form")).toBeNull();
    expect(window.localStorage.length).toBe(0);
    expect(window.sessionStorage.length).toBe(0);
  });
});
