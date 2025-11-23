using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocTask.Core.Dtos.Gemini;
using DocTask.Core.Models;

namespace DocTask.Service.Mappers
{
    public static class AgentMapper
    {
        public static AgentDto ToAgentDto(this AgentContext agent)
        {
            return new AgentDto
            {
                Id = agent.Id,
                ContextName = agent.ContextName,
                ContextDescription = agent.ContextDescription,
                FileId = agent.FileId,
            };
        }
    }
}