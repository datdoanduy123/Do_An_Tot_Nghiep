import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MissonItemComponent } from './misson-item.component';

describe('MissonItemComponent', () => {
  let component: MissonItemComponent;
  let fixture: ComponentFixture<MissonItemComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MissonItemComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(MissonItemComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
