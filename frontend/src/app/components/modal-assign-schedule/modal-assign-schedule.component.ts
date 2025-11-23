import { FrequencyView } from './../../models/frequency-view.model';
import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { SelectRepeatScheduleComponent } from '../select-repeat-schedule/select-repeat-schedule.component';
import { FrequencyTypeMap } from '../../constants/constant';

@Component({
  selector: 'app-modal-assign-schedule',
  standalone: true,
  imports: [
    FormsModule,
    CommonModule,
    NzDropDownModule,
    SelectRepeatScheduleComponent,
  ],
  templateUrl: './modal-assign-schedule.component.html',
  styleUrl: './modal-assign-schedule.component.css',
})
export class ModalAssignScheduleComponent implements OnInit {
  @Input() isEdit: boolean = true;
  @Input() frequency?: FrequencyView | null;
  @Output() frequencyOutput = new EventEmitter<any>();
  dropdownVisible = false;
  frequencySelected: FrequencyView | null = null;

  ngOnInit(): void {
    if (this.frequency != null) {
      this.frequencySelected = this.frequency;
    }
  }
  getfrequency(Frequency: FrequencyView) {
    this.frequencySelected = Frequency;
    this.frequencyOutput.emit(Frequency);
    this.frequencySelected.frequency_type =
      FrequencyTypeMap[
        this.frequencySelected.frequency_type as 'daily' | 'weekly' | 'monthly'
      ];
    this.dropdownVisible = false;
    // console.log(this.frequencySelected);
  }
  toggleDropdown() {
    this.dropdownVisible = !this.dropdownVisible; // Toggle dropdown manually if needed
  }
}
