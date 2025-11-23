// src/app/pages/assign-work-page/create-ai-job-page/create-ai-job-page.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { AiTaskGeneratorComponent } from '../../../components/ai-task-generator/ai-task-generator.component';
import { TaskViewModel } from '../../../models/task-view.model';
import { ToastService } from '../../../service/toast.service';
import { ListAssignWorkService } from '../../../service/list-assign-work.service';

@Component({
  selector: 'app-create-ai-job-page',
  standalone: true,
  imports: [
    CommonModule,
    NzButtonModule,
    NzIconModule,
    AiTaskGeneratorComponent
  ],
  templateUrl: './create-ai-job-page.component.html',
  styleUrl: './create-ai-job-page.component.css'
})
export class CreateAiJobPageComponent implements OnInit {
  
  constructor(
    private router: Router,
    private location: Location,
    private toastService: ToastService,
    private listAssignWorkService: ListAssignWorkService
  ) {}

  ngOnInit(): void {
    // Optional: Load any existing tasks
  }

  /**
   * Quay lại trang assign work
   */
  goBack(): void {
    this.router.navigate(['/assignWork']);
  }

  /**
   * Đóng AI Generator và quay về
   */
  handleAiGeneratorClosed(): void {
    this.goBack();
  }

  /**
   * Xử lý khi AI Generator emit tasks (thêm tất cả không giao việc)
   */
  handleAiTasksGenerated(tasks: TaskViewModel[]): void {
    // Thêm từng task vào service
    tasks.forEach(task => {
      this.listAssignWorkService.addTask(task);
    });

    this.toastService.Success(`Đã thêm ${tasks.length} công việc từ AI!`);
    
    // Chuyển về trang assign work để hiển thị tasks
    this.router.navigate(['/assignWork']);
  }

  /**
   * Xử lý khi parent task được giao từ AI
   */
  handleAiParentTaskAssigned(data: {parentTask: any, subtasks: any[]}): void {
    const { parentTask, subtasks } = data;
    
    // Thêm parent task vào service
    this.listAssignWorkService.addTask(parentTask);
    
    this.toastService.Success(`Đã giao việc cha: ${parentTask.title}. Có ${subtasks.length} việc con cần giao!`);
    
    // Chuyển về trang assign work
    // this.router.navigate(['/assignWork']);
  }

  /**
   * Xử lý khi child task được giao từ AI
   */
  handleAiTaskAssigned(task: TaskViewModel): void {
    // Child task đã có parentTaskId và được lưu vào database rồi
    this.toastService.Success(`Đã giao việc con: ${task.title}`);
    
    // Có thể ở lại trang hoặc chuyển về - tùy UX
    // this.router.navigate(['/assignWork']);
  }
}