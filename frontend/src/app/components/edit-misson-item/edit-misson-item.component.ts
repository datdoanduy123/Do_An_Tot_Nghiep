import { AssignWorkService } from './../../service/assign-work.service';
import { ToastService } from './../../service/toast.service';
import { ListAssignWorkService } from '../../service/list-assign-work.service';
import { TaskViewModel } from '../../models/task-view.model';
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
import { ModalAssignScheduleComponent } from '../modal-assign-schedule/modal-assign-schedule.component';
import { ModalDateTimePicker } from '../modal-date-time-picker/modal-date-time-picker.component';
import { ModalAssignPersonToJobComponent } from '../modal-assign-person-to-job/modal-assign-person-to-job.component';
import { NzModalService } from 'ng-zorro-antd/modal';
import { FrequencyView } from '../../models/frequency-view.model';
import { AssignResult } from '../../models/assign-result.model';
import { convertToVietnameseDate } from '../../helper/convertToVNDate';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { convertDateArrayToISOStrings } from '../../helper/converetToISODate';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzPopoverModule } from 'ng-zorro-antd/popover';
import { QuillModule } from 'ngx-quill';
import { AiTaskStorageService, AiTaskData } from '../../service/ai-task-storage.service';
import { getFrequencySelected } from '../../utils/frequency';

@Component({
  selector: 'edit-app-misson-item',
  imports: [
    CommonModule,
    FormsModule,
    ModalAssignScheduleComponent,
    ModalAssignPersonToJobComponent,
    NzButtonModule,
    NzDatePickerModule,
    NzInputModule,
    NzPopconfirmModule,
    NzPopoverModule,
    QuillModule
  ],
  templateUrl: './edit-misson-item.component.html',
  styleUrl: './edit-misson-item.component.css',
})
export class EditMissonItemComponent implements OnInit {
  @Input() taskIdParent!: number;
  @Input() prefilledTask?: TaskViewModel;
  @Input() isAiMode = false; 
  @Input() isParentTask = false;
 
  @Output() taskAssigned = new EventEmitter<TaskViewModel>();  
  @Output() removetask = new EventEmitter<number>();
  @Output() refreshData = new EventEmitter<void>();
  @Output() closeEdit = new EventEmitter<any>();
  @Output() childCreated = new EventEmitter<TaskViewModel>();

  @ViewChild(ModalDateTimePicker)

  modalDateTimePickerRef!: ModalDateTimePicker;
 
  visiblePopoverConfirmGiaoViec = false;
  visiblePopoverConfirmHuyGiaoViec = false;
  jobName: string = '';

  isFromAi = false;
  aiTaskData: AiTaskData | null = null;

  task: TaskViewModel = {
    id: Date.now(),
    title: '',
    description: '',
    assigneeIds: [],
    assigneeFullNames: [],
    unitIds: [],
    startDate: null,
    endDate: null,
    frequencyType: null,
    intervalValue: null,
    daysOfWeek: [],
    daysOfMonth: [],
    parentTaskId: null,
  };
  constructor(
    private modal: NzModalService,
    private listAssignWorkService: ListAssignWorkService,
    private toastService: ToastService,
    private assignWorkService: AssignWorkService,
    private aiTaskStorageService: AiTaskStorageService
  ) {}

  quillConfig = {
    toolbar: [
      ['bold', 'italic', 'underline', 'strike'],       
      ['blockquote', 'code-block'],
      // [{ 'header': 1 }, { 'header': 2 }],              
      [{ 'list': 'ordered'}, { 'list': 'bullet' }],
      [{ 'script': 'sub'}, { 'script': 'super' }],  
      [{ 'indent': '-1'}, { 'indent': '+1' }],         
      [{ 'direction': 'rtl' }],                         
      [{ 'size': ['small', false, 'large', 'huge'] }],  
      [{ 'header': [1, 2, 3, 4, 5, 6, false] }],
      [{ 'color': [] }, { 'background': [] }],       
      [{ 'font': [] }],
      [{ 'align': [] }],
      ['clean'],                                        
      ['link', 'image', 'video']                     
    ]
  };


