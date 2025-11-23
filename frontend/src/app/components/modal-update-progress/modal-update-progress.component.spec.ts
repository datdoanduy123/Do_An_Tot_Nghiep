import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ModalUpdateProgressComponent } from './modal-update-progress.component';

describe('ModalUpdateProgressComponent', () => {
  let component: ModalUpdateProgressComponent;
  let fixture: ComponentFixture<ModalUpdateProgressComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ModalUpdateProgressComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(ModalUpdateProgressComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
