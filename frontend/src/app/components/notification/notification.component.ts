import { NotificationService } from './../../service/nofication.service';
import {
  Component,
  inject,
  OnInit,
  DestroyRef,
  ChangeDetectorRef,
  OnDestroy,
} from '@angular/core';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { ToastService } from '../../service/toast.service';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { PageEmptyComponent } from '../page-empty/page-empty.component';
import { convertToVietnameseDate } from '../../helper/convertToVNDate';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { NzCollapseModule } from 'ng-zorro-antd/collapse';
import { ReminderModel } from '../../models/reminder.model';
import { Router } from '@angular/router';
import { typeNotification } from '../../constants/constant';
import { catchError, Observable, of, Subscription, tap } from 'rxjs';
import { NotificationItem, NotificationPayload } from '../../models/payload-realtime';
import { SignalrService } from '../../service/signalr/signalr.service';
import { FormsModule } from '@angular/forms';
import { InfiniteScrollModule } from 'ngx-infinite-scroll';
import { Metadata } from '../../interface/response-paganation';
import { DEFAULT_METADATA } from '../../constants/constant'
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { finalize } from 'rxjs/operators';

@Component({
  selector: 'app-notification',
  imports: [
    NzDropDownModule,
    CommonModule,
    PageEmptyComponent,
    NzToolTipModule,
    NzCollapseModule,
    FormsModule,InfiniteScrollModule   
  ],
  templateUrl: './notification.component.html',
  styleUrl: './notification.component.css',
})
export class NotificationComponent implements OnInit,OnDestroy {
  private destroyRef = inject(DestroyRef);
  //phân trang
  page = 1;
  size = 10;
  hasNext = true;
  isReadd = false;
  isLoading = false;
  // listNotification: ReminderModel[] = [];
  isEmptlist = false;
  totalNotificationIsNotRead: number = 0;
  dateTimeNow = new Date().toLocaleDateString('vi-VN', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
});;
  data$!: Observable<{ items: ReminderModel[]; metaData: Metadata }>;
  // realtimeNotifications: NotificationItem[]=[];
  allNotifications: (ReminderModel)[] = [];

  //sub cho realtime
  private sub = new Subscription();

  constructor(
    private toastService: ToastService,
    private NotiService: NotificationService,
    private route: Router,
    private signalrService : SignalrService,
     private cdr: ChangeDetectorRef,
     private sanitizer: DomSanitizer
  ) {}
  

  ngOnInit(): void {
      this.refreshUnreadCount();

  this.signalrService.startConnection();

  // Nhận thông báo realtime
  this.sub.add(
    this.signalrService.notification$.subscribe(msg => {
      if (msg && !this.allNotifications.some(n => n.reminderId === msg.reminderId)) {
        this.allNotifications.unshift(msg);
        this.totalNotificationIsNotRead++;
        this.refreshUnreadCount();
        this.cdr.detectChanges();
      }
      console.log('[Reminder] Realtime Notification:', msg);
    })
  );

  // Nhận sự kiện đọc thông báo realtime
  this.sub.add(
    this.signalrService.reminderRead$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(reminderId => {
        if (!reminderId) return;
        const item = this.allNotifications.find(n => n.reminderId === reminderId);
        if (item && !item.isRead) {
          item.isRead = true;
          this.totalNotificationIsNotRead = Math.max(0, this.totalNotificationIsNotRead - 1);
          this.cdr.detectChanges();
        }
      })
  );

  this.loadReminders();
}

  ngOnDestroy(): void {
    this.sub.unsubscribe(); // tránh memory leak
  }
  // ngAfterViewChecked() {
  //   this.cdr.detectChanges();
  // }
  //Đánh dấu đã đọc đồng bộ
  onNotificationClick(item: any) {
 
  item.isRead = true;
  this.totalNotificationIsNotRead = Math.max(0, this.totalNotificationIsNotRead - 1);

  // Nếu có reminderId (DB) thì sync về API
  const reminderId = item?.reminderId || item?.data?.reminderId;
  
  if (reminderId) {
    this.NotiService.maskReminderRead(reminderId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {  console.log(`[SYNC] Marked read in DB: ${reminderId}`)
        this.signalrService.sendReminderRead(reminderId); 
        this.refreshUnreadCount();
      },
        error: (err) => console.error('[SYNC] Lỗi mark read:', err)
      });
  }
  // Điều hướng sang chi tiết task
   if(item.task == null) 
  {
        this.route.navigate(['/page-not-found']);
        return;
  }
  const taskId = item?.task?.taskId || item?.data?.taskId;
  const parentTaskId= item?.task?.parentTaskId || item?.data?.parentTaskId;
 
  console.log("KKK",taskId);
  if (item.task) {
  if (item.isOwner) {
    // Chủ sở hữu: vào trang việc quản lý
    this.route.navigate([`/viecquanly/chitiet/${parentTaskId}/review/${taskId}`]);
  } else {
    // Không phải chủ sở hữu: vào trang việc được giao
    this.route.navigate([`/viecduocgiao/chitiet/${taskId}`]);
  }
}
  // else {
  //   this.route.navigate([`/viecduocgiao/chitiet/${taskId}/review/${taskId}`]);
  // }
}

