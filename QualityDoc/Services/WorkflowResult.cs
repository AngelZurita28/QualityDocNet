namespace QualityDoc.Services
{
    public class WorkflowResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;

        public static WorkflowResult Ok(string message)
        {
            return new WorkflowResult { Success = true, Message = message };
        }

        public static WorkflowResult Fail(string message)
        {
            return new WorkflowResult { Success = false, Message = message };
        }
    }
}
