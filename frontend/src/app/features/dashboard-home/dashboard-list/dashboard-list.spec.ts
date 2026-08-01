import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DashboardList } from './dashboard-list';

describe('DashboardList', () => {
  let component: DashboardList;
  let fixture: ComponentFixture<DashboardList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardList],
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardList);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