//đánh dấu đã đọc
maskreadNoti(reminderId: number) {
  if (!reminderId) return;

  this.NotiService.maskReminderRead(reminderId)
    // .pipe(takeUntilDestroyed(this.destroyRef))
    .subscribe({
      next: () => {
        console.log(`[Reminder] Marked as read: ${reminderId}`);
        this.totalNotificationIsNotRead = this.totalNotificationIsNotRead -1;
        this.isReadd = true;
      },
      error: (err) => {
        console.error('[Reminder] Lỗi mark read:', err);
        this.toastService.Error('Không thể đánh dấu đã đọc!');
      },
    });
}
 getNotificationDisplayText(item: ReminderModel): string {
    if (item.task?.taskId) {
      return `${item.message} (Task #${item.task.taskId})`;
    }
    return item.message;
  }
  //Hiển thị thông báo từ bao lâu trước đó
  getTimeAgo(dateString: string): string {
  if (!dateString) return '0 giây trước';
  
  const now = new Date();
  const past = new Date(dateString);
  
  past.setHours(past.getHours());
  
  const diffMs = now.getTime() - past.getTime();
  const diffSec = Math.floor(diffMs / 1000);
  
  if (diffSec < 0) return '0 giây trước'; // Xử lý trường hợp âm
  if (diffSec < 60) return `${diffSec} giây trước`;
  
  const diffMin = Math.floor(diffSec / 60);
  if (diffMin < 60) return `${diffMin} phút trước`;
  
  const diffHr = Math.floor(diffMin / 60);
  if (diffHr < 24) return `${diffHr} giờ trước`;
  
  const diffDay = Math.floor(diffHr / 24);
  return `${diffDay} ngày trước`;
}



  convertDate(date: string): string {
    return convertToVietnameseDate(date);
  }
  
//cuộn để load reminder
onScroll(): void {
  if (this.hasNext) {
    this.page++;
    console.log('Next page:', this.page);
    this.loadReminders();
  } 
}


loadReminders(): void {
  if (!this.hasNext || this.isLoading) return;

  this.isLoading = true;
  this.NotiService.onRefresh(this.page, this.size)
    .pipe(
      tap(({ items, metaData }) => {
        // Thêm dữ liệu mới vào cuối danh sách
        this.allNotifications = [...this.allNotifications, ...items];
        this.hasNext = metaData?.hasNext ?? false;
        this.isLoading = false;
        this.isEmptlist = this.allNotifications.length === 0;
        this.cdr.detectChanges();
      }),
       catchError(err => {
        this.toastService.Error(err.message || 'Lấy dữ liệu thất bại!');
        return of({ items: [], metaData: DEFAULT_METADATA });
      }),
      finalize(() => {
        this.isLoading = false;
      })
    )
    .subscribe();
}
//load lại tổng chưa đọc
private refreshUnreadCount() {
  this.NotiService.getUnreadReminder()
    .pipe(takeUntilDestroyed(this.destroyRef))
    .subscribe({
      next: (res) => {
        this.totalNotificationIsNotRead = res.data || 0;
      },
      error: () => {
        this.totalNotificationIsNotRead = 0;
      }
    });
  
}
//in đậm title hoặc tên
formatMessage(item: any): SafeHtml {
  let msg = item.message;
  
  if (item.task?.title) {
    msg = msg.replace(item.task.title, `<span style="font-weight: 600;font-family:"PoppInins"">${item.task.title}</span>`);
  }
  return this.sanitizer.bypassSecurityTrustHtml(msg);
}
//   ChangeIsRead(item: ReminderModel) {
  
