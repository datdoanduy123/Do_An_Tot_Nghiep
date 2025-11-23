import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SelectRepeatScheduleComponent } from './select-repeat-schedule.component';

describe('SelectRepeatScheduleComponent', () => {
  let component: SelectRepeatScheduleComponent;
  let fixture: ComponentFixture<SelectRepeatScheduleComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SelectRepeatScheduleComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(SelectRepeatScheduleComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
