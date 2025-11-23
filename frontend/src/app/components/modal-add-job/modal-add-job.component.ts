import { take } from 'rxjs/operators';
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
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { TaskViewModel } from '../../models/task-view.model';
import { NzInputModule } from 'ng-zorro-antd/input';
import { ToastService } from '../../service/toast.service';
import { ModalDateTimePicker } from '../modal-date-time-picker/modal-date-time-picker.component';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { convertDateArrayToISOStrings } from '../../helper/converetToISODate';
import { AssignWorkService } from '../../service/assign-work.service';
import { QuillModule } from 'ngx-quill';
import { ModalAssignPersonToJobComponent } from "../modal-assign-person-to-job/modal-assign-person-to-job.component";
import { AssignResult } from '../../models/assign-result.model';
import { ModalAssignScheduleComponent } from "../modal-assign-schedule/modal-assign-schedule.component";
import { FrequencyView } from '../../models/frequency-view.model';
import { getFrequencySelected } from '../../utils/frequency';
import { CheckOutline, CloseOutline } from '@ant-design/icons-angular/icons';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { quillToolbarConfig } from '../../configs/quill.config';
import { AiTaskStorageService } from '../../service/ai-task-storage.service';

@Component({
  selector: 'app-modal-add-job',
  imports: [
    NzModalModule,
    CommonModule,
    NzButtonModule,
    NzSwitchModule,
    FormsModule,
    NzSpinModule,
    NzSelectModule,
    NzInputModule,
    NzDatePickerModule,
    QuillModule,
    ModalAssignPersonToJobComponent,
    ModalAssignScheduleComponent,
    NzIconModule
],
  templateUrl: './modal-add-job.component.html',
  styleUrls: ['./modal-add-job.component.css'],
})
export class ModalAddJobComponent implements OnInit {
  
  isVisible = false;
  isConfirmLoading = false;

  isChoosePeriod=false;
  isAssign = false;
  jobName: string = '';
  description: string = '';
  
  isFromAi = false;
  
  selectedMisson: any;
  @Input() listTask: TaskViewModel[] = [];
  @Output() jobAdded = new EventEmitter<any>();
  @ViewChild(ModalDateTimePicker)
  modalDateTimePickerRef!: ModalDateTimePicker;
  task: TaskViewModel = {
    id: Date.now(),
    title: '',
    description: '',
    assigneeIds: [],
    unitIds: [],
    assigneeFullNames: [],
    startDate: null,
    endDate: null,
    frequencyType: null,
    intervalValue: null,
    daysOfWeek: [],
    daysOfMonth: [],
    parentTaskId: null,
  };
  constructor(
    private toastService: ToastService,
    private assignWorkService: AssignWorkService,
    private aiTaskStorageService: AiTaskStorageService,
  ) {}

  quillConfig = quillToolbarConfig;

