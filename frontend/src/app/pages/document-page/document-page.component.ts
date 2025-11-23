import { DocumentService } from '../../service/document.service';
import { Component, DestroyRef, OnInit, inject,  ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FileItemComponent } from '../../components/document-item/document-item.component';
import { LoadingComponent } from '../../components/loading/loading.component';
import { PageEmptyComponent } from '../../components/page-empty/page-empty.component';
import { DocumentModel } from '../../models/document.model';
import { ToastService } from '../../service/toast.service';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { ModalViewFileComponent } from '../../components/modal-view-file/modal-view-file.component';


@Component({
  selector: 'app-document-page',
  standalone: true,
  imports: [
    CommonModule,
    FileItemComponent,
    LoadingComponent,
    PageEmptyComponent,
    NzButtonModule,
    ModalViewFileComponent
],
  templateUrl: './document-page.component.html',
  styleUrl: './document-page.component.css',
})
export class DocumentPageComponent implements OnInit {
  documents: DocumentModel[] = [];
  isLoading = true;
  isLoadingButton = false;
  private destroyRef = inject(DestroyRef);

  @ViewChild(ModalViewFileComponent)
  modalViewFileRef!: ModalViewFileComponent;

  constructor(
    private toastService: ToastService,
    private documentService: DocumentService
  ) {}

  ngOnInit(): void {
    this.documentService.documents$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (doc) => {
          setTimeout(() => {
            this.isLoading = false;
            this.documents = doc;
          }, 500);
        },
        error: (err) => {
          this.toastService.Warning(err.message ?? 'Lấy dữ liệu thất bại !');
          this.documents = [];
          this.isLoading = false;
        },
      });
  }

  //---- file -----
  onFileSelected(event: Event) {
    this.isLoadingButton = true;

    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      const file = input.files[0];
      const formData = new FormData();
      formData.append('file', file);
      this.documentService.uploadDoc(formData).subscribe({
        next: () => {
          this.toastService.Success('Tải tài liệu lên thành công');
          this.isLoadingButton = false;

          this.documentService.triggerRefresh();
        },
        error: () => {
          this.isLoadingButton = false;

          this.toastService.Error('Tải tài liệu lên thất bại');
        },
      });

      // console.log('Selected file:', file);
    }
  }

  previewFile(doc: DocumentModel) {
    if (!doc.filePath) return;
    this.modalViewFileRef.fileUrl = doc.filePath;
    this.modalViewFileRef.fileName = doc.fileName;
    this.modalViewFileRef.showModal();
  }
}
