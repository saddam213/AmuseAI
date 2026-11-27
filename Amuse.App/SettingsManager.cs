using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common;

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
            catch (Exception ex)
            {
                // Keep the user's settings before resetting to defaults
                Log.Logger.Error(ex, "[SettingsManager] [Load] Failed to load settings, resetting to defaults, Settings: {SettingsFile}", appSettingsFile);
                if (File.Exists(appSettingsFile))
                    BackupFile(appSettingsFile);

                var defaultsFile = File.Exists(appDefaultsFile) ? appDefaultsFile : appDefaultsBakupFile;
                File.Copy(defaultsFile, appSettingsFile, true);
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
                var isIgnoreValue = Attribute.IsDefined(property, typeof(JsonIgnoreAttribute));
                if (isIgnoreValue)
                    continue;

                var isDefaultValue = Attribute.IsDefined(property, typeof(AppDefaultAttribute));
                if (isDefaultValue)
                {

                    if (property.Name == nameof(defaultSettings.AccessTokens))
                    {
                        foreach (var existingToken in currentSettings.AccessTokens)
                        {
                            // Add back any Tokens the user has added
                            var defaultToken = defaultSettings.AccessTokens.FirstOrDefault(x => x.Name == existingToken.Name);
                            if (defaultToken != null)
                            {
                                defaultToken.Token = existingToken.Token;
                            }
                        }
                    }

                    // Merge Templates
                    if (property.Name == nameof(defaultSettings.Environments))
                    {
                        foreach (var environment in currentSettings.Environments)
                        {
                            if (environment.Id > 1000)
                            {
                                if (environment.Id == 2000)
                                    continue;

                                // Add back any Environments the user has created
                                if (!defaultSettings.Environments.Any(x => x.Id == environment.Id))
                                    defaultSettings.Environments.Add(environment);
                            }
                            else
                            {
                                var defaultEnvironment = defaultSettings.Environments.FirstOrDefault(x => x.Id == environment.Id);
                                if (defaultEnvironment == null)
                                    continue;

                                if (defaultEnvironment.Version != environment.Version)
                                {
                                    defaultEnvironment.Status = BackendStatus.Update;
                                }
                                else if (environment.Status == BackendStatus.Update)
                                {
                                    defaultEnvironment.Status = environment.Status;
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

                        // Keep the user's default environments, the environment chosen and installed may not be the shipped default
                        foreach (var userDefault in currentSettings.Environments.Where(x => x.IsDefault))
                        {
                            var defaultEnvironment = defaultSettings.Environments.FirstOrDefault(x => x.Id == userDefault.Id);
                            if (defaultEnvironment == null)
                                continue;

                            foreach (var other in defaultSettings.Environments.Where(defaultEnvironment.IsSameDefaultGroup))
                                other.IsDefault = false;

                            defaultEnvironment.IsDefault = true;
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
                                if (diffusionModel.Backend == TensorStack.Common.BackendType.OnnxRuntime)
                                    continue;

                                if (!defaultSettings.DiffusionModels.Any(x => x.Id == diffusionModel.Id))
                                    defaultSettings.DiffusionModels.Add(diffusionModel);
                            }
                            else
                            {
                                var defaultDiffusionModel = defaultSettings.DiffusionModels.FirstOrDefault(x => x.Id == diffusionModel.Id);
                                if (defaultDiffusionModel == null)
                                    continue;

                                // Merge any user settings
                                defaultDiffusionModel.Status = diffusionModel.Status;
                                defaultDiffusionModel.UserMemoryMode = diffusionModel.UserMemoryMode;
                                defaultDiffusionModel.UserExecutionOptions = diffusionModel.UserExecutionOptions;
                                defaultDiffusionModel.CurrentCheckpointType = diffusionModel.CurrentCheckpointType;
                            }
                        }
                    }
                    if (property.Name == nameof(defaultSettings.LanguageModels))
                    {
                        foreach (var languageModel in currentSettings.LanguageModels)
                        {
                            if (languageModel.Id > 1000)
                            {
                                if (!defaultSettings.LanguageModels.Any(x => x.Id == languageModel.Id))
                                    defaultSettings.LanguageModels.Add(languageModel);
                            }
                            else
                            {
                                var defaultLanguageModel = defaultSettings.LanguageModels.FirstOrDefault(x => x.Id == languageModel.Id);
                                if (defaultLanguageModel == null)
                                    continue;

                                // Merge any user settings
                                defaultLanguageModel.Status = languageModel.Status;
                                defaultLanguageModel.UserMemoryMode = languageModel.UserMemoryMode;
                                defaultLanguageModel.UserExecutionOptions = languageModel.UserExecutionOptions;
                                defaultLanguageModel.CurrentCheckpointType = languageModel.CurrentCheckpointType;
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
                            if (!defaultSettings.ControlNetModels.Any(x => x.Id == controlNetModel.Id))
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
                return Json.Load<Settings>(filePath)
                    ?? throw new InvalidDataException($"Settings file could not be read, File: {filePath}");
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
