import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import departments from '../../constants/constant';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-select-unit',
  imports: [NzSelectModule, FormsModule, CommonModule],
  templateUrl: './select-unit.component.html',
  styleUrl: './select-unit.component.css',
})
export class SelectUnitComponent implements OnInit {
  @Input() unit: string[] | null = null;
  @Output() selectedUnit = new EventEmitter<Map<string, any>>();
  selectedPhongBan = '';
  selectedBo: string = '';
  filteredPhongBans: string[] = [];

  departments = departments;

  ngOnInit(): void {
    if (this.unit != null) {
      this.selectedBo = this.unit[0];
      this.onBoChange();
      this.selectedPhongBan = this.unit[1];
    }
    // console.log(this.unit.get('Donvi') ?? '');
  }
  onBoChange() {
    const selected = this.departments.find((d) => d.bo === this.selectedBo);
    this.filteredPhongBans = selected ? selected.phongBans : [];
    this.selectedPhongBan = ''; // reset phòng ban
  }
  onUnitChange() {
    const map = new Map<string, any>();
    map.set('Donvi', this.selectedBo);
    map.set('Phongban', this.selectedPhongBan);
    this.selectedUnit.emit(map);
  }
}
