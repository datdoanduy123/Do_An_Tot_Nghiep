export interface TaskParentModel {
  taskId: number;
  title: string;
  description: string;
  startDate: string;
  dueDate: string;
  percentagecomplete: number;
  frequencyType: string | null;
   intervalValue: number | null;
  daysOfWeek: number[] | null;
  daysOfMonth: number[] | null;
}
