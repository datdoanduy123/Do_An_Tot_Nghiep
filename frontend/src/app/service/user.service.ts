import { Injectable } from '@angular/core';
import { environment } from '../environment/environment';
import { ChangePasswordRequest, ChangePasswordResponse } from '../models/user.model';
import { Observable } from 'rxjs';
import { ResponseApi } from '../interface/response';
import { HttpClient } from '@angular/common/http';

@Injectable({
  providedIn: 'root'
})
export class UserService {
  private url = `${environment.SERVICE_API}`;
  constructor(private httpClient: HttpClient) {
  }
  changePassword(request: ChangePasswordRequest): Observable<ResponseApi<null>> {
  return this.httpClient.post<ResponseApi<null>>(
    `${this.url}user/change-password`,
    request
  );
}

}
