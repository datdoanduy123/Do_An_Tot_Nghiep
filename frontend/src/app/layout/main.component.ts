import { Component, OnInit } from '@angular/core';
import { ToastComponent } from '../components/toast/toast.component';
import { HeaderComponent } from './header/header.component';
import { SidebarComponent } from './sidebar/sidebar.component';
import { SHARED_LIBS } from './main-sharedLib';
import { NzResultModule } from 'ng-zorro-antd/result';
import { LayoutService } from '../service/layout.service';
import { BottomNavigationComponent } from '../components/bottom-navigation/bottom-navigation.component';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [
    ...SHARED_LIBS,
    ToastComponent,
    HeaderComponent,
    SidebarComponent,
    NzResultModule,
    BottomNavigationComponent,
  ],
  templateUrl: './main.component.html',
  styleUrl: './main.component.css',
})
export class MainComponent implements OnInit {
  isMobile: boolean = false;
  isSidebarOpen: boolean = true;

  constructor(private layoutService: LayoutService) {}

  ngOnInit(): void {
    const detect = () =>
      /iPhone|iPad|iPod|Android|webOS|BlackBerry|Windows Phone/i.test(
        navigator.userAgent
      ) || window.innerWidth < 768;

    this.isMobile = detect();
    this.layoutService.setMobile(this.isMobile);

    // Default close sidebar on mobile, open on desktop
    this.layoutService.setSidebarOpen(!this.isMobile);
    this.isSidebarOpen = !this.isMobile;

    window.addEventListener('resize', () => {
      const m = detect();
      this.isMobile = m;
      this.layoutService.setMobile(m);
      if (m) {
        this.layoutService.setSidebarOpen(false);
        this.isSidebarOpen = false;
      } else {
        this.layoutService.setSidebarOpen(true);
        this.isSidebarOpen = true;
      }
    });

    // Subscribe to layout service
    this.layoutService.isMobile$.subscribe(mobile => {
      this.isMobile = mobile;
    });

    this.layoutService.isSidebarOpen$.subscribe(open => {
      this.isSidebarOpen = open;
    });
  }

  closeSidebarFromOverlay() {
    this.layoutService.setSidebarOpen(false);
  }
}