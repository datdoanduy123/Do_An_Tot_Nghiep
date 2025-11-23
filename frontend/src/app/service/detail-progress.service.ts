import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../environment/environment';
import { map, Observable, forkJoin } from 'rxjs';
import { ResponseApi } from '../interface/response';
import {
  DetailProgressTaskChildModel,
  DetailProgressTaskParentModel,
  TaskDetailModel,
  SubtaskModel,
  AssignedUserModel
} from '../models/detail-progress.model';
import { ResponsePaganation } from '../interface/response-paganation';

@Injectable({
  providedIn: 'root',
})
export class DetailProgressService {
  apiUrl: string;

  constructor(private http: HttpClient) {
    this.apiUrl = `${environment.SERVICE_API}`;
  }

  // chi tiết tiến độ việc con
  detailProgressTaskChild(
    taskId: string
  ): Observable<DetailProgressTaskChildModel> {
    const url = `${this.apiUrl}review/detail-progress-taskchildren?taskId=${taskId}`;
    return this.http.get<ResponseApi<DetailProgressTaskChildModel>>(url).pipe(
      map((res) => {
        if (!res.success) throw new Error(res.message);
        // console.log(res.data )
        return res.data;
      })
    );
  }

  // chi tiết tiến độ việc cha
  detailProgressTaskParent(
    taskId: string,
    currentPage: string
  ): Observable<DetailProgressTaskParentModel> {
    const url = `${this.apiUrl}review/detail-TaskParent?parentTaskId=${taskId}&page=${currentPage}&pageSize=5`;
    return this.http.get<ResponseApi<DetailProgressTaskParentModel>>(url).pipe(
      map((res) => {
        if (!res.success) throw new Error(res.message);
        // console.log(res.data )
        return res.data;
      })
    );
  }

  // API mới: Lấy chi tiết task gốc
  getTaskDetail(taskId: string): Observable<TaskDetailModel> {
    const url = `${this.apiUrl}task/${taskId}`;
    return this.http.get<ResponseApi<TaskDetailModel>>(url).pipe(
      map((res) => {
        if (!res.success) throw new Error(res.message);
        return res.data;
      })
    );
  }

  // API mới: Lấy danh sách subtask theo parent task
  getSubtasksByParentTask(parentTaskId: string): Observable<{items: SubtaskModel[], metaData: any}> {
    const url = `${this.apiUrl}subtask/by-parent-task/${parentTaskId}`;
    return this.http.get<ResponseApi<{items: SubtaskModel[], metaData: any}>>(url).pipe(
      map((res) => {
        if (!res.success) throw new Error(res.message);
        return res.data;
      })
    );
  }

  // Kết hợp 2 API để lấy đầy đủ thông tin chi tiết tiến độ
  getFullProgressDetail(parentTaskId: string): Observable<DetailProgressTaskParentModel> {
    return forkJoin({
      taskDetail: this.getTaskDetail(parentTaskId),
      subtasks: this.getSubtasksByParentTask(parentTaskId)
    }).pipe(
      map(({taskDetail, subtasks}) => {
        // Chuyển đổi dữ liệu từ API mới sang format cũ
        const children: DetailProgressTaskChildModel[] = subtasks.items.map(subtask => ({
          taskId: subtask.taskId,
          taskName: subtask.title,
          description: subtask.description,
          taskStatus: subtask.status,
          assigneeFullName: subtask.assignedUsers.map(user => user.fullName),
          startDate: subtask.startDate,
          dueDate: subtask.dueDate,
          completionRate: subtask.percentagecomplete
        }));

        return {
          taskId: taskDetail.taskId,
          taskName: taskDetail.title,
          description: taskDetail.description,
          taskStatus: taskDetail.status,
          assigneeFullNames: [], // API mới không có thông tin này
          startDate: taskDetail.startDate,
          dueDate: taskDetail.dueDate,
          children: children
        };
      })
    );
  }
}