import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ModalReviewProgressComponent } from './modal-review-progress.component';

describe('ModalReviewProgressComponent', () => {
  let component: ModalReviewProgressComponent;
  let fixture: ComponentFixture<ModalReviewProgressComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ModalReviewProgressComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ModalReviewProgressComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
