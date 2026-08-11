export interface CustomerProfile {
  name: string;
  phoneNumber: string;
  phoneVerified: boolean;
  telegramLinked: boolean;
  registrationDate: string;
  activeOrderCount: number;
  completedOrderCount: number;
  rowVersion: string;
}

export interface UpdateCustomerProfileInput {
  name: string;
  rowVersion: string;
}
