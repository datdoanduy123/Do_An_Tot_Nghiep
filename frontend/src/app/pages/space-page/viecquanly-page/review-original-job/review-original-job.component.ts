import {
  UnitProgressModel,
  UserProgressModel,
} from './../../../../models/review-job.model';
import { ReviewOriginalJobService } from './../../../../service/review-original-job.service';
import { ModalSendRemindComponent } from './../../../../components/modal-send-remind/modal-send-remind.component';
import { ToastService } from './../../../../service/toast.service';
import { CommonModule, Location } from '@angular/common';
import { Component, Input, ViewChild, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
// import { Task } from '../../../../models/task.model';
import { SelectMultiUnitComponent } from '../../../../components/select-multi-unit/select-multi-unit.component';
import { Observable } from 'rxjs';

import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzPopoverModule } from 'ng-zorro-antd/popover';
import { ModalViewFileComponent } from '../../../../components/modal-view-file/modal-view-file.component';
import { ActivatedRoute } from '@angular/router';
import { ConvertStatusTask } from '../../../../constants/constant';

@Component({
  selector: 'app-review-original-job',
  imports: [
    CommonModule,
    FormsModule,
    NzPopconfirmModule,
    NzSelectModule,
    NzPaginationModule,
    NzTableModule,
    SelectMultiUnitComponent,
    ModalSendRemindComponent,
    NzSwitchModule,
    NzPopoverModule,
    ModalViewFileComponent,
  ],
  templateUrl: './review-original-job.component.html',
  styleUrl: './review-original-job.component.css',
})
export class ReviewOriginalJobComponent implements OnInit {
  @ViewChild(ModalSendRemindComponent)
  ModalSendRemindRef!: ModalSendRemindComponent;
  @ViewChild(ModalViewFileComponent)
  ModalViewFileRef!: ModalViewFileComponent;

  // listGroupedByFrequency: GroupedByFrequency[] = [];
  // listGroupedByUnit: GroupedByUnit[] = [];
  selectedGroupTableBy = 'Kỳ';
  listGroupTableBy = ['Kỳ', 'Đơn vị'];
  repeatschedule = 1;
  isReviewSchedule = false;
  listSelectedUnit: string[] = [];
  objectKeys = Object.keys;
  // ----
  pageIndex = 1;
  pageSize = 10;
  total = 0;
  taskId!: number;
  // ----
  checked = false;
  indeterminate = false;
  // listOfCurrentPageData: readonly ReviewTable[] = [];
  // listOfData: ReviewTable[] = [];
  setOfCheckedId = new Set<number>();
  isShowTableDataGroupByUnit = false;
  isShowTableDataGroupByFrequency = false;
  isShowTableDataDefault = true;
  // -----
  isShowDataDefault = true;
  isShowDataByUnit = false;
  isloadingTable = true;
  listDataDefault: GroupedUserProgress[] = [];
  listDataByUnit: GroupedUnitProgress[] = [];
  listRemindUser: number[] = [];
  listRemindUnit: number[] = [];
  constructor(
    private toastService: ToastService,
    private route: ActivatedRoute,
    private locationRoute: Location,
    private reviewOriginalJobService: ReviewOriginalJobService
  ) {}

  ngOnInit(): void {
    this.route.params.subscribe((params) => {
      this.taskId = +params['id'];
    });
    this.loadDefaultData(this.taskId.toString());
    // this.listOfCurrentPageData = this.listOfData;
  }

  showModalFile(file: string): void {
    if (!file) return;

    const url = `assets/files/${file}`;
    this.ModalViewFileRef.fileUrl = this.getFilePath(file);
    this.ModalViewFileRef.fileName = file;
    this.ModalViewFileRef.showModal();
  }

  getFilePath(fileName: string): string {
    return `/assets/files/${fileName}`;
  }

  //--- table -----
  updateCheckedSet(id: number, checked: boolean): void {
    if (checked) {
      this.setOfCheckedId.add(id);
      if (this.isShowDataDefault && !this.listRemindUser.includes(id)) {
        this.listRemindUser.push(id);
      }
      if (this.isShowDataByUnit && !this.listRemindUnit.includes(id)) {
        this.listRemindUnit.push(id);
      }
    } else {
      this.setOfCheckedId.delete(id);
      if (this.isShowDataDefault) {
        this.listRemindUser = this.listRemindUser.filter((item) => item !== id);
      }
      if (this.isShowDataByUnit) {
        this.listRemindUnit = this.listRemindUnit.filter((item) => item !== id);
      }
    }
  }

  onPageChange(newPageIndex: number): void {
    this.pageIndex = newPageIndex;
    console.log('Trang hiện tại là:', this.pageIndex);
    // Tại đây bạn có thể gọi API hoặc làm các hành động khác theo trang mới
  }
  onItemChecked(id: number, checked: boolean): void {
    this.updateCheckedSet(id, checked);

    // this.refreshCheckedStatus();
  }
  onAllChecked(value: boolean): void {
    // this.listOfCurrentPageData.forEach((item) =>
    //   this.updateCheckedSet(item.id, value)
    // );
    // this.refreshCheckedStatus();
  }

  // onCurrentPageDataChange($event: readonly ReviewTable[]): void {
  //   this.listOfCurrentPageData = $event;
  //   this.refreshCheckedStatus();
  // }
  // --- select groupBy ---
  ChangeGroupTableBy(value: string) {
    this.selectedGroupTableBy = value;
  }

