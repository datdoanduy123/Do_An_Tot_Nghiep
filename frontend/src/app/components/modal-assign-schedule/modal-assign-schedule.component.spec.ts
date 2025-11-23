import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ModalAssignScheduleComponent } from './modal-assign-schedule.component';

describe('ModalAssignScheduleComponent', () => {
  let component: ModalAssignScheduleComponent;
  let fixture: ComponentFixture<ModalAssignScheduleComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ModalAssignScheduleComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(ModalAssignScheduleComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
