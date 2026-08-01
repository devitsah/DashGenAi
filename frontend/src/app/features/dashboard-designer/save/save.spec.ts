import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Save } from './save';

describe('Save', () => {
  let component: Save;
  let fixture: ComponentFixture<Save>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Save],
    }).compileComponents();

    fixture = TestBed.createComponent(Save);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
