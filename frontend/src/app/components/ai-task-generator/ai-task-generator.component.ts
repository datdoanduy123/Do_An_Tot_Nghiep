import { Component, EventEmitter, Input, Output, inject, DestroyRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzUploadModule, NzUploadFile } from 'ng-zorro-antd/upload';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { DocumentService } from '../../service/document.service';
import { AiAgentService, AITaskSuggestion } from '../../service/ai-agent.service';
import { ToastService } from '../../service/toast.service';
import { TaskViewModel } from '../../models/task-view.model';
import { EditMissonItemComponent } from '../edit-misson-item/edit-misson-item.component';
import { AiTaskStorageService, AiTaskData } from '../../service/ai-task-storage.service';
import { ModalAddJobComponent } from '../modal-add-job/modal-add-job.component';
import { ModalViewFileComponent } from '../modal-view-file/modal-view-file.component';
import { DocumentModel } from '../../models/document.model'; // 👈 NEW: Import DocumentModel

@Component({
  selector: 'app-ai-task-generator',
  standalone: true,
  imports: [
    CommonModule,
    NzButtonModule,
    NzUploadModule,
    NzIconModule,
    NzSpinModule,
    NzCardModule,
    NzTagModule,
    NzModalModule,
    NzAlertModule,
    EditMissonItemComponent,
    ModalAddJobComponent,
    ModalViewFileComponent 
  ],
  templateUrl: './ai-task-generator.component.html',
  styleUrl: './ai-task-generator.component.css'
})
export class AiTaskGeneratorComponent {
  @Input() isVisible = false;
  @Output() tasksGenerated = new EventEmitter<TaskViewModel[]>();
  @Output() closeGenerator = new EventEmitter<void>();
  @Output() taskAssigned = new EventEmitter<TaskViewModel>();
  @Output() parentTaskAssigned = new EventEmitter<{parentTask: any, subtasks: any[]}>();

  @ViewChild(EditMissonItemComponent) editMissionComponent!: EditMissonItemComponent;
  @ViewChild(ModalAddJobComponent) modalAddJobComponent!: ModalAddJobComponent;
  @ViewChild(ModalViewFileComponent) modalViewFileComponent!: ModalViewFileComponent;

  // AI Generator State
  selectedFile: File | null = null;
  uploadedFileId: number | null = null;
  fileName = '';
  isProcessing = false;
  aiSuggestion: AITaskSuggestion | null = null;
  currentStep = 0;
  isSubtasksVisible = false;
  
  showAssignModal = false;
  selectedSubtask: any = null;
  selectedSubtaskIndex: number = -1;
  prefilledTask: TaskViewModel | null = null;

  // Task Assignment State for Parent
  showAssignParentModal = false;
  prefilledParentTask: TaskViewModel | null = null;
  // Parent task assignment ID (when parent is assigned)
  assignedParentTaskId: number | null = null;

  uploadedDocument: DocumentModel | null = null;

  private destroyRef = inject(DestroyRef);

  constructor(
    private documentService: DocumentService,
    private aiAgentService: AiAgentService,
    private toastService: ToastService,
    private aiTaskStorageService: AiTaskStorageService
  ) {}

  // ===== UPDATED: File Preview Methods =====

  /**
   * Xem trước file đã tải lên - áp dụng cách của DocumentPageComponent
   */
  previewUploadedFile(): void {
    if (!this.uploadedDocument) {
      this.toastService.Warning('Không tìm thấy thông tin file!');
      return;
    }

    // Sử dụng cách preview giống DocumentPageComponent
    this.previewFile(this.uploadedDocument);
  }

  /**
   * Preview file method - copy từ DocumentPageComponent
   */
  previewFile(doc: DocumentModel): void {
    if (!doc.filePath) {
      this.toastService.Warning('Không thể xem trước file này!');
      return;
    }

    this.modalViewFileComponent.fileUrl = doc.filePath;
    this.modalViewFileComponent.fileName = doc.fileName;
    this.modalViewFileComponent.showModal();
  }
  canPreviewFile(): boolean {
    if (!this.uploadedDocument || !this.uploadedDocument.filePath) return false;
    
    const extension = this.fileName.toLowerCase().split('.').pop();
    const previewableExtensions = ['pdf', 'doc', 'docx', 'txt', 'jpg', 'jpeg', 'png', 'gif'];
    
    return previewableExtensions.includes(extension || '');
  }

