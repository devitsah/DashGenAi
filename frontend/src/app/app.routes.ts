import { Routes } from '@angular/router';
import { RegisterComponent } from './features/auth/register.component';
import { DashboardHome } from './features/dashboard-home/dashboard-home.component';
import { Layout } from './layout/layout/layout';
import { AIBuilder } from './features/dashboard-designer/designer.component';
import { DashboardView } from './features/dashboard-view/dashboard-view/dashboard-view';
import { View } from './features/dashboard-view/view/view';
import { Design } from './features/dashboard-view/design/design';
import { LoginComponent } from './features/auth/login.component';
import { ShareComponent } from './features/sharing/share-dialog.component';
import { Default } from './features/default/default';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: '', component: RegisterComponent },
  { path: 'register', component: RegisterComponent },
  { path: 'login', component: LoginComponent },
  {
    path: 'homepage',
    component: Layout,
    canActivate: [authGuard],
    children: [
      { path: '', component: Default },
      { path: 'DefaultDashboard', component: Default },
      { path: 'DashboardHome', component: DashboardHome },
      { path: 'AIBuilder', component: AIBuilder },
      {
        path: 'DefaultView/:id',
        component: DashboardView,
        children: [
          { path: 'view', component: View },
          { path: 'design', component: Design }
        ]
      },
      { path: 'share', component: ShareComponent }
    ]
  }
];