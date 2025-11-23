import { Component, EventEmitter, OnInit, Output } from '@angular/core';

import { NzSelectModule } from 'ng-zorro-antd/select';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { UnitModel } from '../../models/unit.model';
import { ReviewOriginalJobService } from '../../service/review-original-job.service';

@Component({
  selector: 'app-select-multi-unit',
  imports: [NzSelectModule, FormsModule, CommonModule],
  templateUrl: './select-multi-unit.component.html',
  styleUrl: './select-multi-unit.component.css',
})
export class SelectMultiUnitComponent implements OnInit {
  // @Input() units: UnitModel[] | null = null;
  @Output() selectedUnit = new EventEmitter<string[]>();
  selectedPhongBan = '';
  units: UnitModel[] = [];
  filteredPhongBans: string[] = [];
  listOfSelectedBo: string[] = [];
  listOfSelectedPhongBan: string[] = [];
  // departments = departments;
  constructor(private reviewOriginalJobService: ReviewOriginalJobService) {}
  ngOnInit(): void {
    this.reviewOriginalJobService.getUnitsReview().subscribe({
      next: (data) => {
        this.units = data;
      },
      error: (err) => {},
    });
    // if (this.unit != null) {
    // this.selectedBo = this.unit[0];
    // this.onBoChange();
    // this.selectedPhongBan = this.unit[1];
    // }
    // console.log(this.unit.get('Donvi') ?? '');
  }
  onBoChange() {
    this.selectedUnit.emit(this.listOfSelectedBo);
    // this.filteredPhongBans = [];
    // if (this.listOfSelectedBo.length == 0) {
    //   this.onUnitChange();
    // }
    // const phongBanSet = new Set<string>();
    // for (const bo of this.listOfSelectedBo) {
    //   const selected = this.departments.find((d) => d.bo === bo);
    //   if (selected?.phongBans) {
    //     selected.phongBans.forEach((pb) => phongBanSet.add(pb));
    //   }
    // }
    // this.filteredPhongBans = Array.from(phongBanSet);
    // this.onUnitChange();
  }
  // onUnitChange() {
  //   const map = new Map<string, any>();
  //   if (this.listOfSelectedBo.length === 0) {
  //     this.selectedUnit.emit(map);
  //     return;
  //   }

  //   map.set('Donvi', this.listOfSelectedBo);
  //   map.set('Phongban', this.listOfSelectedPhongBan);
  //   this.selectedUnit.emit(map);
  // }
}
