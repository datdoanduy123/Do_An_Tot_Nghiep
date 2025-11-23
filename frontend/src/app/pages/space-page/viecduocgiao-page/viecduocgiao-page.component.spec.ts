import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ViecduocgiaoPageComponent } from './viecduocgiao-page.component';

describe('ViecduocgiaoPageComponent', () => {
  let component: ViecduocgiaoPageComponent;
  let fixture: ComponentFixture<ViecduocgiaoPageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ViecduocgiaoPageComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(ViecduocgiaoPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
