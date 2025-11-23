import { Metadata } from "../interface/response-paganation";

export enum FrequencyType {
  Daily = 'Ngày',
  Weekly = 'Tuần',
  Monthly = 'Tháng',
  Yearly = 'Năm',
  Quy = 'Quý',
  SixMonth = '6thang',
}
export enum Status {
  DangThucHien = 'in_progress',
  HoanThanh = 'completed',
  TamDung = 'not_started',
}
export enum ConvertStatusTask {
  in_progress = 'Đang thực hiện',
  completed = 'Hoàn thành',
  pending = 'Chưa xác nhận',
  delay = 'Tạm dừng',
}
export enum WeekDay {
  Monday = 1,
  Tuesday = 2,
  Wednesday = 3,
  Thursday = 4,
  Friday = 5,
  Saturday = 6,
  Sunday = 0,
}
export const FrequencyTypeMap: Record<'daily' | 'weekly' | 'monthly', string> =
  {
    daily: 'ngày',
    weekly: 'tuần',
    monthly: 'tháng',
  };
export enum ConvertStatusProgress {
  in_progress = 'Đang thực hiện',
  completed = 'Hoàn thành',
  pending = 'Chưa xác nhận',
  delay = 'Tạm dừng',
}
export enum typeNotification {
  updateProgress = 'updateProgress',
  createTask = 'createTask', // bạn vừa nhận được việc mới (NOTI)
  taskReminder = 'taskReminder', // nhắc nhở nhiệm vụ (NHẮC NHỞ)
  putDeadlineTask = 'putDeadlineTask', // nhiệm vụ vừa được thay đổi thời hạn (NOTI)
  approved = 'approved', // Báo cáo đã được phê duyệt
  rejected = 'rejected', // Báo cáo bị từ chối
}
//default metadata
  export const DEFAULT_METADATA: Metadata = {
  pageIndex: 0,
  totalPages: 0,
  totalItems: 0,
  currentItems: 0,
  hasPrevious: false,
  hasNext: false,
};