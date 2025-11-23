import { AssignWorkService } from './../../service/assign-work.service';
import { CommonModule, Location } from '@angular/common';
import { Component, OnInit, QueryList, ViewChild, ViewChildren } from '@angular/core';
import { MissonItemComponent } from '../../components/misson-item/misson-item.component';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { ToastService } from '../../service/toast.service';
import { ModalAddJobComponent } from '../../components/modal-add-job/modal-add-job.component';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { TaskViewModel } from '../../models/task-view.model';
import { NzCollapseModule } from 'ng-zorro-antd/collapse';
import { FormsModule } from '@angular/forms';
import { ListAssignWorkService } from '../../service/list-assign-work.service';
import { ModalDateTimePicker } from '../../components/modal-date-time-picker/modal-date-time-picker.component';
import { EditMissonItemComponent } from '../../components/edit-misson-item/edit-misson-item.component';
import { NzPopoverModule } from 'ng-zorro-antd/popover';
import { AiTaskGeneratorComponent } from '../../components/ai-task-generator/ai-task-generator.component';
import { AiTaskStorageService } from '../../service/ai-task-storage.service';
import { Router } from '@angular/router'; 


@Component({
  selector: 'app-assign-work-page',
  templateUrl: './assign-work-page.component.html',
  styleUrl: './assign-work-page.component.css',
  standalone: true,
  imports: [
    CommonModule,
    NzModalModule,
    ModalAddJobComponent,
    NzButtonModule,
    NzIconModule,
    NzCollapseModule,
    FormsModule,
    NzPopoverModule,
    MissonItemComponent,
    EditMissonItemComponent,
    // AiTaskGeneratorComponent 
  ],
})
export class AssignWorkPageComponent implements OnInit {

  @ViewChild(ModalAddJobComponent) ModalAddJobComponentRef!: ModalAddJobComponent;
  @ViewChild(ModalDateTimePicker) modalDateTimePickerRef!: ModalDateTimePicker;
@ViewChildren(EditMissonItemComponent) childComponents!: QueryList<EditMissonItemComponent>;


  taskList: TaskViewModel[] = [];
  isShowDateTimePicker = false;
  isShowAddJob = false;
  uploadedFileName: string | null = null;
  expandedGroups: boolean[] = [];
  visiblePopoverConfirmMap: { [taskId: string]: boolean } = {};

  showAiGenerator = false;

  constructor(
    private modal: NzModalService,
    private toastService: ToastService,
    private locationRoute: Location,
    private listAssignWorkService: ListAssignWorkService,
    private assignWorkService: AssignWorkService, 
     private aiTaskStorageService: AiTaskStorageService,
     private router: Router
  ) {}

  ngOnInit(): void {
    
    this.listAssignWorkService.taskList$.subscribe((tasks) => {
      this.taskList = tasks;
      
    });
  }

  showAddJob() {
    this.isShowAddJob = true;
  }
  
  closeAddJob() {
    this.isShowAddJob = false;
  }
  
  showModalAddJob(): void {
    this.ModalAddJobComponentRef.showModal();
  }

  showModalAiTaskGenerator(): void {
    // this.showAiGenerator = true;
    this.router.navigate(['/assignWork/createaijob']);
  }

  hideAiGenerator(): void {
    this.showAiGenerator = false;
  }

  handleAiTasksGenerated(tasks: TaskViewModel[]): void {
    tasks.forEach(task => {
      this.listAssignWorkService.addTask(task);
    });

    this.toastService.Success(`Đã thêm ${tasks.length} công việc từ AI!`);

    setTimeout(() => {
      const taskGroupElement = document.querySelector('.task-group');
      if (taskGroupElement) {
        taskGroupElement.scrollIntoView({ behavior: 'smooth' });
      }
    }, 100);
  }
   handleAiTaskAssigned(task: TaskViewModel): void {
    // Tạo parent task từ AI data trước
    const parentObj = { 
      title: task.title, 
      description: task.description, 
      priority: '' 
    };
    
    this.assignWorkService.CreateParentTask(parentObj).subscribe({
      next: (resParent) => {
        // Sau đó tạo child task
        const childObj = {
          title: task.title,
          description: task.description,
          assigneeIds: task.assigneeIds,
          unitIds: task.unitIds,
          startDate: task.startDate,
          endDate: task.endDate,
          frequencyType: task.frequencyType,
          intervalValue: task.intervalValue,
          daysOfWeek: task.daysOfWeek,
          daysOfMonth: task.daysOfMonth,
          parentTaskId: resParent.taskId
        };

        this.assignWorkService.CreateChildTask(childObj).subscribe({
          next: () => {
            this.toastService.Success(`Đã giao việc AI: ${task.title}`);
            
            // Refresh view
            setTimeout(() => {
              this.ngOnInit();
            }, 100);
          },
          error: (err) => this.toastService.Error(err.message ?? 'Giao việc AI thất bại!')
        });
      },
      error: (err) => this.toastService.Error(err.message ?? 'Tạo task cha thất bại!')
    });
  }

//   handleAiTaskAssigned(task: TaskViewModel): void {
//   // Gọi API tạo task cha, sau đó tạo task con
//   this.assignWorkService.CreateParentTask(task).subscribe({
//     next: () => {
//       this.toastService.Success(`Đã giao việc con: ${task.title}`);

//       // Refresh view sau khi thành công
//       setTimeout(() => {
//         this.ngOnInit();
//       }, 100);
//     },
//     error: (err) => this.toastService.Error(err.message ?? 'Giao việc AI thất bại!')
//   });
// }

