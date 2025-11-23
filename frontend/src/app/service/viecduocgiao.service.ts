import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ViecduocgiaoModel } from '../models/viecduocgiao.model';
import { environment } from '../environment/environment';
import { ResponseApi } from '../interface/response';
import { Metadata, ResponsePaganation } from '../interface/response-paganation';

@Injectable({ providedIn: 'root' })
export class ViecduocgiaoService {
  apiUrl: string;
  constructor(private http: HttpClient) {
    this.apiUrl = `${environment.SERVICE_API}`;
  }

  getAllData(currentPage: string): Observable<ResponsePaganation<ViecduocgiaoModel>> {
  const url = `${this.apiUrl}subtask/assigned?page=${currentPage}&size=10`;

  return this.http.get<ResponseApi<any>>(url).pipe(
    map((res) => {
      if (!res.success) {
        throw Error(res.message);
      }

      const d = res.data;
      const md: Metadata = d?.metaData ?? {
        pageIndex: 1,
        totalPages: 1,
        totalItems: 0,
        currentItems: 0,
        hasPrevious: false,
        hasNext: false,
      };

      const items = (d?.items ?? []).map((item: any) => ({
        ...item,
        assignedUsers: item.assignedUsers ?? [],
        assignedUnits: item.assignedUnits ?? []
      }));

      const mapped: ResponsePaganation<ViecduocgiaoModel> = {
        items: d?.items ?? [],
        currentPage: md.pageIndex,
        totalPages: md.totalPages,
        pageSize: md.currentItems,
        totalItems: md.totalItems,
        // metaData: md, // ✅ gắn metaData đầy đủ
      };

      return mapped;
    })
  );
}

}