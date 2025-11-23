import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { DocumentModel } from '../models/document.model';
import { environment } from '../environment/environment';

@Injectable({ providedIn: 'root' })
export class CapnhatiendoRepository {
  apiUrl: string;
  constructor(private http: HttpClient) {
    this.apiUrl = `${environment.SERVICE_API}`;
  }

  capnhatiendo(id: string, formData: FormData): Observable<DocumentModel> {
    const url = `${this.apiUrl}projects/assigned-tasks/${id}/progress`;
    return this.http.put<DocumentModel>(url, formData);
  }
}
