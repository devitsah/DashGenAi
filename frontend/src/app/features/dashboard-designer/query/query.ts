import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BuilderStateService } from '../builder-state.service';

@Component({
  selector: 'app-query',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './query.html',
  styleUrls: ['./query.css']
})
export class QueryComponent {
  copyStatus = 'Copy Query';

  constructor(public state: BuilderStateService) {}

  get query(): string {
    return this.state.lastResult()?.generatedSql ?? '';
  }

  copyQuery(): void {
    if (!this.query) return;
    navigator.clipboard.writeText(this.query).then(() => {
      this.copyStatus = 'Copied!';
      setTimeout(() => (this.copyStatus = 'Copy Query'), 2000);
    });
  }
}
