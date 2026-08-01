import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DashboardView } from './dashboard-view';

describe('DashboardView', () => {
  let component: DashboardView;
  let fixture: ComponentFixture<DashboardView>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardView],
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardView);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
