import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ReviewOriginalJobComponent } from './review-original-job.component';

describe('ReviewOriginalJobComponent', () => {
  let component: ReviewOriginalJobComponent;
  let fixture: ComponentFixture<ReviewOriginalJobComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ReviewOriginalJobComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ReviewOriginalJobComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
