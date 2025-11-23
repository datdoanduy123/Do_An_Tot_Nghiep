import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DetailViecduocgiaoComponent } from './detail-viecduocgiao.component';

describe('DetailViecduocgiaoComponent', () => {
  let component: DetailViecduocgiaoComponent;
  let fixture: ComponentFixture<DetailViecduocgiaoComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DetailViecduocgiaoComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(DetailViecduocgiaoComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
