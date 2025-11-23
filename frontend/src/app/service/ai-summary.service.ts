// src/app/service/ai-summary.service.ts
import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../environment/environment';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root',
})
export class AiSummaryService {
  apiUrl: string;

  constructor(private http: HttpClient) {
    this.apiUrl = `${environment.SERVICE_API}`;
  }

  // API tổng hợp báo cáo bằng AI
  generateSummaryReport(taskId: string): Observable<Blob> {
    const url = `${this.apiUrl}chat/${taskId}/summary`;
    
    return this.http.post(url, {}, {
      responseType: 'blob', // Quan trọng: nhận response dạng blob để tải file
      observe: 'body'
    });
  }

  // Hàm helper để tải file
  downloadSummaryFile(taskId: string, fileName?: string): Observable<void> {
    return new Observable(observer => {
      this.generateSummaryReport(taskId).subscribe({
        next: (blob) => {
          // Tạo URL từ blob
          const url = window.URL.createObjectURL(blob);
          
          // Tạo element link để download
          const link = document.createElement('a');
          link.href = url;
          
          // Đặt tên file - sử dụng tên từ parameter hoặc tên mặc định
          const defaultFileName = `Báo_cáo_tổng_hợp_${taskId}_${new Date().toISOString().slice(0, 10)}.pdf`;
          link.download = fileName || defaultFileName;
          
          // Trigger download
          document.body.appendChild(link);
          link.click();
          
          // Cleanup
          document.body.removeChild(link);
          window.URL.revokeObjectURL(url);
          
          observer.next();
          observer.complete();
        },
        error: (error) => {
          observer.error(error);
        }
      });
    });
  }
}