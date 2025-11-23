import { Injectable } from '@angular/core';

export interface AiTaskData {
  title: string;
  description: string;
  startDate: string;
  endDate: string;
  fileName?: string;
  source: 'ai';
}

@Injectable({
  providedIn: 'root'
})
export class AiTaskStorageService {
  private readonly STORAGE_KEY = 'ai_task_prefill_data';

  constructor() {}

  /**
   * Lưu dữ liệu AI task vào localStorage
   */
  saveAiTaskData(data: AiTaskData): void {
    try {
      localStorage.setItem(this.STORAGE_KEY, JSON.stringify(data));
      console.log('✅ Đã lưu AI task data vào localStorage:', data);
    } catch (error) {
      console.error('❌ Lỗi lưu AI task data:', error);
    }
  }

  /**
   * Lấy dữ liệu AI task từ localStorage
   */
  getAiTaskData(): AiTaskData | null {
    try {
      const data = localStorage.getItem(this.STORAGE_KEY);
      if (data) {
        const parsed = JSON.parse(data);
        console.log('📥 Đã lấy AI task data từ localStorage:', parsed);
        return parsed;
      }
      return null;
    } catch (error) {
      console.error('❌ Lỗi đọc AI task data:', error);
      return null;
    }
  }

  /**
   * Xóa dữ liệu AI task khỏi localStorage
   */
  clearAiTaskData(): void {
    try {
      localStorage.removeItem(this.STORAGE_KEY);
      console.log('🗑️ Đã xóa AI task data khỏi localStorage');
    } catch (error) {
      console.error('❌ Lỗi xóa AI task data:', error);
    }
  }

  /**
   * Kiểm tra có dữ liệu AI task không
   */
  hasAiTaskData(): boolean {
    return localStorage.getItem(this.STORAGE_KEY) !== null;
  }
}