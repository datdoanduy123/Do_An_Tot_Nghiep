import {
  Component,
  EventEmitter,
  Input,
  Output,
  ViewChild,
} from '@angular/core';
import { Misson } from '../../../models/misson.model';
import { CommonModule, Location } from '@angular/common';
import { NzCollapseModule } from 'ng-zorro-antd/collapse';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ToastService } from '../../../service/toast.service';
import { NzModalService, NzModalModule } from 'ng-zorro-antd/modal';
import { TaskViewModel } from '../../../models/task-view.model';

@Component({
  selector: 'app-assign-job-page',
  standalone: true,
  imports: [CommonModule, NzCollapseModule, FormsModule, NzModalModule],

  templateUrl: './assign-job-page.component.html',
  styleUrl: './assign-job-page.component.css',
})
export class AssignJobPageComponent {
  @Input() listTask!: TaskViewModel[];
  @Output() closeModal = new EventEmitter<any>();
  title: string = 'Project';
  panels: { active: boolean; name: string; disabled: boolean }[] = [];
  constructor(
    private modal: NzModalService,
    private router: Router,
    private toastService: ToastService,
    private location: Location
  ) {
    const state = this.router.getCurrentNavigation()?.extras.state;
    if (state) {
      this.title = state['title'];
    }
  }
  ngOnInit(): void {
    this.listTask = [];
    this.panels = this.listTask.map((item, index) => ({
      active: true, // chỉ mở panel đầu tiên
      name: item.title,
      disabled: false,
    }));
  }

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      const file = input.files[0];
      const formData = new FormData();
      formData.append('file', file);

      console.log('Selected file:', file);
    }
  }

  showConfirm(): void {
    this.modal.confirm({
      nzTitle: '<i>Bạn chắc chắn hoàn thành giao việc?</i>',
      // nzContent: '<b>Some descriptions</b>',
      nzOnOk: () => {
        this.toastService.Success('Cập nhật thành công !');
      },
      nzOkText: 'Xác nhận',
      nzCancelText: 'Hủy bỏ',
    });
  }

  goBack() {
    this.location.back();
  }
}
export interface listMisson {
  title: string;
  misson: Misson[];
}
