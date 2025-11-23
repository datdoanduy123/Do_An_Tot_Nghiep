// src/app/pages/space-page/viecquanly-page/detail-viecquanly/review-child-job/review-child-job.component.ts

import { Status } from '../../../../../constants/constant';
import { CommonModule, Location } from '@angular/common';
import {
  Component,
  DestroyRef,
  inject,
  OnInit,
  ViewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzPopoverModule } from 'ng-zorro-antd/popover';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { ToastService } from '../../../../../service/toast.service';
import { Observable } from 'rxjs';
import { ModalSendRemindComponent } from '../../../../../components/modal-send-remind/modal-send-remind.component';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { ReviewChildJobService } from '../../../../../service/review-child-job.service';
import { ActivatedRoute } from '@angular/router';
import { ConvertStatusTask } from '../../../../../constants/constant';
import { convertToVietnameseDate } from '../../../../../helper/convertToVNDate';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { UserModel } from '../../../../../models/user.model';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { ModalReviewProgressComponent } from '../../../../../components/modal-review-progress/modal-review-progress.component';
import { ResponsePaganation } from '../../../../../interface/response-paganation';
import { UserProgressModel, UnitProgressModel } from '../../../../../models/review-job.model';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AiSummaryService } from '../../../../../service/ai-summary.service';

@Component({
  selector: 'app-review-child-job',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzSwitchModule,
    NzPopoverModule,
    NzTableModule,
    ModalSendRemindComponent,
    NzPopconfirmModule,
    NzDatePickerModule,
    NzSelectModule,
    NzButtonModule,
    NzToolTipModule,
    NzIconModule,
    ModalReviewProgressComponent,
  ],
  templateUrl: './review-child-job.component.html',
  styleUrl: './review-child-job.component.css',
})
export class ReviewChildJobComponent implements OnInit {
  @ViewChild(ModalSendRemindComponent)
  ModalSendRemindRef!: ModalSendRemindComponent;
  @ViewChild(ModalReviewProgressComponent)
  ModalReviewProgressRef!: ModalReviewProgressComponent;

  // ===== 👈 FIXED: Properties =====
  
  // Basic properties
  pageSize = 10;
  totalItem = 0;
  isloadingTable = true;
  taskId: string = ''; 
  
  // Task assignment detection
  assignedUsers: UserModel[] = []; 
  assignedUnits: UnitProgressModel[] = []; 
  isUnitTask = false; 
  viewMode: 'user' | 'unit' = 'user'; 
  
  // Progress data
  userProgressList: UserProgressModel[] = []; 
  unitProgressList: UnitProgressModel[] = []; 
  totalPages = 0; 
  currentPage = 1; 

  // User/Unit selection
  showUserProgress = true;
  ListselectedUnit: UnitProgressModel[] = [];
  ListselectUnit: UnitProgressModel[] = [];
  ListselectedUser: UserModel[] = [];
  ListselectUser: UserModel[] = [];
  isLoadingUnits = false;
  isLoadingUsers = false;
  
  // Table data
  checked = false;
  indeterminate = false;
  listOfData: GroupedUserProgress[] = [];
  listOfUnitData: GroupedUnitProgress[] = [];
  setOfCheckedId = new Set<number>();
  isShowTableDataGroupByFrequency = false;
  isShowTableDataDefault = true;
  listOfCurrentPageData: GroupedUserProgress[] = [];
  listOfCurrentUnitPageData: GroupedUnitProgress[] = [];
  listRemindUser: number[] = [];
  listRemindUnit: number[] = [];
  currentPageData: GroupedUserProgress[] = [];
  
  // Data streams
  data$!: Observable<ResponsePaganation<UserProgressModel>>;
  unitData$!: Observable<ResponsePaganation<UnitProgressModel>>;
  
  // Other
  isGeneratingSummary = false;
  startDate = new Date();
  endDate = new Date();
  dateRange: [Date | null, Date | null] = [null, null];
  
  private destroyRef = inject(DestroyRef);

