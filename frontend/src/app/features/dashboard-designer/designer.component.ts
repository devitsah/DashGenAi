import { Component, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { Style } from './style/style';
import { Datasource } from './datasource/datasource';
import { Preview } from './preview/preview';
import { BuilderStateService } from './builder-state.service';

@Component({
  selector: 'app-aibuilder',
  standalone: true,
  imports: [CommonModule, Style, Datasource, Preview, RouterModule],
  templateUrl: './designer.component.html',
  styleUrls: ['./designer.component.css']
})
export class AIBuilder {
  @ViewChild(Preview) private previewRef?: Preview;
  public currentStep = 0;

  public readonly steps: string[] = [
    'Style & Create',
    'AI Builder',
    'Staging Preview'
  ];

  constructor(public state: BuilderStateService, private router: Router) {}

  public canContinue(): boolean {
    if (this.currentStep === 0) return !!this.state.dashboardId();
    return true;
  }

  public continue(): void {
    if (this.currentStep < this.steps.length - 1) {
      this.currentStep++;
    } else {
      this.commit();
    }
  }

  public back(): void {
    if (this.currentStep > 0) this.currentStep--;
  }

  public commit(): void {
    const save = this.previewRef ? this.previewRef.saveLayout() : Promise.resolve();
    save.then(() => this.router.navigate(['/homepage/DashboardHome']));
  }
}
