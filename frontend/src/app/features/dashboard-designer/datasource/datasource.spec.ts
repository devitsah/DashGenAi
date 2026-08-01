import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Datasource } from './datasource';

describe('Datasource', () => {
  let component: Datasource;
  let fixture: ComponentFixture<Datasource>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Datasource],
    }).compileComponents();

    fixture = TestBed.createComponent(Datasource);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
