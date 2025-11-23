import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { TaskViewModel } from '../models/task-view.model';

@Injectable({
  providedIn: 'root',
})
export class ListAssignWorkService {
  private taskListSubject = new BehaviorSubject<TaskViewModel[]>([]);
  taskList$: Observable<TaskViewModel[]> = this.taskListSubject.asObservable();

  constructor() {}

  // Gán mới toàn bộ danh sách
  setTasks(tasks: TaskViewModel[]): void {
    this.taskListSubject.next([...tasks]); // clone để tránh mutation
  }

  // Lấy danh sách hiện tại
  getTasks(): TaskViewModel[] {
    return this.taskListSubject.getValue();
  }

  // Thêm task mới
  addTask(task: TaskViewModel): void {
    const current = this.getTasks();
    this.taskListSubject.next([...current, task]);
  }

  // Cập nhật một task theo ID
  updateTask(updated: TaskViewModel): void {
    const newList = this.getTasks().map((t) =>
      t.id === updated.id ? { ...updated } : t
    );
    this.taskListSubject.next(newList);
  }

  // Xóa task theo ID
  removeTask(id: number): void {
    const newList = this.getTasks().filter((t) => t.id !== id);
    this.taskListSubject.next(newList);
  }

  removeTaskWithChildren(taskId: number): void {
    const tasks = this.getTasks();

    // Tìm các task có id = taskId hoặc có parentTaskId = taskId
    const idsToRemove = tasks
      .filter((task) => task.id === taskId || task.parentTaskId === taskId)
      .map((task) => task.id);

    const remainingTasks = tasks.filter(
      (task) => !idsToRemove.includes(task.id)
    );

    this.taskListSubject.next(remainingTasks);
  }
  isTaskValid(task: TaskViewModel): boolean {
    if (task.parentTaskId === null) {
      return true;
    }

    const hasAssigneeOrUnit =
      task.assigneeIds !== null || task.unitIds !== null;
    const hasSchedule =
      task.startDate !== null &&
      task.endDate !== null &&
      task.frequencyType !== null &&
      task.intervalValue !== null;

    return hasAssigneeOrUnit && hasSchedule;
  }
  getInvalidTasks(): TaskViewModel[] {
    return this.getTasks().filter((task) => !this.isTaskValid(task));
  }
  areAllTasksValid(): boolean {
    return this.getInvalidTasks().length === 0;
  }
}
