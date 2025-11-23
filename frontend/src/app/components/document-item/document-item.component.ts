import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DocumentService } from '../../service/document.service';
import { DocumentModel } from '../../models/document.model';
import { ToastService } from '../../service/toast.service';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { ActivatedRoute, Router } from '@angular/router';
import { Output, EventEmitter } from '@angular/core';

@Component({
  selector: 'app-file-item',
  standalone: true,
  imports: [CommonModule, NzModalModule, NzDropDownModule, NzIconModule],
  templateUrl: './document-item.component.html',
  styleUrls: ['./document-item.component.css'],
})
export class FileItemComponent {
  @Input() doc!: DocumentModel;
    @Output() preview = new EventEmitter<DocumentModel>();
  isShowModalAssignWork = false;

  constructor(
    private toastService: ToastService,
    private documentService: DocumentService,
    private modal: NzModalService,
    private router: Router,
    private route: ActivatedRoute
  ) {}

  // xoá file
  deleteFile(id: number) {
    this.documentService.deleteDoc(id.toString()).subscribe({
      next: () => {
        this.toastService.Success('Xóa tài liệu thành công !');
        this.documentService.triggerRefresh();
      },
      error: () => this.toastService.Error('Xóa tài liệu thất bại !'),
    });
  }

  // tải file
  downloadFile(fileId: number, fileName: string) {
    this.documentService.downloadFile(fileId); // hàm trong service
    this.toastService.Success(`Đang tải tài liệu: ${fileName}`);
  }

  // convert ngày
  convertDate(uploadedAt: string): string {
    const date = new Date(uploadedAt);
    return `${date.getDate().toString().padStart(2, '0')}/${(date.getMonth() + 1)
      .toString()
      .padStart(2, '0')}/${date.getFullYear()}`;
  }

  toggleShowAssignWork(): void{
    this.router.navigate(['/ai-agent'], {
      queryParams:{
        fileId: this.doc.fileId,
        fileName: this.doc.fileName
      }
    });
  }

  showConfirm(id: number): void {
    this.modal.confirm({
      nzTitle: '<i>Bạn chắc chắn hoàn thành giao việc?</i>',
      nzOnOk: () => this.deleteFile(id),
    });
  }

  navigateTo() {
     this.preview.emit(this.doc);
  }
}
