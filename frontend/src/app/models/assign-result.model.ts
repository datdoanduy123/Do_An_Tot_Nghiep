import { UnitModel } from './unit.model';
import { UserModel } from './user.model';

export interface AssignResult {
  users: UserModel[];
  units: UnitModel[];
}
