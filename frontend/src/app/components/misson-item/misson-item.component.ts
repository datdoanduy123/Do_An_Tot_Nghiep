import { FrequencyView } from './../../models/frequency-view.model';
import { ListAssignWorkService } from './../../service/list-assign-work.service';
import { TaskViewModel } from './../../models/task-view.model';
import { CommonModule } from '@angular/common';
import {
  Component,
  EventEmitter,
  Input,
  OnInit,
  Output,
  ViewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzModalService } from 'ng-zorro-antd/modal';
import { convertToVietnameseDate } from '../../helper/convertToVNDate';
import { NzPopoverModule } from 'ng-zorro-antd/popover';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { SelectRepeatScheduleComponent } from '../select-repeat-schedule/select-repeat-schedule.component';
import { FrequencyTypeMap } from '../../constants/constant';
import { ModalDetailDespcriptionJobComponent } from '../modal-detail-despcription-job/modal-detail-despcription-job.component';

@Component({
  selector: 'app-misson-item',
  imports: [
    CommonModule,
    FormsModule,
    NzPopoverModule,
    SelectRepeatScheduleComponent,
    NzDropDownModule,
    NzButtonModule,
    NzInputModule,
    ModalDetailDespcriptionJobComponent,
  ],
  templateUrl: './misson-item.component.html',
  styleUrl: './misson-item.component.css',
})
export class MissonItemComponent implements OnInit {
  @ViewChild(ModalDetailDespcriptionJobComponent)
  ModalDetailDespcriptionJobRef!: ModalDetailDespcriptionJobComponent;
  @Input() task!: TaskViewModel;
  @Output() removetask = new EventEmitter<number>();
  frequencyView: FrequencyView = {
    frequency_type: '',
    interval_value: 0,
    daysInWeek: [],
    daysInMonth: [],
  };
  constructor(
    private modal: NzModalService,
    private listAssignWorkService: ListAssignWorkService
  ) {}
  
  ngOnInit(): void {
    this.frequencyView.frequency_type =
      FrequencyTypeMap[
        (this.task.frequencyType as 'daily' | 'weekly' | 'monthly') ?? ''
      ];
    this.frequencyView.interval_value = this.task.intervalValue ?? 0;
    this.frequencyView.daysInMonth = this.task.daysOfMonth ?? [];
    this.frequencyView.daysInWeek = this.task.daysOfWeek ?? [];
    
    console.log("dnah sách nhânh",this.task)
  }

  // ------
  showConfirm(id: number): void {
    this.modal.confirm({
      nzTitle: '<i>Bạn chắc chắn muốn xóa ?</i>',
      // nzContent: '<b>Some descriptions</b>',
      nzOnOk: () => this.removetask.emit(id),

      nzOkText: 'Xác nhận',
      nzCancelText: 'Hủy bỏ',
    });
  }
  showModalDesp() {
    this.ModalDetailDespcriptionJobRef.showModal();
  }
  convertDateTimeVn(datetime: string): string {
    return convertToVietnameseDate(datetime);
  }

  

}
