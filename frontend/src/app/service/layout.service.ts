import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class LayoutService {
  private _isMobile$ = new BehaviorSubject<boolean>(false);
  private _isSidebarOpen$ = new BehaviorSubject<boolean>(true);

  isMobile$ = this._isMobile$.asObservable();
  isSidebarOpen$ = this._isSidebarOpen$.asObservable();

  setMobile(val: boolean) {
    this._isMobile$.next(val);
  }

  setSidebarOpen(val: boolean) {
    this._isSidebarOpen$.next(val);
  }

  toggleSidebar() {
    this._isSidebarOpen$.next(!this._isSidebarOpen$.value);
  }
}