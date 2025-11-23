import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../environment/environment';
import { BehaviorSubject, map, Observable, switchMap, of } from 'rxjs';
import { ResponseApi } from '../interface/response';
import { UserProgressModel } from '../models/review-job.model';
import { ResponsePaganation } from '../interface/response-paganation';
import { UserModel } from '../models/user.model';
import { UnitProgressModel } from '../models/review-job.model';

@Injectable({
  providedIn: 'root',
})
export class ReviewChildJobService {
  private refreshTrigger$ = new BehaviorSubject<void>(undefined); // hoặc new Subject<void>()

  apiUrl: string;

  constructor(private http: HttpClient) {
    this.apiUrl = `${environment.SERVICE_API}`;
  }

  triggerRefresh() {
    this.refreshTrigger$.next();
  }

  onRefresh(
    taskid: string,
    startDate: string,
    endDate: string,
    listUserId: string[],
    numberPage: string
  ): Observable<ResponsePaganation<UserProgressModel>> {
    return this.refreshTrigger$.pipe(
      switchMap(() =>
        this.getReviewByUser(taskid, startDate, endDate, listUserId, numberPage)
      )
    );
  }

  getAssignedUnits(taskId: string): Observable<UnitProgressModel[]> {
    const url = `${this.apiUrl}progress/review-unit/${taskId}`;
    
    return this.http.get<ResponseApi<UnitProgressModel[]>>(url).pipe(
      map((res) => {
        if (!res.success) throw new Error(res.message);
        return res.data.map((unit: any) => ({
          unitId: unit.unitId,
          unitName: unit.unitName,
          leaderFullName: unit.leaderFullName,
          userId: unit.userId, // userId của trưởng phòng
          scheduledProgresses: unit.scheduledProgresses.map((schedule: any) => ({
            periodIndex: schedule.periodIndex,
            periodStartDate: schedule.periodStartDate,
            periodEndDate: schedule.periodEndDate,
            progresses: schedule.progresses,
            status: schedule.status,
            date: schedule.date
          }))
        }));
      })
    );
  }

  onRefreshUnits(
    taskid: string,
    startDate: string,
    endDate: string,
    listUnitIds: string[],
    numberPage: string
  ): Observable<ResponsePaganation<UnitProgressModel>> {
    return this.refreshTrigger$.pipe(
      switchMap(() =>
        this.getReviewByUnits(taskid, startDate, endDate, listUnitIds, numberPage)
      )
    );
  }

  getReviewByUnits(
    taskid: string,
    _startDate: string,
    _endDate: string,
    listUnitIds: string[],
    _numberPage: string
  ): Observable<ResponsePaganation<UnitProgressModel>> {
    const url = `${this.apiUrl}progress/review-unit/${taskid}`;
    
    // Filter by specific units if provided
    let params = new HttpParams();
    if (listUnitIds && listUnitIds.length > 0) {
      listUnitIds.forEach(unitId => {
        params = params.append('unitId', unitId);
      });
    }

    return this.http.get<ResponseApi<UnitProgressModel[]>>(url, { params }).pipe(
      map((res) => {
        if (!res.success) throw new Error(res.message);
        
        let items = res.data ?? [];
        
        // Filter by selected units if needed
        if (listUnitIds && listUnitIds.length > 0) {
          items = items.filter(unit => 
            listUnitIds.includes(unit.unitId.toString())
          );
        }

        const pageSize = items.length || 10;
        const totalItems = items.length;
        
        return {
          currentPage: 1,
          totalPages: 1,
          pageSize,
          totalItems,
          items,
        } as ResponsePaganation<UnitProgressModel>;
      })
    );
  }

  getAssignedUsers(taskId: string): Observable<UserModel[]> {
    const url = `${this.apiUrl}subtask/${taskId}/assigned-users-units`;
    
    return this.http.get<ResponseApi<any[]>>(url).pipe(
      map((res) => {
        if (!res.success) throw new Error(res.message);
        
        // Convert từ API response sang UserModel format
        return (res.data ?? []).map((user: any) => ({
          userId: user.userId,
          username: user.username,
          fullName: user.username, // API chỉ trả về username, dùng làm fullName tạm thời
   
        } as UserModel));
      })
    );
  }

  getReviewByUser(
    taskid: string,
    _startDate: string,
    _endDate: string,
    listUserId: string[],
    _numberPage: string
  ): Observable<ResponsePaganation<UserProgressModel>> {
    // New endpoint: GET /progress/review/{taskId}?assigneeId={id}
    let params = new HttpParams();
    if (listUserId && listUserId.length > 0) {
      // backend currently supports a single assigneeId
      params = params.set('assigneeId', listUserId[0]);
    }

    return this.http
      .get<ResponseApi<UserProgressModel[]>>(
        `${this.apiUrl}progress/review/${taskid}`,
        { params }
      )
      .pipe(
        map((res) => {
          if (!res.success) throw new Error(res.message);
          const items = res.data ?? [];
          // adapt to pagination contract used by UI
          const pageSize = items.length || 10;
          const totalItems = items.length;
          return {
            currentPage: 1,
            totalPages: 1,
            pageSize,
            totalItems,
            items,
          } as ResponsePaganation<UserProgressModel>;
        })
      );
  }

  getTaskAssignmentType(taskId: string): Observable<'user' | 'unit' | 'none'> {
   
    const checkUsers$ = this.getAssignedUsers(taskId);
    const checkUnits$ = this.getAssignedUnits(taskId);

    return checkUsers$.pipe(
      switchMap(users => {
        if (users && users.length > 0) {
          return of('user' as const);
        }
        
        return checkUnits$.pipe(
          map(units => {
            if (units && units.length > 0) {
              return 'unit' as const;
            }
            return 'none' as const;
          })
        );
      })
    );
  }

  // getUsersReview(taskid: string): Observable<UserModel[]> {
  //   const url = `${this.apiUrl}review/task/${taskid}/users`;

  //   return this.http.get<ResponseApi<UserModel[]>>(url).pipe(
  //     map((res) => {
  //       if (!res.success) {
  //         throw new Error(res.message);
  //       } else {
  //         return res.data;
  //       }
  //     })
  //   );
  // }

  // Accept progress (new API)
  acceptProgress(progressId: string): Observable<void> {
    const url = `${this.apiUrl}progress/${progressId}/accept`;
    return this.http.post<ResponseApi<void>>(url, {}).pipe(
      map((res) => {
        if (!res.success) throw new Error(res.message);
        return res.data;
      })
    );
  }

  // If filePath is already a full URL, return it as-is; otherwise call old view-file API
  getFileReport(filePath: string): Observable<string> {
    if (!filePath) return of('');
    if (/^https?:\/\//i.test(filePath)) {
      return of(filePath);
    }
    const url = `${this.apiUrl}review/view-file?filePath=${filePath}`;
    return this.http.get<ResponseApi<string>>(url).pipe(
      map((res) => {
        if (!res.success) {
          throw new Error(res.message);
        } else {
          return res.data;
        }
      })
    );
  }
}