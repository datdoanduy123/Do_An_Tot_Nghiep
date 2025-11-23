import { Component } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import { ToastService } from '../../service/toast.service';
import { SHARED_LIBS } from '../main-sharedLib';
import { LayoutService } from '../../service/layout.service';
import { NzModalRef, NzModalService } from 'ng-zorro-antd/modal';
import { ModalSummarizeReportsComponent } from '../../components/modal-summarize-reports/modal-summarize-reports.component';


@Component({
  selector: 'app-sidebar',
  imports: [...SHARED_LIBS],
  templateUrl: './sidebar.component.html',
  styleUrls: ['./sidebar.component.css'],
})

export class SidebarComponent {
  currentRoute: string = '';
  isSidebarOpen = true;
  isMobile = false;
  isSpaceSubmenuOpen = false;
  isSpaceSubmenuTooltip = false;

  constructor(
    private router: Router,
    private toastService: ToastService,
    private layoutService: LayoutService,
    private modal: NzModalService
  ) {
    this.router.events
      .pipe(filter((event) => event instanceof NavigationEnd))
      .subscribe((event: any) => {
        this.currentRoute = event.url;
      });
  }

  ngOnInit() {
    this.checkScreenSize();
    this.currentRoute = this.router.url;
    document.addEventListener(
      'click',
      this.closeTooltipOnClickOutside.bind(this)
    );
    window.addEventListener('resize', this.checkScreenSize.bind(this));

    this.layoutService.isSidebarOpen$.subscribe((open) => {
      this.isSidebarOpen = open;
    });

    this.layoutService.isMobile$.subscribe((mobile) => {
      this.isMobile = mobile;
    });
  }

  // ------ check route ----------
  get isHomePage(): boolean {
    return this.currentRoute.includes('/home');
  }
  get isSpacePage(): boolean {
    return (
      this.currentRoute.includes('/viecduocgiao') ||
      this.currentRoute.includes('/viecquanly')
    );
  }
  get isViecduocgiaoPage(): boolean {
    return this.currentRoute.includes('/viecduocgiao');
  }
  get isViecquanlyPage(): boolean {
    return this.currentRoute.includes('/viecquanly');
  }
  get isDocumentPage(): boolean {
    return this.currentRoute.includes('/document');
  }
  get isAutomationPage(): boolean {
    return this.currentRoute.includes('/automation');
  }
  get isAIAgentPage(): boolean {
    return this.currentRoute.includes('/AIAgent');
  }
  get isAssignWorkPage(): boolean {
    return this.currentRoute.includes('/assignWork');
  }

  // ------- actions ---------
  toggleSidebar() {
    this.layoutService.toggleSidebar();
  }

  toggleSpaceSubmenuTooltip(event?: MouseEvent) {
    if (event) {
      event.stopPropagation();
    }
    if (!this.isSidebarOpen) {
      this.isSpaceSubmenuTooltip = !this.isSpaceSubmenuTooltip;
    } else {
      this.isSpaceSubmenuOpen = !this.isSpaceSubmenuOpen;
    }
  }

  closeTooltipOnClickOutside() {
    this.isSpaceSubmenuTooltip = false;
  }

  checkScreenSize() {
    const isMobile = window.matchMedia('(max-width: 1300px)').matches;
    if (isMobile) {
      this.layoutService.setSidebarOpen(false);
      this.isSidebarOpen = false;
    } else {
      this.layoutService.setSidebarOpen(true);
      this.isSidebarOpen = true;
    }
  }

  openSummaryModal(): void {
    const modal: NzModalRef = this.modal.create({
      nzTitle: 'Tổng hợp báo cáo',
      nzContent: ModalSummarizeReportsComponent,
      nzFooter: [
        {
          label: 'Đóng',
          onClick: () => modal.destroy(),
        },
      ],
    });
  }

  showInfo() {
    this.toastService.Info('Tính năng này đang phát triển');
  }

  // Mobile specific methods
  navigateAndCloseSidebar(route: string) {
    this.router.navigate([route]);
    if (this.isMobile) {
      this.layoutService.setSidebarOpen(false);
    }
  }

  ngOnDestroy() {
    document.removeEventListener('click', this.closeTooltipOnClickOutside);
    window.removeEventListener('resize', this.checkScreenSize);
  }
}