  assignParentTask(): void {
    if (!this.aiSuggestion) return;

    const aiParentData: AiTaskData = {
      title: this.aiSuggestion.title,
      description: this.aiSuggestion.description,
      startDate: this.convertDateFormat(this.aiSuggestion.startDate),
      endDate: this.convertDateFormat(this.aiSuggestion.endDate),
      fileName: this.fileName,
      source: 'ai'
    };
    
    this.aiTaskStorageService.saveAiTaskData(aiParentData);
    this.modalAddJobComponent.showModal();
    this.prefilledParentTaskToModal();
  }

  private prefilledParentTaskToModal(): void {
    if (!this.aiSuggestion || !this.modalAddJobComponent) return;
    setTimeout(() => {
      this.modalAddJobComponent.jobName = this.aiSuggestion!.title;
      this.modalAddJobComponent.description = this.aiSuggestion!.description;
      this.modalAddJobComponent.task.startDate = this.convertDateFormat(this.aiSuggestion!.startDate);
      this.modalAddJobComponent.task.endDate = this.convertDateFormat(this.aiSuggestion!.endDate);

      this.toastService.Success(`Đã điền sẵn dữ liệu từ AI: ${this.aiSuggestion!.title}`);
    }, 100);
  }

  handleParentJobAdded(parentTask: TaskViewModel): void {
    this.assignedParentTaskId = parentTask.id;
    
    this.parentTaskAssigned.emit({
      parentTask: parentTask,
      subtasks: this.aiSuggestion?.subtasks || []
    });
    
    this.aiTaskStorageService.clearAiTaskData();
    this.toastService.Success(`Đã giao việc cha: ${parentTask.title}. Bây giờ có thể giao các việc con!`);
  }

  assignSubtask(subtask: any, index: number): void {
    if (!this.assignedParentTaskId) {
      this.toastService.Warning('Vui lòng giao việc cha trước khi giao việc con!');
      return;
    }

    this.selectedSubtask = subtask;
    this.selectedSubtaskIndex = index;
    
    const aiTaskData: AiTaskData = {
      title: subtask.title,
      description: subtask.description,
      startDate: this.convertDateFormat(subtask.startDate),
      endDate: this.convertDateFormat(subtask.dueDate),
      fileName: this.fileName,
      source: 'ai'
    };
    
    this.aiTaskStorageService.saveAiTaskData(aiTaskData);
    
    this.prefilledTask = {
      ...this.createPrefilledTask(subtask),
      parentTaskId: this.assignedParentTaskId
    };
    
    this.showAssignModal = true;
  }

  handleTaskAssigned(task: TaskViewModel): void {
    this.taskAssigned.emit(task);
    this.closeAssignModal();
    this.aiTaskStorageService.clearAiTaskData();
    this.toastService.Success(`Đã giao việc: ${task.title}`);
  }

  closeAssignModal(): void {
    this.showAssignModal = false;
    this.selectedSubtask = null;
    this.selectedSubtaskIndex = -1;
    this.prefilledTask = null;
    this.aiTaskStorageService.clearAiTaskData();
  }

  get isParentAssigned(): boolean {
    return this.assignedParentTaskId !== null;
  }

  private createPrefilledTask(subtask: any): TaskViewModel {
    return {
      id: Date.now(),
      title: subtask.title,
      description: subtask.description,
      assigneeIds: [],
      unitIds: [],
      assigneeFullNames: [],
      startDate: this.convertDateFormat(subtask.startDate),
      endDate: this.convertDateFormat(subtask.dueDate),
      frequencyType: 'once',
      intervalValue: 1,
      daysOfWeek: [],
      daysOfMonth: [],
      parentTaskId: null,
    };
  }

