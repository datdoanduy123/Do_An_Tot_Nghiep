import { Router } from '@angular/router';
import { NzTableQueryParams } from 'ng-zorro-antd/table'; // nếu bạn dùng table
import { Location } from '@angular/common';

export interface PaginationState {
  currentPage: number;
  pageSize: number;
  totalPages?: number;
  totalItems?: number;
}

export class PaginationHelper {
  constructor(
    private router: Router,
    private location: Location,
    private basePath: string // ví dụ: '/viecduocgiao' hoặc '/chitiet/:id'
  ) {}

  
   //Lấy trang hiện tại từ URL

  getCurrentPage(): number {
    const url = new URL(window.location.href);
    const page = url.searchParams.get('page');
    return page ? parseInt(page, 10) : 1;
  }

 
   //Cập nhật URL khi đổi trang
   
  updateUrl(currentPage: number): void {
  this.router.navigate([], {
    queryParams: { page: currentPage },
    queryParamsHandling: 'merge', // giữ nguyên các query khác nếu có
  });
}

 
   // Xử lý khi người dùng chuyển trang
   
  onPageChange(
  newPage: number,
  paginationState: PaginationState,
  fetchCallback?: (page: number) => void // cho tùy chọn, không bắt buộc
) {
  if (paginationState.totalPages && newPage > paginationState.totalPages) return;
  if (newPage < 1) return;

  paginationState.currentPage = newPage;
  this.updateUrl(newPage);

}
}