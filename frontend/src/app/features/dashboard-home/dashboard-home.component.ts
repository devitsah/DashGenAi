import { Component } from '@angular/core';
import { HomeNavbar } from "../../layout/navbars/home-navbar/home-navbar";
import { DashboardList } from "./dashboard-list/dashboard-list";
@Component({
  selector: 'app-dashboard-viewer',
  imports: [HomeNavbar, DashboardList],
  templateUrl: './dashboard-home.component.html',
  styleUrl: './dashboard-home.component.css',
})
export class DashboardHome {}
