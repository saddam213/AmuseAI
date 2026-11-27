// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using Amuse.App.Resources;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.WPF.Controls;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for ModelInformationDialog.xaml
    /// </summary>
    public partial class ModelInformationDialog : DialogControl
    {
        private IDownloadModel _downloadModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelInformationDialog"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        public ModelInformationDialog(Settings settings)
        {
            Settings = settings;
            Items = new ObservableCollection<ModelInformationItem>();
            InitializeComponent();
        }

        public Settings Settings { get; }
        public ObservableCollection<ModelInformationItem> Items { get; }

        public IDownloadModel DownloadModel
        {
            get { return _downloadModel; }
            set { SetProperty(ref _downloadModel, value); }
        }


        /// <summary>
        /// Shows the files of a model, each part with where it comes from and, for a shared component, whether it is installed.
        /// </summary>
        /// <param name="downloadModel">The model.</param>
        public Task<bool> ShowDialogAsync(IDownloadModel downloadModel)
        {
            DownloadModel = downloadModel;
            Items.Clear();
            foreach (var item in GetItems(downloadModel))
                Items.Add(item);

            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Gets the rows of a model, a heading per quality set when the model has several.
        /// </summary>
        /// <param name="model">The model.</param>
        private IEnumerable<ModelInformationItem> GetItems(IDownloadModel model)
        {
            if (model is IGenerateModel generateModel)
            {
                foreach (var checkpointSet in generateModel.GetCheckpoints())
                {
                    if (generateModel.HasCheckpointSets)
                        yield return new ModelInformationItem { Heading = checkpointSet.Type.GetDisplayName() };

                    foreach (var checkpoint in checkpointSet.GetComponents())
                        yield return CreateItem(checkpoint);
                }
                yield break;
            }

            IEnumerable<CheckpointComponent> checkpoints = model switch
            {
                UpscaleModel upscaleModel => upscaleModel.GetComponents(),
                LoraAdapterModel loraModel => [loraModel.Checkpoint],
                ControlNetModel controlNetModel => [controlNetModel.Checkpoint],
                ExtractModel extractModel => [extractModel.Checkpoint],
                ComponentModel componentModel => [componentModel.Checkpoint],
                _ => []
            };
            foreach (var checkpoint in checkpoints.Where(x => x != null))
                yield return CreateItem(checkpoint);
        }


        /// <summary>
        /// Creates the row of a model part, a shared component lists its own files and status.
        /// </summary>
        /// <param name="checkpoint">The model part.</param>
        private ModelInformationItem CreateItem(CheckpointComponent checkpoint)
        {
            if (checkpoint.Type == CheckpointType.Component)
            {
                var component = Settings.Components.FirstOrDefault(x => x.Key == checkpoint.Path);
                return new ModelInformationItem
                {
                    Name = checkpoint.Name,
                    Source = string.Format(AppDefault.ModelInfoSharedComponent, component?.Name ?? checkpoint.Path),
                    Status = component?.Status ?? ModelStatusType.Unknown,
                    Files = component is null ? [] : GetFiles(component.Checkpoint)
                };
            }

            return new ModelInformationItem
            {
                Name = checkpoint.Name,
                Source = checkpoint.Type == CheckpointType.OnlineFile || checkpoint.Type == CheckpointType.OnlineFolder
                    ? AppDefault.ModelInfoDownload
                    : AppDefault.ModelInfoLocal,
                Files = GetFiles(checkpoint)
            };
        }


        /// <summary>
        /// Gets the download links of an online model part, or the path of a local one.
        /// </summary>
        /// <param name="checkpoint">The model part.</param>
        private static string[] GetFiles(CheckpointComponent checkpoint)
        {
            if (checkpoint is null)
                return [];

            if (checkpoint.DownloadFiles is { Length: > 0 })
                return checkpoint.DownloadFiles;

            return string.IsNullOrEmpty(checkpoint.Path) ? [] : [checkpoint.Path];
        }
    }
}
