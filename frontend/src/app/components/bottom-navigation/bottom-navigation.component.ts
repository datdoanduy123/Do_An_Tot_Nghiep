import { Component, OnInit } from '@angular/core';
import { Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs';
import { CommonModule } from '@angular/common';
import { LayoutService } from '../../service/layout.service';

@Component({
  selector: 'app-bottom-navigation',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './bottom-navigation.component.html',
  styleUrl: './bottom-navigation.component.css'
})
export class BottomNavigationComponent implements OnInit {
  currentRoute: string = '';
  isMobile = false;

  navigationItems = [
    {
      route: '/document',
      icon: 'fa-solid fa-chart-bar',
      label: 'Tổng hợp',
      key: 'summary'
    },
    {
      route: '/viecduocgiao',
      icon: 'fa-solid fa-list',
      label: 'Việc được giao',
      key: 'assigned'
    },
    {
      route: '/assignWork',
      icon: 'fa-solid fa-user-plus',
      label: 'Giao việc',
      key: 'assign'
    },
    {
      route: '/viecquanly',
      icon: 'fa-solid fa-briefcase',
      label: 'Việc quản lý',
      key: 'manage'
    }
  ];

  constructor(
    private router: Router,
    private layoutService: LayoutService
  ) {
    this.router.events
      .pipe(filter((event) => event instanceof NavigationEnd))
      .subscribe((event: any) => {
        this.currentRoute = event.url;
      });
  }

  ngOnInit() {
    this.currentRoute = this.router.url;
    
    this.layoutService.isMobile$.subscribe(mobile => {
      this.isMobile = mobile;
    });
  }

  navigateTo(route: string) {
    this.router.navigate([route]);
  }

  isActive(route: string): boolean {
    if (route === '/document') {
      return this.currentRoute === '/' || this.currentRoute === '/document';
    }
    return this.currentRoute.startsWith(route);
  }

  showSummaryInfo() {
    // Có thể thay thế bằng modal tổng hợp hoặc navigate đến trang tổng hợp
    this.navigateTo('/document');
  }
}