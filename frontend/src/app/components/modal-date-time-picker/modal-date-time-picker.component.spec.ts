import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ModalDateTimePicker } from './modal-date-time-picker.component';

describe('ModalDateTimePicker', () => {
  let component: ModalDateTimePicker;
  let fixture: ComponentFixture<ModalDateTimePicker>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ModalDateTimePicker],
    }).compileComponents();

    fixture = TestBed.createComponent(ModalDateTimePicker);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
