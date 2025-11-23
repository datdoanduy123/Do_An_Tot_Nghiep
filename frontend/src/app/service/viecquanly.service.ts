import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { map, Observable, BehaviorSubject, switchMap } from 'rxjs';
import { ViecquanlyModel } from '../models/viecquanly.model';
import { environment } from '../environment/environment';
import { ResponseApi } from '../interface/response';
import { ResponsePaganation } from '../interface/response-paganation';

@Injectable({ providedIn: 'root' })
export class ViecQuanlyService {
  private refreshTrigger$ = new BehaviorSubject<void>(undefined);

  apiUrl: string;

  constructor(private http: HttpClient) {
    this.apiUrl = `${environment.SERVICE_API}`;
  }

  triggerRefresh() {
    this.refreshTrigger$.next();
  }

  onRefresh(currentPage: string, keyword: string = ''): Observable<ResponsePaganation<ViecquanlyModel>> {
    return this.refreshTrigger$.pipe(
      switchMap(() => this.getAllData(currentPage, keyword))
    );
  }

  getAllData(currentPage: string, keyword: string = ''): Observable<ResponsePaganation<ViecquanlyModel>> {
   const params = {
    Page: currentPage.toString(),
    Size: '10',
    Key: keyword.trim(),
  };
    const url = `${this.apiUrl}task?`;
    return this.http.get<ResponseApi<any>>(url, {params}).pipe(
      map((res) => {
        if (!res.success) {
          throw Error(res.message);
        } else {
          const d = res.data;
          const md = d?.metaData ?? {};
          const mapped: ResponsePaganation<ViecquanlyModel> = {
            items: d?.items ?? [],
            currentPage: md.pageIndex ?? 1,
            totalPages: md.totalPages ?? 1,
            pageSize: md.currentItems ?? 10,
            totalItems: md.totalItems ?? 0
          };
          return mapped;
        }
      })
    );
  }

    editViecQuanly(taskId: string, obj: any): Observable<any> {
    const url = `${this.apiUrl}task/${taskId}`;
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
}
