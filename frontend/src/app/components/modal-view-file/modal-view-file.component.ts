import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
// import { NzModalRef } from 'ng-zorro-antd/modal';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { PdfViewerModule } from 'ng2-pdf-viewer';
import { NgxDocViewerModule } from 'ngx-doc-viewer';

@Component({
  selector: 'app-modal-view-file',
  templateUrl: './modal-view-file.component.html',
  styleUrl: './modal-view-file.component.css',
  standalone: true,
  imports: [
    CommonModule,
    NzModalModule,
    NzButtonModule,
    PdfViewerModule,
    NgxDocViewerModule,
  ],
})
export class ModalViewFileComponent {
  pdfSrc = '';
  doc =
    'http://103.252.72.72:9001/api/v1/buckets/doc/objects/metadata?prefix=Role-userRole.docx&versionID=null';

    @Input() fileUrl: string = '';
  @Input() fileName: string = '';

  isVisible = false;
  safeFileUrl: SafeResourceUrl | null = null;

  constructor(private sanitizer: DomSanitizer) {}

  ngOnInit(): void {
    if (this.fileUrl) {
      const normalized = this.normalizeUrl(this.fileUrl);
      this.safeFileUrl = this.sanitizer.bypassSecurityTrustResourceUrl(
        normalized
      );
    }
  }

  isNullFile(): boolean {
    return this.fileUrl !== '';
  }

  showModal(): void {
    if (!this.fileUrl) return;

    const normalized = this.normalizeUrl(this.fileUrl);

    if (this.isPdf(normalized) || this.isImage(normalized)) {
      this.safeFileUrl = this.sanitizer.bypassSecurityTrustResourceUrl(
        normalized
      );
    } else if (this.isDocxFile(normalized)) {
      this.safeFileUrl = this.getGoogleDocViewerUrl(normalized);
    } else {
      this.safeFileUrl = this.sanitizer.bypassSecurityTrustResourceUrl(
        normalized
      );
    }
    this.isVisible = true;
    document.body.style.overflow = 'hidden';
  }

  handleOk(): void {
    this.isVisible = false;
    document.body.style.overflow = '';
  }

  handleCancel(): void {
    this.isVisible = false;
    document.body.style.overflow = '';
  }

  // --- helpers ---

  private normalizeUrl(url: string): string {
    if (!url) return url;

    // Fix double 'doctask/doctask/'
    url = url.replace('/doctask/doctask/', '/doctask/');

    // Nếu là dạng Minio bọc Cloudinary: '.../doctask/https%3a%2f%2f...'
    const marker = '/doctask/';
    const idx = url.indexOf(marker);
    if (idx > -1) {
      const tail = url.substring(idx + marker.length);
      // Nếu phần tail là 1 URL đã được encode bắt đầu bằng http(s)%3a
      if (/^(https?|HTTPS?)%3a/i.test(tail)) {
        try {
          const decoded = decodeURIComponent(tail);
          if (/^https?:\/\//i.test(decoded)) {
            return decoded; // dùng thẳng URL Cloudinary
          }
        } catch {}
      }
    }

    return url;
  }

  isImage(fileUrl: string): boolean {
    return /\.(png|jpe?g|gif|webp)$/i.test(fileUrl);
  }

  isPdf(file: string): boolean {
    return file.toLowerCase().endsWith('.pdf');
  }

  isDocxFile(file: string): boolean {
    return (
      file.toLowerCase().endsWith('.docx') ||
      file.toLowerCase().endsWith('.doc')
    );
  }

  getGoogleDocViewerUrl(fileUrl: string): SafeResourceUrl {
    const encodedUrl = encodeURIComponent(fileUrl);
    const googleViewer = `https://docs.google.com/gview?url=${encodedUrl}&embedded=true`;
    return this.sanitizer.bypassSecurityTrustResourceUrl(googleViewer);
  }
}
