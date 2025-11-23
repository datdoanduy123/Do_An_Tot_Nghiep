import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ModalDetailProgressChildJobComponent } from './modal-detail-progress-child-job.component';

describe('ModalDetailProgressChildJobComponent', () => {
  let component: ModalDetailProgressChildJobComponent;
  let fixture: ComponentFixture<ModalDetailProgressChildJobComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ModalDetailProgressChildJobComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ModalDetailProgressChildJobComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
