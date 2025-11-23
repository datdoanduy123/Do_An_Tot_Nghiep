export interface DetailProgressTaskParentModel {
  taskId: number;
  taskName: string;
  description: string;
  taskStatus: string;
  assigneeFullNames: string[];
  startDate: string | null;
  dueDate: string | null;
  children: DetailProgressTaskChildModel[];
}

export interface DetailProgressTaskChildModel {
  taskId: number;
  taskName: string;
  description: string;
  taskStatus: string;
  assigneeFullName: string[];
  startDate: string;
  dueDate: string;
  completionRate: number;
}

// Model mới cho API task detail
export interface TaskDetailModel {
  taskId: number;
  title: string;
  description: string;
  startDate: string;
  dueDate: string;
  percentagecomplete: number;
  status: string;
}

// Model mới cho API subtask
export interface SubtaskModel {
  taskId: number;
  title: string;
  description: string;
  assignerId: number;
  assigneeId: number | null;
  periodId: number | null;
  attachedFile: string | null;
  status: string;
  priority: string;
  startDate: string;
  dueDate: string;
  createdAt: string;
  frequencyType: string;
  intervalValue: number;
  frequencyDays: number[];
  percentagecomplete: number;
  parentTaskId: number;
  assignedUsers: AssignedUserModel[];
}

// Model cho assigned users
export interface AssignedUserModel {
  userId: number;
  username: string;
  fullName: string;
  email: string;
}