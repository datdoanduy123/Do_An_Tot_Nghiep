import { FrequencyView } from '../models/frequency-view.model';
import { TaskViewModel } from '../models/task-view.model';

export function getFrequencySelected(task: TaskViewModel, data: FrequencyView): TaskViewModel {
  if (!task || !data) return task;

  return {
    ...task,
    frequencyType: data.frequency_type,
    intervalValue: data.interval_value,
    daysOfMonth: data.daysInMonth,
    daysOfWeek: data.daysInWeek
  };
}