  constructor(
    private route: ActivatedRoute,
    private toastService: ToastService,
    private locationRoute: Location,
    private reviewChildJobService: ReviewChildJobService,
    private aiSummaryService: AiSummaryService
  ) {}

  
  ngOnInit(): void {
    const taskIdParam = this.route.snapshot.paramMap.get('id');
    if (!taskIdParam) {
      this.toastService.Error('Không tìm thấy ID công việc!');
      return;
    }
    
    this.taskId = taskIdParam;
    this.loadTaskInfo();
    this.determineTaskTypeAndLoadData();
  }

  
  loadTaskInfo(): void {

    console.log('Loading task info for ID:', this.taskId);
  }
  
  private determineTaskTypeAndLoadData(): void {
    this.reviewChildJobService.getAssignedUsers(this.taskId).subscribe({
      next: (users) => {
        if (users && users.length > 0) {
          // ✅ Task giao cho user
          this.assignedUsers = users;
          this.isUnitTask = false;
          this.viewMode = 'user';
          this.initializeDataStreams();
          this.loadData();
        } else {
          this.loadUnitsAsFallback();
        }
      },
      error: (err) => {
        console.error('Load assigned users failed', err);

        this.loadUnitsAsFallback();
      },
    });
  }

  private loadUnitsAsFallback(): void {
    this.reviewChildJobService.getAssignedUnits(this.taskId).subscribe({
      next: (units) => {
        if (units && units.length > 0) {
          this.assignedUnits = units;
          this.isUnitTask = true;
          this.viewMode = 'unit';
          this.initializeDataStreams();
          this.loadData();
        } else {
          this.toastService.Warning('Không tìm thấy người hoặc đơn vị được giao việc');
        }
      },
      error: (unitErr) => {
        console.error('Load assigned units failed', unitErr);
        this.toastService.Error('Không thể tải thông tin người/đơn vị được giao việc');
      },
    });
  }

  // ===== Data Streams =====
  
  initializeDataStreams(): void {
    // User data stream
    this.data$ = this.reviewChildJobService.onRefresh(
      this.taskId,
      this.startDate.toISOString(),
      this.endDate.toISOString(),
      [],
      '1'
    );

    // Unit data stream
    this.unitData$ = this.reviewChildJobService.onRefreshUnits(
      this.taskId,
      this.startDate.toISOString(),  
      this.endDate.toISOString(),
      [],
      '1'
    );
  }

  // ===== Load Assigned Users/Units =====
  
