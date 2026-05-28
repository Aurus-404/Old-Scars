namespace OldScars.Core.Interactions
{
    public readonly struct DebugActionExecutionResult
    {
        public DebugActionExecutionResult(bool hasResult, string title, string body)
        {
            this.hasResult = hasResult;
            this.title = title;
            this.body = body;
        }

        public readonly bool hasResult;
        public readonly string title;
        public readonly string body;

        public static DebugActionExecutionResult None()
        {
            return new DebugActionExecutionResult(false, null, null);
        }

        public static DebugActionExecutionResult Info(string title, string body)
        {
            return new DebugActionExecutionResult(true, title, body);
        }
    }
}
