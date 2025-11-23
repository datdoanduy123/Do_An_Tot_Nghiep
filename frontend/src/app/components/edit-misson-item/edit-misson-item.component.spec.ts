import { ComponentFixture, TestBed } from '@angular/core/testing';

import { EditMissonItemComponent } from './edit-misson-item.component';

describe('EditMissonItemComponent', () => {
  let component: EditMissonItemComponent;
  let fixture: ComponentFixture<EditMissonItemComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EditMissonItemComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(EditMissonItemComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