  // refreshCheckedStatus(): void {
  //   this.checked = this.listOfCurrentPageData.every((item) =>
  //     this.setOfCheckedId.has(item.id)
  //   );
  //   this.indeterminate =
  //     this.listOfCurrentPageData.some((item) =>
  //       this.setOfCheckedId.has(item.id)
  //     ) && !this.checked;
  // }

  // groupByUnit(data: ReviewTable[]): GroupedByUnit[] {
  //   const groupedObj = data.reduce((acc, item) => {
  //     const { unit } = item;
  //     if (!acc[unit]) {
  //       acc[unit] = [];
  //     }
  //     acc[unit].push(item);
  //     return acc;
  //   }, {} as Record<string, ReviewTable[]>);

  //   return Object.entries(groupedObj).map(([unit, items]) => ({
  //     unit,
  //     items,
  //   }));
  // }
  // groupByFrequency(data: ReviewTable[]): GroupedByFrequency[] {
  //   const groupedObj = data.reduce((acc, item) => {
  //     const { frequency } = item;
  //     if (!acc[frequency]) {
  //       acc[frequency] = [];
  //     }
  //     acc[frequency].push(item);
  //     return acc;
  //   }, {} as Record<string, ReviewTable[]>);

  //   return Object.entries(groupedObj).map(([frequency, items]) => ({
  //     frequency,
  //     items,
  //   }));
  // }
  // ----
  review() {
    if (this.listSelectedUnit.length > 0) {
      this.isShowDataDefault = false;
      this.isShowDataByUnit = true;

      this.loadDataByUnit(this.taskId.toString());
    } else {
      this.isShowDataByUnit = false;
      this.isShowDataDefault = true;

      this.loadDefaultData(this.taskId.toString());
    }
    // console.log(this.listSelectedUnit);
  }

  // ---- send remind modal ----
  showModalSendRemind() {
    this.ModalSendRemindRef.showModal(null);
  }

  // -----  create report ----
  cancel(): void {
    // this.nzMessageService.info('click cancel');
  }

  confirm(): void {
    this.toastService.Success('Tạo báo cáo thành công !');
    // this.nzMessageService.info('click confirm');
  }

  beforeConfirm(): Observable<boolean> {
    return new Observable((observer) => {
      setTimeout(() => {
        observer.next(true);
        observer.complete();
      }, 3000);
    });
  }
  //---- back route
  goBack() {
    this.locationRoute.back();
  }
  loadDefaultData(taskid: string) {
    this.reviewOriginalJobService.getReviewByUser(taskid).subscribe({
      next: (data) => {
        this.listDataDefault = data.map((user: any) => {
          const flattenedProgresses: FlatProgressRow[] =
            user.scheduledProgresses.flatMap((period: any) => {
              return period.progresses.map((prog: any) => ({
                periodIndex: period.periodIndex,
                periodStartDate: period.periodStartDate,
                periodEndDate: period.periodEndDate,
                // periodName: period.periodName,
                userName: user.userName,
                scheduledDate: `${this.convertDate(period.periodStartDate)} - ${this.convertDate(period.periodEndDate)}`,
                progressId: prog.progressId,
                status: this.getStatusLabel(prog.status),

                result: prog.result,
                suggest: prog.proposal,
                feedback: prog.feedback,
                file: prog.fileName,
              }));
            });

          return {
            userId: user.userId,
            userName: user.userName,
            progresses: flattenedProgresses,
          };
        });
        this.isloadingTable = false;
      },
      error: (err) => {
        this, this.toastService.Error(err.message ?? 'Lấy dữ liệu thất bại');
      },
    });
  }
  loadDataByUnit(taskid: string) {
    this.reviewOriginalJobService
      .getReviewByUnits(this.listSelectedUnit, taskid.toString())
      .subscribe({
        next: (data) => {
          this.listDataByUnit = data.map((user: any) => {
            const flattenedProgresses: FlatProgressRow[] =
              user.scheduledProgresses.flatMap((period: any) => {
                return period.progresses.map((prog: any) => ({
                  periodIndex: period.periodIndex,
                  
                  periodStartDate: period.periodStartDate,
                  periodEndDate: period.periodEndDate,
                  // periodName: period.periodName,
        

                  scheduledDate: `${this.convertDate(period.periodStartDate)} - ${this.convertDate(period.periodEndDate)}`,
                  progressId: prog.progressId,
                  status: this.getStatusLabel(prog.status),

                  result: prog.result,
                  suggest: prog.proposal,
                  feedback: prog.feedback,
                  file: prog.fileName,
                }));
              });

            return {
              unitId: user.userId,
              unitName: user.userName,
              progresses: flattenedProgresses,
            };
          });
          this.isloadingTable = false;
        },
        error: (err) => {
          this.toastService.Error(err.message ?? 'Lấy dữ liệu thất bại');
        },
      });
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

  getStatusLabel(statusKey: string): string {
    return (
      ConvertStatusTask[statusKey as keyof typeof ConvertStatusTask] ||
      'Chưa báo cáo'
    );
  }

  


}
interface GroupedUnitProgress {
  unitName: string;
  unitId: number;
  progresses: FlatProgressRow[];
}
interface GroupedUserProgress {
  userName: string;
  userId: number;
  progresses: FlatProgressRow[];

}

interface FlatProgressRow {
  userName: string;
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

}