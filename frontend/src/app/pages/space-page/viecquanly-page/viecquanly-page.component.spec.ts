import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ViecquanlyPageComponent } from './viecquanly-page.component';

describe('ViecquanlyPageComponent', () => {
  let component: ViecquanlyPageComponent;
  let fixture: ComponentFixture<ViecquanlyPageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ViecquanlyPageComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ViecquanlyPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
