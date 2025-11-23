import { Component, Input, OnInit } from '@angular/core';
import { ViecduocgiaoModel } from '../../models/viecduocgiao.model';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { convertToVietnameseDate } from '../../helper/convertToVNDate';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { ModalUpdateProgressComponent } from '../modal-update-progress/modal-update-progress.component';
import { FrequencyView } from '../../models/frequency-view.model';
import { FrequencyTypeMap } from '../../constants/constant';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { SelectRepeatScheduleComponent } from '../select-repeat-schedule/select-repeat-schedule.component';
import { NzInputModule } from 'ng-zorro-antd/input';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';

@Component({
  selector: 'app-viecdagiao-item',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ModalUpdateProgressComponent,
    NzProgressModule,
    NzDropDownModule,
    NzInputModule,
    SelectRepeatScheduleComponent,
    NzButtonModule,
  ],
  templateUrl: './viecduocgiao-item.component.html',
  styleUrl: './viecduocgiao-item.component.css',
})
export class ViecduocgiaoItemComponent implements OnInit {
  @Input() viecdagiao!: ViecduocgiaoModel;
  @Input() highlight: boolean = false;
  isShowModalDetailProgress = false;
  isShowTooltip = false;
  isOpenPopup = false;
  frequency: FrequencyView | null = null;
  constructor(private router: Router) {}
  ngOnInit(): void {
    if (this.viecdagiao.frequencyType != null) {
      this.frequency = {
        frequency_type:
          FrequencyTypeMap[
            this.viecdagiao.frequencyType as 'daily' | 'weekly' | 'monthly'
          ],
        interval_value: this.viecdagiao.intervalValue,
        daysInWeek: this.viecdagiao.dayOfWeek ?? [],
        daysInMonth: this.viecdagiao.dayOfMonth ?? [],
      };
    }
  }

  get allAssignees(): AssigneeDisplayItem[] {
    const assignees: AssigneeDisplayItem[] = [];
    
    // Thêm users
    if (this.viecdagiao.assignedUsers) {
      this.viecdagiao.assignedUsers.forEach(user => {
        assignees.push({
          type: 'user',
          name: user.fullName,
          icon: '👤'
        });
      });
    }
    
    // Thêm units  
    if (this.viecdagiao.assignedUnits) {
      this.viecdagiao.assignedUnits.forEach(unit => {
        assignees.push({
          type: 'unit',
          name: unit.unitName,
          icon: '🏢',
          org: unit.org
        });
      });
    }
    
    return assignees;
  }

  get hasAssignees(): boolean {
    return this.allAssignees.length > 0;
  }

  onchildChangedShowPopup(isShow: boolean) {
    this.isOpenPopup = isShow;
  }
  togglePopup() {
    this.isOpenPopup = !this.isOpenPopup;
  }
  convertDate(date: string): string {
    return convertToVietnameseDate(date);
  }
  goToDetail() {
    this.router.navigate(['viecduocgiao/chitiet', this.viecdagiao.taskId], {
      state: { description: this.viecdagiao.description },
    });
  }
  //-----
  toggleShowModalDetailProgress() {
    this.isShowModalDetailProgress = !this.isShowModalDetailProgress;
  }
}

interface AssigneeDisplayItem {
  type: 'user' | 'unit';
  name: string;
  icon: string;
  org?: string;
}