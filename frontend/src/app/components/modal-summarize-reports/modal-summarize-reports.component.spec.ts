import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ModalSummarizeReportsComponent } from './modal-summarize-reports.component';

describe('ModalSummarizeReportsComponent', () => {
  let component: ModalSummarizeReportsComponent;
  let fixture: ComponentFixture<ModalSummarizeReportsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ModalSummarizeReportsComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ModalSummarizeReportsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
