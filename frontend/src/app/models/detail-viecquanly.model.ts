export interface DetailViecquanlyModel {
  TaskId: number;
  Title: string;
  Description: string;
  StartDate: string; // ISO format date string
  DueDate: string; // ISO format date string
  PercentageComplete: number;
  AssigneeFullNames: string[];
  AssignedUnits: AssignedUnit[];
  FrequencyType: string; // hoặc string nếu server không cố định
  IntervalValue: number;
  dayOfWeek: number[] | null;
  dayOfMonth: number[] | null;
}
export interface AssignedUnit {
  unitId: number;
  org: string;
  unitName: string;
  type: string;
}