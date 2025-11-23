import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ModalDesciptionJobComponent } from './modal-desciption-job.component';

describe('ModalDesciptionJobComponent', () => {
  let component: ModalDesciptionJobComponent;
  let fixture: ComponentFixture<ModalDesciptionJobComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ModalDesciptionJobComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ModalDesciptionJobComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
