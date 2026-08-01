CREATE TABLE prompt_statuses (code VARCHAR(30) PRIMARY KEY);
INSERT INTO prompt_statuses (code) VALUES
('Pending'), ('PendingClarification'), ('Processing'), ('Completed'), ('Failed');

CREATE TABLE widget_types (code VARCHAR(50) PRIMARY KEY);
INSERT INTO widget_types (code) VALUES
('KpiCard'), ('Table'), ('PieChart'), ('BarChart'), ('LineChart');

CREATE TABLE dashboard_visibilities (code VARCHAR(20) PRIMARY KEY);
INSERT INTO dashboard_visibilities (code) VALUES ('Private'), ('Shared');

CREATE TABLE dashboard_styles (code VARCHAR(50) PRIMARY KEY);
INSERT INTO dashboard_styles (code) VALUES ('PowerBi'), ('Grafana'), ('Custom');

-- fix the existing lowercase default mismatch before adding the FK
ALTER TABLE prompts ALTER COLUMN status SET DEFAULT 'Pending';

ALTER TABLE prompts ADD CONSTRAINT fk_prompts_status FOREIGN KEY (status) REFERENCES prompt_statuses(code);
ALTER TABLE widgets ADD CONSTRAINT fk_widgets_widget_type FOREIGN KEY (widget_type) REFERENCES widget_types(code);
ALTER TABLE dashboards ADD CONSTRAINT fk_dashboards_visibility FOREIGN KEY (visibility) REFERENCES dashboard_visibilities(code);
ALTER TABLE dashboards ADD CONSTRAINT fk_dashboards_dashboard_style FOREIGN KEY (dashboard_style) REFERENCES dashboard_styles(code);