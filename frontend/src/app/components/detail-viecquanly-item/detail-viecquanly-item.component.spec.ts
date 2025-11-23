import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DetailViecquanlyItemComponent } from './detail-viecquanly-item.component';

describe('DetailViecquanlyItemComponent', () => {
  let component: DetailViecquanlyItemComponent;
  let fixture: ComponentFixture<DetailViecquanlyItemComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DetailViecquanlyItemComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(DetailViecquanlyItemComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
