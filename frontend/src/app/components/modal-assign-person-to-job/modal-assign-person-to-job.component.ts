import { UnitModel, UnitStructureModel } from './../../models/unit.model';
import { ToastService } from './../../service/toast.service';
import { AssignWorkService } from './../../service/assign-work.service';
import { CommonModule } from '@angular/common';
import {
  Component,
  EventEmitter,
  Input,
  OnInit,
  Output,
  DestroyRef,
  inject,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { UserModel } from '../../models/user.model';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { AssignResult } from '../../models/assign-result.model';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzTreeSelectModule } from 'ng-zorro-antd/tree-select';
@Component({
  selector: 'app-modal-assign-person-to-job',
  imports: [
    FormsModule,
    CommonModule,
    NzDropDownModule,
    NzSelectModule,
    NzIconModule,
    NzSwitchModule,
    NzButtonModule,
    NzTreeSelectModule,
  ],
  templateUrl: './modal-assign-person-to-job.component.html',
  styleUrl: './modal-assign-person-to-job.component.css',
})
export class ModalAssignPersonToJobComponent implements OnInit {
  @Input() isOpen!: boolean;
  @Output() Assigned = new EventEmitter<AssignResult>();
  private destroyRef = inject(DestroyRef);
  expandKeys: string[] = [];
  value?: string;
  nodes: any[] = [];
  isSelectAssignUnit = false;
  assignSelected: any[] = [];
  users: UserModel[] = [];
  units: UnitModel[] = [];
  assignResult: AssignResult = {
    users: [],
    units: [],
  };
  constructor(
    private assignWorkService: AssignWorkService,
    private toastService: ToastService
  ) {}
  ngOnInit(): void {
    this.loadUsers();
  }
  loadUsers() {

    this.assignWorkService
      .GetUsersAssign()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => {
          setTimeout(() => {
            const listPeerUnits = this.convertUserToNodes(data.peers);
            const listChildUnits = this.convertUserToNodes(data.subordinates);
            const objectTreePeerUnit = {
              title: '-- Cá nhân ngang cấp --',
              key: '0',
              selectable: false,
              children: [...listPeerUnits],
            };
            const objectTreeChildUnit = {
              title: '-- Cá nhân cấp dưới --',
              key: '1',
              selectable: false,
              children: [...listChildUnits],
            };
            this.nodes = [objectTreePeerUnit, objectTreeChildUnit];
            this.users = [...data.peers, ...data.subordinates];
          }, 300);
        },
        error: (err) => {
          this.users = [];
          this.toastService.Warning(err.message || 'Lấy người dùng thất bại!');
        },
      });
  }

  loadUnits() {

    this.assignWorkService
      .GetUnitsAssign()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => {
          const listPeerUnits = this.convertUnitsToNodes(data.peers);
          const listChildUnits = this.convertUnitsToNodes(data.surbodinates);
          const objectTreePeerUnit = {
            title: '-- Đơn vị ngang cấp --',
            key: '0',
            selectable: false,
            children: [...listPeerUnits],
          };
          const objectTreeChildUnit = {
            title: '-- Đơn vị cấp dưới --',
            key: '1',
            selectable: false,
            children: [...listChildUnits],
          };
          this.nodes = [objectTreePeerUnit, objectTreeChildUnit];
          this.units = [...data.surbodinates, ...data.peers];
        },
        error: (err) => {
          this.units = [];
          this.toastService.Warning(err.message || 'Lấy đơn vị thất bại!');
        },
      });
    console.log("danh sách list units",this.units);


  }
  //  thay đổi khi đổi lựa chọn
  onChangeSelectTreeUnit($event: string[]): void {
    const selectedUnits = this.units.filter((unit) =>
      $event.includes(unit.unitId.toString())
    );
    this.assignResult = {
      users: [],
      units: selectedUnits,
    };
  }
  onChangeSelectTreeUser($event: string[]): void {
    const selectedUsers = this.users.filter((user) =>
      $event.includes(user.userId.toString())
    );
    this.assignResult = {
      users: selectedUsers,
      units: [],
    };
  }
  onSwitchChange(value: boolean) {
    this.assignSelected = [];
    this.isSelectAssignUnit = value;
    if (value) {
      this.users = [];
      this.loadUnits();
    } else {
      this.units = [];
      this.loadUsers();
    }



  }
  onDropdownVisibilityChange(visible: boolean) {
    if (!visible) {
      // Dropdown  đóng
      this.emitAsssignSelect();
    }
  }

  emitAsssignSelect() {
    console.log('Đơn vị được gán:', this.assignResult);
    
    console.log(" danh sách task cha nhận đc ::",this.Assigned)

    this.Assigned.emit(this.assignResult);

  }
  convertUnitsToNodes(units: UnitModel[]): any[] {
    return units.map((unit) => ({
      title: unit.unitName,
      key: unit.unitId.toString(),
      isLeaf: true,
    }));
  }
  convertUserToNodes(users: UserModel[]): any[] {
    return users.map((users) => ({
      title: users.fullName,
      key: users.userId.toString(),
      isLeaf: true,
    }));
  }
}
