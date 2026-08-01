export interface Widget {
  id: number;
  dashboardId: number;
  title: string;
  widgetType: 'KpiCard' | 'Table' | 'BarChart' | 'PieChart' | 'LineChart' | 'AreaChart' | 'Gauge' | 'TimeSeries' | 'HtmlWidget';
  width: number;
  height: number;
  positionX: number;
  positionY: number;
  refreshInterval?: number;
  dataSource: string;
  generatedSql?: string;
  backgroundColor?: string;
  configJson?: string;
}


export interface Dashboard {
  id: number;
  name: string;
  description?: string;
  visibility: string;
  isDefault: boolean;
  dashboardStyle: 'PowerBi' | 'Grafana' | 'Custom';
  versionNo: number;
   backgroundColor?: string;
  widgets: Widget[];
}

// Summary shape for the dashboard-list card grid UI
export interface DashboardCard {
  dashboardId: number;
  title: string;
  description: string;
  platform: string;
  updated: string;
  widgets: number;
}