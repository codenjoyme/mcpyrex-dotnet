using System;
using System.Collections.Generic;
using System.Text.Json;

namespace McpDotnet.Pipeline
{
    /// <summary>
    /// Data models and types for pipeline execution.
    /// 
    /// This module contains all data classes, enums, and type definitions
    /// used throughout the pipeline system.
    /// </summary>

    /// <summary>
    /// Result of pipeline execution.
    /// </summary>
    public class PipelineResult
    {
        public bool Success { get; set; }
        public object? Result { get; set; }
        public string? Error { get; set; }
        public object? Step { get; set; }
        public string? Tool { get; set; }
        public Dictionary<string, object>? Context { get; set; }
        public double? ExecutionTime { get; set; }

        public PipelineResult()
        {
            Context = new Dictionary<string, object>();
        }

        /// <summary>
        /// Convert to dictionary format.
        /// </summary>
        public Dictionary<string, object> ToDict()
        {
            return new Dictionary<string, object>
            {
                ["success"] = Success,
                ["result"] = Result,
                ["error"] = Error,
                ["step"] = Step,
                ["tool"] = Tool,
                ["context"] = Context ?? new Dictionary<string, object>(),
                ["execution_time"] = ExecutionTime
            };
        }
    }

    /// <summary>
    /// Execution context for pipeline.
    /// </summary>
    public class ExecutionContext
    {
        public Dictionary<string, object> Variables { get; set; }
        public object StepNumber { get; set; } = 0;
        public Dictionary<string, object>? Templates { get; set; }

        public ExecutionContext(Dictionary<string, object>? variables = null)
        {
            Variables = variables ?? new Dictionary<string, object>();
            Templates = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Pipeline step types (legacy - not used in strategy architecture).
    /// </summary>
    public enum StepType
    {
        Tool,
        Condition,
        Loop,
        Parallel,
        Template
    }
}
