import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzResultModule } from 'ng-zorro-antd/result';
@Component({
  selector: 'app-page-not-found',
  standalone: true,
  imports: [CommonModule, RouterModule, NzButtonModule, NzResultModule],
  templateUrl: './PageNotFound.component.html',
  styleUrls: ['./PageNotFound.component.css'],
})
export class PageNotFoundComponent {
  constructor() {}

  goBack() {
    window.history.back();
  }
}
