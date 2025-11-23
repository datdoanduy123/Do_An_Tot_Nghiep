import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SelectMultiUnitComponent } from './select-multi-unit.component';

describe('SelectMultiUnitComponent', () => {
  let component: SelectMultiUnitComponent;
  let fixture: ComponentFixture<SelectMultiUnitComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SelectMultiUnitComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(SelectMultiUnitComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
