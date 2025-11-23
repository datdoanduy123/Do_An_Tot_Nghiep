import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ModalSendRemindComponent } from './modal-send-remind.component';

describe('ModalSendRemindComponent', () => {
  let component: ModalSendRemindComponent;
  let fixture: ComponentFixture<ModalSendRemindComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ModalSendRemindComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ModalSendRemindComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
