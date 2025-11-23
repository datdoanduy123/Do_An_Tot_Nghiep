import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ModalAddJobComponent } from './modal-add-job.component';

describe('ModalAddJobComponent', () => {
  let component: ModalAddJobComponent;
  let fixture: ComponentFixture<ModalAddJobComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ModalAddJobComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ModalAddJobComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
