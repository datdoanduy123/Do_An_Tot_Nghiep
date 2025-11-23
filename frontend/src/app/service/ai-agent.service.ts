// src/app/service/ai-agent.service.ts
import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../environment/environment';
import { map, Observable } from 'rxjs';
import { ResponseApi } from '../interface/response';

export interface AITaskSuggestion {
  title: string;
  description: string;
  startDate: string;
  endDate: string;
  subtasks: AISubtask[];
}

export interface AISubtask {
  title: string;
  description: string;
  startDate: string;
  dueDate: string;
}

@Injectable({
  providedIn: 'root',
})
export class AiAgentService {
  apiUrl: string;

  constructor(private http: HttpClient) {
    this.apiUrl = `${environment.SERVICE_API}`;
  }

  // API AI tạo đề xuất công việc từ file
  generateTaskSuggestions(fileId: number): Observable<AITaskSuggestion> {
    const url = `${this.apiUrl}chat/generate-tasks/${fileId}`;
    
    return this.http.post<any>(url, {}).pipe(
  map((res) => {
    const data = res?.data;
    if (data && data.title) {
      return {
        title: this.decodeText(data.title),
        description: this.decodeText(data.description),
        startDate: data.startDate || '',
        endDate: data.endDate || '',
        subtasks: (data.subtasks || []).map((subtask: any) => ({
          title: this.decodeText(subtask.title),
          description: this.decodeText(subtask.description),
          startDate: subtask.startDate,
          dueDate: subtask.dueDate
        }))
      } as AITaskSuggestion;
    }
    throw new Error('Invalid response format');
  })
);
  }

  // Helper method để decode text
  private decodeText(text: string): string {
    if (!text) return '';
    
    try {
      // Thử decode từ các encoding phổ biến
      return decodeURIComponent(escape(text));
    } catch (e) {
      // Nếu không decode được thì trả về text gốc
      return text;
    }
  }

  // Chuyển đổi AI suggestion thành TaskViewModel để có thể sử dụng cho giao việc
  convertToTaskViewModels(suggestion: AITaskSuggestion): {
    parentTask: any;
    childTasks: any[];
  } {
    const parentTask = {
      id: Date.now(), // Temporary ID
      title: suggestion.title,
      description: suggestion.description,
      assigneeIds: [],
      assigneeFullNames: [],
      unitIds: [],
      startDate: this.convertDateFormat(suggestion.startDate),
      endDate: this.convertDateFormat(suggestion.endDate),
      frequencyType: 'once',
      intervalValue: 1,
      daysOfWeek: [],
      daysOfMonth: [],
      parentTaskId: null,
    };

    const childTasks = suggestion.subtasks.map((subtask, index) => ({
      id: Date.now() + index + 1, // Temporary ID
      title: subtask.title,
      description: subtask.description,
      assigneeIds: [],
      assigneeFullNames: [],
      unitIds: [],
      startDate: this.convertDateFormat(subtask.startDate),
      endDate: this.convertDateFormat(subtask.dueDate),
      frequencyType: 'once',
      intervalValue: 1,
      daysOfWeek: [],
      daysOfMonth: [],
      parentTaskId: parentTask.id,
    }));

    return { parentTask, childTasks };
  }

  private convertDateFormat(dateString: string): string {
  if (!dateString) return new Date().toISOString(); // hoặc null tùy logic bạn muốn

  const [day, month, year] = dateString.split('/');
  const date = new Date(parseInt(year), parseInt(month) - 1, parseInt(day));
  return date.toISOString();
}
}