  loadAssignedUsers(): void {
    if (!this.taskId) return;

    this.isLoadingUsers = true;
    this.reviewChildJobService.getAssignedUsers(this.taskId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (users) => {
          this.ListselectUser = users;
          this.isLoadingUsers = false;
        },
        error: (err) => {
          this.toastService.Warning(
            err.message ?? 'Lấy danh sách người được giao thất bại!'
          );
          this.isLoadingUsers = false;
          this.ListselectUser = [];
        },
      });
  }

  loadAssignedUnits(): void {
    if (!this.taskId) return;

    this.isLoadingUnits = true;
    this.reviewChildJobService.getAssignedUnits(this.taskId).subscribe({
      next: (units) => {
        if (units && units.length > 0) {
          this.assignedUnits = units;
          this.isUnitTask = true;
          this.viewMode = 'unit';
          this.loadUnitProgressData();
        } else {
          console.warn('No assigned units found for task:', this.taskId);
          this.toastService.Warning('Không tìm thấy đơn vị được giao việc');
        }
        this.isLoadingUnits = false;
      },
      error: (error) => {
        console.error('Error loading assigned units:', error);
        this.toastService.Error('Không thể tải thông tin đơn vị được giao việc');
        this.isLoadingUnits = false;
      }
    });
  }

  private loadUnitProgressData(): void {
    const unitIds = this.assignedUnits.map((u: UnitProgressModel) => u.unitId.toString());
    
    this.reviewChildJobService.onRefreshUnits(
      this.taskId,
      this.startDate.toISOString(),
      this.endDate.toISOString(),
      unitIds,
      '1'
    ).subscribe({
      next: (response) => {
        this.unitProgressList = response.items;
        this.totalItem = response.totalItems; 
        this.totalPages = response.totalPages;
        this.currentPage = response.currentPage;
      },
      error: (error) => {
        console.error('Error loading unit progress:', error);
        this.toastService.Error('Không thể tải dữ liệu tiến độ đơn vị');
      }
    });
  }

  private loadUserProgressData(): void {
    const userIds = this.assignedUsers.map((u: UserModel) => u.userId.toString());
    
    this.reviewChildJobService.onRefresh(
      this.taskId,
      this.startDate.toISOString(),
      this.endDate.toISOString(),
      userIds,
      '1'
    ).subscribe({
      next: (response) => {
        this.userProgressList = response.items;
        this.totalItem = response.totalItems; 
        this.totalPages = response.totalPages;
        this.currentPage = response.currentPage;
      },
      error: (error) => {
        console.error('Error loading user progress:', error);
        this.toastService.Error('Không thể tải dữ liệu tiến độ người dùng');
      }
    });
  }

  // ===== View Switching =====
  
  switchToUserView(): void {
    this.showUserProgress = true;
    this.ListselectedUnit = [];
    this.review();
  }

  switchToUnitView(): void {
    this.showUserProgress = false;
    this.ListselectedUser = [];
    this.review();
  }

  // ===== Review Method =====
  
  review(): void {
    if (this.showUserProgress) {
      // Review users
      this.data$ = this.reviewChildJobService.onRefresh(
        this.taskId,
        this.startDate?.toISOString() ?? '',
        this.endDate?.toISOString() ?? '',
        this.ListselectedUser.map((user) => user.userId.toString()),
        '1'
      );
    } else {
      // Review units
      this.unitData$ = this.reviewChildJobService.onRefreshUnits(
        this.taskId,
        this.startDate?.toISOString() ?? '',
        this.endDate?.toISOString() ?? '',
        this.ListselectedUnit.map((unit) => unit.unitId.toString()),
        '1'
      );
    }
    this.loadData();
  }

  // ===== Load Data Method =====
  
  loadData(): void {
    this.isloadingTable = true;
    
    if (this.showUserProgress) {
      this.loadUserData();
    } else {
      this.loadUnitData();
    }
  }

  
