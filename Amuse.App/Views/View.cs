using System.Collections.Generic;

namespace Amuse.App.Views
{
    public enum View
    {
        Home = 0,
        General = 50,
        Environment = 51,
        Diffusion = 52,
        LoraAdapter = 53,
        ControlNet = 54,
        Extract = 55,
        Upscale = 56,
        Downloads = 57,

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
        VideoUpscale = 250,
        VideoExtract = 251,
        VideoInterpolate = 252,

        TextToAudio = 300,
        AudioToText = 301,

        History = 1000,
        Gallery = 1001
    }

    public enum ViewCategory
    {
        Other = 0,
        Settings = 1,
        Image = 10,
        Video = 20,
        Audio = 30
    }

    public static class ViewManager
    {

        private static readonly Dictionary<ViewCategory, View> CurrentViewMap = new Dictionary<ViewCategory, View>
        {
            {ViewCategory.Other, View.Gallery },
            {ViewCategory.Settings, View.General },
          //  {ViewCategory.Text, View.TextSummary },
            {ViewCategory.Image, View.TextToImage },
            {ViewCategory.Video, View.TextToVideo },
            {ViewCategory.Audio, View.TextToAudio }
        };


        private static readonly Dictionary<View, ViewCategory> ViewCategoryMap = new Dictionary<View, ViewCategory>
        {
            // General
            { View.Gallery, ViewCategory.Other  },

            // Settings
            { View.General, ViewCategory.Settings  },
            { View.Environment, ViewCategory.Settings  },
            { View.Diffusion, ViewCategory.Settings  },
            { View.LoraAdapter , ViewCategory.Settings  },
            { View.ControlNet, ViewCategory.Settings  },
            { View.Extract , ViewCategory.Settings  },
            { View.Upscale , ViewCategory.Settings  },
            { View.Downloads , ViewCategory.Settings  },

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
            { View.VideoExtract, ViewCategory.Video  },
            { View.VideoUpscale, ViewCategory.Video  },
            { View.VideoInterpolate, ViewCategory.Video  },

             // Audio
            { View.TextToAudio, ViewCategory.Audio  },
            { View.AudioToText, ViewCategory.Audio  },
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
    }
}
