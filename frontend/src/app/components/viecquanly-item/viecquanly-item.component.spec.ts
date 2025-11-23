import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ViecquanlyItemComponent } from './viecquanly-item.component';

describe('ViecquanlyItemComponent', () => {
  let component: ViecquanlyItemComponent;
  let fixture: ComponentFixture<ViecquanlyItemComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ViecquanlyItemComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ViecquanlyItemComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