private loadUserData(): void {
    this.data$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (data) => {
        this.pageSize = data.pageSize;
        this.totalItem = data.totalItems;
        this.listOfData = data.items.map((user: any) => {
          const flattenedProgresses: FlatProgressRow[] =
            user.scheduledProgresses.flatMap((period: any) => {
              return period.progresses.map((prog: any) => ({
                periodIndex: period.periodIndex,
                periodStartDate: period.periodStartDate,
                periodEndDate: period.periodEndDate,
                scheduledDate: `${this.convertDate(period.periodStartDate)} - ${this.convertDate(period.periodEndDate)}`,
                progressId: prog.progressId,
                status: prog.status,
                result: prog.result,
                suggest: prog.proposal,
                feedback: prog.feedback,
                file: prog.fileName,
                filePath: prog.filePath,
              }));
            });

          return {
            userId: user.userId,
            userName: user.userName,
            progresses: flattenedProgresses,
            status: user.status,
          };
        });
        this.listOfCurrentPageData = [...this.listOfData];
        this.isloadingTable = false;
      },
      error: (err) => {
        this.isloadingTable = false;
        this.toastService.Warning(err.message ?? 'Lấy dữ liệu thất bại');
      },
    });
  }

  private loadUnitData(): void {
    this.unitData$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (data) => {
        this.pageSize = data.pageSize;
        this.totalItem = data.totalItems;
        this.listOfUnitData = data.items.map((unit: any) => {
          const flattenedProgresses: FlatProgressRow[] =
            unit.scheduledProgresses.flatMap((period: any) => {
              return period.progresses.map((prog: any) => ({
                periodIndex: period.periodIndex,
                periodStartDate: period.periodStartDate,
                periodEndDate: period.periodEndDate,
                scheduledDate: `${this.convertDate(period.periodStartDate)} - ${this.convertDate(period.periodEndDate)}`,
                progressId: prog.progressId,
                status: prog.status,
                result: prog.result,
                suggest: prog.proposal,
                feedback: prog.feedback,
                file: prog.fileName,
                filePath: prog.filePath,
              }));
            });

          return {
            unitId: unit.unitId,
            unitName: unit.unitName,
            leaderFullName: unit.leaderFullName,
            userId: unit.userId,
            progresses: flattenedProgresses,
          };
        });
        this.listOfCurrentUnitPageData = [...this.listOfUnitData];
        this.isloadingTable = false;
      },
      error: (err) => {
        this.isloadingTable = false;
        this.toastService.Warning(err.message ?? 'Lấy dữ liệu đơn vị thất bại');
      },
    });
  }

  
  refreshData(): void {
    this.reviewChildJobService.triggerRefresh();
  }

  // ===== Item Selection Methods =====

  onItemChecked(id: number, checked: boolean): void {
    if (this.showUserProgress) {
      this.updateCheckedSet(id, checked);
    } else {
      this.updateUnitCheckedSet(id, checked);
    }
    this.refreshCheckedStatus();
  }

  updateCheckedSet(id: number, checked: boolean): void {
    if (checked) {
      this.setOfCheckedId.add(id);
      this.listRemindUser.push(id);
    } else {
      this.setOfCheckedId.delete(id);
      this.listRemindUser = this.listRemindUser.filter((item) => item !== id);
    }
  }
  updateUnitCheckedSet(unitId: number, checked: boolean): void {
    if (checked) {
      this.setOfCheckedId.add(unitId);
      this.listRemindUnit.push(unitId);
    } else {
      this.setOfCheckedId.delete(unitId);
      this.listRemindUnit = this.listRemindUnit.filter((item) => item !== unitId);
    }
  }

  // ===== Pagination =====

  onPageChange(newPageIndex: number): void {
    if (this.showUserProgress) {
      this.data$ = this.reviewChildJobService.onRefresh(
        this.taskId,
        this.startDate.toISOString(),
        this.endDate.toISOString(),
        this.ListselectedUser.map((user) => user.userId.toString()),
        newPageIndex.toString()
      );
    } else {
      this.unitData$ = this.reviewChildJobService.onRefreshUnits(
        this.taskId,
        this.startDate.toISOString(),
        this.endDate.toISOString(),
        this.ListselectedUnit.map((unit) => unit.unitId.toString()),
        newPageIndex.toString()
      );
    }
    this.loadData();
  }

  onAllChecked(value: boolean): void {
    if (this.showUserProgress) {
      this.listOfCurrentPageData.forEach((item) =>
        this.updateCheckedSet(item.userId, value)
      );
    } else {
      this.listOfCurrentUnitPageData.forEach((item) =>
        this.updateUnitCheckedSet(item.unitId, value)
      );
    }
    this.refreshCheckedStatus();
  }

  onCurrentPageDataChange(data: readonly GroupedUserProgress[]): void {
    this.currentPageData = [...data];
  }

  onCurrentUnitPageDataChange(data: readonly GroupedUnitProgress[]): void {
    this.listOfCurrentUnitPageData = [...data];
  }

  refreshCheckedStatus(): void {
    if (this.showUserProgress) {
      this.checked = this.listOfCurrentPageData.every((item) =>
        this.setOfCheckedId.has(item.userId)
      );
      this.indeterminate =
        this.listOfCurrentPageData.some((item) =>
          this.setOfCheckedId.has(item.userId)
        ) && !this.checked;
    } else {
      this.checked = this.listOfCurrentUnitPageData.every((item) =>
        this.setOfCheckedId.has(item.unitId)
      );
      this.indeterminate =
        this.listOfCurrentUnitPageData.some((item) =>
          this.setOfCheckedId.has(item.unitId)
        ) && !this.checked;
    }
  }

  // ===== Send Remind Method =====

  showModalSendRemind(userId: string, isUnit: boolean = false, unitId?: number): void {
  if (isUnit && unitId) {
    this.ModalSendRemindRef.showModal(userId, Number(this.taskId));
  } else {
    this.ModalSendRemindRef.showModal(userId, Number(this.taskId));
  }
}


  // ===== Other Methods =====
  
  cancel(): void {}
  
  confirm(): void {}
  
  beforeConfirm(): Observable<boolean> {
    return new Observable((observer) => {
      setTimeout(() => {
        observer.next(true);
        observer.complete();
      }, 3000);
    });
  }

  goBack(): void {
    this.locationRoute.back();
  }

  convertDate(dateString: string): string {
    if (!dateString || dateString === '0001-01-01T00:00:00') return 'Chưa xác định';
    
    const date = new Date(dateString);
    return date.toLocaleDateString('vi-VN', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric'
    });
  }

  getLinkFileReport(filepath: string): void {
    this.reviewChildJobService.getFileReport(filepath).subscribe({
      next: (link) => {
        window.open(link, '_blank');
      },
      error: () => {
        this.toastService.Warning('Không lấy được liên kết tải về');
      },
    });
  }

  goReviewFileReport(filepath: string): void {
    this.reviewChildJobService.getFileReport(filepath).subscribe({
      next: (link) => {
        const linkPreview =
          'https://docs.google.com/gview?url=' + link + '&embedded=true';
        window.open(linkPreview, '_blank');
      }, 
      error: () => {
        this.toastService.Warning('Không lấy được liên kết tải về');
      },
    });
  }

  apporveProgress(progressId: string, accpect: boolean): void {
    this.ModalReviewProgressRef.showModal(progressId, accpect);
  }

  getStatusLabel(statusKey: string): string {
    return (
      ConvertStatusTask[statusKey as keyof typeof ConvertStatusTask] ||
      'Chưa báo cáo'
    );
  }

  convertStatus(status: string): string {
    switch (status) {
      case 'in_progress':
        return 'Đang thực hiện';
      case 'completed':
        return 'Hoàn thành';
      case 'pending':
        return 'Chờ phê duyệt';
      case 'submitted':
        return 'Chờ phê duyệt';
      case 'approved':
        return 'Đã phê duyệt';
      case 'rejected':
        return 'Đã từ chối';
      case 'Chưa có báo cáo cho mốc này':
        return 'Chưa có báo cáo';
      default:
        return status;
    }
  }

  isReportPending(status: string): boolean {
    return status === 'in_progress' || status === 'submitted' || status === 'pending';
  }

  isNoReport(status: string): boolean {
    return status === 'Chưa có báo cáo cho mốc này';
  }

  isCompleted(status: string): boolean {
    return status === 'completed' || status === 'approved';
  }

  accept(progressId: number): void {
    this.reviewChildJobService.acceptProgress(progressId.toString()).subscribe({
      next: () => {
        this.toastService.Success('Chấp nhận báo cáo thành công!');
        this.refreshData();
      },
      error: (err) => {
        this.toastService.Warning(err?.message || 'Lỗi phê duyệt báo cáo!');
      },
    });
  }

  showInfo(): void {
    if (!this.taskId) {
      this.toastService.Warning('Không tìm thấy ID công việc!');
      return;
    }

    this.isGeneratingSummary = true;
    
    this.aiSummaryService.downloadSummaryFile(this.taskId).subscribe({
      next: () => {
        this.toastService.Success('Tải báo cáo tổng hợp thành công!');
        this.isGeneratingSummary = false;
      },
      error: (err) => {
        console.error('Lỗi tổng hợp báo cáo:', err);
        this.toastService.Error(
          err?.message || 'Không thể tạo báo cáo tổng hợp. Vui lòng thử lại!'
        );
        this.isGeneratingSummary = false;
      }
    });
  }
}

// ===== Interface Definitions =====

interface GroupedUserProgress {
  userName: string;
  userId: number;
  progresses: FlatProgressRow[];
  status: string;
}

interface GroupedUnitProgress {
  unitId: number;
  unitName: string;
  leaderFullName: string;
  userId: number; // Leader's userId
  progresses: FlatProgressRow[];
}

interface FlatProgressRow {
  userName?: string;
  periodIndex: number;
  periodStartDate: string;
  periodEndDate: string; 
  scheduledDate: string;
  status: string;
  progressId: number;
  result: string | null;
  suggest: string | null;
  feedback: string | null;
  file: string | null;
  filePath: string | null;
}