  ngOnInit(): void {
    this.loadAiDataIfAvailable();

    if (this.prefilledTask && this.isAiMode) {
      this.task = { ...this.prefilledTask };
      this.jobName = this.task.title;
    }
  }

  private loadAiDataIfAvailable(): void {
    this.aiTaskData = this.aiTaskStorageService.getAiTaskData();
    
    if (this.aiTaskData) {
      this.isFromAi = true;
      
      // Pre-fill form với dữ liệu AI
      this.jobName = this.aiTaskData.title;
      this.task.title = this.aiTaskData.title;
      this.task.description = this.aiTaskData.description;
      this.task.startDate = this.aiTaskData.startDate;
      this.task.endDate = this.aiTaskData.endDate;
      
      console.log('Đã load dữ liệu AI vào form:', this.aiTaskData);
      
      // Hiển thị thông báo cho user biết đây là dữ liệu từ AI
      this.toastService.Success(`Đã điền sẵn dữ liệu từ AI: ${this.aiTaskData.title}`);
    }
  }


  onChange(result: Date): void {
    const dates = result.toString().split(',');
    this.task.startDate = dates[0];
    this.task.endDate = dates[1];
  }

  getAssigned(data: AssignResult) {
    // Reset trước
    this.task.assigneeIds = [];
    this.task.assigneeFullNames = [];
    this.task.unitIds = [];
    
    if (data.users.length > 0) {
      this.task.assigneeIds = data.users.map((u) => u.userId);
      this.task.assigneeFullNames = data.users.map((u) => u.fullName);
    }

    if (data.units.length > 0) {
      this.task.unitIds = data.units.map((u) => u.unitId);
      this.task.assigneeFullNames = this.task.assigneeFullNames.concat(
        data.units.map((u) => u.unitName)
      );
    }
    console.log(" danh sách unit task cha  ",this.task.unitIds)
  }
  //----- assign tags popup ------

   onFrequencySelect(data: FrequencyView) {
    this.task = getFrequencySelected(this.task, data);
  }

  getContentTask(data: string) {
    this.task.description = data;
  }
  
  confirmGiaoViec(): void {
  if (!this.jobName?.trim()) {
    this.toastService.Warning('Vui lòng điền tên công việc!');
    return;
  }

  if (!this.task.assigneeIds?.length && !this.task.unitIds?.length) {
    this.toastService.Warning('Vui lòng phân công công việc!');
    return;
  }

  if (!this.task.startDate || !this.task.endDate) {
    this.toastService.Warning('Vui lòng chọn thời hạn công việc!');
    return;
  }

  if (!this.task.frequencyType) {
    this.toastService.Warning('Vui lòng chọn kỳ báo cáo công việc!');
    return;
  }

  // ✅ Trường hợp AI mode (chỉ emit ra cho AssignWorkPage xử lý)
  if (this.isAiMode || this.isFromAi) {
    const finalTask: TaskViewModel = {
      ...this.task,

      title: this.jobName,
      startDate: convertDateArrayToISOStrings(this.task.startDate ?? ''),
      endDate: convertDateArrayToISOStrings(this.task.endDate ?? ''),
    };
    this.aiTaskStorageService.clearAiTaskData();
    this.taskAssigned.emit(finalTask);
    return;
  }

  // ✅ Dữ liệu tạo task con
  const objectTaskChild = {
    title: this.jobName,
    description: this.task.description,
    assigneeIds: this.task.assigneeIds,
    unitIds: this.task.unitIds,
    startDate: convertDateArrayToISOStrings(this.task.startDate ?? ''),
    endDate: convertDateArrayToISOStrings(this.task.endDate ?? ''),
    frequencyType: this.task.frequencyType,
    intervalValue: this.task.intervalValue,
    daysOfWeek: this.task.daysOfWeek,
    daysOfMonth: this.task.daysOfMonth,
    parentTaskId: this.taskIdParent,
    assigneeFullNames: this.task.assigneeFullNames
  };

  this.assignWorkService.CreateChildTask(objectTaskChild).subscribe({
    next: (res) => {
      this.toastService.Success('Giao việc thành công!');
      const newChild: TaskViewModel = {
        ...objectTaskChild,
        id: res.taskId,
      };

      // ✅ Thêm trực tiếp vào danh sách
      this.listAssignWorkService.addTask(newChild);

      this.closeAddJob();
      this.refreshData.emit();
    },
    error: (err) => {
      this.toastService.Warning(err.message ?? 'Giao việc thất bại!');
      this.closeAddJob();
    },
  });
}

