import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ReviewChildJobComponent } from './review-child-job.component';

describe('ReviewChildJobComponent', () => {
  let component: ReviewChildJobComponent;
  let fixture: ComponentFixture<ReviewChildJobComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ReviewChildJobComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ReviewChildJobComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
