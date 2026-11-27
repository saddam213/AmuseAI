using Amuse.App.Common;
using Amuse.Common;
using System.Collections.Generic;

namespace Amuse.App.Views
{
    public enum View
    {
        Home = 0,
        SettingsGeneral = 50,
        SettingsEnvironment = 51,
        SettingsImage = 52,
        SettingsLoraAdapter = 53,
        SettingsControlNet = 54,
        SettingsExtract = 55,
        SettingsUpscale = 56,
        SettingsComponent = 58,
        SettingsLanguage = 59,
        SettingsVideo = 60,
        SettingsAudio = 61,
        SettingsTool = 62,

        TextToImage = 100,
        ImageToImage = 101,
        ImageEdit = 102,
        ImageInpaint = 103,
        PaintToImage = 104,

        ImageUpscale = 150,
        ImageExtract = 151,
        ImageCompose = 152,

        TextToVideo = 200,
        ImageToVideo = 201,
        VideoToVideo = 202,
        FrameToFrame = 203,
        MediaToVideo = 204,
        VideoUpscale = 250,
        VideoExtract = 251,
        VideoInterpolate = 252,
        VideoCompose = 253,

        TextToAudio = 300,
        AudioToText = 301,
        TextToMusic = 302,
        AudioToMusic = 303,
        VoiceClone = 304,
        AudioCompose = 350,

        TextInstruct = 400,
        TextConverse = 401,
        ImageToText = 410,

        Recent = 1000,
        Gallery = 1001,
        Models = 1002
    }

    public enum ViewCategory
    {
        Other = 0,
        Settings = 1,
        Image = 10,
        Video = 20,
        Audio = 30,
        Text = 40
    }

    public static class ViewManager
    {

        private static readonly Dictionary<ViewCategory, View> CurrentViewMap = new Dictionary<ViewCategory, View>
        {
            {ViewCategory.Other, View.Gallery },
            {ViewCategory.Settings, View.SettingsGeneral },
            {ViewCategory.Image, View.TextToImage },
            {ViewCategory.Video, View.TextToVideo },
            {ViewCategory.Audio, View.TextToAudio },
            {ViewCategory.Text, View.TextConverse }
        };


        private static readonly Dictionary<View, ViewCategory> ViewCategoryMap = new Dictionary<View, ViewCategory>
        {
            // General
            { View.Home, ViewCategory.Other  },
            { View.Gallery, ViewCategory.Other  },
            { View.Models, ViewCategory.Other  },

            // Settings
            { View.SettingsGeneral, ViewCategory.Settings  },
            { View.SettingsEnvironment, ViewCategory.Settings  },
            { View.SettingsImage, ViewCategory.Settings  },
            { View.SettingsVideo, ViewCategory.Settings  },
            { View.SettingsAudio, ViewCategory.Settings  },
            { View.SettingsLoraAdapter , ViewCategory.Settings  },
            { View.SettingsControlNet, ViewCategory.Settings  },
            { View.SettingsExtract , ViewCategory.Settings  },
            { View.SettingsUpscale , ViewCategory.Settings  },
            { View.SettingsComponent , ViewCategory.Settings  },
            { View.SettingsLanguage , ViewCategory.Settings  },
            { View.SettingsTool , ViewCategory.Settings  },

            // Image
            { View.TextToImage, ViewCategory.Image  },
            { View.ImageToImage, ViewCategory.Image  },
            { View.ImageEdit, ViewCategory.Image  },
            { View.ImageInpaint, ViewCategory.Image  },
            { View.PaintToImage, ViewCategory.Image  },
            { View.ImageExtract, ViewCategory.Image  },
            { View.ImageUpscale, ViewCategory.Image  },
            { View.ImageCompose, ViewCategory.Image  },

            // Video
            { View.TextToVideo, ViewCategory.Video  },
            { View.ImageToVideo, ViewCategory.Video  },
            { View.VideoToVideo, ViewCategory.Video  },
            { View.FrameToFrame, ViewCategory.Video  },
            { View.MediaToVideo, ViewCategory.Video  },
            { View.VideoExtract, ViewCategory.Video  },
            { View.VideoUpscale, ViewCategory.Video  },
            { View.VideoInterpolate, ViewCategory.Video },
            { View.VideoCompose, ViewCategory.Video  },

             // Audio
            { View.TextToMusic, ViewCategory.Audio  },
            { View.AudioToMusic, ViewCategory.Audio  },
            { View.TextToAudio, ViewCategory.Audio  },
            { View.VoiceClone, ViewCategory.Audio  },
            { View.AudioToText, ViewCategory.Audio  },
            { View.AudioCompose, ViewCategory.Audio  },

            // Text
            { View.TextInstruct, ViewCategory.Text  },
            { View.TextConverse, ViewCategory.Text  },
            { View.ImageToText, ViewCategory.Text  },
        };


        internal static View GetCurrentView(ViewCategory category)
        {
            return CurrentViewMap[category];
        }


        internal static ViewCategory SetCurrentView(View view)
        {
            var category = ViewCategoryMap[view];
            CurrentViewMap[category] = view;
            return category;
        }


        internal static ViewCategory GetViewCategory(View view)
        {
            return ViewCategoryMap[view];
        }


        /// <summary>
        /// Gets the view that runs the process type.
        /// </summary>
        /// <param name="processType">Type of the process.</param>
        internal static View GetView(ProcessType processType)
        {
            return processType switch
            {
                ProcessType.TextToImage => View.TextToImage,
                ProcessType.ImageToImage => View.ImageToImage,
                ProcessType.ImageEdit => View.ImageEdit,
                ProcessType.ImageInpaint => View.ImageInpaint,
                ProcessType.ImageControlNet => View.TextToImage,
                ProcessType.ImageToImageControlNet => View.ImageToImage,
                ProcessType.TextToVideo => View.TextToVideo,
                ProcessType.ImageToVideo => View.ImageToVideo,
                ProcessType.VideoToVideo => View.VideoToVideo,
                ProcessType.MediaToVideo => View.MediaToVideo,
                ProcessType.TextToAudio => View.TextToAudio,
                ProcessType.AudioToText => View.AudioToText,
                ProcessType.TextToText => View.TextInstruct,
                ProcessType.ImageToText => View.ImageToText,
                _ => View.Models
            };
        }


        /// <summary>
        /// Gets the view a model opens in, its first view filter or the view of its first process type.
        /// </summary>
        /// <param name="model">The model.</param>
        internal static View GetView(IGenerateModel model)
        {
            if (model.ViewFilter is { Length: > 0 })
                return model.ViewFilter[0];

            if (model.ProcessTypes is { Length: > 0 })
                return GetView(model.ProcessTypes[0]);

            return View.Models;
        }
    }
}
