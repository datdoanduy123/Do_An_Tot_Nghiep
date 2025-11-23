
import { Component, OnInit, inject, DestroyRef } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { AiAgentService, AITaskSuggestion } from '../../service/ai-agent.service';
import { ToastService } from '../../service/toast.service';

@Component({
  selector: 'app-ai-agent-page',
  standalone: true,
  imports: [
    CommonModule,
    NzButtonModule,
    NzCardModule,
    NzSpinModule,
    NzIconModule,
    NzDividerModule,
    NzTagModule,
    NzPopconfirmModule,
  ],
  templateUrl: './ai-agent-page.component.html',
  styleUrl: './ai-agent-page.component.css',
})
export class AiAgentPageComponent implements OnInit {
  fileId: number | null = null;
  fileName: string = '';
  aiSuggestion: AITaskSuggestion | null = null;
  isGenerating = false;
  isSubtasksVisible = false;
  private destroyRef = inject(DestroyRef);

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private location: Location,
    private aiAgentService: AiAgentService,
    private toastService: ToastService
  ) {}

  ngOnInit(): void {
    this.route.queryParams
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((params) => {
        this.fileId = +params['fileId'];
        this.fileName = params['fileName'] || 'File';
        
        if (this.fileId) {
          this.generateTaskSuggestions();
        }
      });
  }

  generateTaskSuggestions(): void {
    if (!this.fileId) return;

    this.isGenerating = true;
    this.aiAgentService.generateTaskSuggestions(this.fileId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (suggestion) => {
          this.aiSuggestion = suggestion;
          this.isGenerating = false;
          console.log('AI Suggestion received:', suggestion);
        },
        error: (err) => {
          console.error('Error generating suggestion:', err);
          this.toastService.Error(
            'Không thể tạo đề xuất công việc. Vui lòng thử lại!'
          );
          this.isGenerating = false;
        }
      });
  }

  regenerateSuggestions(): void {
    this.aiSuggestion = null;
    this.generateTaskSuggestions();
  }

  goBack(): void {
    this.location.back();
  }

  formatDate(dateString: string): string {
    if (!dateString) return 'Chưa xác định';
    return dateString;
  }

  calculateDuration(startDate: string, endDate: string): number {
    try {
      const start = new Date(startDate.split('/').reverse().join('-'));
      const end = new Date(endDate.split('/').reverse().join('-'));
      const diffTime = Math.abs(end.getTime() - start.getTime());
      const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
      return diffDays;
    } catch (e) {
      return 0;
    }
  }

  toggleSubtasks(): void {
    this.isSubtasksVisible = !this.isSubtasksVisible;
  }

  copyContent(content: string): void {
    navigator.clipboard.writeText(content).then(() => {
      this.toastService.Success('Đã copy nội dung!');
    }).catch(() => {
      this.toastService.Warning('Không thể copy nội dung!');
    });
  }
}