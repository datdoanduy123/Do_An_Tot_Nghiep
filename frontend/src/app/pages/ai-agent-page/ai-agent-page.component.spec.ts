import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AiagentPageComponent } from './ai-agent-page.component';

describe('AiAgentPageComponent', () => {
  let component: AiAgentPageComponent;
  let fixture: ComponentFixture<AiAgentPageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AiAgentPageComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(AiAgentPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
