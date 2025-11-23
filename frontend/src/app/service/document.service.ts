import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { BehaviorSubject, map, Observable, switchMap } from 'rxjs';
import { DocumentModel } from '../models/document.model';
import { environment } from '../environment/environment';
import { ResponseApi } from '../interface/response';
import { ResponsePaganation } from '../interface/response-paganation';
import { ToastService } from './toast.service';

@Injectable({ providedIn: 'root' })
export class DocumentService {
  apiUrl: string;
  private refreshTrigger$ = new BehaviorSubject<void>(undefined);
  constructor(private http: HttpClient, private toastService: ToastService) {
    this.apiUrl = `${environment.SERVICE_API}`;
  }

  // Dữ liệu sẽ tự động gọi lại khi trigger thay đổi
  get documents$(): Observable<DocumentModel[]> {
    return this.refreshTrigger$.pipe(switchMap(() => this.getAllDoc()));
  }

  getAllDoc(): Observable<DocumentModel[]> {
  const url = `${this.apiUrl}file/user`;
  return this.http.get<ResponseApi<ResponsePaganation<DocumentModel>>>(url).pipe(
    map((res) => {
      if (!res.success) throw Error(res.message);
      return res.data.items;
    })
  );
}
  uploadDoc(formData: FormData): Observable<DocumentModel> {
    const url = `${this.apiUrl}file/upload`;

    return this.http.post<ResponseApi<DocumentModel>>(url, formData).pipe(
      map((res) => {
        if (!res.success) throw Error(res.message);

        return res.data;
      })
    );
  }

  deleteDoc(id: string): Observable<ResponseApi> {
    const url = `${this.apiUrl}file/delete/${id}`;

    return this.http.delete<ResponseApi>(url).pipe(
      map((res) => {
        if (!res.success) throw Error(res.message);

        return res.data;
      })
    );
  }

  downloadFile(fileId: number): void {
  const url = `${this.apiUrl}file/download/${fileId}`;
  this.http.get(url, { responseType: 'blob', observe: 'response' }).subscribe({
    next: (res) => {
      const contentDisposition = res.headers.get('content-disposition') || '';
      // lấy filename* nếu có
      let match = /filename\*?=(?:UTF-8'')?([^;]+)/i.exec(contentDisposition);
      let filename = match && match[1] ? decodeURIComponent(match[1].trim()) : `file_${fileId}`;

      const blob = new Blob([res.body as BlobPart]);
      const link = document.createElement('a');
      link.href = window.URL.createObjectURL(blob);
      link.download = filename; // ở đây filename đã có đuôi .jpg, .pdf, .docx…
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(link.href);
    },
    error: () => {
      this.toastService.Error('Tải file thất bại!');
    },
  });
}
  triggerRefresh(): void {
    this.refreshTrigger$.next();
  }


}
