import { Component, Input, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { ReviewChildJobService } from '../../service/review-child-job.service';
import { ToastService } from '../../service/toast.service';

@Component({
  selector: 'app-progress-schedule',
  standalone: true,
  imports: [
    CommonModule,
    NzTableModule,
    NzTagModule,
    NzButtonModule,
    NzIconModule,
    NzToolTipModule
  ],
  templateUrl: './progress-schedule.component.html',
  styleUrl: './progress-schedule.component.css'
})
export class ProgressScheduleComponent implements  OnChanges {
  @Input() taskId!: number;
  
  progressPeriods: ProgressPeriod[] = [];
  isLoading = false;
  userName = '';

  constructor(
    private reviewService: ReviewChildJobService,
    private toastService: ToastService
  ) {}

  // ngOnInit() {
  //   if (this.taskId) {
  //     this.loadProgressData();
  //   }
  // }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['taskId'] && this.taskId) {
      this.loadProgressData();
    }
  }

  loadProgressData() {
    this.isLoading = true;
    // Sử dụng API mới để lấy dữ liệu tiến độ
    this.reviewService.getReviewByUser(this.taskId.toString(), '', '', [], '1').subscribe({
      next: (data) => {
        if (data.items && data.items.length > 0) {
          const userData = data.items[0]; // Lấy dữ liệu người dùng đầu tiên
          this.userName = userData.userName;
          
          this.progressPeriods = userData.scheduledProgresses.map((period: any) => {
            const progress = period.progresses[0]; // Lấy progress đầu tiên của mỗi kỳ
            return {
              periodIndex: period.periodIndex,
              periodStartDate: period.periodStartDate,
              periodEndDate: period.periodEndDate,
              status: progress.status,
              progressId: progress.progressId,
              proposal: progress.proposal,
              result: progress.result,
              feedback: progress.feedback,
              fileName: progress.fileName,
              filePath: progress.filePath,
              updatedAt: progress.updatedAt,
              updateByName: progress.updateByName
            };
          });
        }
        this.isLoading = false;
      },
      error: (err) => {
        this.toastService.Warning(err.message ?? 'Lấy dữ liệu tiến độ thất bại');
        this.isLoading = false;
      }
    });
  }

  getStatusColor(status: string): string {
    switch (status) {
      case 'completed':
        return 'green';
      case 'in_progress':
        return 'blue';
      case 'pending':
        return 'orange';
      case 'Chưa có báo cáo cho mốc này':
        return 'red';
      default:
        return 'default';
    }
  }

  getStatusText(status: string): string {
    switch (status) {
      case 'completed':
        return 'Đã hoàn thành';
      case 'in_progress':
        return 'Đang thực hiện';
      case 'pending':
        return 'Chờ xử lý';
      case 'Chưa có báo cáo cho mốc này':
        return 'Chưa nộp';
      default:
        return status;
    }
  }

  formatDate(dateString: string): string {
    if (!dateString || dateString === '0001-01-01T00:00:00') {
      return 'Chưa xác định';
    }
    const date = new Date(dateString);
    return date.toLocaleDateString('vi-VN', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric'
    });
  }

  downloadFile(filePath: string, fileName: string) {
    if (filePath && fileName) {
      window.open(filePath, '_blank');
    }
  }

  hasReport(period: ProgressPeriod): boolean {
    return period.status !== 'Chưa có báo cáo cho mốc này' && period.progressId > 0;
  }
}

interface ProgressPeriod {
  periodIndex: number;
  periodStartDate: string;
  periodEndDate: string;
  status: string;
  progressId: number;
  proposal: string | null;
  result: string | null;
  feedback: string | null;
  fileName: string | null;
  filePath: string | null;
  updatedAt: string | null;
  updateByName: string | null;
}