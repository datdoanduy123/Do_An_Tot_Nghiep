import { WeekDay } from '../../constants/constant';
import { CommonModule } from '@angular/common';
import {
  Component,
  EventEmitter,
  Output,
  OnInit,
  Input,
  SimpleChanges,
} from '@angular/core';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { FormsModule } from '@angular/forms';

import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { FrequencyView } from '../../models/frequency-view.model';

@Component({
  selector: 'app-select-repeat-schedule',
  standalone: true,
  imports: [NzTabsModule, CommonModule, FormsModule, NzDropDownModule],
  templateUrl: './select-repeat-schedule.component.html',
  styleUrl: './select-repeat-schedule.component.css',
})
export class SelectRepeatScheduleComponent implements OnInit {
  @Output() closeDropdown = new EventEmitter<any>();
  @Output() frequency = new EventEmitter<any>();
  @Input() isEdit: boolean = true;
  @Input() initValue?: FrequencyView | null;
  daysInWeek = [
    { title: 'T2', value: 'Thứ hai', index: WeekDay.Monday },
    { title: 'T3', value: 'Thứ ba', index: WeekDay.Tuesday },
    { title: 'T4', value: 'Thứ tư', index: WeekDay.Wednesday },
    { title: 'T5', value: 'Thứ năm', index: WeekDay.Thursday },
    { title: 'T6', value: 'Thứ sáu', index: WeekDay.Friday },
    { title: 'T7', value: 'Thứ bảy', index: WeekDay.Saturday },
    { title: 'CN', value: 'Chủ nhật', index: WeekDay.Sunday },
  ];

  daysInMonth = 1;

  selectedDaysInWeekIndex: WeekDay[] = [];
  selectedDaysInMonthIndex: number | null = null;

  repeatDay = 1;
  repeatWeek = 1;
  repeatMonth = 1;
  selectedTabIndex = 0;

  ngOnInit() {
    // console.log(this.isEdit);
    this.selectedTabIndex = 0;
    this.repeatDay = 1;
    this.repeatWeek = 1;
    this.repeatMonth = 1;
    // console.log(this.initValue);
    this.applyInitValue();
  }
  ngOnChanges(changes: SimpleChanges): void {
    if (changes['initValue'] && changes['initValue'].currentValue) {
      this.applyInitValue();
    }
  }
  private applyInitValue() {
    if (!this.initValue) return;

    const { frequency_type, interval_value, daysInWeek, daysInMonth } =
      this.initValue;

    switch (frequency_type) {
      case 'ngày':
        this.selectedTabIndex = 0;
        this.repeatDay = interval_value;
        break;

      case 'tuần':
        this.selectedTabIndex = 1;
        this.repeatWeek = interval_value;
        this.selectedDaysInWeekIndex = daysInWeek as WeekDay[];
        break;

      case 'tháng':
        this.selectedTabIndex = 2;
        this.repeatMonth = interval_value;
        if (daysInMonth?.length > 0) {
          this.daysInMonth = daysInMonth[0];
        }
        break;
    }
  }

  onTabChange(index: number) {
    if (this.isEdit) {
      this.selectedTabIndex = index; // Only update if isEdit is true
    }
    // No else needed; nzSelectedIndex binding prevents tab change
  }

  getValueTabSelected(): FrequencyView | null {
    switch (this.selectedTabIndex) {
      case 0:
        return {
          frequency_type: 'daily',
          interval_value: this.repeatDay,
          daysInWeek: [],
          daysInMonth: [],
        };

      case 1:
        return {
          frequency_type: 'weekly',
          interval_value: this.repeatWeek,
          daysInWeek: this.selectedDaysInWeekIndex,
          daysInMonth: [],
        };

      case 2:
        return {
          frequency_type: 'monthly',
          interval_value: this.repeatMonth,
          daysInWeek: [],
          daysInMonth: [this.daysInMonth],
        };

      default:
        return null;
    }
  }

  getSelectedDaysTextArray(): string[] {
    return this.selectedDaysInWeekIndex
      .map(
        (dayIndex) => this.daysInWeek.find((d) => d.index === dayIndex)?.value
      )
      .filter(Boolean) as string[];
  }

  selectDaysInWeek(index: WeekDay) {
    const i = this.selectedDaysInWeekIndex.indexOf(index);
    if (i === -1) {
      this.selectedDaysInWeekIndex.push(index);
    } else {
      this.selectedDaysInWeekIndex.splice(i, 1);
    }
  }

  selectDaysInMonth(index: number) {
    this.selectedDaysInMonthIndex = index;
  }

  get selectedDaysInWeekValues() {
    return this.selectedDaysInWeekIndex
      .map(
        (dayIndex) => this.daysInWeek.find((d) => d.index === dayIndex)?.value
      )
      .filter(Boolean); // lọc undefined nếu không khớp
  }

  getSelectedDaysText(): string {
    return this.getSelectedDaysTextArray().join(', ');
  }

  resetSelections() {
    this.selectedDaysInWeekIndex = [];
    this.selectedDaysInMonthIndex = null;
  }

  submit() {
    const FrequencyMap = this.getValueTabSelected();
    this.closeDropdown.emit(true);
    this.frequency.emit(FrequencyMap);
    // console.log(FrequencyMap);
  }
}
