using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Serilog;
using TensorStack.Common.Common;

namespace Amuse.App
{
    public static class Json
    {
        public readonly static JsonSerializerOptions DefaultOptions;

        static Json()
        {
            DefaultOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            };
        }


        public static T Load<T>(string filePath) where T : class
        {
            try
            {
                using (var jsonReader = File.OpenRead(filePath))
                {
                    return JsonSerializer.Deserialize<T>(jsonReader, DefaultOptions);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "[Json] [Load] An exception occurred loading JSON file: {filePath}", filePath);
                return default;
            }
        }


        public static async Task<T> LoadAsync<T>(string filePath) where T : class
        {
            try
            {
                using (var jsonReader = File.OpenRead(filePath))
                {
                    return await JsonSerializer.DeserializeAsync<T>(jsonReader, DefaultOptions);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "[Json] [LoadAsync] An exception occurred loading JSON file: {filePath}", filePath);
                return default;
            }
        }


        public static void Save<T>(string filePath, T obj)
        {
            try
            {
                using (var jsonWriter = File.OpenWrite(filePath))
                {
                    JsonSerializer.Serialize<T>(jsonWriter, obj, DefaultOptions);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "[Json] [Save] An exception occurred saving JSON file: {filePath}", filePath);
            }
        }


        public static async Task SaveAsync<T>(string filePath, T obj)
        {
            var temp = filePath + ".tmp";
            try
            {
                await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await JsonSerializer.SerializeAsync(stream, obj, DefaultOptions);
                }
                File.Move(temp, filePath, overwrite: true);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "[Json] [SaveAsync] An exception occurred saving JSON file: {filePath}", filePath);
            }
            finally
            {
                FileHelper.DeleteFile(temp);
            }
        }
    }
}
