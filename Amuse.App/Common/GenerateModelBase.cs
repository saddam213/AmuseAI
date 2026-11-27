using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using TensorStack.Common;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    /// <summary>
    /// The checkpoint sets, current checkpoint set and install status shared by the generate models.
    /// </summary>
    /// <typeparam name="TCheckpoint">The checkpoint set type.</typeparam>
    public abstract class GenerateModelBase<TCheckpoint> : BaseModel where TCheckpoint : class, ICheckpointModel
    {
        private List<TCheckpoint> _checkpoints = [];
        private ModelStatusType _status;
        private DataType? _currentCheckpointType;
        private ModelMetadata _metadata;

        public List<TCheckpoint> Checkpoints
        {
            get { return _checkpoints; }
            set
            {
                SetProperty(ref _checkpoints, value ?? []);
                NotifyCheckpointChanged();
            }
        }

        [JsonIgnore]
        public TCheckpoint Checkpoint
        {
            get { return GetCheckpoint(null); }
        }

        [JsonIgnore]
        public bool HasCheckpointSets
        {
            get { return _checkpoints.Count > 1; }
        }

        [JsonIgnore]
        public float MemorySize
        {
            get { return GetMemorySize(null); }
        }

        public ModelStatusType Status
        {
            get { return _status; }
            set { SetProperty(ref _status, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DataType? CurrentCheckpointType
        {
            get { return _currentCheckpointType; }
            set
            {
                SetProperty(ref _currentCheckpointType, value);
                NotifyCheckpointChanged();
            }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ModelMetadata Metadata
        {
            get { return _metadata; }
            set { SetProperty(ref _metadata, value); }
        }


        /// <summary>
        /// Gets the directory the checkpoint sets are installed in.
        /// </summary>
        /// <param name="settings">The settings.</param>
        public abstract string GetDirectory(Settings settings);


        /// <summary>
        /// Refreshes the metadata, the installed state of every checkpoint set and the status, kept as is while a download is active.
        /// </summary>
        /// <param name="settings">The settings.</param>
        public void Initialize(Settings settings)
        {
            _metadata?.Initialize(settings);
            foreach (var checkpoint in _checkpoints)
                checkpoint.CheckInstall(settings, GetDirectory(settings));

            if (_status.IsDownloadActive())
                return;

            ResetCurrentCheckpoint(settings);
            Status = GetModelStatus(settings);
        }


        /// <summary>
        /// Switches to an installed checkpoint set when the current set is not installed, e.g. after a cancelled download.
        /// </summary>
        /// <param name="settings">The settings.</param>
        public void ResetCurrentCheckpoint(Settings settings)
        {
            if (IsCheckpointInstalled(null, settings))
                return;

            var installed = _checkpoints.FirstOrDefault(x => x.CheckInstall(settings, GetDirectory(settings)));
            if (installed is not null)
                CurrentCheckpointType = installed.Type;
        }


        /// <summary>
        /// Deletes the files of every checkpoint set.
        /// </summary>
        /// <param name="settings">The settings.</param>
        public void Delete(Settings settings)
        {
            var directory = GetDirectory(settings);
            foreach (var checkpoint in _checkpoints)
            {
                foreach (var component in checkpoint.GetComponents())
                {
                    if (component.Type == CheckpointType.Component)
                        continue;

                    component.DeleteFiles(settings, directory);
                }
                checkpoint.CheckInstall(settings, directory);
            }
        }


        /// <summary>
        /// Gets the checkpoint set of the given type, the current set when no type is given.
        /// </summary>
        /// <param name="type">The checkpoint set type.</param>
        public TCheckpoint GetCheckpoint(DataType? type)
        {
            var checkpointType = type ?? _currentCheckpointType;
            if (checkpointType.HasValue)
            {
                var checkpoint = _checkpoints.FirstOrDefault(x => x.Type == checkpointType.Value);
                if (checkpoint is not null)
                    return checkpoint;
            }
            return _checkpoints.FirstOrDefault();
        }


        /// <summary>
        /// Gets the checkpoint sets.
        /// </summary>
        public IReadOnlyList<ICheckpointModel> GetCheckpoints()
        {
            return _checkpoints;
        }


        /// <summary>
        /// Gets the components of the current checkpoint set.
        /// </summary>
        public IEnumerable<CheckpointComponent> GetComponents()
        {
            return Checkpoint?.GetComponents() ?? [];
        }


        /// <summary>
        /// Gets the memory size in GB of the given checkpoint set, the current set when no type is given.
        /// </summary>
        /// <param name="type">The checkpoint set type.</param>
        public float GetMemorySize(DataType? type)
        {
            var checkpoint = GetCheckpoint(type);
            if (checkpoint is null)
                return 0;

            return checkpoint.MemorySize;
        }


        /// <summary>
        /// Determines whether the files of the given checkpoint set are installed, the current set when no type is given.
        /// Also updates the checkpoint set <see cref="ICheckpointModel.IsInstalled"/> flag.
        /// </summary>
        /// <param name="type">The checkpoint set type.</param>
        /// <param name="settings">The settings.</param>
        public bool IsCheckpointInstalled(DataType? type, Settings settings)
        {
            var checkpoint = GetCheckpoint(type);
            if (checkpoint is null)
                return false;

            return checkpoint.CheckInstall(settings, GetDirectory(settings));
        }


        /// <summary>
        /// Sets the current checkpoint set and updates the status from its installed files.
        /// </summary>
        /// <param name="type">The checkpoint set type.</param>
        /// <param name="settings">The settings.</param>
        public void SetCurrentCheckpoint(DataType? type, Settings settings)
        {
            if (_checkpoints.All(x => x.Type != type))
                return;

            CurrentCheckpointType = type;
            Status = IsCheckpointInstalled(null, settings)
                ? ModelStatusType.Installed
                : ModelStatusType.Available;
        }


        /// <summary>
        /// Gets the status from the installed files of the current checkpoint set.
        /// </summary>
        /// <param name="settings">The settings.</param>
        private ModelStatusType GetModelStatus(Settings settings)
        {
            if (Checkpoint == null)
                return ModelStatusType.Available;

            var isValid = IsCheckpointInstalled(null, settings);
            if ((_status == ModelStatusType.Available || _status == ModelStatusType.Unknown) && isValid)
                return ModelStatusType.Installed;
            else if (_status == ModelStatusType.Installed && !isValid)
                return ModelStatusType.Unknown;

            return _status;
        }


        /// <summary>
        /// Notifies the properties computed from the current checkpoint set.
        /// </summary>
        private void NotifyCheckpointChanged()
        {
            NotifyPropertyChanged(nameof(Checkpoint));
            NotifyPropertyChanged(nameof(HasCheckpointSets));
            NotifyPropertyChanged(nameof(MemorySize));
        }
    }
}