  ngOnInit(): void {
    this.checkForAiData();
  }
  onChange(result: Date): void {
    const dates = result.toString().split(',');
    this.task.startDate = dates[0];
    this.task.endDate = dates[1];
  }
  showModal(): void {
    this.isVisible = true;
    this.resetForm();
    
    // Check for AI data when modal opens
    this.checkForAiData();
  }
   onFrequencySelect(data: FrequencyView) {
    this.task = getFrequencySelected(this.task, data);
  }
  handleOk(): void {
  this.isConfirmLoading = true;

  if (!this.jobName.trim()) {
    this.toastService.Warning('Vui lòng nhập tên công việc!');
    this.isConfirmLoading = false;
    return;
  }

  if (!this.description.trim()) {
    this.toastService.Warning('Vui lòng nhập nội dung công việc!');
    this.isConfirmLoading = false;
    return;
  }

  if (!this.task.startDate || !this.task.endDate) {
    this.toastService.Warning('Vui lòng nhập thời hạn!');
    this.isConfirmLoading = false;
    return;
  }

  if (this.isChoosePeriod) {
    if (!this.task.frequencyType) {
      this.toastService.Warning('Vui lòng chọn kỳ báo cáo');
      this.isConfirmLoading = false;
      return;
    }
    if (!this.task.assigneeIds || this.task.assigneeIds.length === 0) {
      this.toastService.Warning('Vui lòng phân công khi chọn kỳ báo cáo');
      this.isConfirmLoading = false;
      return;
    }
  }

  const taskPayload: any = {
    title: this.jobName.trim(),
    description: this.description.trim(),
    startDate: new Date(this.task.startDate as string).toISOString(),
    dueDate: new Date(this.task.endDate as string).toISOString(),
    frequency: this.task.frequencyType ?? null,
    intervalValue: this.task.intervalValue ?? null,
    days: this.task.daysOfWeek?.length
      ? this.task.daysOfWeek
      : this.task.daysOfMonth?.length
        ? this.task.daysOfMonth
        : null,
    assignedUsersIds: this.task.assigneeIds?.length
      ? this.task.assigneeIds
      : null,
    assignedUnitIds: this.task.unitIds?.length
      ? this.task.unitIds
      : null,
  };

  Object.keys(taskPayload).forEach(
    (key) => taskPayload[key] === null && delete taskPayload[key]
  );
  this.assignWorkService.CreateParentTask(taskPayload)
    .pipe(take(1))
    .subscribe({
      next: (data) => {
        const newTask: TaskViewModel = {
          ...this.task,
          id: data.taskId,
          title: data.title,
          description: data.description,
          startDate: data.startDate,
          endDate: data.dueDate,
          frequencyType: data.frequencyType,
        };
        this.jobAdded.emit(newTask);

        if(this.isFromAi){
          this.aiTaskStorageService.clearAiTaskData();
          this.isFromAi = false;
          
        }

        this.toastService.Success('Giao việc thành công!');
        this.isVisible = false;
        this.resetForm();
      },
      error: (err) => {
        console.error('Lỗi tạo task cha:', err);
        this.isConfirmLoading=false;
              },
      complete: () => {
        this.isConfirmLoading = false;
      },
    });
}



  handleCancel(): void {
    this.isVisible = false;
    this.resetForm();
    
    // Clear AI data if cancelling
    if (this.isFromAi) {
      this.aiTaskStorageService.clearAiTaskData();
      this.isFromAi = false;
    }
  }
  getAssigned(data: AssignResult) {
      // Reset trước
      this.task.assigneeIds = [];
      this.task.assigneeFullNames = [];
      this.task.unitIds = [];
      
      // Gán người dùng nếu có
      if (data.users.length > 0) {
        this.task.assigneeIds = data.users.map((u) => u.userId);
        this.task.assigneeFullNames = data.users.map((u) => u.fullName);
      }
  
      // Gán đơn vị nếu có
      if (data.units.length > 0) {
        this.task.unitIds = data.units.map((u) => u.unitId);
        // Gộp với tên user nếu cần, hoặc thay thế nếu bạn muốn
        this.task.assigneeFullNames = this.task.assigneeFullNames.concat(
          data.units.map((u) => u.unitName)
        );
      }
      console.log(" danh sách unit task cha  ",this.task.unitIds)
    }
  
     private checkForAiData(): void {
    const aiData = this.aiTaskStorageService.getAiTaskData();
    if (aiData) {
      this.isFromAi = true;
      // Dữ liệu sẽ được fill từ component cha khi modal mở
    }
  }

  private resetForm(): void {
    // Chỉ reset nếu không phải từ AI
    if (!this.isFromAi) {
      this.jobName = '';
      this.description = '';
      this.task = {
        id: Date.now(),
        title: '',
        description: '',
        assigneeIds: [],
        unitIds: [],
        assigneeFullNames: [],
        startDate: null,
        endDate: null,
        frequencyType: null,
        intervalValue: null,
        daysOfWeek: [],
        daysOfMonth: [],
        parentTaskId: null,
      };
    }
  }



}