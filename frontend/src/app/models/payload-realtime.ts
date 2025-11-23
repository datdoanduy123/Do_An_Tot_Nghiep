export interface NotificationPayload {
  title: string;
  message: string;
  timestamp: string;
  data?: any;
}
export type NotificationItem = NotificationPayload & { isRead: boolean };

