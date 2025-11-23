export interface UnitModel {
  unitId: number;
  unitName: string;
}

// 
export interface Surbodinate {
  unitId: number
  org: string
  unitName: string
  type: string
}

export interface UnitStructureModel {
  // headUnit: UnitModel;
  // childUnits: UnitModel[];
  // peerUnits: UnitModel[];
  surbodinates: Surbodinate[]
  peers: any[]
}
