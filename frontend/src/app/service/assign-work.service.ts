import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../environment/environment';
import { map, Observable } from 'rxjs';
import { ResponseApi } from '../interface/response';
import { UserModel, UserRelationModel } from '../models/user.model';
import { UnitStructureModel } from '../models/unit.model';
import { TaskParentModel } from '../models/task-parent.model';

@Injectable({
  providedIn: 'root',
})
export class AssignWorkService {
  apiUrl: string;
  constructor(private http: HttpClient) {
    this.apiUrl = `${environment.SERVICE_API}`;
  }

  // lay theo don vi - backlend chua có
  GetUnitsAssign(): Observable<UnitStructureModel> {
    // https://doctask-production.up.railway.app/api/v1/subtask/assignable-units
    const url = `${this.apiUrl}subtask/assignable-units`;
    return this.http.get<ResponseApi<UnitStructureModel>>(url).pipe(
      map((res) => {
        if (!res.success) {
          throw Error(res.message);
        }
        return res.data;
      })
    );
  }
// lay danh sach ngang cap
  GetUsersAssign(): Observable<UserRelationModel> {
    const url = `${this.apiUrl}subtask/assignable-users`;
    return this.http.get<ResponseApi<UserRelationModel>>(url).pipe(
      map((res) => {
        if (!res.success) {
          throw Error(res.message);
        }
        return res.data;
      })
    );
  }
  //tao task giao viec 
 CreateParentTask(taskDesp: Partial<TaskParentModel>): Observable<TaskParentModel> {
  const url = `${this.apiUrl}task`;
  return this.http.post<ResponseApi<TaskParentModel>>(url, taskDesp).pipe(
    map((res) => {
      if (!res.success) {
        throw new Error(res.message || 'Không thể tạo công việc cha');
      }
      return res.data;
    })
  );
}


    // tạo giao viec task con
    // CreateChildTask(taskDesp: any): Observable<CreateChildTaskResponse> {
    //   const parentId = taskDesp.parentTaskId;
    //   const url = `${this.apiUrl}subtask/${parentId}`;
  
    //   // Map payload to new API shape
    //   const payload = {
    //     title: taskDesp.title,
    //     description: taskDesp.description,
    //     startDate: taskDesp.startDate,        // already ISO
    //     dueDate: taskDesp.endDate,            // rename endDate -> dueDate
    //     frequency: taskDesp.frequencyType,    // rename frequencyType -> frequency
    //     intervalValue: taskDesp.intervalValue,
    //     days: (taskDesp.daysOfWeek?.length ? taskDesp.daysOfWeek : taskDesp.daysOfMonth) ?? [],
    //     // choose the first assignee as primary if provided, and send full list as assignedUserIds
    //     assigneeId: Array.isArray(taskDesp.assigneeIds) && taskDesp.assigneeIds.length ? taskDesp.assigneeIds[0] : null,
    //     assignedUserIds: taskDesp.assigneeIds ?? [],
    //     //  thêm paylaod unit
    //     assignedUnits : Array.isArray(taskDesp.assignedUnits) && taskDesp.assignedUnits.length ? taskDesp.assignedUserIds[0]:null,
    //     assignedUnitsIds : taskDesp.assignedUserIds ?? []
    //   };
  
    //   return this.http
    //     .post<ResponseApi<any>>(url, payload)
    //     .pipe(
    //       map((res) => {
    //         if (!res.success) {
    //           throw Error(res.message);
    //         }
    //         // Normalize response so callers can keep using { taskId }
    //         const d = res.data ?? {};
    //         const mapped: CreateChildTaskResponse = {
    //           taskId: d.taskId,
    //           scheduleDates: []
    //         };
    //         return mapped;
    //       })
    //     );
    // }
    CreateChildTask(taskDesp: any): Observable<CreateChildTaskResponse> {
  const parentId = taskDesp.parentTaskId;
  const url = `${this.apiUrl}subtask/${parentId}`;

  // ✅ Chuẩn payload theo yêu cầu mới
  const payload = {
    title: taskDesp.title,
    description: taskDesp.description,
    startDate: taskDesp.startDate,
    dueDate: taskDesp.endDate,
    frequency: taskDesp.frequencyType,
    intervalValue: taskDesp.intervalValue ?? 0,
    days:
      (taskDesp.daysOfWeek?.length
        ? taskDesp.daysOfWeek
        : taskDesp.daysOfMonth) ?? [],
    assignedUserIds: taskDesp.assigneeIds ?? [],
    assignedUnitIds: taskDesp.unitIds ?? [],
  };

  console.log('📦 Payload gửi API:', payload);

  return this.http.post<ResponseApi<any>>(url, payload).pipe(
    map((res) => {
      if (!res.success) {
        throw Error(res.error || res.message || 'Tạo task con thất bại!');
      }
      const d = res.data ?? {};
      const mapped: CreateChildTaskResponse = {
        taskId: d.taskId,
        scheduleDates: [],
      };
      return mapped;
    })
  );
}

}
export interface CreateChildTaskResponse {
  taskId: number;
  scheduleDates: string[]; // hoặc Date[] nếu bạn parse về Date
}