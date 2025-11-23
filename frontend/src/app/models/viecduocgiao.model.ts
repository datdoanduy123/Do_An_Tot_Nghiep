export interface ViecduocgiaoModel {
  taskId: number;
  title: string;
  description: string;
  startDate: string;
  dueDate: string;
  percentageComplete: number;
  frequencyType: string;
  intervalValue: number;
  dayOfWeek: number[] | null;
  dayOfMonth: number[] | null;
   assignedUsers?: AssignedUser[]; 
  assignedUnits?: AssignedUnit[];
}
export interface AssignedUser {
  userId: number;
  username: string;
  fullName: string;
  email: string;
}

export interface AssignedUnit {
  unitId: number;
  org: string;
  unitName: string;
  type: string;
}
