import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DetailViecquanlyComponent } from './detail-viecquanly.component';

describe('DetailViecquanlyComponent', () => {
  let component: DetailViecquanlyComponent;
  let fixture: ComponentFixture<DetailViecquanlyComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DetailViecquanlyComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(DetailViecquanlyComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
