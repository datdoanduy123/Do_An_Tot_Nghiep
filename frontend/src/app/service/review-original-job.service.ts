import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../environment/environment';
import { map, Observable } from 'rxjs';
import { ResponseApi } from '../interface/response';
import { UnitModel } from '../models/unit.model';
import {
  UnitProgressModel,
  UserProgressModel,
} from '../models/review-job.model';
// LỖI ENDPOINT API
@Injectable({
  providedIn: 'root',
})
export class ReviewOriginalJobService {
  apiUrl: string;
  constructor(private http: HttpClient) {
    this.apiUrl = `${environment.SERVICE_API}`;
  }
  getUnitsReview(): Observable<UnitModel[]> {
    const url = `${this.apiUrl}subs-unit-current`;

    return this.http.get<ResponseApi<UnitModel[]>>(url).pipe(
      map((res) => {
        if (!res.success) {
          throw new Error(res.message);
        } else {
          return res.data;
        }
      })
    );
  }
  getReviewByUnits(
    listUnits: string[],
    taskid: string
  ): Observable<UnitProgressModel[]> {
    const query = listUnits.map((id) => `unitIds=${id}`).join('&');

    const url = `${this.apiUrl}review/by-task-units?taskId=${taskid}&${query}`;

    return this.http.get<ResponseApi<UnitProgressModel[]>>(url).pipe(
      map((res) => {
        if (!res.success) {
          throw new Error(res.message);
        } else {
          return res.data;
        }
      })
    );
  }
  getReviewByUser(taskid: string): Observable<UserProgressModel[]> {
    const url = `${this.apiUrl}review/by-task-frequency?taskId=${taskid}`;

    return this.http.get<ResponseApi<UserProgressModel[]>>(url).pipe(
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
