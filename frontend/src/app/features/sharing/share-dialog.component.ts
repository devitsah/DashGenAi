import { Component, EventEmitter, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

@Component({
  selector: 'app-share',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './share-dialog.component.html',
  styleUrls: ['./share-dialog.component.css']
})
export class ShareComponent {

  constructor(private router:Router){};
  email = "";

  share() {
    alert(`Dashboard shared with ${this.email}`);
  }

  cancel() {
    this.router.navigate(['/homepage']);
  }

}