// if (!item?.task.taskId) {
//     console.error("Reminder không có taskid:", item);
//     return;
//   }
//   this.route.navigate([`/viecduocgiao/chitiet/${item.task.taskId}`]);

  
// }

  
//     if (!item.isNotified) {
//       item.isNotified = true; // update UI trước
//       this.NotiService.maskReminderRead(item.reminderId)
//         .pipe(takeUntilDestroyed(this.destroyRef))
//         .subscribe({
//           next: () => this.refreshUnreadCount(),
//           error: () => {
//             item.isNotified = false; // rollback
//             this.toastService.Error('Đánh dấu đọc thất bại!');
//           }
//         });
//     }

//     // Điều hướng dựa vào type và taskId
//     if (item.type == typeNotification.taskReminder && item.taskid) {
//       // Nếu có taskId thì điều hướng đến task cụ thể
//         this.route.navigate(['/viecduocgiao'], {
//           queryParams: { highlightId: item.taskid, _t: Date.now() },
//         });
//       } else if (
//         item.type == typeNotification.createTask ||
//         item.type == typeNotification.putDeadlineTask ||
//         item.type == typeNotification.taskReminder
//       ) {
//         this.route.navigate(['/viecduocgiao'], {
//           queryParams: { highlightId: item.taskid, _t: Date.now() },
//         });
//       }
//     }
//     getNotificationDisplayText(item: ReminderModel): string {
//     if (item.taskid) {
//       return `${item.message} (Task #${item.taskid})`;
//     }
//     return item.message;
//   }

//     //kiểm tra thông báo realtime đã đọc chưa
//     markNotificationRead(item: NotificationItem) {
//     if (!item.isRead) {
//       item.isRead = true;
//       console.log('[Reminder] Marked as read:', item.title);
//     }
//   }
// >>>>>>> origin/main
  //click chuông đồng nghĩa thông báo được đọc
//   markAllNotificationRead() {
//   // Cập nhật UI local ngay
 

//   // Gọi API markRead cho từng thông báo DB
//   const reminderIds = this.listNotification
//     .filter(r => !r.isRead) // lọc chưa đọc
//     .map(r => r.reminderId);

//   reminderIds.forEach(id => {
//     this.NotiService.maskReminderRead(id)
//       .pipe(takeUntilDestroyed(this.destroyRef))
//       .subscribe({
//         next: () => {}, // thành công thì thôi
//         error: () => {
//           console.warn(`Đánh dấu thất bại cho reminderId=${id}`);
//         }
//       });
//   });

//   // Sau cùng sync lại count từ server (cho chắc)
//   this.refreshUnreadCount();
// }



  // loadData() {
  //   this.isLoading = true;
  //   this.data$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
  //     next: (list) => {
  //       if (list.length === 0) {
  //         this.isEmptlist = true;
  //       } else {
  //         this.totalNotificationIsNotRead = list.filter(
  //           (r) => !r.isNotified
  //         ).length;
  //         this.isEmptlist = false;
  //         this.listNotification = list;
  //       }
  //       this.isLoading = false;
  //     },
  //     error: (err) => {
  //       this.isLoading = false;
  //       this.toastService.Error(err.message || 'Lấy dữ liệu thất bại !');
  //     },
  //   });
  //   this.updateUnreadCount();
  // }
 
}