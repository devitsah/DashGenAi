import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface SchemaColumn {
  name: string;
  dataType: string;
  numeric: boolean;
}

export interface SchemaTable {
  table: string;
  columns: SchemaColumn[];
}

@Injectable({ providedIn: 'root' })
export class SchemaService {
  constructor(private http: HttpClient) {}

  getTables(): Observable<SchemaTable[]> {
    return this.http.get<SchemaTable[]>(`${environment.apiUrl}/schema/tables`);
  }
}