import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ModalAssignPersonToJobComponent } from './modal-assign-person-to-job.component';

describe('ModalAssignPersonToJobComponent', () => {
  let component: ModalAssignPersonToJobComponent;
  let fixture: ComponentFixture<ModalAssignPersonToJobComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ModalAssignPersonToJobComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(ModalAssignPersonToJobComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
