export interface ProgressModel {
  progressId: number;
  status: string;
  updatedBy: number;
  updateByName: string | null;
  proposal: string | null;
  result: string | null;
  feedback: string | null;
  updatedAt: string | null;
  fileName: string | null;
  filePath: string;
}

export interface ScheduledProgressModel {
  periodIndex: number;
  periodStartDate: string;
  periodEndDate: string;
  progresses: ProgressModel[];
  status: string;
  date: string;
}

export interface UnitProgressModel {
  unitId: number;
  unitName: string;
  leaderFullName: string; // Tên trưởng phòng
  userId: number; // UserId của trưởng phòng
  scheduledProgresses: ScheduledProgressModel[];
}

export interface UserProgressModel {
  userId: number;
  userName: string;
  scheduledProgresses: ScheduledProgressModel[];
  // isUnitRepresentative?: boolean; // Flag để phân biệt user thường và đại diện unit
  // unitId?: number; // ID unit nếu là đại diện
  // unitName?: string; // Tên unit nếu là đại diện
  // org?: string; // Tổ chức nếu là đại diện
}