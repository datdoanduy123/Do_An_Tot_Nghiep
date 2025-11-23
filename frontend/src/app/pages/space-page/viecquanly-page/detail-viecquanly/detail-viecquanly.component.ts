import { ToastService } from './../../../../service/toast.service';
import { Router } from '@angular/router';
import { Component, HostListener, DestroyRef, inject, OnInit, Output, EventEmitter } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Location } from '@angular/common';
import { DetailViecquanlyItemComponent } from '../../../../components/detail-viecquanly-item/detail-viecquanly-item.component';
import { SHARED_LIBS } from '../viecquanly-sharedLib';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DetailViecquanlyService } from '../../../../service/detail-viecquanly.service';
import { DetailViecquanlyModel } from '../../../../models/detail-viecquanly.model';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { LoadingComponent } from '../../../../components/loading/loading.component';
import { PageEmptyComponent } from '../../../../components/page-empty/page-empty.component';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { PaginationHelper, PaginationState } from '../../../../utils/pagination-helper';
import { ResponsePaganation } from '../../../../interface/response-paganation';

@Component({
  selector: 'app-detail-viecquanly',
  imports: [
    ...SHARED_LIBS,
    DetailViecquanlyItemComponent,
    NzIconModule,
    NzInputModule,
    NzPaginationModule,
    LoadingComponent,
    PageEmptyComponent
  ],
  templateUrl: './detail-viecquanly.component.html',
  styleUrl: './detail-viecquanly.component.css',
})
export class DetailViecquanlyComponent implements OnInit {
  @Output() taskDeleted = new EventEmitter<string>();

  taskId!: number;
  openedMoreOptionId: string | null = null;
  isShowModalAssignWork = false;
  isLoading = true;

  listData: DetailViecquanlyModel[] = [];
  filterListDetailViecQuanLy: DetailViecquanlyModel[] = [];

  pagination!: PaginationHelper;
  paginationState: PaginationState = {
    currentPage: 1,
    pageSize: 10,
    totalItems: 0,
    totalPages: 0,
  };

  textSearch = '';
  private destroyRef = inject(DestroyRef);

  constructor(
    private route: ActivatedRoute,
    private location: Location,
    private detailViecquanlyService: DetailViecquanlyService,
    private toastService: ToastService,
    private router: Router
  ) {
    this.pagination = new PaginationHelper(router, location, '/chitiet');
  }

  ngOnInit() {
    // Lấy currentPage từ URL (?page=)
    this.paginationState.currentPage = this.pagination.getCurrentPage();

    this.route.params.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      this.taskId = +params['id'];
      this.loadData(this.paginationState.currentPage);
    });
  }


onTaskDeleted(taskId: string) {
  this.listData = this.listData.filter(task => task.TaskId.toString() !== taskId);
}


  /**Load data */
  loadData(page: number) {
    this.isLoading = true;

    this.detailViecquanlyService
      .getAllData(this.taskId.toString(), page.toString())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.isLoading = false;
          this.listData = res.items;
          this.filterListDetailViecQuanLy = res.items;

          this.paginationState.currentPage = res.currentPage;
          this.paginationState.pageSize = res.pageSize;
          this.paginationState.totalItems = res.totalItems;
          this.paginationState.totalPages = res.totalPages;
        },
        error: (err) => {
          this.toastService.Warning(err.message ?? 'Lấy dữ liệu thất bại!');
          this.isLoading = false;
        },
      });
  }

  //xoa task ngay lap tuc 

  onTaskDelte(TaskId : string){
    this.listData = this.listData.filter(task => task.TaskId.toString() !== TaskId)
    this.taskDeleted.emit(TaskId.toString());

  }
  /** Tìm kiếm */
  onSearchChange(textSearch: string) {
    const keyword = textSearch.toLowerCase().trim();

    if (!keyword) {
      this.listData = this.filterListDetailViecQuanLy;
      return;
    }

    this.listData = this.filterListDetailViecQuanLy.filter((task) =>
      task.Title?.toLowerCase().includes(keyword)
    );
  }

  /**  Chuyển trang */
  onPageChange(newPage: number) {
    this.pagination.onPageChange(newPage, this.paginationState, (page) =>
      this.loadData(page)
    );
  }

  // -------- More options
  onToggleMoreOption(id: string) {
    this.openedMoreOptionId = this.openedMoreOptionId === id ? null : id;
  }

  @HostListener('document:click')
  closeAllMoreOptions() {
    this.openedMoreOptionId = null;
  }

  toggleShowAssignWork() {
    this.isShowModalAssignWork = !this.isShowModalAssignWork;
  }

  goBack() {
    this.location.back();
  }
}