  private convertDateFormat(dateString: string): string {
    if (!dateString) return new Date().toISOString();
    
    try {
      if (dateString.includes('/')) {
        const [day, month, year] = dateString.split('/');
        const date = new Date(parseInt(year), parseInt(month) - 1, parseInt(day));
        return date.toISOString();
      }
      
      return new Date(dateString).toISOString();
    } catch (error) {
      return new Date().toISOString();
    }
  }

  beforeUpload = (file: NzUploadFile): boolean => {
    const isValidType = [
      'application/pdf',
      'application/msword',
      'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
      'text/plain'
    ].includes(file.type!);

    if (!isValidType) {
      this.toastService.Warning('Chỉ hỗ trợ file PDF, DOC, DOCX, TXT!');
      return false;
    }

    if (file.size! / 1024 / 1024 >= 10) {
      this.toastService.Warning('File phải nhỏ hơn 10MB!');
      return false;
    }

    this.selectedFile = file as any;
    this.fileName = file.name;
    this.uploadedFileId = null;
    this.uploadedDocument = null; 
    this.aiSuggestion = null;
    this.currentStep = 0;
    return false;
  };

  removeFile(): void {
    this.selectedFile = null;
    this.fileName = '';
    this.uploadedFileId = null;
    this.uploadedDocument = null; 
    this.aiSuggestion = null;
    this.currentStep = 0;
  }

  generateWithAI(): void {
    if (!this.selectedFile) {
      this.toastService.Warning('Vui lòng chọn file!');
      return;
    }

    this.isProcessing = true;
    this.currentStep = 1;

    const formData = new FormData();
    formData.append('file', this.selectedFile);

    this.documentService.uploadDoc(formData)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => this.handleFileUploaded(response),
        error: (err) => this.handleError('Tải file thất bại!', err)
      });
  }

  private handleFileUploaded(documentModel: DocumentModel): void {
    this.uploadedFileId = documentModel.fileId;
    this.uploadedDocument = documentModel; 
    this.currentStep = 2;

    setTimeout(() => {
      this.currentStep = 3;
      this.generateAISuggestions();
    }, 1000);
  }

  private generateAISuggestions(): void {
    if (!this.uploadedFileId) return;

    this.aiAgentService.generateTaskSuggestions(this.uploadedFileId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (suggestion) => {
          this.aiSuggestion = suggestion;
          this.isProcessing = false;
          this.toastService.Success('AI đã tạo công việc thành công!');
        },
        error: (err) => this.handleError('AI không thể tạo được công việc từ file này!', err)
      });
  }

  regenerate(): void {
    if (!this.uploadedFileId) return;
    
    this.isProcessing = true;
    this.currentStep = 2;
    this.aiSuggestion = null;

    setTimeout(() => {
      this.currentStep = 3;
      this.generateAISuggestions();
    }, 1000);
  }

  applyAllTasks(): void {
    if (!this.aiSuggestion) return;

    const convertedTasks = this.aiAgentService.convertToTaskViewModels(this.aiSuggestion);
    
    const tasksToEmit: TaskViewModel[] = [
      convertedTasks.parentTask,
      ...convertedTasks.childTasks
    ];

    this.tasksGenerated.emit(tasksToEmit);
    this.toastService.Success(`Đã thêm ${tasksToEmit.length} công việc từ AI!`);
    this.close();
  }

  close(): void {
    this.resetState();
    this.closeGenerator.emit();
  }

  private resetState(): void {
    this.selectedFile = null;
    this.uploadedFileId = null;
    this.fileName = '';
    this.uploadedDocument = null;
    this.isProcessing = false;
    this.currentStep = 0;
    this.aiSuggestion = null;
    this.isSubtasksVisible = false;
  }

  copyContent(content: string): void {
    navigator.clipboard.writeText(content).then(() => {
      this.toastService.Success('Đã copy nội dung!');
    }).catch(() => {
      this.toastService.Warning('Không thể copy nội dung!');
    });
  }

  private handleError(msg: string, err: any): void {
    this.isProcessing = false;
    this.currentStep = 0;
    this.toastService.Error(msg);
    console.error(err);
  }
}