  handleAiGeneratorClosed(): void {
    this.showAiGenerator = false;
  }
   handleAiParentTaskAssigned(data: {parentTask: any, subtasks: any[]}): void {
    const { parentTask, subtasks } = data;

    this.listAssignWorkService.addTask(parentTask);

    this.toastService.Success(`Đã giao việc cha: ${parentTask.title}. Có ${subtasks.length} việc con cần giao!`);
    
    // Refresh view
    setTimeout(() => {
      this.ngOnInit();
    }, 100);
  }

  get parentTasks(): TaskViewModel[] {
    return this.taskList.filter((t) => t.parentTaskId === null);
  }

  getChildTasks(parentId: number): TaskViewModel[] {
        console.log("Các task con", this.taskList)

    return this.taskList.filter((task) => task.parentTaskId === parentId);
  }

  removeTask(id: number) {
    this.listAssignWorkService.removeTaskWithChildren(id);
  }

  getAllNestedTaskIds(parentId: number): number[] {
    const toRemove: number[] = [parentId];

    const findChildren = (pid: number) => {
      const children = this.taskList.filter((t) => t.parentTaskId === pid);
      for (const child of children) {
        toRemove.push(child.id);
      }
    };

    findChildren(parentId);
    return toRemove;
  }
  handleChildCreated(newTask: TaskViewModel) {
  this.listAssignWorkService.addTask(newTask);
  this.toastService.Success(`Đã thêm công việc con: ${newTask.title}`);
}

  
  handleJobAdded(task: TaskViewModel) {
    this.listAssignWorkService.addTask(task);
  }


  showConfirm(): void {
    if (!this.listAssignWorkService.areAllTasksValid()) {
      return this.toastService.Warning('Vui lòng điền đầy đủ các trường!');
    }

    this.modal.confirm({
      nzTitle: '<i>Bạn chắc chắn hoàn thành giao việc?</i>',
      nzOnOk: () => this.createTasksFromList(),
      nzOkText: 'Xác nhận',
      nzCancelText: 'Hủy bỏ',
    });
  }

  private createTasksFromList(): void {
    this.parentTasks.forEach(parentTask => {
      const parentObj = { 
        title: parentTask.title, 
        description: parentTask.description, 
        priority: '' 
      };
      
      this.assignWorkService.CreateParentTask(parentObj).subscribe({
        next: (resParent) => this.createChildTasks(resParent.taskId, parentTask.id),
        error: (err) => this.toastService.Warning(err.message ?? 'Giao việc thất bại !'),
      });
    });
  }

  private createChildTasks(parentTaskId: number, parentId: number): void {
    this.getChildTasks(parentId).forEach(child => {
      const childObj = {
        title: child.title,
        description: child.description,
        assigneeIds: child.assigneeIds,
        unitIds: child.unitIds,
        startDate: child.startDate,
        endDate: child.endDate,
        frequencyType: child.frequencyType,
        intervalValue: child.intervalValue,
        daysOfWeek: child.daysOfWeek,
        daysOfMonth: child.daysOfMonth,
        parentTaskId
      };

      this.assignWorkService.CreateChildTask(childObj).subscribe({
        next: () => this.toastService.Success('Giao việc thành công!'),
        error: (err) => this.toastService.Warning(err.message ?? 'Giao việc thất bại !'),
      });
    });
  }

  showConfirmDelete(id: number): void {
    this.modal.confirm({
      nzTitle: '<i>Bạn chắc chắn muốn xóa ?</i>',
      nzOnOk: () => this.removeTask(id),
      nzOkText: 'Xác nhận',
      nzCancelText: 'Hủy bỏ',
    });
  }

  cancelAssign(): void {
    this.modal.confirm({
      nzTitle: '<i>Bạn chắc chắn chứ ?</i>',
      nzContent: 'Dữ liệu giao việc hiện tại sẽ mất !',
      nzOnOk: () => this.locationRoute.back(),
      nzOkText: 'Xác nhận',
      nzCancelText: 'Hủy bỏ',
    });
  }
  confirmGiaoViec(taskId: number) {
  this.visiblePopoverConfirmMap[taskId] = false;

  const targetChild = this.childComponents.find(
    (child) => child.taskIdParent === taskId
  );

  if (targetChild) {
    targetChild.confirmGiaoViec();
  } else {
    console.warn('Không tìm thấy EditMissonItemComponent cho taskId:', taskId);
  }
}

onCancelConfirmGiaoViec(taskId: number) {
  this.visiblePopoverConfirmMap[taskId] = false;

  const targetChild = this.childComponents.find(
    (child) => child.taskIdParent === taskId
  );
  

  if (targetChild) {
    targetChild.onCancelConfirmGiaoViec();
  }
}

}