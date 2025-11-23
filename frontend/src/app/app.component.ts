import { Component, OnInit } from '@angular/core';
import { RouterModule } from '@angular/router';
import { NotificationPayload, SignalrService } from './service/signalr/signalr.service';



@Component({
  selector: 'app-root',
  imports: [RouterModule],
  template: `
    <main>
      <router-outlet></router-outlet>
    </main>
  `,
  styleUrl: './app.component.css',
})
export class AppComponent{
  title = 'AI-Task';
//  messages: NotificationPayload[] = [];

//   constructor(private signalrService: SignalrService) {}

//   ngOnInit(): void {
//     this.signalrService.startConnection((msg: NotificationPayload) => {
//       this.messages.push(msg);
//       console.log('[AppComponent] 📩 Notification:', msg);
//     });
//   }
}

