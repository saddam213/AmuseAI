namespace Amuse.Common
{
    public record ConversationMessage(ConversationRole Role, string Content, int[] ImageIndex, int[] AudioIndex, string[] ToolCalls);
}
