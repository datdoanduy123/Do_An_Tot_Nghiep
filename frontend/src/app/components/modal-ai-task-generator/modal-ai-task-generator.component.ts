import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTreeModule, NzTreeNodeOptions } from 'ng-zorro-antd/tree';
import { GenerateProjectResponse } from '../../service/ai-agent.service';

@Component({
  selector: 'app-modal-ai-task-generator',
  standalone: true,
  imports: [
    CommonModule,
    NzTagModule,
    NzModalModule,
    NzButtonModule,
    NzIconModule,
    NzSpinModule,
    NzCardModule,
    NzTreeModule
  ],
  templateUrl: './modal-ai-task-generator.component.html',
  styleUrls: ['./modal-ai-task-generator.component.css']
})
export class ModalAiTaskGeneratorComponent implements OnChanges {
  @Input() isVisible = false;
  @Input() generatedProject: GenerateProjectResponse | null = null;
  @Output() closeModal = new EventEmitter<void>();

  treeData: NzTreeNodeOptions[] = [];

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['generatedProject'] && this.generatedProject) {
      // Exclude warnings from the createdNodes
      const nodes = this.generatedProject.createdNodes.filter(node => !node.hasOwnProperty('warnings'));
      this.treeData = this.buildTree(nodes);
    }
  }

  buildTree(nodes: any[], parentTaskId: number | null = null): NzTreeNodeOptions[] {
    const tree: NzTreeNodeOptions[] = [];
    nodes
      .filter(node => node.parentTaskId === parentTaskId)
      .forEach(node => {
        const children = this.buildTree(nodes, node.taskId);
        const treeNode: NzTreeNodeOptions = {
          title: node.title,
          key: node.taskId.toString(),
          expanded: true,
          children: children,
          isLeaf: children.length === 0,
          // Custom properties for template
          nodeType: node.nodeType,
          assigneeName: node.assigneeName,
          assignMatchScore: node.assignMatchScore
        };
        tree.push(treeNode);
      });
    return tree;
  }

  handleCancel(): void {
    this.closeModal.emit();
  }

  handleOk(): void {
    this.closeModal.emit();
  }

  getIconType(nodeType: string): string {
    switch (nodeType) {
      case 'Project': return 'project';
      case 'Epic': return 'folder-open';
      case 'Story': return 'book';
      case 'Task': return 'file-text';
      default: return 'file';
    }
  }

  getColor(nodeType: string): string {
    switch (nodeType) {
      case 'Project': return '#108ee9';
      case 'Epic': return '#87d068';
      case 'Story': return '#2db7f5';
      case 'Task': return '#f50';
      default: return 'gray';
    }
  }
}