  onCancelConfirmGiaoViec() {
    this.visiblePopoverConfirmGiaoViec = false;
  }
  confirmHuyGiaoViec(): void {
    this.closeAddJob();
  }
  onCancelConfirmHuyGiaoViec() {
    this.visiblePopoverConfirmHuyGiaoViec = false;
  }

  onContentChanged(event: any) {
    this.task.description = event.html;
  }

  closeAddJob(): void {
    // Xóa dữ liệu AI khi đóng form
    if (this.isFromAi) {
      this.aiTaskStorageService.clearAiTaskData();
    }
    
    this.closeEdit.emit();
  }
  
  hadleAddJob(taskId: number) {
    const newTask: TaskViewModel = {
      ...this.task,
    };

    // this.listAssignWorkService.addTask(newTask);
  }
  convertDateTimeVn(datetime: string): string {
    return convertToVietnameseDate(datetime);
  }


  /**
   * Tạo Parent Task từ AI
   */
  private createParentTask(): void {
    const parentObj = {
      title: this.jobName,
      description: this.task.description,
      startDate: convertDateArrayToISOStrings(this.task.startDate ?? ''),
      endDate: convertDateArrayToISOStrings(this.task.endDate ?? ''),
      frequencyType: this.task.frequencyType,
      intervalValue: this.task.intervalValue,
      daysOfWeek: this.task.daysOfWeek,
      daysOfMonth: this.task.daysOfMonth,
      assigneeIds: this.task.assigneeIds,
      unitIds: this.task.unitIds
    };

    this.assignWorkService.CreateParentTask(parentObj).subscribe({
      next: (response) => {
        const finalTask: TaskViewModel = {
          ...this.task,
          id: response.taskId, 
          title: this.jobName,
          startDate: response.startDate,
          endDate: response.dueDate,
        };

        this.aiTaskStorageService.clearAiTaskData();
        this.taskAssigned.emit(finalTask);
        this.toastService.Success('Đã giao việc cha thành công!');
      },
      error: (err) => {
        this.toastService.Warning(err.message ?? 'Giao việc cha thất bại!');
      }
    });
  }

  /**
   * Tạo Child Task từ AI
   */
  private createChildTask(): void {
    if (!this.task.parentTaskId) {
      this.toastService.Warning('Không tìm thấy ID việc cha!');
      return;
    }

    const childObj = {
      title: this.jobName,
      description: this.task.description,
      assigneeIds: this.task.assigneeIds,
      unitIds: this.task.unitIds,
      startDate: convertDateArrayToISOStrings(this.task.startDate ?? ''),
      endDate: convertDateArrayToISOStrings(this.task.endDate ?? ''),
      frequencyType: this.task.frequencyType,
      intervalValue: this.task.intervalValue,
      daysOfWeek: this.task.daysOfWeek,
      daysOfMonth: this.task.daysOfMonth,
      parentTaskId: this.task.parentTaskId
    };

    this.assignWorkService.CreateChildTask(childObj).subscribe({
      next: (response) => {
        const finalTask: TaskViewModel = {
          ...this.task,
          id: response.taskId,
          title: this.jobName,
          parentTaskId: this.task.parentTaskId
        };

        this.aiTaskStorageService.clearAiTaskData();
        this.taskAssigned.emit(finalTask);
        this.toastService.Success('Đã giao việc con thành công!');
      },
      error: (err) => {
        this.toastService.Warning(err.message ?? 'Giao việc con thất bại!');
      }
    });
  }

}