import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AssignJobPageComponent } from './assign-job-page.component';

describe('AssignJobPageComponent', () => {
  let component: AssignJobPageComponent;
  let fixture: ComponentFixture<AssignJobPageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AssignJobPageComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(AssignJobPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
