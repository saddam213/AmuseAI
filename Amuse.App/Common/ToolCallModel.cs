using Amuse.App.Tools;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public class ToolCallModel : BaseModel
    {
        private bool _isEnabled;
        public ToolCallModel() { }
        public ToolCallModel(ToolCallBase toolCall)
        {
            Name = toolCall.Name;
            Icon = toolCall.DisplayIcon;
            IsEnabled = toolCall.IsDefault;
            Description = toolCall.Description;
        }

        public string Name { get; set; }
        public string Icon { get; set; }
        public string Description { get; set; }
        public bool IsEnabled
        {
            get { return _isEnabled; }
            set { SetProperty(ref _isEnabled, value); }
        }
    }
}
