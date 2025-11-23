import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../environment/environment';
import { map, Observable } from 'rxjs';
import { ResponseApi } from '../interface/response';

@Injectable({
  providedIn: 'root',
})
export class UpdateProgressService {
  apiUrl: string;
  constructor(private http: HttpClient  ) {
    this.apiUrl = `${environment.SERVICE_API}`;
  }
  updateProgress(taskid: string, formData: FormData): Observable<void> {
     
    const url = `${this.apiUrl}progress?taskId=${taskid}`;
    
    return this.http.post<ResponseApi>(url, formData).pipe(
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
