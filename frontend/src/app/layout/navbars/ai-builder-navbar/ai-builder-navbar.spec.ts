import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AiBuilderNavbar } from './ai-builder-navbar';

describe('AiBuilderNavbar', () => {
  let component: AiBuilderNavbar;
  let fixture: ComponentFixture<AiBuilderNavbar>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AiBuilderNavbar],
    }).compileComponents();

    fixture = TestBed.createComponent(AiBuilderNavbar);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
