using TensorStack.WPF;

namespace Amuse.App.Common
{
    /// <summary>
    /// A folder outside the model directory that is searched for the files of one of the fixed model folders.
    /// </summary>
    public sealed class ModelSearchPath : BaseModel
    {
        private ModelFolderType _folder;
        private string _path;

        public ModelFolderType Folder
        {
            get { return _folder; }
            set { SetProperty(ref _folder, value); }
        }

        public string Path
        {
            get { return _path; }
            set { SetProperty(ref _path, value); }
        }


        /// <summary>
        /// Creates a copy of the search path.
        /// </summary>
        public ModelSearchPath DeepClone()
        {
            return new ModelSearchPath
            {
                Folder = Folder,
                Path = Path
            };
        }
    }
}
