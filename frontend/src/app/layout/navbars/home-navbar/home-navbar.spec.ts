import { ComponentFixture, TestBed } from '@angular/core/testing';

import { HomeNavbar } from './home-navbar';

describe('HomeNavbar', () => {
  let component: HomeNavbar;
  let fixture: ComponentFixture<HomeNavbar>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HomeNavbar],
    }).compileComponents();

    fixture = TestBed.createComponent(HomeNavbar);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
