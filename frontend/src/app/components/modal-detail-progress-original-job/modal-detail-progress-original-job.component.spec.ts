import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ModalDetailProgressOriginalJobComponent } from './modal-detail-progress-original-job.component';

describe('ModalDetailProgressOriginalJobComponent', () => {
  let component: ModalDetailProgressOriginalJobComponent;
  let fixture: ComponentFixture<ModalDetailProgressOriginalJobComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ModalDetailProgressOriginalJobComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ModalDetailProgressOriginalJobComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
