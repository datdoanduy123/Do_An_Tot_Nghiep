import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SelectUnitComponent } from './select-unit.component';

describe('SelectUnitComponent', () => {
  let component: SelectUnitComponent;
  let fixture: ComponentFixture<SelectUnitComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SelectUnitComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(SelectUnitComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
