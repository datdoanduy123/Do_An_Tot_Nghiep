import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';

@Component({
  selector: 'app-modal-desciption-job',
  imports: [
    NzDropDownModule,
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzInputModule,
  ],
  templateUrl: './modal-desciption-job.component.html',
  styleUrl: './modal-desciption-job.component.css',
})
export class ModalDesciptionJobComponent {
  @Input() isEdit: boolean = true;
  @Input() description!: string;
  @Output() descriptionChanged = new EventEmitter<string>();
  dropdownVisible = false;
  onDropdownVisibilityChange(visible: boolean) {
    if (!visible) {
      // Dropdown  đóng
      this.emitContent();
    }
  }
  emitContent() {
    this.descriptionChanged.emit(this.description);
  }
  toggleDropdown() {
    this.dropdownVisible = !this.dropdownVisible;
  }
}
