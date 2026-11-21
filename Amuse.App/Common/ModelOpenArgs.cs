using TensorStack.WPF.Controls;

namespace Amuse.App.Common
{
    /// <summary>
    /// Opens a view with a model preselected.
    /// </summary>
    /// <param name="Model">The model to select.</param>
    public sealed record ModelOpenArgs(IGenerateModel Model) : OpenViewArgs;
}
