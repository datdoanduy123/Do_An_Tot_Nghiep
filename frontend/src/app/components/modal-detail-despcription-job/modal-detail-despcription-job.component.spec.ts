import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ModalDetailDespcriptionJobComponent } from './modal-detail-despcription-job.component';

describe('ModalDetailDespcriptionJobComponent', () => {
  let component: ModalDetailDespcriptionJobComponent;
  let fixture: ComponentFixture<ModalDetailDespcriptionJobComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ModalDetailDespcriptionJobComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ModalDetailDespcriptionJobComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
