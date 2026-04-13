using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Python.Common;

namespace Amuse.App
{
    public class SettingsManager
    {
        private readonly static SemaphoreSlim FileLock = new SemaphoreSlim(1, 1);

        public static Settings Load()
        {
            var appDefaultsFile = Path.Combine(App.DirectoryData, "Settings.default.json");
            var appDefaultsBakupFile = Path.Combine(App.DirectoryData, "Settings.backup.json");
            var appSettingsFile = Path.Combine(App.DirectoryData, "Settings.json");

            try
            {
                // Is New Install
                if (File.Exists(appDefaultsFile) && !File.Exists(appSettingsFile))
                {
                    File.Copy(appDefaultsFile, appSettingsFile);
                    return LoadSettingsFile(appSettingsFile);
                }

                // Is Update
                if (File.Exists(appDefaultsFile) && File.Exists(appSettingsFile))
                {
                    // merge
                    var appDefaults = LoadSettingsFile(appDefaultsFile);
                    var appSettings = LoadSettingsFile(appSettingsFile);
                    return MergeSettings(appSettingsFile, appSettings, appDefaults);
                }

                // Is Installed
                if (File.Exists(appSettingsFile))
                    return LoadSettingsFile(appSettingsFile);

                // Both files missing
                throw new FileNotFoundException(appSettingsFile);
            }
            catch
            {
                File.Copy(appDefaultsFile, appSettingsFile, true);
                return LoadSettingsFile(appSettingsFile);
            }
            finally
            {
                if (File.Exists(appDefaultsFile))
                    File.Move(appDefaultsFile, appDefaultsBakupFile, true);
            }
        }


        public static void Save(Settings settings)
        {
            FileLock.Wait();
            try
            {
                var tempJson = Path.Combine(App.DirectoryData, "Settings.temp");
                var settingsJson = Path.Combine(App.DirectoryData, "Settings.json");
                Json.Save(tempJson, settings);
                File.Move(tempJson, settingsJson, true);
            }
            finally
            {
                FileLock.Release();
            }
        }


        public static async Task SaveAsync(Settings settings)
        {
            await FileLock.WaitAsync();
            try
            {
                var tempJson = Path.Combine(App.DirectoryData, "Settings.temp");
                var settingsJson = Path.Combine(App.DirectoryData, "Settings.json");
                await Json.SaveAsync(tempJson, settings);
                File.Move(tempJson, settingsJson, true);
            }
            finally
            {
                FileLock.Release();
            }
        }


        private static Settings MergeSettings(string appSettingsFile, Settings currentSettings, Settings defaultSettings)
        {
            if (defaultSettings.Version != currentSettings.Version)
            {
                // No Update Path
                BackupFile(appSettingsFile);
                return defaultSettings;
            }

            // Map all user settings to new defaults
            var properties = typeof(Settings).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                var isDefaultValue = Attribute.IsDefined(property, typeof(AppDefaultAttribute));
                if (isDefaultValue)
                {
                    // Merge Templates
                    if (property.Name == nameof(defaultSettings.Environments))
                    {
                        foreach (var environment in currentSettings.Environments)
                        {
                            if (environment.Id > 1000)
                            {
                                // Add back any Environments the user has created
                                defaultSettings.Environments.Add(environment);
                            }
                            else
                            {
                                var defaultEnvironment = defaultSettings.Environments.FirstOrDefault(x => x.Id == environment.Id);
                                if (defaultEnvironment == null)
                                    continue;

                                if (defaultEnvironment.Version > environment.Version)
                                {
                                    defaultEnvironment.Status = int.IsEvenInteger(defaultEnvironment.Version)
                                        ? EnvironmentMode.Update
                                        : EnvironmentMode.Rebuild;
                                }

                                // Merge any user settings
                                foreach (var variables in environment.Variables)
                                {
                                    if (defaultEnvironment.Variables.ContainsKey(variables.Key))
                                        continue;

                                    // Add back any user environment variables
                                    defaultEnvironment.Variables.Add(variables.Key, variables.Value);
                                }
                            }
                        }

                    }
                    if (property.Name == nameof(defaultSettings.UpscaleModels))
                    {
                        foreach (var upscaleModel in currentSettings.UpscaleModels.Where(x => x.Id > 1000))
                        {
                            // Add back any upscale Model the user has created
                            defaultSettings.UpscaleModels.Add(upscaleModel);
                        }
                    }
                    if (property.Name == nameof(defaultSettings.DiffusionModels))
                    {
                        foreach (var diffusionModel in currentSettings.DiffusionModels)
                        {
                            if (diffusionModel.Id > 1000)
                            {
                                // Add back any diffusion Model the user has created
                                defaultSettings.DiffusionModels.Add(diffusionModel);
                            }
                            else
                            {
                                var defaultDiffusionModel = defaultSettings.DiffusionModels.FirstOrDefault(x => x.Id == diffusionModel.Id);
                                if (defaultDiffusionModel == null)
                                    continue;

                                // Merge any user settings
                                defaultDiffusionModel.Status = diffusionModel.Status;
                                defaultDiffusionModel.UserQualityMode = diffusionModel.UserQualityMode;
                                defaultDiffusionModel.UserMemoryMode = diffusionModel.UserMemoryMode;
                            }
                        }
                    }
                    if (property.Name == nameof(defaultSettings.LoraAdapterModels))
                    {
                        foreach (var loraAdapterModel in currentSettings.LoraAdapterModels.Where(x => x.Id > 1000))
                        {
                            // Add back any loraAdapter Model the user has created
                            defaultSettings.LoraAdapterModels.Add(loraAdapterModel);
                        }
                    }
                    if (property.Name == nameof(defaultSettings.ControlNetModels))
                    {
                        foreach (var controlNetModel in currentSettings.ControlNetModels.Where(x => x.Id > 1000))
                        {
                            // Add back any controlNet Model the user has created
                            defaultSettings.ControlNetModels.Add(controlNetModel);
                        }
                    }
                    if (property.Name == nameof(defaultSettings.ExtractModels))
                    {
                        foreach (var extractModel in currentSettings.ExtractModels.Where(x => x.Id > 1000))
                        {
                            // Add back any extract Model the user has created
                            defaultSettings.ExtractModels.Add(extractModel);
                        }

                    }
                    continue;
                }

                var defaultValue = property.GetValue(defaultSettings);
                var existingValue = property.GetValue(currentSettings);
                if (existingValue != null)
                    property.SetValue(defaultSettings, existingValue);
            }

            return defaultSettings;
        }


        private static Settings LoadSettingsFile(string filePath)
        {
            FileLock.Wait();
            try
            {
                return Json.Load<Settings>(filePath);
            }
            finally
            {
                FileLock.Release();
            }
        }


        private static void BackupFile(string filePath)
        {
            FileLock.Wait();
            try
            {
                File.Copy(filePath, filePath.Replace(".json", ".backup"), true);
            }
            catch { }
            finally
            {
                FileLock.Release();
            }
        }

    }

    [AttributeUsage(AttributeTargets.Property)]
    public class AppDefaultAttribute : Attribute { }
}
