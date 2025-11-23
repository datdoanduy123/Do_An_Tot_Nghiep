import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ViecduocgiaoItemComponent } from './viecduocgiao-item.component';

describe('ViecduocgiaoItemComponent', () => {
  let component: ViecduocgiaoItemComponent;
  let fixture: ComponentFixture<ViecduocgiaoItemComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ViecduocgiaoItemComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(ViecduocgiaoItemComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
