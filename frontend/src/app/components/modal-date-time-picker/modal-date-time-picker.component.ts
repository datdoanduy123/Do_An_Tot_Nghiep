import { Component, EventEmitter, inject, Input, Output } from '@angular/core';
import {
  NgbCalendar,
  NgbDate,
  NgbDatepickerModule,
} from '@ng-bootstrap/ng-bootstrap';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { NzTimePickerModule } from 'ng-zorro-antd/time-picker';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzModalModule, NzModalRef, NzModalService } from 'ng-zorro-antd/modal';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
@Component({
  selector: 'modal-datetime-picker',
  imports: [
    NgbDatepickerModule,
    FormsModule,
    CommonModule,
    NzTimePickerModule,
    NzButtonModule,
    NzModalModule,
    NzDatePickerModule,
  ],
  templateUrl: './modal-date-time-picker.component.html',
  styleUrls: ['./modal-date-time-picker.component.css'],
})
export class ModalDateTimePicker {
  @Output() dataDateTimePicker = new EventEmitter<any>();
  confirmCloseModal?: NzModalRef;

  isVisible = false;
  startTime: Date | null = new Date();
  endTime: Date | null = null;
  date = null;
  rangeDates: Date[] = [];

  constructor(private modal: NzModalService) {}
  showModal(): void {
    this.isVisible = true;
  }
  closeModal(): void {
    this.isVisible = false;
  }
  onChange(result: Date[]): void {
    this.rangeDates.push(result[0]);
    this.rangeDates.push(result[1]);
    const mergedStart = this.mergeTimeIntoDate(
      this.rangeDates[0],
      this.startTime!
    );
    const mergedEnd = this.mergeTimeIntoDate(this.rangeDates[1], this.endTime!);
    console.log(mergedStart);
    console.log(mergedEnd);
  }

  //---
  showConfirm(): void {
    this.confirmCloseModal = this.modal.confirm({
      nzTitle: 'Bạn có chắc chắn muốn hủy chọn lịch trình ?',
      nzContent: 'Dữ liệu bạn đã chọn sẽ mất !',
      nzOnOk: () =>
        new Promise((resolve, reject) => {
          setTimeout(Math.random() > 0.5 ? resolve : reject, 1000);
        }).catch(() => console.log('')),
    });
  }

  SubmitSelect() {
    const data: Object = {
      startDateTime: this.rangeDates[0],
      startEndTime: this.rangeDates[1],
    };
    // // console.log(data);
    this.dataDateTimePicker.emit(data);
    this.closeModal();
  }

  convertDateArrayToISOStrings(dates: Date[]): string[] {
    return dates.map((date) => date.toISOString());
  }
  mergeTimeIntoDate(date: Date, time: Date): Date {
    const result = new Date(date);
    result.setHours(time.getHours());
    result.setMinutes(time.getMinutes());
    result.setSeconds(time.getSeconds());
    result.setMilliseconds(time.getMilliseconds());
    return result;
  }
}
