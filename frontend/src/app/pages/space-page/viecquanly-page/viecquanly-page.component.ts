import { ToastService } from './../../../service/toast.service';
import { ViecquanlyItemComponent } from './../../../components/viecquanly-item/viecquanly-item.component';
import { Component, inject, OnInit, DestroyRef } from '@angular/core';
import { LoadingComponent } from '../../../components/loading/loading.component';
import { PageEmptyComponent } from '../../../components/page-empty/page-empty.component';
import { ViecQuanlyService } from '../../../service/viecquanly.service';
import { ViecquanlyModel } from '../../../models/viecquanly.model';
import { SHARED_LIBS } from './viecquanly-sharedLib';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { ActivatedRoute, Router } from '@angular/router';
import { Location } from '@angular/common';
import { PaginationHelper, PaginationState } from '../../../utils/pagination-helper';
import { debounceTime, distinctUntilChanged, Subject } from 'rxjs';

@Component({
  selector: 'app-viecquanly-page',
  standalone: true,
  imports: [
    ...SHARED_LIBS,
    LoadingComponent,
    PageEmptyComponent,
    ViecquanlyItemComponent,
    NzPaginationModule,
    NzIconModule,
    NzInputModule,
  ],
  templateUrl: './viecquanly-page.component.html',
  styleUrl: './viecquanly-page.component.css',
})
export class ViecquanlyPageComponent implements OnInit {
  isLoading = true;
  listViecquanly: ViecquanlyModel[] = [];

  textSearch = '';
  private searchSubject = new Subject<string>();

  pagination!: PaginationHelper;
  paginationState: PaginationState = {
    currentPage: 1,
    pageSize: 10,
    totalItems: 0,
    totalPages: 0,
  };
  private updatingFromRouter = false;

  private destroyRef = inject(DestroyRef);

  constructor(
    private toastService: ToastService,
    private viecQuanlyService: ViecQuanlyService,
    private router: Router,
    private route: ActivatedRoute,
    private location: Location
  ) {}

  ngOnInit(): void {
    // lắng nghe query param (page)
    this.route.queryParamMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((params) => {
        const page = Number(params.get('page')) || 1;
        this.updatingFromRouter = true;
        this.paginationState.currentPage = page;
        this.loadData(page, this.textSearch);
        setTimeout(() => (this.updatingFromRouter = false), 0);
      });

    // debounce input search
    this.searchSubject
      .pipe(debounceTime(400), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe((keyword) => {
        this.textSearch = keyword;
        this.loadData(1, keyword);
      });
  }

  onPageChange(page: number) {
    if (this.updatingFromRouter) return;
    if (page === this.paginationState.currentPage) return;

    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page },
      queryParamsHandling: 'merge',
    });
  }

  loadData(page: number, keyword: string = '') {
    this.isLoading = true;

    this.viecQuanlyService
      .getAllData(page.toString(), keyword)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.isLoading = false;
          this.listViecquanly = res.items;

          this.paginationState.currentPage = res.currentPage;
          this.paginationState.pageSize = res.pageSize;
          this.paginationState.totalItems = res.totalItems;
          this.paginationState.totalPages = res.totalPages;
        },
        error: (err) => {
          this.isLoading = false;
          this.toastService.Warning(err.message ?? 'Lấy dữ liệu thất bại!');
        },
      });
  }

  // ⌨️ gọi khi người dùng nhập ô search
  onSearchChange(keyword: string) {
    this.searchSubject.next(keyword.trim());
  }
}
