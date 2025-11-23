import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../environment/environment';
import { BehaviorSubject, map, Observable, switchMap } from 'rxjs';
import { DetailViecquanlyModel } from '../models/detail-viecquanly.model';
import { ResponseApi } from '../interface/response';
import { ResponsePaganation } from '../interface/response-paganation';

@Injectable({
  providedIn: 'root',
})
export class DetailViecquanlyService {
  private refreshTrigger$ = new BehaviorSubject<void>(undefined); // BehaviorSubject để kích hoạt refresh
  apiUrl: string;

  constructor(private http: HttpClient) {
    this.apiUrl = `${environment.SERVICE_API}`;
  }

  // Phương thức để kích hoạt refresh
  triggerRefresh() {
    this.refreshTrigger$.next();
  }

  // Observable để component subscribe và nhận dữ liệu khi refresh
  onRefresh(
    taskId: string,
    pageNumber: string
  ): Observable<ResponsePaganation<DetailViecquanlyModel>> {
    return this.refreshTrigger$.pipe(
      switchMap(() => this.getAllData(taskId, pageNumber))
    );
  }

  editDetailViecQuanly(taskId: string, obj: any): Observable<any> {
    const url = `${this.apiUrl}subtask?subTaskId=${taskId}`;
    return this.http.put<ResponseApi<any>>(url, obj).pipe(
      map((res) => {
        if (!res.success) {
          throw Error(res.message);
        } else {
          return res.data;
        }
      })
    );
  }
  
  // getDetailTaskById()
  
  deleteSubtask(subTaskId: string): Observable<boolean> {
    const url = `${this.apiUrl}subtask?subTaskId=${subTaskId}`;
    return this.http.delete<ResponseApi<boolean>>(url).pipe(
      map((res) => {
        if (!res.success) {
          throw Error(res.message);
        } else {
          return res.data;
        }
      })
    );
  }

   getAllData(
  taskId: string,
  pageNumber: string
): Observable<ResponsePaganation<DetailViecquanlyModel>> {
  const url = `${this.apiUrl}subtask/by-parent-task/${taskId}?page=${pageNumber}&pageSize=10`;

  return this.http.get<ResponseApi<any>>(url).pipe(
    map((res) => {
      if (!res.success) throw Error(res.message);

      const d = res.data;
      const md = d?.metaData ?? {};

      const items: DetailViecquanlyModel[] = (d?.items ?? []).map((it: any) => ({
        TaskId: it.taskId,
        Title: it.title,
        Description: it.description,
        StartDate: it.startDate,
        DueDate: it.dueDate,
        PercentageComplete: it.percentagecomplete,
        AssigneeFullNames: (it.assignedUsers ?? []).map((u: any) => u.fullName),
        AssignedUnits: it.assignedUnits ?? [],
        FrequencyType: it.frequencyType,
        IntervalValue: it.intervalValue,
        dayOfWeek: it.frequencyDays ?? [],
        dayOfMonth: null,
      }));

      const mapped: ResponsePaganation<DetailViecquanlyModel> = {
        items,
        currentPage: md.pageIndex ?? 1,
        totalPages: md.totalPages ?? 1,
        pageSize: md.currentItems ?? 10,
        totalItems: md.totalItems ?? 0,
        // metaData: {
        //   pageIndex: md.pageIndex ?? 1,
        //   totalPages: md.totalPages ?? 1,
        //   totalItems: md.totalItems ?? 0,
        //   currentItems: md.currentItems ?? items.length,
        //   hasPrevious: md.hasPrevious ?? false,
        //   hasNext: md.hasNext ?? false,
        // },
      };

      return mapped;
    })
  );
}

}
