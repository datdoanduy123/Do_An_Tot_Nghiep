import { Component, EventEmitter, Output, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzUploadModule, NzUploadFile } from 'ng-zorro-antd/upload';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { DocumentService } from '../../service/document.service';
import { AiAgentService, AITaskSuggestion } from '../../service/ai-agent.service';
import { ToastService } from '../../service/toast.service';
import { TaskViewModel } from '../../models/task-view.model';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DestroyRef, inject } from '@angular/core';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzTagModule } from 'ng-zorro-antd/tag';

@Component({
  selector: 'app-modal-ai-task-generator',
  standalone: true,
  imports: [
    CommonModule,
    NzTagModule, 
    NzModalModule,
    NzButtonModule,
    NzUploadModule,
    NzIconModule,
    NzSpinModule,
    NzCardModule
  ],
  templateUrl: './modal-ai-task-generator.component.html',
  styleUrls: ['./modal-ai-task-generator.component.css']
})
export class ModalAiTaskGeneratorComponent {
  @Output() tasksGenerated = new EventEmitter<TaskViewModel[]>();

  isVisible = false;
  selectedFile: File | null = null;
  uploadedFileId: number | null = null;
  fileName = '';
  isProcessing = false;
  currentStep = 0;
  aiSuggestion: AITaskSuggestion | null = null;
  isSubtasksVisible = false;

  private destroyRef = inject(DestroyRef);

  constructor(
    private documentService: DocumentService,
    private aiAgentService: AiAgentService,
    private toastService: ToastService
  ) {}

  showModal(): void {
    this.isVisible = true;
    this.resetState();
  }

  handleCancel(): void {
    this.isVisible = false;
    this.resetState();
  }

  beforeUpload = (file: NzUploadFile): boolean => {
  const isValidType = ['application/pdf', 'application/msword', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 'text/plain'].includes(file.type!);

  if (!isValidType) {
    this.toastService.Warning('Chỉ hỗ trợ file PDF, DOC, DOCX, TXT!');
    return false;
  }

  const isLt10M = file.size! / 1024 / 1024 < 10;
  if (!isLt10M) {
    this.toastService.Warning('File phải nhỏ hơn 10MB!');
    return false;
  }

  // Reset lại trạng thái khi chọn file mới
  this.selectedFile = file as any;
  this.fileName = file.name;
  this.uploadedFileId = null;
  this.aiSuggestion = null;
  this.isProcessing = false;
  this.currentStep = 0;
  return false; // Prevent auto upload
};

  handleFileChange(fileList: NzUploadFile[]): void {
    // Có thể xử lý khi danh sách file thay đổi nếu cần
  }

  removeFile(): void {
    this.selectedFile = null;
    this.fileName = '';
    this.uploadedFileId = null;
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
        next: (response) => {
          this.uploadedFileId = response.fileId;
          this.currentStep = 2;

          setTimeout(() => {
            this.currentStep = 3;
            this.generateAISuggestions();
          }, 1000);
        },
        error: (err) => {
          this.isProcessing = false;
          this.currentStep = 0;
          this.toastService.Error('Tải file thất bại!');
          console.error('Upload error:', err);
        }
      });
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
        error: (err) => {
          this.isProcessing = false;
          this.currentStep = 0;
          this.toastService.Error('AI không thể tạo được công việc từ file này!');
          console.error('AI generation error:', err);
        }
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

  applyTasks(): void {
  if (!this.aiSuggestion) return;

  // Convert AI suggestion to task format
  const convertedTasks = this.aiAgentService.convertToTaskViewModels(this.aiSuggestion);
  
  // Tạo array TaskViewModel[] để emit
  const tasksToEmit: TaskViewModel[] = [
    convertedTasks.parentTask,
    ...convertedTasks.childTasks
  ];

  // Emit array TaskViewModel[] thay vì object
  this.tasksGenerated.emit(tasksToEmit);

  this.toastService.Success(`Đã thêm ${tasksToEmit.length} công việc từ AI!`);
  this.handleCancel(); // Đóng modal
}

  private resetState(): void {
    this.selectedFile = null;
    this.uploadedFileId = null;
    this.fileName = '';
    this.isProcessing = false;
    this.currentStep = 0;
    this.aiSuggestion = null;
  }

  copyContent(content: string): void {
    navigator.clipboard.writeText(content).then(() => {
      this.toastService.Success('Đã copy nội dung!');
    }).catch(() => {
      this.toastService.Warning('Không thể copy nội dung!');
    });
  }
}
