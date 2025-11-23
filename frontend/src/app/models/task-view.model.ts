export interface TaskViewModel {
  id: number;
  title: string;
  description: string;
  assigneeIds: number[] | null;
  assigneeFullNames: string[] | null;
  unitIds: number[] | null;
  startDate: string | null;
  endDate: string | null;
  frequencyType: string | null;
  intervalValue: number | null;
  daysOfWeek: number[] | null;
  daysOfMonth: number[] | null;
  parentTaskId: number | null;
}
