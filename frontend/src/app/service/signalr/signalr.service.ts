import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject } from 'rxjs';
import { environment } from '../../environment/environment';
import { ReminderModel } from '../../models/reminder.model';

export interface NotificationPayload {
  title: string;
  message: string;
  timestamp: string;
  data?: any;
}

@Injectable({ providedIn: 'root' })
export class SignalrService {
  private hubConnection!: signalR.HubConnection;
  private notificationSubject = new BehaviorSubject<ReminderModel | null>(null);
  notification$ = this.notificationSubject.asObservable();

  private reminderReadSubject = new BehaviorSubject<number | null>(null);
reminderRead$ = this.reminderReadSubject.asObservable();
  startConnection(onNotify?: (data: ReminderModel) => void) {
    if (this.hubConnection) return; // tránh khởi tạo nhiều lần

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${environment.realtimeURL}/notificationHub`, {
  accessTokenFactory: () => {
    const token = localStorage.getItem('accessToken');
    console.log('[SignalR] Using access token:', token);
    return token || '';
  },
})     
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)   
      .build();
    this.hubConnection.start()
      .then(() => {
        console.log('[SignalR] Connected to NotiicationHub');
          this.hubConnection.on('ReceiveNotification', (data: NotificationPayload) => {
      console.log('====================================================');
      console.log('[SignalR] Notification received!');
      console.log('[SignalR] Raw payload:', data);
      console.log('[SignalR] JSON:', JSON.stringify(data));
      console.log(' Title:', data.title);
      console.log(' Message:', data.message);
      console.log(' Timestamp:', data.timestamp);
      if (data.data) console.log('Extra Data:', data.data);
      console.log('====================================================');
       const reminder: ReminderModel = {
    reminderId: data.data?.reminderId ?? 0,
    title: data.title,
    message: data.message,
    task: data.data?.task || null,
    progressId: data.data?.progressId ?? null,
    isRead: data.data?.isRead,
    createdBy: data.data?.createdBy || '',
    createdAt: data.timestamp,
    isOwner: data.data?.isOwner
  };
      this.notificationSubject.next(reminder);
      if (onNotify) onNotify(reminder);
    });
  this.hubConnection.on('ReminderRead', (reminderId: number) => {
  console.log(`[SignalR] ReminderRead event: reminderId = ${reminderId}`);
  this.reminderReadSubject.next(reminderId);
});
})
    .catch(err => console.error('[SignalR] Error starting connection', err));

  this.hubConnection.onclose(err => 
  { console.warn('[signalR] Connection close');}
  );
}   

    

  sendMessage(method: string, data: any) {
    if (!this.hubConnection) {
      console.error('[SignalR] Hub connection chưa khởi tạo!');
      return;
    }
    return this.hubConnection.invoke(method, data);
  }
  sendReminderRead(reminderId: number) {
  if (!this.hubConnection || this.hubConnection.state !== signalR.HubConnectionState.Connected) {
    console.warn('[SignalR] Hub chưa kết nối, không thể gửi ReminderRead');
    return;
  }

  this.hubConnection.invoke('SendReminderRead', reminderId)
    .then(() => console.log('[SignalR] Đã gửi sự kiện ReminderRead:', reminderId))
    .catch(err => console.error('[SignalR] Lỗi gửi ReminderRead:', err));
}

}
